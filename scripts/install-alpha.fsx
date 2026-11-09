#!/usr/bin/env -S dotnet fsi

// =================================================================================
// Install-Alpha Script
// =================================================================================
// Packs the current source as a dotnet tool with a patch-bumped alpha version and
// installs it as a global dotnet tool, useful for dog-fooding in-progress work.
//
// Version logic: reads <Version> from the tool .fsproj, strips any existing suffix,
// increments the patch segment, and appends "-alpha"  (e.g. 0.7.1 -> 0.7.2-alpha).
//
// Arguments:
//   --fsproj <path>       Relative path from repo root to the tool .fsproj (required).
//   --solution <path>     Relative path from repo root to the solution file (required).
//   --package-id <id>     The tool's NuGet package id, e.g. Acme.Tool (required).
//   --dry-run             Print what would happen without building or installing.
//
// Usage:
//   ./install-alpha.sh
//   ./install-alpha.sh --dry-run
// =================================================================================

open System
open System.IO
open System.Diagnostics
open System.Xml.Linq

let runProcess executable (arguments: string list) workingDirectory =
    let psi = ProcessStartInfo(executable)
    for arg in arguments do psi.ArgumentList.Add(arg)
    psi.RedirectStandardOutput <- true
    psi.RedirectStandardError <- true
    psi.UseShellExecute <- false
    psi.WorkingDirectory <- workingDirectory

    use p = Process.Start(psi)
    let output = p.StandardOutput.ReadToEnd()
    let error = p.StandardError.ReadToEnd()
    p.WaitForExit()

    if p.ExitCode <> 0 then
        failwithf "%s %s failed with exit code %d\nstdout:\n%s\nstderr:\n%s"
            executable (arguments |> String.concat " ") p.ExitCode output error

    output.Trim()

let tryRunProcess executable (arguments: string list) workingDirectory =
    try runProcess executable arguments workingDirectory |> ignore; true
    with ex ->
        printfn "  (ignored: %s)" ex.Message
        false

let args = fsi.CommandLineArgs |> Array.skip 1
let isDryRun = args |> Array.contains "--dry-run"

let getArg name =
    args
    |> Array.tryFindIndex ((=) name)
    |> Option.bind (fun i -> args |> Array.tryItem (i + 1))
    |> Option.defaultWith (fun () -> failwithf "Missing required argument: %s" name)

if isDryRun then
    printfn "=== DRY RUN MODE ==="
    printfn "No changes will be made."
    printfn "===================="

let rootDir = __SOURCE_DIRECTORY__ |> Directory.GetParent
let rootPath = rootDir.FullName
let fsprojRelative = getArg "--fsproj"
let fsprojPath = Path.Combine(rootPath, fsprojRelative)
let slnPath = Path.Combine(rootPath, getArg "--solution")
let packageId = getArg "--package-id"
// dotnet tool commands treat package ids case-insensitively and list them in lowercase
let toolId = packageId.ToLowerInvariant()

if not (File.Exists fsprojPath) then failwithf "Project file not found: %s" fsprojPath
if not (File.Exists slnPath) then failwithf "Solution file not found: %s" slnPath

let projectDoc = XDocument.Load(fsprojPath)
let projectRoot =
    match projectDoc.Root with
    | null -> failwith "Project XML is empty"
    | r -> r
let ns = projectRoot.Name.Namespace
let propertyGroup =
    projectRoot.Elements(ns + "PropertyGroup")
    |> Seq.tryHead
    |> Option.defaultWith (fun () -> failwith "No <PropertyGroup> found in fsproj")
let versionElement =
    match propertyGroup.Element(ns + "Version") with
    | null -> failwith "No <Version> element found in fsproj"
    | el -> el

let versionString = versionElement.Value
let numericPart =
    match versionString.IndexOf('-') with
    | -1 -> versionString
    | idx -> versionString.[..idx-1]

let currentVersion = Version.Parse(numericPart)
let nextPatch = Version(currentVersion.Major, currentVersion.Minor, currentVersion.Build + 1)
let alphaVersion = sprintf "%O-alpha" nextPatch

printfn "fsproj version : %s" versionString
printfn "alpha version  : %s" alphaVersion
printfn ""

let nupkgDir = Path.Combine(Path.GetDirectoryName(fsprojPath), "nupkg")

// Clean first: a stale incremental build can leave the tool compiled against
// dependency assemblies from a previous (differently-versioned) build, which
// produces a tool that throws FileNotFoundException for its own dependencies
// at runtime even though the correct files are right there on disk.
if isDryRun then
    printfn "[Dry Run] Would clean: dotnet clean %s -c Release" slnPath
else
    printfn "Cleaning solution..."
    runProcess "dotnet" [ "clean"; slnPath; "-c"; "Release" ] rootPath |> ignore
    printfn "Clean complete."

printfn ""

// Pack
if isDryRun then
    printfn "[Dry Run] Would pack: dotnet pack %s -c Release -p:Version=%s -o %s" fsprojRelative alphaVersion nupkgDir
else
    printfn "Packing %s %s..." packageId alphaVersion
    runProcess "dotnet"
        [ "pack"; fsprojRelative
          "-c"; "Release"
          (sprintf "-p:Version=%s" alphaVersion)
          "-o"; nupkgDir
          "--nologo" ]
        rootPath
    |> ignore
    printfn "Pack complete."

printfn ""

// Uninstall existing (best-effort: not installed is fine)
if isDryRun then
    printfn "[Dry Run] Would uninstall: dotnet tool uninstall --global %s" toolId
else
    printfn "Uninstalling existing %s (if present)..." toolId
    tryRunProcess "dotnet" ["tool"; "uninstall"; "--global"; toolId] rootPath |> ignore

// Install from local source
if isDryRun then
    printfn "[Dry Run] Would install: dotnet tool install --global --add-source %s %s --version %s" nupkgDir toolId alphaVersion
else
    printfn "Installing %s %s..." toolId alphaVersion
    runProcess "dotnet"
        [ "tool"; "install"; "--global"
          "--add-source"; nupkgDir
          toolId
          "--version"; alphaVersion ]
        rootPath
    |> ignore
    printfn ""
    printfn "Done! Installed %s %s" toolId alphaVersion
