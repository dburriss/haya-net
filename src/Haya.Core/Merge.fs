namespace Haya.Core

open Haya.Core.Analysis

/// Combines the apps of several describe files / solutions into one system model.
module Merge =

    let private key (app: App) =
        app.Meta |> Option.map (fun m -> String.toLower m.System, String.toLower m.AppName)

    let private conflict (field: string) (m: MetaDescriptor) (a: string) (b: string) =
        if a <> "" && b <> "" && a <> b then
            [ $"Conflicting {field} for {m.System}/{m.AppName}: '{a}' and '{b}'; keeping '{a}'" ]
        else []

    let private metaWarnings (x: MetaDescriptor) (y: MetaDescriptor) =
        conflict "description" x x.Description y.Description
        @ conflict "owner" x x.Owner y.Owner
        @ conflict "repository" x x.Repository y.Repository
        @ conflict "tech" x x.Tech y.Tech
        @ conflict "kind" x (Encode.appKind x.Kind) (Encode.appKind y.Kind)

    /// Apps with the same system and app name (case-insensitive) become one: responsibilities and
    /// collaborators are concatenated without duplicates and the first meta wins (conflicting fields are
    /// returned as warnings). Apps without a meta are never merged. Order of first appearance is kept.
    let apps (input: App list) : App list * string list =
        let step (acc: App list, warnings: string list) (app: App) =
            match key app |> Option.bind (fun k -> acc |> List.tryFindIndex (fun x -> key x = Some k)) with
            | None -> acc @ [ app ], warnings
            | Some i ->
                let existing = acc.[i]
                let extra =
                    match existing.Meta, app.Meta with
                    | Some x, Some y -> metaWarnings x y
                    | _ -> []
                let merged =
                    { Meta = existing.Meta
                      Responsibilities = existing.Responsibilities @ app.Responsibilities |> List.distinct
                      Collaborators = existing.Collaborators @ app.Collaborators |> List.distinct }
                acc |> List.mapi (fun j x -> if j = i then merged else x), warnings @ extra
        input |> List.fold step ([], [])
