# How to document several systems together

Use this when applications live in different repositories (C#, TypeScript or both) and you want one diagram, catalog
or CRC document for all of them.

## 1. Describe each codebase once, in its own repository

```bash
haya describe -o describe/Orders.json Orders/Orders.sln       # C#
haya describe -o describe/Web.yaml -f yaml .                   # TypeScript, from the project folder
```

Commit the describe files (or publish them as CI artifacts) so a central repository can read them.

## 2. Render from all the describe files

```bash
haya crc -cc -o CRC.md           describe/Storefront.json describe/Orders.json describe/Payments.json
haya diagram -o system.mermaid   describe/Storefront.json describe/Orders.json describe/Payments.json
haya backstage -o backstage.yaml describe/Storefront.json describe/Orders.json describe/Payments.json
```

Input paths go last. Describe files (`.json`, `.yaml`, `.yml`) can be mixed with solutions or tsconfig projects in one call.

## How apps are merged

Apps with the same system and app name (case-insensitive) become one: responsibilities and collaborators are combined
without duplicates and the first `Meta` wins. A conflict in description, owner or repository prints
`Warning: ...` on stderr. Apps without a `Meta` are never merged. Background: [ADR006](../adr/ADR006-multi-app-system-model.md).

## Working example

[examples/dotnet-multi-system](../../examples/dotnet-multi-system) and [examples/ts-multisystem](../../examples/ts-multisystem)
each contain describe files, a `generate.sh` and the generated output.
