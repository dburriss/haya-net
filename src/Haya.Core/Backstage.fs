namespace Haya.Core

open System.Text.RegularExpressions

module Backstage =
    open Haya
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
        
        {|
            apiVersion = "backstage.io/v1alpha1"
            kind = "Component"
            metadata =
                {|
                    name = createName meta.System meta.AppName
                    description = meta.Description
                    title = meta.AppName
                |}
            spec =
                {|
                    owner = meta.Owner
                    domain = meta.System
                    lifecycle = "production"
                |}
            dependsOn = dependsOn
            dependencyOf = dependencyOf
        |}

    
    let genCollaboratorComponents (meta: Analysis.MetaDescriptor) (collaborators: Analysis.CollaboratorDescriptor list) =
        // An AppName in the decriptors matches a Backstage System
        // A system in the descriptors matches a Backstage Domain
        // Components seem to be the same
        let externalSystems =
            collaborators
            |> List.distinctBy (_.AppName)
            |> List.map (
                fun x ->
                    {|
                        apiVersion = "backstage.io/v1alpha1"
                        kind = "Component"
                        metadata =
                            {|
                                name = createName x.System x.AppName
                                description = x.Description
                                title = x.AppName
                            |}
                        spec =
                            {|
                                owner = x.Owner
                                domain = x.System
                                lifecycle = "production"
                            |}
                        // upstream
                        dependsOn = []
                        // downstream
                        dependencyOf = []
                    |})
        externalSystems
    
    let generateCatalog (cmd: BackstageCommand) (descriptors: Analysis.Descriptor list) =
        // todo: what if meta missing?
        let meta = descriptors |> Analysis.Descriptor.metas |> List.head
        let collaborators = descriptors |> Analysis.Descriptor.collaborators
        let thisComponent = genComponent meta collaborators |> box
        let otherComponents = genCollaboratorComponents meta collaborators |> List.map box
        let components = [ thisComponent ] @ otherComponents
        if components |> List.isEmpty then
            ""
        else
            components |> List.map Serializer.toYaml |> String.concat "---\n"
    

