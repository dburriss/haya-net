# Tutorial: document a C# project with Haya

In this tutorial you annotate a small .NET project and generate a CRC document with two C4 diagrams from it.
It takes about ten minutes. You need the .NET SDK (see [`global.json`](../../global.json) for the version used by this repository).

## 1. Install the tool

```bash
dotnet tool install --global Haya.Tool --prerelease
haya --help
```

`--prerelease` is needed while Haya is in alpha (`0.0.4-alpha2`). Drop it once a stable version is published.

## 2. Add the attributes to your project

In the project you want to document, add the attributes package:

```bash
dotnet add package Haya --prerelease
```

## 3. Describe the application

Add this to any `.cs` file, for example `Haya.cs`:

```csharp
using Haya;
[assembly: Meta(
    AppName = "CheckoutApi",
    Description = "Handles processing payment for a shopping cart",
    System = "HayaEcomm",
    Technology = ".NET 10 / ASP.NET Core",
    Kind = AppKind.Api)]

namespace Example;

[Responsibility(Description = "Handles order processing")]
public class CreditCardPaymentUsecase { }

[Collaborator(AppName = "Checkout",
    Relationship = Relationship.Internal,
    Direction = Direction.Upstream,
    System = "WebShop",
    Description = "Handles cart checkout for a customer",
    DataDescription = "Request payment")]
public class PaymentsController { }
```

## 4. Generate the document

From the folder with your solution:

```bash
haya crc -cc ./YourApp.sln
```

Open the new `CRC.md`. You will see a table of responsibilities, a table of collaborators, and two Mermaid diagrams:
the systems and how they relate (C4 level 1), and the applications inside each system (level 2).

Compare with the [generated CRC](../../examples/dotnet/Example/CRC.md) for the [example project](../../examples/dotnet/Example/HayaEcomm.cs).

## Where next

- Add more detail with the [attribute reference](../reference/attributes.md).
- [Generate only the diagrams](../how-to/generate-diagrams.md) or [publish to Backstage](../how-to/publish-to-backstage.md).
- Understand the vocabulary in [Concepts](../explanation/concepts.md).
