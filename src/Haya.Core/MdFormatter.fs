namespace Haya.Core

open System
open System.Text
open FSharpPlus
open FuncyDown.Document
open FuncyDown.Element
open Haya
type SB = StringBuilder
module SB =
    let empty = StringBuilder()
    let create (s: string) = StringBuilder(s)
    let append (s: string) (sb: StringBuilder) = sb.Append(s)
    let line (s: string) (sb: StringBuilder) = sb.AppendLine(s)
    let lines (ss: string list) (sb: StringBuilder) = ss |> List.fold (fun sb ss -> line ss sb) sb
    let emptyLine (sb: StringBuilder) = sb.AppendLine()
    let toString (sb: StringBuilder) = sb.ToString()

module MdFormatter =
    
    let write file data =
        // TODO: check file extension
        IO.File.WriteAllText(file, data)
    let mdRowData (desc: Analysis.Descriptor) =
        match desc with
        | Analysis.Responsibility r -> [r.Description; $"[{r.ComponentName}]({r.ComponentSource})"]
        | Analysis.Collaborator c -> [c.AppName; $"[{c.ComponentName}]({c.ComponentSource})"; c.Description] 
        | Analysis.Meta m -> []

    let buildResponsibilities (descriptors: Analysis.Descriptor list) doc =
        
        let rows =
            descriptors
            |> List.filter (fun x -> match x with | Analysis.Responsibility _ -> true | _ -> false)
            |> List.map mdRowData
        doc
        |> addH2 "Responsibilities"
        |> addTable ["Responsibility"; "Component"] rows
    
    let buildCollaborators (descriptors: Analysis.Descriptor list) doc =
        let rows =
            descriptors
            |> List.filter (fun x -> match x with | Analysis.Collaborator _ -> true | _ -> false)
            |> List.map mdRowData
        doc
        |> addH2 "Collaborators"
        |> addTable ["Collaborator"; "Component"; "Description" ] rows
    
    
    let mermaidC4Level1 (descriptors: Analysis.Descriptor list) doc =
        let meta = descriptors
                   |> List.filter (fun x -> match x with | Analysis.Meta _ -> true | _ -> false)
                   |> List.tryHead
                   |> Option.defaultValue (Analysis.Meta {AppName = ""; Description = ""; Team = ""; System = ""; Repository = ""})
                   |> (fun x -> match x with | Analysis.Meta m -> m | _ -> failwith "impossible")
                   
        let collaborators = descriptors
                              |> List.choose (function | Analysis.Collaborator c -> Some (c) | _ -> None)
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
            // external systems
            |> SB.lines externalSystems
            // enterprise boundary
            |> SB.emptyLine
            |> SB.line "Enterprise_Boundary(Enterprise, Enterprise, \"The enterprise boundary\") {"
            |> SB.lines  internalSystems
            |> SB.line thisSystem
            |> SB.line "}"
            // relationships
            |> SB.emptyLine
            |> SB.lines relationships
            // string
            |> SB.toString         
        doc
        |> addBlockCode { Code = diagram; Language = Some("mermaid") }
    
  
    let sprintCrc cmd (descriptors: Analysis.Descriptor list) =
        let doc =
            emptyDocument
            |> addH1 "CRC"
            |> addParagraph "This document describes the components, responsibilities, and collaborators for a codebase."
            |> buildResponsibilities descriptors
            |> addNewline
            |> buildCollaborators descriptors
            |> addNewline
            |> fun d -> if cmd.IncludeL1Diagram then mermaidC4Level1 descriptors d else d
        render [(fun _ -> doc)]
       
       
        
        
        
