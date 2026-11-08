namespace Haya.Core

open Haya.Core.Analysis


module Diagram =

    /// C4 system context for all the apps. Apps are grouped by system; collaborators in another system
    /// become relationships (to a known system, or to an external/internal system declared from the collaborator).
    let sprintMermaidC4L1 (apps: App list) =
        let metas = apps |> List.map App.meta
        let ownSystems = metas |> List.map (_.System) |> List.distinct
        let isOther (x: CollaboratorDescriptor) = x.System <> "" && not (List.contains x.System ownSystems)
        let collaborators = apps |> List.collect (_.Collaborators)

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
                                app.Collaborators
                                |> List.filter (fun x -> x.System <> meta.System && x.System <> "")
                                |> List.map (fun x ->
                                    if x.Direction = Direction.Upstream then
                                        $"Rel({String.toLower x.System}, {String.toLower meta.System}, \"{x.DataDescription}\")"
                                    else $"Rel({String.toLower meta.System}, {String.toLower x.System}, \"{x.DataDescription}\")"))
                            |> List.distinct

        let title = String.concat ", " ownSystems
        let diagram =
            SB.create "C4Context"
            |> SB.emptyLine
            |> SB.line $"title C4 System Diagram for {title}"

            // enterprise boundary
            |> SB.emptyLine
            |> SB.line "Enterprise_Boundary(Enterprise, Enterprise, \"The enterprise boundary\") {"
            |> SB.lines  internalSystems
            |> SB.lines thisSystems
            |> SB.line "}"
            // external systems
            |> SB.lines externalSystems
            // relationships
            |> SB.emptyLine
            |> SB.lines relationships
            // string
            |> SB.toString

        diagram

    let generateDiagram (cmd: DiagramCommand) (apps: App list) =
        match (cmd.DiagramType, cmd.DiagramFormat) with
        | C4System, Mermaid -> sprintMermaidC4L1 apps
