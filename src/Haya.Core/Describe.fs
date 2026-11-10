namespace Haya.Core

open Haya.Core.Analysis

module Describe =

    /// Version of the describe document. 0.1 introduced the top-level `apps` array (0.0 was a single app).
    /// 0.2 added the optional `tech` and `kind` to an app's `meta`.
    /// 0.3 added the optional `lifecycle` and `tier` to an app's `meta` and the optional `language` of the app.
    [<Literal>]
    let Version = "0.3"

    let serialize (cmd: DescribeCommand) (apps: App list) =
        let data = Encode.description Version apps
        match cmd.Format with
        | DataFormat.Yaml -> data |> Serializer.toYaml
        | DataFormat.Json -> data |> Serializer.toJson
