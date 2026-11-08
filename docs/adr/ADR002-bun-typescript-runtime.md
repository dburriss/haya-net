# ADR002: Use Bun as the TypeScript runtime

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
