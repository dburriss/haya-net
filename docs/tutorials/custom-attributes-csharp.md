# Tutorial: create your own C# attributes with defaults

In this tutorial you build a `[CallsPayments]` attribute that already knows which application it points at, so you stop
repeating the same values on every class. It takes about ten minutes. You need a C# project that already uses Haya
(see [Getting started with C#](getting-started-csharp.md)).

## 1. Notice the repetition

Suppose many classes call the same payments service:

```csharp
[Collaborator(AppName = "Payments", System = "Acme", Protocol = "gRPC", DataDescription = "Charge a card")]
public class CheckoutHandler { }

[Collaborator(AppName = "Payments", System = "Acme", Protocol = "gRPC", DataDescription = "Refund a payment")]
public class RefundHandler { }
```

Only `DataDescription` differs.

## 2. Define the attribute

Add a class that derives from `CollaboratorAttribute`, and put `[CollaboratorDefaults]` on it with the values to bake in:

```csharp
using Haya;

[CollaboratorDefaults(AppName = "Payments", System = "Acme", Protocol = "gRPC")]
public class CallsPaymentsAttribute : CollaboratorAttribute { }
```

`CollaboratorDefaults` accepts the same properties as `Collaborator`. There is one `*Defaults` attribute for each
Haya attribute: `ResponsibilityDefaults`, `CollaboratorDefaults` and `MetaDefaults`.

## 3. Use it

```csharp
[CallsPayments(DataDescription = "Charge a card")]
public class CheckoutHandler { }

[CallsPayments(DataDescription = "Refund a payment")]
public class RefundHandler { }
```

## 4. Generate the document

```bash
haya crc ./YourApp.sln
```

In `CRC.md` both classes appear as collaborators of `Payments` in `Acme` over `gRPC`, each with its own data description.
The values written on the class are used exactly as before; the defaults only fill in what you did not write.

## 5. Override a default

A value written where you use the attribute wins over the default:

```csharp
[CallsPayments(Protocol = "HTTPS", DataDescription = "Payment webhook")]
public class WebhookController { }
```

`WebhookController` is reported with protocol `HTTPS`; everything else still comes from the defaults.

## 6. Do the same for the assembly Meta

Defaults work for `Meta` too, which is useful when every application in your organisation shares an owner and system:

```csharp
[MetaDefaults(Owner = "Acme Team", System = "Acme", Technology = ".NET 10")]
public class AcmeMetaAttribute : MetaAttribute { }
```

```csharp
[assembly: AcmeMeta(AppName = "CheckoutApi", Kind = AppKind.Api)]
```

If the attribute lives in a namespace, qualify it in the assembly attribute, as in `[assembly: Acme.AcmeMeta(...)]`:
assembly attributes are written before any `namespace` declaration.

## Share the attributes

The attribute classes can live in a library that you publish as a NuGet package and reference from your applications.
Haya reads the defaults from the compiled attribute, so the package does not have to be part of the solution you analyse.

## Where next

- The [attribute reference](../reference/attributes.md) lists every property and the rules for custom attributes.
- A complete [example](../../examples/dotnet-custom-attributes) with its [generated CRC](../../examples/dotnet-custom-attributes/CRC.md).
- [Why defaults are separate attributes](../adr/ADR008-custom-attribute-defaults.md).
