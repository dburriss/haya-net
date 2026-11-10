# `haya datadog` command: design notes

Companion to [datadog-entity-definition.md](datadog-entity-definition.md). Status: proposal, nothing implemented.

## Mapping Haya to Datadog v3

| Haya | Datadog v3 |
| --- | --- |
| `Meta.AppName` (via `createName`) | `metadata.name` |
| `Meta.Description` | `metadata.description` |
| `Meta.Owner` | `metadata.owner` |
| `Meta.Repository` | `datadog.codeLocations[].repositoryURL` |
| `Meta.System` | `kind: system` entity, apps in `spec.components` (`service:name`) |
| `Meta.Kind` Service / WebApp | `kind: service` (`spec.type: web` for WebApp) |
| `Meta.Kind` Api | `kind: service`, `spec.type: rest` (not `kind: api`: api specs cannot carry `dependsOn`) |
| `Meta.Kind` Worker | `kind: service` (type unverified, omit or `worker`) |
| `Meta.Kind` Database / Queue | `kind: datastore` / `kind: queue` |
| `Meta.Tech` | not parsed (free text, stays for diagrams) |
| Downstream collaborator | `spec.dependsOn: [kind:name]` |
| Upstream collaborator, or in-catalog caller | the caller's `dependsOn` pointing at this app |
| Collaborator not in the catalog | stub entity (as `Backstage.genCollaboratorComponents`) |
| `lifecycle`, `tier` | not in Haya's model: omit, or CLI flags later |

## Language from the analyser

`spec.languages` should come from what the analyser knows, not from `Meta.Tech` and not from the executing tool.

- Add `type Language = Unknown | DotNet | JavaScript` to `Haya.Core` `Model.fs`.
- Put `Language` on `App` (not `MetaDescriptor`, which mirrors the user's `[Meta]` / `meta()`).
- `Haya.Roslyn` stamps `DotNet`; the TS analyser stamps `JavaScript`. Mapping: `dotnet` / `js`.
- Describe format gets an optional `language` field so a describe file keeps it; missing decodes to `Unknown`.
- Fallback for `Unknown`: host runtime via one `#if FABLE_COMPILER` in a small `Platform` helper (JavaScript under Fable, DotNet otherwise).
- When still unknown, omit `languages` rather than guess.
- Closed enum preferred over a string: the set of analysers is small and deliberate.
- Other uses: a `language:` tag in Backstage; default `Meta.Tech` text in diagrams/CRC.

## Implementation outline

- `src/Haya.Core/Datadog.fs` modelled on `Backstage.fs` (share the dependents/dependencies/stub logic).
- `Encode.datadogEntity` (Flexi.Yaml / Flexi.Json, no reflection; keep Fable-compatible). Add files to the `.fsproj` in dependency order.
- `datadog` subcommand in `src/Haya.Tool/Cli.fs` and `Program.fs`, and in `src/Haya.Bun`. Default output `entity.datadog.yaml`.
- Tests; `bun run parity` must pass (describe output changes with `language`); CLI reference, `CHANGELOG.md`. ADR001 already covers one command per format.
- Validate output against `DataDog/schema` `service-catalog/v3`.

## Decisions (2026-11-10)

- **Stubs:** emit stub entities for collaborators not in the catalog, as Backstage does.
- **TypeScript language:** `js`.
- **Worker:** `kind: service` with no `spec.type`.
- **Lifecycle / tier:** add optional fields to `[Meta]` and `meta()`, carried through the describe format and both analysers, and emit `spec.lifecycle` / `spec.tier`.
- **Owner:** `Meta.Owner` passes through unchanged to `metadata.owner`.

## Still open (cannot be settled from docs)

- Does Datadog's backend tolerate a `dependsOn` to an entity defined in no file? (Stubs mitigate this.)
- Does the GitHub integration scan only the repo root or default branch?
