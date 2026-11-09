# ADR006: Multi-app system model and describe format 0.1

Date: 2026-11-08  
Status: Accepted (describe format superseded by `0.2` in [ADR007](ADR007-container-diagram-and-app-kind.md))  
Author: dburriss

### Context
A describe file held one app: a single `meta` plus flat responsibilities and collaborators. To document a whole system
you want to gather the describe files of many solutions (C# and TypeScript) and render one catalog, diagram or CRC
document. Renderers assumed exactly one meta (`List.head` in Backstage, `tryHead` in Diagram and Encode).

### Decision
- `Haya.Core` has an `App` (`Meta option`, responsibilities, collaborators). One solution, tsconfig project or
  describe-file entry is one app. All renderers take `App list`.
- Describe format `0.1`: `{ "apps": [ { meta, responsibilities, collaborators } ], "version": "0.1" }`. The decoder also
  reads `0.0` (a single app at the top level), so older files keep working as input.
- Every command takes one or more inputs of any kind (solutions, tsconfig projects, describe files, mixed). Inputs are
  concatenated and then merged by `Merge.apps`.
- **Identity** of an app is its system plus app name, compared case-insensitively. Apps with the same identity are
  merged: responsibilities and collaborators are concatenated without duplicates, the first meta wins and a conflict in
  description, owner or repository is a warning on stderr. Apps without a meta are never merged.
- Rendering: Backstage emits a component per app and only emits stubs for collaborators that are not an app in the
  catalog (the stub would otherwise duplicate the real component); an app's `dependencyOf` also lists the apps that
  depend on it. Diagram declares each system once and relates systems to each other, and relationships are
  deduplicated. CRC keeps its layout for one app and gets a section per app for several.

### Consequences
- Single-app output of `crc`, `diagram` and `backstage` is unchanged; `describe` output changes shape (version bump).
- `ComponentSource` is relative to the directory each tool ran in, so paths from different repos are kept as written and
  are not part of an app's identity.
- Both CLIs share the merge code (Fable-compiled), so merged output is identical (`bun run parity`).
