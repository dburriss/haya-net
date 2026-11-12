namespace Haya.Core

open Haya.Core.Analysis


module Diagram =

    /// C4 system context for all the apps. Apps are grouped by system; collaborators in another system
    /// become relationships (to a known system, or to an external/internal system declared from the collaborator).
    let sprintMermaidC4L1 (apps: App list) =
        let apps = apps |> List.map App.withDefaultSystems
        let metas = apps |> List.map App.meta
        let ownSystems = metas |> List.map (_.System) |> List.distinct
        let isOther (x: CollaboratorDescriptor) = x.Kind <> AppKind.Person && x.System <> "" && not (List.contains x.System ownSystems)
        let collaborators = apps |> List.collect (_.Collaborators)
        // a person is known by name alone; one without a name cannot be drawn
        let persons = collaborators
                      |> List.filter (fun x -> x.Kind = AppKind.Person && x.AppName <> "")
                      |> List.distinctBy (fun x -> String.toLower x.AppName)
        let externalPersons = persons
                              |> List.filter (fun x -> x.Relationship = Relationship.External)
                              |> List.map (fun x -> $"Person_Ext({Naming.personId x.AppName}, {x.AppName}, \"{x.Description}\")")
        let internalPersons = persons
                              |> List.filter (fun x -> x.Relationship <> Relationship.External)
                              |> List.map (fun x -> $"  Person({Naming.personId x.AppName}, {x.AppName}, \"{x.Description}\")")

        let externalSystems = collaborators
                              |> List.filter (fun x -> x.Relationship = Relationship.External && isOther x)
                              |> List.distinctBy (_.System)
                              |> List.map (fun x -> $"System_Ext({String.toLower x.System}, {x.System}, \"{x.Description}\")")
        let internalSystems = collaborators
                              |> List.filter (fun x -> x.Relationship = Relationship.Internal && isOther x)
                              |> List.distinctBy (_.System)
                              |> List.map (fun x -> $"  System({String.toLower x.System}, {x.System}, \"{x.Description}\")")
        let thisSystems = ownSystems
                          |> List.map (fun system ->
                              let description =
                                  metas
                                  |> List.filter (fun m -> m.System = system && m.Description <> "")
                                  |> List.tryHead
                                  |> Option.map (_.Description)
                                  |> Option.defaultValue ""
                              $"  System({String.toLower system}, {system}, \"{description}\")")
        let relationships = apps
                            |> List.collect (fun app ->
                                let meta = App.meta app
                                let toSystems =
                                    app.Collaborators
                                    |> List.filter (fun x -> x.Kind <> AppKind.Person && x.System <> meta.System && x.System <> "")
                                    |> List.map (fun x ->
                                        if x.Direction = Direction.Upstream then
                                            $"Rel({String.toLower x.System}, {String.toLower meta.System}, \"{x.DataDescription}\")"
                                        else $"Rel({String.toLower meta.System}, {String.toLower x.System}, \"{x.DataDescription}\")")
                                let toPersons =
                                    app.Collaborators
                                    |> List.filter (fun x -> x.Kind = AppKind.Person && x.AppName <> "" && meta.System <> "")
                                    |> List.map (fun x ->
                                        let person = Naming.personId x.AppName
                                        if x.Direction = Direction.Upstream then
                                            $"Rel({person}, {String.toLower meta.System}, \"{x.DataDescription}\")"
                                        else $"Rel({String.toLower meta.System}, {person}, \"{x.DataDescription}\")")
                                toSystems @ toPersons)
                            |> List.distinct

        let title = String.concat ", " ownSystems
        let diagram =
            SB.create "C4Context"
            |> SB.emptyLine
            |> SB.line $"title C4 System Diagram for {title}"

            // enterprise boundary
            |> SB.emptyLine
            |> SB.line "Enterprise_Boundary(Enterprise, Enterprise, \"The enterprise boundary\") {"
            |> SB.lines internalPersons
            |> SB.lines  internalSystems
            |> SB.lines thisSystems
            |> SB.line "}"
            // external systems and people
            |> SB.lines externalPersons
            |> SB.lines externalSystems
            // relationships
            |> SB.emptyLine
            |> SB.lines relationships
            // string
            |> SB.toString

        diagram

    /// C4 container diagram for all the apps. Each app (with a meta) is a container inside the boundary of its
    /// system; the shape follows the app's kind. Collaborators become relationships: to the container of a known
    /// app, to a container declared from the collaborator when it is in one of our systems, or to the system
    /// when it is in another one.
    let sprintMermaidC4L2 (apps: App list) =
        let apps = apps |> List.map App.withDefaultSystems
        // apps without a meta (or app name) have no identity, so they cannot be a container
        let ownApps = apps |> List.choose (fun a -> a.Meta |> Option.filter (fun m -> m.AppName <> "") |> Option.map (fun m -> m, a))
        let ownSystems = ownApps |> List.map (fun (m, _) -> m.System) |> List.distinct
        let containerId (system: string) (appName: string) = Naming.mermaidId (Naming.createName system appName)
        let sameKey (system: string) (appName: string) = String.toLower system + "/" + String.toLower appName
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

        let element (kind: AppKind) (system: string) (name: string) (tech: string) (description: string) =
            let shape =
                match kind with
                | AppKind.Database -> "ContainerDb"
                | AppKind.Queue -> "ContainerQueue"
                | _ -> "Container"
            $"{shape}({containerId system name}, \"{name}\", \"{tech}\", \"{description}\")"

        let containersOf (system: string) =
            let own = ownApps |> List.filter (fun (m, _) -> m.System = system) |> List.map (fun (m, _) -> element m.Kind m.System m.AppName m.Tech m.Description)
            let declared = declaredHere |> List.filter (fun c -> c.System = system) |> List.map (fun c -> element AppKind.Unspecified c.System c.AppName c.Tech c.Description)
            own @ declared
        // a collaborator without a system in an app that has one is already defaulted, so "" means we have no systems at all
        let systemsToDraw = (ownSystems @ (declaredHere |> List.map (_.System))) |> List.distinct
        let boundaries =
            systemsToDraw
            |> List.collect (fun system ->
                let inner = containersOf system
                if system = "" then inner
                else [ yield $"System_Boundary({Naming.mermaidId system}, \"{system}\") {{"; yield! inner |> List.map (fun x -> "  " + x); yield "}" ])

        let otherSystemElements =
            otherSystems
            |> List.map (fun c ->
                let shape = if c.Relationship = Relationship.External then "System_Ext" else "System"
                $"{shape}({Naming.mermaidId c.System}, \"{c.System}\", \"{c.Description}\")")

        let personElements =
            persons
            |> List.map (fun c ->
                let shape = if c.Relationship = Relationship.External then "Person_Ext" else "Person"
                $"{shape}({Naming.personId c.AppName}, \"{c.AppName}\", \"{c.Description}\")")

        // the id a collaborator is drawn as, if it is drawn at all
        let targetOf (c: CollaboratorDescriptor) =
            if c.Kind = AppKind.Person then (if c.AppName <> "" then Some (Naming.personId c.AppName) else None)
            elif isOwnSystem c then Some (containerId c.System c.AppName)
            elif c.System <> "" then Some (Naming.mermaidId c.System)
            else None
        let relationships =
            ownApps
            |> List.collect (fun (m, a) ->
                let self = containerId m.System m.AppName
                a.Collaborators
                |> List.choose (fun c ->
                    targetOf c
                    |> Option.filter (fun target -> target <> self)
                    |> Option.map (fun target ->
                        let from, to_ = if c.Direction = Direction.Upstream then target, self else self, target
                        if c.Protocol = "" then $"Rel({from}, {to_}, \"{c.DataDescription}\")"
                        else $"Rel({from}, {to_}, \"{c.DataDescription}\", \"{c.Protocol}\")")))
            |> List.distinct

        let title = String.concat ", " (ownSystems |> List.filter (fun s -> s <> ""))
        SB.create "C4Container"
        |> SB.emptyLine
        |> SB.line $"title C4 Container Diagram for {title}"
        |> SB.emptyLine
        |> SB.lines boundaries
        |> SB.lines otherSystemElements
        |> SB.lines personElements
        |> SB.emptyLine
        |> SB.lines relationships
        |> SB.toString

    /// The technology of a class, from the extension of its source file. A type from a referenced assembly
    /// (`Assembly::Namespace.Type`), or an unknown extension, has none.
    let componentTech (source: string) =
        let file = source.Substring(source.LastIndexOf('/') + 1)
        let dot = file.LastIndexOf('.')
        if source.Contains("::") || dot < 0 then ""
        else
            match String.toLower (file.Substring dot) with
            | ".cs" -> "C#"
            | ".fs" -> "F#"
            | ".vb" -> "Visual Basic"
            | ".ts" | ".tsx" | ".mts" | ".cts" -> "TypeScript"
            | ".js" | ".jsx" | ".mjs" | ".cjs" -> "JavaScript"
            | _ -> ""

    /// C4 component diagram for all the apps. Each app (with a meta) that has classes is a boundary around them, inside
    /// the boundary of its system; an app without classes, or one known only from a collaborator, is a container as in
    /// the container diagram. A class is drawn with the technology of its source file and its responsibilities.
    /// A collaborator is a relationship from its class, to the classes at the other end (`Graph.Edge.ToComponents`); when an
    /// end with classes has none of them, to a container for that app inside its boundary.
    /// At `Detail.Members`, each dependency between two classes of one app is a `uses` relationship (ADR018).
    let sprintMermaidC4L3 (detail: Detail) (apps: App list) =
        let detail = if Detail.atLeast Detail.Members detail then Detail.Members else Detail.Components
        let g = Graph.ofApps detail apps
        let node id = g.Nodes |> List.find (fun n -> n.Id = id)
        let hasClasses (n: Graph.Node) = n.Kind = Graph.NodeKind.App && not n.Components.IsEmpty

        // the container that stands for an app with classes when a relationship cannot name one of them
        let appContainerId (app: string) = app + "__app"
        // the drawn ends of one end of an edge: its classes, or the node (its stand-in container when it has classes)
        let endOf (id: string) (classes: string list) =
            if not classes.IsEmpty then classes
            elif hasClasses (node id) then [ appContainerId id ]
            else [ id ]
        let ends =
            g.Edges
            |> List.collect (fun e ->
                [ for f in endOf e.From e.FromComponents do
                    for t in endOf e.To e.ToComponents do
                        yield e, f, t ])
        let relationships =
            ends
            |> List.map (fun (e, from, to_) ->
                if e.Protocol = "" then $"Rel({from}, {to_}, \"{e.Label}\")"
                else $"Rel({from}, {to_}, \"{e.Label}\", \"{e.Protocol}\")")
            |> List.distinct
        // only at members: the code dependencies between the classes of one app, in component order
        let dependencies =
            g.Nodes
            |> List.collect (fun n -> n.Components)
            |> List.collect (fun c -> c.DependsOn |> List.map (fun l -> $"Rel({c.Id}, {l.Id}, \"uses\")"))
        // the app containers that a relationship needs
        let appContainers =
            ends
            |> List.collect (fun (_, a, b) -> [ a; b ])
            |> List.filter (fun id -> id.EndsWith "__app")
            |> List.distinct

        let container (n: Graph.Node) =
            let shape =
                match n.AppKind with
                | AppKind.Database -> "ContainerDb"
                | AppKind.Queue -> "ContainerQueue"
                | _ -> "Container"
            $"{shape}({n.Id}, \"{n.Name}\", \"{n.Tech}\", \"{n.Description}\")"
        let appElements (n: Graph.Node) =
            if hasClasses n then
                [ yield $"Container_Boundary({n.Id}, \"{n.Name}\") {{"
                  if List.contains (appContainerId n.Id) appContainers then
                      yield $"  Container({appContainerId n.Id}, \"{n.Name}\", \"{n.Tech}\", \"{n.Description}\")"
                  for c in n.Components do
                      let responsibilities = String.concat "; " c.Responsibilities
                      yield $"  Component({c.Id}, \"{c.Name}\", \"{componentTech c.Source}\", \"{responsibilities}\")"
                  yield "}" ]
            else [ container n ]
        let appsIn (parent: string option) =
            g.Nodes |> List.filter (fun n -> n.Kind = Graph.NodeKind.App && n.Parent = parent) |> List.collect appElements

        let ownSystems = g.Nodes |> List.filter (fun n -> n.Kind = Graph.NodeKind.System && n.OwnSystem)
        let boundaries =
            ownSystems
            |> List.collect (fun s ->
                [ yield $"System_Boundary({s.Id}, \"{s.Name}\") {{"
                  yield! appsIn (Some s.Id) |> List.map (fun x -> "  " + x)
                  yield "}" ])
        let otherSystems =
            g.Nodes
            |> List.filter (fun n -> n.Kind = Graph.NodeKind.System && not n.OwnSystem)
            |> List.map (fun n ->
                let shape = if n.Relationship = Relationship.External then "System_Ext" else "System"
                $"{shape}({n.Id}, \"{n.Name}\", \"{n.Description}\")")
        let persons =
            g.Nodes
            |> List.filter (fun n -> n.Kind = Graph.NodeKind.Person)
            |> List.map (fun n ->
                let shape = if n.Relationship = Relationship.External then "Person_Ext" else "Person"
                $"{shape}({n.Id}, \"{n.Name}\", \"{n.Description}\")")

        // one app: its name; several: their systems, as in the container diagram
        let title =
            match g.Nodes |> List.filter (fun n -> n.Kind = Graph.NodeKind.App && n.HasMeta) with
            | [ app ] -> app.Name
            | _ -> ownSystems |> List.map (_.Name) |> String.concat ", "
        SB.create "C4Component"
        |> SB.emptyLine
        |> SB.line $"title C4 Component Diagram for {title}"
        |> SB.emptyLine
        |> SB.lines boundaries
        |> SB.lines (appsIn None)
        |> SB.lines otherSystems
        |> SB.lines persons
        |> SB.emptyLine
        |> SB.lines relationships
        |> SB.lines dependencies
        |> SB.toString

    let generateDiagram (cmd: DiagramCommand) (apps: App list) =
        match (cmd.Detail, cmd.DiagramFormat) with
        | Systems, Mermaid -> sprintMermaidC4L1 apps
        | Apps, Mermaid -> sprintMermaidC4L2 apps
        // the component diagram; at members it also draws the dependencies between classes
        | Components, Mermaid
        | Members, Mermaid -> sprintMermaidC4L3 cmd.Detail apps
        // the explorer always shows systems and apps; the detail only decides whether the classes are in it
        | detail, Html -> Explore.sprintHtml cmd.Viewer detail apps
