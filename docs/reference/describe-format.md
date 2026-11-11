# Describe file format

The output of `haya describe` and an accepted input to every command. JSON or YAML with the same shape.

## Version 0.4 (current)

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
      "language": "dotnet"
    }
  ],
  "version": "0.4"
}
```

| Field | Notes |
| --- | --- |
| `apps[]` | One entry per application. |
| `meta` | From `Meta`. `tech`, `kind`, `lifecycle` and `tier` are left out when unset. May be absent. |
| `language` | What the analyser that wrote the file analysed: `dotnet` (`Haya.Tool`) or `javascript` (`Haya.Bun`). Left out when unknown, e.g. in a file written by hand. |
| `responsibilities[]` | `description`, plus the class (`componentName`) and file (`componentSource`, relative to where `haya` ran). |
| `collaborators[]` | From `Collaborator`, plus `componentName`/`componentSource`. `direction` is `Upstream`/`Downstream`; `relationship` is one of `Owned`, `Shared`, `Internal`, `External` (a missing one reads as `Internal`). `kind` is `Person` for a person and is left out otherwise: a person is named by `appName` and has no `system`. |
| `version` | `"0.4"`. |

## Older versions (still read)

- `0.3`: same without `collaborators[].kind`. The `relationship` values `InternalUser` and `ExternalUser` that earlier
  versions allowed are no longer read (an error): write `"kind": "Person"` with `Internal` or `External` instead.- `0.2`: same without `meta.lifecycle`, `meta.tier` and `language`.
- `0.1`: same without `meta.tech` and `meta.kind`.
- `0.0`: a single app at the top level (`meta`, `responsibilities`, `collaborators`) instead of `apps`.

See [ADR006](../adr/ADR006-multi-app-system-model.md), [ADR007](../adr/ADR007-container-diagram-and-app-kind.md), [ADR010](../adr/ADR010-datadog-command-and-language.md) and [ADR013](../adr/ADR013-person-collaborators.md).
