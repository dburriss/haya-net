namespace Haya.Roslyn

open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.CSharp.Syntax
open Microsoft.CodeAnalysis.MSBuild
open Microsoft.CodeAnalysis.FindSymbols
open Haya.Core.Analysis

module Roslyn =

    type ReferencesResult =
     | NoMatch
     | MultipleMatch of ISymbol seq
     | SingleMatch of ISymbol * ReferencedSymbol seq
     | NoReferences of ISymbol

    /// Open a solution with MSBuild global properties (e.g. "Configuration", "Release").
    /// Non-C# project references (e.g. F#) resolve via their built output for that configuration.
    let openSolutionWith (properties: (string * string) list) (solutionPath: string) =
        task {
            let ws = MSBuildWorkspace.Create(properties |> dict)
            return! ws.OpenSolutionAsync(solutionPath)
        }

    let openSolution solutionPath = openSolutionWith [] solutionPath

    /// Haya only analyses C# code. Roslyn also loads other project types (e.g. F#) but cannot compile them.
    let csharpProjects (solution: Solution) =
        solution.Projects |> Seq.filter (fun p -> p.SupportsCompilation && p.Language = LanguageNames.CSharp)

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
            for project in csharpProjects solution do
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
 
    /// True when the attribute is the named attribute type or derives from it (a custom attribute).
    let isAttributeOf (attributeFullName: string) (attr: AttributeData) =
        let rec loop (t: INamedTypeSymbol) =
            not (isNull t) && (t.ToDisplayString() = attributeFullName || loop t.BaseType)
        loop attr.AttributeClass

    let filterAttributes (attributeNames: string list) (attributes: AttributeData seq) =
        attributes
        |> Seq.filter (fun attr -> attributeNames |> List.exists (fun name -> isAttributeOf name attr))
        |> Seq.toList

    /// The property values baked into a custom attribute: the named arguments of the `*Defaults` marker attribute
    /// (`markerFullName`) on the attribute class and its base classes, base first so a derived class overrides.
    /// Only attribute metadata is read, so this works for attribute classes from source and from compiled references.
    let attributeDefaults (markerFullName: string) (attr: AttributeData) : (string * TypedConstant) list =
        let rec chain (t: INamedTypeSymbol) = if isNull t then [] else t :: chain t.BaseType
        chain attr.AttributeClass
        |> List.rev
        |> List.collect (fun t ->
            t.GetAttributes()
            |> Seq.filter (fun a -> not (isNull a.AttributeClass) && a.AttributeClass.ToDisplayString() = markerFullName)
            |> Seq.collect (fun a -> a.NamedArguments |> Seq.map (fun kv -> kv.Key, kv.Value))
            |> Seq.toList)

    let findClassesWithAttribute (attributeFullName: string) (solution: Solution) =
        task {
            let results = ResizeArray<ISymbol>()
            for project in csharpProjects solution do
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
                        if symbol.GetAttributes() |> Seq.exists (fun attr -> isAttributeOf attributeFullName attr) then
                            results.Add(symbol)
            return results |> Seq.toList
        }

    /// Which referenced (compiled) assemblies to scan for Haya attributes. Off by default.
    type ReferenceOptions = { Include: bool; AssemblyGlobs: string list }

    module ReferenceOptions =
        let none = { Include = false; AssemblyGlobs = [] }

        /// Wildcard match (`*`, `?`) on an assembly name, case-insensitive. No globs matches every assembly.
        let matches (opts: ReferenceOptions) (assemblyName: string) =
            let toRegex (glob: string) =
                "^" + System.Text.RegularExpressions.Regex.Escape(glob).Replace("\\*", ".*").Replace("\\?", ".") + "$"
            opts.AssemblyGlobs |> List.isEmpty
            || opts.AssemblyGlobs |> List.exists (fun g ->
                System.Text.RegularExpressions.Regex.IsMatch(assemblyName, toRegex g, System.Text.RegularExpressions.RegexOptions.IgnoreCase))

        /// Referenced assemblies are scanned when asked for, either with the flag or with an assembly glob.
        let isActive (opts: ReferenceOptions) = opts.Include || not (List.isEmpty opts.AssemblyGlobs)

    /// Public classes (including public nested ones) declared in a namespace and its child namespaces.
    let rec private publicClasses (ns: INamespaceSymbol) : INamedTypeSymbol seq =
        let rec nested (t: INamedTypeSymbol) : INamedTypeSymbol seq =
            seq {
                if t.TypeKind = TypeKind.Class then yield t
                for n in t.GetTypeMembers() do
                    if n.DeclaredAccessibility = Accessibility.Public then yield! nested n
            }
        seq {
            for t in ns.GetTypeMembers() do
                if t.DeclaredAccessibility = Accessibility.Public then yield! nested t
            for child in ns.GetNamespaceMembers() do
                yield! publicClasses child
        }

    /// Public classes in compiled references (NuGet packages, DLLs) that carry the attribute.
    /// Only assemblies that reference Haya and match the options are walked. References to projects in the
    /// solution are skipped: they are analysed from source already. Each type is returned once.
    let findReferencedTypesWithAttribute (attributeFullName: string) (opts: ReferenceOptions) (solution: Solution) =
        task {
            let results = ResizeArray<IAssemblySymbol * INamedTypeSymbol>()
            let seen = System.Collections.Generic.HashSet<string>()
            if ReferenceOptions.isActive opts then
                for project in csharpProjects solution do
                    let! compilation = project.GetCompilationAsync()
                    for reference in compilation.References do
                        match reference with
                        | :? PortableExecutableReference ->
                            match compilation.GetAssemblyOrModuleSymbol(reference) with
                            | :? IAssemblySymbol as assembly
                                when ReferenceOptions.matches opts assembly.Name
                                     && assembly.Modules |> Seq.exists (fun m -> m.ReferencedAssemblies |> Seq.exists (fun a -> a.Name = "Haya")) ->
                                for t in publicClasses assembly.GlobalNamespace do
                                    if t.GetAttributes() |> Seq.exists (isAttributeOf attributeFullName)
                                       && seen.Add(assembly.Name + "::" + t.ToDisplayString()) then
                                        results.Add((assembly, t))
                            | _ -> ()
                        | _ -> ()
            return results |> Seq.toList
        }

    let findAssemblies (solution: Solution) =
        task {
            let results = ResizeArray<IAssemblySymbol>()
            for project in csharpProjects solution do
                let! compilation = project.GetCompilationAsync()
                let assemblySymbol = compilation.Assembly
                results.Add(assemblySymbol)
            return results |> Seq.toList
        }
    let findAllAssembliesWithAttribute (attributeFullName: string) (solution: Solution) =
        task {
            let results = ResizeArray<ISymbol>()
            for project in csharpProjects solution do
                let! compilation = project.GetCompilationAsync()
                let assemblySymbol = compilation.Assembly
                if assemblySymbol.GetAttributes() |> Seq.exists (fun attr -> isAttributeOf attributeFullName attr) then
                    results.Add(assemblySymbol)
            return results |> Seq.toList
        }

    /// The type as C# writes it in short form, e.g. `Task<bool>` or `string?`.
    let typeDisplay (t: ITypeSymbol) = t.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)

    let private parameters (ps: IParameterSymbol seq) : Parameter list =
        ps |> Seq.map (fun p -> ({ Name = p.Name; Type = typeDisplay p.Type } : Parameter)) |> Seq.toList

    /// True when the method overrides one first declared on `System.Object` (`ToString`, `Equals`, `GetHashCode`).
    let private overridesObject (m: IMethodSymbol) =
        let rec root (m: IMethodSymbol) = if isNull m.OverriddenMethod then m else root m.OverriddenMethod
        m.IsOverride && (root m).ContainingType.SpecialType = SpecialType.System_Object

    /// The public constructors and the public members declared on a class (ADR017). Compiler-generated members (the
    /// implicit constructor, record members, backing fields), accessors, operators, nested types and overrides of
    /// `System.Object` are left out. Order is set by `ComponentDetail.normalise`.
    let componentDetail (name: string) (source: string) (t: INamedTypeSymbol) : ComponentDetail =
        let isPublic (s: ISymbol) = s.DeclaredAccessibility = Accessibility.Public && not s.IsImplicitlyDeclared
        let constructors =
            t.InstanceConstructors
            |> Seq.filter isPublic
            |> Seq.map (fun c -> parameters c.Parameters)
            |> Seq.toList
        let members : Member list =
            t.GetMembers()
            // an indexer is named `this[]`, which cannot be referenced by name
            |> Seq.filter (fun m -> isPublic m && (m.CanBeReferencedByName || (match m with :? IPropertySymbol as p -> p.IsIndexer | _ -> false)))
            |> Seq.choose (fun m ->
                match m with
                | :? IMethodSymbol as x when x.MethodKind = MethodKind.Ordinary && not (overridesObject x) ->
                    Some { Name = x.Name; Kind = MemberKind.Method; Type = typeDisplay x.ReturnType; Parameters = parameters x.Parameters }
                | :? IPropertySymbol as x ->
                    Some { Name = x.Name; Kind = MemberKind.Property; Type = typeDisplay x.Type; Parameters = parameters x.Parameters }
                | :? IFieldSymbol as x when isNull x.AssociatedSymbol ->
                    Some { Name = x.Name; Kind = MemberKind.Field; Type = typeDisplay x.Type; Parameters = [] }
                | :? IEventSymbol as x ->
                    Some { Name = x.Name; Kind = MemberKind.Event; Type = typeDisplay x.Type; Parameters = [] }
                | _ -> None)
            |> Seq.toList
        { Name = name; Source = source; Constructors = constructors; Members = members }
