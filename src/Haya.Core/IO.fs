namespace Haya.Core

#if FABLE_COMPILER
module private NodeFs =
    open Fable.Core
    // Bun implements the node:fs API, so plain Node bindings work under Bun.
    [<Import("writeFileSync", "node:fs")>]
    let writeFileSync (path: string, data: string) : unit = jsNative
    [<Import("readFileSync", "node:fs")>]
    let readFileSync (path: string, encoding: string) : string = jsNative
    [<Import("existsSync", "node:fs")>]
    let existsSync (path: string) : bool = jsNative
#endif

module IO =
    #if !FABLE_COMPILER
    open System.IO
    #endif

    let write (file: string) (data: string) =
        // TODO: check file extension
        #if FABLE_COMPILER
        NodeFs.writeFileSync(file, data)
        #else
        File.WriteAllText(file, data)
        #endif
        Ok(file)

    let fileExists (file: string) =
        #if FABLE_COMPILER
        NodeFs.existsSync(file)
        #else
        File.Exists(file)
        #endif

    let readAllText (file: string) =
        #if FABLE_COMPILER
        NodeFs.readFileSync(file, "utf-8")
        #else
        File.ReadAllText(file)
        #endif
