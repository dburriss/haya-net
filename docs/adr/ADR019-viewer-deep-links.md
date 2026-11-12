# ADR019: The explorer keeps its view in the address hash

Date: 2026-11-12  
Status: Accepted  
Author: dburriss

### Context
The explorer could read `#open=` and `#select=` once on load, but never wrote them back, so a view reached by exploring could not
be shared, and the links could not express a single shown app or an app opened to its classes.

### Decision
The hash is the state: `#open=<systems>&show=<apps>&classes=<apps>&select=<id>&edge=<a>,<b>`. `open` lists systems with every
app shown, `show` the other shown apps, `classes` the apps opened to their classes, `select` the selected node, `edge` the two
ends of a selected line. The page rewrites the hash with `history.replaceState` after every change (no history entries, no
reload, works from `file://` and Pages; failures are ignored) and rebuilds the view on load and on `hashchange`. Unknown ids are
ignored. Old `#open=`/`#select=` links keep their meaning.

### Consequences
- A link restores what is shown, opened and selected, and centres on the selection. Box positions are not stored (the layout is
  randomly seeded), so the picture can differ.
- Ids come from names, so renaming a system, app or class breaks links to it.
- Filters and theme are not in the link.
