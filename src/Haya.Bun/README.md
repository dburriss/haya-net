# Haya.Bun

Haya for **TypeScript** codebases: the same concepts, commands (`crc`, `describe`, `diagram`,
`backstage`) and outputs as `Haya.Tool` provides for C#, running on [Bun](https://bun.sh).

F# from `Haya.Core` (linked into `Haya.Bun.fsproj`) is compiled to TypeScript by
[Fable](https://fable.io) into `fable_output/`. `haya.ts` is the hand-written entry point that
imports the generated code. Only files that are free of .NET-only dependencies (Roslyn, YamlDotNet,
etc.) can be linked; platform specifics go behind `#if FABLE_COMPILER` (see `Haya.Core/IO.fs`).

Status: command skeleton only. `describe` constructs a Fable-compiled `DescribeCommand` and writes it
out via the Fable-compiled `IO.write`; the other commands are not implemented yet.

## Prerequisites

- .NET SDK (see `/global.json`) and `dotnet tool restore` at the repo root (installs Fable)
- Bun

## Build & run

```bash
bun install
bun run fable        # dotnet fable --lang ts --outDir fable_output
bun run typecheck    # tsc --noEmit
bun run start -- --help
bun run build        # fable + typecheck + `bun build --compile` -> dist/haya
./dist/haya --help
```

Fable 5 emits `.ts` extensions in imports (fixed in fable-compiler/Fable#3973), so the generated
code runs directly in Bun; `tsconfig.json` enables `allowImportingTsExtensions` for type-checking.
