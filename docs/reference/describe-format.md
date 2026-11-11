# Describe file format

The output of `haya describe` and an accepted input to every command. JSON or YAML with the same shape.

## Version 0.6 (current)

```json
{
  "apps": [
    {
      "meta": {
        "appName": "Orders", "description": "Accepts and tracks customer orders", "system": "Ordering",
        "repository": "org/orders", "owner": "Ordering Team",
        "tech": ".NET 10 / ASP.NET Core", "kind": "Api", "lifecycle": "production", "tier": "1"
      },
      "responsibilities": [
        { "description": "Creates and tracks orders", "componentName": "OrderService", "componentSource": "Orders/Orders.cs" }
      ],
      "collaborators": [
        {
          "appName": "Payments", "system": "Billing", "description": "Takes payments for orders",
          "direction": "Downstream", "relationship": "Internal", "tech": "dotnet", "protocol": "HTTPS",
          "dataDescription": "Request payment", "repository": "org/payments", "owner": "Billing Team",
          "componentName": "PaymentsClient", "componentSource": "Orders/Orders.cs"
        },
        {
          "appName": "Customer", "system": "", "description": "Places and tracks orders",
          "direction": "Upstream", "relationship": "External", "kind": "Person", "tech": "", "protocol": "",
          "dataDescription": "Places an order", "repository": "", "owner": "",
          "componentName": "OrderEndpoints", "componentSource": "Orders/Orders.cs"
        }
      ],
      "components": [
        {
          "name": "OrderService", "source": "Orders/Orders.cs",
          "constructors": [ [ { "name": "payments", "type": "PaymentsClient" } ] ],
          "dependencies": [
            { "name": "PaymentsClient", "source": "Orders/Orders.cs", "via": [] },
            { "name": "StockClient", "source": "Orders/Stock.cs", "via": [ "ReservationService", "IStockApi" ] }
          ],
          "members": [
            { "kind": "Method", "name": "Place", "type": "Task<bool>", "parameters": [ { "name": "orderId", "type": "string" } ] },
            { "kind": "Property", "name": "Name", "type": "string" }
          ]
        }
      ],
      "language": "dotnet"
    }
  ],
  "detail": "members",
  "version": "0.6"
}
```

| Field | Notes |
| --- | --- |
| `apps[]` | One entry per application. |
| `meta` | From `Meta`. `tech`, `kind`, `lifecycle` and `tier` are left out when unset. May be absent. |
| `language` | What the analyser that wrote the file analysed: `dotnet` (`Haya.Tool`) or `javascript` (`Haya.Bun`). Left out when unknown, e.g. in a file written by hand. |
| `responsibilities[]` | `description`, plus the class (`componentName`) and file (`componentSource`, relative to where `haya` ran). |
| `collaborators[]` | From `Collaborator`, plus `componentName`/`componentSource`. `direction` is `Upstream`/`Downstream`; `relationship` is one of `Owned`, `Shared`, `Internal`, `External` (a missing one reads as `Internal`). `kind` is `Person` for a person and is left out otherwise: a person is named by `appName` and has no `system`. |
| `components[]` | Only with `describe --detail members`, left out when empty: each class with a responsibility or collaborator, by `name` and `source` (as `componentName`/`componentSource`). `constructors` holds each public constructor's parameters in order. `members` holds the public members declared on the class: `kind` (`Method`, `Property`, `Field` or `Event`), `name`, `type`, and `parameters` for methods (and C# indexers). Types are as the language writes them (`Task<bool>` in C#, `Promise<boolean>` in TypeScript). See [ADR017](../adr/ADR017-member-detail.md) for what is left out. `dependencies`, left out when empty: the other annotated classes of the same app that this class reaches in code, by `name` then `source`, with `via` the unannotated types in between along the shortest chain (`[]` when direct). Best effort: see [ADR018](../adr/ADR018-component-dependencies.md) for what is followed and what is not. |
| `detail` | What the document was asked to hold: `apps` (the default), `components` (the same as `apps` today) or `members` (adds `components[]`). Tells "not asked for" apart from "none". |
| `version` | `"0.6"`. |

## Older versions (still read)

- `0.5`: same without `components[].dependencies` (read as none).
- `0.4`: same without `detail` and `components[]`.
- `0.3`: same without `collaborators[].kind`. The `relationship` values `InternalUser` and `ExternalUser` that earlier
  versions allowed are no longer read (an error): write `"kind": "Person"` with `Internal` or `External` instead.
- `0.2`: same without `meta.lifecycle`, `meta.tier` and `language`.
- `0.1`: same without `meta.tech` and `meta.kind`.
- `0.0`: a single app at the top level (`meta`, `responsibilities`, `collaborators`) instead of `apps`.

See [ADR006](../adr/ADR006-multi-app-system-model.md), [ADR007](../adr/ADR007-container-diagram-and-app-kind.md), [ADR010](../adr/ADR010-datadog-command-and-language.md), [ADR013](../adr/ADR013-person-collaborators.md) and [ADR017](../adr/ADR017-member-detail.md).
