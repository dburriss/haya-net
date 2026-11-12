# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

> Semantic Versioning will only be applied once the project reaches a stable v1.

## [Unreleased]

### Added
- The html explorer's side panel can be hidden and shown again with the **Panel** button or `p`. It is open by default on a
  wide screen (the choice is remembered) and a sheet over the graph on a phone, opened on demand or by selecting something.
- The html explorer's address now always holds a link to the current view: shown apps, apps opened to their classes, and the
  selected node or line (`#open=…&show=…&classes=…&select=…&edge=…`). Opening such a link, or editing the hash, restores the
  view. Existing `#open=` and `#select=` links still work. See ADR019.

### Changed
- **Breaking:** `--detail members` records **dependencies** between annotated classes instead of `Uses`. When an annotated
  class reaches another annotated class of its app in code, directly or through any number of classes and interfaces without
  a Haya attribute, that is a dependency, with the unannotated types in between (`via`). The walk stops at the first annotated
  class; using an interface or abstract class also reaches its implementations in the source; framework, package and
  referenced-assembly types are never walked. Best effort: DI registrations, reflection, messaging and TypeScript free
  functions are not followed. Describe format `0.6` adds an optional per-component `dependencies: [{ name, source, via }]`
  (`0.0` to `0.5` still read). Graph format `0.5` replaces `uses` with `dependsOn: [{ id, via }]` and `usedBy`. The explorer's
  class panel shows **Depends on** and **Used by**, and an opened app draws a thin dashed line per dependency between its
  classes. `diagram --detail members -f mermaid` adds a `uses` relationship per dependency. `--verbose` (.NET) prints the size
  of the type graph and the time it took. Default output differs only in the describe and graph version strings. See ADR018.
- `haya describe` writes describe format `0.5`: every document has a top-level `"detail"` (`"apps"` by default), so default
  output differs from before in the version and that one line. Files from `0.0` to `0.4` are still read. The graph in
  `haya diagram -f html` is format `0.4`; the default page differs in that version string and the viewer code only. See ADR017.
- **Breaking:** `haya diagram -t/--diagram-type c4system|c4container` is replaced by `-d/--detail systems|apps|components`
  (default `systems`), in both CLIs. `--detail` picks the Mermaid level (systems, apps inside systems, classes inside apps),
  and for `-f html` whether the explorer has the classes in it: they are only there with `--detail components`. See ADR016.

### Added
- A `members` detail level, opt-in: `haya describe --detail members` and `haya diagram --detail members` record, for each class
  with a `[Responsibility]` or `[Collaborator]`, its public constructors (parameters in order) and the public methods,
  properties, fields and events declared on it, with types as the language writes them (`Task<bool>`, `Promise<boolean>`).
  Compiler-generated, inherited, non-public and `Object` override members are left out. Describe gains an optional per-app
  `components` array; `describe --detail apps|components|members` (default `apps`; `components` is the same as `apps` today).
  `crc`, `backstage` and `datadog` ignore it. See ADR017.
- With `--detail members`, the explorer's class panel shows **Constructor** and **Members** (graph format `0.4` adds
  `constructors` and `members` to a component). The exact-match **Uses** that came with them is replaced by dependencies
  (see Changed, ADR018).
