# Configuration reference

Settings that would otherwise be repeated on every command line. Both CLIs (`Haya.Tool` and `Haya.Bun`) read the same
configuration and the same sources, through the shared `Haya.Core`. See [Configure Haya](../how-to/configure-haya.md) for
how to use it and [ADR020](../adr/ADR020-layered-config.md) for why it is built this way.

## Sources and precedence

Highest wins. Each setting is resolved on its own, so a local file can change one setting and leave the rest to the global file.

| # | Source | Where |
| --- | --- | --- |
| 1 | Command line flag | `-o`, `-f`, `-d`, `-c`, `--include-references`, ... |
| 2 | Environment variable | `HAYA_<SECTION>__<KEY>`, for example `HAYA_OUTPUT__PATH` |
| 3 | Local file | The nearest `.haya/config.json`, looking in the current directory and then its parents |
| 4 | Global file | `haya/config.json` in the OS config directory (below) |
| 5 | Built-in default | As listed in the [CLI reference](cli.md) |

The OS config directory is `~/Library/Application Support` on macOS, `%APPDATA%` on Windows, and `$XDG_CONFIG_HOME` or
`~/.config` elsewhere. Set `HAYA_CONFIG_DIR` to use `$HAYA_CONFIG_DIR/config.json` as the global file instead (used by the
parity check and handy in CI).

## File format

JSON, with one section per area. Language- or tool-specific settings live in a section named for it (`dotnet`).
A setting the other CLI does not use is ignored there without a warning. An unknown key is a warning, a wrong type or value is an error
that names the file.

```json
{
  "dotnet": { "references": { "include": true, "assemblies": ["MyCo.*"] } },
  "output": { "path": "docs/haya", "formats": ["yaml"] },
  "crc": { "diagram": "apps" },
  "diagram": { "format": "html", "detail": "components" },
  "viewer": { "detail": "apps", "complexity": { "systems": [7, 9, 13, 20], "links": [3, 4, 6, 9] } }
}
```

Environment variable names are `HAYA_`, then the key in upper case with `.` written as `__`: `viewer.complexity.systems` is
`HAYA_VIEWER__COMPLEXITY__SYSTEMS`. Lists and the four numbers are comma-separated (`7,9,13,20`).

## Settings

| Key | Values | Meaning |
| --- | --- | --- |
| `dotnet.references.include` | `true`, `false` | Same as `--include-references` (`Haya.Tool` only). The flag can only turn it on |
| `dotnet.references.assemblies` | list of globs | Same as `--include-assembly`; joined with the flags (`Haya.Tool` only) |
| `output.path` | directory | Where commands write by default. Each command keeps its own file name in it. Missing directories are created |
| `output.formats` | list | Default formats. The first one a command accepts is used (`yaml` for `describe`, `backstage`, `datadog`; `html` for `diagram`) |
| `crc.diagram` | `none`, `systems`, `apps` | C4 diagrams in the CRC document: `systems` is Level 1 (`-c`), `apps` is Level 2 (`-cc`). Uses the `--detail` names |
| `crc.output` | file | Output file of `crc`. Beats `output.path` |
| `diagram.detail`, `describe.detail` | as `--detail` | Default detail (`describe` has no `systems`) |
| `diagram.format` | `mermaid`, `html` | Default `-f` of `diagram`. Beats `output.formats` |
| `describe.format`, `backstage.format`, `datadog.format` | `json`, `yaml` | Default `-f`. Beats `output.formats` |
| `diagram.output`, `describe.output`, `backstage.output`, `datadog.output` | file | Output file of the command. Beats `output.path` |
| `viewer.detail` | `systems`, `apps`, `components`, `members` | How deep the html explorer opens when its address has no view of its own: `systems` collapsed, `apps` every app shown, `components` and `members` also every app opened into its classes (there are classes only if `diagram.detail` includes them) |
| `viewer.complexity.systems` | four ascending numbers | The stops of the colour ramp for a system's border: apps + relationships. See [Complexity colours](../explanation/complexity-colours.md) |
| `viewer.complexity.links` | four ascending numbers | The stops of the colour ramp for a line between two systems: relationships |

The viewer settings are embedded in the page as the optional `viewer` object of the [graph format](graph-format.md).

## `haya config`

| Command | Does |
| --- | --- |
| `haya config list [--show-origin]` | The effective settings, with the file or variable each came from |
| `haya config get <key> [--local\|--global]` | The effective value, or that file's value. Exit code `1` when unset |
| `haya config set <key> <value> [--local\|--global]` | Validates and writes the value. Default is the nearest local file (a new `.haya/config.json` in the current directory) |
| `haya config unset <key> [--local\|--global]` | Removes the value from a file |

`set` rewrites the file as indented JSON, keeping the other settings. Comments are not kept.
