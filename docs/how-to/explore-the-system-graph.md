# How to explore the system graph

Use this when the level 2 Mermaid diagram has become too big to read. It writes one HTML page that starts small and
lets you open up the parts you care about. It assumes you have [annotated a project](../tutorials/getting-started-csharp.md).

## Generate the page

```bash
haya diagram -f html -o system.html ./Example.sln
```

The default output is `./diagram.html`. For TypeScript, use the Bun CLI with a `tsconfig.json` or its folder. Several
inputs (solutions or describe files) are drawn as one graph: see
[Document several systems together](document-multiple-systems.md).

The page is self-contained: open the file in a browser, attach it to a ticket or serve it from anywhere. It makes no network requests.
`-t` (the diagram type) does not apply; the page always covers everything.

## Read the page

- It starts with every system **collapsed** to one box with its app count. Dashed boxes are systems outside what is documented.
  Lines between boxes group all the relationships between the two systems.
- **Click** a node or line to read its details in the panel on the right.
- **Double-click a system** to open it: a dashed boundary appears around its apps. Double-click again to collapse it.
- **Double-click an app** to show the apps it connects to. The panel buttons do the same, and **Hide** removes one app.
- **Search** finds an app or system by name, description, technology or owner, even if it is hidden, and shows just that app.
- **Kind, Tier and Lifecycle** chips fade everything that does not match. **Fit** zooms to everything and **Reset** starts over.
- An app marked **undocumented** is known only because another app declares it as a collaborator. It has no `[Meta]` of its
  own, so the panel shows what the collaborator said about it. The overview lists them all, which makes them easy to find
  and annotate.

## Link to a view

The address can open systems or select a node: `system.html#open=ordering,billing&select=ordering_payments_api`. The ids are
the lower-case names with `_` between words, as in the Mermaid output.

## See also

- [Generate C4 diagrams](generate-diagrams.md) for the static Mermaid output.
- [ADR011](../adr/ADR011-interactive-html-explorer.md) for why it is built this way.
