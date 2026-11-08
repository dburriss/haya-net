namespace Haya.Core

open Haya.Core.Analysis

module Crc =
    
    let internal mdRowData (desc: Analysis.Descriptor) =
        match desc with
        | Responsibility r -> [r.Description; $"[{r.ComponentName}]({r.ComponentSource})"]
        | Collaborator c -> [c.AppName; $"[{c.ComponentName}]({c.ComponentSource})"; c.Description] 
        | Meta m -> []

    let internal buildResponsibilitiesForMd (descriptors: Analysis.Descriptor list) doc =
        
        let rows =
            descriptors
            |> List.filter (fun x -> match x with | Analysis.Responsibility _ -> true | _ -> false)
            |> List.map mdRowData
        doc
        |> Markdown.addH2 "Responsibilities"
        |> Markdown.addTable ["Responsibility"; "Component"] rows
    
    let internal buildCollaboratorsForMd (descriptors: Analysis.Descriptor list) doc =
        let rows =
            descriptors
            |> List.filter (fun x -> match x with | Analysis.Collaborator _ -> true | _ -> false)
            |> List.map mdRowData
        doc
        |> Markdown.addH2 "Collaborators"
        |> Markdown.addTable ["Collaborator"; "Component"; "Description" ] rows
    
    let internal mermaidC4Level1 (descriptors: Analysis.Descriptor list) doc =
        let diagram = Diagram.sprintMermaidC4L1 descriptors
        doc |> Markdown.addCode (Some "mermaid") diagram
        
    let sprintMarkdown cmd (descriptors: Descriptor list) =
        let doc =
            Markdown.empty
            |> Markdown.addH1 "CRC"
            |> Markdown.addParagraph "This document describes the components, responsibilities, and collaborators for a codebase."
            |> buildResponsibilitiesForMd descriptors
            |> Markdown.addBlankLine
            |> buildCollaboratorsForMd descriptors
            |> Markdown.addBlankLine
            |> fun d -> if cmd.IncludeL1Diagram then mermaidC4Level1 descriptors d else d
        Markdown.render doc
