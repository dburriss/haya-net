# Vendored Datadog Software Catalog schemas

JSON Schemas (draft-07) for the v3 entity model (`entity.datadog.yaml`), used by `DatadogSchemaTests.fs` to check what
`haya datadog` writes. Nothing is fetched at test time: the schemas refer to each other by absolute
`raw.githubusercontent.com` URLs, which are their `$id`s, and the tests register every file here under that `$id`.

- **Source:** https://github.com/DataDog/schema, directory `service-catalog/v3`
- **Commit:** `1fe094ddde7020e0f5b549c43d8817e15c82cec6`
- **Licence:** Apache-2.0, see `LICENSE` here (copied from the same commit). The files are unmodified.

## Refreshing

```bash
cd tests/Haya.Core.Tests
SHA=$(gh api repos/DataDog/schema/commits/main --jq .sha)
for f in $(gh api "repos/DataDog/schema/contents/service-catalog/v3?ref=$SHA" --jq '.[].name'); do
  gh api -H "Accept: application/vnd.github.raw" "repos/DataDog/schema/contents/service-catalog/v3/$f?ref=$SHA" > "schemas/datadog/v3/$f"
done
gh api -H "Accept: application/vnd.github.raw" "repos/DataDog/schema/contents/LICENSE?ref=$SHA" > schemas/datadog/LICENSE
```

Then update the commit above, run `dotnet test`, and check `research/datadog-entity-definition.md` still holds.

## What the schemas can and cannot tell us

They catch keys a kind does not allow (each kind's `spec` is closed: `dependsOn` is rejected on `api` and `system`,
`dependencyOf` on `service`), a wrong `apiVersion`, a missing `metadata.name`, and wrong types.

They cannot tell us:

- that a `dependsOn` or `components` entry refers to an entity that exists, or is written `kind:name` (plain strings);
- that `spec.languages`, `spec.type`, `lifecycle` or `tier` hold values Datadog recognises (free strings, examples only);
- that `datadog.codeLocations[].repositoryURL` is a URL: that schema is written with `items` under `properties`,
  so validators may not enforce it.
