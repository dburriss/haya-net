// Entry point for the Bun build of the Haya CLI: the TypeScript counterpart of `Haya.Tool` (src/Haya.Tool/Cli.fs).
// Rendering is F# from Haya.Core compiled to TypeScript by Fable into ./fable_output (see README.md);
// analysis is the TypeScript analyser in ./analyser.
import { existsSync } from "node:fs";
import yargs from "yargs";
import { hideBin } from "yargs/helpers";
import { analyse, AnalysisError } from "./analyser/analyser.ts";
import { toFableDescriptors } from "./analyser/bridge.ts";
import {
  BackstageCommand,
  CrcCommand,
  DataFormat_Json,
  DataFormat_Yaml,
  DescribeCommand,
  DiagramCommand,
  DiagramFormat,
  DiagramType,
} from "./fable_output/Haya.Core/Types.ts";
import { IO_write, IO_readAllText } from "./fable_output/Haya.Core/IO.ts";
import { descriptors as decodeDescriptors, isDescribeFile } from "./fable_output/Haya.Core/Decode.ts";
import { serialize } from "./fable_output/Haya.Core/Describe.ts";
import { sprintMarkdown } from "./fable_output/Haya.Core/Crc.ts";
import { generateDiagram } from "./fable_output/Haya.Core/Diagram.ts";
import { generateCatalog } from "./fable_output/Haya.Core/Backstage.ts";

type Format = "json" | "yaml";

const dataFormat = (f: Format) => (f === "yaml" ? DataFormat_Yaml() : DataFormat_Json());
const defaultFile = (format: Format, name: string) => `./${name}.${format}`;

/** Descriptors from a haya describe file (json/yaml) or by analysing a tsconfig.json or directory. */
const load = (inputPath: string) => {
  if (isDescribeFile(inputPath)) {
    if (!existsSync(inputPath)) throw new Error(`File not found: ${inputPath}`);
    const decoded = decodeDescriptors(inputPath, IO_readAllText(inputPath));
    if (decoded.tag === 1) throw new Error(decoded.fields[0]);
    return decoded.fields[0];
  }
  return toFableDescriptors(analyse(inputPath, process.cwd()));
};

/** Load, render and write; prints the success message like `Haya.Tool` or an error with exit code 1. */
const exec = (label: string, inputPath: string, outputPath: string, render: (descriptors: ReturnType<typeof toFableDescriptors>) => string) => {
  try {
    const descriptors = load(inputPath);
    const result = IO_write(outputPath, render(descriptors));
    if (result.tag === 0) console.info(`${label} successfully generated from ${inputPath}`);
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
  y.positional("inputPath", { type: "string", demandOption: true, describe: "Path to tsconfig.json, a directory containing one, or a haya describe file (json/yaml)" });

await yargs(hideBin(Bun.argv))
  .scriptName("haya")
  .command(
    "crc <inputPath>",
    "Generate Components, Responsibilities, and Collaborator documentation",
    (y) =>
      inputPath(y)
        .option("output-path", { alias: "o", type: "string", default: "./CRC.md", describe: "Path to output folder or file" })
        .option("c4", { alias: "c", type: "count", describe: "Include diagram (markdown only). -c for C4 Level 1, -cc for C4 Level 2" }),
    (argv) => {
      const cmd = new CrcCommand(argv.inputPath, argv["output-path"], argv.c4 > 0, argv.c4 > 1, process.cwd());
      exec("CRC", cmd.PathToSln, cmd.OutputPath, (ds) => sprintMarkdown(cmd, ds));
    },
  )
  .command(
    "describe <inputPath>",
    "Generate Haya solution description",
    (y) =>
      inputPath(y)
        .option("output-path", { alias: "o", type: "string", describe: "Path to output folder or file (default: ./haya.json)" })
        .option("format", { alias: "f", choices: ["json", "yaml"] as const, default: "json" as const, describe: "Output format" }),
    (argv) => {
      const out = argv["output-path"] ?? defaultFile(argv.format, "haya");
      const cmd = new DescribeCommand(argv.inputPath, out, dataFormat(argv.format), process.cwd());
      exec("Describe", cmd.PathToSln, cmd.OutputPath, (ds) => serialize(cmd, ds));
    },
  )
  .command(
    "backstage <inputPath>",
    "Generate Backstage catalog data",
    (y) =>
      inputPath(y)
        .option("output-path", { alias: "o", type: "string", describe: "Path to output folder or file (default: ./backstage.yaml)" })
        .option("format", { alias: "f", choices: ["json", "yaml"] as const, default: "yaml" as const, describe: "Output format" }),
    (argv) => {
      const out = argv["output-path"] ?? defaultFile(argv.format, "backstage");
      const cmd = new BackstageCommand(argv.inputPath, out, dataFormat(argv.format), process.cwd());
      exec("Backstage", cmd.PathToSln, cmd.OutputPath, (ds) => generateCatalog(cmd, ds));
    },
  )
  .command(
    "diagram <inputPath>",
    "Generate diagram files",
    (y) =>
      inputPath(y)
        .option("output-path", { alias: "o", type: "string", default: "./c4-system-context.mermaid", describe: "Path to output folder or file" })
        .option("diagram-type", { alias: "t", choices: ["c4system"] as const, default: "c4system" as const, describe: "Diagram type" })
        .option("diagram-format", { alias: "f", choices: ["mermaid"] as const, default: "mermaid" as const, describe: "Diagram format" }),
    (argv) => {
      const cmd = new DiagramCommand(argv.inputPath, argv["output-path"], DiagramType.C4System, DiagramFormat.Mermaid, process.cwd());
      exec("Diagram", cmd.PathToSln, cmd.OutputPath, (ds) => generateDiagram(cmd, ds));
    },
  )
  .strictCommands()
  .demandCommand(1)
  .help()
  .parseAsync();
