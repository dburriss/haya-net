# Tutorial: create your own TypeScript annotations with defaults

In this tutorial you build a `@CallsPayments` decorator that already knows which application it points at, so you stop
repeating the same options on every class. It takes about ten minutes. You need a TypeScript project that already uses
Haya (see [Getting started with TypeScript](getting-started-typescript.md)).

## 1. Notice the repetition

Suppose many classes call the same payments service:

```ts
@Collaborator({ appName: "Payments", system: "Acme", protocol: "gRPC", dataDescription: "Charge a card" })
export class CheckoutHandler {}

@Collaborator({ appName: "Payments", system: "Acme", protocol: "gRPC", dataDescription: "Refund a payment" })
export class RefundHandler {}
```

Only `dataDescription` differs.

## 2. Define the annotation

TypeScript has no attribute classes to derive from, so you declare your own annotation with `defineCollaborator` and
the options to bake in. Put it in any file, for example `src/annotations.ts`:

```ts
import { defineCollaborator } from "@haya/annotations";

export const CallsPayments = defineCollaborator({ appName: "Payments", system: "Acme", protocol: "gRPC" });
```

It has to be a `const` initialised directly with the `define...` call, and the options have to be static (the same
rules as for the annotations themselves), because Haya reads it from the source without running your code.
There is one function for each annotation: `defineResponsibility`, `defineCollaborator` and `defineMeta`.

## 3. Use it

```ts
import { CallsPayments } from "./annotations";

@CallsPayments({ dataDescription: "Charge a card" })
export class CheckoutHandler {}

@CallsPayments({ dataDescription: "Refund a payment" })
export class RefundHandler {}
```

Because `appName` is already a default, it is no longer required: your editor only asks for what is missing.
When the defaults cover everything required you can even write `@CallsPayments()`.

## 4. Generate the document

```bash
haya crc .
```

In `CRC.md` both classes appear as collaborators of `Payments` in `Acme` over `gRPC`, each with its own data
description. The options written on the class are used exactly as before; the defaults only fill in what you left out.

## 5. Override a default

An option written where you use the annotation wins over the default:

```ts
@CallsPayments({ protocol: "HTTPS", dataDescription: "Payment webhook" })
export class WebhookController {}
```

## 6. Do the same for meta

`defineMeta` is useful when every application in your organisation shares an owner and system:

```ts
export const acmeMeta = defineMeta({ owner: "Acme Team", system: "Acme", technology: "Bun / TypeScript" });
```

```ts
import { AppKind } from "@haya/annotations";
import { acmeMeta } from "./annotations";

acmeMeta({ appName: "CheckoutApi", kind: AppKind.Api });
```

Call it once, at the top level, in place of `meta()`. A project can only have one meta, so a custom meta and a plain
`meta()` call together are an error.

## Share the annotations

The file with your `define...` calls can live in a workspace package that your applications depend on: Haya analyses
the source of linked workspace packages. A package that is installed from a registry with only compiled `.js` and
`.d.ts` files does not work, because TypeScript declaration files do not keep the options.

## Where next

- The [annotation reference](../reference/attributes.md#custom-annotations-with-defaults-typescript) has the details.
- [Why defaults are declared statically](../adr/ADR008-custom-attribute-defaults.md).
- The C# equivalent: [Custom C# attributes](custom-attributes-csharp.md).
