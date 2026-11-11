# Graph format

The graph that `haya diagram -f html` embeds in the page and the viewer reads. It is resolved from the apps by `Graph.ofApps`
in `Haya.Core`, written by `Encode.graph`, and identical on both stacks. The current version is `0.5`. It is an internal
format for the page: it may change between versions, so read the [describe format](describe-format.md) if you need data
to consume.

## Where it is

One `<script type="application/json" id="haya-data">` element in the page. `<` and the line separators U+2028 and U+2029 inside
values are written as `\u` escapes, so the document is still valid JSON and no value can end the script element.

## Document

```json
{
  "edges": [
    { "from": "web_shop", "to": "ordering_payments_api", "direction": "Upstream", "protocol": "HTTPS", "label": "Request payment",
      "toComponents": [ "ordering_payments_api__payments_controller" ] }
  ],
  "nodes": [
    { "id": "ordering", "kind": "system", "name": "Ordering", "ownSystem": true, "relationship": "Owned" },
    {
      "id": "ordering_payments_api", "kind": "app", "name": "PaymentsApi", "parent": "ordering", "hasMeta": true,
      "appKind": "Api", "relationship": "Owned", "description": "Handles processing payment for a shopping cart",
      "tech": ".NET 10 / ASP.NET Core", "owner": "Ordering Team", "repository": "org/payment-api",
      "tier": "1", "lifecycle": "production", "responsibilities": [ "Handles order processing" ],
      "components": [ { "id": "ordering_payments_api__payments_controller", "name": "PaymentsController", "source": "HayaEcomm.cs" } ]
    }
  ],
  "version": "0.5"
}
```

Nodes are sorted by `id`; edges keep the order in which the apps and collaborators appear. Empty strings and empty lists are left out.

## Nodes

| Field | Notes |
| --- | --- |
| `id` | Lower-case name with `_` between words: `system_app` for an app, the system name for a system. The same ids as the Mermaid output. |
| `kind` | `system`, `app` or `person`. |
| `name` | The name as written. |
| `parent` | Apps only (a person has no system): the `id` of the system it is in. Left out when the app has no system. |
| `ownSystem` | Systems only. `true` when the system holds apps of ours (the viewer draws a boundary), `false` for another system known only from collaborators. |
| `hasMeta` | Apps only (a person has no `Meta` to be without). `true` when the app has its own `[Meta]`; `false` when it is known only from a collaborator that names it. |
| `appKind` | `Service`, `WebApp`, `Api`, `Worker`, `Database` or `Queue`. Left out when unspecified, and for a person (its `kind` says so). |
| `relationship` | `Owned`, `Shared`, `Internal` or `External`. For a node known only from a collaborator, what that collaborator said. |
| `description`, `tech`, `owner`, `repository`, `tier`, `lifecycle` | From the app's `Meta`; for a node known only from a collaborator, from that collaborator. |
| `responsibilities` | Apps with a `Meta` only: the description of each responsibility. |
| `components` | Only with `diagram --detail components`, and on apps with a `Meta` only: each class that carries a responsibility or collaborator, once, as `{ "id", "name", "source", "responsibilities" }`, sorted by name then source. `id` is the app id, `__` and the class name (`_2`, `_3` for a second class of the same name in the app), the same alias as in the component diagram. `source` is relative to the analysed root, or `Assembly::Namespace.Type` for a referenced assembly. `source` and `responsibilities` are left out when empty. Added in `0.3` ([ADR014](../adr/ADR014-components-in-the-explorer-panel.md)). |

With `diagram --detail members` a component may also have, each left out when empty (added in `0.4`, [ADR017](../adr/ADR017-member-detail.md); `dependsOn` and `usedBy` replaced `uses` in `0.5`, [ADR018](../adr/ADR018-component-dependencies.md)):

| Field | Notes |
| --- | --- |
| `constructors` | Each public constructor's parameters in order, as `[ { "name", "type" } ]`. |
| `dependsOn` | The other components of the same app this class reaches in code, directly or through unannotated types, as `{ "id", "via" }`: `via` is the names of the unannotated types in between along the shortest chain, `[]` when direct. In the order of the class's dependencies (name, then source). |
| `usedBy` | The `id`s of the components of the same app whose `dependsOn` names this one, in component order. |
| `members` | The public members declared on the class: `{ "kind", "name", "type", "parameters" }`, `kind` being `Method`, `Property`, `Field` or `Event`. `parameters` only for methods (and C# indexers). Types are as the language writes them. |

Language is not part of the graph.

## Edges

| Field | Notes |
| --- | --- |
| `from`, `to` | Node ids. With a collaborator `Direction` of `Upstream` the other node is the source. |
| `direction` | `Upstream` or `Downstream`, as written on the collaborator. |
| `protocol` | The collaborator's `Protocol`. |
| `label` | The collaborator's `DataDescription`. |
| `fromComponents`, `toComponents` | The class ids at each end, left out when empty. The end with the collaborator: the class it is on. The other end, when it is one of our apps: its classes whose collaborators name this app back. Empty means the node as a whole. The viewer draws lines to these when an app is opened. Only with `diagram --detail components`. Added in `0.3`. |

An edge goes to the app when the collaborator names an app in one of our systems, and to the system when it is in another system.
A collaborator that points at its own app is left out. The viewer draws one line per pair of nodes however many edges there are.

## How nodes are resolved

The rules are those of the level 2 diagram ([ADR007](../adr/ADR007-container-diagram-and-app-kind.md),
[ADR011](../adr/ADR011-interactive-html-explorer.md)):

- An app with a `Meta` (and an `AppName`) is a node; one without has no identity and is skipped.
- A system that holds such an app is an own system. A collaborator without a system takes its owner's system, or `External`
  when it is external.
- A collaborator naming an app in an own system, with no `Meta` of its own, becomes an app node with `hasMeta` `false`.
- A collaborator with `Kind` `Person` and an `AppName` becomes a `person` node, whatever system it names: its `id` is
  `person_` and the name, it has no `parent`, and persons with the same name (ignoring case) are one node. One without a
  name is skipped.
- A collaborator in another system becomes a system node. When apps disagree about the same node, the one with its own
  `Meta` wins, then the first in order.
