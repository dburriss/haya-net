// Entry point for the Bun build of the Haya CLI: the TypeScript counterpart of `Haya.Tool` (src/Haya.Tool/Cli.fs).
// Rendering is F# from Haya.Core compiled to TypeScript by Fable into ./fable_output (see README.md);
// analysis is the TypeScript analyser in ./analyser.
import { existsSync } from "node:fs";
import yargs from "yargs";
import { hideBin } from "yargs/helpers";
import { analyse, AnalysisError } from "./analyser/analyser.ts";
import { toFableApp } from "./analyser/bridge.ts";
import type { App } from "./fable_output/Haya.Core/Model.ts";
import { ofArray } from "./fable_output/fable_modules/fable-library-ts.5.27.0/List.ts";
import {
  BackstageCommand,
  CrcCommand,
  DataFormat_Json,
  DataFormat_Yaml,
  DatadogCommand,
  DescribeCommand,
  DiagramCommand,
  DiagramFormat,
  DiagramType,
} from "./fable_output/Haya.Core/Types.ts";
import { IO_write, IO_readAllText } from "./fable_output/Haya.Core/IO.ts";
import { apps as decodeApps, isDescribeFile } from "./fable_output/Haya.Core/Decode.ts";
import { apps as mergeApps } from "./fable_output/Haya.Core/Merge.ts";
import { serialize } from "./fable_output/Haya.Core/Describe.ts";
import { sprintMarkdown } from "./fable_output/Haya.Core/Crc.ts";
import { generateDiagram } from "./fable_output/Haya.Core/Diagram.ts";
import { generateCatalog } from "./fable_output/Haya.Core/Backstage.ts";
import { generateCatalog as generateDatadogCatalog } from "./fable_output/Haya.Core/Datadog.ts";

type Format = "json" | "yaml";

const dataFormat = (f: Format) => (f === "yaml" ? DataFormat_Yaml() : DataFormat_Json());
const defaultFile = (format: Format, name: string) => `./${name}.${format}`;

/** The apps of one input: a haya describe file (json/yaml) or an analysed tsconfig.json / directory (one app). */
const loadOne = (inputPath: string): App[] => {
  if (isDescribeFile(inputPath)) {
    if (!existsSync(inputPath)) throw new Error(`File not found: ${inputPath}`);
    const decoded = decodeApps(inputPath, IO_readAllText(inputPath));
    if (decoded.tag === 1) throw new Error(decoded.fields[0]);
    return Array.from(decoded.fields[0]);
  }
  return [toFableApp(analyse(inputPath, process.cwd()))];
};

/** Loads every input and merges them into one system; conflicts between inputs are warned about on stderr. */
const load = (inputPaths: string[]) => {
  const [merged, warnings] = mergeApps(ofArray(inputPaths.flatMap(loadOne)));
  for (const w of warnings) console.error(`Warning: ${w}`);
  return merged;
};

/** Load, render and write; prints the success message like `Haya.Tool` or an error with exit code 1. */
const exec = (label: string, inputPaths: string[], outputPath: string, render: (apps: ReturnType<typeof load>) => string) => {
  try {
    const apps = load(inputPaths);
    const result = IO_write(outputPath, render(apps));
    if (result.tag === 0) console.info(`${label} successfully generated from ${inputPaths.join(", ")}`);
    else {
      console.error(`Error: ${result.fields[0]}`);
      process.exitCode = 1;
    }
  } catch (e) {
    console.error(e instanceof AnalysisError ? `Error: ${e.message}` : `Error: ${e instanceof Error ? e.message : String(e)}`);
    process.exitCode = 1;
  }
};

const inputPath = (y: ReturnType<typeof yargs>) =>
  y.positional("inputPaths", {
    type: "string",
    array: true,
    demandOption: true,
    describe: "Path(s) to a tsconfig.json, a directory containing one, or a haya describe file (json/yaml). Several build one system",
  });

