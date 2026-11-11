namespace Haya.Core

open Haya.Core.Analysis

/// The interactive html graph explorer: one self-contained page with the viewer inlined and the graph embedded as JSON.
module Explore =

    let private titleToken = "@@HAYA_TITLE@@"
    let private dataToken = "@@HAYA_DATA@@"

    let escapeHtml (s: string) =
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;")

    /// The page for a graph. The title goes into the head (before the data) and the data is not scanned for tokens, so
    /// neither can inject into the other.
    let render (title: string) (graph: Graph.Graph) =
        let json = graph |> Encode.graph |> Serializer.toScriptSafeJson
        let template = ViewerAssets.template
        let at = template.IndexOf dataToken
        let before = template.Substring(0, at).Replace(titleToken, escapeHtml title)
        let after = template.Substring(at + dataToken.Length)
        before + json + after

    /// The page for the apps. The title is short on purpose: the systems are in the graph itself.
    /// The classes are on the page from `Detail.Components`; their constructors, members and dependencies at `Detail.Members`.
    let sprintHtml (detail: Detail) (apps: App list) = render "Haya Explorer" (Graph.ofApps detail apps)
