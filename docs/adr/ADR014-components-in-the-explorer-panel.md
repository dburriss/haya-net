# ADR014: Components in the explorer panel

Date: 2026-11-11  
Status: Accepted, extended by ADR015  
Author: dburriss

### Context
Haya documents systems and apps (C4 levels 1 and 2) but not what is inside an app. Every `[Responsibility]` and
`[Collaborator]` descriptor already carries the class it sits on (`ComponentName`) and where that class is (`ComponentSource`),
set by both analysers, but the resolved graph (`Graph.Node`) kept only the responsibility descriptions. The explorer could not
say which classes make up an app or where to find them.

ADR011 left component sources out of the graph because the two tools stamp them differently (`HayaEcomm.cs` against
`src/ecomm.ts`). The parity check already maps the TypeScript path to the .NET one for every output, including the html page,
so that is no reason to keep them out.

### Decision
- **A first, minimal level 3: list the classes in the explorer's side panel only.** An app's panel gets a **Components**
  section with each class name and its source beneath it. Nothing is drawn on the canvas.
- **`Graph.Node` gets `Components`**, a list of `{ Name; Source }`, filled for apps with their own `[Meta]` only. There is one
  entry per distinct class name and source: a class with both a responsibility and a collaborator, or with several of either,
  appears once. Entries are sorted by name, then source. A descriptor without a class name is not a component. `Responsibilities`
  is unchanged.
- **Graph format `0.3`** adds the optional `components` field to app nodes (`[{ "name", "source" }]`, left out when empty, as
  `source` is when empty). Language stays out of the graph.
- **Sources are shown as given**: a path relative to the analysed root, or `Assembly::Namespace.Type` for a type found in a
  referenced assembly.
- **No new attribute, no analyser change, no new Mermaid diagram.** `describe`, `crc`, `diagram` (Mermaid), `backstage` and
  `datadog` output is unchanged. The logic is in `Haya.Core`, so both tools produce the same graph.

### Alternatives considered
- **A component-level Mermaid diagram now.** Without knowing which component uses which, it would be a list of boxes; the panel
  shows the same with less to maintain.
- **Keeping only class names, without sources.** The source is what makes the list useful: it says where to start reading.

### Consequences
- The explorer page carries file paths. They are relative to the analysed root, so they do not leak absolute paths.
- The direction for later, each its own decision:
  - a component-level Mermaid diagram per app;
  - an optional attribute to mark classes that carry no responsibility or collaborator but are worth showing;
  - which collaborator each responsibility's class uses, so components can be linked to the apps they call;
  - call tracing between components, from the analysers.
