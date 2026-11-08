namespace Haya.Core

open Haya.Core.Analysis

module Describe =

    let serialize (cmd: DescribeCommand) (descriptors: Descriptor list) =
        let data = Encode.description "0.0" descriptors
        match cmd.Format with
        | DataFormat.Yaml -> data |> Serializer.toYaml
        | DataFormat.Json -> data |> Serializer.toJson
