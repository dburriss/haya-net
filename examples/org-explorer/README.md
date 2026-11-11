# Org-wide explorer example

Nine systems of an imaginary company (Storefront, Ordering, Billing, Catalog, Warehouse, Shipping, Notifications, Identity
and Analytics) shown as one graph, with the people who use them: a customer of the storefront and a picker in the
warehouse (collaborators with `kind: Person`). It is too big for a readable level 2 Mermaid diagram, which is what the
[interactive explorer](https://dburriss.github.io/haya-net/) is for.

Each team publishes the describe file for its own system (`haya describe -o describe/orders.yaml Orders/Orders.sln` in its
repository). Someone merges them centrally: [describe/](describe) holds one file per system, and a single `haya diagram`
run reads them all. JSON and YAML are mixed on purpose, since teams pick their own; both are read in one call.

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
- Select **Customer** (a person outside the organisation, dashed) to see the apps it uses; double-click to show them.
- Switch a **Kind**, **Tier** or **Lifecycle** filter off and watch matching apps fade.
- Press **/** to jump to search, and **r** to reset the view.
