# Describe file format

The output of `haya describe` and an accepted input to every command. JSON or YAML with the same shape.

## Version 0.3 (current)

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
        }
      ],
      "language": "dotnet"
    }
  ],
  "version": "0.3"
}
```

| Field | Notes |
| --- | --- |
| `apps[]` | One entry per application. |
| `meta` | From `Meta`. `tech`, `kind`, `lifecycle` and `tier` are left out when unset. May be absent. |
| `language` | What the analyser that wrote the file analysed: `dotnet` (`Haya.Tool`) or `javascript` (`Haya.Bun`). Left out when unknown, e.g. in a file written by hand. |
| `responsibilities[]` | `description`, plus the class (`componentName`) and file (`componentSource`, relative to where `haya` ran). |
| `collaborators[]` | From `Collaborator`, plus `componentName`/`componentSource`. `direction` is `Upstream`/`Downstream`; `relationship` is one of `Owned`, `Shared`, `Internal`, `External`, `InternalUser`, `ExternalUser`. |
| `version` | `"0.3"`. |

## Older versions (still read)

- `0.2`: same without `meta.lifecycle`, `meta.tier` and `language`.
- `0.1`: same without `meta.tech` and `meta.kind`.
- `0.0`: a single app at the top level (`meta`, `responsibilities`, `collaborators`) instead of `apps`.

See [ADR006](../adr/ADR006-multi-app-system-model.md), [ADR007](../adr/ADR007-container-diagram-and-app-kind.md) and [ADR010](../adr/ADR010-datadog-command-and-language.md).
