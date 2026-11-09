# ADR007: Container (C4 level 2) diagram, and `Technology` and `Kind` on Meta

Date: 2026-11-09  
Status: Accepted  
Author: dburriss

### Context
`crc -cc` was documented as "C4 Level 2" and set `IncludeL2Diagram`, but nothing rendered it, so it behaved like `-c`.
A useful level 2 shows the applications inside each system with their technology, and tells data stores and queues
apart from services. The model had no technology for an app and no way to say what kind of thing an app is.

### Decision
- `Meta` gets `Technology` (free text) and `Kind`, an `AppKind` enum: `Service`, `WebApp`, `Api`, `Worker`,
  `Database`, `Queue` (`Unspecified` when not set). In `Haya.Core` they are `Tech` and `Kind`; in the describe document
  `tech` and `kind`, written only when set. The describe version is `0.2`; `0.0` and `0.1` still decode.
- Public names avoid C4 vocabulary (it is `AppKind`, not `ContainerKind`), because users do not need to know C4 to
  annotate their code. "Container" only appears in the Mermaid output and in the renderer.
- `Diagram.sprintMermaidC4L2` (shared by both stacks via Fable) renders a `C4Container` diagram:
  - each app with a meta is a container inside a `System_Boundary` for its system; `Database` is `ContainerDb`,
    `Queue` is `ContainerQueue`, everything else `Container`;
  - a collaborator that names an app in one of our systems (or in no system) is that app's container, and one without
    its own meta is declared as a container from the collaborator (its `Technology`);
  - a collaborator in another system is drawn as that system (`System`, or `System_Ext` when external), as in level 1;
  - relationships are `Rel(from, to, dataDescription, protocol)`, flipped for upstream collaborators.
  Apps without a meta (or app name) have no identity and are not drawn.
- `crc -cc` renders level 1 and then level 2; `diagram -t c4container` renders only level 2.
- Mermaid ids and Backstage names come from one shared `Naming` module so renderers agree on app identity.

### Consequences
- Level 1, Backstage and CRC table output are unchanged; `describe` output gains the optional fields and the new version.
- `Kind` and `Technology` are on `Meta` only. A database or queue shows its shape only when it has its own meta (for
  example from a describe file); as a plain collaborator it is a generic container. Collaborator kind, and mapping
  `Kind` to Backstage `spec.type`, are left for later.
- Descriptions are not escaped in Mermaid output (as in level 1), so a `"` in a description breaks the diagram.
