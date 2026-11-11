/// Validates what `haya datadog` writes against Datadog's own JSON Schemas for the v3 entity model, which are vendored in
/// `schemas/datadog/v3` (see the README there). Nothing is fetched at test time.
module DatadogSchemaTests

open System
open System.IO
open System.Text.Json
open System.Text.Json.Nodes
open Flexi.Yaml
open Haya.Core.Analysis
open Json.Schema
open Xunit

let private schemaDirectory = Path.Combine(AppContext.BaseDirectory, "schemas", "datadog", "v3")

// The schemas refer to each other by absolute raw.githubusercontent.com URLs, which are their `$id`s, so building them all
// into one registry makes every `$ref` resolve to the vendored file.
let private buildOptions = BuildOptions(SchemaRegistry = SchemaRegistry())

let private schemas =
    Directory.GetFiles(schemaDirectory, "*.schema.json")
    |> Array.map (fun file -> Path.GetFileName(file).Replace(".schema.json", ""), JsonSchema.FromText(File.ReadAllText file, buildOptions))
    |> Map.ofArray

/// The problems of validating `entity` against the schema of its kind, none when it is valid.
let private problems (entity: JsonNode) : string list =
    let kind = entity["kind"].GetValue<string>()
    match schemas |> Map.tryFind kind with
    | None -> [ $"there is no schema for kind '{kind}'" ]
    | Some schema ->
        let results = schema.Evaluate(JsonSerializer.SerializeToElement entity, EvaluationOptions(OutputFormat = OutputFormat.List))
        if results.IsValid then []
        else
            results.Details
            |> Seq.filter (fun d -> not d.IsValid && d.Errors <> null)
            |> Seq.collect (fun d -> d.Errors |> Seq.map (fun e -> $"{d.InstanceLocation}: {e.Key} {e.Value}"))
            |> Seq.toList
            |> function [] -> [ "invalid" ] | errors -> errors

let private assertValid (entities: JsonNode list) =
    Assert.NotEmpty entities
    for entity in entities do
        let found = problems entity
        Assert.True(found.IsEmpty, $"{entity.ToJsonString()}\n{String.Join(Environment.NewLine, found)}")

let private entitiesOf (text: string) : JsonNode list =
    (JsonNode.Parse text).AsArray() |> Seq.map (fun n -> n) |> Seq.toList

let private jsonCatalog (apps: App list) =
    Haya.Core.Datadog.generateCatalog { InputPaths = []; OutputPath = ""; Format = Haya.Core.DataFormat.Json; CurrentDirectory = "" } apps
    |> entitiesOf

let private yamlCatalog (apps: App list) =
    Haya.Core.Datadog.generateCatalog { InputPaths = []; OutputPath = ""; Format = Haya.Core.DataFormat.Yaml; CurrentDirectory = "" } apps

let rec private ofYaml (value: YamlValue) : JsonNode =
    match value with
    | YamlValue.Null -> null
    | YamlValue.Boolean b -> JsonValue.Create b
    | YamlValue.Number n -> JsonValue.Create(float n)
    | YamlValue.Float f -> JsonValue.Create f
    | YamlValue.String s -> JsonValue.Create s
    | YamlValue.Sequence items ->
        let array = JsonArray()
        for item in items do array.Add(ofYaml item)
        array :> JsonNode
    | YamlValue.Mapping props ->
        let o = JsonObject()
        for (k, v) in props do o[(match k with YamlValue.String s -> s | other -> other.ToString())] <- ofYaml v
        o :> JsonNode
    | other -> JsonValue.Create(other.ToString())

// ---- inputs -------------------------------------------------------------------------------------------------------------

let private meta name system kind : MetaDescriptor =
    { AppName = name; Description = $"{name} description"; System = system; Repository = ""; Owner = "Team"
      Tech = ""; Kind = kind; Lifecycle = ""; Tier = "" }

let private collaborator name system direction relationship : CollaboratorDescriptor =
    { Direction = direction; Tech = ""; Protocol = ""; DataDescription = "uses"; Description = $"{name} description"
      AppName = name; System = system; Relationship = relationship; ComponentName = ""; ComponentSource = ""
      Repository = ""; Owner = "Owner"; Kind = AppKind.Unspecified }

let private app (m: MetaDescriptor) language collaborators : App =
    { Meta = Some m; Language = language; Responsibilities = []; Collaborators = collaborators }

