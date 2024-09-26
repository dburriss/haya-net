namespace Haya.Core


module Serializer =

    open System.Text.Json
    open System.Text.Json.Serialization
    open System.Text.Json.Nodes
    open System.Text.Encodings.Web
    open System.Collections.Generic

    let jsonSerializerOptions =
        JsonSerializerOptions(
            WriteIndented = true,
            DefaultIgnoreCondition  = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNameCaseInsensitive = true
        )

    let toJson x =
        JsonSerializer.Serialize(x, jsonSerializerOptions)

    let ofJson<'T> (x: string) =
        JsonSerializer.Deserialize<'T>(x, jsonSerializerOptions)

    let getNumber (jEl: JsonElement) =
        let (b,i) = jEl.TryGetInt64()
        if b then box i
        else jEl.TryGetDecimal() |> box

    let getValue (jEl: JsonElement) =
        match jEl.ValueKind with
        | JsonValueKind.Number -> getNumber jEl
        | JsonValueKind.True | JsonValueKind.False -> jEl.GetBoolean() |> box
        | _ -> jEl.GetString() |> box

    let rec private toObj (jEl: JsonNode) =
        match jEl with
        | :? JsonValue -> 
            let v = jEl.GetValue()
            getValue v
        | :? JsonArray -> 
            let jArr = jEl.AsArray()
            Array.init (jArr.Count) (fun i -> toObj(jArr.Item(i)))
        | :? JsonObject -> 
            let d = jEl.AsObject() :> IDictionary<string,JsonNode>
            d.Keys
            |> Seq.map (fun k -> (k, toObj(d[k])))
            |> dict
            |> box
        | _ -> failwithf "Unexpected token %s at %s" (jEl.ToJsonString()) (jEl.GetPath())
        
    let private serializer = YamlDotNet.Serialization.SerializerBuilder().Build()
    let toYaml x = 
        let json = toJson x
        let jNode = JsonNode.Parse(json)
        let o = toObj jNode
        serializer.Serialize(o)
