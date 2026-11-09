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
| [ADR007](ADR007-container-diagram-and-app-kind.md) | C4 container diagram and `AppKind`, describe format 0.2 | |
| [ADR008](ADR008-custom-attribute-defaults.md) | Defaults for custom attributes and annotations | |
| [ADR009](ADR009-referenced-assembly-analysis.md) | Opt-in analysis of referenced assemblies (C#) | |
