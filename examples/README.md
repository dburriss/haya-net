# Examples

This directory contains examples of how to use the Haya attributes to generate documentation and diagrams for a codebase.

| Language | Annotations | Generated CRC |
| --- | --- | --- |
| C# | [HayaEcomm.cs](dotnet/Example/HayaEcomm.cs) | [CRC.md](dotnet/Example/CRC.md) |
| TypeScript | [ecomm.ts](typescript/example/src/ecomm.ts) | [CRC.md](typescript/example/CRC.md) |

Both describe the same application. The TypeScript annotations are class decorators (`@Responsibility`, `@Collaborator`)
and a top level `meta({...})` call, imported from [`@haya/annotations`](../src/Haya.TypeScript). Both `CRC.md` files are
generated (`haya crc -c`); for the TypeScript example run `bun install` once, then, from `typescript/example`:

```bash
bun run ../../../src/Haya.Bun/haya.ts crc -c .
```

`bun run parity` in `src/Haya.Bun` runs both tools on these examples and diffs every output. The only substitution is
the source path (`HayaEcomm.cs` becomes `src/ecomm.ts`).
