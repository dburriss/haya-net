# haya-net

Haya is a tool and a set of attributes that help define what your application is doing and how it interacts with other applications or systems to achieve this.

Haya automates the generating of documentation and diagrams for a .NET codebase. 
It uses attributes to define concepts like Responsibilities and Collaborators.
The tool uses these concepts to generate CRC descriptions and C4 Level 1 diagrams.

See the *examples* folder of the [attribute annotations](examples/Example/HayaEcomm.cs) and the [generated CRC](examples/Example/CRC.md).
 
> **Note:** This currently only works for C# codebases.

This is not meant to be a replacement for good documentation practices, but rather a tool to help you get started, 
or at the least have a minimum level for a new joiner to start exploring the codebase.

## Terminology

- **CRC**: Component, Responsibilities, Collaborators. Taken from the idea of a CRC card.
- **System**: A collection of applications that work together to provide a service.
- **Application**: A single deployable piece of software.
- **Responsibility**: A usecase, feature, or capability that an application has.
- **Collaborator**: Another application or system that the application interacts with.

## Features

- Generate CRC markdown documentation, optionally with C4 diagrams.
- Generate diagrams for C4 Level 1 architecture, currently only in the Mermaid format.
- Generate a description of the solution in Backstage catalog format.

## Why?

Haya uses attributes to define metadata about your codebase. This puts the source of documentation next to the code it describes.
The closer the documentation is to the code, the more likely it is to be kept up to date.

This also allows for the automation of generating documentation and diagrams into not only human-readable formats but also machine-readable formats.

## Getting Started

1. Install the Haya CLI tool
```bash
dotnet tool install --global Haya.Tool
```
2. Add the Haya attributes package to your project.
```bash
dotnet add package Haya
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
haya crc --c4 ./Example.sln
```

## Usage

```bash
USAGE: haya [--help] [<subcommand> [<options>]]

SUBCOMMANDS:

    crc <options>         Generate Components, Responsibilities, and Collaborator documentation
    describe <options>    Generate Haya solution description
    backstage <options>   Generate Backstage catalog data
    diagram <options>     Generate diagram files

    Use 'haya <subcommand> --help' for additional information.

OPTIONS:

    --help                display this list of options.

```
