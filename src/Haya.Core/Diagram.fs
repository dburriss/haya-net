namespace Haya.Core

open Haya
open Haya.Core.Analysis
open FSharpPlus

module Diagram =
    
    let sprintMermaidC4L1 (descriptors: Analysis.Descriptor list) =
        let meta = descriptors
                   |> Descriptor.metas
                   |> List.tryHead
                   |> Option.defaultValue { AppName = ""; Description = ""; Owner =  ""; System = ""; Repository = "" }
                   
        let collaborators = descriptors |> Descriptor.collaborators
        let externalSystems = collaborators
                              |> List.filter (fun x -> x.Relationship = Relationship.External && x.System <> meta.System && x.System <> "")
                              |> List.distinctBy (_.System)
                              |> List.map (fun x -> $"System_Ext({String.toLower x.System}, {x.System}, \"{x.Description}\")")
        let internalSystems = collaborators
                              |> List.filter (fun x -> x.Relationship = Relationship.Internal && x.System <> meta.System && x.System <> "")
                              |> List.distinctBy (_.System)
                              |> List.map (fun x -> $"  System({String.toLower x.System}, {x.System}, \"{x.Description}\")")
        let thisSystem = $"  System({String.toLower meta.System}, {meta.System}, \"{meta.Description}\")"
        let relationships = collaborators
                            |> List.filter (fun x -> x.System <> meta.System && x.System <> "")
                            |> List.map (
                                fun x ->
                                    if x.Direction = Direction.Upstream then
                                        $"Rel({String.toLower x.System}, {String.toLower meta.System}, \"{x.DataDescription}\")"
                                    else $"Rel({String.toLower meta.System}, {String.toLower x.System}, \"{x.DataDescription}\")")
                            
        let diagram =
            SB.create "C4Context"
            |> SB.emptyLine
            |> SB.line $"title C4 System Diagram for {meta.System}"
            
            // enterprise boundary
            |> SB.emptyLine
            |> SB.line "Enterprise_Boundary(Enterprise, Enterprise, \"The enterprise boundary\") {"
            |> SB.lines  internalSystems
            |> SB.line thisSystem
            |> SB.line "}"
            // external systems
            |> SB.lines externalSystems
            // relationships
            |> SB.emptyLine
            |> SB.lines relationships
            // string
            |> SB.toString
            
        diagram
        
    let generateDiagram (cmd: DiagramCommand) (descriptors: Descriptor list) =
        match (cmd.DiagramType, cmd.DiagramFormat) with
        | C4System, Mermaid -> sprintMermaidC4L1 descriptors
        
