module ReferencedAttributeTests

open System
open System.IO
open Haya
open Haya.Core.Analysis
open Haya.Roslyn
open InMemoryAnalysis
open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.CSharp
open Microsoft.CodeAnalysis.Text
open Xunit

// Haya attributes on public types in compiled references are only read when asked for.
// Small C# snippets are compiled in memory, so no MSBuild or NuGet restore is needed.

let private library = compileLibrary

let private analyse = analyseWith

let private libSource = """
using Haya;
[assembly: Meta(AppName = "Lib", System = "Acme")]
namespace Acme.Orders
{
    [Responsibility(Description = "Place orders")]
    [Collaborator(AppName = "Payments", System = "Acme", Protocol = "gRPC")]
    public class OrderService
    {
        [Responsibility(Description = "Nested")]
        public class Inner { }
    }
    [Responsibility(Description = "Internal only")]
    internal class Hidden { }
}
"""

let private appSource = """
using Haya;
[Responsibility(Description = "Own")]
public class Own { }
"""

let private on = { Roslyn.ReferenceOptions.none with Include = true }
let private lib = library "Acme.Orders" true libSource

[<Fact>]
let ``Referenced assemblies are not read by default`` () =
    let a = analyse Roslyn.ReferenceOptions.none [ lib ] appSource
    let r = a.Descriptors |> Descriptor.responsibilities |> List.exactlyOne
    Assert.Equal("Own", r.ComponentName)
    Assert.Empty a.Referenced

[<Fact>]
let ``Public types from references are read when included, with an assembly based source`` () =
    let a = analyse on [ lib ] appSource
    let rs = a.Descriptors |> Descriptor.responsibilities
    Assert.Equal<string list>([ "Inner"; "Own"; "OrderService" ] |> List.sort, rs |> List.map _.ComponentName |> List.sort)
    let order = rs |> List.find (fun r -> r.ComponentName = "OrderService")
    Assert.Equal("Acme.Orders::Acme.Orders.OrderService", order.ComponentSource)
    let c = a.Descriptors |> Descriptor.collaborators |> List.exactlyOne
    Assert.Equal("Payments", c.AppName)
    Assert.Equal("gRPC", c.Protocol)

[<Fact>]
let ``Internal types in references are ignored`` () =
    let a = analyse on [ lib ] appSource
    Assert.DoesNotContain(a.Descriptors |> Descriptor.responsibilities, fun r -> r.ComponentName = "Hidden")

[<Fact>]
let ``The assembly Meta of a reference never becomes the app's Meta`` () =
    let a = analyse on [ lib ] appSource
    Assert.Empty(a.Descriptors |> Descriptor.metas)

[<Fact>]
let ``An assembly glob selects which references are read`` () =
    let hit = analyse { Include = false; AssemblyGlobs = [ "Acme.*" ] } [ lib ] appSource
    Assert.Equal(3, hit.Descriptors |> Descriptor.responsibilities |> List.length)
    let miss = analyse { Include = false; AssemblyGlobs = [ "Other.*" ] } [ lib ] appSource
    Assert.Equal(1, miss.Descriptors |> Descriptor.responsibilities |> List.length)
    Assert.Empty miss.Referenced

[<Fact>]
let ``References that do not use Haya are skipped`` () =
    let plain = library "Plain" false "public class Thing { }"
    let a = analyse on [ plain ] appSource
    Assert.Empty a.Referenced

[<Fact>]
let ``Custom attribute defaults apply to types in references`` () =
    let custom = library "Acme.Custom" true """
using Haya;
[CollaboratorDefaults(AppName = "Payments", Protocol = "gRPC")]
public class CallsPaymentsAttribute : CollaboratorAttribute { }
[CallsPayments]
public class Client { }
"""
    let c = analyse on [ custom ] appSource |> _.Descriptors |> Descriptor.collaborators |> List.exactlyOne
    Assert.Equal("Payments", c.AppName)
    Assert.Equal("Client", c.ComponentName)

[<Fact>]
let ``A referenced component renders as plain text in the CRC`` () =
    let a = analyse on [ lib ] appSource
    let app = App.ofDescriptors a.Descriptors
    let md = Haya.Core.Crc.sprintMarkdown { InputPaths = []; OutputPath = ""; IncludeL1Diagram = false; IncludeL2Diagram = false; CurrentDirectory = "/virtual" } [ app ]
    Assert.Contains("OrderService (Acme.Orders::Acme.Orders.OrderService)", md)
    Assert.Contains("[Own](Source.cs)", md)
