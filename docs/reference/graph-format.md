# Graph format

The graph that `haya diagram -f html` embeds in the page and the viewer reads. It is resolved from the apps by `Graph.ofApps`
in `Haya.Core`, written by `Encode.graph`, and identical on both stacks. The current version is `0.2`. It is an internal
format for the page: it may change between versions, so read the [describe format](describe-format.md) if you need data
to consume.

## Where it is

One `<script type="application/json" id="haya-data">` element in the page. `<` and the line separators U+2028 and U+2029 inside
values are written as `\u` escapes, so the document is still valid JSON and no value can end the script element.

## Document

```json
{
  "edges": [
    { "from": "web_shop", "to": "ordering_payments_api", "direction": "Upstream", "protocol": "HTTPS", "label": "Request payment" }
  ],
  "nodes": [
    { "id": "ordering", "kind": "system", "name": "Ordering", "ownSystem": true, "relationship": "Owned" },
    {
      "id": "ordering_payments_api", "kind": "app", "name": "PaymentsApi", "parent": "ordering", "hasMeta": true,
      "appKind": "Api", "relationship": "Owned", "description": "Handles processing payment for a shopping cart",
      "tech": ".NET 10 / ASP.NET Core", "owner": "Ordering Team", "repository": "org/payment-api",
      "tier": "1", "lifecycle": "production", "responsibilities": [ "Handles order processing" ]
    }
  ],
  "version": "0.2"
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

Language and component sources are not part of the graph.

## Edges

| Field | Notes |
| --- | --- |
| `from`, `to` | Node ids. With a collaborator `Direction` of `Upstream` the other node is the source. |
| `direction` | `Upstream` or `Downstream`, as written on the collaborator. |
| `protocol` | The collaborator's `Protocol`. |
| `label` | The collaborator's `DataDescription`. |

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
