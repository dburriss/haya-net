# ADR009: Analyse referenced assemblies (C# only)

Date: 2026-11-09  
Status: Accepted  
Author: dburriss

### Context
The Roslyn analyser walks the documents of each project in the solution. Types in referenced NuGet packages or DLLs
have no documents, so annotations on shared libraries never reached the consuming app's docs.

### Decision
- Opt-in, so existing output does not change: `--include-references` and `--include-assembly <glob>` (repeatable), plus
  `--verbose` to list what was picked up.
- Walk the namespaces and types of each metadata reference that itself references `Haya`. References to projects in the
  solution are skipped: they are analysed from source. Reading attributes and `*Defaults` markers uses the same code as
  for source, since Roslyn exposes attributes on metadata symbols the same way.
- Public types only.
- Assembly-level `[Meta]` of references is not read. `App.ofDescriptors` takes the first Meta, and a package would
  otherwise replace the app's own (ADR006).
- A type without a source file has the `ComponentSource` `Assembly::Namespace.Type`. The CRC renders it as plain text,
  not a link. The describe model does not record origin; `--verbose` reports it instead.

### Consequences
Scanning is slower with many references (mitigated by only walking assemblies that reference Haya). Packages must be
restored. TypeScript has no equivalent: decorators and `meta()` calls are not in `.d.ts` files, so reading a package would
mean analysing its source, a separate mechanism.
