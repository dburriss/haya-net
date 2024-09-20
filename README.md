# haya-net
Haya automates the generating of documentation and diagrams for a .NET codebase. 
It uses attributes to define concepts like Responsibilities and Collaborators.
The tool uses these concepts to generate CRC descriptions and C4 Level 1 diagrams.

See the *examples* folder of the [attribute annotations](examples/Example/HayaEcomm.cs) and the [generated CRC](examples/Example/CRC.md).
 
> **Note:** This currently only works for C# codebases.

This is not meant to be a replacement for good documentation practices, but rather a tool to help you get started, 
or at the least have a minimum level for a new joiner to start exploring the codebase.

## Getting Started

1. Install the Haya CLI tool
```bash
dotnet tool install --global Haya.Tool
```
2. Add the Haya attributes package to your project.
```bash
dotnet add package Haya.Core
```
3. Run the Haya CLI tool
```bash
haya --help
```

## Usage

1. Add the Haya attributes to your codebase. See [Attributes docs](Attributes.md) for more information. 

```csharp
using Haya;
[assembly:Meta(AppName = "CheckoutApi", Description = "Handles processing payment for a shopping cart", System = "HayaEcomm")]

namespace Example;

[Responsibility(Description = "Handles order processing")]
public class CreditCardPaymentUsecase
{ }

[Collaborator(AppName = "Checkout",
    Relationship = Relationship.Internal,
    Direction = Direction.Upstream,
    System = "WebShop",
    Description = "Handles cart checkout for a customer",
    DataDescription = "Request payment")]
public class PaymentsController
{ }
```
Running the Haya CLI tool will generate a CRC document and a C4 Level 1 diagram.
```bash
haya crc --format md --c4 ./Example.sln
```

```bash
USAGE: haya crc [--help] [--outputpath <output path>] [--format <md|json>] [--c4] <path to sln>

PATHTOSLN:

    <path to sln>         Path to solution file

OPTIONS:

    --outputpath, -o <output path>
                          Path to output folder or file (default: ./CRC.md)
    --format, -f <md|json>
                          Output format: md | json (default: md)
    --c4, -c              Include diagram (markdown only). -c for C4 Level 1, -cc for C4 Level 2
    --help                display this list of options.

USAGE: haya [--help] [<subcommand> [<options>]]

SUBCOMMANDS:

    crc <options>         Generate Components, Responsibilities, and Collaborator data

    Use 'haya <subcommand> --help' for additional information.

OPTIONS:

    --help                display this list of options.
```
