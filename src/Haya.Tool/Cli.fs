namespace Haya.Tool
open Argu
open Haya.Core

module CliText =
    let includeReferencesUsage = "Also read Haya attributes from public types in referenced packages/DLLs that reference Haya"
    let includeAssemblyUsage = "Also read referenced assemblies whose name matches this pattern (* and ? wildcards). Can be repeated"
    let verboseUsage = "Print the referenced assemblies and types that were included, and at members the size of the type graph and the time spent on dependencies"
    let detailUsage = "Level of detail. systems (systems and how they connect), apps (the apps inside their systems), components (the classes inside the apps) or members (also each class's constructors, public members and dependencies). With -f html, components adds the classes to the explorer and members their constructors, members and dependencies; in the Mermaid diagram, members also draws the dependencies between classes (default: systems)"
    let describeDetailUsage = "Level of detail. apps (responsibilities, collaborators and meta), components (the same as apps today: classes are already named on each responsibility and collaborator) or members (adds each annotated class's constructors, public members and dependencies) (default: apps)"

/// The levels `describe` takes: it always holds the apps, so there is no `systems`.
type DescribeDetail =
    | Apps
    | Components
    | Members

type CrcArgs =
    | [<MainCommand; Last>] InputPath of input_path: string list
    | [<AltCommandLine("-o"); Unique>] OutputPath of output_path: string
    | [<AltCommandLine("-c")>] C4
    | [<CustomCommandLine("--include-references")>] IncludeReferences
    | [<CustomCommandLine("--include-assembly")>] IncludeAssembly of assembly_glob: string
    | [<AltCommandLine("-v")>] Verbose
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | IncludeReferences -> CliText.includeReferencesUsage
            | IncludeAssembly _ -> CliText.includeAssemblyUsage
            | Verbose -> CliText.verboseUsage
            | InputPath _ -> "Path(s) to a solution or haya describe file (json/yaml). Several can be given to build one system"
            | OutputPath _ -> "Path to output folder or file (default: ./CRC.md)"
            | C4 -> "Include diagram (markdown only). -c for C4 Level 1, -cc for C4 Level 2"

type DescribeArgs =
    | [<MainCommand; Last>] InputPath of input_path: string list
    | [<AltCommandLine("-o"); Unique>] OutputPath of output_path: string
    | [<AltCommandLine("-f")>] Format of format: DataFormat
    | [<AltCommandLine("-d")>] Detail of DescribeDetail
    | [<CustomCommandLine("--include-references")>] IncludeReferences
    | [<CustomCommandLine("--include-assembly")>] IncludeAssembly of assembly_glob: string
    | [<AltCommandLine("-v")>] Verbose
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | IncludeReferences -> CliText.includeReferencesUsage
            | IncludeAssembly _ -> CliText.includeAssemblyUsage
            | Verbose -> CliText.verboseUsage
            | Detail _ -> CliText.describeDetailUsage
            | InputPath _ -> "Path(s) to a solution or haya describe file (json/yaml). Several can be given to build one system"
            | OutputPath _ -> "Path to output folder or file (default: ./haya.json)"
            | Format _ -> "Output format. json | yaml (default: json)"
            
type BackstageArgs =
    | [<MainCommand; Last>] InputPath of input_path: string list
    | [<AltCommandLine("-o"); Unique>] OutputPath of output_path: string
    | [<AltCommandLine("-f")>] Format of format: DataFormat
    | [<CustomCommandLine("--include-references")>] IncludeReferences
    | [<CustomCommandLine("--include-assembly")>] IncludeAssembly of assembly_glob: string
    | [<AltCommandLine("-v")>] Verbose
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | IncludeReferences -> CliText.includeReferencesUsage
            | IncludeAssembly _ -> CliText.includeAssemblyUsage
            | Verbose -> CliText.verboseUsage
            | InputPath _ -> "Path(s) to a solution or haya describe file (json/yaml). Several can be given to build one system"
            | OutputPath _ -> "Path to output folder or file (default: ./backstage.yaml)"
            | Format _ -> "Output format. json | yaml (default: yaml)"

