namespace Haya.Tool

open Argu
open Haya.Tool.Cli
module Program =

    [<EntryPoint>]
    let main argv =
        
        let parser = ArgumentParser.Create<CliArguments>(programName = "haya")
        try
            let pr = parser.ParseCommandLine(inputs = argv, raiseOnUsage = false)
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
