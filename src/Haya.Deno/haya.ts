import yargs from "https://deno.land/x/yargs@v17.7.2-deno/deno.ts";
import { Arguments } from "https://deno.land/x/yargs@v17.7.2-deno/deno-types.ts";

/*
USAGE: haya [--help] [<subcommand> [<options>]]

SUBCOMMANDS:

    crc <options>         Generate Components, Responsibilities, and Collaborator documentation
    describe <options>    Generate Haya solution description
    backstage <options>   Generate Backstage catalog data
    diagram <options>     Generate diagram files

    Use 'haya <subcommand> --help' for additional information.

OPTIONS:

    --help                display this list of options.
*/

yargs(Deno.args)
  .scriptName("haya")
  .command(
    "crc <options...>",
    "Generate Components, Responsibilities, and Collaborator documentation",
    (yargs: any) => {
      return yargs.positional("files", {
        describe: "a list of files to do something with",
      });
    },
    (argv: Arguments) => {
      console.info(argv);
    },
  )
  .command(
    "describe <options...>",
    "Generate Haya solution description",
    (yargs: any) => {
      return yargs.option("output", {
        alias: "o",
        describe: "output file",
        type: "string",
        demandOption: true,
      });
    },
    (argv: Arguments) => {
      console.info(argv);
    },
  )
  .command(
    "backstage <options...>",
    "Generate Backstage catalog data",
    (yargs: any) => {
      return yargs.option("output", {
        alias: "o",
        describe: "output file",
        type: "string",
        demandOption: true,
      });
    },
    (argv: Arguments) => {
      console.info(argv);
    },
  )
  .command("diagram <options...>", "Generate diagram files", (yargs: any) => {
    return yargs.option("output", {
      alias: "o",
      describe: "output file",
      type: "string",
      demandOption: true,
    });
  }, (argv: Arguments) => {
    console.info(argv);
  })
  .help()
  .strictCommands()
  .demandCommand(1)
  .parse();
