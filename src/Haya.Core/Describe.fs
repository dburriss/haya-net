namespace Haya.Core

open Haya.Core.Analysis

module Describe =

    /// Version of the describe document. 0.1 introduced the top-level `apps` array (0.0 was a single app).
    /// 0.2 added the optional `tech` and `kind` to an app's `meta`.
    /// 0.3 added the optional `lifecycle` and `tier` to an app's `meta` and the optional `language` of the app.
    /// 0.4 added the optional `kind` of a collaborator (`Person` for a user). The relationship values `InternalUser`
    /// and `ExternalUser` were removed: use `kind: Person` with `Internal` or `External`.
    /// 0.5 added the top-level `detail` (`apps`, `components` or `members`) and, at `members`, the optional per-app
    /// `components` array: each annotated class's constructors and public members.
    [<Literal>]
    let Version = "0.6"

    let serialize (cmd: DescribeCommand) (apps: App list) =
        let data = Encode.description Version cmd.Detail apps
        match cmd.Format with
        | DataFormat.Yaml -> data |> Serializer.toYaml
        | DataFormat.Json -> data |> Serializer.toJson