type DiagramArgs =
    | [<MainCommand; Last>] InputPath of input_path: string list
    | [<AltCommandLine("-o"); Unique>] OutputPath of output_path: string
    | [<AltCommandLine("-d")>] Detail of Detail
    | [<AltCommandLine("-f")>] DiagramFormat of DiagramFormat
    | [<CustomCommandLine("--include-references")>] IncludeReferences
    | [<CustomCommandLine("--include-assembly")>] IncludeAssembly of assembly_glob: string
    | [<AltCommandLine("-v")>] Verbose
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | IncludeReferences -> CliText.includeReferencesUsage
            | IncludeAssembly _ -> CliText.includeAssemblyUsage
            | Verbose -> CliText.verboseUsage
            | InputPath _ -> "Path(s) to a solution or haya describe file (json/yaml). Several can be given to build one system"
            | OutputPath _ -> "Path to output folder or file (default: ./c4-system-context.mermaid, or ./diagram.html for -f html)"
            | Detail _ -> CliText.detailUsage
            | DiagramFormat _ -> "Output format. mermaid | html (interactive explorer, a self-contained page) (default: mermaid)"

type DatadogArgs =
    | [<MainCommand; Last>] InputPath of input_path: string list
    | [<AltCommandLine("-o"); Unique>] OutputPath of output_path: string
    | [<AltCommandLine("-f")>] Format of format: DataFormat
    | [<CustomCommandLine("--include-references")>] IncludeReferences
    | [<CustomCommandLine("--include-assembly")>] IncludeAssembly of assembly_glob: string
    | [<AltCommandLine("-v")>] Verbose
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | IncludeReferences -> CliText.includeReferencesUsage
            | IncludeAssembly _ -> CliText.includeAssemblyUsage
            | Verbose -> CliText.verboseUsage
            | InputPath _ -> "Path(s) to a solution or haya describe file (json/yaml). Several can be given to build one system"
            | OutputPath _ -> "Path to output folder or file (default: ./entity.datadog.yaml)"
            | Format _ -> "Output format. json | yaml (default: yaml)"

type ConfigArgs =
    | [<MainCommand>] Words of words: string list
    | Local
    | Global
    | [<CustomCommandLine("--show-origin")>] ShowOrigin
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Words _ -> "list | get <key> | set <key> <value> | unset <key>"
            | Local -> "Read or write the nearest .haya/config.json (set and unset default to it; for a new file, the current directory)"
            | Global -> "Read or write the global file in the OS config directory (haya/config.json)"
            | ShowOrigin -> "With list: show which file or environment variable each value comes from"

type CliArguments =
    | [<CliPrefix(CliPrefix.None)>] Config of ParseResults<ConfigArgs>
    | [<CliPrefix(CliPrefix.None)>] Crc of ParseResults<CrcArgs>
    | [<CliPrefix(CliPrefix.None)>] Describe of ParseResults<DescribeArgs>
    | [<CliPrefix(CliPrefix.None)>] Backstage of ParseResults<BackstageArgs>
    | [<CliPrefix(CliPrefix.None)>] Datadog of ParseResults<DatadogArgs>
    | [<CliPrefix(CliPrefix.None)>] Diagram of ParseResults<DiagramArgs>
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Config _ -> "Get and set configuration (env vars, .haya/config.json, global config)"
            | Crc _ -> "Generate Components, Responsibilities, and Collaborator documentation"
            | Describe _ -> "Generate Haya solution description"
            | Backstage _ -> "Generate Backstage catalog data"
            | Datadog _ -> "Generate Datadog Software Catalog entities"
            | Diagram _ -> "Generate diagram files"

