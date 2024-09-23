namespace Haya.Core.Tests

open System.Text
open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.Text

module SolutionBuilder =
    
    type SolutionData = {
        ``namespace``: string
        assemblyName: string
        usings: string list
        references: System.Type list
        source: StringBuilder
        fileName: string
    }
    
    let init () = {
        ``namespace`` = "TestNs"
        assemblyName = "TestAssembly"
        usings = ["System"; "Haya"]
        references = [typeof<Haya.MetaAttribute>]
        source = StringBuilder()
        fileName = "TestDocument.cs"
    }

    
    let withNamespace ns solutionData = { solutionData with ``namespace`` = ns }
    let withAssemblyName name solutionData = { solutionData with assemblyName = name }
    
    let appendCode (code:string) (solutionData:SolutionData) = { solutionData with source = appendLine code solutionData.source }
    
    let code (code: string) =
        init() |> appendCode code
    let private toCode (solutionData:SolutionData) =
        let mutable sb = StringBuilder()
        sb <- solutionData.usings |> List.fold (fun sb u -> usingLine u sb) sb
        sb |> emptyLine
        |> nsLine solutionData.``namespace``
        |> openCurly
        |> appendLine (solutionData.source.ToString())
        |> closeCurly
        |> string
        
    let createSolution (solutionData: SolutionData) =
        // Create a new AdhocWorkspace
        let workspace = new AdhocWorkspace()

        // Create a new Solution
        let solution = workspace.CurrentSolution

        // Define a project ID and project info
        let projectId = ProjectId.CreateNewId()
        let projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            solutionData.assemblyName,
            solutionData.assemblyName,
            LanguageNames.CSharp)

        // Add the project to the solution
        let solution = solution.AddProject(projectInfo)

        // Define a document ID and document info
        let documentId = DocumentId.CreateNewId(projectId)
        let codeText = solutionData |> toCode
        let sourceText = SourceText.From(codeText)
        let documentInfo = DocumentInfo.Create(
            documentId,
            solutionData.fileName,
            null,
            SourceCodeKind.Regular,
            TextLoader.From(TextAndVersion.Create(sourceText, VersionStamp.Create())))

        // Add the document to the project
        let solution = solution.AddDocument(documentInfo)

        solution 
    
    let asSolution (solutionData:SolutionData) =
        solutionData |> createSolution 
