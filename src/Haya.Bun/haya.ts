// Entry point for the Bun build of the Haya CLI.
// F# code from Haya.Core is compiled to TypeScript by Fable into ./fable_output (see README.md).
import yargs from "yargs";
import { hideBin } from "yargs/helpers";
import {
  DataFormat_Json,
  DataFormat_Yaml,
  DescribeCommand,
} from "./fable_output/Haya.Core/Types.ts";
import { IO_write } from "./fable_output/Haya.Core/IO.ts";
import { toString } from "./fable_output/fable_modules/fable-library-ts.5.27.0/Types.ts";

const notImplemented = (name: string) => () => {
  console.error(`'${name}' is not implemented in the Bun build yet.`);
  process.exitCode = 1;
};

await yargs(hideBin(Bun.argv))
  .scriptName("haya")
  .command(
    "crc <inputPath>",
    "Generate Components, Responsibilities, and Collaborator documentation",
    (y) => y.positional("inputPath", { type: "string", demandOption: true }),
    notImplemented("crc"),
  )
  .command(
    "describe <inputPath>",
    "Generate Haya solution description",
    (y) =>
      y
        .positional("inputPath", { type: "string", demandOption: true })
        .option("outputpath", { alias: "o", type: "string", default: "./haya.json" })
        .option("format", { alias: "f", choices: ["json", "yaml"] as const, default: "json" as const }),
    (argv) => {
      const format = argv.format === "yaml" ? DataFormat_Yaml() : DataFormat_Json();
      const cmd = new DescribeCommand(argv.inputPath, argv.outputpath, format, process.cwd());
      // Placeholder until Describe is ported: round-trips through Fable-compiled F# (types + IO).
      const result = IO_write(cmd.OutputPath, toString(cmd) + "\n");
      console.info(`Wrote ${cmd.OutputPath}`, result.tag === 0 ? "(ok)" : "(error)");
    },
  )
  .command(
    "backstage <inputPath>",
    "Generate Backstage catalog data",
    (y) => y.positional("inputPath", { type: "string", demandOption: true }),
    notImplemented("backstage"),
  )
  .command(
    "diagram <inputPath>",
    "Generate diagram files",
    (y) => y.positional("inputPath", { type: "string", demandOption: true }),
    notImplemented("diagram"),
  )
  .strictCommands()
  .demandCommand(1)
  .help()
  .parseAsync();
