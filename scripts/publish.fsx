#!/usr/bin/env -S dotnet fsi

// =================================================================================
// Publish Script
// =================================================================================
// This script automates the release of a .NET project that is published to NuGet and
// GitHub Releases by tag-triggered CI. It never talks to nuget.org or GitHub itself:
// pushing the v* tag is what starts the workflows.
//
// Normal release flow (when Unreleased changes exist):
// 1. Reads the current version from the --fsproj file (or a Directory.Build.props that
//    holds <Version> and <PackageReleaseNotes> in its first <PropertyGroup>)
// 2. Extracts the "Unreleased" section from CHANGELOG.md
// 3. Runs a preflight release build and test pass
// 4. Prompts the user to select the next version (Major, Minor, or Patch)
// 5. Updates CHANGELOG.md (stable releases only — prereleases leave it untouched
//    so entries roll forward into the eventual stable release):
//    - Moves "Unreleased" changes to a new versioned section
//    - Creates a new empty "Unreleased" section
// 6. Updates that file via XML APIs:
//    - Increments the <Version> tag
//    - Updates <PackageReleaseNotes> with escaped text content
// 7. Runs a final release build before any git operations
// 8. Stages, commits, and tags the release in Git
// 9. Optionally pushes the changes and tag to the remote repository
//
// Retag flow (when there are no Unreleased changes):
// 1. Shows the latest commit hash and message
// 2. Asks whether to retag that commit with the current version
// 3. Prompts the user to type the current version to confirm
// 4. Runs a preflight release build and test pass
// 5. Warns that the local tag will be deleted and recreated, and the remote
//    tag will be force-pushed if the user chooses to push
// 6. Deletes the existing local tag (if present) and recreates it at HEAD
// 7. Optionally force-pushes the tag to the remote
//
// Arguments:
//   --dry-run        Simulate the process without making any changes to disk or git.
//   --allow-dirty    Allow running with a dirty working tree.
//   --require-clean  Fail if the working tree is dirty.
//   --fsproj <path>  Relative path from repo root to the tool .fsproj (required).
//   --solution <path> Relative path from repo root to the solution file (required).
//
// Usage:
//   ./publish.sh
//   ./publish.sh --dry-run
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

let promptYesNo prompt =
    printf "%s" prompt
    match Console.ReadLine() with
    | null -> false
    | response -> response.Trim().ToLowerInvariant() = "y"

let requireElement (parent: XElement) (name: XName) =
    match parent.Element(name) with
    | null -> failwithf "Missing <%s> element in %s" name.LocalName parent.Name.LocalName
    | element -> element

let args = fsi.CommandLineArgs |> Array.skip 1
let isDryRun = args |> Array.contains "--dry-run"
let allowDirty =
    if args |> Array.contains "--require-clean" then false
    else true

let getArg name =
    args
    |> Array.tryFindIndex ((=) name)
    |> Option.bind (fun i -> args |> Array.tryItem (i + 1))
    |> Option.defaultWith (fun () -> failwithf "Missing required argument: %s" name)

if isDryRun then
    printfn "=== DRY RUN MODE ==="
    printfn "No changes will be written to disk."
    printfn "===================="

let rootDir = __SOURCE_DIRECTORY__ |> Directory.GetParent
let rootPath = rootDir.FullName
let fsprojPath = Path.Combine(rootPath, getArg "--fsproj")
let changelogPath = Path.Combine(rootPath, "CHANGELOG.md")
let solutionPath = Path.Combine(rootPath, getArg "--solution")

// Tests run on Microsoft.Testing.Platform (.NET 10, opted in through the "test" runner in
// global.json): the solution is passed with --solution, and --nologo is left out because it
// makes the test host report "Zero tests ran" (exit 5).
let testArguments = ["test"; "--solution"; solutionPath; "-c"; "Release"; "--no-build"]
let testCommandDisplay = sprintf "dotnet test --solution \"%s\" -c Release --no-build" solutionPath

printfn "Checking files..."
if not (File.Exists fsprojPath) then failwithf "Project file not found: %s" fsprojPath
if not (File.Exists changelogPath) then failwithf "Changelog not found: %s" changelogPath
if not (File.Exists solutionPath) then failwithf "Solution file not found: %s" solutionPath

let gitStatus = runProcess "git" ["status"; "--porcelain"] rootPath
if gitStatus.Length > 0 && not allowDirty then
    failwith "Working tree is dirty. Commit or stash changes first, or rerun with --allow-dirty."

let projectDoc = XDocument.Load(fsprojPath)
let projectRoot =
    match projectDoc.Root with
    | null -> failwithf "Project XML is empty: %s" fsprojPath
    | root -> root
let ns = projectRoot.Name.Namespace
let propertyGroup =
    projectRoot.Elements(ns + "PropertyGroup")
    |> Seq.tryHead
    |> Option.defaultWith (fun () -> failwithf "No <PropertyGroup> found in %s" fsprojPath)
let versionElement = requireElement propertyGroup (ns + "Version")
let releaseNotesElement = requireElement propertyGroup (ns + "PackageReleaseNotes")

