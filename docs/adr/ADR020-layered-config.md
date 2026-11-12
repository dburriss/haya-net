# ADR020: Layered configuration

Date: 2026-11-12  
Status: Accepted  
Author: dburriss

### Context
Every default was fixed in the CLIs (and written twice, in `Cli.fs` and `haya.ts`), and the explorer's colour stops were constants in
`app.js`. People repeat the same flags on every run. The usual .NET answer, `Microsoft.Extensions.Configuration`, does not
compile under Fable, and the TypeScript CLI needs the same behaviour.

### Decision
Configuration is `Haya.Core.Config`, written in plain F# on Flexi.Json and small `IO` helpers (environment, home and config
directory), so it compiles to both stacks. Settings come from, highest first: command line flags, `HAYA_*` environment variables, the
nearest `.haya/config.json`, the global `haya/config.json` in the OS config directory, and the built-in defaults. Each setting
is resolved on its own.

The settings form one table of dotted keys (`output.path`, `viewer.complexity.systems`) with a type each, and the table drives
reading files, reading environment variables (`HAYA_` + key with `.` as `__`), `haya config set` and the docs. Files are
sectioned; settings for one stack sit in a section for it (`dotnet.references`), which the other CLI ignores.
`output.path` is a directory (each command keeps its file name), and each command can also name its own file. `crc.diagram` takes the
`--detail` names (`none`, `systems`, `apps`) rather than a number.

The viewer settings travel in the page as an optional `viewer` object in the graph JSON. The format version stays 0.5: the object is
absent unless something is configured, so existing pages are unchanged, and the viewer keeps its built-in values for what is missing.

`haya config list | get | set | unset` exists on both CLIs. It only writes the local or global file.

### Consequences
- Flags that default (`-f`, `-d`, `-o`) no longer carry a parser default, so an explicit flag can be told from an absent one.
- A broken config file stops the command with its path; an unknown key is a warning.
- Rewriting a file with `set` drops comments, as JSON has none.
- `HAYA_CONFIG_DIR` replaces the OS config directory, which keeps the parity check and tests independent of the machine.
- A flag cannot turn `dotnet.references.include` off once configured.
