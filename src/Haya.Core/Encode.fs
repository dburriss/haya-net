namespace Haya.Core

open Flexi.Json
open Haya.Core.Analysis

/// Explicit `JsonValue` encoders for the descriptor model. Property names are camelCase;
/// these are the shapes written by `describe`.
module Encode =

    let private str (s: string) = JsonValue.String s

    let private strings (xs: string list) = xs |> List.map str |> List.toArray |> JsonValue.Array

    let private record (props: (string * JsonValue) list) = props |> List.toArray |> JsonValue.Record

    let direction (d: Direction) =
        match d with
        | Direction.Upstream -> "Upstream"
        | _ -> "Downstream"

    let relationship (r: Relationship) =
        match r with
        | Relationship.Owned -> "Owned"
        | Relationship.Shared -> "Shared"
        | Relationship.Internal -> "Internal"
        | Relationship.External -> "External"
        | Relationship.InternalUser -> "InternalUser"
        | _ -> "ExternalUser"

    let appKind (k: AppKind) =
        match k with
        | AppKind.Service -> "Service"
        | AppKind.WebApp -> "WebApp"
        | AppKind.Api -> "Api"
        | AppKind.Worker -> "Worker"
        | AppKind.Database -> "Database"
        | AppKind.Queue -> "Queue"
        | _ -> ""

    let language (l: Language) =
        match l with
        | Language.DotNet -> "dotnet"
        | Language.JavaScript -> "javascript"
        | _ -> ""

    let responsibility (x: ResponsibilityDescriptor) =
        record [
            "description", str x.Description
            "componentName", str x.ComponentName
            "componentSource", str x.ComponentSource
        ]

    let collaborator (x: CollaboratorDescriptor) =
        record [
            "direction", str (direction x.Direction)
            "tech", str x.Tech
            "protocol", str x.Protocol
            "dataDescription", str x.DataDescription
            "description", str x.Description
            "appName", str x.AppName
            "system", str x.System
            "relationship", str (relationship x.Relationship)
            "componentName", str x.ComponentName
            "componentSource", str x.ComponentSource
            "repository", str x.Repository
            "owner", str x.Owner
        ]

    let meta (x: MetaDescriptor) =
        record [
            "appName", str x.AppName
            "description", str x.Description
            "system", str x.System
            "repository", str x.Repository
            "owner", str x.Owner
            // optional: left out when unset so documents without them stay unchanged
            if x.Tech <> "" then "tech", str x.Tech
            if x.Kind <> AppKind.Unspecified then "kind", str (appKind x.Kind)
            if x.Lifecycle <> "" then "lifecycle", str x.Lifecycle
            if x.Tier <> "" then "tier", str x.Tier
        ]

    /// One app of the document written by `describe`. `meta` is omitted when there is none.
    let app (x: App) =
        record [
            "collaborators", x.Collaborators |> List.map collaborator |> List.toArray |> JsonValue.Array
            match x.Meta with
            | Some m -> "meta", meta m
            | None -> ()
            "responsibilities", x.Responsibilities |> List.map responsibility |> List.toArray |> JsonValue.Array
            // optional: left out when unknown so documents without it stay unchanged
            if x.Language <> Language.Unknown then "language", str (language x.Language)
        ]

    /// The document written by `describe`: `{ apps: [...], version }`.
    let description (version: string) (apps: App list) =
        record [
            "apps", apps |> List.map app |> List.toArray |> JsonValue.Array
            "version", str version
        ]

    /// Version of the graph document embedded in the interactive html diagram.
    [<Literal>]
    let GraphVersion = "0.1"

    /// The resolved graph the html explorer reads: `{ edges: [...], nodes: [...], version }`. Empty strings and lists
    /// are left out. `Language` and component sources are deliberately not part of it.
    let graph (g: Graph.Graph) =
        let optional (name: string) (value: string) = if value = "" then [] else [ name, str value ]
        let node (n: Graph.Node) =
            record [
                yield "id", str n.Id
                yield "kind", str (if n.Kind = Graph.NodeKind.System then "system" else "app")
                yield! optional "name" n.Name
                match n.Parent with
                | Some p -> yield "parent", str p
                | None -> ()
                if n.Kind = Graph.NodeKind.System then yield "ownSystem", JsonValue.Boolean n.OwnSystem
                else yield "hasMeta", JsonValue.Boolean n.HasMeta
                yield! optional "appKind" (appKind n.AppKind)
                yield "relationship", str (relationship n.Relationship)
                yield! optional "description" n.Description
                yield! optional "tech" n.Tech
                yield! optional "owner" n.Owner
                yield! optional "repository" n.Repository
                yield! optional "tier" n.Tier
                yield! optional "lifecycle" n.Lifecycle
                if not n.Responsibilities.IsEmpty then yield "responsibilities", strings n.Responsibilities
            ]
        let edge (e: Graph.Edge) =
            record [
                yield "from", str e.From
                yield "to", str e.To
                yield "direction", str (direction e.Direction)
                yield! optional "protocol" e.Protocol
                yield! optional "label" e.Label
            ]
        record [
            "edges", g.Edges |> List.map edge |> List.toArray |> JsonValue.Array
            "nodes", g.Nodes |> List.map node |> List.toArray |> JsonValue.Array
            "version", str GraphVersion
        ]

    /// Backstage `Component` entity.
    let backstageComponent
        (name: string) (title: string) (description: string)
        (owner: string) (domain: string)
        (dependsOn: string list) (dependencyOf: string list) =
        record [
            "apiVersion", str "backstage.io/v1alpha1"
            "dependencyOf", strings dependencyOf
            "dependsOn", strings dependsOn
            "kind", str "Component"
            "metadata", record [ "description", str description; "name", str name; "title", str title ]
            "spec", record [ "domain", str domain; "lifecycle", str "production"; "owner", str owner ]
        ]

    /// What a Datadog Software Catalog (schema v3) entity is written from. Empty strings and lists are left out.
    type DatadogEntity =
        { /// `service`, `datastore`, `queue` or `system`.
          Kind: string
          /// `metadata.name`, which other entities refer to as `kind:name`.
          Name: string
          DisplayName: string
          Description: string
          Owner: string
          Lifecycle: string
          Tier: string
          /// `spec.type`, e.g. `web`. Free text in the schema.
          Type: string
          /// `spec.languages`, only valid on a `service`.
          Languages: string list
          /// `spec.dependsOn` as `kind:name`. Not valid on a `system`.
          DependsOn: string list
          /// `spec.components` as `kind:name`. Only valid on a `system`.
          Components: string list
          /// `datadog.codeLocations[].repositoryURL`.
          RepositoryUrl: string }

    /// Datadog Software Catalog `entity.datadog.yaml` entity, schema version v3. Each kind's `spec` rejects keys it
    /// does not define, so callers only fill in what the kind has.
    let datadogEntity (x: DatadogEntity) =
        let optional (name: string) (value: string) = if value <> "" then [ name, str value ] else []
        let optionalList (name: string) (values: string list) = if values.IsEmpty then [] else [ name, strings values ]
        let spec =
            optional "lifecycle" x.Lifecycle
            @ optional "tier" x.Tier
            @ optional "type" x.Type
            @ optionalList "languages" x.Languages
            @ optionalList "dependsOn" x.DependsOn
            @ optionalList "components" x.Components
        record [
            "apiVersion", str "v3"
            "kind", str x.Kind
            "metadata", record (
                [ "name", str x.Name ]
                @ (if x.DisplayName <> x.Name then optional "displayName" x.DisplayName else [])
                @ optional "description" x.Description
                @ optional "owner" x.Owner)
            if not spec.IsEmpty then "spec", record spec
            if x.RepositoryUrl <> "" then
                "datadog", record [ "codeLocations", [| record [ "repositoryURL", str x.RepositoryUrl ] |] |> JsonValue.Array ]
        ]
