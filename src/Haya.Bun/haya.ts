// Entry point for the Bun build of the Haya CLI: the TypeScript counterpart of `Haya.Tool` (src/Haya.Tool/Cli.fs).
// Rendering is F# from Haya.Core compiled to TypeScript by Fable into ./fable_output (see README.md);
// analysis is the TypeScript analyser in ./analyser.
import { existsSync } from "node:fs";
import yargs from "yargs";
import { hideBin } from "yargs/helpers";
import { analyse, analyseWithMembers, AnalysisError } from "./analyser/analyser.ts";
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
  Detail,
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

const details = { systems: Detail.Systems, apps: Detail.Apps, components: Detail.Components, members: Detail.Members } as const;
// Same text as CliText.detailUsage in src/Haya.Tool/Cli.fs
const detailUsage =
  "Level of detail. systems (systems and how they connect), apps (the apps inside their systems), components (the classes inside the apps) or members (also each class's constructors, public members and dependencies). With -f html, components adds the classes to the explorer and members their constructors, members and dependencies; in the Mermaid diagram, members also draws the dependencies between classes (default: systems)";
// Same text as CliText.describeDetailUsage in src/Haya.Tool/Cli.fs
const describeDetailUsage =
  "Level of detail. apps (responsibilities, collaborators and meta), components (the same as apps today: classes are already named on each responsibility and collaborator) or members (adds each annotated class's constructors, public members and dependencies) (default: apps)";
const dataFormat = (f: Format) => (f === "yaml" ? DataFormat_Yaml() : DataFormat_Json());
const defaultFile = (format: Format, name: string) => `./${name}.${format}`;

/**
 * The apps of one input: a haya describe file (json/yaml) or an analysed tsconfig.json / directory (one app). With
 * `members`, the analyser also collects each annotated class's constructors and public members.
 */
const loadOne = (inputPath: string, members: boolean): App[] => {
  if (isDescribeFile(inputPath)) {
    if (!existsSync(inputPath)) throw new Error(`File not found: ${inputPath}`);
    const decoded = decodeApps(inputPath, IO_readAllText(inputPath));
    if (decoded.tag === 1) throw new Error(decoded.fields[0]);
    return Array.from(decoded.fields[0]);
  }
  if (!members) return [toFableApp(analyse(inputPath, process.cwd()))];
  const analysis = analyseWithMembers(inputPath, process.cwd());
  return [toFableApp(analysis.descriptors, analysis.components, analysis.typeGraph)];
};

/** Loads every input and merges them into one system; conflicts between inputs are warned about on stderr. */
const load = (inputPaths: string[], members = false) => {
  const [merged, warnings] = mergeApps(ofArray(inputPaths.flatMap((p) => loadOne(p, members))));
  for (const w of warnings) console.error(`Warning: ${w}`);
  return merged;
};

/** Load, render and write; prints the success message like `Haya.Tool` or an error with exit code 1. */
const exec = (label: string, inputPaths: string[], outputPath: string, render: (apps: ReturnType<typeof load>) => string, members = false) => {
  try {
    const apps = load(inputPaths, members);
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
        .option("format", { alias: "f", choices: ["json", "yaml"] as const, default: "json" as const, describe: "Output format" })
        .option("detail", { alias: "d", choices: ["apps", "components", "members"] as const, default: "apps" as const, describe: describeDetailUsage }),
    (argv) => {
      const out = argv["output-path"] ?? defaultFile(argv.format, "haya");
      const cmd = new DescribeCommand(ofArray(argv.inputPaths), out, dataFormat(argv.format), details[argv.detail], process.cwd());
      exec("Describe", Array.from(cmd.InputPaths), cmd.OutputPath, (ds) => serialize(cmd, ds), argv.detail === "members");
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
        .option("output-path", { alias: "o", type: "string", describe: "Path to output folder or file (default: ./c4-system-context.mermaid, or ./diagram.html for -f html)" })
        .option("detail", { alias: "d", choices: ["systems", "apps", "components", "members"] as const, default: "systems" as const, describe: detailUsage })
        .option("diagram-format", { alias: "f", choices: ["mermaid", "html"] as const, default: "mermaid" as const, describe: "Diagram format. html is an interactive explorer (a self-contained page)" }),
    (argv) => {
      const html = argv["diagram-format"] === "html";
      const out = argv["output-path"] ?? (html ? "./diagram.html" : "./c4-system-context.mermaid");
      const cmd = new DiagramCommand(ofArray(argv.inputPaths), out, details[argv.detail], html ? DiagramFormat.Html : DiagramFormat.Mermaid, process.cwd());
      exec("Diagram", Array.from(cmd.InputPaths), cmd.OutputPath, (ds) => generateDiagram(cmd, ds), argv.detail === "members");
    },
  )
  .strictCommands()
  .demandCommand(1)
  .help()
  .parseAsync();
