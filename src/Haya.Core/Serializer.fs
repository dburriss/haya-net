namespace Haya.Core

open Flexi.Json
open Flexi.Yaml

/// Writes Flexi `JsonValue` documents as JSON or YAML. Free of reflection so it compiles under Fable.
module Serializer =

    /// 2-space indented JSON.
    let toJson (value: JsonValue) = value.ToString(true)

    let rec private toYamlValue (value: JsonValue) =
        match value with
        | JsonValue.Null -> YamlValue.Null
        | JsonValue.Boolean b -> YamlValue.Boolean b
        | JsonValue.Integer i -> YamlValue.Number (decimal i)
        | JsonValue.Float f -> YamlValue.Float f
        | JsonValue.String s -> YamlValue.String s
        | JsonValue.Array items -> YamlValue.Sequence (items |> Array.map toYamlValue)
        | JsonValue.Record props -> YamlValue.Mapping (props |> Array.map (fun (k, v) -> YamlValue.String k, toYamlValue v))

    /// Block style YAML with 2-space indentation.
    let toYaml (value: JsonValue) = (toYamlValue value).ToString(YamlSaveOptions.None)
