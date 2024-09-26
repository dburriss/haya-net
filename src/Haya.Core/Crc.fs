namespace Haya.Core

open System
open System.Text
open System.Text.Json
open FSharpPlus
open FuncyDown.Document
open FuncyDown.Element
open Haya
open Haya.Core.Analysis

module Crc =
    
    let mdRowData (desc: Analysis.Descriptor) =
        match desc with
        | Responsibility r -> [r.Description; $"[{r.ComponentName}]({r.ComponentSource})"]
        | Collaborator c -> [c.AppName; $"[{c.ComponentName}]({c.ComponentSource})"; c.Description] 
        | Meta m -> []

    let buildResponsibilitiesForMd (descriptors: Analysis.Descriptor list) doc =
        
        let rows =
            descriptors
            |> List.filter (fun x -> match x with | Analysis.Responsibility _ -> true | _ -> false)
            |> List.map mdRowData
        doc
        |> addH2 "Responsibilities"
        |> addTable ["Responsibility"; "Component"] rows
    
    let buildCollaboratorsForMd (descriptors: Analysis.Descriptor list) doc =
        let rows =
            descriptors
            |> List.filter (fun x -> match x with | Analysis.Collaborator _ -> true | _ -> false)
            |> List.map mdRowData
        doc
        |> addH2 "Collaborators"
        |> addTable ["Collaborator"; "Component"; "Description" ] rows
    
    
    let mermaidC4Level1 (descriptors: Analysis.Descriptor list) doc =
        let meta = descriptors
                   |> Descriptor.metas
                   |> List.tryHead
                   |> Option.defaultValue { AppName = ""; Description = ""; Team = ""; System = ""; Repository = "" }
                   
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
        doc
        |> addBlockCode { Code = diagram; Language = Some("mermaid") }
    
  
    let sprintMarkdown cmd (descriptors: Descriptor list) =
        let doc =
            emptyDocument
            |> addH1 "CRC"
            |> addParagraph "This document describes the components, responsibilities, and collaborators for a codebase."
            |> buildResponsibilitiesForMd descriptors
            |> addNewline
            |> buildCollaboratorsForMd descriptors
            |> addNewline
            |> fun d -> if cmd.IncludeL1Diagram then mermaidC4Level1 descriptors d else d
        render [(fun _ -> doc)]
