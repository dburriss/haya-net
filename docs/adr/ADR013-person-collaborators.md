# ADR013: Person collaborators

Date: 2026-11-11  
Status: Accepted  
Author: dburriss

### Context
C4 has a person: a user of the system, drawn as `Person` or `Person_Ext`. Haya could not describe one. `Relationship` had
`InternalUser` and `ExternalUser`, and they survived the whole pipeline, but no renderer treated them as people: diagrams drew
them as ordinary systems, and Backstage and Datadog made stub entities for them. They also mixed two ideas in one enum: where the
other party sits (inside or outside the organisation) and what it is (a person or software).

### Decision
- **Split the two ideas.** `Relationship` keeps its meaning (`Owned`, `Shared`, `Internal`, `External`). What a collaborator is
  becomes a separate `Kind` on `Collaborator`, of type `AppKind`, which gains a `Person` value. Unset keeps today's behaviour.
  Reusing `AppKind` avoids a second enum; the C4 jargon stays out of the API (`Person`, not `Person_Ext`).
- **A person has no app, so no `Meta`.** It exists only as a collaborator. It is named with `AppName`, belongs to no system
  (`System` is ignored) and is one node however many apps name it, matched by name ignoring case.
- **Remove `InternalUser` and `ExternalUser`.** No aliases: the project is a beta and breaking changes are acceptable. A describe
  file that still uses them is rejected with an error naming the value. The decoder is also strict about unknown relationship
  values now; before, anything it did not know read as `ExternalUser`, which would have turned bad input into people. A missing
  `relationship` reads as `Internal`, the attribute default.
- **Formats.** Describe format `0.4` adds the optional collaborator `kind`, written only when set, so existing output is
  unchanged. Graph format `0.2` adds the node kind `person`, with no `parent`, `hasMeta` or `ownSystem`.
- **Rendering.** Mermaid levels 1 and 2 draw `Person` (internal, inside the enterprise boundary on level 1) or `Person_Ext`, with a
  relationship in the direction of the collaborator (`Upstream` means the person uses the app). The explorer draws a rounded node
  with a person glyph, always visible, never marked undocumented. Backstage and Datadog leave people out: neither has a matching
  entity, and a stub `service` would be wrong. CRC lists a person as any other collaborator.

### Alternatives considered
- **Keep `InternalUser`/`ExternalUser` as deprecated aliases.** Rejected: more code and documentation to carry for a beta.
- **A `[Person]` attribute.** More surface (descriptor, extractor, decorator, every renderer) and it would still need a
  collaborator to say who uses the person. Worth revisiting if people need their own description shared across apps.
- **A boolean `IsPerson`.** Cheaper, but a flag per new kind of collaborator.
- **A new enum on `Collaborator`.** Clearer name, but a second enum next to `AppKind` for one value.

### Consequences
- A breaking change to `Relationship` in the `Haya` package, the TypeScript annotations and the describe format. See the CHANGELOG.
- `Kind` on a collaborator can be any `AppKind`, but only `Person` has an effect. Other values are read and written but do not
  change how a collaborator is drawn.
- A person counts as a relationship in the complexity colours of the system it is connected to ([ADR012](ADR012-complexity-colours-in-the-explorer.md)).
- A collaborator that is a person and has no `AppName` is ignored.
