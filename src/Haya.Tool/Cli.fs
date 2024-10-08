namespace Haya.Tool
open Argu
open Haya.Core

type CrcArgs =
    | [<MainCommand; ExactlyOnce; Last>] InputPath of input_path: string
    | [<AltCommandLine("-o"); Unique>] OutputPath of output_path: string
    | [<AltCommandLine("-c")>] C4
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | InputPath _ -> "Path to solution or haya describe file"
            | OutputPath _ -> "Path to output folder or file (default: ./CRC.md)"
            | C4 -> "Include diagram (markdown only). -c for C4 Level 1, -cc for C4 Level 2"

type DescribeArgs =
    | [<MainCommand; ExactlyOnce; Last>] InputPath of input_path: string
    | [<AltCommandLine("-o"); Unique>] OutputPath of output_path: string
    | [<AltCommandLine("-f")>] Format of format: DataFormat
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | InputPath _ -> "Path to solution or haya describe file"
            | OutputPath _ -> "Path to output folder or file (default: ./haya.json)"
            | Format _ -> "Output format. json | yaml (default: json)"
            
type BackstageArgs =
    | [<MainCommand; ExactlyOnce; Last>] InputPath of input_path: string
    | [<AltCommandLine("-o"); Unique>] OutputPath of output_path: string
    | [<AltCommandLine("-f")>] Format of format: DataFormat
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | InputPath _ -> "Path to solution or haya describe file"
            | OutputPath _ -> "Path to output folder or file (default: ./haya.yaml)"
            | Format _ -> "Output format. json | yaml (default: yaml)"

type DiagramArgs =
    | [<MainCommand; ExactlyOnce; Last>] InputPath of input_path: string
    | [<AltCommandLine("-o"); Unique>] OutputPath of output_path: string
    | [<AltCommandLine("-t")>] DiagramType of DiagramType
    | [<AltCommandLine("-f")>] DiagramFormat of DiagramFormat
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | InputPath _ -> "Path to solution or haya describe file"
            | OutputPath _ -> "Path to output folder or file (default: ./c4-system-context.mermaid)"
            | DiagramType _ -> "Output format. c4system (default: c4system)"
            | DiagramFormat _ -> "Output format. mermaid (default: mermaid)"

type CliArguments =
    | [<CliPrefix(CliPrefix.None)>] Crc of ParseResults<CrcArgs>
    | [<CliPrefix(CliPrefix.None)>] Describe of ParseResults<DescribeArgs>
    | [<CliPrefix(CliPrefix.None)>] Backstage of ParseResults<BackstageArgs>
    | [<CliPrefix(CliPrefix.None)>] Diagram of ParseResults<DiagramArgs>
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Crc _ -> "Generate Components, Responsibilities, and Collaborator documentation"
            | Describe _ -> "Generate Haya solution description"
            | Backstage _ -> "Generate Backstage catalog data"
            | Diagram _ -> "Generate diagram files"

