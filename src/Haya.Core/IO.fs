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
    [<Import("mkdirSync", "node:fs")>]
    let mkdirSync (path: string, options: obj) : unit = jsNative
    [<Import("rmSync", "node:fs")>]
    let rmSync (path: string) : unit = jsNative
    [<Import("statSync", "node:fs")>]
    let statSync (path: string) : obj = jsNative
    [<Import("homedir", "node:os")>]
    let homedir () : string = jsNative
    [<Emit("process.env[$0]")>]
    let env (name: string) : string option = jsNative
    [<Emit("process.platform")>]
    let platform () : string = jsNative
#endif

module IO =
    #if !FABLE_COMPILER
    open System.IO
    #endif

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

    /// An environment variable, or None when it is unset or empty.
    let getEnv (name: string) : string option =
        #if FABLE_COMPILER
        match NodeFs.env name with
        | Some "" | None -> None
        | v -> v
        #else
        match System.Environment.GetEnvironmentVariable name with
        | null | "" -> None
        | v -> Some v
        #endif

    let homeDir () : string =
        #if FABLE_COMPILER
        NodeFs.homedir ()
        #else
        System.Environment.GetFolderPath System.Environment.SpecialFolder.UserProfile
        #endif

    /// Joins with `/`, which every supported OS (Windows included) accepts.
    let combine (a: string) (b: string) =
        if a = "" then b
        elif b.StartsWith "/" || (b.Length > 1 && b.[1] = ':') then b
        else a.TrimEnd('/', '\\') + "/" + b

    /// The parent directory, or None at a root.
    let parent (path: string) : string option =
        let p = path.TrimEnd('/', '\\')
        let i = max (p.LastIndexOf '/') (p.LastIndexOf '\\')
        if i <= 0 then (if i = 0 && p.Length > 1 then Some "/" else None) else Some (p.Substring(0, i))

    let directoryExists (dir: string) =
        #if FABLE_COMPILER
        NodeFs.existsSync(dir)
        #else
        Directory.Exists(dir)
        #endif

    let createDirectory (dir: string) =
        #if FABLE_COMPILER
        NodeFs.mkdirSync(dir, {| recursive = true |})
        #else
        Directory.CreateDirectory dir |> ignore
        #endif

    let write (file: string) (data: string) =
        // the directory may come from configuration (`output.path`) and not exist yet
        match parent file with
        | Some dir when dir <> "" && not (directoryExists dir) -> createDirectory dir
        | _ -> ()
        #if FABLE_COMPILER
        NodeFs.writeFileSync(file, data)
        #else
        File.WriteAllText(file, data)
        #endif
        Ok(file)

    let deleteFile (file: string) =
        #if FABLE_COMPILER
        if NodeFs.existsSync file then NodeFs.rmSync file
        #else
        if File.Exists file then File.Delete file
        #endif

    /// The per-user configuration directory of the OS: `~/Library/Application Support` on macOS, `%APPDATA%` on Windows,
    /// `$XDG_CONFIG_HOME` or `~/.config` elsewhere.
    let configDir () : string =
        #if FABLE_COMPILER
        match NodeFs.platform () with
        | "darwin" -> combine (homeDir ()) "Library/Application Support"
        | "win32" -> (getEnv "APPDATA") |> Option.defaultWith (fun () -> combine (homeDir ()) "AppData/Roaming")
        | _ -> getEnv "XDG_CONFIG_HOME" |> Option.defaultWith (fun () -> combine (homeDir ()) ".config")
        #else
        if System.OperatingSystem.IsMacOS() then combine (homeDir ()) "Library/Application Support"
        elif System.OperatingSystem.IsWindows() then
            getEnv "APPDATA" |> Option.defaultWith (fun () -> combine (homeDir ()) "AppData/Roaming")
        else getEnv "XDG_CONFIG_HOME" |> Option.defaultWith (fun () -> combine (homeDir ()) ".config")
        #endif
