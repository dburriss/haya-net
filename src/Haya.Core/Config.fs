namespace Haya.Core

open Flexi.Json

/// Layered configuration (ADR020). Settings are addressed by dotted keys (`output.path`, `viewer.complexity.systems`) from a
/// fixed key table, and come from, lowest to highest precedence: the global config file, the nearest local
/// `.haya/config.json`, and `HAYA_*` environment variables. Command line flags beat all of them (the CLIs apply that).
/// Free of reflection and .NET-only APIs so it compiles under Fable and both CLIs share one implementation.
module Config =

    type Kind =
        | KText
        | KBool
        | KList
        | KInts
        | KChoice of string list

    type Value =
        | VText of string
        | VBool of bool
        | VList of string list
        | VInts of int list

    type KeyInfo = { Key: string; Kind: Kind; Doc: string }

    let private detailsAll = [ "systems"; "apps"; "components"; "members" ]
    let private dataFormats = [ "json"; "yaml" ]
    let private diagramFormats = [ "mermaid"; "html" ]

    /// Every setting. Language-specific settings live in a section named for the stack (`dotnet.*`).
    let keys : KeyInfo list =
        [ { Key = "dotnet.references.include"; Kind = KBool; Doc = "Also analyse referenced assemblies (.NET only)" }
          { Key = "dotnet.references.assemblies"; Kind = KList; Doc = "Glob patterns of referenced assemblies to include (.NET only)" }
          { Key = "output.path"; Kind = KText; Doc = "Directory the commands write to; each keeps its own file name" }
          { Key = "output.formats"; Kind = KList; Doc = "Default formats, first one a command accepts wins" }
          { Key = "crc.diagram"; Kind = KChoice [ "none"; "systems"; "apps" ]; Doc = "C4 diagrams in the CRC document: systems (L1) or apps (L2)" }
          { Key = "crc.output"; Kind = KText; Doc = "Output file of crc" }
          { Key = "diagram.detail"; Kind = KChoice detailsAll; Doc = "Default detail of diagram" }
          { Key = "diagram.format"; Kind = KChoice diagramFormats; Doc = "Default format of diagram" }
          { Key = "diagram.output"; Kind = KText; Doc = "Output file of diagram" }
          { Key = "describe.detail"; Kind = KChoice [ "apps"; "components"; "members" ]; Doc = "Default detail of describe" }
          { Key = "describe.format"; Kind = KChoice dataFormats; Doc = "Default format of describe" }
          { Key = "describe.output"; Kind = KText; Doc = "Output file of describe" }
          { Key = "backstage.format"; Kind = KChoice dataFormats; Doc = "Default format of backstage" }
          { Key = "backstage.output"; Kind = KText; Doc = "Output file of backstage" }
          { Key = "datadog.format"; Kind = KChoice dataFormats; Doc = "Default format of datadog" }
          { Key = "datadog.output"; Kind = KText; Doc = "Output file of datadog" }
          { Key = "viewer.detail"; Kind = KChoice detailsAll; Doc = "How deep the html explorer opens at first" }
          { Key = "viewer.complexity.systems"; Kind = KInts; Doc = "Four thresholds colouring a system border by apps + relationships" }
          { Key = "viewer.complexity.links"; Kind = KInts; Doc = "Four thresholds colouring a system-to-system line by relationships" } ]

    let tryKey (key: string) = keys |> List.tryFind (fun k -> k.Key = key)

    // ------------------------------------------------------------ values

    let private kindName (kind: Kind) =
        match kind with
        | KText -> "text"
        | KBool -> "true or false"
        | KList -> "a comma-separated list"
        | KInts -> "four comma-separated whole numbers"
        | KChoice xs -> "one of " + String.concat ", " xs

    let private checkInts (key: string) (xs: int list) =
        // the viewer ramps have exactly four stops, rising
        if key.StartsWith "viewer.complexity." then
            if List.length xs <> 4 then Error $"{key} needs four numbers, got {List.length xs}"
            elif xs <> List.sort xs then Error $"{key} must be in ascending order"
            else Ok (VInts xs)
        else Ok (VInts xs)

    /// Parses text typed by a person (command line or environment variable) for the key's kind.
    let parseValue (info: KeyInfo) (raw: string) : Result<Value, string> =
        let raw = raw.Trim()
        let bad () = Error $"'{raw}' is not valid for {info.Key}: expected {kindName info.Kind}"
        match info.Kind with
        | KText -> Ok (VText raw)
        | KBool ->
            match raw.ToLowerInvariant() with
            | "true" -> Ok (VBool true)
            | "false" -> Ok (VBool false)
            | _ -> bad ()
        | KList ->
            raw.Split(',') |> Array.map (fun s -> s.Trim()) |> Array.filter (fun s -> s <> "") |> Array.toList |> VList |> Ok
        | KInts ->
            let parts = raw.Split(',') |> Array.map (fun s -> s.Trim()) |> Array.filter (fun s -> s <> "")
            let parsed = parts |> Array.map (fun s -> System.Int32.TryParse s)
            if parsed |> Array.exists (fun (ok, _) -> not ok) then bad ()
            else checkInts info.Key (parsed |> Array.map snd |> Array.toList)
        | KChoice xs ->
            let v = raw.ToLowerInvariant()
            if List.contains v xs then Ok (VText v) else bad ()

    let formatValue (value: Value) =
        match value with
        | VText s -> s
        | VBool b -> if b then "true" else "false"
        | VList xs -> String.concat "," xs
        | VInts xs -> xs |> List.map string |> String.concat ","

    let private toJsonValue (value: Value) : JsonValue =
        match value with
        | VText s -> JsonValue.String s
        | VBool b -> JsonValue.Boolean b
        | VList xs -> xs |> List.map JsonValue.String |> List.toArray |> JsonValue.Array
        | VInts xs -> xs |> List.map (fun i -> JsonValue.Integer (int64 i)) |> List.toArray |> JsonValue.Array

    let private ofJsonValue (info: KeyInfo) (json: JsonValue) : Result<Value, string> =
        let bad () = Error $"{info.Key} must be {kindName info.Kind}"
        match info.Kind, json with
        | KText, JsonValue.String s -> Ok (VText s)
        | KBool, JsonValue.Boolean b -> Ok (VBool b)
        | KList, JsonValue.Array xs ->
            let strings = xs |> Array.choose (fun x -> match x with JsonValue.String s -> Some s | _ -> None)
            if strings.Length = xs.Length then Ok (VList (Array.toList strings)) else bad ()
        | KInts, JsonValue.Array xs ->
            let nums =
                xs |> Array.choose (fun x ->
                    match x with
                    | JsonValue.Integer i -> Some (int i)
                    | JsonValue.Float f when f = System.Math.Floor f -> Some (int f)
                    | _ -> None)
            if nums.Length = xs.Length then checkInts info.Key (Array.toList nums) else bad ()
        | KChoice _, JsonValue.String s -> parseValue info s
        | _ -> bad ()

    // ------------------------------------------------------------ layers

    /// One source of settings: flat dotted key -> value.
    type Layer = Map<string, Value>

    type Origin =
        | Global of string
        | Local of string
        | Env of string

    let originText (o: Origin) =
        match o with
        | Global f -> $"global ({f})"
        | Local f -> $"local ({f})"
        | Env n -> $"env ({n})"

    /// The effective settings, each with where it came from, plus warnings (unknown keys).
    type Resolved = { Values: Map<string, Value * Origin>; Warnings: string list }

    let empty : Resolved = { Values = Map.empty; Warnings = [] }

    let rec private flatten (prefix: string) (json: JsonValue) : (string * JsonValue) list =
        match json with
        | JsonValue.Record props ->
            props
            |> Array.toList
            |> List.collect (fun (k, v) -> flatten (if prefix = "" then k else prefix + "." + k) v)
        | leaf -> [ prefix, leaf ]

    /// Reads a config file's text. Unknown keys are returned as warnings; a wrong type or value is an error.
    let ofJson (text: string) : Result<Layer * string list, string> =
        let parsed = try Ok (JsonValue.Parse text) with e -> Error e.Message
        match parsed with
        | Error m -> Error $"not valid JSON: {m}"
        | Ok (JsonValue.Record _ as root) ->
            let step (acc: Result<Layer * string list, string>) (key: string, json: JsonValue) =
                match acc with
                | Error _ -> acc
                | Ok (layer, warnings) ->
                    match tryKey key with
                    | None -> Ok (layer, warnings @ [ $"unknown setting '{key}'" ])
                    | Some info ->
                        match ofJsonValue info json with
                        | Ok v -> Ok (Map.add key v layer, warnings)
                        | Error m -> Error m
            flatten "" root |> List.fold step (Ok (Map.empty, []))
        | Ok _ -> Error "expected a JSON object"

    /// The nested JSON document for a layer, sections in key table order.
    let toJson (layer: Layer) : JsonValue =
        let rec build (prefix: string) : JsonValue =
            // child names of this prefix, in key table order, without repeats
            let rest = if prefix = "" then (fun (k: string) -> Some k) else (fun (k: string) -> if k.StartsWith(prefix + ".") then Some (k.Substring(prefix.Length + 1)) else None)
            let names =
                keys
                |> List.choose (fun k -> if Map.containsKey k.Key layer then rest k.Key else None)
                |> List.map (fun r -> (r.Split('.')).[0])
                |> List.distinct
            names
            |> List.map (fun n ->
                let full = if prefix = "" then n else prefix + "." + n
                match Map.tryFind full layer with
                | Some v -> n, toJsonValue v
                | None -> n, build full)
            |> List.toArray
            |> JsonValue.Record
        build ""

    let envName (key: string) = "HAYA_" + key.Replace(".", "__").ToUpperInvariant()

    /// Settings from environment variables, e.g. `HAYA_OUTPUT__PATH`. A bad value is an error naming the variable.
    let ofEnv () : Result<(string * Value * Origin) list, string> =
        let step (acc: Result<(string * Value * Origin) list, string>) (info: KeyInfo) =
            match acc with
            | Error _ -> acc
            | Ok found ->
                let name = envName info.Key
                match IO.getEnv name with
                | None -> acc
                | Some raw ->
                    match parseValue info raw with
                    | Ok v -> Ok (found @ [ info.Key, v, Env name ])
                    | Error m -> Error $"{name}: {m}"
        keys |> List.fold step (Ok [])

    // ------------------------------------------------------------ files

    let private fileName = "config.json"

    /// The local config file of a directory: `<dir>/.haya/config.json`.
    let localFileIn (dir: string) = IO.combine (IO.combine dir ".haya") fileName

    /// The nearest `.haya/config.json` from `dir` upwards.
    let rec findLocal (dir: string) : string option =
        let candidate = localFileIn dir
        if IO.fileExists candidate then Some candidate
        else
            match IO.parent dir with
            | Some p when p <> dir -> findLocal p
            | _ -> None

    /// The global config file: `$HAYA_CONFIG_DIR/config.json` when set, else `<OS config dir>/haya/config.json`.
    let globalFile () =
        match IO.getEnv "HAYA_CONFIG_DIR" with
        | Some dir -> IO.combine dir fileName
        | None -> IO.combine (IO.combine (IO.configDir ()) "haya") fileName

    let private readLayer (file: string) : Result<Layer * string list, string> =
        if not (IO.fileExists file) then Ok (Map.empty, [])
        else
            let text = try Ok (IO.readAllText file) with e -> Error e.Message
            match text with
            | Error m -> Error $"{file}: {m}"
            | Ok t ->
                match ofJson (if t.Trim() = "" then "{}" else t) with
                | Ok (layer, warnings) -> Ok (layer, warnings |> List.map (fun w -> $"{file}: {w}"))
                | Error m -> Error $"{file}: {m}"

    let private overlay (origin: Origin) (layer: Layer) (resolved: Resolved) =
        let values = layer |> Map.fold (fun acc k v -> Map.add k (v, origin) acc) resolved.Values
        { resolved with Values = values }

    /// The effective configuration for commands run in `cwd`.
    let load (cwd: string) : Result<Resolved, string> =
        let gfile = globalFile ()
        let lfile = findLocal cwd
        match readLayer gfile with
        | Error m -> Error m
        | Ok (g, gw) ->
            let local = match lfile with Some f -> readLayer f |> Result.map (fun (l, w) -> f, l, w) | None -> Ok ("", Map.empty, [])
            match local with
            | Error m -> Error m
            | Ok (lf, l, lw) ->
                match ofEnv () with
                | Error m -> Error m
                | Ok env ->
                    let r = { empty with Warnings = gw @ lw } |> overlay (Global gfile) g |> overlay (Local lf) l
                    let values = env |> List.fold (fun acc (k, v, o) -> Map.add k (v, o) acc) r.Values
                    Ok { r with Values = values }

    // ------------------------------------------------------------ reading settings

    let tryValue (cfg: Resolved) (key: string) = cfg.Values |> Map.tryFind key |> Option.map fst

    let text (cfg: Resolved) (key: string) =
        match tryValue cfg key with
        | Some (VText s) -> Some s
        | _ -> None

    let flag (cfg: Resolved) (key: string) =
        match tryValue cfg key with
        | Some (VBool b) -> Some b
        | _ -> None

    let list (cfg: Resolved) (key: string) =
        match tryValue cfg key with
        | Some (VList xs) -> xs
        | _ -> []

    let ints (cfg: Resolved) (key: string) =
        match tryValue cfg key with
        | Some (VInts xs) -> Some xs
        | _ -> None

    let detail (cfg: Resolved) (key: string) : Detail option =
        match text cfg key with
        | Some "systems" -> Some Detail.Systems
        | Some "apps" -> Some Detail.Apps
        | Some "components" -> Some Detail.Components
        | Some "members" -> Some Detail.Members
        | _ -> None

    /// The format a command writes: its own `<command>.format`, else the first of `output.formats` it accepts (lower case names).
    let format (cfg: Resolved) (command: string) (accepted: string list) : string option =
        match text cfg (command + ".format") with
        | Some f -> Some f
        | None ->
            list cfg "output.formats"
            |> List.map (fun f -> f.ToLowerInvariant())
            |> List.tryFind (fun f -> List.contains f accepted)

    /// Where a command writes when `-o` was not given: its own `<command>.output`, else `output.path` plus the command's
    /// default file name, else the default as it is.
    let outputPath (cfg: Resolved) (command: string) (defaultFile: string) : string =
        match text cfg (command + ".output") with
        | Some f -> f
        | None ->
            match text cfg "output.path" with
            | Some dir ->
                let name = defaultFile.Replace('\\', '/').Split('/') |> Array.last
                IO.combine dir name
            | None -> defaultFile

    /// The explorer settings in the configuration.
    let viewer (cfg: Resolved) : ViewerSettings =
        { Detail = detail cfg "viewer.detail"
          Systems = ints cfg "viewer.complexity.systems"
          Links = ints cfg "viewer.complexity.links" }

    /// `crc.diagram` as the (L1, L2) flags of the crc command, or None when unset.
    let crcDiagrams (cfg: Resolved) : (bool * bool) option =
        match text cfg "crc.diagram" with
        | Some "none" -> Some (false, false)
        | Some "systems" -> Some (true, false)
        | Some "apps" -> Some (true, true)
        | _ -> None

    // ------------------------------------------------------------ the config command

    /// `key = value` lines of the effective configuration, optionally with where each value came from.
    let lines (cfg: Resolved) (showOrigin: bool) : string list =
        keys
        |> List.choose (fun k ->
            cfg.Values
            |> Map.tryFind k.Key
            |> Option.map (fun (v, o) ->
                if showOrigin then $"{k.Key} = {formatValue v}\t{originText o}" else $"{k.Key} = {formatValue v}"))

    let private checkKey (key: string) =
        match tryKey key with
        | Some info -> Ok info
        | None ->
            let known = keys |> List.map (fun k -> k.Key) |> String.concat ", "
            Error $"Unknown setting '{key}'. Known settings: {known}"

    /// The value of a key from the effective configuration, or from one file's layer only.
    let get (cfg: Resolved) (key: string) : Result<string option, string> =
        checkKey key |> Result.map (fun _ -> tryValue cfg key |> Option.map formatValue)

    /// Where `--local` and `--global` write: the nearest local file, else a new one in `cwd`.
    let targetFile (cwd: string) (isGlobal: bool) =
        if isGlobal then globalFile () else (findLocal cwd |> Option.defaultWith (fun () -> localFileIn cwd))

    /// The value of a key in one file only.
    let getFrom (file: string) (key: string) : Result<string option, string> =
        match checkKey key with
        | Error m -> Error m
        | Ok _ -> readLayer file |> Result.map (fun (l, _) -> Map.tryFind key l |> Option.map formatValue)

    let private update (file: string) (key: string) (change: Layer -> Result<Layer, string>) : Result<string, string> =
        match readLayer file with
        | Error m -> Error m
        | Ok (layer, _) ->
            match change layer with
            | Error m -> Error m
            | Ok changed ->
                IO.createDirectory (match IO.parent file with Some d -> d | None -> ".")
                IO.write file (Serializer.toJson (toJson changed) + "\n") |> ignore
                Ok file

    /// Writes `key` = `raw` to a config file, validating first. Other settings in the file are kept.
    let set (file: string) (key: string) (raw: string) : Result<string, string> =
        match checkKey key with
        | Error m -> Error m
        | Ok info ->
            match parseValue info raw with
            | Error m -> Error m
            | Ok v -> update file key (fun layer -> Ok (Map.add key v layer))

    /// Removes `key` from a config file.
    let unset (file: string) (key: string) : Result<string, string> =
        match checkKey key with
        | Error m -> Error m
        | Ok _ ->
            update file key (fun layer -> Ok (Map.remove key layer))
