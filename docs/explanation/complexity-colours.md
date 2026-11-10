# Complexity colours in the explorer

Why some system borders and some lines in the [interactive explorer](../how-to/explore-the-system-graph.md) are coloured, what
the colour measures, and what it does not. The decisions behind it are in
[ADR012](../adr/ADR012-complexity-colours-in-the-explorer.md).

## The idea

A big graph is hard to scan, and the interesting parts are the heavy ones: the system everything depends on, the pair of
systems that talk to each other constantly. The explorer therefore colours only the **heavy end**. Plain means nothing to flag;
colour means look here. A system or line is plain until it passes a cutoff, then goes yellow, fades through orange, and reaches a
deep red for the most complex. There is no green on purpose: "fine" is just "no colour", and the page does not fill with green
when most things are small.

## What is measured

**A system's border** is coloured by its **complexity: the apps it holds plus the relationships that touch them.**

- *Apps* are all the apps in the system: those with a `[Meta]` and those known only because another app names them as a
  collaborator.
- *Relationships* are the collaborator relationships with one of those apps at either end, whether the other end is inside the
  system or outside it. A relationship between two apps of the same system counts once. A relationship that two apps declare
  identically is one relationship.
- Only systems that hold apps of yours are coloured. Systems outside what you documented (the dashed boxes) never are.

The system's panel shows the sum, for example `11 apps + 20 relationships = 31`, so the colour is never the only way to learn
the number.

**A line** between two systems is coloured by the **total number of relationships between those two systems**, in both
directions together. That is also the number on the line when both systems are closed. Lines to or from an individual app are
never coloured: between two apps there is almost always one relationship, so there is nothing to compare.

The line's colour comes from the whole data, not from what is drawn at the moment. Opening a system can show fewer relationships on
a line (the rest are on lines to its apps) but the colour of the system-to-system line does not change.

## The ramp

The numbers below are the defaults. Each ramp has four stops.

| | System border (apps + relationships) | Line (relationships) |
| --- | --- | --- |
| Plain below | 7 | 3 |
| Yellow starts at | 7 | 3 |
| Full yellow, then fading to orange | 9 | 4 |
| Orange, then fading to red | 13 | 6 |
| Deepest red | 20 | 9 |

Between the stops the colour is blended, so 11 is between yellow and orange and 16 is between orange and red. The colours are
muted on purpose: they inform without competing with the node and line colours that already mean something.

### When the data is bigger than the last stop

The deepest red is reserved for the most complex. If a system or pair is bigger than the last number, the red end **stretches to
the largest value in the page**: with a system at 43, 43 is the deepest red and a system at 20 is only a little past orange. So the
most complex thing always has the deepest red, however big the numbers get. Borders and lines stretch independently.

### Why fixed numbers and not "the biggest is red"

An earlier version scaled between the smallest and largest in the page. That made colour mean something different on every page: a
system with 8 apps was red if it happened to be the biggest, and a lone huge system turned everything else green. Fixed stops mean
the same thing in every project and every regeneration, and a modest system is not alarming just because its neighbours are
smaller. The stretch rule keeps the "most complex is deepest" part for large systems.

## What the colour does not mean

- **It is a size, not a verdict.** A red border says the system holds and connects to a lot. It does not say the design is bad,
  and a plain one is not necessarily healthy. A well-structured system can be big.
- **It counts what is declared.** Relationships and apps come from the annotations. A system with few annotations looks simpler
  than it is, and apps known only from a collaborator count as apps.
- **The thresholds are starting points.** 7, 9, 13 and 20 (and 3, 4, 6, 9) were chosen to flag the heavy end of a typical
  graph. They have not been tuned against real systems. Because a system's sum includes its relationships, it runs well above its
  app count alone, so the border numbers may be too low for your data. Check them against a system you know.

## Changing the numbers

The stops, `HEAT_SYSTEMS` and `HEAT_LINKS`, and the colours, `HEAT_COLOURS`, are constants at the top of `src/Haya.Viewer/app.js`.
After changing them run `bun run gen` in `src/Haya.Viewer` and commit the regenerated `ViewerAssets.fs`; see
[the viewer README](../../src/Haya.Viewer/README.md) and [Contributing](../contributing.md). They are not command-line options
yet.
