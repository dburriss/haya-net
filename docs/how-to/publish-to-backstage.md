# How to publish to a Backstage catalog

Use this to generate Backstage `Component` entities from your annotations.

```bash
haya backstage -o catalog-info.yaml ./Example.sln          # YAML (default)
haya backstage -f json -o catalog.json ./Example.sln       # JSON array of Component entities
```

The default output is `./backstage.yaml` (or `./backstage.json` with `-f json`). Commit the file where your Backstage
instance discovers catalog files, or reference it from a `Location` entity.

## What you get

- One `Component` per application that has a `Meta`. Its `name` is the system and app name in kebab case
  (`WebShop` + `Checkout` is `web-shop-checkout`), `domain` is the system, `owner` is `Meta.Owner`, and `lifecycle` is
  always `production`.
- `dependsOn` lists the applications this one calls (its `Downstream` collaborators). `dependencyOf` lists the ones that
  call it (`Upstream` collaborators).
- A collaborator that is not an application in the same run becomes a stub `Component` so references resolve. When you
  [pass several systems](document-multiple-systems.md) the real component is used instead of a stub.
- A collaborator without a `System` takes the owning application's system, or `External` for an `External` relationship.

## Fill the gaps

Set `Owner` and `Description` on `Meta` and `Collaborator`; they are copied into the entities. See the
[attribute reference](../reference/attributes.md).
