namespace Haya.Core

open Haya.Core.Analysis

module Describe =
    
    let serialize (cmd: DescribeCommand) (descriptors: Descriptor list) =
        let metaOpt = descriptors |> Descriptor.metas |> List.tryHead
        let responsibilities = descriptors |> Descriptor.responsibilities
        let collaborators = descriptors |> Descriptor.collaborators
        let data =
            {|
              version = "0.0"
              meta = metaOpt |> Option.map box |> Option.defaultValue null
              collaborators = collaborators
              responsibilities = responsibilities |}
        match cmd.Format with
        | DataFormat.Yaml -> data |> Serializer.toYaml
        | DataFormat.Json -> data |> Serializer.toJson
