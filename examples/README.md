# Examples

This directory contains examples of how to use the Haya attributes to generate documentation and diagrams for a codebase.

| Language | Annotations | Generated CRC |
| --- | --- | --- |
| C# | [HayaEcomm.cs](dotnet/Example/HayaEcomm.cs) | [CRC.md](dotnet/Example/CRC.md) |
| TypeScript | [ecomm.ts](typescript/example/src/ecomm.ts) | [CRC.md](typescript/example/CRC.md) |

Both describe the same application. The TypeScript annotations are class decorators (`@Responsibility`, `@Collaborator`)
and a top level `meta({...})` call, defined in [haya.ts](typescript/example/src/haya.ts). They are a placeholder until
there is an npm package, and the TypeScript analyser that reads them does not exist yet, so the TypeScript `CRC.md` is the
expected output (it is the C# output with the source path changed), not generated.
