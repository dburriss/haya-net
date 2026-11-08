# ADR003: Split Roslyn analysis out of Haya.Core

Date: 2026-11-08  
Status: Accepted  
Author: dburriss

### Context
`Haya.Core` mixes two concerns: C# analysis (`Roslyn.fs`, and the symbol-to-descriptor construction in
`Descriptor.fs`) and the model and rendering (`Describe`, `Crc`, `Diagram`, `Backstage`, `Serializer`).
`Descriptor.fs` both defines the `Descriptor` model and builds it from Roslyn symbols, and every renderer
consumes that model. As a result the renderers cannot be shared with `Haya.Bun`, which links individual
files (`Types.fs`, `String.fs`, `IO.fs`) as a workaround.

### Decision
Make `Haya.Core` the language-neutral, Fable-compatible core, and move everything Roslyn-specific out:

| Project | Contents | Targets |
| --- | --- | --- |
| `Haya` | Attribute definitions | netstandard2.0 |
| `Haya.Core` | Descriptor model (pure records), `Types`, `String`, `IO`, `Serializer`, `Describe`, `Diagram`, `Crc`, `Backstage` | .NET and Fable |
| `Haya.Roslyn` (new) | `Roslyn.fs` and the Roslyn symbol-to-descriptor construction | .NET only |
| `Haya.Tool` | Argu CLI wiring `Haya.Roslyn` to `Haya.Core` | .NET |
| `Haya.Bun` | Bun CLI; references `Haya.Core` as a whole, plus a TypeScript analyser | Fable and Bun |

The descriptor model has no dependency on Roslyn, `Haya`, or any .NET-only library. Analysers (Roslyn for C#,
TypeScript tooling for TS) produce descriptors; `Haya.Core` renders them.

### Consequences
- *Pros:*
  - `Haya.Bun` references `Haya.Core` directly instead of linking selected files.
  - Both stacks share one implementation of every output format, which keeps them at parity.
  - Adding another language means writing an analyser only.
- *Cons:*
  - One more project to build and package.
  - Everything in `Haya.Core` must stay Fable-compatible, so .NET-only libraries cannot be used there.
  - Requires migrating `Descriptor.fs` and the dependent renderers; verify by diffing `describe`, `crc --c4`,
    `diagram` and `backstage` output for `examples/dotnet/Example.sln` before and after.
