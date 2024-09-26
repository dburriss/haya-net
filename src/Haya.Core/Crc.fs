namespace Haya.Core

open System
open System.Text
open System.Text.Json
open FSharpPlus
open FuncyDown.Document
open FuncyDown.Element
open Haya
open Haya.Core.Analysis
type SB = StringBuilder
module SB =
    let empty = StringBuilder()
    let create (s: string) = StringBuilder(s)
    let append (s: string) (sb: StringBuilder) = sb.Append(s)
    let line (s: string) (sb: StringBuilder) = sb.AppendLine(s)
    let lines (ss: string list) (sb: StringBuilder) = ss |> List.fold (fun sb ss -> line ss sb) sb
    let emptyLine (sb: StringBuilder) = sb.AppendLine()
    let toString (sb: StringBuilder) = sb.ToString()

module Crc =
    
    let write file data =
        // TODO: check file extension
        IO.File.WriteAllText(file, data)
    let mdRowData (desc: Analysis.Descriptor) =
        match desc with
        | Analysis.Responsibility r -> [r.Description; $"[{r.ComponentName}]({r.ComponentSource})"]
        | Analysis.Collaborator c -> [c.AppName; $"[{c.ComponentName}]({c.ComponentSource})"; c.Description] 
        | Analysis.Meta m -> []

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
                   |> Describe.metas
                   |> List.tryHead
                   |> Option.defaultValue { AppName = ""; Description = ""; Team = ""; System = ""; Repository = "" }
                   
        let collaborators = descriptors |> Describe.collaborators
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
    
    let private serialize o =
        let opt = JsonSerializerOptions(WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
        opt.Converters.Add(Serialization.JsonStringEnumConverter())
        JsonSerializer.Serialize(o, opt)
        
    let sprintJson cmd (descriptors: Descriptor list) =
        let metaOpt = descriptors |> Describe.metas |> List.tryHead
        let responsibilities = descriptors |> Describe.responsibilities
        let collaborators = descriptors |> Describe.collaborators
        let json =
            {|
              version = "0.0"
              meta = metaOpt |> Option.map box |> Option.defaultValue null
              collaborators = collaborators
              responsibilities = responsibilities |}
        json |> serialize 
       
        
        
        
