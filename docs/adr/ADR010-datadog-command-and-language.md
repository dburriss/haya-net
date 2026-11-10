# ADR010: Datadog command, analyser-stamped language, lifecycle and tier

Date: 2026-11-10  
Status: Accepted (supersedes the describe format of [ADR007](ADR007-container-diagram-and-app-kind.md) with `0.3`)  
Author: dburriss

### Context
Datadog's Software Catalog reads `entity.datadog.yaml` (schema v3) from repositories, which is the same job Backstage's
`catalog-info.yaml` does. The model has what it needs, apart from three things: Datadog wants the language of a service,
and has `lifecycle` and `tier`. The research is in `research/datadog-entity-definition.md` and
`research/datadog-command-design.md`.

### Decision
- A `datadog` command, one per output format as in [ADR001](ADR001-separate-commands-per-format.md), on both stacks, with the
  rendering in `Haya.Core` (`Datadog.fs`). The naming and relations that it shares with Backstage moved to `Catalog.fs`.
- **Language is set by the analyser, on `App`.** `Haya.Roslyn`/`Haya.Tool` stamp `DotNet`, the TypeScript analyser
  `JavaScript`. It is not a property of `Meta`, which mirrors what a user writes, and it is not taken from the running tool:
  a describe file written by the other tool must keep the language of what was analysed. A file that does not say is
  `Unknown`, and `spec.languages` is then left out rather than guessed. Datadog's names are `dotnet` and `js`, because the
  schema's examples list no `typescript`.
- `Meta` gains optional `Lifecycle` and `Tier`, passed to `spec.lifecycle` and `spec.tier`.
- Describe format `0.3` adds `meta.lifecycle`, `meta.tier` and `language`, all left out when unset. `0.2` and older are read.
- Mapping: `Database` is a `datastore`, `Queue` a `queue`, everything else a `service` (`Api` has type `rest`, `WebApp`
  `web`). The `api` kind is not used because each kind's `spec` is closed and `api` has no `dependsOn`. A `service` has no
  `dependencyOf`, so relations are always written as `dependsOn` on the caller.
- The output is validated in the tests against Datadog's JSON Schemas, vendored at a pinned commit (Apache-2.0), so the
  tests need no network.

### Consequences
- `bun run parity` compares `datadog` output of both tools; the language each tool stamps is the one intended difference and
  is mapped before comparing.
- Whether Datadog accepts a `dependsOn` to an entity defined in no file, and whether it accepts `typescript` as a language,
  is not known. Stubs are emitted so every reference resolves, and `js` is used.
- Refreshing the schemas is a manual step (see the README next to them).
