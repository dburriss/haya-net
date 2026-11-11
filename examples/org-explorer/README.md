# Org-wide explorer example

Nine systems of an imaginary company (Storefront, Ordering, Billing, Catalog, Warehouse, Shipping, Notifications, Identity
and Analytics) shown as one graph, with the people who use them: a customer of the storefront and a picker in the
warehouse (collaborators with `kind: Person`). It is too big for a readable level 2 Mermaid diagram, which is what the
[interactive explorer](https://dburriss.github.io/haya-net/) is for.

Each team publishes the describe file for its own system (`haya describe -o describe/orders.yaml Orders/Orders.sln` in its
repository). Someone merges them centrally: [describe/](describe) holds one file per system, and a single `haya diagram`
run reads them all. JSON and YAML are mixed on purpose, since teams pick their own; both are read in one call.
Ordering, Billing and Catalog published theirs with `haya describe --detail members`, so their files also hold each class's
constructors, public members and dependencies on the app's other classes (a `components` array per app). A dependency's
`via` names the unannotated classes and interfaces in between, as the analyser would find them in the code. The other teams did not, and a merge of both kinds is fine.
Every app's code is C# (`.cs`) or TypeScript (`.ts`), the two languages Haya analyses, and the member types are written in
the class's own language (`Task<…>` or `Promise<…>`).

The live page is built in CI from this folder (`.github/workflows/pages.yml`) on every push to `main` that touches `src/` or
here. For the how-to, see [Document several systems together](../../docs/how-to/document-multiple-systems.md).

## Run it

```bash
./generate.sh
```

It builds `Haya.Tool` and writes `output/explorer.html` (git-ignored; open it in a browser), plus the static
`system.mermaid` and `containers.mermaid` for comparison.

With [mise](https://mise.jdx.dev), from anywhere in the repository:

```bash
mise run org-explorer:generate   # same as ./generate.sh
mise run org-explorer:open       # opens output/explorer.html in your default browser
```

## What to try

- Open **Catalog**: double-click it to open the system into its apps.
- Search for `legacy` to reveal the hidden `LegacyErp` app and what talks to it.
- Double-click **OrderService** (in Ordering) to open it into its classes, then click the `OrderService` class: its panel
  shows the **Constructor**, the client classes it **Depends on** (click one to select it; the dashed lines between the
  opened classes are the same dependencies), the classes it is **Used by** and its **Members**.
- Double-click **CheckoutApi** (in Ordering) and click the `CheckoutApi` class: **Depends on** shows `PaymentsApiClient`
  *via CheckoutHandler → IPaymentPort*, a chain through classes without a Haya attribute (an interface among them). The other
  chains are on `OrderService` (via `PricingCalculator`, and via the `IStockReservations` interface), `PaymentGateway`
  (Stripe and Visa clients via `CardRouter`), `InvoiceWorker` (via `InvoiceMailer`) and, three deep, `CatalogApi` in Catalog
  (`StockLevelsClient` via `ProductQuery → AvailabilityService`). See
  [ADR018](../../docs/adr/ADR018-component-dependencies.md).
- Select **Customer** (a person outside the organisation, dashed) to see the apps it uses; double-click to show them.
- Switch a **Kind**, **Tier** or **Lifecycle** filter off and watch matching apps fade.
- Press **/** to jump to search, and **r** to reset the view.
