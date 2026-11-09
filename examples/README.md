# Examples

This directory contains examples of how to use the Haya attributes to generate documentation and diagrams for a codebase.

| Language | Annotations | Generated CRC |
| --- | --- | --- |
| C# | [HayaEcomm.cs](dotnet/Example/HayaEcomm.cs) | [CRC.md](dotnet/Example/CRC.md) |
| TypeScript | [ecomm.ts](typescript/example/src/ecomm.ts) | [CRC.md](typescript/example/CRC.md) |

Both describe the same application. The TypeScript annotations are class decorators (`@Responsibility`, `@Collaborator`)
and a top level `meta({...})` call, imported from [`@haya/annotations`](../src/Haya.TypeScript). Both `CRC.md` files are
generated with `haya crc -cc` ([CLI reference](../docs/reference/cli.md)). For the C# example run, from `dotnet/Example`,
`haya crc -cc ../Example.sln` (paths in the output are relative to the working directory); for the TypeScript example run `bun install` once, then, from `typescript/example`:

```bash
bun run ../../../src/Haya.Bun/haya.ts crc -cc .
```

`bun run parity` in `src/Haya.Bun` runs both tools on these examples and diffs every output. The only substitution is
the source path (`HayaEcomm.cs` becomes `src/ecomm.ts`).

## Custom attributes and annotations

The same application again, with organisation-wide attributes (`AcmeMeta`, `CallsPayments`) that bake in values, so each
class only says what differs. A value written where the attribute is used overrides the default.

| Language | Definitions | Usage | Generated CRC |
| --- | --- | --- | --- |
| C# | [Attributes.cs](dotnet-custom-attributes/Acme.Attributes/Attributes.cs) | [Checkout.cs](dotnet-custom-attributes/Checkout/Checkout.cs) | [CRC.md](dotnet-custom-attributes/CRC.md) |
| TypeScript | [annotations.ts](typescript/custom-annotations/src/annotations.ts) | [checkout.ts](typescript/custom-annotations/src/checkout.ts) | [CRC.md](typescript/custom-annotations/CRC.md) |

In C# the defaults are `[CollaboratorDefaults]` and friends on a class that derives from the Haya attribute; in TypeScript
they are `defineCollaborator({...})` and friends. See the tutorials for [C#](../docs/tutorials/custom-attributes-csharp.md)
and [TypeScript](../docs/tutorials/custom-attributes-typescript.md). Generate each `CRC.md` as above, from
`dotnet-custom-attributes` with `haya crc -cc CustomAttributes.sln` and from `typescript/custom-annotations` with
`bun run ../../../src/Haya.Bun/haya.ts crc -cc .`. `bun run parity` compares these two as well (`Checkout.cs` becomes
`src/checkout.ts`).

## Multiple systems

Two more examples build a whole system from describe files only. Three apps (Storefront, Orders and Payments, each in
its own system) were described separately, in their own repositories; one `crc`, `diagram` and `backstage` run takes all
the describe files and produces system-wide output.

| Tool | Example | Describe files | Generated |
| --- | --- | --- | --- |
| `Haya.Tool` | [dotnet-multi-system](dotnet-multi-system) | JSON | [CRC.md](dotnet-multi-system/output/CRC.md), [system.mermaid](dotnet-multi-system/output/system.mermaid), [backstage.yaml](dotnet-multi-system/output/backstage.yaml) |
| `Haya.Bun` | [ts-multisystem](ts-multisystem) | YAML | [CRC.md](ts-multisystem/output/CRC.md), [system.mermaid](ts-multisystem/output/system.mermaid), [backstage.yaml](ts-multisystem/output/backstage.yaml) |

Run `generate.sh` in either folder. The two sets of describe files hold the same system, so the diagram and catalog are
identical. Orders also depends on a Shipping service that has no describe file, so it appears as a stub in the Backstage
catalog and as an extra system in the diagram. Describe files and solutions or projects can be mixed in one call.

`dotnet-multi` is a three-project solution used as a test fixture by `tests/Haya.Core.Tests`; it has no generated output.
