module ConfigTests

open System
open System.IO
open Haya.Core
open Xunit

// Layered configuration (ADR020). All in one module so the tests that set environment variables run one after another.

let private ok r = match r with Ok v -> v | Error (m: string) -> failwith m

/// A scratch directory holding a project (with its own .haya) and a global config directory; the environment points at it.
let private withDirs (test: string -> string -> unit) =
    let root = Path.Combine(Path.GetTempPath(), "haya-config-" + Guid.NewGuid().ToString("N"))
    let project = Path.Combine(root, "project", "sub")
    let globalDir = Path.Combine(root, "global")
    Directory.CreateDirectory project |> ignore
    let before = Config.keys |> List.map (fun k -> Config.envName k.Key, Environment.GetEnvironmentVariable(Config.envName k.Key))
    let beforeDir = Environment.GetEnvironmentVariable "HAYA_CONFIG_DIR"
    for name, _ in before do Environment.SetEnvironmentVariable(name, null)
    Environment.SetEnvironmentVariable("HAYA_CONFIG_DIR", globalDir)
    try test (Path.Combine(root, "project")) globalDir
    finally
        for name, value in before do Environment.SetEnvironmentVariable(name, value)
        Environment.SetEnvironmentVariable("HAYA_CONFIG_DIR", beforeDir)
        Directory.Delete(root, true)

let private write (file: string) (text: string) =
    Directory.CreateDirectory(Path.GetDirectoryName file) |> ignore
    File.WriteAllText(file, text)

[<Fact>]
let ``Nothing configured resolves to nothing`` () =
    withDirs (fun project _ ->
        let cfg = Config.load project |> ok
        Assert.Empty cfg.Values
        Assert.Equal<string list>([ "./CRC.md" ], [ Config.outputPath cfg "crc" "./CRC.md" ]))

[<Fact>]
let ``A local file is found from a subdirectory`` () =
    withDirs (fun project _ ->
        write (Path.Combine(project, ".haya", "config.json")) """{ "crc": { "diagram": "apps" } }"""
        let cfg = Config.load (Path.Combine(project, "sub")) |> ok
        Assert.Equal(Some (true, true), Config.crcDiagrams cfg))

[<Fact>]
let ``Local beats global per setting and env beats both`` () =
    withDirs (fun project globalDir ->
        write (Path.Combine(globalDir, "config.json")) """{ "output": { "path": "g" }, "diagram": { "format": "html", "detail": "apps" } }"""
        write (Path.Combine(project, ".haya", "config.json")) """{ "output": { "path": "l" }, "diagram": { "detail": "members" } }"""
        Environment.SetEnvironmentVariable("HAYA_DIAGRAM__DETAIL", "components")
        let cfg = Config.load project |> ok
        Assert.Equal(Some "l", Config.text cfg "output.path")            // local over global
        Assert.Equal(Some "html", Config.text cfg "diagram.format")      // only global sets it
        Assert.Equal(Some Detail.Components, Config.detail cfg "diagram.detail") // env over local and global
        match Map.find "diagram.detail" cfg.Values |> snd with
        | Config.Env name -> Assert.Equal("HAYA_DIAGRAM__DETAIL", name)
        | other -> failwithf "unexpected origin %A" other)

[<Fact>]
let ``The output directory joins the command's file name and a command output wins`` () =
    withDirs (fun project _ ->
        write (Path.Combine(project, ".haya", "config.json")) """{ "output": { "path": "docs/haya" }, "crc": { "output": "docs/CRC.md" } }"""
        let cfg = Config.load project |> ok
        Assert.Equal("docs/haya/haya.json", Config.outputPath cfg "describe" "./haya.json")
        Assert.Equal("docs/CRC.md", Config.outputPath cfg "crc" "./CRC.md"))

[<Fact>]
let ``The first output format a command accepts is used`` () =
    withDirs (fun project _ ->
        write (Path.Combine(project, ".haya", "config.json")) """{ "output": { "formats": ["html", "yaml"] }, "datadog": { "format": "json" } }"""
        let cfg = Config.load project |> ok
        Assert.Equal(Some "html", Config.format cfg "diagram" [ "mermaid"; "html" ])
        Assert.Equal(Some "yaml", Config.format cfg "backstage" [ "json"; "yaml" ])
        Assert.Equal(Some "json", Config.format cfg "datadog" [ "json"; "yaml" ]) // its own setting wins
        Assert.Equal(None, Config.format cfg "crc" [ "markdown" ]))

