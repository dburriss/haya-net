# ADR011: Interactive html graph explorer

Date: 2026-11-10  
Status: Accepted  
Author: dburriss

### Context
The C4 diagrams are static Mermaid. Level 2 (and anything below it) does not scale: a system with dozens of apps
draws as one unreadable picture. We want to explore a large system by starting small and expanding what is of interest.

### Decision
- A new format of the existing command, `diagram -f html` ([ADR001](ADR001-separate-commands-per-format.md): formats of
  a diagram live under `diagram`). It writes **one self-contained page**: no network, no server, works from `file://`,
  and a CSP meta tag forbids everything but its own inline script and style. The diagram type (`-t`) does not apply: the
  explorer always covers the whole graph.
- **A resolved graph in `Haya.Core` (`Graph.fs`)** is the model the page reads. It follows the rules of the level 2
  diagram: systems that hold our apps are boundaries around their apps, other systems are single nodes, and a collaborator
  in one of our systems with no `[Meta]` of its own becomes a node flagged as undocumented. A collaborator describes its
  target node as well as the edge: `AppName`, `System`, `Description`, `Technology`, `Owner`, `Repository` and
  `Relationship` are the node's; `Direction`, `Protocol` and `DataDescription` are the edge's. A node with its own `[Meta]`
  wins over the same node declared by a collaborator.
- The graph is embedded as JSON (`Encode.graph`, version `0.1`) in a `<script type="application/json">` block.
  `Serializer.toScriptSafeJson` escapes `<` and U+2028/9 so no value can end the script element. It is a graph document of
  its own rather than describe JSON, which is shaped for round-tripping descriptors and would make the page redo the
  resolution. It leaves out language and component sources, which the two tools stamp differently.
- **The viewer is Cytoscape.js (+ fcose) in `src/Haya.Viewer`.** It was chosen over d3 (smaller, but boundaries, edge labels
  and incremental layout would be ours to write), Sigma, vis-network, React Flow and ELK (no nested nodes, framework
  weight, or a layout that moves everything on each change). The cost is ~0.5 MB per page and a canvas, which is why all
  node detail lives in a DOM side panel, which is also what screen readers use. fcose is unmaintained, so versions are pinned.
- **The page starts collapsed**: every own system is one box with an app count, plus the other systems. Double-click (or
  the panel) opens a system, double-click on an app shows the apps it connects to, search reveals any node, and filters (all on
  at the start) fade the apps of a value that is switched off. What is drawn follows from a set of shown apps. While a system is open, lines that do not touch it and systems or apps only those lines reach are muted (faded but still clickable), so its neighbourhood stands out. There is one
  line per pair of drawn nodes, with an arrowhead per direction and the number of relationships it stands for; edges
  between hidden apps roll up into the line between their systems, and the text lives in the panel (`A → B` with a bullet per
  relationship). Nodes are small: a name and glyphs for kind, tier and lifecycle. The filter buttons, glyph plus name, are
  the legend for those; a small legend covers system, external, undocumented and the lines.
- **How both stacks emit identical pages:** `gen-viewer.ts` bundles the app and writes `src/Haya.Core/ViewerAssets.fs`, which
  is committed, so `dotnet build` and Fable need no Node. Embedded resources do not work under Fable, and a template file
  read at runtime would need a second mechanism on the Bun side. The generated file is a list of ~1.5 KB string chunks
  (a ~600 KB literal in one piece is avoided); a spike showed .NET and Fable produce byte-identical output for it, with
  quotes, backslashes, `${}`, `</script>` and astral characters. `bun run check` in `src/Haya.Viewer` fails when the
  committed file is stale.

### Consequences
- Changing the viewer means running `bun run gen` in `src/Haya.Viewer` and committing `ViewerAssets.fs`.
- `bun run parity` covers the page (`diagram.html`, `multi-diagram.html`). Parity proves the two tools agree, not that the
  page works: that was checked in a browser.
- The graph builder follows the level 2 rules but does not yet replace them: `Diagram.sprintMermaidC4L2` still has its own
  copy of the resolution. Folding it onto `Graph.ofApps` is possible later.
- Cytoscape and fcose are MIT and their notices are kept in a banner in the page.
