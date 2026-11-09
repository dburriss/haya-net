# How to troubleshoot Haya errors

| Message or symptom | Cause and fix |
| --- | --- |
| `Input not found: <path>` | The path is wrong. Input paths go **last** on the command line, after all options. |
| `file:line:column - message` (TypeScript) | An annotation value is not static. Use string literals, template literals without substitutions and `Direction.*` / `Relationship.*` members. Also raised for unknown options and more than one `meta()`. |
| A class with `[Responsibility]` is missing from the output (C#) | The project is not C#. Only C# projects in a solution are analysed; F# projects load without a compilation and are skipped. |
| `Warning: ...` about a conflict | Two inputs describe the same app (same system and name) with different description, owner or repository. The first wins; align the annotations. |
| `Protocol` is empty in the output | An unset `Protocol` is output as `""` by the analysers; the C# attribute default `"HTTPS"` is not applied. Set it explicitly. |
| A `\|` or line break in a description looks odd | Markdown table cells escape `\|` and newlines. Mermaid descriptions are not escaped, so avoid quotes and newlines there. |
| Paths in output are not what you expect | `ComponentSource` is relative to the directory you run `haya` from. |
| Nothing is found in a TypeScript workspace | Run `haya` against the `tsconfig.json` (or its folder) that includes the annotated files. Packages in `node_modules` workspaces and tsconfig `references` are followed. |

See the [CLI reference](../reference/cli.md) for flags and the [attribute reference](../reference/attributes.md) for valid values.