[<Fact>]
let ``Viewer settings are read and the complexity stops are validated`` () =
    withDirs (fun project _ ->
        write (Path.Combine(project, ".haya", "config.json")) """{ "viewer": { "detail": "apps", "complexity": { "systems": [2,3,4,5] } } }"""
        let v = Config.load project |> ok |> Config.viewer
        Assert.Equal(Some Detail.Apps, v.Detail)
        Assert.Equal(Some [ 2; 3; 4; 5 ], v.Systems)
        Assert.Equal(None, v.Links)
        write (Path.Combine(project, ".haya", "config.json")) """{ "viewer": { "complexity": { "systems": [5,4,3,2] } } }"""
        match Config.load project with
        | Error m -> Assert.Contains("ascending", m)
        | Ok _ -> failwith "expected an error")

[<Fact>]
let ``A wrong value names the file and a unknown key is only a warning`` () =
    withDirs (fun project _ ->
        let file = Path.Combine(project, ".haya", "config.json")
        write file """{ "crc": { "diagram": "everything" } }"""
        match Config.load project with
        | Error m ->
            Assert.Contains("config.json", m)
            Assert.Contains("crc.diagram", m)
        | Ok _ -> failwith "expected an error"
        write file """{ "crc": { "dagram": "apps" } }"""
        let cfg = Config.load project |> ok
        Assert.Contains(cfg.Warnings, fun w -> w.Contains "crc.dagram"))

[<Fact>]
let ``A bad environment variable is an error naming the variable`` () =
    withDirs (fun project _ ->
        Environment.SetEnvironmentVariable("HAYA_DOTNET__REFERENCES__INCLUDE", "maybe")
        match Config.load project with
        | Error m -> Assert.Contains("HAYA_DOTNET__REFERENCES__INCLUDE", m)
        | Ok _ -> failwith "expected an error")

[<Fact>]
let ``Environment lists are comma separated`` () =
    withDirs (fun project _ ->
        Environment.SetEnvironmentVariable("HAYA_DOTNET__REFERENCES__ASSEMBLIES", "A.*, B.*")
        let cfg = Config.load project |> ok
        Assert.Equal<string list>([ "A.*"; "B.*" ], Config.list cfg "dotnet.references.assemblies"))

[<Fact>]
let ``set, get and unset round trip in the local file and keep other settings`` () =
    withDirs (fun project _ ->
        let file = Config.targetFile project false
        Assert.EndsWith(Path.Combine(".haya", "config.json"), file)
        Config.set file "crc.diagram" "systems" |> ok |> ignore
        Config.set file "dotnet.references.assemblies" "A.*,B.*" |> ok |> ignore
        Assert.Equal(Some "systems", Config.getFrom file "crc.diagram" |> ok)
        Assert.Equal(Some "A.*,B.*", Config.getFrom file "dotnet.references.assemblies" |> ok)
        Config.unset file "crc.diagram" |> ok |> ignore
        Assert.Equal(None, Config.getFrom file "crc.diagram" |> ok)
        Assert.Equal(Some "A.*,B.*", Config.getFrom file "dotnet.references.assemblies" |> ok)
        // the file is sectioned JSON
        Assert.Contains("\"dotnet\"", File.ReadAllText file))

[<Fact>]
let ``set refuses an unknown key or a bad value and writes nothing`` () =
    withDirs (fun project _ ->
        let file = Config.targetFile project false
        Assert.True(Config.set file "nope" "1" |> Result.isError)
        Assert.True(Config.set file "viewer.complexity.systems" "1,2" |> Result.isError)
        Assert.True(Config.set file "crc.diagram" "components" |> Result.isError)
        Assert.False(File.Exists file))

[<Fact>]
let ``The global file is written in the config directory`` () =
    withDirs (fun project globalDir ->
        let file = Config.targetFile project true
        Assert.Equal(Path.Combine(globalDir, "config.json").Replace('\\', '/'), file.Replace('\\', '/'))
        Config.set file "output.path" "out" |> ok |> ignore
        Assert.Equal(Some "out", Config.text (Config.load project |> ok) "output.path"))

[<Fact>]
let ``The explorer page carries the viewer settings only when set`` () =
    let graph = Graph.ofApps Detail.Apps []
    let plain = graph |> Encode.graph |> Serializer.toJson
    Assert.DoesNotContain("viewer", plain)
    let withViewer = graph |> Encode.graphWith { ViewerSettings.none with Detail = Some Detail.Apps; Links = Some [ 1; 2; 3; 4 ] } |> Serializer.toJson
    Assert.Contains("\"viewer\"", withViewer)
    Assert.Contains("\"detail\": \"apps\"", withViewer)
    Assert.Contains("\"links\"", withViewer)
