namespace Haya.Core

open System.Text.RegularExpressions

module Backstage =
    let private toKebabCase (input: string) =
        let pattern = @"([a-z])([A-Z])"
        let replacement = "$1-$2"
        Regex.Replace(input, pattern, replacement).ToLower().Replace(" ", "-")

    let private createName (systemName: string) (appName: string) =
        let systemId = toKebabCase systemName
        let appId = toKebabCase appName
        $"{systemId}-{appId}" 
    
    let genComponent (meta: Analysis.MetaDescriptor) (collaborators: Analysis.CollaboratorDescriptor list) =
        let dependsOn =
            collaborators
            |> List.map (fun c -> createName c.System c.AppName)

        let dependencyOf =
            collaborators
            |> List.map (fun c -> createName meta.System c.AppName)

        Encode.backstageComponent
            (createName meta.System meta.AppName) meta.AppName meta.Description
            meta.Owner meta.System
            dependsOn dependencyOf

    let genCollaboratorComponents (meta: Analysis.MetaDescriptor) (collaborators: Analysis.CollaboratorDescriptor list) =
        // An AppName in the decriptors matches a Backstage System
        // A system in the descriptors matches a Backstage Domain
        // Components seem to be the same
        collaborators
        |> List.distinctBy (_.AppName)
        |> List.map (fun x ->
            // no upstream (dependsOn) or downstream (dependencyOf) known for external systems
            Encode.backstageComponent
                (createName x.System x.AppName) x.AppName x.Description
                x.Owner x.System
                [] [])

    let generateCatalog (cmd: BackstageCommand) (descriptors: Analysis.Descriptor list) =
        // todo: what if meta missing?
        let meta = descriptors |> Analysis.Descriptor.metas |> List.head
        let collaborators = descriptors |> Analysis.Descriptor.collaborators
        let thisComponent = genComponent meta collaborators
        let otherComponents = genCollaboratorComponents meta collaborators
        let components = [ thisComponent ] @ otherComponents
        if components |> List.isEmpty then
            ""
        else
            components |> List.map Serializer.toYaml |> String.concat "---\n"
    