let versionString = versionElement.Value
let numericPart, currentSuffix =
    match versionString.IndexOf('-') with
    | -1 -> versionString, None
    | idx -> versionString.[..idx-1], Some versionString.[idx+1..]
let currentNumericVersion = Version.Parse(numericPart)
printfn "Current Version: %s" versionString

let changelogLines = File.ReadAllLines(changelogPath) |> Array.toList

let unreleasedHeaderIdx =
    changelogLines
    |> List.tryFindIndex (fun l -> l.StartsWith("## [Unreleased]"))

if unreleasedHeaderIdx.IsNone then failwith "Could not find '## [Unreleased]' in CHANGELOG.md"

let unreleasedIdx = unreleasedHeaderIdx.Value

let nextSectionIdx =
    changelogLines
    |> List.skip (unreleasedIdx + 1)
    |> List.tryFindIndex (fun l -> l.StartsWith("## [") && not (l.Contains("Unreleased")))
    |> Option.map (fun i -> i + unreleasedIdx + 1)
    |> Option.defaultValue changelogLines.Length

let unreleasedContent =
    changelogLines
    |> List.skip (unreleasedIdx + 1)
    |> List.take (nextSectionIdx - unreleasedIdx - 1)
    |> List.filter (fun l -> not (String.IsNullOrWhiteSpace(l)))

printfn "\nUnreleased Changes:"
if unreleasedContent.IsEmpty then
    printfn "  (None)"
else
    unreleasedContent |> List.iter (fun l -> printfn "  %s" l)

printfn ""

let runReleaseBuild () =
    printfn "Running release build..."
    runProcess "dotnet" ["build"; solutionPath; "-c"; "Release"; "--nologo"; "--no-incremental"] rootPath |> ignore

let runReleaseTests () =
    printfn "Running release tests..."
    runProcess "dotnet" testArguments rootPath |> ignore

// ---------------------------------------------------------------------------
// Retag path — no unreleased changes
// ---------------------------------------------------------------------------

if unreleasedContent.IsEmpty then
    let headInfo = runProcess "git" ["log"; "-1"; "--pretty=format:%h %s"] rootPath
    printfn "No unreleased changes found."
    printfn "Latest commit: %s" headInfo
    printfn ""

    if not (promptYesNo (sprintf "Do you want to retag this commit as v%s? (y/n): " versionString)) then
        printfn "Nothing to do."
        exit 0

    printf "Type the current version to confirm (%s): " versionString
    let typed = Console.ReadLine()
    if typed <> versionString then
        eprintfn "Version mismatch — expected '%s', got '%s'. Aborting." versionString typed
        exit 1

    if isDryRun then
        printfn "[Dry Run] Would run: dotnet build \"%s\" -c Release --nologo --no-incremental" solutionPath
        printfn "[Dry Run] Would run: %s" testCommandDisplay
    else
        runReleaseBuild ()
        runReleaseTests ()

    let tagName = sprintf "v%s" versionString
    printfn ""
    printfn "Warning: this will delete and recreate tag %s locally." tagName
    printfn "If you choose to push, it will force-push the tag to the remote,"
    printfn "overwriting any existing remote tag."
    printfn ""

    if isDryRun then
        printfn "[Dry Run] Git operations skipped. Would execute:"
        printfn "1. git tag -d %s  (if tag exists)" tagName
        printfn "2. git tag %s" tagName
        printfn "3. git push origin %s --force  (if push confirmed)" tagName
        exit 0

    let existingTag = runProcess "git" ["tag"; "-l"; tagName] rootPath
    if existingTag.Length > 0 then
        printfn "Deleting existing local tag %s..." tagName
        runProcess "git" ["tag"; "-d"; tagName] rootPath |> ignore

    printfn "Creating tag %s at HEAD..." tagName
    runProcess "git" ["tag"; tagName] rootPath |> ignore
    printfn "Tag %s created." tagName

    if promptYesNo (sprintf "Push tag %s to remote (force)? (y/n): " tagName) then
        printfn "Force-pushing tag %s..." tagName
        try
            runProcess "git" ["push"; "origin"; tagName; "--force"] rootPath |> printfn "%s"
            printfn "Done!"
        with ex ->
            printfn "Error pushing tag: %s" ex.Message
    else
        printfn "Push skipped. Run: git push origin %s --force" tagName

    exit 0

// ---------------------------------------------------------------------------
// Normal release path — unreleased changes exist
// ---------------------------------------------------------------------------

if isDryRun then
    printfn "[Dry Run] Would run: dotnet build \"%s\" -c Release --nologo --no-incremental" solutionPath
    printfn "[Dry Run] Would run: %s" testCommandDisplay
else
    runReleaseBuild ()
    runReleaseTests ()

