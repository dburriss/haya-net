# ADR016: One `--detail` level for diagrams, classes opt-in

Date: 2026-11-11  
Status: Accepted  
Author: dburriss

### Context
ADR014 and ADR015 put each app's classes into the graph, and so into every explorer page: the panel lists them and
double-click opens an app into them. Most readers of the explorer want systems and apps. The classes make the page bigger and
the panels longer for everyone, so they should be something you ask for.

`diagram` already had a detail choice: `-t c4system|c4container|c4component` picked the level of the Mermaid diagram, and
`-f html` ignored it. A second option for the explorer (`--detail`) would have given two flags for the same idea, plus a rule
for which one wins (`-t c4component` with `--detail apps`). The `c4…` values were also C4 jargon in the public CLI.

### Decision
- **`-t/--diagram-type` is replaced by `-d/--detail systems|apps|components`** on `diagram`, in both CLIs, with the same usage
  text. The default is `systems`. No alias for `-t` (beta, breaking changes are fine).
- **One level, both formats.** The format picks the picture and the detail picks how deep it goes:

  | `--detail` | `-f mermaid` | `-f html` |
  | --- | --- | --- |
  | `systems` (default) | systems and how they connect (C4 level 1) | explorer without classes |
  | `apps` | the apps inside their systems (level 2) | explorer without classes |
  | `components` | the classes inside the apps (level 3) | explorer with classes |

  The explorer always holds systems and apps (you open systems in the page), so `systems` and `apps` give the same page.
- **A shared `Detail` type** in `Haya.Core/Types.fs` (`Systems | Apps | Components`) replaces `DiagramType`, and
  `Graph.ofApps` takes it. Below `Components` it leaves `Node.Components` and `Edge.FromComponents`/`ToComponents` empty, so the
  graph document leaves them out (they were already omitted when empty) and the viewer works as before ADR014: no Components
  section, no opening apps into classes, and double-click on an app shows the apps it connects to. The viewer needed no change.
  The component diagram always asks the graph for `Components`.
- **Describe, crc, backstage and datadog do not change and take no `--detail`.** Describe already carries `componentName` and
  `componentSource` on each responsibility and collaborator, and the CRC table uses them; nothing there needs a level.
- **`members` is reserved** as the next level: constructor parameters and public members per class. It is not built yet.

### Alternatives considered
- **Keep `-t` and add `--detail` for html only**, with `-t c4component` always drawing classes. Two options for one idea and a
  precedence rule to explain.
- **Reuse `-t` for html** (`-f html -t c4component` adds classes). No new option, but it keeps the jargon, and `c4system` and
  `c4container` would mean the same thing for html.

### Consequences
- Breaking: `-t c4system|c4container|c4component` becomes `--detail systems|apps|components`, and the default explorer page has
  no classes. `--detail components -f html` is byte for byte the page from before.
- A new level (`members`) is one case in `Detail` and one place in `Graph.ofApps`, not a new option.
- `crc -c`/`-cc` keep their own flags for embedding diagrams.