- The org-explorer example (and its [live page](https://dburriss.github.io/haya-net/)) is built with `--detail members`: the
  Ordering, Billing and Catalog describe files now hold their classes' constructors and members. Its apps are all C# or
  TypeScript now (sources, `tech` and `language`), the languages Haya analyses; Go, Java, Kotlin and Python are gone.
- With `--detail components`, the explorer (`haya diagram -f html`) lists an app's components in its panel: each class that carries a `[Responsibility]`
  or `[Collaborator]` (`@Responsibility`/`@Collaborator` in TypeScript), once, with its source file. Graph format `0.3` adds
  the optional `components` field to app nodes. Other outputs are unchanged. See ADR014.
- `haya diagram --detail components` writes a Mermaid C4 component diagram (level 3): each app's annotated classes inside its
  boundary, with the technology of their source file (`.cs` is C#, `.ts` is TypeScript) and their responsibilities, and a
  relationship from the class each collaborator is on. See [Generate diagrams](docs/how-to/generate-diagrams.md) and ADR015.
- In the explorer with `--detail components`, double-click an app with annotated classes to open it: its classes are drawn inside it, the apps it connects
  to are shown, and lines go to the classes they are written on. Click a class for its source, responsibilities and
  relationships. Graph format `0.3` gives each component an `id` and edges `fromComponents`/`toComponents`.

### Fixed
- In the explorer, selecting a class (or searching for one) no longer keeps its app's and system's own lines and neighbours
  lit; only the class and its lines stay focused, with the app and system boundaries still drawn around it.

## [0.0.6] - 2026-11-11

### Changed
- **Breaking:** `Relationship.InternalUser` and `ExternalUser` are removed (C#, TypeScript and describe files). Use a
  collaborator with `Kind = AppKind.Person` and `Relationship.Internal` or `External`. A describe file that still uses them, or
  any unknown relationship, is now an error; before, it read as `ExternalUser`. See ADR013.
- Releases are tag-driven: pushing a `v*` tag publishes `Haya` and `Haya.Tool` to NuGet with
  Trusted Publishing (no API key), both at one version. Use `./publish.sh` to bump, tag and push. Previously every push to
  `main` tried to publish. `Haya.Core` and `Haya.Roslyn` are no longer packable; `Haya.Tool` bundles them.

### Added
- People: a collaborator with `Kind = AppKind.Person` (C#; `kind: AppKind.Person` in TypeScript) is a user rather than an
  application. It is named with `AppName`, has no system and no `Meta`, and is one node however many apps name it. The Mermaid
  system and container diagrams draw `Person` or `Person_Ext`, and the explorer draws a person node. Backstage and Datadog leave
  people out. Describe format `0.4` adds the optional collaborator `kind`; graph format `0.2` adds the `person` node kind.
  `0.3` and older describe files are still read. See [the reference](docs/reference/attributes.md#people) and ADR013.
- The interactive explorer is published to GitHub Pages at <https://dburriss.github.io/haya-net/>, built in CI from `examples/org-explorer` (nine systems, JSON and YAML describe files); no HTML is committed.
- `haya diagram -f html` writes an interactive graph explorer: one self-contained page (no network, works from a file)
  for systems too big for a Mermaid diagram. It starts with every system collapsed to a box with its app count; double-click
  opens a system into a boundary around its apps (or shows the apps an app connects to), and selecting a node mutes what has
  no relationship with it. There is one numbered line per pair of boxes with an arrowhead per direction, small nodes with
  glyphs for kind, tier and lifecycle, kind/tier/lifecycle buttons that double as the legend and fade what is switched
  off, search that reveals hidden apps, and a side panel with details, inbound and outbound relationships. Apps known only
  from a collaborator are marked undocumented. A Theme button switches between automatic, light and dark, and the borders of systems, and the lines between them, are coloured yellow to deep red once they get complex (a system by its apps plus relationships, 7 or more; a line by its relationships, 3 or more), so the heavy ones stand out. Both stacks emit identical pages. See
  [the how-to](docs/how-to/explore-the-system-graph.md), the [graph format](docs/reference/graph-format.md) and ADR011.
- `haya datadog` writes Datadog Software Catalog entities (`entity.datadog.yaml`, schema v3) on both the .NET tool and
  the Bun CLI: a `service`, `datastore` or `queue` per application with `dependsOn`, a `system` per system, and stubs for
  collaborators that are not in the catalog. Checked against Datadog's JSON Schemas in the tests. See
  [the how-to](docs/how-to/publish-to-datadog.md) and ADR010.
- `Lifecycle` and `Tier` on `Meta` (C# attributes and TypeScript `meta()`), written to the Datadog entity.
- Describe format `0.3`: optional `meta.lifecycle`, `meta.tier` and an app-level `language` (`dotnet` or `javascript`)
  stamped by the analyser. `0.2` and older files are still read.
- `--include-references` and `--include-assembly <glob>` (with `--verbose`) read Haya attributes from public types in
  referenced packages/DLLs (C# only). Off by default. See
  [the how-to](docs/how-to/include-referenced-packages.md) and ADR009.
- Custom TypeScript annotations with defaults: `defineResponsibility`, `defineCollaborator` and `defineMeta` in
  `@haya/annotations` declare an annotation with options baked in; options at the usage site override them. See
  [the tutorial](docs/tutorials/custom-attributes-typescript.md) and ADR008.
- Custom C# attributes with defaults: derive from `ResponsibilityAttribute`, `CollaboratorAttribute` or `MetaAttribute` and
  put `ResponsibilityDefaults`, `CollaboratorDefaults` or `MetaDefaults` on the class to bake in values; values at the
  usage site override them. Works for attributes in source and in compiled assemblies. See
  [the tutorial](docs/tutorials/custom-attributes-csharp.md) and ADR008. (Previously, subclasses were silently ignored.)
- Documentation reorganised by Diataxis under `docs/` (tutorials, how-to guides, reference, explanation), a new README
  with logo and badges, and an ADR index. `Attributes.md` moved to `docs/reference/attributes.md`.
- C4 Level 2 (container) diagram: `crc -cc` adds it after the level 1 diagram and `diagram -t c4container` writes only
  it, in both `Haya.Tool` and `Haya.Bun`. Each app is drawn inside the boundary of its system; other systems are
  drawn as systems, and collaborator `Technology`/`Protocol` label the relationships. See ADR007.
- `Technology` and `Kind` on `Meta` (`meta({ technology, kind })` in TypeScript). `Kind` is an `AppKind`
  (`Service`, `WebApp`, `Api`, `Worker`, `Database`, `Queue`) and picks the shape of the app in the level 2 diagram.
- Every command accepts several inputs (solutions, tsconfig projects and/or describe files, mixed) and builds one
  system from them, in both `Haya.Tool` and `Haya.Bun`: `haya backstage a.json b.json ../other/Other.sln`.
  Apps with the same system and name are merged (conflicting meta fields produce a warning, first wins). `diagram`
  shows every system with the relationships between them, `backstage` links apps in the catalog instead of emitting
  stubs, and `crc` gets a section per app. Single-app output is unchanged. See ADR006.
- `crc`, `describe`, `diagram` and `backstage` accept a `haya describe` file (`.json`, `.yaml`, `.yml`) as input
  in both `Haya.Tool` and `Haya.Bun`, so a describe file can be generated once and rendered into other formats
  (or converted json/yaml) without re-analysing the code. Decoding lives in `Haya.Core` (`Decode.fs`).
- `Haya.Bun` is a working CLI: `crc`, `describe`, `diagram` and `backstage` for TypeScript projects, with a static analyser
  built on the TypeScript compiler API (ADR005). Output is identical to `Haya.Tool` for the examples (`bun run parity`).
- `src/Haya.TypeScript` (`@haya/annotations`): `@Responsibility`, `@Collaborator` and `meta()` for TypeScript, replacing
  the example's copied `haya.ts`.
- `examples/typescript/example`: TypeScript equivalent of the C# example; its `CRC.md` is now generated by the CLI.
- `src/Haya.Bun`: experimental Fable → TypeScript build of the CLI running on Bun (command skeleton).
- `AGENTS.md` (with `CLAUDE.md` symlink), `global.json`, `nuget.config`, local Fable tool manifest.

### Changed
- The `describe` document is now version `0.2`: an app's `meta` may have `tech` and `kind` (left out when unset).
  Versions `0.0` and `0.1` are still read as input.
- The `describe` document moved to version `0.1` before `0.2`: `{ "apps": [ { meta, responsibilities, collaborators } ], "version": "0.1" }`
  (was a single app at the top level, version `0.0`). Version `0.0` files are still read as input.
- Library API: `Describe`, `Crc`, `Diagram` and `Backstage` take an `App list` instead of a `Descriptor list`;
  command records have `InputPaths: string list` instead of `PathToSln`.
- `Haya.Bun` references `Haya.Core` as a whole and compiles all of it with Fable (previously three linked files).
- `crc` markdown is rendered by an internal builder instead of FuncyDown (output unchanged, always LF line endings).
- JSON/YAML output is written with Flexi.Json and Flexi.Yaml via explicit encoders (ADR004), replacing
  System.Text.Json and YamlDotNet. JSON is unchanged. YAML is equivalent but laid out differently: sequences are
  indented under their key, sequence items that are mappings start on a new line, and `version` is now the
  string `'0.0'` (previously it parsed as a float).
- Roslyn analysis moved out of `Haya.Core` into the new `Haya.Roslyn` project (ADR003). `Haya.Core` no longer
  references Roslyn or `Haya`; `Direction`/`Relationship` now also live in `Haya.Core.Analysis`. CLI output is unchanged.
- Target .NET 10 (`Haya` attributes stay on netstandard2.0).
- Upgraded dependencies: Roslyn 5.9, YamlDotNet 18.1, FuncyDown 1.4.2, Argu 6.2.5.
- Tests migrated to xunit.v3 on Microsoft.Testing.Platform.
- CI uses the SDK from `global.json` and builds the Bun project.

### Fixed
- Markdown table cells escape `|` and newlines.
- `diagram` (and the `crc` C4 diagram) apply the same default system for a collaborator without a `System` as
  `backstage`: the owning app's system, or `External` for external relationships.
- `backstage`: an app now lists, in `dependsOn`, the apps in the catalog that declare it as an upstream collaborator.
- `backstage`: a collaborator without a `System` now takes its owning app's system (or `External` when its
  relationship is external) instead of producing a component named `-name` with an empty domain.
- `backstage`: `dependsOn` and `dependencyOf` now follow the collaborator `Direction`: downstream collaborators go in
  `dependsOn`, upstream ones (they call this app) in `dependencyOf`, together with the apps in the catalog that call
  it. Before, every collaborator was in `dependsOn` and, with a wrong name, in `dependencyOf`. Single-app output changes.
- `backstage` no longer fails for an app without `meta()`; it catalogues the collaborators only (regression test added).
- `backstage -f json` now writes JSON (an array of Component entities) instead of YAML, on both stacks.
- `Haya.Bun` now finds annotations in workspace packages imported by name (symlinked into `node_modules`) and in
  projects listed under tsconfig `references`; both were silently skipped before.
- `Haya.Tool` no longer emits a responsibility twice and drops the collaborator for a C# class that has both
  `[Responsibility]` and `[Collaborator]`; both are now emitted once, matching `Haya.Bun`.
- Solutions containing F# projects no longer crash analysis; only C# projects are analysed.

### Removed
- FSharpPlus dependency.
- Direct `Microsoft.Build` package references (Roslyn's MSBuild build host handles project loading).

## [0.0.4] - 2024-09-27

### Added
- `Technology` field to the `CollaboratorAttribute` attribute.
- `describe` command to the `Haya.Tool` for outputting json or yaml description.
- `diagram` command for outputting diagrams with mermaid support.
- `backstage` command for outputting a backstage catalog spec.
- `version` field for the internal spec.
- YAML support as an output format.
- `Owner` on CollaboratorAttribute.
- `diagram` command for outputting diagrams. Currently only mermaid support.

### Changed
- Json output of `componentSource` is now relative to the executing directory.
- Added `protocol` field to the Descriptor, used in json output.
- `Team` to `Owner` on MetaAttribute.

### Removed

- json format for the crc command.

## [0.0.3] - 2024-09-21

### Added

- DocComments on the Attributes
- Added this changelog

### Changed
- Attributes are now published independently in the `Haya` package

### Removed

## [0.0.2] - 2024-09-20

### Added

- Added documentation page for the attributes.

## Changed

- Changes `Tech` field to `Protocol` field on the `CollaboratorAttribute` attribute.

## [0.0.1] - 2024-09-16

Initial release of the project.

### Added

- Added the `MetaAttribute` attribute.
- Added the `ResponsibilityAttribute` attribute.
- Added the `CollaboratorAttribute` attribute.
- Added the `Haya` dotnet tool with the `crc` command.

### Fixed

### Changed
### Removed
