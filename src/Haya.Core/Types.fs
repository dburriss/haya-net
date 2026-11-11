namespace Haya.Core

open System.Text

type DataFormat = | Json | Yaml 

type DescribeCommand = {
    InputPaths: string list
    OutputPath: string
    Format: DataFormat
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

type DiagramType = | C4System | C4Container | C4Component
type DiagramFormat = | Mermaid | Html
type DiagramCommand = {
    InputPaths: string list
    OutputPath: string
    DiagramType: DiagramType
    DiagramFormat: DiagramFormat
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
