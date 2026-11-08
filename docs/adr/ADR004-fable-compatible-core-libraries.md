# ADR004: Fable-compatible libraries for Haya.Core

Date: 2026-11-08  
Status: Accepted  
Author: dburriss

### Context
To live in the shared `Haya.Core` (see [ADR003](ADR003-split-roslyn-from-core.md)), all dependencies must compile
under Fable. Current dependencies are not Fable-compatible:
- `Serializer.fs` uses `System.Text.Json` (reflection-based, over arbitrary F# values) and `YamlDotNet`.
  It serializes to JSON, parses to a `JsonNode`, converts to an object tree and then emits YAML.
- `Crc.fs` uses `FuncyDown` for markdown. (Initially assumed not Fable-compatible; later verified to compile, see the status update.)
- `Diagram.fs` opens the `Haya` attributes assembly.

### Decision
- Use [`Flexi.Json`](https://github.com/dburriss/flexi-json) and
  [`Flexi.Yaml`](https://github.com/dburriss/flexi-yaml) for JSON and YAML output. Both are dependency-free
  untyped document libraries (`JsonValue`, `YamlValue`) with printers, no reflection, and Fable support.
- Replace reflection with explicit encoders: each model type has a `toJsonValue` function that preserves
  the current conventions (camelCase keys, null/`None` omitted, enums as strings). YAML is produced by mapping
  the `JsonValue` to a `YamlValue`, so there is one encoder per type and no JSON round trip.
- Remove `System.Text.Json` and `YamlDotNet` from `Haya.Core`, along with the unused `ofJson`.
- Replace `FuncyDown` in `Crc.fs` with a small internal markdown builder (`Markdown.fs`: headings, paragraphs,
  tables, fenced code), and remove the `Haya` attribute dependency from `Diagram.fs`.

### Consequences
- *Pros:*
  - JSON and YAML rendering is shared by C# and TypeScript, and the dependency list shrinks.
  - Both libraries are owned by the same author, so gaps can be fixed upstream.
  - Explicit encoders are trivially AOT- and Fable-safe.
- *Cons:*
  - Encoders must be maintained by hand when the model changes.
  - Output may differ cosmetically (indentation, string escaping, YAML quoting, key order). Diff `describe`
    JSON/YAML and `backstage` output for `examples/dotnet/Example.sln` before and after, and keep Backstage output
    identical where consumers depend on it.
  - Flexi.Json and Flexi.Yaml are pre-1.0 (0.1.0).

### Status update (2026-11-08)
`Serializer.fs` now uses Flexi.Json and Flexi.Yaml with encoders in `Encode.fs`; System.Text.Json and YamlDotNet
are gone from `Haya.Core`. On `examples/dotnet/Example.sln` JSON output is byte-identical. YAML parses to the same data
except `version`, which is now the string `'0.0'` (it was previously emitted as a plain scalar that parses as a float).
Layout differs: sequences are indented and mapping items start on a new line. `Crc.fs` now uses an internal `Markdown.fs` builder instead of FuncyDown; CRC output is byte-identical.

Correction: FuncyDown 1.4.2 was assumed to be incompatible with Fable without being checked. A later check compiled it
with Fable 5.27 (TypeScript) with no changes (it ships its sources under `fable/`) and rendered the same markdown under
Bun and .NET. It remains a viable option. The internal builder was kept because Haya needs very little of it, it removes a
dependency, and it always emits `\n` (FuncyDown uses `Environment.NewLine`, so output differs per OS on Windows).
