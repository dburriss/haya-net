# How to generate C4 diagrams

Use this to produce Mermaid C4 diagrams from your annotations. It assumes you have already
[annotated a project](../tutorials/getting-started-csharp.md). In the examples `haya` is the C# tool; for TypeScript
use the Bun CLI with a `tsconfig.json` or its folder instead of a solution.

## Embed the diagrams in the CRC document

```bash
haya crc -c  ./Example.sln   # C4 level 1: systems and how they relate
haya crc -cc ./Example.sln   # level 1 and level 2: applications inside each system
```

## Write a diagram on its own

```bash
haya diagram -t c4system    -o system.mermaid     ./Example.sln   # level 1 (default)
haya diagram -t c4container -o containers.mermaid ./Example.sln   # level 2
```

The default output file is `./c4-system-context.mermaid` for both types, so pass `-o` when you generate both.

## Make level 2 useful

Level 2 draws each application inside its system, labelled with the `Technology` from its `Meta`, and shaped by its
`Kind` (`Service`, `WebApp`, `Api`, `Worker`, `Database`, `Queue`). Set both on `Meta`; see the
[attribute reference](../reference/attributes.md). Collaborator `Technology` and `Protocol` label the relationships.

## Render the output

The `.mermaid` file is plain Mermaid. Paste it into the [Mermaid live editor](https://mermaid.live), or use the
`crc` output, whose fenced `mermaid` blocks GitHub and most Markdown viewers render directly.

## Combine several systems

Pass several inputs to draw all of them: see [Document several systems together](document-multiple-systems.md).
