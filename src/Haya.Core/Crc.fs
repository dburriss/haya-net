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
        let diagram = Diagram.sprintMermaidC4L1 descriptors
        doc |> addBlockCode { Code = diagram; Language = Some("mermaid") }
        
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
