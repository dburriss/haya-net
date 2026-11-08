# ADR005: TypeScript annotations and static analyser

Date: 2026-11-08  
Status: Accepted  
Author: dburriss

### Context
`Haya.Bun` must produce the same documentation for TypeScript codebases as `Haya.Tool` does for C#
(see [ADR002](ADR002-bun-typescript-runtime.md), [ADR003](ADR003-split-roslyn-from-core.md)). It needs an annotation
syntax for TypeScript and an analyser that turns annotated source into `Haya.Core` descriptors, without running user code.

### Decision
**Annotation syntax.** Class decorators `@Responsibility({...})` and `@Collaborator({...})` (both repeatable) and a
top-level `meta({...})` call (once per project, the counterpart of the C# assembly-level `Meta`). Option names are
camelCase versions of the C# property names (`technology` maps to the descriptor's `Tech`). `Direction` and `Relationship`
are string enums. Decorator names are PascalCase: there is no official convention (the TS handbook, the TS 5.0 post
and the TC39 proposal are silent and use incidental lowercase examples), but the major frameworks that take an options
object (Angular, NestJS, TypeORM, MikroORM, Stencil) and the Google TS style guide use PascalCase. The C# attributes are
not a reason: the two syntaxes are independent and only the generated output must match.

**Static analysis.** The analyser reads the annotations with the TypeScript compiler API and never executes user code.
It creates a `Program` from a `tsconfig.json` (or a directory containing one), and resolves decorators and `meta` by
symbol, following import aliases, to a declaration inside the `@haya/annotations` package. A same-named function from
another module is ignored. Option values must be static: string literals, template literals without substitutions, and
`Direction.*` / `Relationship.*` members. Anything else (identifiers, spreads, computed values, unknown option names,
more than one `meta()`) is an error with file, line and column.

**Order and semantics** follow `Haya.Roslyn.Descriptors.getDescriptors`: all responsibilities, then all collaborators,
then meta; source files sorted by path, classes in source order, repeated decorators in source order. Unset properties
default to `""`, direction `Downstream` and relationship `Internal`. `ComponentName` is the class name and
`ComponentSource` the file path relative to the current directory with `/` separators (URL-escaped, like .NET's
`Uri.MakeRelativeUri`).

**TypeScript version.** TypeScript 7 (the native port) ships no stable programmatic API: `typescript/unstable/*` exists,
but the 7.0 announcement says 7.1 will ship a new, different API. We therefore use the officially supported
side-by-side route from the announcement: `typescript` is aliased to `@typescript/typescript6` (the 6.0 API, used by the
analyser) and `@typescript/native` is `typescript@^7` (provides `tsc` for type checking). Revisit when 7.1 ships.

**Annotation package.** The annotations live in a small package, `src/Haya.TypeScript` (npm name `@haya/annotations`),
mirroring `src/Haya` for C#. It is private for now and consumed with `"file:"` dependencies; the example imports it
instead of keeping a copy of `haya.ts`.

**Bridge.** The analyser is plain TypeScript producing plain objects. A thin bridge builds the Fable-compiled `Model`
values (enums are ints in Fable output, lists via fable-library `ofArray`) and hands them to the Fable-compiled
`Describe`, `Crc`, `Diagram` and `Backstage` modules.

### Consequences
- *Pros:*
  - Resolution by symbol is robust to import aliases and name clashes; errors point at the offending source.
  - Both stacks share all rendering code; only analysis is per language.
  - The annotation package gives users a stable import and one place to document the API.
- *Cons:*
  - Static-only values: no constants or computed options.
  - Two TypeScript versions in `Haya.Bun` until the 7.1 API is stable.
  - The package must be published (and its decorator types validated against real consumers) before users can adopt it.
- *Known differences from .NET:* the .NET analyser drops a class's collaborators when the same class also has a
  `[Responsibility]` (and duplicates its responsibilities). The TypeScript analyser emits both; the .NET behaviour is
  considered a bug and tracked rather than replicated.