module Cli =
    open Haya.Core.Analysis
    open System
    
    let defaultFile format filename =
        match format with
        | DataFormat.Json -> $"./{filename}.json"
        | DataFormat.Yaml -> $"./{filename}.yaml"
    
    let (|IsDescribeCommand|_|) (parserResults: ParseResults<CliArguments>) : DescribeCommand option =
        if parserResults.Contains(CliArguments.Describe) then
            let currentDir = Environment.CurrentDirectory
            let pr = parserResults.GetResult(CliArguments.Describe)
            let sln = pr.GetResult(DescribeArgs.InputPath)
            let format = pr.GetResult(DescribeArgs.Format, defaultValue = DataFormat.Json)
            //todo: check if output path is a file or a directory
            let outputPath: string = pr.TryGetResult(DescribeArgs.OutputPath) |> Option.defaultWith (fun () -> defaultFile format "haya")
            Some { PathToSln = sln
                   OutputPath = outputPath
                   Format = format
                   CurrentDirectory = currentDir }
        else None
           
    let execDescribeAsync (cmd: DescribeCommand) = task {
        let pathToSln = cmd.PathToSln
        //todo: check if pathToSln is a sln or a describe file (json/yaml)
        if IO.File.Exists(pathToSln) then
            let! sln = pathToSln |> Roslyn.openSolution
            let! descriptors =
                sln |> Descriptor.getDescriptors cmd.CurrentDirectory Descriptor.attributeNames
            
            return
                Describe.serialize cmd descriptors
                |> IO.write cmd.OutputPath
                |> function
                    | Ok _ -> Ok(0, $"Describe successfully generated from {pathToSln}")
                    | Error e -> Error(1, e)
        else
            return Error(1, "Solution not found")
    }
    
    let (|IsCrcCommand|_|) (parserResults: ParseResults<CliArguments>) =
        if parserResults.Contains(CliArguments.Crc) then
            let currentDir = Environment.CurrentDirectory
            let pr = parserResults.GetResult(CliArguments.Crc)
            let sln = pr.GetResult(CrcArgs.InputPath)
            let outputPath: string = pr.TryGetResult(CrcArgs.OutputPath) |> Option.defaultValue "./CRC.md"
            let includeL1Diagram = pr.GetResults(CrcArgs.C4) |> List.length > 0
            let includeL2Diagram = pr.GetResults(CrcArgs.C4) |> List.length > 1
            Some { PathToSln = sln; OutputPath = outputPath
                   IncludeL1Diagram = includeL1Diagram
                   IncludeL2Diagram = includeL2Diagram
                   CurrentDirectory = currentDir }
        else None

    let execCrcAsync (cmd: CrcCommand) = task {
        let pathToSln = cmd.PathToSln
        // todo: check if pathToSln is a sln or a describe file (json/yaml)
        if IO.File.Exists(pathToSln) then
            let! sln = pathToSln |> Roslyn.openSolution
            let! descriptors =
                sln |> Descriptor.getDescriptors cmd.CurrentDirectory Descriptor.attributeNames
            return
                Crc.sprintMarkdown cmd descriptors
                |> IO.write cmd.OutputPath
                |> function
                | Ok _ -> Ok(0, $"CRC successfully generated from {pathToSln}")
                | Error e -> Error(1, e)
        else
            return Error(1, "Solution not found")
    }
    
    let (|IsBackstageCommand|_|) (parserResults: ParseResults<CliArguments>) : BackstageCommand option =
        if parserResults.Contains(CliArguments.Backstage) then
            let currentDir = Environment.CurrentDirectory
            let pr = parserResults.GetResult(CliArguments.Backstage)
            let sln = pr.GetResult(BackstageArgs.InputPath)
            let format = pr.GetResult(BackstageArgs.Format, defaultValue = DataFormat.Yaml)
            let outputPath: string = pr.TryGetResult(BackstageArgs.OutputPath) |> Option.defaultWith (fun () -> defaultFile format "backstage")
            Some { PathToSln = sln
                   OutputPath = outputPath
                   Format = format
                   CurrentDirectory = currentDir }
        else None
        
    let execBackstageAsync (cmd: BackstageCommand) = task {
        let pathToSln = cmd.PathToSln
        if IO.fileExists pathToSln then
            let! sln = pathToSln |> Roslyn.openSolution
            let! descriptors =
                sln |> Descriptor.getDescriptors cmd.CurrentDirectory Descriptor.attributeNames
            return
                Backstage.generateCatalog cmd descriptors
                |> IO.write cmd.OutputPath
                |> function
                | Ok _ -> Ok(0, $"Backstage successfully generated from {pathToSln}")
                | Error e -> Error(1, e)
        else
            return Error(1, "Solution not found")
    }
    
    let (|IsDiagramCommand|_|) (parserResults: ParseResults<CliArguments>) : DiagramCommand option =
        if parserResults.Contains(CliArguments.Diagram) then
            let currentDir = Environment.CurrentDirectory
            let pr = parserResults.GetResult(CliArguments.Diagram)
            let sln = pr.GetResult(DiagramArgs.InputPath)
            let outputPath: string = pr.TryGetResult(DiagramArgs.OutputPath) |> Option.defaultValue "./c4-system-context.mermaid"
            let diagramType = pr.GetResult(DiagramArgs.DiagramType, defaultValue = DiagramType.C4System)
            let diagramFormat = pr.GetResult(DiagramArgs.DiagramFormat, defaultValue = DiagramFormat.Mermaid)
            Some { PathToSln = sln
                   OutputPath = outputPath
                   DiagramType = diagramType
                   DiagramFormat = diagramFormat
                   CurrentDirectory = currentDir }
        else None
        
    let execDiagramAsync (cmd: DiagramCommand) = task {
        let pathToSln = cmd.PathToSln
        if IO.fileExists pathToSln then
            let! sln = pathToSln |> Roslyn.openSolution
            let! descriptors =
                sln |> Descriptor.getDescriptors cmd.CurrentDirectory Descriptor.attributeNames
            return
                Diagram.generateDiagram cmd descriptors
                |> IO.write cmd.OutputPath
                |> function
                | Ok _ -> Ok(0, $"Diagram successfully generated from {pathToSln}")
                | Error e -> Error(1, e)
        else
            return Error(1, "Solution not found")    
    }
