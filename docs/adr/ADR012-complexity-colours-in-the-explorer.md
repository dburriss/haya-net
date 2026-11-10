# ADR012: Complexity colours in the explorer

Date: 2026-11-10  
Status: Accepted  
Author: dburriss

### Context
In a large system the explorer ([ADR011](ADR011-interactive-html-explorer.md)) shows many boxes and lines that all look alike. We
want the heavy ones to stand out: the systems that hold and connect to the most, and the pairs of systems with the most relationships
between them. The first version coloured a system's border from green (fewest apps) to red (most apps), scaled to the page. Using it
showed three problems: with a typical spread most boxes turned green and only an outlier was warm; the same system was red on one page
and green on another; and a graph with one huge system flattened everything else.

### Decision
- **Colour only the heavy end.** Below a cutoff a system or line is plain. There is no green. Colour means "look here".
- **The measure.** A system's border uses **apps plus the relationships that touch them** (inside the system or out; a relationship
  between two of its own apps counts once). Apps alone ignore how connected a system is; relationships alone ignore how much it holds.
  A line between two systems uses the **total number of relationships between them**, both directions, taken from the whole graph
  rather than from what is drawn, so colours do not change as systems are opened. Lines to or from an app, and outside systems, are
  never coloured.
- **Fixed stops, one ramp shape.** A ramp has four stops: plain below the first, yellow from it, fading to orange from the second,
  fading to red from the third, deepest red at the fourth, blended linearly between. Defaults: 7, 9, 13, 20 for borders and 3, 4, 6,
  9 for lines. Fixed numbers make a colour mean the same in every project and on every regeneration.
- **The red end stretches.** If the data goes past the fourth stop it moves to the largest value, so the most complex system or pair
  always has the deepest red and a large page still distinguishes its heaviest members. Borders and lines stretch independently.
- **Muted palette.** Saturation is kept low so the colour informs without competing with the kind glyphs, selection and muting.
- **Where it lives.** The stops and colours are constants (`HEAT_SYSTEMS`, `HEAT_LINKS`, `HEAT_COLOURS`) at the top of
  `src/Haya.Viewer/app.js`. The measure is computed in the viewer from the embedded graph, so the graph format
  ([graph-format](../reference/graph-format.md)) is unchanged and both stacks emit the same page. The legend states the actual numbers
  and the system panel shows the sum.

### Alternatives considered
- **Scale to the page (green to red).** Rejected for the three reasons above.
- **Bands (grey, yellow, orange, red).** Simple to explain, but a step at every boundary and no way to tell a 14 from a 19. A ramp
  with floors keeps the "only the highest are deeply red" property with less arbitrariness.
- **Rank or percentile.** Robust to outliers but means "the top third" rather than "complex", which is not a property of the system.
- **Apps only for the border.** Missed highly connected systems with few apps, which is the case worth seeing.
- **Per-project options (`--heat-apps ...`).** Deferred until the defaults have been lived with; they would need carrying in the
  graph document to keep the two stacks identical.

### Consequences
- The numbers are untested defaults. Because the border measure is a sum, it runs larger than app count alone and the border stops
  are likely too low for real systems; on a test graph of 9 systems almost every system was coloured. Expect to raise them.
- The colour is a size, not a quality judgement, and depends on the annotations being complete. This is stated in
  [the explanation](../explanation/complexity-colours.md).
- Changing a number means regenerating `ViewerAssets.fs` (see [Contributing](../contributing.md)).
- Colour is never the only channel: the panel shows the sum, and every line carries its relationship count.
