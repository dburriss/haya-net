# ADR017: A `members` detail level: constructors and public members per class, opt-in

Date: 2026-11-11  
Status: Accepted  
Author: dburriss

### Context
ADR014 to ADR016 put each app's annotated classes into the graph, behind `--detail components`. The next question readers ask
is "which classes does this class use, and what does it offer?". Full class diagrams answer that, but they are big, and they
drift into every class in the code base. Later we also want to trace a collaborator call through the responsibility classes
that lead to it. For that we need, for each annotated class, what it takes in its constructor.

Constructors and members are much more data than names. On `examples/dotnet/Example.sln` the describe file grows from 2,681
to 5,732 bytes as JSON (2,127 to 4,199 as YAML) for five small classes. Most readers do not want this, so it has to be something
you ask for, like the classes themselves (ADR016).

### Decision
- **A fourth level, `members`**: `Detail = Systems | Apps | Components | Members`, ordered, each including the levels before it
  (`Detail.atLeast`). `diagram --detail members` and `describe --detail members` turn it on, in both CLIs with the same help.
  At lower levels the analysers do no extra work.
- **What is recorded** (`ComponentDetail` in `Haya.Core/Model.fs`, keyed like components by class name and source): only for
  classes that carry a `Responsibility` or `Collaborator`.
  - *Constructors*: every public constructor, with its parameters in order (`Parameter = { Name; Type }`).
  - *Members*: the public members declared on the class itself, as `Member = { Name; Kind; Type; Parameters }`, where
    `Kind` is `Method | Property | Field | Event` and `Type` is the return, property, field or event type. `Parameters`
    is filled for methods (and C# indexers).
  - *Left out*: anything that isn't public (C# `Accessibility.Public`; in TypeScript, `private`, `protected` and `#name`
    members). Also left out: inherited members, nested types, and anything the compiler writes. In C# that means the
    implicit constructor, record members, backing fields, property and event accessors, operators, and overrides of
    `System.Object` (`ToString`, `Equals`, `GetHashCode`). In TypeScript it means the implementation signature when
    overloads exist, and `toString`/`valueOf`.
  - *TypeScript has no fields or events here*: a property declaration, a get/set accessor pair (one entry) and a public
    parameter property (`constructor(public repo: Repo)`) are all `Property`. Private parameter properties are still
    constructor parameters.
  - *One ordering rule*, in Core (`ComponentDetail.normalise`), applied by `App.withComponents` for both analysers:
    constructors by parameter count, then signature text; members by kind, then name, then signature (ordinal).
- **Type strings are each compiler's own short form**: Roslyn's `SymbolDisplayFormat.MinimallyQualifiedFormat` (`Task<bool>`,
  `string?`, `int`) and the TypeScript checker's `typeToString` (`Promise<boolean>`, `string | undefined`, `number`). The model
  does not try to make them the same across languages.
- **`Uses`, exact match only**: `Graph.Component.Uses` lists the components of the same app whose class name is exactly
  the type of one of its constructor parameters, once each, never itself. `IRepo`, `List<Repo>`, `Repo?` and
  `Promise<Repo>` do not match `Repo`. Stripping generics, nullability or an `I` prefix would guess, and a wrong edge is
  worse than a missing one in a trace. It is computed by `Graph.uses` in Core, so the later trace is shared too.
- **Describe 0.5**: a top-level `detail` (`apps`, `components` or `members`) on every document, so a reader can tell
  "not asked for" from "none exist". At `members`, each app may have a `components` array (`name`, `source`, `constructors`,
  `members`), left out when empty. Below `members` it is stripped even when the input was a describe file that had it, so
  `detail` stays true. `describe --detail components` gives the same apps as `apps` today: the class names already ride on each
  responsibility and collaborator. Documents 0.0 to 0.4 still decode, with no classes. `crc`, `backstage` and `datadog` ignore
  the classes and take no `--detail`.
- **Graph 0.4**: a component may have `constructors`, `members` and `uses`, each left out when empty. The explorer's class panel
  shows **Constructor**, **Uses** (links that select the class) and **Members** sections when they are there. All text goes through
  the `h` helper, so it is never parsed as html.
- **Mermaid draws no members.** `diagram --detail members -f mermaid` is the component diagram, the same as at `components`.
- **Parity maps the known language differences**, in `src/Haya.Bun/scripts/parity.ts`, only in member and parameter `type`
  values and in the first letter of a member name:

  | TypeScript | C# |
  | --- | --- |
  | `Promise<void>` | `Task` |
  | `Promise<T>` | `Task<T>` |
  | `number` | `int` |
  | `boolean` | `bool` |
  | `pay` (member name) | `Pay` |

  `string` and `void` are the same in both. The example twins (`examples/dotnet/Example`, `examples/typescript/example`) gained
  constructors that take other annotated classes and a few public methods and a property, so both stacks have something to show.

### Alternatives considered
- **A new `Descriptor` case per class.** It would flow through `App.ofDescriptors` for free, but every consumer of descriptors
  would then have to skip it, and it is not something you declare. A separate `App.Components` keeps the descriptors as they were.
- **Members always on, without a level.** That's simpler, but it costs every user the size and the analysis time for data few
  of them read.
- **Fuzzy `Uses`** (interfaces, generics, nullability). It would find more edges, but some would be wrong.
- **Normalising types across languages in the model.** Readers of one code base want their own language's types. The mapping
  belongs to the parity check, where the differences are listed, not hidden.

### Consequences
- Default `describe` output changes: `"version": "0.5"` and a new `"detail": "apps"` line. Default `diagram -f html` changes in
  its graph version (`0.4`) and the viewer code. Every other default output is byte for byte the same.
- The analyser finds classes only (`ClassDeclarationSyntax`, `ts.ClassDeclaration`), so a C# `record` with a Haya attribute
  is still not found. That was already the case before this ADR.
- **Not done yet, for later:**
  - trace queries over `Uses`, from a collaborator to the responsibility classes that lead to it;
  - `Uses` drawn as lines between opened classes in the explorer, or in Mermaid (left out to keep the viewer change small);
  - unannotated classes, and an attribute to include extra classes;
  - looser `Uses` matching, if a trace shows exact matching misses too much.
