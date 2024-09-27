namespace Haya.Tool

open Argu
open Haya.Tool.Cli
module Program =

    [<EntryPoint>]
    let main argv =
        
        let parser = ArgumentParser.Create<CliArguments>(
            programName = "haya",
            helpTextMessage = "Haya is a tool used in combination with the Haya package attributes to generate data about your program and the systems it interacts with.")
        try
            let pr = parser.ParseCommandLine(inputs = argv, raiseOnUsage = false)
            let result = 
                match pr with
                | IsCrcCommand cmd ->
                    execCrcAsync cmd |> Async.AwaitTask |> Async.RunSynchronously
                | IsDescribeCommand cmd ->
                    execDescribeAsync cmd |> Async.AwaitTask |> Async.RunSynchronously
                | IsBackstageCommand cmd ->
                    execBackstageAsync cmd |> Async.AwaitTask |> Async.RunSynchronously
                | IsDiagramCommand cmd ->
                    execDiagramAsync cmd |> Async.AwaitTask |> Async.RunSynchronously
                | _ when pr.IsUsageRequested -> Ok (0, parser.PrintUsage())
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
