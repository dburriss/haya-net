# AGENTS.md

Guidance for AI coding agents working in this repository. `CLAUDE.md` is a symlink to this file.

## What this is

Haya generates documentation (CRC markdown, Mermaid C4 diagrams, Backstage catalog, JSON/YAML
descriptions) from attributes (`[Responsibility]`, `[Collaborator]`, `[Meta]`) placed in a **C#**
codebase. `Haya.Tool` opens a solution with Roslyn `MSBuildWorkspace`, finds the attributes, and
renders outputs. See `README.md`, `Attributes.md`, and `examples/` (`examples/dotnet` is the C# example).

**Goal of the TypeScript side (`src/Haya.Bun`):** provide the same functionality for **TypeScript**
codebases: the same concepts (Responsibility, Collaborator, Meta), the same commands
(`crc`, `describe`, `diagram`, `backstage`), and the same output formats, so both stacks produce
equivalent docs. Analysis is language-specific (Roslyn for C#, TS tooling for TypeScript);
the model and rendering are shared F# in `Haya.Core`, compiled to TS with Fable. Status: early,
all of `Haya.Core` compiles under Fable with output identical to .NET; the TS analyser and CLI wiring are missing.

## Layout

| Path | What |
| --- | --- |
| `src/Haya` | Attribute definitions. **netstandard2.0** on purpose (consumed by user projects). NuGet `Haya`. |
| `src/Haya.Core` | Language-neutral model + rendering (no Roslyn, see ADR003): `Model.fs` (descriptors), `Types.fs`, `IO.fs`, `Serializer.fs` + `Encode.fs` (Flexi.Json/Flexi.Yaml, no reflection), `Markdown.fs` (internal markdown builder, no deps), `Describe/Diagram/Crc/Backstage.fs`. |
| `src/Haya.Roslyn` | C# analysis: `Roslyn.fs` and `Descriptors.fs` (attributes → `Haya.Core` descriptors). .NET only. |
| `src/Haya.Tool` | Argu CLI (`haya` dotnet tool, wires `Haya.Roslyn` to `Haya.Core`): `crc`, `describe`, `diagram`, `backstage`. |
| `src/Haya.Bun` | Haya for TypeScript codebases (WIP). Bun CLI in TS; compiles `Haya.Core` (model + rendering) with Fable via a project reference. See its README. |
| `tests/Haya.Core.Tests` | xunit.v3 tests; load `examples/dotnet/Example.sln` via Roslyn. |
| `examples/dotnet/Example` | C# sample annotated with Haya attributes; used by tests. |
| `docs/adr/` / `CHANGELOG.md` | Record design decisions (one `ADRnnn-description.md` each) / user-visible changes here. |

## Toolchain

- .NET SDK pinned in `global.json` (net10.0; `src/Haya` stays netstandard2.0).
- Tests run on Microsoft.Testing.Platform (`global.json` `test.runner`), xunit.v3.
- Local dotnet tools in `dotnet-tools.json` (Fable). Run `dotnet tool restore` first.
- `nuget.config` clears sources and uses nuget.org only.
- Bun + TypeScript for `src/Haya.Bun` (`bun.lock` is committed).

## Commands

```bash
dotnet tool restore
dotnet build
dotnet test
dotnet run --project src/Haya.Tool -- describe -o out.json examples/dotnet/Example.sln   # positional path goes LAST (Argu)

cd src/Haya.Bun && bun install && bun run build && ./dist/haya --help
```

## Conventions & gotchas

- F# compile order matters: add new files to the `<Compile>` list in the `.fsproj` in dependency order.
- Keep the C# and TypeScript sides at feature parity: a new concept, command or output format should
  land in shared `Haya.Core` code and be wired up on both sides (or tracked as a gap).
- All of `Haya.Core` is compiled by Fable into `Haya.Bun` (project reference), so it must compile under
  Fable: no .NET-only APIs (Roslyn, reflection-based serializers), use `#if FABLE_COMPILER`
  for platform code (JS side uses `node:fs`, which Bun implements). Prefer moving rendering logic into
  Fable-compatible files so both sides reuse it.
- Roslyn only analyses C# projects (`Roslyn.csharpProjects`); F# projects in a solution load without
  a compilation and must be skipped.
- No FSharpPlus; prefer plain FSharp.Core.
- Do not create branches or git worktrees unless explicitly instructed to. Work on the branch that is
  currently checked out (including `main`) when told to work "on main" or "here".
- Verify behavioural changes by diffing the tool's output (`describe`, `crc --c4`, `diagram`,
  `backstage`) for `examples/dotnet/Example.sln` before/after.
