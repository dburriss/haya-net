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
    
    let private nameOfApp (meta: Analysis.MetaDescriptor) = createName meta.System meta.AppName

    /// `dependencyOf` also lists the apps in the same catalog that have this app as a collaborator.
    let genComponent (meta: Analysis.MetaDescriptor) (collaborators: Analysis.CollaboratorDescriptor list) (dependents: string list) =
        let dependsOn =
            collaborators
            |> List.map (fun c -> createName c.System c.AppName)
            |> List.distinct

        let dependencyOf =
            (collaborators |> List.map (fun c -> createName meta.System c.AppName)) @ dependents
            |> List.distinct

        Encode.backstageComponent
            (nameOfApp meta) meta.AppName meta.Description
            meta.Owner meta.System
            dependsOn dependencyOf

    /// Stubs for collaborators that are not one of the apps in the catalog.
    let genCollaboratorComponents (knownApps: string list) (collaborators: Analysis.CollaboratorDescriptor list) =
        // An AppName in the decriptors matches a Backstage System
        // A system in the descriptors matches a Backstage Domain
        // Components seem to be the same
        collaborators
        |> List.distinctBy (fun x -> createName x.System x.AppName)
        |> List.filter (fun x -> not (List.contains (createName x.System x.AppName) knownApps))
        |> List.map (fun x ->
            // no upstream (dependsOn) or downstream (dependencyOf) known for external systems
            Encode.backstageComponent
                (createName x.System x.AppName) x.AppName x.Description
                x.Owner x.System
                [] [])

    let generateCatalog (cmd: BackstageCommand) (apps: Analysis.App list) =
        // apps without a meta have no identity, so only their collaborators are catalogued
        let withMeta = apps |> List.choose (fun a -> a.Meta |> Option.map (fun m -> m, a))
        let knownApps = withMeta |> List.map (fun (m, _) -> nameOfApp m)
        let dependentsOf (target: Analysis.MetaDescriptor) =
            withMeta
            |> List.filter (fun (m, a) ->
                nameOfApp m <> nameOfApp target
                && a.Collaborators |> List.exists (fun c -> createName c.System c.AppName = nameOfApp target))
            |> List.map (fun (m, _) -> nameOfApp m)
        let appComponents = withMeta |> List.map (fun (m, a) -> genComponent m a.Collaborators (dependentsOf m))
        let otherComponents = apps |> List.collect (_.Collaborators) |> genCollaboratorComponents knownApps
        let components = appComponents @ otherComponents
        if components |> List.isEmpty then
            ""
        else
            components |> List.map Serializer.toYaml |> String.concat "---\n"
