namespace Haya.Core

open System.Text

type DataFormat = | Json | Yaml 

type DescribeCommand = {
    PathToSln: string
    OutputPath: string
    Format: DataFormat
    CurrentDirectory: string
}

type CrcCommand = {
    PathToSln: string
    OutputPath: string
    IncludeL1Diagram: bool
    IncludeL2Diagram: bool
    CurrentDirectory: string
}

type BackstageCommand = {
    PathToSln: string
    OutputPath: string
    Format: DataFormat
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

module IO =
    open System.IO
    let write file data =
        // TODO: check file extension
        File.WriteAllText(file, data)
        Ok(file)
        
    let fileExists file = File.Exists(file)
