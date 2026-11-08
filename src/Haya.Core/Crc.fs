namespace Haya.Core

open Haya.Core.Analysis

module Crc =

    let internal responsibilityRow (r: ResponsibilityDescriptor) =
        [r.Description; $"[{r.ComponentName}]({r.ComponentSource})"]

    let internal collaboratorRow (c: CollaboratorDescriptor) =
        [c.AppName; $"[{c.ComponentName}]({c.ComponentSource})"; c.Description]

    let internal buildResponsibilitiesForMd (level: int) (app: App) doc =
        doc
        |> Markdown.addHeading level "Responsibilities"
        |> Markdown.addTable ["Responsibility"; "Component"] (app.Responsibilities |> List.map responsibilityRow)

    let internal buildCollaboratorsForMd (level: int) (app: App) doc =
        doc
        |> Markdown.addHeading level "Collaborators"
        |> Markdown.addTable ["Collaborator"; "Component"; "Description" ] (app.Collaborators |> List.map collaboratorRow)

    let internal mermaidC4Level1 (apps: App list) doc =
        let diagram = Diagram.sprintMermaidC4L1 apps
        doc |> Markdown.addCode (Some "mermaid") diagram

    /// One app keeps the original layout; with several apps each gets its own section.
    let private appSections (apps: App list) doc =
        match apps with
        | [ app ] ->
            doc
            |> buildResponsibilitiesForMd 2 app
            |> Markdown.addBlankLine
            |> buildCollaboratorsForMd 2 app
            |> Markdown.addBlankLine
        | _ ->
            apps
            |> List.fold (fun d app ->
                let title = (App.meta app).AppName
                d
                |> Markdown.addH2 (if title = "" then "(unnamed app)" else title)
                |> buildResponsibilitiesForMd 3 app
                |> Markdown.addBlankLine
                |> buildCollaboratorsForMd 3 app
                |> Markdown.addBlankLine) doc

    let sprintMarkdown cmd (apps: App list) =
        let doc =
            Markdown.empty
            |> Markdown.addH1 "CRC"
            |> Markdown.addParagraph "This document describes the components, responsibilities, and collaborators for a codebase."
            |> appSections apps
            |> fun d -> if cmd.IncludeL1Diagram then mermaidC4Level1 apps d else d
        Markdown.render doc
