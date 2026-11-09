# @haya/annotations

Annotations for documenting a **TypeScript** codebase with Haya: the TypeScript counterpart of the C# `Haya`
package (`src/Haya`). The annotations do nothing at runtime. `haya` (see `src/Haya.Bun`) reads them statically
with the TypeScript compiler API and never executes your code.

```ts
import { AppKind, Collaborator, Direction, meta, Relationship, Responsibility } from "@haya/annotations";

meta({ appName: "PaymentsApi", system: "Ordering", owner: "Ordering Team", technology: "Bun / TypeScript", kind: AppKind.Api });

@Responsibility({ description: "Handles order processing" })
@Collaborator({ appName: "Checkout", direction: Direction.Upstream, relationship: Relationship.Internal })
export class CreditCardPaymentUsecase {}
```

- `@Responsibility({...})` and `@Collaborator({...})` are class decorators and are repeatable.
- `meta({...})` is a top-level call, once per project (the equivalent of the C# assembly-level `Meta`).
- Values must be static: string literals, template literals without substitutions, and `AppKind.*`, `Direction.*` /
  `Relationship.*` members. Otherwise `haya` reports an error with file and line.

To bake options into your own annotation, declare it with `defineResponsibility`, `defineCollaborator` or
`defineMeta`; options written at the usage site override the defaults:

```ts
export const CallsPayments = defineCollaborator({ appName: "Payments", protocol: "gRPC" });

@CallsPayments({ dataDescription: "Charge a card" })
export class CheckoutHandler {}
```

All options are in the [attribute reference](../../docs/reference/attributes.md); a walkthrough is in the
[TypeScript tutorial](../../docs/tutorials/getting-started-typescript.md). No special `tsconfig` settings are needed.

Status: the package is private and consumed from source (`"@haya/annotations": "file:..."`). It is not published yet.
