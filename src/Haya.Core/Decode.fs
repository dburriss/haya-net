namespace Haya.Core

open Flexi.Json
open Flexi.Yaml
open Haya.Core.Analysis

/// Reads a document written by `describe` (JSON or YAML) back into descriptors. Free of reflection so it compiles under Fable.
module Decode =

    let rec private ofYaml (value: YamlValue) : JsonValue =
        match value with
        | YamlValue.Null -> JsonValue.Null
        | YamlValue.Boolean b -> JsonValue.Boolean b
        | YamlValue.Number n -> JsonValue.Float (float n)
        | YamlValue.Float f -> JsonValue.Float f
        | YamlValue.String s -> JsonValue.String s
        | YamlValue.Sequence items -> JsonValue.Array (items |> Array.map ofYaml)
        | YamlValue.Mapping props ->
            props
            |> Array.map (fun (k, v) -> (match k with YamlValue.String s -> s | other -> other.ToString()), ofYaml v)
            |> JsonValue.Record
        | other -> JsonValue.String (other.ToString())

    let private prop (name: string) (value: JsonValue) =
        match value with
        | JsonValue.Record props -> props |> Array.tryFind (fun (k, _) -> k = name) |> Option.map snd
        | _ -> None

    let private str (name: string) (value: JsonValue) =
        match prop name value with
        | Some (JsonValue.String s) -> s
        | _ -> ""

    let private items (name: string) (value: JsonValue) =
        match prop name value with
        | Some (JsonValue.Array xs) -> xs |> Array.toList
        | _ -> []

    let private direction (s: string) =
        match s with
        | "Upstream" -> Direction.Upstream
        | _ -> Direction.Downstream

    let private relationship (s: string) =
        match s with
        | "Owned" -> Relationship.Owned
        | "Shared" -> Relationship.Shared
        | "Internal" -> Relationship.Internal
        | "External" -> Relationship.External
        | "InternalUser" -> Relationship.InternalUser
        | _ -> Relationship.ExternalUser

    let private responsibility (v: JsonValue) =
        Responsibility { Description = str "description" v
                         ComponentName = str "componentName" v
                         ComponentSource = str "componentSource" v }

    let private collaborator (v: JsonValue) =
        Collaborator { Direction = direction (str "direction" v)
                       Tech = str "tech" v
                       Protocol = str "protocol" v
                       DataDescription = str "dataDescription" v
                       Description = str "description" v
                       AppName = str "appName" v
                       System = str "system" v
                       Relationship = relationship (str "relationship" v)
                       ComponentName = str "componentName" v
                       ComponentSource = str "componentSource" v
                       Repository = str "repository" v
                       Owner = str "owner" v }

    let private meta (v: JsonValue) =
        Meta { AppName = str "appName" v
               Description = str "description" v
               System = str "system" v
               Repository = str "repository" v
               Owner = str "owner" v }

    let private ofDocument (doc: JsonValue) : Result<Descriptor list, string> =
        match doc with
        | JsonValue.Record _ when (prop "responsibilities" doc).IsSome || (prop "collaborators" doc).IsSome || (prop "meta" doc).IsSome ->
            Ok [ match prop "meta" doc with
                 | Some m -> meta m
                 | None -> ()
                 yield! items "responsibilities" doc |> List.map responsibility
                 yield! items "collaborators" doc |> List.map collaborator ]
        | _ -> Error "Not a haya describe document: expected 'responsibilities', 'collaborators' or 'meta'"

    /// Parse describe output. `.yaml`/`.yml` is read as YAML, anything else as JSON.
    let descriptors (fileName: string) (text: string) : Result<Descriptor list, string> =
        let lower = fileName.ToLowerInvariant()
        let isYaml = lower.EndsWith ".yaml" || lower.EndsWith ".yml"
        try
            let doc = if isYaml then ofYaml (YamlValue.Parse text) else JsonValue.Parse text
            ofDocument doc
        with e -> Error $"Could not parse describe file {fileName}: {e.Message}"

    /// True when the path looks like a describe file rather than a solution or tsconfig.
    let isDescribeFile (path: string) =
        let lower = path.ToLowerInvariant()
        lower.EndsWith ".yaml" || lower.EndsWith ".yml" || (lower.EndsWith ".json" && not (lower.EndsWith "tsconfig.json") && not (lower.EndsWith "jsconfig.json"))
