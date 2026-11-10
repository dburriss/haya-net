# Concepts

This page explains the vocabulary Haya uses and the thinking behind it. For lookups see the
[attribute reference](../reference/attributes.md); to try it, start with a [tutorial](../tutorials/getting-started-csharp.md).

## Why annotations in the code

Documentation drifts when it lives far from the thing it describes. Haya puts the facts that are hard to recover from
code (what an application is for, who it talks to, who owns it) next to the code, where a reviewer sees them change
in the same pull request. Because they are structured, they can be rendered into human formats (CRC, diagrams) and
machine formats (describe files, a Backstage catalog, Datadog entities) from one source.

It is a floor, not a replacement for good documentation: enough for a new joiner to start exploring.

## The model

- **System**: a collection of applications that work together to provide a service.
- **Application**: a single deployable piece of software. Declared once with `Meta`.
- **Responsibility**: a use case, feature or capability an application has.
- **Collaborator**: another application or system it interacts with, with a direction and a relationship.
- **CRC**: Component, Responsibilities, Collaborators, after the idea of a CRC card. A "component" is the class that carries an annotation.

## How the model becomes diagrams

- **C4 level 1 (system context)**: systems and the relationships between them, derived from collaborators.
  Collaborators with an `External` relationship are drawn as external systems.
- **C4 level 2 (containers)**: each application inside its system, labelled with `Meta.Technology` and shaped by
  `Meta.Kind`. The naming avoids C4 jargon in the API: the C4 "container" is an *application* here, and the
  `Kind` is an `AppKind`. See [ADR007](../adr/ADR007-container-diagram-and-app-kind.md).

## One application or many

A solution or project is one application. Describe files let you gather many into one system, merged by system plus
app name. See [ADR006](../adr/ADR006-multi-app-system-model.md) and the
[how-to](../how-to/document-multiple-systems.md).
