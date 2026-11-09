# Contributing

How to build and test Haya. Agents and humans should also read [AGENTS.md](../AGENTS.md) for layout and conventions.

## .NET (C# tool, core, tests)

```bash
dotnet tool restore
dotnet build
dotnet test
dotnet run --project src/Haya.Tool -- describe -o out.json examples/dotnet/Example.sln   # input path goes last
```

## Bun (TypeScript tool)

Prerequisites: the .NET SDK from `global.json` (with `dotnet tool restore` for Fable) and Bun.

```bash
cd src/Haya.Bun
bun install
bun run fable        # compile Haya.Core to TypeScript
bun run typecheck
bun test             # analyser unit tests
bun run parity       # diff against Haya.Tool for the examples (needs .NET)
bun run build        # fable + typecheck + compile -> dist/haya
```

`bun run parity` must pass after changes to `Haya.Core`, either analyser, or the examples.

## Docs

Docs follow [Diataxis](https://diataxis.fr): put a page under `docs/tutorials`, `docs/how-to`, `docs/reference` or
`docs/explanation` by what the reader is doing, keep each page to one mode, and link from [the index](index.md).
Record design decisions as ADRs in [docs/adr](adr/README.md) and user-visible changes in the [changelog](../CHANGELOG.md).
