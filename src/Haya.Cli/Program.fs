namespace Haya

open System
open Haya.Core
open Haya.Core.Analysis

module Program =
    
    let execAsync pathToSln = task {
        if IO.File.Exists(pathToSln) then
            let! sln = pathToSln |> Roslyn.openSolution
            let! descriptors =
                sln |> Describe.getDescriptors Describe.attributeNames
            // printfn "Descriptors %A" descriptors
            MdFormatter.printCrc descriptors
            printfn "Done."
            return 0
        else
            printfn "Solution not found at %s" pathToSln
            return 1
        
    }
    
    [<EntryPoint>]
    let main argv =
        let pathToSln = argv.[0]
        execAsync pathToSln |> Async.AwaitTask |> Async.RunSynchronously

        
