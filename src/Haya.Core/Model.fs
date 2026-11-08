namespace Haya.Core.Analysis

/// Direction of a collaboration relative to the component. Mirrors `Haya.Direction`.
type Direction =
    | Upstream = 1
    | Downstream = 2

/// The type of relationship between the team and the application or system. Mirrors `Haya.Relationship`.
type Relationship = Owned = 1 | Shared = 2 | Internal = 3 | External = 4 | InternalUser = 5 | ExternalUser = 6

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
}

module App =
    let private emptyMeta = { AppName = ""; Description = ""; Owner = ""; System = ""; Repository = "" }

    let ofDescriptors (descriptors: Descriptor list) =
        { Meta = descriptors |> Descriptor.metas |> List.tryHead
          Responsibilities = descriptors |> Descriptor.responsibilities
          Collaborators = descriptors |> Descriptor.collaborators }

    /// The app's meta, or an empty one when it has none.
    let meta (app: App) = app.Meta |> Option.defaultValue emptyMeta
