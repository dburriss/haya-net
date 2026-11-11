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

    /// A dependency of a class on another class of its app (ADR018): its id, and the unannotated types in between.
    type ComponentLink = { Id: string; Via: string list }

    /// A class of an app that carries a `[Responsibility]` or `[Collaborator]`: its name and the source it was found in
    /// (relative to the analysed root; `Assembly::Namespace.Type` for a type from a referenced assembly).
    type Component =
        { /// The app id, `__`, then the class name; a second class of the same name in the app gets `_2`, and so on.
          Id: string
          Name: string
          Source: string
          /// The descriptions of the class's responsibilities, in the order they were found.
          Responsibilities: string list
          /// `Detail.Members` only: each public constructor's parameters.
          Constructors: Parameter list list
          /// `Detail.Members` only: the public members declared on the class.
          Members: Member list
          /// `Detail.Members` only: the other classes of the same app this class reaches in code, by name then source.
          DependsOn: ComponentLink list
          /// `Detail.Members` only: the ids of the classes of the same app that depend on this one, in component order.
          UsedBy: string list }

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
          Responsibilities: string list
          /// Apps with a `Meta` only: each class that carries a responsibility or collaborator, once, by name then source.
          Components: Component list }

    type Edge =
        { From: string
          To: string
          Direction: Direction
          Protocol: string
          Label: string
          /// The ids of the classes at the `From` end: the class the collaborator is on, or, at the far end of one, the
          /// classes of that app that name this one back. Empty when the end is the node as a whole.
          FromComponents: string list
          /// The ids of the classes at the `To` end, as `FromComponents`.
          ToComponents: string list }

    type Graph = { Nodes: Node list; Edges: Edge list }

    let private emptyNode =
        { Id = ""; Kind = NodeKind.App; Name = ""; Parent = None; OwnSystem = false; HasMeta = false
          AppKind = AppKind.Unspecified; Relationship = Relationship.Owned
          Description = ""; Tech = ""; Owner = ""; Repository = ""; Tier = ""; Lifecycle = ""; Responsibilities = []
          Components = [] }

    let private appId (system: string) (appName: string) = Naming.mermaidId (Naming.createName system appName)
    let private systemId (system: string) = Naming.mermaidId system
    let private sameKey (system: string) (appName: string) = String.toLower system + "/" + String.toLower appName

    /// Resolve each class's dependencies (by name and source) to the ids of the other components of its app, and fill
    /// `UsedBy` as their inverse. A dependency on a class that is not a component of the app is dropped.
    let private linkDependencies (details: ComponentDetail list) (components: Component list) =
        let idOf name source = components |> List.tryFind (fun x -> x.Name = name && x.Source = source) |> Option.map (_.Id)
        let linked =
            components
            |> List.map (fun c ->
                let deps =
                    details
                    |> List.tryFind (fun d -> d.Name = c.Name && d.Source = c.Source)
                    |> Option.map (_.Dependencies)
                    |> Option.defaultValue []
                { c with
                    DependsOn =
                        deps
                        |> List.choose (fun d -> idOf d.Name d.Source |> Option.filter (fun id -> id <> c.Id) |> Option.map (fun id -> { Id = id; Via = d.Via }))
                        |> List.distinctBy (_.Id) })
        linked
        |> List.map (fun c ->
            { c with UsedBy = linked |> List.filter (fun x -> x.DependsOn |> List.exists (fun l -> l.Id = c.Id)) |> List.map (_.Id) })

    /// Resolve the apps into nodes and edges. Output is ordered by id (nodes) and by first appearance (edges) so it is deterministic.
    /// The classes of the apps (`Node.Components`, `Edge.FromComponents`/`ToComponents`) are only filled from `Detail.Components`,
    /// and their constructors, members and dependencies only at `Detail.Members`.
    let ofApps (detail: Detail) (apps: App list) : Graph =
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

        // one entry per class, however many attributes it carries; a descriptor without a class name is not one
        let componentsOf ((m: MetaDescriptor), (a: App)) =
            if not (Detail.atLeast Detail.Components detail) then [] else
            let detailOf (name: string) (source: string) =
                if Detail.atLeast Detail.Members detail then
                    a.Components |> List.tryFind (fun d -> d.Name = name && d.Source = source)
                else None
            (a.Responsibilities |> List.map (fun r -> r.ComponentName, r.ComponentSource))
            @ (a.Collaborators |> List.map (fun c -> c.ComponentName, c.ComponentSource))
            |> List.filter (fun (name, _) -> name <> "")
            |> List.distinct
            |> List.sort
            |> List.mapi (fun i (name, source) -> i, (name, source))
            |> fun all ->
                all
                |> List.map (fun (i, (name, source)) ->
                    let before = all |> List.filter (fun (j, (n, _)) -> j < i && n = name) |> List.length
                    let id = appId m.System m.AppName + "__" + Naming.mermaidId name
                    { Id = (if before = 0 then id else $"{id}_{before + 1}")
                      Name = name
                      Source = source
                      Responsibilities =
                          a.Responsibilities
                          |> List.filter (fun r -> r.ComponentName = name && r.ComponentSource = source)
                          |> List.map (_.Description)
                      Constructors = detailOf name source |> Option.map (_.Constructors) |> Option.defaultValue []
                      Members = detailOf name source |> Option.map (_.Members) |> Option.defaultValue []
                      DependsOn = []
                      UsedBy = [] })
                |> fun cs -> if Detail.atLeast Detail.Members detail then linkDependencies a.Components cs else cs

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
                    Responsibilities = a.Responsibilities |> List.map (_.Description)
                    Components = componentsOf (m, a) })
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

        // the classes of an app that a collaborator is on, by app id
        let componentsById = ownApps |> List.map (fun (m, a) -> appId m.System m.AppName, componentsOf (m, a))
        let classesOf (app: string) (c: CollaboratorDescriptor) =
            componentsById
            |> List.tryFind (fun (id, _) -> id = app)
            |> Option.bind (fun (_, cs) -> cs |> List.tryFind (fun x -> x.Name = c.ComponentName && x.Source = c.ComponentSource))
            |> Option.map (_.Id)
            |> Option.toList
        // every collaborator of an own app, resolved: (app id, collaborator, target id)
        let resolved =
            ownApps
            |> List.collect (fun (m, a) ->
                let self = appId m.System m.AppName
                a.Collaborators
                |> List.choose (fun c -> targetOf c |> Option.filter (fun target -> target <> self) |> Option.map (fun target -> self, c, target)))
        // the classes of `app` whose collaborators name `other`
        let namingBack (app: string) (other: string) =
            resolved
            |> List.filter (fun (self, _, target) -> self = app && target = other)
            |> List.collect (fun (self, c, _) -> classesOf self c)
            |> List.distinct

        let edges =
            resolved
            |> List.map (fun (self, c, target) ->
                let mine = classesOf self c
                let theirs = namingBack target self
                let edge =
                    if c.Direction = Direction.Upstream then
                        { From = target; To = self; Direction = c.Direction; Protocol = c.Protocol; Label = c.DataDescription
                          FromComponents = []; ToComponents = [] }
                    else
                        { From = self; To = target; Direction = c.Direction; Protocol = c.Protocol; Label = c.DataDescription
                          FromComponents = []; ToComponents = [] }
                let fromClasses, toClasses = if c.Direction = Direction.Upstream then theirs, mine else mine, theirs
                edge, (fromClasses, toClasses))
            // one edge per app pair, direction, protocol and label, as before; the classes of all its collaborators together
            |> fun all ->
                all
                |> List.map fst
                |> List.distinct
                |> List.map (fun edge -> edge, all |> List.filter (fun (e, _) -> e = edge))
            |> List.map (fun (edge, xs) ->
                { edge with
                    FromComponents = xs |> List.collect (fun (_, (f, _)) -> f) |> List.distinct
                    ToComponents = xs |> List.collect (fun (_, (_, t)) -> t) |> List.distinct })

        let nodes =
            ownSystemNodes @ otherSystemNodes @ personNodes @ appNodes @ declaredNodes
            // a node with its own meta wins over the same id declared from a collaborator
            |> List.distinctBy (_.Id)
            |> List.sortBy (_.Id)
        { Nodes = nodes; Edges = edges }
