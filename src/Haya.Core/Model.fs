namespace Haya.Core.Analysis

/// Direction of a collaboration relative to the component. Mirrors `Haya.Direction`.
type Direction =
    | Upstream = 1
    | Downstream = 2

/// The type of relationship between the team and the application or system. Mirrors `Haya.Relationship`.
type Relationship = Owned = 1 | Shared = 2 | Internal = 3 | External = 4

/// What kind of application this is. Used to pick the shape of the app in diagrams. Mirrors `Haya.AppKind`.
type AppKind = Unspecified = 0 | Service = 1 | WebApp = 2 | Api = 3 | Worker = 4 | Database = 5 | Queue = 6 | Person = 7

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
    /// What the collaborator is. `Person` for a user: it has no app, so it is named by `AppName` and has no system.
    Kind: AppKind
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


/// A parameter of a constructor or method: its name and the display string of its type, as the language writes it.
type Parameter = { Name: string; Type: string }

/// What a public member of a class is.
type MemberKind = Method = 1 | Property = 2 | Field = 3 | Event = 4

/// A public member declared on a class (not inherited).
type Member = {
    Name: string
    Kind: MemberKind
    /// The display string of the return, property, field or event type, in the language's own short form
    /// (`Task<bool>` in C#, `Promise<boolean>` in TypeScript).
    Type: string
    /// Methods and indexers only: the parameters in order.
    Parameters: Parameter list
}

/// Another annotated class of the same app that a class reaches in code (ADR018): directly, or through unannotated types.
type ComponentDependency = {
    /// The class reached.
    Name: string
    Source: string
    /// The names of the unannotated types between, in order along the shortest chain. Empty when direct.
    Via: string list
}

/// The constructors and public members of a class that carries a Haya attribute. Keyed like the descriptors by
/// `ComponentName` and `ComponentSource`. Only collected at `Detail.Members` (ADR017, ADR018).
type ComponentDetail = {
    Name: string
    Source: string
    /// Each public constructor's parameters, in order.
    Constructors: Parameter list list
    Members: Member list
    /// The annotated classes of the same app this class reaches (ADR018), by name then source.
    Dependencies: ComponentDependency list
}

module ComponentDetail =
    let private signature (ps: Parameter list) = ps |> List.map (fun p -> p.Name + ": " + p.Type) |> String.concat ", "

    /// The one ordering rule for both analysers: constructors by parameter count then signature; members by kind,
    /// then name, then signature. Ordinal string comparison.
    let normalise (c: ComponentDetail) =
        { c with
            Constructors = c.Constructors |> List.sortBy (fun ps -> List.length ps, signature ps)
            Members = c.Members |> List.sortBy (fun m -> int m.Kind, m.Name, signature m.Parameters + " -> " + m.Type)
            Dependencies = c.Dependencies |> List.sortBy (fun d -> d.Name, d.Source) }

/// One application: what a single describe file (or one analysed solution) holds.
type App = {
    Meta: MetaDescriptor option
    Responsibilities: ResponsibilityDescriptor list
    Collaborators: CollaboratorDescriptor list
    /// What the app is written in, as known by the analyser. `Unknown` when the source did not say.
    Language: Language
    /// The constructors and members of the annotated classes. Empty unless analysed (or described) at `Detail.Members`.
    Components: ComponentDetail list
}

module App =
    let private emptyMeta = { AppName = ""; Description = ""; Owner = ""; System = ""; Repository = ""; Tech = ""; Kind = AppKind.Unspecified; Lifecycle = ""; Tier = "" }

    let ofDescriptors (descriptors: Descriptor list) =
        { Meta = descriptors |> Descriptor.metas |> List.tryHead
          Responsibilities = descriptors |> Descriptor.responsibilities
          Collaborators = descriptors |> Descriptor.collaborators
          Language = Language.Unknown
          Components = [] }

    /// The same app with the details of its classes, in the shared order (`ComponentDetail.normalise`).
    let withComponents (components: ComponentDetail list) (app: App) =
        { app with Components = components |> List.map ComponentDetail.normalise }

    /// The same app, marked as written in `language`.
    let withLanguage (language: Language) (app: App) = { app with Language = language }

    /// The app's meta, or an empty one when it has none.
    let meta (app: App) = app.Meta |> Option.defaultValue emptyMeta

    /// A collaborator without a system belongs to the owning app's system, or to "External" when it is external.
    /// A person never belongs to a system. Renderers apply this so that they agree on which system a collaborator is in.
    let withDefaultSystems (app: App) =
        let owner = app.Meta |> Option.map (_.System) |> Option.defaultValue ""
        let fill (c: CollaboratorDescriptor) =
            if c.System <> "" || c.Kind = AppKind.Person then c
            elif c.Relationship = Relationship.External then { c with System = "External" }
            else { c with System = owner }
        { app with Collaborators = app.Collaborators |> List.map fill }
