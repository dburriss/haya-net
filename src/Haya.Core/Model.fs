namespace Haya.Core.Analysis

/// Direction of a collaboration relative to the component. Mirrors `Haya.Direction`.
type Direction =
    | Upstream = 1
    | Downstream = 2

/// The type of relationship between the team and the application or system. Mirrors `Haya.Relationship`.
type Relationship = Owned = 1 | Shared = 2 | Internal = 3 | External = 4 | InternalUser = 5 | ExternalUser = 6

/// What kind of application this is. Used to pick the shape of the app in diagrams. Mirrors `Haya.AppKind`.
type AppKind = Unspecified = 0 | Service = 1 | WebApp = 2 | Api = 3 | Worker = 4 | Database = 5 | Queue = 6

/// The language/runtime of the analysed codebase. Set by the analyser that produced the app. Mirrors nothing in `Haya`:
/// users never declare it.
type Language = Unknown = 0 | DotNet = 1 | JavaScript = 2

type ResponsibilityDescriptor = {
    Description: string
    ComponentName: string
    ComponentSource: string
}

type CollaboratorDescriptor = {
    Direction: Direction
    Tech: string
    Protocol: string
    DataDescription: string
    Description: string
    AppName: string
    System: string
    Relationship: Relationship
    ComponentName: string
    ComponentSource: string
    Repository: string
    Owner: string
}

type MetaDescriptor = {
    AppName: string
    Description: string
    System: string
    Repository: string
    Owner: string
    /// The technology the application is built with, e.g. ".NET 10 / ASP.NET Core".
    Tech: string
    Kind: AppKind
    /// Lifecycle stage of the application, e.g. "production" or "experimental". Empty when not given.
    Lifecycle: string
    /// Criticality tier of the application, e.g. "1". Empty when not given.
    Tier: string
}

type Descriptor =
    | Responsibility of ResponsibilityDescriptor
    | Collaborator of CollaboratorDescriptor
    | Meta of MetaDescriptor
    

module Descriptor =
    let collaborators (descriptors: Descriptor list) =
        descriptors |> List.choose (function | Collaborator x -> Some x | _ -> None)
    
    let responsibilities (descriptors: Descriptor list) =
        descriptors |> List.choose (function | Responsibility x -> Some x | _ -> None) 
    
    let metas (descriptors: Descriptor list) =
        descriptors |> List.choose (function | Meta x -> Some x | _ -> None)


/// One application: what a single describe file (or one analysed solution) holds.
type App = {
    Meta: MetaDescriptor option
    Responsibilities: ResponsibilityDescriptor list
    Collaborators: CollaboratorDescriptor list
    /// What the app is written in, as known by the analyser. `Unknown` when the source did not say.
    Language: Language
}

module App =
    let private emptyMeta = { AppName = ""; Description = ""; Owner = ""; System = ""; Repository = ""; Tech = ""; Kind = AppKind.Unspecified; Lifecycle = ""; Tier = "" }

    let ofDescriptors (descriptors: Descriptor list) =
        { Meta = descriptors |> Descriptor.metas |> List.tryHead
          Responsibilities = descriptors |> Descriptor.responsibilities
          Collaborators = descriptors |> Descriptor.collaborators
          Language = Language.Unknown }

    /// The same app, marked as written in `language`.
    let withLanguage (language: Language) (app: App) = { app with Language = language }

    /// The app's meta, or an empty one when it has none.
    let meta (app: App) = app.Meta |> Option.defaultValue emptyMeta

    /// A collaborator without a system belongs to the owning app's system, or to "External" when it is external.
    /// Renderers apply this so that they agree on which system a collaborator is in.
    let withDefaultSystems (app: App) =
        let owner = app.Meta |> Option.map (_.System) |> Option.defaultValue ""
        let fill (c: CollaboratorDescriptor) =
            if c.System <> "" then c
            elif c.Relationship = Relationship.External then { c with System = "External" }
            else { c with System = owner }
        { app with Collaborators = app.Collaborators |> List.map fill }
