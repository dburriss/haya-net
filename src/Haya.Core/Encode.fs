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
        | _ -> "External"

    let appKind (k: AppKind) =
        match k with
        | AppKind.Service -> "Service"
        | AppKind.WebApp -> "WebApp"
        | AppKind.Api -> "Api"
        | AppKind.Worker -> "Worker"
        | AppKind.Database -> "Database"
        | AppKind.Queue -> "Queue"
        | AppKind.Person -> "Person"
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
            // optional: left out when unset so documents without it stay unchanged
            if x.Kind <> AppKind.Unspecified then "kind", str (appKind x.Kind)
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

    let memberKind (k: MemberKind) =
        match k with
        | MemberKind.Method -> "Method"
        | MemberKind.Property -> "Property"
        | MemberKind.Field -> "Field"
        | _ -> "Event"

    let private array (f: 'a -> JsonValue) (xs: 'a list) = xs |> List.map f |> List.toArray |> JsonValue.Array

    let parameter (x: Parameter) = record [ "name", str x.Name; "type", str x.Type ]

    let constructors (xs: Parameter list list) = xs |> array (array parameter)

    let classMember (x: Member) =
        record [
            "kind", str (memberKind x.Kind)
            "name", str x.Name
            "type", str x.Type
            // methods and indexers only
            if not x.Parameters.IsEmpty || x.Kind = MemberKind.Method then "parameters", x.Parameters |> array parameter
        ]

    /// A dependency of a class on another annotated class of its app (ADR018). `via` is `[]` when direct.
    let dependency (x: ComponentDependency) =
        record [ "name", str x.Name; "source", str x.Source; "via", x.Via |> array str ]

    /// The constructors, public members and dependencies of one annotated class (`describe --detail members`).
    let componentDetail (x: ComponentDetail) =
        record [
            "constructors", constructors x.Constructors
            // optional: left out when the class reaches no other annotated class
            if not x.Dependencies.IsEmpty then "dependencies", x.Dependencies |> array dependency
            "members", x.Members |> array classMember
            "name", str x.Name
            "source", str x.Source
        ]

    /// One app of the document written by `describe`. `meta` is omitted when there is none.
    let app (x: App) =
        record [
            "collaborators", x.Collaborators |> List.map collaborator |> List.toArray |> JsonValue.Array
            // optional: only written at `--detail members`
            if not x.Components.IsEmpty then "components", x.Components |> array componentDetail
            match x.Meta with
            | Some m -> "meta", meta m
            | None -> ()
            "responsibilities", x.Responsibilities |> List.map responsibility |> List.toArray |> JsonValue.Array
            // optional: left out when unknown so documents without it stay unchanged
            if x.Language <> Language.Unknown then "language", str (language x.Language)
        ]

    /// The document written by `describe`: `{ apps: [...], detail, version }`. Below `Detail.Members` the classes'
    /// members are left out, so `detail` always says what the document holds.
    let description (version: string) (detail: Detail) (apps: App list) =
        let apps = if Detail.atLeast Detail.Members detail then apps else apps |> List.map (fun a -> { a with Components = [] })
        record [
            "apps", apps |> List.map app |> List.toArray |> JsonValue.Array
            "detail", str (Detail.name detail)
            "version", str version
        ]

    /// Version of the graph document embedded in the interactive html diagram.
    [<Literal>]
    let GraphVersion = "0.5"

    /// The resolved graph the html explorer reads: `{ edges: [...], nodes: [...], version }`. Empty strings and lists
    /// are left out. `Language` is deliberately not part of it.
    let graphWith (viewer: ViewerSettings) (g: Graph.Graph) =
        let optional (name: string) (value: string) = if value = "" then [] else [ name, str value ]
        let node (n: Graph.Node) =
            record [
                yield "id", str n.Id
                yield "kind", str (match n.Kind with
                                   | Graph.NodeKind.System -> "system"
                                   | Graph.NodeKind.Person -> "person"
                                   | _ -> "app")
                yield! optional "name" n.Name
                match n.Parent with
                | Some p -> yield "parent", str p
                | None -> ()
                match n.Kind with
                | Graph.NodeKind.System -> yield "ownSystem", JsonValue.Boolean n.OwnSystem
                | Graph.NodeKind.Person -> ()
                | _ -> yield "hasMeta", JsonValue.Boolean n.HasMeta
                yield! optional "appKind" (appKind n.AppKind)
                yield "relationship", str (relationship n.Relationship)
                yield! optional "description" n.Description
                yield! optional "tech" n.Tech
                yield! optional "owner" n.Owner
                yield! optional "repository" n.Repository
                yield! optional "tier" n.Tier
                yield! optional "lifecycle" n.Lifecycle
                if not n.Responsibilities.IsEmpty then yield "responsibilities", strings n.Responsibilities
                if not n.Components.IsEmpty then
                    let comp (c: Graph.Component) =
                        record [
                            yield "id", str c.Id
                            yield "name", str c.Name
                            yield! optional "source" c.Source
                            if not c.Responsibilities.IsEmpty then yield "responsibilities", strings c.Responsibilities
                            // optional: only at `--detail members`
                            if not c.Constructors.IsEmpty then yield "constructors", constructors c.Constructors
                            if not c.DependsOn.IsEmpty then
                                let link (l: Graph.ComponentLink) = record [ "id", str l.Id; "via", l.Via |> array str ]
                                yield "dependsOn", c.DependsOn |> array link
                            if not c.UsedBy.IsEmpty then yield "usedBy", strings c.UsedBy
                            if not c.Members.IsEmpty then yield "members", c.Members |> array classMember
                        ]
                    yield "components", n.Components |> List.map comp |> List.toArray |> JsonValue.Array
            ]
        let edge (e: Graph.Edge) =
            record [
                yield "from", str e.From
                yield "to", str e.To
                yield "direction", str (direction e.Direction)
                yield! optional "protocol" e.Protocol
                yield! optional "label" e.Label
                if not e.FromComponents.IsEmpty then yield "fromComponents", strings e.FromComponents
                if not e.ToComponents.IsEmpty then yield "toComponents", strings e.ToComponents
            ]
        record [
            yield "edges", g.Edges |> List.map edge |> List.toArray |> JsonValue.Array
            yield "nodes", g.Nodes |> List.map node |> List.toArray |> JsonValue.Array
            // optional, only when the configuration sets something: the page falls back to its own defaults
            let ints (xs: int list) = xs |> List.map (fun i -> JsonValue.Integer (int64 i)) |> List.toArray |> JsonValue.Array
            let complexity =
                [ match viewer.Links with Some xs -> yield "links", ints xs | None -> ()
                  match viewer.Systems with Some xs -> yield "systems", ints xs | None -> () ]
            let settings =
                [ match viewer.Detail with Some d -> yield "detail", str (Detail.name d) | None -> ()
                  if not complexity.IsEmpty then yield "complexity", record complexity ]
            if not settings.IsEmpty then yield "viewer", record settings
            yield "version", str GraphVersion
        ]

    let graph (g: Graph.Graph) = graphWith ViewerSettings.none g

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
