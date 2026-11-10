namespace Haya.Core

open Haya.Core.Analysis

/// Datadog Software Catalog definitions (`entity.datadog.yaml`, schema v3). See `research/datadog-command-design.md`.
module Datadog =

    /// The Datadog entity kind of an app and its `spec.type`. An API is a `service` of type `rest`, because the `api`
    /// kind cannot depend on anything.
    let private kindOf (kind: AppKind) =
        match kind with
        | AppKind.Database -> "datastore", ""
        | AppKind.Queue -> "queue", ""
        | AppKind.WebApp -> "service", "web"
        | AppKind.Api -> "service", "rest"
        | _ -> "service", ""

    /// The language as Datadog names it in `spec.languages`.
    let private languagesOf (language: Language) =
        match language with
        | Language.DotNet -> [ "dotnet" ]
        | Language.JavaScript -> [ "js" ]
        | _ -> []

    /// `codeLocations` wants a URL, and a repository is often only a slug like `org/repo`.
    let private repositoryUrl (repository: string) =
        if repository.StartsWith "https://" || repository.StartsWith "http://" then repository else ""

    /// An entity with nothing but empty values, to start each one from.
    let private blank : Encode.DatadogEntity =
        { Kind = ""; Name = ""; DisplayName = ""; Description = ""; Owner = ""; Lifecycle = ""; Tier = ""; Type = ""
          Languages = []; DependsOn = []; Components = []; RepositoryUrl = "" }

    let private reference (kind: string) (name: string) = $"{kind}:{name}"

    /// The entities of a catalog: one `system` per system, one entity per app that has a meta, and a `service` stub for each
    /// collaborator that is not one of those apps. A `service` has no `dependencyOf`, so a relation is always written on
    /// the entity that calls: an upstream collaborator calls the app, which makes it depend on the app.
    let genEntities (apps: App list) =
        let apps = Catalog.withDefaultSystems apps
        let withMeta = Catalog.withMeta apps
        let knownApps = withMeta |> List.map (fst >> Catalog.nameOfApp)
        let kindsOfApps = withMeta |> List.map (fun (m, _) -> Catalog.nameOfApp m, fst (kindOf m.Kind)) |> Map.ofList
        // anything that is not an app of the catalog is a stub, which is a service
        let refTo (name: string) =
            reference (kindsOfApps |> Map.tryFind name |> Option.defaultValue "service") name
        let callsOf (c: CollaboratorDescriptor) = Catalog.nameOfCollaborator c

        let appEntities =
            withMeta
            |> List.map (fun (m, a) ->
                let kind, typ = kindOf m.Kind
                let calls =
                    (a.Collaborators |> List.filter (Catalog.isUpstream >> not) |> List.map callsOf)
                    @ Catalog.dependenciesOf withMeta m
                    |> List.distinct
                { blank with
                    Kind = kind
                    Name = Catalog.nameOfApp m
                    DisplayName = m.AppName
                    Description = m.Description
                    Owner = m.Owner
                    Lifecycle = m.Lifecycle
                    Tier = m.Tier
                    Type = typ
                    Languages = if kind = "service" then languagesOf a.Language else []
                    DependsOn = calls |> List.map refTo
                    RepositoryUrl = repositoryUrl m.Repository })

        let stubs =
            apps
            |> List.collect (_.Collaborators)
            |> Catalog.externalCollaborators knownApps
            |> List.map (fun c ->
                let name = callsOf c
                // the apps that list this collaborator as upstream are called by it
                let calls =
                    withMeta
                    |> List.filter (fun (_, a) -> a.Collaborators |> List.exists (fun x -> Catalog.isUpstream x && callsOf x = name))
                    |> List.map (fst >> Catalog.nameOfApp)
                { blank with
                    Kind = "service"
                    Name = name
                    DisplayName = c.AppName
                    Description = c.Description
                    Owner = c.Owner
                    DependsOn = calls |> List.map refTo },
                c.System)

        let members : (string * string) list =
            (withMeta |> List.map (fun (m, _) -> m.System, reference (fst (kindOf m.Kind)) (Catalog.nameOfApp m)))
            @ (stubs |> List.map (fun (e: Encode.DatadogEntity, system) -> system, reference e.Kind e.Name))
            |> List.filter (fun (system, _) -> system <> "")
        let systems =
            members
            |> List.map fst
            |> List.distinct
            |> List.map (fun system ->
                { blank with
                    Kind = "system"
                    Name = Naming.createName "" system
                    DisplayName = system
                    Components = members |> List.filter (fst >> (=) system) |> List.map snd })

        systems @ appEntities @ (stubs |> List.map fst)

    let generateCatalog (cmd: DatadogCommand) (apps: App list) =
        let entities = genEntities apps |> List.map Encode.datadogEntity
        if entities |> List.isEmpty then
            ""
        else
            match cmd.Format with
            | DataFormat.Yaml -> entities |> List.map Serializer.toYaml |> String.concat "---\n"
            | DataFormat.Json -> entities |> List.toArray |> Flexi.Json.JsonValue.Array |> Serializer.toJson
