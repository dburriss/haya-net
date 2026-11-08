# @haya/annotations

Annotations for documenting a **TypeScript** codebase with Haya: the TypeScript counterpart of the C# `Haya`
package (`src/Haya`). The annotations do nothing at runtime. `haya` (see `src/Haya.Bun`) reads them statically
with the TypeScript compiler API and never executes your code.

```ts
import { Collaborator, Direction, meta, Relationship, Responsibility } from "@haya/annotations";

meta({ appName: "PaymentsApi", system: "Ordering", owner: "Ordering Team" });

@Responsibility({ description: "Handles order processing" })
@Collaborator({ appName: "Checkout", direction: Direction.Upstream, relationship: Relationship.Internal })
export class CreditCardPaymentUsecase {}
```

- `@Responsibility({...})` and `@Collaborator({...})` are class decorators and are repeatable.
- `meta({...})` is a top-level call, once per project (the equivalent of the C# assembly-level `Meta`).
- Values must be static: string literals, template literals without substitutions, and `Direction.*` /
  `Relationship.*` members. Otherwise `haya` reports an error with file and line.

Status: the package is private and consumed from source (`"@haya/annotations": "file:..."`). It is not published yet.
