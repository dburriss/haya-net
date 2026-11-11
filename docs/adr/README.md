# Architecture decision records

Why Haya is built the way it is. Each record is one decision; newer records can supersede parts of older ones.

| ADR | Decision | Notes |
| --- | --- | --- |
| [ADR001](ADR001-separate-commands-per-format.md) | Separate commands per output format | |
| [ADR002](ADR002-bun-typescript-runtime.md) | Bun and TypeScript runtime for the TypeScript tool | |
| [ADR003](ADR003-split-roslyn-from-core.md) | Split Roslyn analysis from `Haya.Core` | |
| [ADR004](ADR004-fable-compatible-core-libraries.md) | Fable-compatible libraries in the core | |
| [ADR005](ADR005-typescript-annotations-and-analyser.md) | TypeScript annotations and static analyser | |
| [ADR006](ADR006-multi-app-system-model.md) | Multi-app system model, describe format 0.1 | Describe format superseded by 0.2 in ADR007 |
| [ADR007](ADR007-container-diagram-and-app-kind.md) | C4 container diagram and `AppKind`, describe format 0.2 | Describe format superseded by 0.3 in ADR010 |
| [ADR008](ADR008-custom-attribute-defaults.md) | Defaults for custom attributes and annotations | |
| [ADR009](ADR009-referenced-assembly-analysis.md) | Opt-in analysis of referenced assemblies (C#) | |
| [ADR010](ADR010-datadog-command-and-language.md) | Datadog command, analyser-stamped language, lifecycle and tier, describe format 0.3 | Describe format superseded by 0.4 in ADR013 |
| [ADR011](ADR011-interactive-html-explorer.md) | Interactive html graph explorer (`diagram -f html`) | Component sources added to the graph in ADR014 |
| [ADR012](ADR012-complexity-colours-in-the-explorer.md) | Complexity colours in the explorer: a ramp from a cutoff, by apps + relationships | |
| [ADR013](ADR013-person-collaborators.md) | Person collaborators: a `Kind` on `Collaborator`, `InternalUser`/`ExternalUser` removed, describe format 0.4 | Describe format superseded by 0.5 in ADR017 |
| [ADR014](ADR014-components-in-the-explorer-panel.md) | Components (classes and their source) listed in the explorer panel, graph format 0.3 | Component diagram added in ADR015; opt-in with `--detail components` in ADR016 |
| [ADR015](ADR015-component-diagram.md) | C4 component diagram (`diagram -t c4component`) and opening apps into their classes in the explorer | `-t` replaced by `--detail` in ADR016 |
| [ADR016](ADR016-diagram-detail.md) | One `--detail systems\|apps\|components` level for diagrams; classes in the explorer opt-in | Extended with `members` in ADR017 |
| [ADR017](ADR017-member-detail.md) | A `members` detail level: constructors, public members and `Uses` per class, describe format 0.5, graph format 0.4 | `Uses` replaced by dependencies in ADR018 |
| [ADR018](ADR018-component-dependencies.md) | Dependencies between annotated classes, walked through unannotated types, under `members`; describe format 0.6, graph format 0.5 | |
