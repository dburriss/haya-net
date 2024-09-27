namespace Haya.Core.Analysis

open System
open System.Collections.Generic
open System.Collections.Immutable
open System.Threading.Tasks
open Microsoft.CodeAnalysis
open FSharpPlus
open Haya

type ResponsibilityDescriptor = {
    Description: string
    ComponentName: string
    ComponentSource: string
}

// type Direction = Upstream | Downstream
// type Relationship = Owned | Shared | Internal | External

type CollaboratorDescriptor = {
    Direction: Direction
    Tech: string
    Protocol: string
    DataDescription: string
    Description: string
    AppName: string
    System: string
    Relationship: Relationship
    ComponentName: string
    ComponentSource: string
    Repository: string
    Owner: string
}

type MetaDescriptor = {
    AppName: string
    Description: string
    System: string
    Repository: string
    Owner: string
}

type Descriptor =
    | Responsibility of ResponsibilityDescriptor
    | Collaborator of CollaboratorDescriptor
    | Meta of MetaDescriptor
    

module Descriptor =
    let attributeNames = [typeof<ResponsibilityAttribute>.FullName; typeof<CollaboratorAttribute>.FullName; typeof<MetaAttribute>.FullName]
    let makeRelativePath (baseDir: string) (targetPath: string) =
        let baseUri = Uri(baseDir.TrimEnd('/') + "/")
        let targetUri = Uri(targetPath)
        baseUri.MakeRelativeUri(targetUri).ToString()
    let namedArgumentsToMap (args: ImmutableArray<KeyValuePair<string, TypedConstant>>) =
        args |> Seq.map (fun x -> x.Key, x.Value.Value.ToString()) |> Map.ofSeq
    let (|IsResponsibility|_|) (rootPath: string) (symbol: ISymbol) =
        let attrs = symbol.GetAttributes() |> Roslyn.filterAttributes [typeof<ResponsibilityAttribute>.FullName]
        if attrs |> List.isEmpty |> not then
            attrs |> List.map (fun attr ->
                let map = attr.NamedArguments |> namedArgumentsToMap
                let description = map |> Map.tryFind "Description" |> Option.defaultValue ""
                Responsibility {
                    Description = description
                    ComponentName = symbol.Name
                    ComponentSource = symbol.Locations |> Seq.head |> _.SourceTree.FilePath |> makeRelativePath rootPath
                }
            ) |> Some
        else
            None
    let private parseEnum<'a> (value: string) = Enum.Parse(typeof<'a>, value) :?> 'a
    
    let (|IsCollaborator|_|) (rootPath: string) (symbol: ISymbol) =
        let attrs = symbol.GetAttributes() |> Roslyn.filterAttributes [typeof<CollaboratorAttribute>.FullName]
        if attrs |> List.isEmpty |> not then
            attrs |> List.map (fun attr ->
                let map = attr.NamedArguments |> namedArgumentsToMap
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
                    ComponentSource = symbol.Locations |> Seq.head |> _.SourceTree.FilePath |> makeRelativePath rootPath
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
                    let map = attr.NamedArguments |> namedArgumentsToMap
                    let appName = map |> Map.tryFind "AppName" |> Option.defaultValue ""
                    let description = map |> Map.tryFind "Description" |> Option.defaultValue ""
                    let owner = map |> Map.tryFind "Owner" |> Option.defaultValue ""
                    let system = map |> Map.tryFind "System" |> Option.defaultValue ""
                    let repository = map |> Map.tryFind "Repository" |> Option.defaultValue ""
                    Meta {
                        AppName = appName
                        Description = description
                        System = system
                        Repository = repository
                        Owner = owner
                    }
                ) |> Some
            else None // no attributes
        else None // not an assembly
    
    let mapDescriptors (rootPath: string) (symbol: ISymbol) =
        match symbol with
        | IsResponsibility rootPath xs -> xs
        | IsCollaborator rootPath xs -> xs
        | IsMeta xs -> xs
        | _ -> []//failwith "Not implemented"
    
    let getDescriptors (rootPath: string) (attributeNames: string list) (solution: Solution) =
        task {
            let! classes = attributeNames
                           |> List.map (fun n -> Roslyn.findClassesWithAttribute n solution)
                           |> Task.WhenAll
                           |> Task.map (Seq.concat >> Seq.toList)
                           |> Task.map (List.collect (mapDescriptors rootPath))
            
            let! meta = attributeNames
                        |> List.map (fun n -> Roslyn.findAllAssembliesWithAttribute n solution)
                           |> Task.WhenAll
                           |> Task.map (Seq.concat >> Seq.toList)
                           |> Task.map (List.collect (mapDescriptors rootPath))
            return classes @ meta
        }
    
    let collaborators (descriptors: Descriptor list) =
        descriptors |> List.choose (function | Collaborator x -> Some x | _ -> None)
    
    let responsibilities (descriptors: Descriptor list) =
        descriptors |> List.choose (function | Responsibility x -> Some x | _ -> None) 
    
    let metas (descriptors: Descriptor list) =
        descriptors |> List.choose (function | Meta x -> Some x | _ -> None) 
