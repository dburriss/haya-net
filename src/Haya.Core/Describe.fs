namespace Haya.Core

open Haya.Core.Analysis

module Describe =

    /// Version of the describe document. 0.1 introduced the top-level `apps` array (0.0 was a single app).
    [<Literal>]
    let Version = "0.1"

    let serialize (cmd: DescribeCommand) (apps: App list) =
        let data = Encode.description Version apps
        match cmd.Format with
        | DataFormat.Yaml -> data |> Serializer.toYaml
        | DataFormat.Json -> data |> Serializer.toJson
