# CLI reference

Two CLIs share the same commands, flags and output:

| Tool | For | Run as |
| --- | --- | --- |
| `Haya.Tool` | C# solutions | `haya` (.NET tool) |
| `Haya.Bun` | TypeScript projects | `bun run src/Haya.Bun/haya.ts` or the compiled `dist/haya` |

```
haya <command> [options] <inputPath...>
```

**Inputs** (always last): one or more of a solution (`Haya.Tool`), a `tsconfig.json` or folder containing one
(`Haya.Bun`), or a [describe file](describe-format.md) (`.json`, `.yaml`, `.yml`) for either tool. Inputs are merged
into one system. `haya --help` and `haya <command> --help` list the options.

Exit code is `0` on success and `1` on error. Warnings go to stderr.

## Referenced assemblies (`Haya.Tool` only)

All four commands accept these flags. By default only the C# source in the solution is analysed.

| Flag | Meaning |
| --- | --- |
| `--include-references` | Also read public types in referenced packages/DLLs that reference Haya |
| `--include-assembly <glob>` | Also read referenced assemblies whose name matches the glob (`*`, `?`). Repeatable; on its own it limits the scan to matching assemblies |
| `-v`, `--verbose` | Print each `Including <assembly>: <type>` that was picked up |

See [Include attributes from referenced packages](../how-to/include-referenced-packages.md). `Haya.Bun` has no equivalent.

## `crc`

Components, Responsibilities and Collaborators as Markdown.

| Flag | Meaning | Default |
| --- | --- | --- |
| `-o`, `--output-path` | Output file | `./CRC.md` |
| `-c`, `--c4` | Embed C4 level 1. Repeat (`-cc`) to also embed level 2 | off |

## `describe`

A [describe file](describe-format.md) of the analysed code.

| Flag | Meaning | Default |
| --- | --- | --- |
| `-o`, `--output-path` | Output file | `./haya.json` or `./haya.yaml` |
| `-f`, `--format` | `json` or `yaml` | `json` |

## `diagram`

| Flag | Meaning | Default |
| --- | --- | --- |
| `-o`, `--output-path` | Output file | `./c4-system-context.mermaid`, or `./diagram.html` for `-f html` |
| `-d`, `--detail` | `systems` (systems and how they connect), `apps` (the apps inside their systems) or `components` (the classes inside the apps). With `-f html`, `components` adds the classes to the explorer; `systems` and `apps` give the same page | `systems` |
| `-f`, `--diagram-format` | `mermaid`, or `html` (an interactive explorer, one self-contained page) | `mermaid` |

## `backstage`

| Flag | Meaning | Default |
| --- | --- | --- |
| `-o`, `--output-path` | Output file | `./backstage.yaml` or `./backstage.json` |
| `-f`, `--format` | `json` or `yaml` | `yaml` |

## `datadog`

| Flag | Meaning | Default |
| --- | --- | --- |
| `-o`, `--output-path` | Output file | `./entity.datadog.yaml` or `./entity.datadog.json` |
| `-f`, `--format` | `json` or `yaml` | `yaml` |

Datadog only discovers a file named `entity.datadog.yaml` (or `service.datadog.yaml`) in your repository. The JSON
output, an array of entities, is for the Datadog Catalog API.

How-to guides: [diagrams](../how-to/generate-diagrams.md), [Backstage](../how-to/publish-to-backstage.md),
[Datadog](../how-to/publish-to-datadog.md),
[several systems](../how-to/document-multiple-systems.md), [CI](../how-to/run-in-ci.md).
