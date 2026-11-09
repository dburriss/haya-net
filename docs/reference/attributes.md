# Attributes and annotations reference

Haya has three annotations. In C# they are attributes from the `Haya` package; in TypeScript they are
`@Responsibility`, `@Collaborator` and `meta()` from `@haya/annotations`. TypeScript option names are the camelCase
form of the C# property names (`AppName` becomes `appName`).

| | C# | TypeScript |
| --- | --- | --- |
| Meta | `[assembly: Meta(...)]`, once per assembly | `meta({...})`, top-level call, once per project |
| Responsibility | `[Responsibility(...)]` on a class or struct, repeatable | `@Responsibility({...})` class decorator, repeatable |
| Collaborator | `[Collaborator(...)]` on a class or struct, repeatable | `@Collaborator({...})` class decorator, repeatable |

TypeScript values must be static: string literals, template literals without substitutions, and `Direction.*`,
`Relationship.*` and `AppKind.*` members. Anything else is an error with file and line. The C# analyser reads named
arguments only.

## Meta

The application represented by the codebase.

| Property | Required (TS) | Description |
| --- | --- | --- |
| `AppName` | yes | Name of the application. |
| `Description` | | What the application does. |
| `System` | | The system the application belongs to. |
| `Repository` | | VCS repository of the codebase. |
| `Owner` | | Owner of the application (a team). |
| `Technology` | | What it is built with, e.g. `.NET 10 / ASP.NET Core`. Shown in the container diagram. |
| `Kind` | | An `AppKind`. Picks the shape in diagrams. Unset is a generic application. |

| `AppKind` | Meaning |
| --- | --- |
| `Service` | A backend service |
| `WebApp` | A web application or single page app that users interact with |
| `Api` | An API that other applications call |
| `Worker` | A background worker or job that does not serve requests |
| `Database` | A data store |
| `Queue` | A message queue or broker |

```csharp
[assembly: Meta(AppName = "CheckoutApi", Description = "Handles payment for a cart", System = "HayaEcomm",
    Repository = "dburriss/haya-net", Technology = ".NET 10 / ASP.NET Core", Kind = AppKind.Api)]
```

```ts
meta({ appName: "CheckoutApi", description: "Handles payment for a cart", system: "HayaEcomm",
       technology: "Bun / TypeScript", kind: AppKind.Api });
```

## Responsibility

A use case, feature or capability of the application.

| Property | Required (TS) | Description |
| --- | --- | --- |
| `Description` | yes | What the responsibility is. |

```csharp
[Responsibility(Description = "Handles order processing")]
public class CreditCardPaymentUsecase { }
```

## Collaborator

Another application or system this application interacts with.

| Property | Required (TS) | Default | Description |
| --- | --- | --- | --- |
| `AppName` | yes | | Name of the collaborator application. |
| `System` | | the owning app's system (`External` for external relationships) | System the collaborator belongs to. |
| `Description` | | | What the collaborator does. |
| `Direction` | | `Downstream` | See below. |
| `Relationship` | | `Internal` | See below. |
| `Technology` | | | Technology of the **collaborator** (contrast `Meta.Technology`, which is this application's). |
| `Protocol` | | C#: `"HTTPS"` attribute default, but an unset value is analysed as `""` | How the two communicate, e.g. `HTTPS`, `MQTT`. |
| `DataDescription` | | | The data exchanged. |
| `Repository` | | | VCS repository of the collaborator. |
| `Owner` | | | Owner of the collaborator. |

```csharp
[Collaborator(AppName = "Shipping Service", System = "Shipping", Relationship = Relationship.Internal,
    Direction = Direction.Downstream, Description = "Handles shipping of orders", DataDescription = "Ship order")]
public class ShippingService { }
```

### `Direction`

| Value | Meaning |
| --- | --- |
| `Upstream` | The collaborator sends data to (calls) the application. |
| `Downstream` | The application sends data to the collaborator. |

### `Relationship`

| Value | Meaning |
| --- | --- |
| `Owned` | The team owns the collaborator. |
| `Shared` | The team shares the collaborator with other teams. |
| `Internal` | The collaborator is part of the same system. |
| `External` | The collaborator is part of a different system or organisation. |
| `InternalUser` | A user or person inside the organisation who uses or is used by the application. |
| `ExternalUser` | A user or person outside the organisation. |

## Custom attributes with defaults (C#)

Derive from `ResponsibilityAttribute`, `CollaboratorAttribute` or `MetaAttribute` to create your own attribute, and put
the matching defaults attribute on it to bake in values:

| Base attribute | Defaults attribute | Properties |
| --- | --- | --- |
| `Responsibility` | `ResponsibilityDefaults` | Same as `Responsibility`. |
| `Collaborator` | `CollaboratorDefaults` | Same as `Collaborator`. |
| `Meta` | `MetaDefaults` | Same as `Meta`. |

```csharp
[CollaboratorDefaults(AppName = "Payments", System = "Acme", Protocol = "gRPC")]
public class CallsPaymentsAttribute : CollaboratorAttribute { }

[CallsPayments(DataDescription = "Charge a card")]
public class CheckoutHandler { }
```

- Only properties you set on the defaults attribute are defaults; a property you leave out behaves as it does on the
  base attribute.
- A value at the usage site overrides the default. For a chain of custom attributes, a derived attribute's defaults
  override those of its base attribute.
- Custom attributes can be defined in the analysed source or in a compiled assembly (for example a NuGet package).
- A custom attribute without a defaults attribute is still recognised; it simply has no defaults.
- The defaults attribute is not itself reported as a responsibility, collaborator or meta.
