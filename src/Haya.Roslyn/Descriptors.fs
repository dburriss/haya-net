namespace Haya.Roslyn

open System
open System.Collections.Generic
open System.Collections.Immutable
open System.Threading.Tasks
open Microsoft.CodeAnalysis
open Haya
open Haya.Core.Analysis

/// Builds Haya descriptors from the Haya attributes found in a C# solution with Roslyn.
module Descriptors =
    let attributeNames = [typeof<ResponsibilityAttribute>.FullName; typeof<CollaboratorAttribute>.FullName; typeof<MetaAttribute>.FullName]
    let makeRelativePath (baseDir: string) (targetPath: string) =
        let baseUri = Uri(baseDir.TrimEnd('/') + "/")
        let targetUri = Uri(targetPath)
        baseUri.MakeRelativeUri(targetUri).ToString()
    /// Where a component lives: its source file relative to `rootPath`, or `Assembly::Namespace.Type` for a type
    /// from a compiled reference (no source file).
    let componentSource (rootPath: string) (symbol: ISymbol) =
        match symbol.Locations |> Seq.tryFind (fun l -> l.IsInSource && not (isNull l.SourceTree)) with
        | Some l -> makeRelativePath rootPath l.SourceTree.FilePath
        | None -> $"{symbol.ContainingAssembly.Name}::{symbol.ToDisplayString()}"
    let namedArgumentsToMap (args: ImmutableArray<KeyValuePair<string, TypedConstant>>) =
        args |> Seq.map (fun x -> x.Key, x.Value.Value.ToString()) |> Map.ofSeq
    let private constantToString (c: TypedConstant) = c.Value.ToString()
    /// The named arguments of an attribute: the defaults baked into a custom attribute with its `*Defaults` marker
    /// (`markerFullName`), overridden by the arguments written at the usage site.
    let namedArguments (markerFullName: string) (attr: AttributeData) =
        let defaults = Roslyn.attributeDefaults markerFullName attr |> List.map (fun (k, v) -> k, constantToString v) |> Map.ofList
        attr.NamedArguments |> Seq.fold (fun m x -> Map.add x.Key (constantToString x.Value) m) defaults
    let (|IsResponsibility|_|) (rootPath: string) (symbol: ISymbol) =
        let attrs = symbol.GetAttributes() |> Roslyn.filterAttributes [typeof<ResponsibilityAttribute>.FullName]
        if attrs |> List.isEmpty |> not then
            attrs |> List.map (fun attr ->
                let map = namedArguments typeof<ResponsibilityDefaultsAttribute>.FullName attr
                let description = map |> Map.tryFind "Description" |> Option.defaultValue ""
                Responsibility {
                    Description = description
                    ComponentName = symbol.Name
                    ComponentSource = componentSource rootPath symbol
                }
            ) |> Some
        else
            None
    let private parseEnum<'a> (value: string) = Enum.Parse(typeof<'a>, value) :?> 'a
    
    let (|IsCollaborator|_|) (rootPath: string) (symbol: ISymbol) =
        let attrs = symbol.GetAttributes() |> Roslyn.filterAttributes [typeof<CollaboratorAttribute>.FullName]
        if attrs |> List.isEmpty |> not then
            attrs |> List.map (fun attr ->
                let map = namedArguments typeof<CollaboratorDefaultsAttribute>.FullName attr
                let direction = map |> Map.tryFind "Direction" |> Option.map parseEnum<Direction> |> Option.defaultValue Direction.Downstream
                let tech = map |> Map.tryFind "Technology" |> Option.defaultValue "" 
                let protocol = map |> Map.tryFind "Protocol" |> Option.defaultValue "" 
                let dataDescription = map |> Map.tryFind "DataDescription" |> Option.defaultValue ""
                let description = map |> Map.tryFind "Description" |> Option.defaultValue ""
                let appName = map |> Map.tryFind "AppName" |> Option.defaultValue ""
                let system = map |> Map.tryFind "System" |> Option.defaultValue ""
                let relationship = map |> Map.tryFind "Relationship" |> Option.map parseEnum<Relationship>|> Option.defaultValue Relationship.Internal
                let repository = map |> Map.tryFind "Repository" |> Option.defaultValue ""
                let owner = map |> Map.tryFind "Owner" |> Option.defaultValue ""
                Collaborator {
                    Direction = direction
                    Tech = tech
                    Protocol = protocol 
                    DataDescription = dataDescription
                    Description = description
                    AppName = appName
                    System = system
                    Relationship = relationship
                    ComponentName = symbol.Name
                    ComponentSource = componentSource rootPath symbol
                    Repository = repository
                    Owner = owner 
                }
            ) |> Some
        else
            None
    
    let (|IsMeta|_|) (symbol: ISymbol) =
        if symbol.Kind = SymbolKind.Assembly then
            let attrs = symbol.GetAttributes() |> Roslyn.filterAttributes [typeof<MetaAttribute>.FullName]
            // printfn "Meta Attrs %A" attrs
            if attrs |> List.isEmpty |> not then
                attrs |> List.map (fun attr ->
                    let map = namedArguments typeof<MetaDefaultsAttribute>.FullName attr
                    let appName = map |> Map.tryFind "AppName" |> Option.defaultValue ""
                    let description = map |> Map.tryFind "Description" |> Option.defaultValue ""
                    let owner = map |> Map.tryFind "Owner" |> Option.defaultValue ""
                    let system = map |> Map.tryFind "System" |> Option.defaultValue ""
                    let repository = map |> Map.tryFind "Repository" |> Option.defaultValue ""
                    let tech = map |> Map.tryFind "Technology" |> Option.defaultValue ""
                    let kind = map |> Map.tryFind "Kind" |> Option.map parseEnum<AppKind> |> Option.defaultValue AppKind.Unspecified
                    let lifecycle = map |> Map.tryFind "Lifecycle" |> Option.defaultValue ""
                    let tier = map |> Map.tryFind "Tier" |> Option.defaultValue ""
                    Meta {
                        AppName = appName
                        Description = description
                        System = system
                        Repository = repository
                        Owner = owner
                        Tech = tech
                        Kind = kind
                        Lifecycle = lifecycle
                        Tier = tier
                    }
                ) |> Some
            else None // no attributes
        else None // not an assembly
    
    let mapDescriptors (rootPath: string) (symbol: ISymbol) =
        // A symbol may carry several Haya attributes (e.g. Responsibility and Collaborator): collect all of them.
        [ match symbol with IsResponsibility rootPath xs -> yield! xs | _ -> ()
          match symbol with IsCollaborator rootPath xs -> yield! xs | _ -> ()
          match symbol with IsMeta xs -> yield! xs | _ -> () ]
    
    /// Descriptors of an analysed solution, and the compiled-reference types they were partly built from
    /// (assembly name, type name), so a caller can report them.
    type Analysis = { Descriptors: Descriptor list; Referenced: (string * string) list }

    let getAnalysisWith (references: Roslyn.ReferenceOptions) (rootPath: string) (attributeNames: string list) (solution: Solution) =
        task {
            let! classes = attributeNames
                           |> List.map (fun n -> Roslyn.findClassesWithAttribute n solution)
                           |> Task.WhenAll
            let! meta = attributeNames
                        |> List.map (fun n -> Roslyn.findAllAssembliesWithAttribute n solution)
                        |> Task.WhenAll
            let! referenced = attributeNames
                              |> List.map (fun n -> Roslyn.findReferencedTypesWithAttribute n references solution)
                              |> Task.WhenAll
            let toDescriptors (symbols: ISymbol list seq) =
                // the same symbol is found once per attribute name searched: map each only once
                symbols |> Seq.concat |> Seq.distinctBy (fun s -> s.ToDisplayString(), s.Kind) |> Seq.toList |> List.collect (mapDescriptors rootPath)
            let referencedTypes =
                referenced |> Seq.concat |> Seq.distinctBy (fun (a, t) -> a.Name, t.ToDisplayString()) |> Seq.toList
            // Only the types: an assembly-level Meta of a package describes another app, so it is never read.
            let fromReferences = referencedTypes |> List.collect (fun (_, t) -> mapDescriptors rootPath t)
            return { Descriptors = toDescriptors classes @ fromReferences @ toDescriptors meta
                     Referenced = referencedTypes |> List.map (fun (a, t) -> a.Name, t.ToDisplayString()) }
        }

    let getDescriptorsWith (references: Roslyn.ReferenceOptions) (rootPath: string) (attributeNames: string list) (solution: Solution) =
        task {
            let! analysis = getAnalysisWith references rootPath attributeNames solution
            return analysis.Descriptors
        }

    let getDescriptors (rootPath: string) (attributeNames: string list) (solution: Solution) =
        getDescriptorsWith Roslyn.ReferenceOptions.none rootPath attributeNames solution
