# How to publish to the Datadog Software Catalog

Use this to generate Datadog Software Catalog entities (schema v3) from your annotations.

```bash
haya datadog -o entity.datadog.yaml ./Example.sln          # YAML (default)
haya datadog -f json -o entities.json ./Example.sln        # JSON array, for the Catalog API
```

The default output is `./entity.datadog.yaml` (or `./entity.datadog.json` with `-f json`). Datadog's GitHub and GitLab
integration finds files named `entity.datadog.yaml` or `service.datadog.yaml` in your repositories, so commit the YAML
there. One file can hold many entities; Haya separates them with `---`. Whether Datadog scans only the root of a
repository or any folder, and only the default branch, is not something Haya has confirmed: check your integration.

## What you get

- A `kind: service` per application that has a `Meta`. `AppKind.Database` becomes a `datastore` and `AppKind.Queue` a
  `queue`. An `Api` is a `service` of `spec.type: rest` and a `WebApp` one of `web`; Datadog's `api` kind cannot depend on
  anything, so it is not used. A `Worker`, `Service` or unset kind is a plain `service`.
- `metadata.name` is the system and app name in kebab case (`WebShop` + `Checkout` is `web-shop-checkout`), the same name
  the [Backstage](publish-to-backstage.md) output uses. `owner` is `Meta.Owner`, `description` is `Meta.Description`,
  and `spec.lifecycle` and `spec.tier` come from `Meta.Lifecycle` and `Meta.Tier` when set.
- `spec.languages` is `dotnet` for a codebase analysed by `Haya.Tool` and `js` for one analysed by `Haya.Bun`. It is
  left out for describe files that do not say, and for entities that are not a `service`.
- `datadog.codeLocations` holds `Meta.Repository` only when it is a URL (`https://...`). A slug such as `org/repo` is
  left out.
- A `kind: system` per system, with `spec.components` listing its applications.
- `spec.dependsOn` lists what an application calls, as `kind:name`. A `service` has no `dependencyOf`, so a relation is
  always written on the caller: an `Upstream` collaborator (which calls this application) depends on it.
- A collaborator that is not an application in the same run becomes a stub `service` so references resolve. When you
  [pass several systems](document-multiple-systems.md) the real entity is used instead of a stub.

## What is checked

The tests validate the JSON output against Datadog's own v3 JSON Schemas, vendored in
`tests/Haya.Core.Tests/schemas/datadog`. That catches keys a kind does not allow, a wrong `apiVersion` or a missing
name. The schemas cannot tell whether a `dependsOn` entry refers to an entity that exists, whether `languages`, `type`,
`lifecycle` or `tier` are values Datadog recognises (they are free strings) or whether `codeLocations` holds a URL.

## Fill the gaps

Set `Owner`, `Description`, `Lifecycle`, `Tier` and `Repository` on `Meta`, and `Owner` and `Description` on
`Collaborator`. `Owner` is passed through as is, so use the Datadog team handle. See the
[attribute reference](../reference/attributes.md).
