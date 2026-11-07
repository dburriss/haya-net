# Decision Log

## Decision: Separate Commands for Different Formats

Date: 2024-09-26  
Status: Accepted  
Author: dburriss

### Context 
`crc` was outputting md, json, and yaml in the same command.
It also had option of adding C4 level 1 and 2 diagrams, which was not 
relevant for the json and yaml output.

### Decision
Separate the commands for different formats, rather than trying to fit them into the crc command.
- `describe` for json and yaml output.
- `diagram` for C4 level 1 and 2 diagrams with various formats supported (initially mermaid).
- `backstage` for outputting a backstage catalog spec.

### Consequences
- *Pros:*
  - Easier to understand what each command does.
  - Easier to add new formats/functionality.
  - Simplifies the codebase.
- *Cons:*
  - More commands to remember.
  - More commands to document.

## Decision: Use Bun as the TypeScript runtime

Date: 2026-11-07  
Status: Accepted  
Author: dburriss

### Context
An experimental TypeScript build (Fable → TS) was started targeting Deno (`Haya.Deno`, `Fable.Deno`).
It stalled because Fable 4 emitted `.js` import extensions for TS output (fable-compiler/Fable#3973).
Fable 5 fixes this and emits `.ts` imports.

### Decision
Use Bun instead of Deno: `src/Haya.Bun`, npm `yargs`, `bun build --compile` for a single binary.
Platform access uses `node:fs` bindings (implemented by Bun) instead of the unmaintained `Fable.Deno`.

### Consequences
- *Pros:*
  - Bun runs the generated `.ts` directly and compiles a standalone binary.
  - Standard npm ecosystem; no Deno-specific bindings to maintain.
  - `node:fs` bindings also work on Node.
- *Cons:*
  - Another toolchain (Bun + TypeScript) alongside .NET.
  - Only Fable-compatible parts of `Haya.Core` can be shared; Roslyn analysis stays .NET-only.
