# ADR015: C4 component diagram and opening apps in the explorer

Date: 2026-11-11  
Status: Accepted, extended by ADR016 (`-t c4component` is now `--detail components`)  
Author: dburriss

### Context
ADR014 listed each app's classes in the explorer panel and named a component-level Mermaid diagram as the next step. Once the
classes were listed, the explorer was expected to open an app into its classes the way it opens a system into its apps, with
lines to the class each relationship is on. The graph already had the classes per app. What it did not keep was which class a
collaborator is written on.

A C4 component diagram is usually drawn for one container. The other diagram types draw everything given to them, and Mermaid
cannot draw a relationship to a boundary, so lines between two of our apps need a box at each end.

### Decision
- **`diagram -t c4component` draws one Mermaid `C4Component` diagram for all the apps**, like levels 1 and 2. Own systems are
  boundaries, as at level 2. An app with a `[Meta]` and annotated classes is a `Container_Boundary` around its classes. An
  app without classes, one known only from a collaborator, other systems and people are drawn as at level 2.
- **A class is a `Component(id, name, technology, description)`.** The technology comes from the source file's extension
  (`.cs` C#, `.fs` F#, `.vb` Visual Basic, `.ts`/`.tsx`/`.mts`/`.cts` TypeScript, `.js`/`.jsx`/`.mjs`/`.cjs` JavaScript).
  A type from a referenced assembly (`Assembly::Namespace.Type`) or an unknown extension has none. The description is the
  class's responsibilities, joined with `; `. The id is the app id, `__`, then the class name; a second class of the same
  name in one app gets `_2`, and so on.
- **The classes at each end of an edge are part of the graph.** `Graph.Component` gains an `Id` (the alias above) and its
  responsibilities. `Graph.Edge` gains `FromComponents` and `ToComponents`: at the collaborator's end, the class it is on; at
  the other end, when it is one of our apps, its classes whose collaborators name this app back. Edges stay one per app pair,
  direction, protocol and label, with the classes of all their collaborators together, so the explorer's counts do not
  change. Both the Mermaid diagram and the explorer read them, so the rule lives in one place. Graph format `0.3` writes them.
- **A relationship starts at the class the collaborator is on** and ends at the classes on the other end. An end with no
  classes listed, at an app that has classes (no class names this app back, or a hand-written describe file), goes to a
  `Container` for the app (`<app id>__app`), drawn inside the app's boundary only when a line needs it.
- **In the explorer, double-click opens an app with classes**, like a system: the classes are drawn inside the app, the apps it
  connects to are shown so the lines have somewhere to go, and lines to or from the opened app go to the classes in
  `fromComponents`/`toComponents`, or to the app's box when the list is empty. Double-click again closes it. A class is
  selectable, with a panel for its source, responsibilities and relationships. An app without classes keeps the old
  double-click, which shows the apps it connects to.
- **Title**: the app's name when there is one app, otherwise the own systems, as at level 2.
- Parity: .NET stamps `C#` and TypeScript stamps `TypeScript` on the same classes, so the parity check maps the technology
  on `Component` lines, as it does for the language elsewhere.

### Alternatives considered
- **An `--app` option drawing one app**, others as plain containers. Closer to textbook C4, but it adds an option to both CLIs
  and breaks with how the other diagram types take their input. It can still be added if large inputs need it.
- **One file per app.** A different output shape from every other diagram type.
- **The source path, or the app's `Technology`, in the technology slot.** The path is in the explorer panel, and the app's
  technology would be the same on every class.

### Consequences
- Large inputs make large diagrams; the explorer remains the way to look at many systems.
- Many lines go to the `__app` box when the other app (a database, a queue) declares nothing back. That box is the app itself,
  not one of its classes.
- `crc -cc` still stops at level 2. Collaborator use per responsibility and call tracing (ADR014) remain future work.