module Cli =
    open Haya.Core.Analysis
    open Haya.Roslyn
    open System
    open System.Threading.Tasks
    
    let defaultFile format filename =
        match format with
        | DataFormat.Json -> $"./{filename}.json"
        | DataFormat.Yaml -> $"./{filename}.yaml"
    
    let private dataFormatOf (name: string) = if name = "json" then DataFormat.Json else DataFormat.Yaml

    /// The configuration for commands run in `currentDir`; a broken file or variable stops the command with its message.
    let private loadConfig (currentDir: string) (fail: string -> Config.Resolved) : Config.Resolved =
        match Config.load currentDir with
        | Ok cfg ->
            for w in cfg.Warnings do eprintfn "Warning: %s" w
            cfg
        | Error m -> fail m

    /// Command line values beat configuration; the two lists are joined.
    let private referenceSettings (cfg: Config.Resolved) (includeFlag: bool) (globs: string list) =
        let include' = includeFlag || (Config.flag cfg "dotnet.references.include" = Some true)
        include', (globs @ Config.list cfg "dotnet.references.assemblies") |> List.distinct

    /// How a solution is analysed beyond its own source: which compiled references to read, whether to report them, and
    /// whether to collect the constructors and members of the annotated classes (`Detail.Members`).
    type AnalysisOptions = { References: Roslyn.ReferenceOptions; Verbose: bool; Members: bool }

    let analysisOptions (includeReferences: bool) (assemblyGlobs: string list) (verbose: bool) =
        { References = { Include = includeReferences; AssemblyGlobs = assemblyGlobs }; Verbose = verbose; Members = false }

    /// The apps of one input: a haya describe file (json/yaml) or an analysed solution (one app).
    let private loadOne (options: AnalysisOptions) (currentDirectory: string) (path: string) : Task<Result<App list, string>> = task {
        if not (IO.fileExists path) then return Error $"Input not found: {path}"
        elif Decode.isDescribeFile path then
            return Decode.apps path (IO.readAllText path)
        else
            let! sln = path |> Roslyn.openSolution
            let! analysis = sln |> Descriptors.getAnalysisWithMembers options.Members options.References currentDirectory Descriptors.attributeNames
            if options.Verbose then
                for (assembly, typeName) in analysis.Referenced do
                    printfn "Including %s: %s" assembly typeName
                match analysis.Dependencies with
                | Some d -> printfn "Dependencies: %d types, %d edges in %d ms" d.Types d.Edges d.Milliseconds
                | None -> ()
            return Ok [ App.ofDescriptors analysis.Descriptors |> App.withLanguage Language.DotNet |> App.withComponents analysis.Components ]
    }

    /// Loads every input and merges them into one system (warnings about conflicts go to stderr).
    let loadApps (options: AnalysisOptions) (currentDirectory: string) (paths: string list) : Task<Result<App list, string>> = task {
        let apps = ref []
        let error = ref None
        for path in paths do
            if error.Value.IsNone then
                match! loadOne options currentDirectory path with
                | Ok loaded -> apps.Value <- apps.Value @ loaded
                | Error e -> error.Value <- Some e
        match error.Value with
        | Some e -> return Error e
        | None ->
            let merged, warnings = Merge.apps apps.Value
            for w in warnings do eprintfn "Warning: %s" w
            return Ok merged
    }

    let private inputList (inputs: string list) (raise': string -> list<string>) =
        if List.isEmpty inputs then raise' "At least one input path is required" else inputs

    let (|IsDescribeCommand|_|) (parserResults: ParseResults<CliArguments>) : (DescribeCommand * AnalysisOptions) option =
        if parserResults.Contains(CliArguments.Describe) then
            let currentDir = Environment.CurrentDirectory
            let pr = parserResults.GetResult(CliArguments.Describe)
            let cfg = loadConfig currentDir pr.Raise
            let sln = inputList (pr.GetResults(DescribeArgs.InputPath) |> List.concat) (fun m -> pr.Raise m)
            let format =
                pr.TryGetResult(DescribeArgs.Format)
                |> Option.orElse (Config.format cfg "describe" [ "json"; "yaml" ] |> Option.map dataFormatOf)
                |> Option.defaultValue DataFormat.Json
            //todo: check if output path is a file or a directory
            let outputPath: string =
                pr.TryGetResult(DescribeArgs.OutputPath)
                |> Option.defaultWith (fun () -> Config.outputPath cfg "describe" (defaultFile format "haya"))
            let detail =
                match pr.TryGetResult(DescribeArgs.Detail) with
                | Some DescribeDetail.Apps -> Detail.Apps
                | Some DescribeDetail.Components -> Detail.Components
                | Some DescribeDetail.Members -> Detail.Members
                | None -> Config.detail cfg "describe.detail" |> Option.defaultValue Detail.Apps
            let includeRefs, globs = referenceSettings cfg (pr.Contains DescribeArgs.IncludeReferences) (pr.GetResults DescribeArgs.IncludeAssembly)
            let options = analysisOptions includeRefs globs (pr.Contains DescribeArgs.Verbose)
            Some ({ InputPaths = sln
                    OutputPath = outputPath
                    Format = format
                    Detail = detail
                    CurrentDirectory = currentDir }, { options with Members = detail = Detail.Members })
        else None
           
    let execDescribeAsync (cmd: DescribeCommand, options: AnalysisOptions) = task {
        let pathToSln = String.concat ", " cmd.InputPaths
        match! loadApps options cmd.CurrentDirectory cmd.InputPaths with
        | Error e -> return Error(1, e)
        | Ok apps ->
            return
                Describe.serialize cmd apps
                |> IO.write cmd.OutputPath |> function
                    | Ok _ -> Ok(0, $"Describe successfully generated from {pathToSln}")
                    | Error e -> Error(1, e)
    }
    
    let (|IsCrcCommand|_|) (parserResults: ParseResults<CliArguments>) : (CrcCommand * AnalysisOptions) option =
        if parserResults.Contains(CliArguments.Crc) then
            let currentDir = Environment.CurrentDirectory
            let pr = parserResults.GetResult(CliArguments.Crc)
            let cfg = loadConfig currentDir pr.Raise
            let sln = inputList (pr.GetResults(CrcArgs.InputPath) |> List.concat) (fun m -> pr.Raise m)
            let outputPath: string =
                pr.TryGetResult(CrcArgs.OutputPath) |> Option.defaultWith (fun () -> Config.outputPath cfg "crc" "./CRC.md")
            let c4 = pr.GetResults(CrcArgs.C4) |> List.length
            // -c on the command line decides; without it crc.diagram does
            let includeL1Diagram, includeL2Diagram =
                if c4 > 0 then true, c4 > 1 else Config.crcDiagrams cfg |> Option.defaultValue (false, false)
            let includeRefs, globs = referenceSettings cfg (pr.Contains CrcArgs.IncludeReferences) (pr.GetResults CrcArgs.IncludeAssembly)
            let options = analysisOptions includeRefs globs (pr.Contains CrcArgs.Verbose)
            Some ({ InputPaths = sln; OutputPath = outputPath
                    IncludeL1Diagram = includeL1Diagram
                    IncludeL2Diagram = includeL2Diagram
                    CurrentDirectory = currentDir }, options)
        else None

    let execCrcAsync (cmd: CrcCommand, options: AnalysisOptions) = task {
        let pathToSln = String.concat ", " cmd.InputPaths
        match! loadApps options cmd.CurrentDirectory cmd.InputPaths with
        | Error e -> return Error(1, e)
        | Ok apps ->
            return
                Crc.sprintMarkdown cmd apps
                |> IO.write cmd.OutputPath |> function
                | Ok _ -> Ok(0, $"CRC successfully generated from {pathToSln}")
                | Error e -> Error(1, e)
    }
    
    let (|IsBackstageCommand|_|) (parserResults: ParseResults<CliArguments>) : (BackstageCommand * AnalysisOptions) option =
        if parserResults.Contains(CliArguments.Backstage) then
            let currentDir = Environment.CurrentDirectory
            let pr = parserResults.GetResult(CliArguments.Backstage)
            let cfg = loadConfig currentDir pr.Raise
            let sln = inputList (pr.GetResults(BackstageArgs.InputPath) |> List.concat) (fun m -> pr.Raise m)
            let format =
                pr.TryGetResult(BackstageArgs.Format)
                |> Option.orElse (Config.format cfg "backstage" [ "json"; "yaml" ] |> Option.map dataFormatOf)
                |> Option.defaultValue DataFormat.Yaml
            let outputPath: string =
                pr.TryGetResult(BackstageArgs.OutputPath)
                |> Option.defaultWith (fun () -> Config.outputPath cfg "backstage" (defaultFile format "backstage"))
            let includeRefs, globs = referenceSettings cfg (pr.Contains BackstageArgs.IncludeReferences) (pr.GetResults BackstageArgs.IncludeAssembly)
            let options = analysisOptions includeRefs globs (pr.Contains BackstageArgs.Verbose)
            Some ({ InputPaths = sln
                    OutputPath = outputPath
                    Format = format
                    CurrentDirectory = currentDir }, options)
        else None
        
    let execBackstageAsync (cmd: BackstageCommand, options: AnalysisOptions) = task {
        let pathToSln = String.concat ", " cmd.InputPaths
        match! loadApps options cmd.CurrentDirectory cmd.InputPaths with
        | Error e -> return Error(1, e)
        | Ok apps ->
            return
                Backstage.generateCatalog cmd apps
                |> IO.write cmd.OutputPath
                |> function
                | Ok _ -> Ok(0, $"Backstage successfully generated from {pathToSln}")
                | Error e -> Error(1, e)
    }
    
    let (|IsDatadogCommand|_|) (parserResults: ParseResults<CliArguments>) : (DatadogCommand * AnalysisOptions) option =
        if parserResults.Contains(CliArguments.Datadog) then
            let currentDir = Environment.CurrentDirectory
            let pr = parserResults.GetResult(CliArguments.Datadog)
            let cfg = loadConfig currentDir pr.Raise
            let sln = inputList (pr.GetResults(DatadogArgs.InputPath) |> List.concat) (fun m -> pr.Raise m)
            let format =
                pr.TryGetResult(DatadogArgs.Format)
                |> Option.orElse (Config.format cfg "datadog" [ "json"; "yaml" ] |> Option.map dataFormatOf)
                |> Option.defaultValue DataFormat.Yaml
            let outputPath: string =
                pr.TryGetResult(DatadogArgs.OutputPath)
                |> Option.defaultWith (fun () -> Config.outputPath cfg "datadog" (defaultFile format "entity.datadog"))
            let includeRefs, globs = referenceSettings cfg (pr.Contains DatadogArgs.IncludeReferences) (pr.GetResults DatadogArgs.IncludeAssembly)
            let options = analysisOptions includeRefs globs (pr.Contains DatadogArgs.Verbose)
            Some ({ InputPaths = sln
                    OutputPath = outputPath
                    Format = format
                    CurrentDirectory = currentDir }, options)
        else None

    let execDatadogAsync (cmd: DatadogCommand, options: AnalysisOptions) = task {
        let pathToSln = String.concat ", " cmd.InputPaths
        match! loadApps options cmd.CurrentDirectory cmd.InputPaths with
        | Error e -> return Error(1, e)
        | Ok apps ->
            return
                Datadog.generateCatalog cmd apps
                |> IO.write cmd.OutputPath
                |> function
                | Ok _ -> Ok(0, $"Datadog successfully generated from {pathToSln}")
                | Error e -> Error(1, e)
    }

    let (|IsDiagramCommand|_|) (parserResults: ParseResults<CliArguments>) : (DiagramCommand * AnalysisOptions) option =
        if parserResults.Contains(CliArguments.Diagram) then
            let currentDir = Environment.CurrentDirectory
            let pr = parserResults.GetResult(CliArguments.Diagram)
            let cfg = loadConfig currentDir pr.Raise
            let sln = inputList (pr.GetResults(DiagramArgs.InputPath) |> List.concat) (fun m -> pr.Raise m)
            let detail =
                pr.TryGetResult(DiagramArgs.Detail)
                |> Option.orElse (Config.detail cfg "diagram.detail")
                |> Option.defaultValue Detail.Systems
            let diagramFormat =
                pr.TryGetResult(DiagramArgs.DiagramFormat)
                |> Option.orElse (Config.format cfg "diagram" [ "mermaid"; "html" ] |> Option.map (fun f -> if f = "html" then DiagramFormat.Html else DiagramFormat.Mermaid))
                |> Option.defaultValue DiagramFormat.Mermaid
            let defaultOutput = match diagramFormat with | DiagramFormat.Html -> "./diagram.html" | DiagramFormat.Mermaid -> "./c4-system-context.mermaid"
            let outputPath: string =
                pr.TryGetResult(DiagramArgs.OutputPath) |> Option.defaultWith (fun () -> Config.outputPath cfg "diagram" defaultOutput)
            let includeRefs, globs = referenceSettings cfg (pr.Contains DiagramArgs.IncludeReferences) (pr.GetResults DiagramArgs.IncludeAssembly)
            let options = analysisOptions includeRefs globs (pr.Contains DiagramArgs.Verbose)
            Some ({ InputPaths = sln
                    OutputPath = outputPath
                    Detail = detail
                    DiagramFormat = diagramFormat
                    Viewer = Config.viewer cfg
                    CurrentDirectory = currentDir }, { options with Members = detail = Detail.Members })
        else None
        
    let execDiagramAsync (cmd: DiagramCommand, options: AnalysisOptions) = task {
        let pathToSln = String.concat ", " cmd.InputPaths
        match! loadApps options cmd.CurrentDirectory cmd.InputPaths with
        | Error e -> return Error(1, e)
        | Ok apps ->
            return
                Diagram.generateDiagram cmd apps
                |> IO.write cmd.OutputPath
                |> function
                | Ok _ -> Ok(0, $"Diagram successfully generated from {pathToSln}")
                | Error e -> Error(1, e)    
    }

    /// `haya config list | get | set | unset`. Needs no analysis, so it has its own entry rather than an active pattern with options.
    let (|IsConfigCommand|_|) (parserResults: ParseResults<CliArguments>) : Result<int * string, int * string> option =
        if parserResults.Contains(CliArguments.Config) then
            let currentDir = Environment.CurrentDirectory
            let pr = parserResults.GetResult(CliArguments.Config)
            let words = pr.GetResults(ConfigArgs.Words) |> List.concat
            let isGlobal = pr.Contains ConfigArgs.Global
            let isLocal = pr.Contains ConfigArgs.Local
            if isGlobal && isLocal then pr.Raise "Use either --local or --global, not both"
            let target = Config.targetFile currentDir isGlobal
            let ofResult (r: Result<string, string>) (message: string -> string) =
                match r with
                | Ok file -> Ok (0, message file)
                | Error m -> Error (1, m)
            let loaded () = Config.load currentDir
            let result =
                match words with
                | [ "list" ] ->
                    loaded () |> Result.map (fun cfg -> 0, String.concat "\n" (Config.lines cfg (pr.Contains ConfigArgs.ShowOrigin)))
                    |> Result.mapError (fun m -> 1, m)
                | [ "get"; key ] ->
                    let value = if isGlobal || isLocal then Config.getFrom target key else loaded () |> Result.bind (fun cfg -> Config.get cfg key)
                    match value with
                    | Ok (Some v) -> Ok (0, v)
                    | Ok None -> Error (1, $"{key} is not set")
                    | Error m -> Error (1, m)
                | [ "set"; key; value ] -> ofResult (Config.set target key value) (fun f -> $"{key} = {value} ({f})")
                | [ "unset"; key ] -> ofResult (Config.unset target key) (fun f -> $"Removed {key} ({f})")
                | _ -> pr.Raise "Usage: haya config list | get <key> | set <key> <value> | unset <key>"
            Some result
        else None
