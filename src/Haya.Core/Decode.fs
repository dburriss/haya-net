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
        // not written: the attribute default
        | "" -> Relationship.Internal
        | other -> failwith $"Unknown relationship '{other}': expected Owned, Shared, Internal or External"

    let private appKind (s: string) =
        match s with
        | "Service" -> AppKind.Service
        | "WebApp" -> AppKind.WebApp
        | "Api" -> AppKind.Api
        | "Worker" -> AppKind.Worker
        | "Database" -> AppKind.Database
        | "Queue" -> AppKind.Queue
        | "Person" -> AppKind.Person
        | _ -> AppKind.Unspecified

    let private language (s: string) =
        match s with
        | "dotnet" -> Language.DotNet
        | "javascript" -> Language.JavaScript
        | _ -> Language.Unknown

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
                       Owner = str "owner" v
                       Kind = appKind (str "kind" v) }

    let private meta (v: JsonValue) =
        Meta { AppName = str "appName" v
               Description = str "description" v
               System = str "system" v
               Repository = str "repository" v
               Owner = str "owner" v
               Tech = str "tech" v
               Kind = appKind (str "kind" v)
               Lifecycle = str "lifecycle" v
               Tier = str "tier" v }

    let private memberKind (s: string) =
        match s with
        | "Property" -> MemberKind.Property
        | "Field" -> MemberKind.Field
        | "Event" -> MemberKind.Event
        | _ -> MemberKind.Method

    let private parameter (v: JsonValue) : Parameter = { Name = str "name" v; Type = str "type" v }

    let private classMember (v: JsonValue) : Member =
        { Name = str "name" v
          Kind = memberKind (str "kind" v)
          Type = str "type" v
          Parameters = items "parameters" v |> List.map parameter }

    let private componentDetail (v: JsonValue) : ComponentDetail =
        { Name = str "name" v
          Source = str "source" v
          Constructors =
              items "constructors" v
              |> List.map (fun c -> match c with
                                    | JsonValue.Array ps -> ps |> Array.toList |> List.map parameter
                                    | _ -> [])
          Members = items "members" v |> List.map classMember
          // 0.6; missing (and in 0.5) means none
          Dependencies =
              items "dependencies" v
              |> List.map (fun d -> { Name = str "name" d; Source = str "source" d; Via = items "via" d |> List.map (fun x -> match x with JsonValue.String s -> s | _ -> "") }) }

    let private isApp (doc: JsonValue) =
        match doc with
        | JsonValue.Record _ -> (prop "responsibilities" doc).IsSome || (prop "collaborators" doc).IsSome || (prop "meta" doc).IsSome
        | _ -> false

    let private ofApp (doc: JsonValue) : App =
        App.ofDescriptors
            [ match prop "meta" doc with
              | Some m -> meta m
              | None -> ()
              yield! items "responsibilities" doc |> List.map responsibility
              yield! items "collaborators" doc |> List.map collaborator ]
        |> App.withLanguage (language (str "language" doc))
        // 0.5 at `--detail members`; missing (and in 0.0 to 0.4) means none
        |> App.withComponents (items "components" doc |> List.map componentDetail)

    /// Version 0.1 documents hold an `apps` array; version 0.0 documents are a single app.
    let private ofDocument (doc: JsonValue) : Result<App list, string> =
        match prop "apps" doc with
        | Some (JsonValue.Array xs) when xs |> Array.forall isApp -> Ok (xs |> Array.toList |> List.map ofApp)
        | Some _ -> Error "Not a haya describe document: every entry of 'apps' needs 'responsibilities', 'collaborators' or 'meta'"
        | None when isApp doc -> Ok [ ofApp doc ]
        | None -> Error "Not a haya describe document: expected 'apps', 'responsibilities', 'collaborators' or 'meta'"

    /// Parse describe output. `.yaml`/`.yml` is read as YAML, anything else as JSON.
    let apps (fileName: string) (text: string) : Result<App list, string> =
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
