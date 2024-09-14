namespace Haya

open System
open Argu
open Haya.Core
open Haya.Core.Analysis

module Program =
    type CrcArgs =
        | [<MainCommand; ExactlyOnce; Last>] PathToSln of path_to_sln: string
        | [<AltCommandLine("-o"); Unique>] OutputPath of output_path: string
        | [<AltCommandLine("-f"); Unique>]Format of CrcFormat
        | [<AltCommandLine("-c")>] C4
        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | PathToSln _ -> "Path to solution file"
                | OutputPath _ -> "Path to output folder or file (default: ./CRC.md)"
                | Format _ -> "Output format: md | json (default: md)"
                | C4 -> "Include diagram (markdown only). -c for C4 Level 1, -cc for C4 Level 2"
    
    type CliArguments =
        | [<CliPrefix(CliPrefix.None)>] Crc of ParseResults<CrcArgs>
        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | Crc _ -> "Generate Components, Responsibilities, and Collaborator data"

    
    let (|IsCrcCommand|_|) (parserResults: ParseResults<CliArguments>) =
        if parserResults.Contains(CliArguments.Crc) then
            let pr = parserResults.GetResult(CliArguments.Crc)
            let sln = pr.GetResult(CrcArgs.PathToSln)
            let outputPath: string = pr.TryGetResult(CrcArgs.OutputPath) |> Option.defaultValue "./CRC.md"
            let format = pr.TryGetResult(CrcArgs.Format) |> Option.defaultValue (CrcFormat.Md)
            let includeL1Diagram = pr.GetResults(CrcArgs.C4) |> List.length > 0
            let includeL2Diagram = pr.GetResults(CrcArgs.C4) |> List.length > 1
            Some { PathToSln = sln; OutputPath = outputPath; Format = format; IncludeL1Diagram = includeL1Diagram; IncludeL2Diagram = includeL2Diagram }
        else None
    
    let execCrcAsync cmd = task {
        let pathToSln = cmd.PathToSln
        if IO.File.Exists(pathToSln) then
            let! sln = pathToSln |> Roslyn.openSolution
            let! descriptors =
                sln |> Describe.getDescriptors Describe.attributeNames
            MdFormatter.sprintCrc cmd descriptors
            |> MdFormatter.write cmd.OutputPath
            return Ok(0, $"CRC successfully generated from {pathToSln}")
        else
            return Error(1, "Solution not found")
        
    }
    [<EntryPoint>]
    let main argv =
        
        let parser = ArgumentParser.Create<CliArguments>(programName = "haya")
        try
            let pr = parser.ParseCommandLine(inputs = argv, raiseOnUsage = true)
            let result = 
                match pr with
                | IsCrcCommand cmd ->
                    execCrcAsync cmd |> Async.AwaitTask |> Async.RunSynchronously
                | _ -> Error (1, "Invalid command")
            match result with
            | Ok (code, message) ->
                printfn "%s" message
                code
            | Error(code, msg) ->
                eprintfn "Error: %s" msg
                eprintfn "%s" (parser.PrintUsage())
                code
                
        with :? ArguParseException as e ->
            eprintfn "%s" e.Message
            eprintfn "%s" (parser.PrintUsage())
            1
