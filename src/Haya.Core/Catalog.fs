namespace Haya.Core

open Haya.Core.Naming

/// What the catalog formats (`Backstage`, `Datadog`) share: how apps are named and how they relate to each other.
/// A collaborator that goes downstream is called by its app (the app depends on it); an upstream collaborator calls
/// the app. Relations between apps of the same catalog are found from both directions.
module Catalog =

    /// The catalog name of an app: its system and name in kebab case.
    let nameOfApp (meta: Analysis.MetaDescriptor) = createName meta.System meta.AppName

    /// The catalog name of a collaborator: its system and name in kebab case.
    let nameOfCollaborator (c: Analysis.CollaboratorDescriptor) = createName c.System c.AppName

    let isUpstream (c: Analysis.CollaboratorDescriptor) = c.Direction = Analysis.Direction.Upstream

    /// Apps with their collaborators' systems defaulted, so that every format agrees on which system a collaborator is in.
    let withDefaultSystems (apps: Analysis.App list) = apps |> List.map Analysis.App.withDefaultSystems

    /// The apps that have a meta, i.e. an identity. Apps without one only contribute their collaborators.
    let withMeta (apps: Analysis.App list) =
        apps |> List.choose (fun a -> a.Meta |> Option.map (fun m -> m, a))

    /// The names of the apps in `apps` (as returned by `withMeta`) that call `target`: they list it as a downstream collaborator.
    let dependentsOf (apps: (Analysis.MetaDescriptor * Analysis.App) list) (target: Analysis.MetaDescriptor) =
        apps
        |> List.filter (fun (m, a) ->
            nameOfApp m <> nameOfApp target
            && a.Collaborators |> List.exists (fun c ->
                c.Direction = Analysis.Direction.Downstream && nameOfCollaborator c = nameOfApp target))
        |> List.map (fun (m, _) -> nameOfApp m)

    /// The names of the apps in `apps` that `target` calls without declaring it: they list `target` as an upstream collaborator.
    let dependenciesOf (apps: (Analysis.MetaDescriptor * Analysis.App) list) (target: Analysis.MetaDescriptor) =
        apps
        |> List.filter (fun (m, a) ->
            nameOfApp m <> nameOfApp target
            && a.Collaborators |> List.exists (fun c -> isUpstream c && nameOfCollaborator c = nameOfApp target))
        |> List.map (fun (m, _) -> nameOfApp m)

    /// One collaborator per catalog name, left out when it is one of the apps in the catalog (`knownApps`).
    let externalCollaborators (knownApps: string list) (collaborators: Analysis.CollaboratorDescriptor list) =
        collaborators
        |> List.distinctBy nameOfCollaborator
        |> List.filter (fun c -> not (List.contains (nameOfCollaborator c) knownApps))