await yargs(hideBin(Bun.argv))
  .scriptName("haya")
  .command(
    "crc <inputPaths..>",
    "Generate Components, Responsibilities, and Collaborator documentation",
    (y) =>
      inputPath(y)
        .option("output-path", { alias: "o", type: "string", default: "./CRC.md", describe: "Path to output folder or file" })
        .option("c4", { alias: "c", type: "count", describe: "Include diagram (markdown only). -c for C4 Level 1, -cc for C4 Level 2" }),
    (argv) => {
      const cmd = new CrcCommand(ofArray(argv.inputPaths), argv["output-path"], argv.c4 > 0, argv.c4 > 1, process.cwd());
      exec("CRC", Array.from(cmd.InputPaths), cmd.OutputPath, (ds) => sprintMarkdown(cmd, ds));
    },
  )
  .command(
    "describe <inputPaths..>",
    "Generate Haya solution description",
    (y) =>
      inputPath(y)
        .option("output-path", { alias: "o", type: "string", describe: "Path to output folder or file (default: ./haya.json)" })
        .option("format", { alias: "f", choices: ["json", "yaml"] as const, default: "json" as const, describe: "Output format" }),
    (argv) => {
      const out = argv["output-path"] ?? defaultFile(argv.format, "haya");
      const cmd = new DescribeCommand(ofArray(argv.inputPaths), out, dataFormat(argv.format), process.cwd());
      exec("Describe", Array.from(cmd.InputPaths), cmd.OutputPath, (ds) => serialize(cmd, ds));
    },
  )
  .command(
    "backstage <inputPaths..>",
    "Generate Backstage catalog data",
    (y) =>
      inputPath(y)
        .option("output-path", { alias: "o", type: "string", describe: "Path to output folder or file (default: ./backstage.yaml)" })
        .option("format", { alias: "f", choices: ["json", "yaml"] as const, default: "yaml" as const, describe: "Output format" }),
    (argv) => {
      const out = argv["output-path"] ?? defaultFile(argv.format, "backstage");
      const cmd = new BackstageCommand(ofArray(argv.inputPaths), out, dataFormat(argv.format), process.cwd());
      exec("Backstage", Array.from(cmd.InputPaths), cmd.OutputPath, (ds) => generateCatalog(cmd, ds));
    },
  )
  .command(
    "datadog <inputPaths..>",
    "Generate Datadog Software Catalog entities (entity.datadog.yaml)",
    (y) =>
      inputPath(y)
        .option("output-path", { alias: "o", type: "string", describe: "Path to output folder or file (default: ./entity.datadog.yaml)" })
        .option("format", { alias: "f", choices: ["json", "yaml"] as const, default: "yaml" as const, describe: "Output format" }),
    (argv) => {
      const out = argv["output-path"] ?? defaultFile(argv.format, "entity.datadog");
      const cmd = new DatadogCommand(ofArray(argv.inputPaths), out, dataFormat(argv.format), process.cwd());
      exec("Datadog", Array.from(cmd.InputPaths), cmd.OutputPath, (ds) => generateDatadogCatalog(cmd, ds));
    },
  )
  .command(
    "diagram <inputPaths..>",
    "Generate diagram files",
    (y) =>
      inputPath(y)
        .option("output-path", { alias: "o", type: "string", default: "./c4-system-context.mermaid", describe: "Path to output folder or file" })
        .option("diagram-type", { alias: "t", choices: ["c4system", "c4container"] as const, default: "c4system" as const, describe: "Diagram type" })
        .option("diagram-format", { alias: "f", choices: ["mermaid"] as const, default: "mermaid" as const, describe: "Diagram format" }),
    (argv) => {
      const cmd = new DiagramCommand(ofArray(argv.inputPaths), argv["output-path"], argv["diagram-type"] === "c4container" ? DiagramType.C4Container : DiagramType.C4System, DiagramFormat.Mermaid, process.cwd());
      exec("Diagram", Array.from(cmd.InputPaths), cmd.OutputPath, (ds) => generateDiagram(cmd, ds));
    },
  )
  .strictCommands()
  .demandCommand(1)
  .help()
  .parseAsync();
