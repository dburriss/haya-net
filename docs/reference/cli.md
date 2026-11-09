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
| `-o`, `--output-path` | Output file | `./c4-system-context.mermaid` |
| `-t`, `--diagram-type` | `c4system` (level 1) or `c4container` (level 2) | `c4system` |
| `-f`, `--diagram-format` | `mermaid` | `mermaid` |

## `backstage`

| Flag | Meaning | Default |
| --- | --- | --- |
| `-o`, `--output-path` | Output file | `./backstage.yaml` or `./backstage.json` |
| `-f`, `--format` | `json` or `yaml` | `yaml` |

How-to guides: [diagrams](../how-to/generate-diagrams.md), [Backstage](../how-to/publish-to-backstage.md),
[several systems](../how-to/document-multiple-systems.md), [CI](../how-to/run-in-ci.md).
