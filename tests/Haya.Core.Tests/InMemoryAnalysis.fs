/// Helpers to compile small C# snippets in memory and analyse them with Haya, so tests need no MSBuild or restore.
module InMemoryAnalysis

open System
open System.IO
open Haya
open Haya.Roslyn
open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.CSharp
open Microsoft.CodeAnalysis.Text
open Xunit

let platformReferences =
    (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") :?> string).Split(Path.PathSeparator)
    // the test host references the annotated Example project: leave it out so only the code under test is seen
    |> Array.filter (fun p -> not (Path.GetFileName(p).StartsWith "Example"))
    |> Array.map (fun p -> MetadataReference.CreateFromFile p :> MetadataReference)
    |> Array.toList

let hayaReference = MetadataReference.CreateFromFile(typeof<MetaAttribute>.Assembly.Location) :> MetadataReference

/// Compile `source` to a DLL and return it as a metadata reference; `withHaya` = whether it references Haya.
let compileLibrary (assemblyName: string) (withHaya: bool) (source: string) : MetadataReference =
    let compilation =
        CSharpCompilation.Create(
            assemblyName,
            [ CSharpSyntaxTree.ParseText(source, path = "/virtual/Lib.cs") ],
            platformReferences @ (if withHaya then [ hayaReference ] else []),
            CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
    use stream = new MemoryStream()
    let result = compilation.Emit stream
    Assert.True(result.Success, String.Join("\n", result.Diagnostics))
    MetadataReference.CreateFromImage(stream.ToArray()) :> MetadataReference

/// Analyse `source` as the only document of a C# project (which references Haya and `extraReferences`). With `members`,
/// also collect the constructors and public members of the annotated classes.
let analyseWithMembers (members: bool) (options: Roslyn.ReferenceOptions) (extraReferences: MetadataReference list) (source: string) =
    let workspace = new AdhocWorkspace()
    let projectId = ProjectId.CreateNewId()
    let projectInfo =
        ProjectInfo.Create(projectId, VersionStamp.Create(), "Test", "Test", LanguageNames.CSharp)
            .WithMetadataReferences(platformReferences @ [ hayaReference ] @ extraReferences)
    let solution = workspace.CurrentSolution.AddProject projectInfo
    let solution =
        solution.AddDocument(
            DocumentInfo.Create(
                DocumentId.CreateNewId projectId, "Source.cs", null, SourceCodeKind.Regular,
                TextLoader.From(TextAndVersion.Create(SourceText.From source, VersionStamp.Create())),
                filePath = "/virtual/Source.cs"))
    Descriptors.getAnalysisWithMembers members options "/virtual" Descriptors.attributeNames solution
    |> Async.AwaitTask |> Async.RunSynchronously

/// Analyse `source` as the only document of a C# project (which references Haya and `extraReferences`).
let analyseWith (options: Roslyn.ReferenceOptions) (extraReferences: MetadataReference list) (source: string) =
    analyseWithMembers false options extraReferences source
