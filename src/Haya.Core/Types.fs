namespace Haya.Core

open System.Text

type DataFormat = | Json | Yaml 

/// How much of the graph a command shows: systems, the apps inside them, the classes inside the apps, or also the
/// constructors and public members of those classes. Each level includes the ones before it (ADR016, ADR017).
type Detail = | Systems | Apps | Components | Members

module Detail =
    let private rank (d: Detail) =
        match d with
        | Systems -> 0
        | Apps -> 1
        | Components -> 2
        | Members -> 3

    /// True when `detail` includes `level`.
    let atLeast (level: Detail) (detail: Detail) = rank detail >= rank level

    /// The name used on the command line and in the describe document.
    let name (d: Detail) =
        match d with
        | Systems -> "systems"
        | Apps -> "apps"
        | Components -> "components"
        | Members -> "members"

/// Settings of the html explorer from the configuration (ADR020). Every field is optional: the page keeps its own default
/// for what is not set. `Systems` and `Links` are the four thresholds of the complexity colours.
type ViewerSettings = { Detail: Detail option; Systems: int list option; Links: int list option }

module ViewerSettings =
    let none : ViewerSettings = { Detail = None; Systems = None; Links = None }

type DescribeCommand = {
    InputPaths: string list
    OutputPath: string
    Format: DataFormat
    /// `Members` adds each annotated class's constructors and public members; lower levels add nothing.
    Detail: Detail
    CurrentDirectory: string
}

type CrcCommand = {
    InputPaths: string list
    OutputPath: string
    IncludeL1Diagram: bool
    IncludeL2Diagram: bool
    CurrentDirectory: string
}

type BackstageCommand = {
    InputPaths: string list
    OutputPath: string
    Format: DataFormat
    CurrentDirectory: string
}

type DatadogCommand = {
    InputPaths: string list
    OutputPath: string
    Format: DataFormat
    CurrentDirectory: string
}

type DiagramFormat = | Mermaid | Html
type DiagramCommand = {
    InputPaths: string list
    OutputPath: string
    Detail: Detail
    DiagramFormat: DiagramFormat
    /// Only read for `DiagramFormat.Html`.
    Viewer: ViewerSettings
    CurrentDirectory: string
}

type SB = StringBuilder
module SB =
    let empty = StringBuilder()
    let create (s: string) = StringBuilder(s)
    let append (s: string) (sb: StringBuilder) = sb.Append(s)
    let line (s: string) (sb: StringBuilder) = sb.AppendLine(s)
    let lines (ss: string list) (sb: StringBuilder) = ss |> List.fold (fun sb ss -> line ss sb) sb
    let emptyLine (sb: StringBuilder) = sb.AppendLine()
    let toString (sb: StringBuilder) = sb.ToString()
