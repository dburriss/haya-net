namespace Haya

open System
type Direction = Upstream = 1 | Downstream = 2
type Relationship = Owned = 1 | Shared = 2 | Internal = 3 | External = 4

[<AttributeUsage(AttributeTargets.Class ||| AttributeTargets.Struct, AllowMultiple = true)>]
type ResponsibilityAttribute() =
    inherit Attribute()
    member val Description = "" with get,set
    
[<AttributeUsage(AttributeTargets.Class ||| AttributeTargets.Struct, AllowMultiple = true)>]
type CollaboratorAttribute() =
    inherit Attribute()
    member val Direction = Direction.Downstream with get,set
    member val Protocol = "HTTPS" with get,set
    member val DataDescription = "" with get,set
    member val Description = "" with get,set
    member val AppName = "" with get,set
    member val System = "" with get,set
    member val Relationship = Relationship.Internal with get,set
    member val Repository = "" with get,set

[<AttributeUsage(AttributeTargets.Assembly)>]
type MetaAttribute() =
    inherit Attribute()
    member val AppName = "" with get,set
    member val Description = "" with get,set
    member val System = "" with get,set
    member val Repository = "" with get,set
