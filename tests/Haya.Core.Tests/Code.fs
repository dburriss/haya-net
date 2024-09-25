namespace Haya.Core.Tests

open System
open Haya

module Code =
    open System.Text
   
    let empty = StringBuilder()
    let emptyLine (sb:StringBuilder) = sb.AppendLine() 
    let appendLine (line:string) (sb:StringBuilder) = sb.AppendLine(line)
    let usingLine (u:string) (sb:StringBuilder) = sb.AppendLine(sprintf "using %s;" u)
    let nsLine (ns:string) (sb:StringBuilder) = sb.AppendLine(sprintf "namespace %s" ns)
    let openCurly (sb:StringBuilder) = sb.AppendLine("{")
    let closeCurly (sb:StringBuilder) = sb.AppendLine("}")
    let append (lines:string list) (sb:StringBuilder) = lines |> List.fold (fun sb l -> appendLine l sb) sb
    let init (ls: string list) =
        empty |> append ls
    
    let attrString (attr: Attribute) =
        match attr with
        | :? ResponsibilityAttribute as r -> sprintf "[Haya.ResponsibilityAttribute(Description = \"%s\")]" r.Description
        | :? MetaAttribute as m ->
            sprintf "[assembly:Haya.MetaAttribute(AppName = \"%s\", Description = \"%s\", Team = \"%s\", System = \"%s\", Repository = \"%s\")]" m.AppName m.Description m.Team m.System m.Repository
        | :? CollaboratorAttribute as c ->
            sprintf "[Haya.CollaboratorAttribute(Direction = %s, Protocol = \"%s\", DataDescription = \"%s\", Description = \"%s\", AppName = \"%s\", System = \"%s\", Relationship = %s, Repository = \"%s\")]" (c.Direction.ToString()) c.Protocol c.DataDescription c.Description c.AppName c.System (c.Relationship.ToString()) c.Repository
        | _ -> attr.ToString()
        
    let classString (name: string) =
        sprintf "public class %s(){}" name
