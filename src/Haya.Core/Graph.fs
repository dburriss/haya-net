namespace Haya.Core

open Haya.Core.Analysis

/// A resolved graph of systems, apps and the relationships between them. Language-neutral and renderer-neutral:
/// the interactive HTML explorer is built from it. The resolution rules are those of the C4 container diagram
/// (`Diagram.sprintMermaidC4L2`): own systems are boundaries around their apps, other systems are single nodes.
module Graph =

    type NodeKind =
        /// A system that holds our apps (drawn as a boundary), or another system known only from collaborators.
        | System = 1
        /// An app with its own `[Meta]`, or one declared from a collaborator inside one of our systems.
        | App = 2
        /// A person who uses (or is used by) our apps. Known only from collaborators: no app, no system, no `[Meta]`.
        | Person = 3

    type Node =
        { Id: string
          Kind: NodeKind
          Name: string
          /// The id of the system node this app is in. `None` for systems, and for apps without a system.
          Parent: string option
          /// Systems only: true when the system holds apps of ours (a boundary), false for another system.
          OwnSystem: bool
          /// Apps: true when the node has its own `[Meta]`; false when it is known only from a collaborator.
          HasMeta: bool
          AppKind: AppKind
          Relationship: Relationship
          Description: string
          Tech: string
          Owner: string
          Repository: string
          Tier: string
          Lifecycle: string
          Responsibilities: string list }

    type Edge =
        { From: string
          To: string
          Direction: Direction
          Protocol: string
          Label: string }

    type Graph = { Nodes: Node list; Edges: Edge list }

    let private emptyNode =
        { Id = ""; Kind = NodeKind.App; Name = ""; Parent = None; OwnSystem = false; HasMeta = false
          AppKind = AppKind.Unspecified; Relationship = Relationship.Owned
          Description = ""; Tech = ""; Owner = ""; Repository = ""; Tier = ""; Lifecycle = ""; Responsibilities = [] }

    let private appId (system: string) (appName: string) = Naming.mermaidId (Naming.createName system appName)
    let private systemId (system: string) = Naming.mermaidId system
    let private sameKey (system: string) (appName: string) = String.toLower system + "/" + String.toLower appName

    /// Resolve the apps into nodes and edges. Output is ordered by id (nodes) and by first appearance (edges) so it is deterministic.
    let ofApps (apps: App list) : Graph =
        let apps = apps |> List.map App.withDefaultSystems
        // apps without a meta (or app name) have no identity, so they cannot be a node
        let ownApps =
            apps
            |> List.choose (fun a -> a.Meta |> Option.filter (fun m -> m.AppName <> "") |> Option.map (fun m -> m, a))
        let ownSystems = ownApps |> List.map (fun (m, _) -> m.System) |> List.distinct
        let ownKeys = ownApps |> List.map (fun (m, _) -> sameKey m.System m.AppName)

        // collaborators in one of our systems (or without any system) that name an app
        let isOwnSystem (c: CollaboratorDescriptor) =
            c.Kind <> AppKind.Person && c.AppName <> "" && (c.System = "" || List.contains c.System ownSystems)
        let collaborators = ownApps |> List.collect (fun (_, a) -> a.Collaborators)
        let declaredHere =
            collaborators
            |> List.filter isOwnSystem
            |> List.filter (fun c -> not (List.contains (sameKey c.System c.AppName) ownKeys))
            |> List.distinctBy (fun c -> sameKey c.System c.AppName)
        let otherSystems =
            collaborators
            |> List.filter (fun c -> c.Kind <> AppKind.Person && c.System <> "" && not (List.contains c.System ownSystems))
            |> List.distinctBy (_.System)
        // a person is known by name alone, whatever system the collaborator names; one without a name cannot be drawn
        let persons =
            collaborators
            |> List.filter (fun c -> c.Kind = AppKind.Person && c.AppName <> "")
            |> List.distinctBy (fun c -> String.toLower c.AppName)

        let parentOf (system: string) = if system = "" then None else Some (systemId system)

        let appNodes =
            ownApps
            |> List.map (fun (m, a) ->
                { emptyNode with
                    Id = appId m.System m.AppName
                    Name = m.AppName
                    Parent = parentOf m.System
                    HasMeta = true
                    AppKind = m.Kind
                    Description = m.Description
                    Tech = m.Tech
                    Owner = m.Owner
                    Repository = m.Repository
                    Tier = m.Tier
                    Lifecycle = m.Lifecycle
                    Responsibilities = a.Responsibilities |> List.map (_.Description) })
        let declaredNodes =
            declaredHere
            |> List.map (fun c ->
                { emptyNode with
                    Id = appId c.System c.AppName
                    Name = c.AppName
                    Parent = parentOf c.System
                    Relationship = c.Relationship
                    Description = c.Description
                    Tech = c.Tech
                    Owner = c.Owner
                    Repository = c.Repository })

        let ownSystemNodes =
            (ownSystems @ (declaredHere |> List.map (_.System)))
            |> List.distinct
            |> List.filter (fun s -> s <> "")
            |> List.map (fun s -> { emptyNode with Id = systemId s; Kind = NodeKind.System; Name = s; OwnSystem = true })
        let otherSystemNodes =
            otherSystems
            |> List.map (fun c ->
                { emptyNode with
                    Id = systemId c.System
                    Kind = NodeKind.System
                    Name = c.System
                    Relationship = c.Relationship
                    Description = c.Description })

        let personNodes =
            persons
            |> List.map (fun c ->
                { emptyNode with
                    Id = Naming.personId c.AppName
                    Kind = NodeKind.Person
                    Name = c.AppName
                    Relationship = c.Relationship
                    Description = c.Description
                    Owner = c.Owner })

        // the id a collaborator resolves to, if it is a node at all
        let targetOf (c: CollaboratorDescriptor) =
            if c.Kind = AppKind.Person then (if c.AppName <> "" then Some (Naming.personId c.AppName) else None)
            elif isOwnSystem c then Some (appId c.System c.AppName)
            elif c.System <> "" then Some (systemId c.System)
            else None

        let edges =
            ownApps
            |> List.collect (fun (m, a) ->
                let self = appId m.System m.AppName
                a.Collaborators
                |> List.choose (fun c ->
                    targetOf c
                    |> Option.filter (fun target -> target <> self)
                    |> Option.map (fun target ->
                        let from, to_ = if c.Direction = Direction.Upstream then target, self else self, target
                        { From = from; To = to_; Direction = c.Direction; Protocol = c.Protocol; Label = c.DataDescription })))
            |> List.distinct

        let nodes =
            ownSystemNodes @ otherSystemNodes @ personNodes @ appNodes @ declaredNodes
            // a node with its own meta wins over the same id declared from a collaborator
            |> List.distinctBy (_.Id)
            |> List.sortBy (_.Id)
        { Nodes = nodes; Edges = edges }