let newVersionString =
    match currentSuffix with
    | Some _ when promptYesNo (sprintf "Current version is %s. Promote to stable %s? (y/n): " versionString numericPart) ->
        numericPart
    | _ ->
        let prereleaseSuffix =
            if promptYesNo "Is this a prerelease? (y/n): " then
                printf "Prerelease suffix (e.g. beta1, rc1): "
                let suffix = (Console.ReadLine() |> Option.ofObj |> Option.defaultValue "").Trim()
                if suffix.Length = 0 || suffix.Contains(" ") || suffix.Contains("-") then
                    failwith "Invalid prerelease suffix: must be non-empty with no spaces or hyphens"
                Some suffix
            else
                None

        let nextMajor = Version(currentNumericVersion.Major + 1, 0, 0)
        let nextMinor = Version(currentNumericVersion.Major, currentNumericVersion.Minor + 1, 0)
        let nextPatch = Version(currentNumericVersion.Major, currentNumericVersion.Minor, currentNumericVersion.Build + 1)

        printfn "Select increment type:"
        printfn "1) Major (%O -> %O)" currentNumericVersion nextMajor
        printfn "2) Minor (%O -> %O)" currentNumericVersion nextMinor
        printfn "3) Patch (%O -> %O)" currentNumericVersion nextPatch

        printf "Choice (1-3): "
        let choice = Console.ReadLine()
        let baseVersion =
            match choice with
            | "1" -> nextMajor
            | "2" -> nextMinor
            | "3" -> nextPatch
            | _ -> failwith "Invalid selection"

        match prereleaseSuffix with
        | Some s -> sprintf "%O-%s" baseVersion s
        | None -> sprintf "%O" baseVersion

printfn "New Version: %s" newVersionString

let isPrerelease = newVersionString.Contains('-')

let releaseNotes =
    unreleasedContent
    |> List.filter (fun l -> l.Trim().StartsWith("-") || l.Trim().StartsWith("*"))
    |> List.map (fun l -> l.Trim().TrimStart('-', '*').Trim())
    |> String.concat "\n"

printfn "Updating fsproj..."
if isDryRun then
    printfn "\n[Dry Run] Would update fsproj:"
    printfn "  Version: %s" newVersionString
    printfn "  ReleaseNotes: %s" releaseNotes
else
    versionElement.Value <- newVersionString
    releaseNotesElement.Value <- releaseNotes
    projectDoc.Save(fsprojPath)

if isPrerelease then
    printfn "Prerelease detected — CHANGELOG.md left unchanged; entries remain under [Unreleased]."
else
    printfn "Updating CHANGELOG.md..."
    let today = DateTime.Now.ToString("yyyy-MM-dd")

    let preUnreleasedLines = changelogLines |> List.take unreleasedIdx
    let postUnreleasedLines = changelogLines |> List.skip (unreleasedIdx + 1)

    let newChangelogSection =
        [
            "## [Unreleased]"
            ""
            sprintf "## [%s] - %s" newVersionString today
        ]

    let newChangelogLines =
        preUnreleasedLines @
        newChangelogSection @
        postUnreleasedLines

    if isDryRun then
        printfn "\n[Dry Run] Would update CHANGELOG.md with new section:"
        newChangelogSection |> List.iter (fun l -> printfn "  %s" l)
    else
        File.WriteAllLines(changelogPath, newChangelogLines)

if isDryRun then
    printfn "[Dry Run] Would run: dotnet build \"%s\" -c Release --nologo --no-incremental" solutionPath
else
    printfn "Running final release build..."
    runReleaseBuild ()

let filesToStage =
    if isPrerelease then [fsprojPath]
    else [fsprojPath; changelogPath]
let stagedFilesDisplay = filesToStage |> List.map (sprintf "\"%s\"") |> String.concat " "

if isDryRun then
    printfn "\n[Dry Run] Git operations skipped. Would execute:"
    printfn "1. git add %s" stagedFilesDisplay
    printfn "2. git commit -m \"release: prepare v%s\"" newVersionString
    printfn "3. git tag v%s" newVersionString
    printfn "4. git push origin main"
    printfn "5. git push origin v%s" newVersionString
    exit 0

printfn "\nFiles updated. Ready to commit."
printfn "Commands to be executed:"
printfn "1. git add %s" stagedFilesDisplay
printfn "2. git commit -m \"release: prepare v%s\"" newVersionString
printfn "3. git tag v%s" newVersionString
printfn "4. git push origin main"
printfn "5. git push origin v%s" newVersionString

if promptYesNo "Proceed with git operations? (y/n): " then
    printfn "Executing git add..."
    runProcess "git" ("add" :: filesToStage) rootPath |> ignore

    printfn "Executing git commit..."
    runProcess "git" ["commit"; "-m"; sprintf "release: prepare v%s" newVersionString] rootPath |> ignore

    printfn "Executing git tag..."
    runProcess "git" ["tag"; sprintf "v%s" newVersionString] rootPath |> ignore

    if promptYesNo "Push to remote? (y/n): " then
        printfn "Pushing main..."
        try
            runProcess "git" ["push"; "origin"; "main"] rootPath |> printfn "%s"
            printfn "Pushing tag..."
            runProcess "git" ["push"; "origin"; sprintf "v%s" newVersionString] rootPath |> printfn "%s"
            printfn "Done!"
        with ex ->
            printfn "Error pushing: %s" ex.Message
    else
        printfn "Push skipped. Don't forget to push manually!"
else
    printfn "Git operations skipped."
