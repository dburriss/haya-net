namespace Haya.Core

open Flexi.Json
open Haya.Core.Analysis

/// Explicit `JsonValue` encoders for the descriptor model. Property names are camelCase;
/// these are the shapes written by `describe`.
module Encode =

    let private str (s: string) = JsonValue.String s

    let private strings (xs: string list) = xs |> List.map str |> List.toArray |> JsonValue.Array

    let private record (props: (string * JsonValue) list) = props |> List.toArray |> JsonValue.Record

    let direction (d: Direction) =
        match d with
        | Direction.Upstream -> "Upstream"
        | _ -> "Downstream"

    let relationship (r: Relationship) =
        match r with
        | Relationship.Owned -> "Owned"
        | Relationship.Shared -> "Shared"
        | Relationship.Internal -> "Internal"
        | Relationship.External -> "External"
        | Relationship.InternalUser -> "InternalUser"
        | _ -> "ExternalUser"

    let responsibility (x: ResponsibilityDescriptor) =
        record [
            "description", str x.Description
            "componentName", str x.ComponentName
            "componentSource", str x.ComponentSource
        ]

    let collaborator (x: CollaboratorDescriptor) =
        record [
            "direction", str (direction x.Direction)
            "tech", str x.Tech
            "protocol", str x.Protocol
            "dataDescription", str x.DataDescription
            "description", str x.Description
            "appName", str x.AppName
            "system", str x.System
            "relationship", str (relationship x.Relationship)
            "componentName", str x.ComponentName
            "componentSource", str x.ComponentSource
            "repository", str x.Repository
            "owner", str x.Owner
        ]

    let meta (x: MetaDescriptor) =
        record [
            "appName", str x.AppName
            "description", str x.Description
            "system", str x.System
            "repository", str x.Repository
            "owner", str x.Owner
        ]

    /// The document written by `describe`. `meta` is omitted when there is none.
    let description (version: string) (descriptors: Descriptor list) =
        record [
            "collaborators", descriptors |> Descriptor.collaborators |> List.map collaborator |> List.toArray |> JsonValue.Array
            match descriptors |> Descriptor.metas |> List.tryHead with
            | Some m -> "meta", meta m
            | None -> ()
            "responsibilities", descriptors |> Descriptor.responsibilities |> List.map responsibility |> List.toArray |> JsonValue.Array
            "version", str version
        ]

    /// Backstage `Component` entity.
    let backstageComponent
        (name: string) (title: string) (description: string)
        (owner: string) (domain: string)
        (dependsOn: string list) (dependencyOf: string list) =
        record [
            "apiVersion", str "backstage.io/v1alpha1"
            "dependencyOf", strings dependencyOf
            "dependsOn", strings dependsOn
            "kind", str "Component"
            "metadata", record [ "description", str description; "name", str name; "title", str title ]
            "spec", record [ "domain", str domain; "lifecycle", str "production"; "owner", str owner ]
        ]
