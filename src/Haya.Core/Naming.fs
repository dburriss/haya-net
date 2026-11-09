namespace Haya.Core

open System.Text.RegularExpressions

/// Identifiers for apps and systems, shared so that the renderers agree on what an app is called.
module Naming =
    let toKebabCase (input: string) =
        let pattern = @"([a-z])([A-Z])"
        let replacement = "$1-$2"
        Regex.Replace(input, pattern, replacement).ToLower().Replace(" ", "-")

    /// `system-app` in kebab case; just the app when there is no system.
    let createName (systemName: string) (appName: string) =
        let systemId = toKebabCase systemName
        let appId = toKebabCase appName
        if systemId = "" then appId else $"{systemId}-{appId}"

    /// An identifier that is safe to use as a Mermaid alias: lower case letters, digits and underscores.
    let mermaidId (name: string) =
        Regex.Replace(toKebabCase name, "[^a-z0-9]+", "_")
