namespace Haya

open System

/// The direction of the data flow or dependency
type Direction =
    /// The data flows from the collaborator to this system/application/component
    Upstream = 1
    /// The data flows from the component to the collaborator
    | Downstream = 2

/// The type of relationship between the team and the application or system
type Relationship = Owned = 1 | Shared = 2 | Internal = 3 | External = 4 | InternalUser = 5 | ExternalUser = 6

/// The responsibility, usecase, or feature of the component
[<AttributeUsage(AttributeTargets.Class ||| AttributeTargets.Struct, AllowMultiple = true)>]
type ResponsibilityAttribute() =
    inherit Attribute()
    /// A description of the responsibility, usecase, or feature
    member val Description = "" with get,set

/// The collaborator, system, or application that the component interacts with.
[<AttributeUsage(AttributeTargets.Class ||| AttributeTargets.Struct, AllowMultiple = true)>]
type CollaboratorAttribute() =
    inherit Attribute()
    /// The direction of the data flow or dependency
    member val Direction = Direction.Downstream with get,set
    /// Then technology used by the collaborator
    member val Technology = "" with get,set
    /// The technology or protocol used to communicate with the collaborator
    member val Protocol = "HTTPS" with get,set
    /// A description of the data or dependency flowing between the component and the collaborator
    member val DataDescription = "" with get,set
    /// A description of the collaborator, system, or application
    member val Description = "" with get,set
    /// The name of the application being interacted with
    member val AppName = "" with get,set
    /// The name of the system being interacted with
    member val System = "" with get,set
    /// The type of relationship between the team and the application or system
    member val Relationship = Relationship.Internal with get,set
    /// The slug of the VCS repository for the collaborator
    member val Repository = "" with get,set
    /// The owner of the system or application
    member val Owner = "" with get,set

/// Information about this application or system
[<AttributeUsage(AttributeTargets.Assembly)>]
type MetaAttribute() =
    inherit Attribute()
    /// The name of this application
    member val AppName = "" with get,set
    /// The team responsible for this application
    member val Owner = "" with get,set
    /// A description of this application
    member val Description = "" with get,set
    /// The team responsible for this application
    member val System = "" with get,set
    /// The slug of the VCS repository for this application
    member val Repository = "" with get,set
