# Tutorial: document a TypeScript project with Haya

In this tutorial you annotate a small TypeScript project and generate a CRC document with two C4 diagrams from it.
You need [Bun](https://bun.sh) and the .NET SDK (the TypeScript tool is built from F# with Fable).

> **Install note:** `@haya/annotations` and the Bun CLI are not published to npm yet. This tutorial uses a clone of
> this repository.

## 1. Get the tool

```bash
git clone https://github.com/dburriss/haya-net.git
cd haya-net && dotnet tool restore
cd src/Haya.Bun && bun install && bun run build
```

This produces `src/Haya.Bun/dist/haya`. From here on, `haya` means that binary.

## 2. Add the annotations to your project

In your project's `package.json`, add the package from your clone:

```json
"dependencies": { "@haya/annotations": "file:../path/to/haya-net/src/Haya.TypeScript" }
```

then run `bun install`. Your `tsconfig.json` needs nothing special; compare the
[example's](../../examples/typescript/example/tsconfig.json).

## 3. Describe the application

```ts
import { AppKind, Collaborator, Direction, meta, Relationship, Responsibility } from "@haya/annotations";

meta({
  appName: "CheckoutApi",
  description: "Handles processing payment for a shopping cart",
  system: "HayaEcomm",
  technology: "Bun / TypeScript",
  kind: AppKind.Api,
});

@Responsibility({ description: "Handles order processing" })
export class CreditCardPaymentUsecase {}

@Collaborator({
  appName: "Checkout",
  relationship: Relationship.Internal,
  direction: Direction.Upstream,
  system: "WebShop",
  description: "Handles cart checkout for a customer",
  dataDescription: "Request payment",
})
export class PaymentsController {}
```

## 4. Generate the document

From your project folder (the one with `tsconfig.json`):

```bash
haya crc -cc .
```

Open `CRC.md`: responsibilities, collaborators and two Mermaid diagrams. Compare with the
[generated CRC](../../examples/typescript/example/CRC.md) for the [example](../../examples/typescript/example/src/ecomm.ts).
Run `haya` from the project folder so the component paths in the output are relative to it.

## Where next

- Every option is in the [attribute reference](../reference/attributes.md).
- [Generate only the diagrams](../how-to/generate-diagrams.md) or [publish to Backstage](../how-to/publish-to-backstage.md).
- The C# tool produces the same output for equivalent code: see [Architecture](../explanation/architecture.md).