/// Every kind of app, calls both ways, apps of several systems, stubs, and every optional value set and unset.
let private rich : App list =
    let down = collaborator
    [ app { meta "Web" "Shop" AppKind.WebApp with Lifecycle = "production"; Tier = "1"; Repository = "https://github.com/org/web" }
          Language.JavaScript [ down "Api" "Ordering" Direction.Downstream Relationship.Internal ]
      app { meta "Api" "Ordering" AppKind.Api with Lifecycle = "experimental"; Repository = "org/api" }
          Language.DotNet
          [ down "Db" "Ordering" Direction.Downstream Relationship.Internal
            down "Events" "Ordering" Direction.Downstream Relationship.Internal
            down "Visa" "Visa" Direction.Downstream Relationship.External
            down "Mobile" "Shop" Direction.Upstream Relationship.External
            // a person has no entity in Datadog: it must not turn into a stub
            { down "Customer" "" Direction.Upstream Relationship.External with Kind = AppKind.Person } ]
      app { meta "Db" "Ordering" AppKind.Database with Tier = "2" } Language.Unknown []
      app (meta "Events" "Ordering" AppKind.Queue) Language.Unknown []
      app (meta "Worker" "Ordering" AppKind.Worker) Language.DotNet [ down "Events" "Ordering" Direction.Upstream Relationship.Internal ]
      app (meta "Plain" "" AppKind.Unspecified) Language.Unknown [] ]

// ---- the output is valid ------------------------------------------------------------------------------------------------

[<Fact>]
let ``The vendored schemas are there and cover every kind haya writes`` () =
    for kind in [ "service"; "datastore"; "queue"; "system"; "entity"; "metadata" ] do
        Assert.True(schemas.ContainsKey kind, kind)

[<Fact>]
let ``The entities of the example solution are valid`` () =
    let example = App.ofDescriptors DescriptorTests.allDescriptors |> App.withLanguage Language.DotNet
    assertValid (jsonCatalog [ example ])

[<Fact>]
let ``The entities of every kind of app, relation and optional value are valid`` () =
    let entities = jsonCatalog rich
    assertValid entities
    // the input really did produce every kind that has a schema here
    let kinds = entities |> List.map (fun e -> e["kind"].GetValue<string>()) |> List.distinct |> List.sort
    Assert.Equal<string list>([ "datastore"; "queue"; "service"; "system" ], kinds)

[<Fact>]
let ``The yaml is the same entities as the json, and valid`` () =
    let fromYaml =
        (yamlCatalog rich).Split("---\n")
        |> Array.map (fun doc -> ofYaml (YamlValue.Parse doc))
        |> Array.toList
    assertValid fromYaml
    Assert.Equal<string list>(
        (jsonCatalog rich |> List.map (fun e -> e.ToJsonString())),
        (fromYaml |> List.map (fun e -> e.ToJsonString())))

[<Fact>]
let ``Apps without a meta only contribute stubs, which are valid`` () =
    let anon : App = { Meta = None; Language = Language.Unknown; Responsibilities = []; Collaborators = [ collaborator "Visa" "Visa" Direction.Downstream Relationship.External ] }
    assertValid (jsonCatalog [ anon ])

// ---- the validation can fail --------------------------------------------------------------------------------------------

let private node (json: string) = JsonNode.Parse json

[<Fact>]
let ``A valid entity passes and an entity the schema forbids does not`` () =
    let valid = node """{ "apiVersion": "v3", "kind": "service", "metadata": { "name": "a" }, "spec": { "dependsOn": [ "service:b" ] } }"""
    Assert.Empty(problems valid)
    let invalid =
        [ "dependsOn on an api", """{ "apiVersion": "v3", "kind": "api", "metadata": { "name": "a" }, "spec": { "dependsOn": [ "service:b" ] } }"""
          "dependsOn on a system", """{ "apiVersion": "v3", "kind": "system", "metadata": { "name": "a" }, "spec": { "dependsOn": [ "service:b" ] } }"""
          "dependencyOf on a service", """{ "apiVersion": "v3", "kind": "service", "metadata": { "name": "a" }, "spec": { "dependencyOf": [ "service:b" ] } }"""
          "languages on a datastore", """{ "apiVersion": "v3", "kind": "datastore", "metadata": { "name": "a" }, "spec": { "languages": [ "dotnet" ] } }"""
          "a namespace in the metadata", """{ "apiVersion": "v3", "kind": "service", "metadata": { "name": "a", "namespace": "default" } }"""
          "an unknown apiVersion", """{ "apiVersion": "v2", "kind": "service", "metadata": { "name": "a" } }"""
          "a missing name", """{ "apiVersion": "v3", "kind": "service", "metadata": { } }"""
          "an unknown top-level key", """{ "apiVersion": "v3", "kind": "service", "metadata": { "name": "a" }, "dependsOn": [] }""" ]
    for what, json in invalid do
        Assert.True(not (problems (node json)).IsEmpty, $"the schema should reject {what}")
