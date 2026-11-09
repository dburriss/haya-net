<p align="center">
  <img src="docs/images/haya-logo.svg" alt="haya" width="280">
</p>

<h3 align="center">Living architecture docs, generated from your code.</h3>

<p align="center">
  Haya reads small annotations you put next to your code (<code>Responsibility</code>, <code>Collaborator</code>, <code>Meta</code>)
  and generates CRC documents, C4 diagrams and a Backstage catalog, for <b>C#</b> and <b>TypeScript</b> codebases.
</p>

<p align="center">
  <a href="https://github.com/dburriss/haya-net/actions/workflows/build.yml"><img src="https://github.com/dburriss/haya-net/actions/workflows/build.yml/badge.svg" alt="Build and test"></a>
  <a href="https://www.nuget.org/packages/Haya.Tool"><img src="https://img.shields.io/nuget/v/Haya.Tool?logo=nuget&label=Haya.Tool" alt="NuGet Haya.Tool"></a>
  <a href="https://www.nuget.org/packages/Haya"><img src="https://img.shields.io/nuget/v/Haya?logo=nuget&label=Haya" alt="NuGet Haya"></a>
  <a href="https://github.com/dburriss/haya-net/blob/main/LICENSE"><img src="https://img.shields.io/github/license/dburriss/haya-net" alt="License"></a>
</p>

## Get started

Install the tool and the attributes package (alpha, hence `--prerelease`; .NET 10 SDK required):

```bash
dotnet tool install --global Haya.Tool --prerelease
dotnet add package Haya --prerelease
```

For TypeScript, see the [TypeScript tutorial](docs/tutorials/getting-started-typescript.md).

Then annotate your code and run one command:

```csharp
[assembly: Meta(AppName = "CheckoutApi", System = "HayaEcomm", Description = "Handles payment for a cart")]

[Responsibility(Description = "Handles order processing")]
public class CreditCardPaymentUsecase { }
```

```bash
haya crc -cc ./Example.sln
```

Start with the tutorial for your stack:

- [Getting started with C#](docs/tutorials/getting-started-csharp.md)
- [Getting started with TypeScript](docs/tutorials/getting-started-typescript.md)

See the annotated [C# example](examples/dotnet/Example/HayaEcomm.cs) and its [generated CRC](examples/dotnet/Example/CRC.md).

## What it does

- **CRC documents.** Components, Responsibilities and Collaborators as Markdown, optionally with embedded diagrams. See the [CLI reference](docs/reference/cli.md).
- **C4 diagrams.** Level 1 (system context) and level 2 (containers) as Mermaid. See [Generate C4 diagrams](docs/how-to/generate-diagrams.md).
- **Backstage catalog.** One component per application with dependencies. See [Publish to Backstage](docs/how-to/publish-to-backstage.md).
- **Describe files.** A JSON/YAML description of an application that every other command accepts as input. See the [describe format](docs/reference/describe-format.md).
- **Many systems at once.** Merge several codebases, C# and TypeScript mixed, into one picture. See [Document several systems together](docs/how-to/document-multiple-systems.md).
- **Same output on both stacks.** The C# tool (`Haya.Tool`) and the TypeScript tool (`Haya.Bun`) share one rendering core. See [Architecture](docs/explanation/architecture.md).

Haya is not a replacement for good documentation practices. It keeps a minimum level of documentation next to the code,
so it stays current and a new joiner has somewhere to start exploring.

## Documentation

Organised by [Diataxis](https://diataxis.fr/), starting at the [docs index](docs/index.md):

| I want to... | Go to |
| --- | --- |
| Learn Haya by doing | [Tutorials](docs/tutorials/getting-started-csharp.md) |
| Get a specific job done | [How-to guides](docs/index.md#how-to-guides-get-a-specific-job-done) |
| Look up a flag or attribute | [CLI](docs/reference/cli.md), [Attributes](docs/reference/attributes.md) |
| Understand why it works this way | [Concepts](docs/explanation/concepts.md), [ADRs](docs/adr/README.md) |

More: [examples](examples/README.md) · [changelog](CHANGELOG.md) · [contributing](docs/contributing.md)

## License

[MIT](LICENSE)
