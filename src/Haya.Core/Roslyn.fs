namespace Haya.Core.Analysis

open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.CSharp.Syntax
open Microsoft.CodeAnalysis.MSBuild
open Microsoft.CodeAnalysis.FindSymbols

module Roslyn =

    type ReferencesResult =
     | NoMatch
     | MultipleMatch of ISymbol seq
     | SingleMatch of ISymbol * ReferencedSymbol seq
     | NoReferences of ISymbol

    let openSolution solutionPath =
        task {
            let ws = MSBuildWorkspace.Create()
            return! ws.OpenSolutionAsync(solutionPath)
        }

    let rec ancestors (s:ISymbol) = 
        seq {   
                yield s
                match s.ContainingSymbol with
                 | :? INamespaceSymbol as ns when ns.IsGlobalNamespace -> ()
                 | c -> yield! ancestors c
            }
    
    let declarationOfType (fullName:string) (solution:Solution) =
      task {
        let nameParts = fullName.Split('.') |> Seq.rev
        let nameFilter n = nameParts |> Seq.head = n
        let! symbols = SymbolFinder.FindSourceDeclarationsAsync(solution, nameFilter) 

        let isMatch symbol = (symbol |> ancestors |> Seq.map (fun x -> x.Name), nameParts) ||> Seq.compareWith Operators.compare = 0
            
        return symbols |> Seq.filter isMatch |> Seq.toList
      }

    let referencesOfType (s:ISymbol) (solution:Solution) =
        task {
            let! ret = SymbolFinder.FindReferencesAsync(s, solution) 
            return ret |> Seq.toList
        }

    let findReferencesOf typeName (solution:Solution) = 
        task {
            let! wantedSymbols = solution |> declarationOfType typeName 

            match wantedSymbols with
             | [] -> return NoMatch
             | [s] -> let! refs = solution |> referencesOfType s
                      match refs with
                       | [] -> return NoReferences (s) 
                       | _ -> return SingleMatch (s, refs)
             | _ -> return MultipleMatch (wantedSymbols)
        }
    let findAllAttributesOnClasses (solution: Solution) =
        task {
            let results = ResizeArray<AttributeData>()
            for project in solution.Projects do
                for document in project.Documents do
                    let! syntaxTree = document.GetSyntaxTreeAsync()
                    let! semanticModel = document.GetSemanticModelAsync()
                    let root = syntaxTree.GetRoot()
                    let classDeclarations =
                        root.DescendantNodes()
                        |> Seq.filter (fun x -> x :? ClassDeclarationSyntax)
                        |> Seq.map(fun x -> x :?> ClassDeclarationSyntax)
                        
                    for classDecl in classDeclarations do
                        let symbol = semanticModel.GetDeclaredSymbol(classDecl)
                        let attr = symbol.GetAttributes()
                        do results.AddRange(attr)
            return results |> Seq.distinct |> Seq.toList
        }
 
    let filterAttributes (attributeNames: string list) (attributes: AttributeData seq) =
        attributes
        |> Seq.filter (fun attr -> attributeNames |> List.exists (fun name -> attr.AttributeClass.ToDisplayString() = name))
        |> Seq.toList
    
    let findClassesWithAttribute (attributeFullName: string) (solution: Solution) =
        task {
            let results = ResizeArray<ISymbol>()
            for project in solution.Projects do
                for document in project.Documents do
                    let! syntaxTree = document.GetSyntaxTreeAsync()
                    let! semanticModel = document.GetSemanticModelAsync()
                    let root = syntaxTree.GetRoot()
                    let classDeclarations =
                        root.DescendantNodes()
                        |> Seq.filter (fun x -> x :? ClassDeclarationSyntax)
                        |> Seq.map(fun x -> x :?> ClassDeclarationSyntax)
                        
                    for classDecl in classDeclarations do
                        let symbol = semanticModel.GetDeclaredSymbol(classDecl)
                        if symbol.GetAttributes() |> Seq.exists (fun attr -> attr.AttributeClass.ToDisplayString() = attributeFullName) then
                            results.Add(symbol)
            return results |> Seq.toList
        }
    let findAssemblies (solution: Solution) =
        task {
            let results = ResizeArray<IAssemblySymbol>()
            for project in solution.Projects do
                let! compilation = project.GetCompilationAsync()
                let assemblySymbol = compilation.Assembly
                results.Add(assemblySymbol)
            return results |> Seq.toList
        }
    let findAllAssembliesWithAttribute (attributeFullName: string) (solution: Solution) =
        task {
            let results = ResizeArray<ISymbol>()
            for project in solution.Projects do
                let! compilation = project.GetCompilationAsync()
                let assemblySymbol = compilation.Assembly
                if assemblySymbol.GetAttributes() |> Seq.exists (fun attr -> attr.AttributeClass.ToDisplayString() = attributeFullName) then
                    results.Add(assemblySymbol)
            return results |> Seq.toList
        }
    
    
