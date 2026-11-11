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
haya diagram                      -o system.mermaid     ./Example.sln   # level 1: systems (default)
haya diagram --detail apps        -o containers.mermaid ./Example.sln   # level 2: apps inside their systems
haya diagram --detail components  -o components.mermaid ./Example.sln   # level 3: classes inside the apps
```

The default output file is `./c4-system-context.mermaid` at every detail, so pass `-o` when you generate more than one.
`-d` is short for `--detail`.

## Make level 2 useful

Level 2 draws each application inside its system, labelled with the `Technology` from its `Meta`, and shaped by its
`Kind` (`Service`, `WebApp`, `Api`, `Worker`, `Database`, `Queue`). Set both on `Meta`; see the
[attribute reference](../reference/attributes.md). Collaborator `Technology` and `Protocol` label the relationships.

## Draw the classes inside each application (level 3)

`--detail components` draws each application that has annotated classes as a boundary around them. A class is every class with
a `[Responsibility]` or `[Collaborator]` (`@Responsibility`/`@Collaborator` in TypeScript); it shows the technology of its
source file (`.cs` is C#, `.ts` is TypeScript; a type from a referenced assembly has none) and its responsibilities. Each
collaborator is a relationship from the class it is on. Between two of your applications, the line joins the classes that
name each other; when the other application has no class that names this one, it goes to a box for that application inside
its boundary. Applications without annotated classes, and ones known only from a collaborator, are drawn as at level 2.

Level 3 grows with the number of classes, so it reads best for one system or a few applications. The explorer lists the
same classes in each application's panel when you ask for them: `-f html --detail components`.

## Show which classes use which (level 3 with members)

```bash
haya diagram --detail members -o components.mermaid ./Example.sln
```

`--detail members` draws the same diagram as `components`, plus a `uses` relationship between two classes of one application
when one reaches the other in code: directly, or through classes and interfaces without a Haya attribute (a controller that
calls an unannotated service that calls an annotated gateway is drawn as controller `uses` gateway). It is best effort:
dependency injection registrations, reflection, message handlers and TypeScript free functions are not followed. See
[ADR018](../adr/ADR018-component-dependencies.md).

## Show the people who use a system

Add a collaborator with `Kind = AppKind.Person` for each kind of user, `Upstream` when they use the application. Every
level draws it as a person, outside the boundary when its `Relationship` is `External`, with a relationship to the system
(level 1) or the application (level 2). See [People](../reference/attributes.md#people).

## Explore a large system interactively

Level 2 stops being readable once a system has many applications. `-f html` writes one self-contained page instead:
see [Explore the system graph](explore-the-system-graph.md).

## Render the output

The `.mermaid` file is plain Mermaid. Paste it into the [Mermaid live editor](https://mermaid.live), or use the
`crc` output, whose fenced `mermaid` blocks GitHub and most Markdown viewers render directly.

## Combine several systems

Pass several inputs to draw all of them: see [Document several systems together](document-multiple-systems.md).
