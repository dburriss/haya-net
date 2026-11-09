namespace Haya.Core

/// A minimal, internal markdown document builder. It covers only what Haya renders (headings, paragraphs,
/// tables, fenced code) and uses plain FSharp.Core string functions so it compiles under Fable.
/// Output is LF-only. Text is written as given, except table cells, where `|` and newlines are escaped.
module internal Markdown =

    type Block =
        | Heading of level: int * text: string
        | Paragraph of text: string
        | Table of headers: string list * rows: string list list
        | CodeBlock of language: string option * code: string
        | BlankLine

    /// Blocks are kept newest first and reversed when rendering.
    type Document = private { Blocks: Block list }

    let empty = { Blocks = [] }

    let private add (block: Block) (doc: Document) = { Blocks = block :: doc.Blocks }

    let addHeading (level: int) (text: string) = add (Heading (level, text))
    let addH1 (text: string) = addHeading 1 text
    let addH2 (text: string) = addHeading 2 text
    let addParagraph (text: string) = add (Paragraph text)
    let addTable (headers: string list) (rows: string list list) = add (Table (headers, rows))
    let addCode (language: string option) (code: string) = add (CodeBlock (language, code))
    let addBlankLine (doc: Document) = add BlankLine doc

    /// Escapes table cell text: a literal pipe would end the cell, and a newline would end the row.
    let private escapeCell (text: string) =
        text.Replace("|", "\\|").Replace("\r\n", "<br>").Replace("\n", "<br>")

    let private row (cells: string list) = "|" + String.concat "|" (List.map escapeCell cells) + "|\n"

    let private renderBlock (block: Block) =
        match block with
        | Heading (level, text) -> String.replicate level "#" + " " + text + "\n\n"
        | Paragraph text -> text + "\n\n"
        | Table (headers, rows) ->
            let divider = headers |> List.map (fun _ -> "---")
            String.concat "" (row headers :: row divider :: List.map row rows)
        | CodeBlock (language, code) -> "```" + Option.defaultValue "" language + "\n" + code + "\n```\n"
        | BlankLine -> "\n"

    let render (doc: Document) =
        doc.Blocks |> List.rev |> List.map renderBlock |> String.concat ""
