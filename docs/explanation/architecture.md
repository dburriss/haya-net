# Architecture

Why Haya is built as several projects, and how the C# and TypeScript tools stay equivalent.

```
 C# source ──▶ Haya.Roslyn ──┐
                             ├──▶ Haya.Core (model + rendering) ──▶ crc / describe / diagram / backstage / datadog
 TS source ──▶ TS analyser ──┘
```

- **Haya.Core** is language-neutral F#: the descriptor model, merging, and all renderers (Markdown, Mermaid, JSON/YAML,
  Backstage). It has no Roslyn dependency ([ADR003](../adr/ADR003-split-roslyn-from-core.md)).
- The interactive html diagram is also rendered in Core: `Graph.fs` resolves apps into systems, apps and edges, and
  `Explore.fs` embeds that graph in a page whose viewer (a Cytoscape.js app in **Haya.Viewer**) is bundled into the committed,
  generated `ViewerAssets.fs`, so both stacks emit the same page ([ADR011](../adr/ADR011-interactive-html-explorer.md)).
- **Haya.Roslyn** analyses C# with Roslyn and turns attributes into Core descriptors. **Haya.Tool** is its Argu CLI.
- **Haya.Bun** is the TypeScript CLI. Its analyser reads decorators and `meta()` with the TypeScript compiler API without
  running your code ([ADR005](../adr/ADR005-typescript-annotations-and-analyser.md)). Core is compiled to TypeScript with
  Fable, so rendering is the same code on both stacks ([ADR004](../adr/ADR004-fable-compatible-core-libraries.md),
  [ADR002](../adr/ADR002-bun-typescript-runtime.md)).
- **Haya** (NuGet) and **Haya.TypeScript** (`@haya/annotations`) are the annotation packages users depend on.

Parity is enforced: `bun run parity` runs both tools on the examples and diffs every output. Commands are separate per
output format ([ADR001](../adr/ADR001-separate-commands-per-format.md)).

All decisions are in the [ADR index](../adr/README.md). To work on the code see [Contributing](../contributing.md).
