# Datadog Software Catalog entity definition (research)

Researched 2026-11-10 to decide whether Haya can emit a Datadog definition file like it does for Backstage.
Conclusion: yes. Target schema **v3** (`entity.datadog.yaml`).

## The file

- Datadog's Software Catalog reads `entity.datadog.yaml` or `service.datadog.yaml` (both names accepted).
- Datadog's GitHub/GitLab integration scans for them throughout each readable repository.
- One file may hold several definitions separated by `---` (same shape as Haya's Backstage output).
- Ingestion: GitHub/GitLab integration, Catalog API, Terraform (`service_definition_yaml` for v2.x, `software_catalog` for v3), Backstage, ServiceNow, UI.
- Do not edit the same definition through both GitHub and the API: they can overwrite each other.
- v3.0 is not available on ddog-gov.com sites.
- **Unverified:** whether only the repo root or any folder is scanned; whether only the default branch is read (the GitHub integration page returned 404).

## Schema versions

### v2.2 (`service.datadog.yaml`, services only)

Fields: `schema-version`, `dd-service`, `team`, `application`, `tier`, `type`, `languages`, `contacts`, `links`, `tags`,
`integrations`, `ci-pipeline-fingerprints`, `extensions`. No dependency field (dependencies come from APM).

### v3.0 (`entity.datadog.yaml`)

Top-level keys (no others allowed): `apiVersion` (enum `v3`), `kind`, `metadata`, `spec`, `integrations`, `datadog`, `extensions`.
Required: `apiVersion`, `kind`, `metadata`. No `spec` is required for any kind.

```yaml
apiVersion: v3
kind: service
metadata:
  name: shopping-cart-api
  owner: shopping-team
  additionalOwners:
    - name: sre-team
      type: operator
spec:
  lifecycle: production
  tier: critical
datadog:
  codeLocations:
    - repositoryURL: https://github.com/myorg/myrepo.git
      paths:
        - path/to/service/code/**
```

## metadata (`metadata.schema.json`, `additionalProperties: false`)

| Key | Notes |
| --- | --- |
| `name` | **Only required key.** String, `minLength: 1`. No pattern, charset, length cap or case rule in the schema or docs. Lowercase kebab-case is a safe convention, not enforced. |
| `description` | Exists. Plain string ("Short description of the entity"). |
| `displayName` | Plain string. |
| `owner` | Plain string, no pattern. Described as "usually a team"; example `myTeam`. That it is the team handle is an assumption. |
| `additionalOwners` | Array of `{name (required), type}`. No other keys. |
| `tags` | Array of strings, e.g. `my:tag`. |
| `contacts` | Array of `{type (required), contact (required), name (minLength 2)}`. `type: email` needs an email in `contact`. Types seen: email, slack, microsoft-teams. |
| `links` | Array of `{name, type, url}` all required, optional `provider`. `url` is `format: uri`. Types seen: runbook, doc, repo, dashboard, other. |
| `inheritFrom` | `<kind>:<name>` reference to inherit metadata (example `application:default/myapp`). |
| `id`, `managed` | Read-only. |

**No `namespace` property**: with `additionalProperties: false` it would be rejected. The `default/` segment only appears in the `inheritFrom` example; a third-party skill site claims a `namespace` default, which contradicts the schema.

## Kinds

- `kind` is lowercase and case-sensitive (`api`, not `API`).
- Per-kind schemas exist for: `service`, `datastore`, `queue`, `api`, `system`, `application`.
- `custom` accepts any kind matching `^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$`.
- **`library` has no schema file.** The entity schema accepts any kind string and the docs use `kind: library` with metadata only, so emitting it is plausible but not schema-backed.
- `datadog.codeLocations` (`{repositoryURL, paths[]}`) is allowed on service, datastore, queue, api, system and custom, **not** application. The code-locations schema is oddly written (`items` under `properties`), so validators may not enforce it.

## spec fields per kind

| Kind | Notable spec fields |
| --- | --- |
| service | `lifecycle`, `tier`, `type`, `languages`, `dependsOn`, `componentOf` (exactly six, `additionalProperties: false`) |
| datastore, queue | `lifecycle`, `tier`, `type`, `componentOf`, `dependsOn`, `dependencyOf` (exactly six, `additionalProperties: false`) |
| api | `lifecycle`, `tier`, `type`, `interface` (exactly one of `fileRef` uri / `definition`), `implementedBy`, `componentOf`. **No `dependsOn`.** |
| system | `lifecycle`, `tier`, `type`, `components`, `componentOf` (exactly five). **No `dependsOn`.** |

Emitting `dependsOn` on an `api` or `system` fails validation (each per-kind `spec` has `additionalProperties: false`).

Verified against the raw schemas (2026-11-10): `entity.schema.json` is the shared base (`apiVersion`, `kind`, `metadata`, free-form `spec`, `integrations`, `datadog`, `extensions`) and does **not** define `dependsOn`, so the per-kind schemas are the only source. Datastore and queue descriptions read "A list of components the datastore/queue depends on" and "...is a dependency of". `dependencyOf` is only on datastore and queue, not service. `metadata.schema.json` was not inspected (`dependsOn` is a `spec` field, so not expected there).

Semantics: `dependsOn` lists what this entity relies on (outgoing edge). Only running components (service, datastore, queue, custom) can declare it. `api` is a spec of an interface and `system` is a grouping, so neither can. Dependencies of a system or API must be declared on the components that implement them.

### Value sets (all free strings, no enums, examples only)

- `lifecycle`: experimental, production, deprecated.
- `tier`: `"1"`, `"High"`.
- service `type`: web, grpc, http, rest, graphql. A `worker` value passes the schema but is unverified as recognised.
- datastore `type`: redis, postgres, cassandra. queue `type`: kafka, rabbitmq. api `type`: openapi, graphql, rest, grpc. system `type`: public-web, mobile, internal, shopping-cart.

## Languages (`spec.languages`, service only)

Array of free strings, no enum. Schema says "See examples for a list of recognizable languages":
`dotnet, go, java, js, php, python, ruby, c++`.

| Haya analyser | Datadog value | Confidence |
| --- | --- | --- |
| C# (Roslyn) | `dotnet` | High (in schema examples) |
| TypeScript (Bun) | `js` | Medium (only JS-family example; `typescript` not listed, acceptance unverified) |

## Relationships

- `dependsOn` / `dependencyOf` / `componentOf` / `components` entries are free strings in `kind:name` form, where name is `metadata.name`. Examples: `service:myapp`, `queue:myqueue`, `datastore:mydatastore`, `library:mylibrary`. No namespace shown.
- `dependsOn` is defined on service, datastore, queue, custom (not api, system). Datastore and queue also have `dependencyOf`; service does not.
- v3 supports manual declarations to augment auto-detected (APM/USM) topology.
- Components inherit system metadata implicitly when exactly one system is defined in the file, or explicitly via `inheritFrom`.
- **Unverified:** whether the backend tolerates a `dependsOn` pointing to an entity no file defines (the schema cannot enforce existence).

## Sources

- https://docs.datadoghq.com/internal_developer_portal/software_catalog/entity_model/
- https://docs.datadoghq.com/internal_developer_portal/catalog/entity_model.md
- https://docs.datadoghq.com/internal_developer_portal/catalog/set_up/create_entities.md
- https://docs.datadoghq.com/internal_developer_portal/use_cases/dependency_management/
- https://datadoghq.com/blog/service-catalog-schema-v3
- https://github.com/DataDog/schema/tree/main/service-catalog/v3 (raw schemas: `entity`, `metadata`, `service`, `datastore`, `queue`, `api`, `system`, `application`, `custom`, `datadog_code_locations`)
