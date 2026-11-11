namespace Haya

open System

/// The direction of the data flow or dependency
type Direction =
    /// The data flows from the collaborator to this system/application/component
    Upstream = 1
    /// The data flows from the component to the collaborator
    | Downstream = 2

/// The type of relationship between the team and the application or system
type Relationship = Owned = 1 | Shared = 2 | Internal = 3 | External = 4

/// What kind of application this is. Used to pick the shape of the application in diagrams.
/// On a collaborator it says what the collaborator is: `Person` for a user rather than an application.
type AppKind =
    /// Not specified: shown as a generic application
    Unspecified = 0
    /// A backend service
    | Service = 1
    /// A web application or single page app that users interact with
    | WebApp = 2
    /// An API that other applications call
    | Api = 3
    /// A background worker or job that does not serve requests
    | Worker = 4
    /// A data store
    | Database = 5
    /// A message queue or broker
    | Queue = 6
    /// A person who uses, or is used by, the application. Only meaningful on a collaborator: a person has no application,
    /// so it is named with `AppName` and does not belong to a system.
    | Person = 7

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
    /// What the collaborator is. Use `AppKind.Person` for a user: name it with `AppName`; `System` is ignored.
    member val Kind = AppKind.Unspecified with get,set

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
    /// The system this application belongs to
    member val System = "" with get,set
    /// The slug of the VCS repository for this application
    member val Repository = "" with get,set
    /// The technology this application is built with, e.g. ".NET 10 / ASP.NET Core"
    member val Technology = "" with get,set
    /// What kind of application this is. Picks the shape in diagrams.
    member val Kind = AppKind.Unspecified with get,set
    /// The lifecycle stage of this application, e.g. "production" or "experimental"
    member val Lifecycle = "" with get,set
    /// The criticality tier of this application, e.g. "1"
    member val Tier = "" with get,set

// ---------------------------------------------------------------------------------------------------------------
// Defaults for custom attributes.
//
// To create your own attribute with some values baked in, derive from one of the attributes above and put the
// matching `*Defaults` attribute on it:
//
//     [CollaboratorDefaults(AppName = "Payments", Protocol = "gRPC")]
//     public class CallsPaymentsAttribute : CollaboratorAttribute { }
//
// The Haya analyser reads the defaults from the attribute class and applies them underneath whatever is written at
// the usage site. They are separate types (not derived from the attributes above) so that the custom attribute
// class itself is not reported as a responsibility, collaborator, or meta.
// ---------------------------------------------------------------------------------------------------------------

/// Default values for a custom attribute derived from <see cref="ResponsibilityAttribute"/>.
[<AttributeUsage(AttributeTargets.Class, Inherited = false)>]
type ResponsibilityDefaultsAttribute() =
    inherit Attribute()
    /// A description of the responsibility, usecase, or feature
    member val Description = "" with get,set

/// Default values for a custom attribute derived from <see cref="CollaboratorAttribute"/>.
[<AttributeUsage(AttributeTargets.Class, Inherited = false)>]
type CollaboratorDefaultsAttribute() =
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
    /// What the collaborator is. Use `AppKind.Person` for a user: name it with `AppName`; `System` is ignored.
    member val Kind = AppKind.Unspecified with get,set

/// Default values for a custom attribute derived from <see cref="MetaAttribute"/>.
[<AttributeUsage(AttributeTargets.Class, Inherited = false)>]
type MetaDefaultsAttribute() =
    inherit Attribute()
    /// The name of this application
    member val AppName = "" with get,set
    /// The team responsible for this application
    member val Owner = "" with get,set
    /// A description of this application
    member val Description = "" with get,set
    /// The system this application belongs to
    member val System = "" with get,set
    /// The slug of the VCS repository for this application
    member val Repository = "" with get,set
    /// The technology this application is built with, e.g. ".NET 10 / ASP.NET Core"
    member val Technology = "" with get,set
    /// What kind of application this is. Picks the shape in diagrams.
    member val Kind = AppKind.Unspecified with get,set
    /// The lifecycle stage of this application, e.g. "production" or "experimental"
    member val Lifecycle = "" with get,set
    /// The criticality tier of this application, e.g. "1"
    member val Tier = "" with get,set
