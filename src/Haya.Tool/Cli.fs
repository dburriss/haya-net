namespace Haya.Tool
open Argu
open Haya.Core

module CliText =
    let includeReferencesUsage = "Also read Haya attributes from public types in referenced packages/DLLs that reference Haya"
    let includeAssemblyUsage = "Also read referenced assemblies whose name matches this pattern (* and ? wildcards). Can be repeated"
    let verboseUsage = "Print the referenced assemblies and types that were included"

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
    | [<AltCommandLine("-t")>] DiagramType of DiagramType
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
            | DiagramType _ -> "Diagram type. c4system (system context), c4container (apps inside their systems) or c4component (the classes inside the apps) (default: c4system). Ignored by -f html"
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

type CliArguments =
    | [<CliPrefix(CliPrefix.None)>] Crc of ParseResults<CrcArgs>
    | [<CliPrefix(CliPrefix.None)>] Describe of ParseResults<DescribeArgs>
    | [<CliPrefix(CliPrefix.None)>] Backstage of ParseResults<BackstageArgs>
    | [<CliPrefix(CliPrefix.None)>] Datadog of ParseResults<DatadogArgs>
    | [<CliPrefix(CliPrefix.None)>] Diagram of ParseResults<DiagramArgs>
    interface IArgParserTemplate with
        member s.Usage =
            match s with
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
    
    /// How a solution is analysed beyond its own source: which compiled references to read, and whether to report them.
    type AnalysisOptions = { References: Roslyn.ReferenceOptions; Verbose: bool }

    let analysisOptions (includeReferences: bool) (assemblyGlobs: string list) (verbose: bool) =
        { References = { Include = includeReferences; AssemblyGlobs = assemblyGlobs }; Verbose = verbose }

    /// The apps of one input: a haya describe file (json/yaml) or an analysed solution (one app).
    let private loadOne (options: AnalysisOptions) (currentDirectory: string) (path: string) : Task<Result<App list, string>> = task {
        if not (IO.fileExists path) then return Error $"Input not found: {path}"
        elif Decode.isDescribeFile path then
            return Decode.apps path (IO.readAllText path)
        else
            let! sln = path |> Roslyn.openSolution
            let! analysis = sln |> Descriptors.getAnalysisWith options.References currentDirectory Descriptors.attributeNames
            if options.Verbose then
                for (assembly, typeName) in analysis.Referenced do
                    printfn "Including %s: %s" assembly typeName
            return Ok [ App.ofDescriptors analysis.Descriptors |> App.withLanguage Language.DotNet ]
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
            let sln = inputList (pr.GetResults(DescribeArgs.InputPath) |> List.concat) (fun m -> pr.Raise m)
            let format = pr.GetResult(DescribeArgs.Format, defaultValue = DataFormat.Json)
            //todo: check if output path is a file or a directory
            let outputPath: string = pr.TryGetResult(DescribeArgs.OutputPath) |> Option.defaultWith (fun () -> defaultFile format "haya")
            let options = analysisOptions (pr.Contains DescribeArgs.IncludeReferences) (pr.GetResults DescribeArgs.IncludeAssembly) (pr.Contains DescribeArgs.Verbose)
            Some ({ InputPaths = sln
                    OutputPath = outputPath
                    Format = format
                    CurrentDirectory = currentDir }, options)
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
            let sln = inputList (pr.GetResults(CrcArgs.InputPath) |> List.concat) (fun m -> pr.Raise m)
            let outputPath: string = pr.TryGetResult(CrcArgs.OutputPath) |> Option.defaultValue "./CRC.md"
            let includeL1Diagram = pr.GetResults(CrcArgs.C4) |> List.length > 0
            let includeL2Diagram = pr.GetResults(CrcArgs.C4) |> List.length > 1
            let options = analysisOptions (pr.Contains CrcArgs.IncludeReferences) (pr.GetResults CrcArgs.IncludeAssembly) (pr.Contains CrcArgs.Verbose)
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
            let sln = inputList (pr.GetResults(BackstageArgs.InputPath) |> List.concat) (fun m -> pr.Raise m)
            let format = pr.GetResult(BackstageArgs.Format, defaultValue = DataFormat.Yaml)
            let outputPath: string = pr.TryGetResult(BackstageArgs.OutputPath) |> Option.defaultWith (fun () -> defaultFile format "backstage")
            let options = analysisOptions (pr.Contains BackstageArgs.IncludeReferences) (pr.GetResults BackstageArgs.IncludeAssembly) (pr.Contains BackstageArgs.Verbose)
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
            let sln = inputList (pr.GetResults(DatadogArgs.InputPath) |> List.concat) (fun m -> pr.Raise m)
            let format = pr.GetResult(DatadogArgs.Format, defaultValue = DataFormat.Yaml)
            let outputPath: string = pr.TryGetResult(DatadogArgs.OutputPath) |> Option.defaultWith (fun () -> defaultFile format "entity.datadog")
            let options = analysisOptions (pr.Contains DatadogArgs.IncludeReferences) (pr.GetResults DatadogArgs.IncludeAssembly) (pr.Contains DatadogArgs.Verbose)
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
            let sln = inputList (pr.GetResults(DiagramArgs.InputPath) |> List.concat) (fun m -> pr.Raise m)
            let diagramType = pr.GetResult(DiagramArgs.DiagramType, defaultValue = DiagramType.C4System)
            let diagramFormat = pr.GetResult(DiagramArgs.DiagramFormat, defaultValue = DiagramFormat.Mermaid)
            let defaultOutput = match diagramFormat with | DiagramFormat.Html -> "./diagram.html" | DiagramFormat.Mermaid -> "./c4-system-context.mermaid"
            let outputPath: string = pr.TryGetResult(DiagramArgs.OutputPath) |> Option.defaultValue defaultOutput
            let options = analysisOptions (pr.Contains DiagramArgs.IncludeReferences) (pr.GetResults DiagramArgs.IncludeAssembly) (pr.Contains DiagramArgs.Verbose)
            Some ({ InputPaths = sln
                    OutputPath = outputPath
                    DiagramType = diagramType
                    DiagramFormat = diagramFormat
                    CurrentDirectory = currentDir }, options)
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
