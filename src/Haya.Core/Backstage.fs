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
        if systemId = "" then appId else $"{systemId}-{appId}"

    /// A collaborator without a system belongs to the owning app's system, or to "External" when it is external.
    let private withSystem (owner: string) (c: Analysis.CollaboratorDescriptor) =
        if c.System <> "" then c
        else
            match c.Relationship with
            | Analysis.Relationship.External -> { c with System = "External" }
            | _ -> { c with System = owner }
    
    let private nameOfApp (meta: Analysis.MetaDescriptor) = createName meta.System meta.AppName

    /// An upstream collaborator calls this app, so it is a dependency of this app's `dependencyOf`; a downstream
    /// collaborator is called by this app, so it goes in `dependsOn`. `dependents` are the apps in the same catalog
    /// that call this app.
    let genComponent (meta: Analysis.MetaDescriptor) (collaborators: Analysis.CollaboratorDescriptor list) (dependents: string list) =
        let named (cs: Analysis.CollaboratorDescriptor list) =
            cs |> List.map (fun c -> createName c.System c.AppName)
        let isUpstream (c: Analysis.CollaboratorDescriptor) = c.Direction = Analysis.Direction.Upstream

        let dependsOn =
            collaborators |> List.filter (isUpstream >> not) |> named |> List.distinct

        let dependencyOf =
            (collaborators |> List.filter isUpstream |> named) @ dependents |> List.distinct

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
        let apps =
            apps |> List.map (fun a ->
                let owner = a.Meta |> Option.map (_.System) |> Option.defaultValue ""
                { a with Collaborators = a.Collaborators |> List.map (withSystem owner) })
        let withMeta = apps |> List.choose (fun a -> a.Meta |> Option.map (fun m -> m, a))
        let knownApps = withMeta |> List.map (fun (m, _) -> nameOfApp m)
        let dependentsOf (target: Analysis.MetaDescriptor) =
            withMeta
            |> List.filter (fun (m, a) ->
                nameOfApp m <> nameOfApp target
                && a.Collaborators |> List.exists (fun c ->
                    c.Direction = Analysis.Direction.Downstream && createName c.System c.AppName = nameOfApp target))
            |> List.map (fun (m, _) -> nameOfApp m)
        let appComponents = withMeta |> List.map (fun (m, a) -> genComponent m a.Collaborators (dependentsOf m))
        let otherComponents = apps |> List.collect (_.Collaborators) |> genCollaboratorComponents knownApps
        let components = appComponents @ otherComponents
        if components |> List.isEmpty then
            ""
        else
            match cmd.Format with
            | DataFormat.Yaml -> components |> List.map Serializer.toYaml |> String.concat "---\n"
            | DataFormat.Json -> components |> List.toArray |> Flexi.Json.JsonValue.Array |> Serializer.toJson
