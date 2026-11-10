namespace Haya.Core

open Haya.Core.Naming

module Backstage =

    /// An upstream collaborator calls this app, so it is a dependency of this app's `dependencyOf`; a downstream
    /// collaborator is called by this app, so it goes in `dependsOn`. `dependents` are the apps in the same catalog
    /// that call this app; `dependencies` are the apps in the same catalog that this app calls without declaring it
    /// (they list this app as upstream).
    let genComponent (meta: Analysis.MetaDescriptor) (collaborators: Analysis.CollaboratorDescriptor list) (dependents: string list) (dependencies: string list) =
        let named (cs: Analysis.CollaboratorDescriptor list) = cs |> List.map Catalog.nameOfCollaborator

        let dependsOn =
            (collaborators |> List.filter (Catalog.isUpstream >> not) |> named) @ dependencies |> List.distinct

        let dependencyOf =
            (collaborators |> List.filter Catalog.isUpstream |> named) @ dependents |> List.distinct

        Encode.backstageComponent
            (Catalog.nameOfApp meta) meta.AppName meta.Description
            meta.Owner meta.System
            dependsOn dependencyOf

    /// Stubs for collaborators that are not one of the apps in the catalog.
    let genCollaboratorComponents (knownApps: string list) (collaborators: Analysis.CollaboratorDescriptor list) =
        // An AppName in the decriptors matches a Backstage System
        // A system in the descriptors matches a Backstage Domain
        // Components seem to be the same
        Catalog.externalCollaborators knownApps collaborators
        |> List.map (fun x ->
            // no upstream (dependsOn) or downstream (dependencyOf) known for external systems
            Encode.backstageComponent
                (Catalog.nameOfCollaborator x) x.AppName x.Description
                x.Owner x.System
                [] [])

    let generateCatalog (cmd: BackstageCommand) (apps: Analysis.App list) =
        // apps without a meta have no identity, so only their collaborators are catalogued
        let apps = Catalog.withDefaultSystems apps
        let withMeta = Catalog.withMeta apps
        let knownApps = withMeta |> List.map (fst >> Catalog.nameOfApp)
        let appComponents =
            withMeta
            |> List.map (fun (m, a) ->
                genComponent m a.Collaborators (Catalog.dependentsOf withMeta m) (Catalog.dependenciesOf withMeta m))
        let otherComponents = apps |> List.collect (_.Collaborators) |> genCollaboratorComponents knownApps
        let components = appComponents @ otherComponents
        if components |> List.isEmpty then
            ""
        else
            match cmd.Format with
            | DataFormat.Yaml -> components |> List.map Serializer.toYaml |> String.concat "---\n"
            | DataFormat.Json -> components |> List.toArray |> Flexi.Json.JsonValue.Array |> Serializer.toJson
