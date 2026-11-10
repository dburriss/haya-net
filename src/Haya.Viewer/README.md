# Haya.Viewer

The interactive graph explorer behind `haya diagram -f html`: a small Cytoscape.js app, bundled into one HTML template and
committed as `src/Haya.Core/ViewerAssets.fs` so that both the .NET tool and the Fable-compiled Bun CLI emit the same page.
Why it is built this way: [ADR011](../../docs/adr/ADR011-interactive-html-explorer.md). How to use the result:
[Explore the system graph](../../docs/how-to/explore-the-system-graph.md).

| File | What |
| --- | --- |
| `index.html` | Page template. `@@HAYA_TITLE@@` and `@@HAYA_DATA@@` are filled in at render time by `Haya.Core/Explore.fs`; css and js are inlined by the generator. |
| `app.js` | The viewer. The graph is read from the embedded JSON ([graph format](../../docs/reference/graph-format.md)); everything derived from it is written as text, never as html. |
| `app.css` | Styles, light and dark. |
| `gen-viewer.ts` | Bundles the above with Cytoscape and fcose (minified, licence notices kept) and writes `ViewerAssets.fs` as chunked string literals. |

```bash
bun install
bun run gen      # write src/Haya.Core/ViewerAssets.fs
bun run check    # fail if it is out of date
```

After `gen`, rebuild and run `bun run parity` in `src/Haya.Bun`; commit the regenerated file with the change.

## How it works

What is drawn follows from a set of shown apps. A system with shown apps is a boundary around them, one without is a single
box; lines between hidden apps roll up into one line between their systems. Selecting a node mutes what has no
relationship with it. See `sync`, `desired` and `applyFocus` in `app.js`.

## Poking at it

- `window.__haya` is the Cytoscape instance.
- `#open=system_id,other&select=node_id` in the address opens systems and selects a node (ids as in the
  [graph format](../../docs/reference/graph-format.md)).
- Open a generated page straight from disk: `haya diagram -f html -o /tmp/x.html ./Example.sln`.

Page content is under a CSP that allows only its own inline script and style, with no network access. Keep it that way: no
CDN links, no `innerHTML`.
