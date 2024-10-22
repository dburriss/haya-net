namespace Haya.Core

module IO =
    #if FABLE_COMPILER
    open Fable.Deno
    #else
    open System.IO
    #endif
    let write file data =
        // TODO: check file extension
        
        #if FABLE_COMPILER
        Deno.writeTextFileSync(file, data)
        #else
        File.WriteAllText(file, data)
        #endif
        Ok(file)
        
    let fileExists file =
        
        #if FABLE_COMPILER
        Deno.statSync(file).isFile
        #else
        File.Exists(file)
        #endif
