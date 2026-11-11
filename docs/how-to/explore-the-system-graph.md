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

To see one first, open the [live example](https://dburriss.github.io/haya-net/), built in CI from [examples/org-explorer](../../examples/org-explorer).

## Read the page

- It starts with every system **collapsed** to one box with its app count. Dashed boxes are systems outside what is
  documented. Between two boxes there is **one line**: an arrowhead for each direction the relationships go, and a number
  for how many relationships it stands for.
- **Colour flags the heavy ones.** A system's border and the line between two systems stay plain until they get complex, then turn
  yellow, orange and finally a deep red for the most complex. A system is measured by its apps plus its relationships (its panel
  shows the sum); a line by how many relationships there are between the two systems. See
  [Complexity colours](../explanation/complexity-colours.md) for what is counted, the numbers and what the colour does not tell you.
- **Click** a node or line to read its details in the panel on the right. Clicking a node also **mutes** everything with no
  relationship to it: the lines that do not touch it, and the systems and apps only those lines reach. Muted items stay
  clickable. Click the background to clear the selection. A node shows its name and small glyphs: the
  kind, the tier and a lifecycle dot. The **Kind**, **Tier** and **Lifecycle** buttons at the top are the legend for those
  glyphs, and the **Legend** at the bottom left explains the rest.
- An app with its own `[Meta]` lists its **Responsibilities** and its **Components**: each class that carries a
  `[Responsibility]` or `[Collaborator]`, with the source file it is in.
- A system or app lists its **Inbound** and **Outbound** relationships. A line lists what it carries as bullets,
  `Publish events [AMQP]`, under a header such as `Ordering → Billing`, or `Ordering ↔ Billing` with a list for each
  direction.
- **Double-click a system** to open it: a dashed boundary appears around its apps. While nothing is selected, the open
  systems are the focus for muting (when several are open, what relates to any of them stays). Double-click again to collapse it.
- A system's app list is sorted by kind, then by name. In it, an app that is on the graph has its kind glyph before its name; an app that is in the system
  but not drawn yet has a **closed eye** instead. Click its name to add just that app (screen readers announce it as "hidden").
- **Esc** deselects, so the panel returns to the overview and the muting that came from the selection lifts. In the search box
  Esc closes the search first. Esc does not collapse systems: while one is open, the rest stays muted until you collapse it or press **r**.
- **Enter or Space** does the same as a double-click on the selected node: open or collapse a system, open or close an app,
  or show the apps an app connects to. It is ignored while a button, link or text field has focus, so those keep their usual keys.
- **Double-click an app** with annotated classes to open it: its classes are drawn inside it, the apps it connects to are shown,
  and each line goes to the class the collaborator is on. A line to another of your apps goes to its classes that name this
  app back, or to the app's box when none do. Double-click again (or **Hide classes**) to close it. Click a class for its source
  file, responsibilities and relationships; the class names in an app's panel open it too.
- **Double-click an app** without classes to show the apps it connects to. The panel buttons do the same, and **Hide** removes one app.
- **Search** finds an app or system by name, description, technology or owner, even if it is hidden, and shows just that app. Press **/** to jump to the search box from anywhere on the page (it is ignored while you are typing in a field), and **Esc** to leave it.
- The **Kind, Tier and Lifecycle** buttons all start on. Switch one off to fade the apps that have that value (and the lines
  to them); switch it back on to restore them. **Fit** zooms to everything and **Reset** (or **r**) starts over.
- **Theme** cycles Auto (follows your system), Light and Dark. The choice is remembered in the browser when it allows storage.
- An app with a **?** and a dashed outline is **undocumented**: it is known only because another app declares it as a
  collaborator and has no `[Meta]` of its own, so the panel shows what the collaborator said about it. The overview lists them
  all, which makes them easy to find and annotate.
- A **person** (a collaborator with `Kind` `Person`) is a rounded node with a head-and-shoulders glyph, dashed when it is
  outside the organisation. People are always on the graph, belong to no system, and are not marked undocumented. Double-click
  one to show the apps it uses. They are listed in the overview.

## Link to a view

The address can open systems or select a node: `system.html#open=ordering,billing&select=ordering_payments_api`. The ids are
the lower-case names with `_` between words, as in the Mermaid output.

## Know the limits

- It needs JavaScript, so a wiki or ticket system that strips scripts will show nothing. Open the file in a browser instead.
- The page is about 0.6 MB before your data, because the graph library is inside it.
- The graph is drawn on a canvas, so a screen reader cannot read it. The side panel, the search box and the lists in the
  overview are ordinary page content and work with a keyboard; use those.
- Very dense systems are still hard to read when many boxes are open at once. Open one system at a time and select a node: the
  rest is muted.
- Positions are not saved and change a little when the graph grows. There is no export; use `-f mermaid` for a static picture.

## See also

- [Complexity colours](../explanation/complexity-colours.md) for what the border and line colours mean.
- [Graph format](../reference/graph-format.md) for the data the page embeds.

- [Generate C4 diagrams](generate-diagrams.md) for the static Mermaid output.
- [ADR011](../adr/ADR011-interactive-html-explorer.md) for why it is built this way.
