module DependencyTests

open Haya.Core
open Haya.Core.Analysis
open Haya.Roslyn
open InMemoryAnalysis
open Xunit

// `--detail members` (ADR018): the annotated classes each annotated class reaches in code, through unannotated types.

// ---------------------------------------------------------------- the walk (Haya.Core, shared by both analysers)

let private node name = { Key = name; Name = name; Source = "x.cs"; Abstract = false }
let private abstractNode name = { node name with Abstract = true }
let private uses a b = { From = a; To = b; Inherits = false }
let private inherits a b = { From = a; To = b; Inherits = true }
let private comp name : ComponentDetail = { Name = name; Source = "x.cs"; Constructors = []; Members = []; Dependencies = [] }

/// The dependencies of every component, as `from -> to [via]`.
let private walk (nodes: TypeNode list) (edges: TypeEdge list) (annotated: string list) =
    Dependencies.attach nodes edges (annotated |> List.map comp)
    |> List.collect (fun c -> c.Dependencies |> List.map (fun d -> c.Name, d.Name, d.Via))

[<Fact>]
let ``A direct reference is a dependency with no via`` () =
    Assert.Equal<(string * string * string list) list>([ "A", "B", [] ], walk [ node "A"; node "B" ] [ uses "A" "B" ] [ "A"; "B" ])

[<Fact>]
let ``A chain through unannotated types records them in order`` () =
    let nodes = [ node "Controller"; node "PaymentService"; node "CardGateway"; node "Visa" ]
    let edges = [ uses "Controller" "PaymentService"; uses "PaymentService" "CardGateway"; uses "CardGateway" "Visa" ]
    Assert.Equal<(string * string * string list) list>(
        [ "Controller", "Visa", [ "PaymentService"; "CardGateway" ] ],
        walk nodes edges [ "Controller"; "Visa" ])

[<Fact>]
let ``An inbound entry reaches a responsibility the same way`` () =
    let nodes = [ node "Entry"; node "Handler"; node "Usecase" ]
    Assert.Equal<(string * string * string list) list>(
        [ "Entry", "Usecase", [ "Handler" ] ],
        walk nodes [ uses "Entry" "Handler"; uses "Handler" "Usecase" ] [ "Entry"; "Usecase" ])

[<Fact>]
let ``Using an interface reaches its implementations, implementing one does not link siblings`` () =
    let nodes = [ node "Entry"; abstractNode "IFlow"; node "CardFlow"; node "CashFlow"; node "Usecase"; node "Till" ]
    let edges =
        [ uses "Entry" "IFlow"
          inherits "CardFlow" "IFlow"; uses "CardFlow" "Usecase"
          inherits "CashFlow" "IFlow"; uses "CashFlow" "Till" ]
    let result = walk nodes edges [ "Entry"; "Usecase"; "Till"; "CardFlow" ]
    Assert.Contains(("Entry", "CardFlow", [ "IFlow" ]), result)
    Assert.Contains(("Entry", "Till", [ "IFlow"; "CashFlow" ]), result)
    // CardFlow implements IFlow: that does not make it reach CashFlow (and so Till)
    Assert.DoesNotContain(("CardFlow", "Till", [ "IFlow"; "CashFlow" ]), result)
    Assert.Contains(("CardFlow", "Usecase", []), result)

[<Fact>]
let ``Extending an abstract class reaches what the base uses`` () =
    let nodes = [ node "Job"; abstractNode "Base"; node "Store" ]
    Assert.Equal<(string * string * string list) list>(
        [ "Job", "Store", [ "Base" ] ],
        walk nodes [ inherits "Job" "Base"; uses "Base" "Store" ] [ "Job"; "Store" ])

[<Fact>]
let ``A cycle is walked once`` () =
    let nodes = [ node "A"; node "P"; node "Q"; node "B" ]
    let edges = [ uses "A" "P"; uses "P" "Q"; uses "Q" "P"; uses "Q" "A"; uses "Q" "B" ]
    Assert.Equal<(string * string * string list) list>([ "A", "B", [ "P"; "Q" ] ], walk nodes edges [ "A"; "B" ])

[<Fact>]
let ``The walk stops at the first annotated class`` () =
    let nodes = [ node "A"; node "B"; node "C" ]
    let result = walk nodes [ uses "A" "B"; uses "B" "C" ] [ "A"; "B"; "C" ]
    Assert.Equal<(string * string * string list) list>([ "A", "B", []; "B", "C", [] ], result)

[<Fact>]
let ``Never a dependency on itself`` () =
    let nodes = [ node "A"; node "Helper" ]
    Assert.Empty(walk nodes [ uses "A" "A"; uses "A" "Helper"; uses "Helper" "A" ] [ "A" ])

[<Fact>]
let ``Types that are not nodes are not walked`` () =
    // `Task` is not declared in the source: the edge is dropped, and nothing past it is reached
    Assert.Empty(walk [ node "A"; node "B" ] [ uses "A" "Task"; uses "Task" "B" ] [ "A"; "B" ])

[<Fact>]
let ``The shortest chain wins, ties broken by name, and the order is deterministic`` () =
    let nodes = [ node "A"; node "Long1"; node "Long2"; node "Mid"; node "Alt"; node "B"; node "C" ]
    let edges =
        [ uses "A" "Long1"; uses "Long1" "Long2"; uses "Long2" "B"
          uses "A" "Mid"; uses "Mid" "B"; uses "A" "Alt"; uses "Alt" "B"
          uses "A" "C" ]
    let expected = [ "A", "B", [ "Alt" ]; "A", "C", [] ]
    Assert.Equal<(string * string * string list) list>(expected, walk nodes edges [ "A"; "B"; "C" ])
    Assert.Equal<(string * string * string list) list>(expected, walk (List.rev nodes) (List.rev edges) [ "C"; "B"; "A" ] |> List.sortBy (fun (a, _, _) -> a))

// ---------------------------------------------------------------- Roslyn: what is an edge

let private source = """
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Haya;

namespace Shop;

[Responsibility(Description = "Entry")]
public class Entry
{
    public void Run()
    {
        var a = new ByNew();
        ByStatic.Go();
        var t = typeof(ByTypeof);
        var n = nameof(ByNameof);
        List<ByGeneric> list = new();
        ByImplicit i = new();
    }
    public Task<ByReturn> Get() => null!;
    public void Take(ByParameter p) { }
    public ByProperty Prop { get; set; } = null!;
    public void Call(Holder h) => h.Member.Hit();
    public class Nested { public void N(ByNested x) { } }
}

[Responsibility(Description = "Marked")]
[Marker(typeof(ByAttribute))]
public class Attributed { }

public class MarkerAttribute(Type t) : Attribute { }

public class ByNew { public Target T = null!; }
public static class ByStatic { public static void Go() { Target.Ping(); } }
public class ByTypeof { Target T = null!; }
public class ByNameof { Target T = null!; }
public class ByGeneric { Target T = null!; }
public class ByImplicit { Target T = null!; }
public class ByReturn { Target T = null!; }
public class ByParameter { Target T = null!; }
public class ByProperty { Target T = null!; }
public class Holder { public Inner Member = null!; }
public class Inner { public void Hit() { Target.Ping(); } }
public class ByNested { Target T = null!; }
public class ByAttribute { Other O = null!; }

[Responsibility(Description = "Target")]
public class Target { public static void Ping() { } }

[Responsibility(Description = "Other")]
public class Other { }

[Responsibility(Description = "Uses the framework only")]
public class FrameworkOnly
{
    private readonly List<string> _xs = new();
    private readonly IServiceProvider _sp = null!;
    public Task Run() => Task.CompletedTask;
}
"""

let private components () =
    let a = analyseWithMembers true Roslyn.ReferenceOptions.none [] source
    App.ofDescriptors a.Descriptors |> App.withComponents a.Components |> _.Components

let private depsOf name =
    components () |> List.find (fun c -> c.Name = name) |> _.Dependencies |> List.map (fun d -> d.Name, d.Via)

[<Fact>]
let ``Roslyn finds dependencies through new, static calls, typeof, nameof, generics, returns, parameters, members and nested types`` () =
    let deps = depsOf "Entry"
    Assert.Equal<string list>([ "Target" ], deps |> List.map fst)
    // the shortest of the many chains, tie-broken by name
    Assert.Equal<string list>([ "ByGeneric" ], deps |> List.head |> snd)

[<Fact>]
let ``Roslyn reaches each kind of reference`` () =
    // each `By*` class reaches Target on its own; checked by making each the only route
    let single (route: string) =
        let marked =
            source
                .Replace("public class " + route + " ", "[Responsibility(Description = \"x\")] public class " + route + " ")
                .Replace("public static class " + route + " ", "[Responsibility(Description = \"x\")] public static class " + route + " ")
        let a = analyseWithMembers true Roslyn.ReferenceOptions.none [] marked
        App.ofDescriptors a.Descriptors |> App.withComponents a.Components |> _.Components
        |> List.find (fun c -> c.Name = "Entry") |> _.Dependencies |> List.map _.Name
    for route in [ "ByNew"; "ByStatic"; "ByTypeof"; "ByNameof"; "ByGeneric"; "ByImplicit"; "ByReturn"; "ByParameter"; "ByProperty"; "Inner"; "ByNested" ] do
        Assert.Contains(route, single route)

[<Fact>]
let ``Attributes are not dependencies`` () =
    Assert.Empty(depsOf "Attributed")

[<Fact>]
let ``Framework types are not walked`` () =
    Assert.Empty(depsOf "FrameworkOnly")

[<Fact>]
let ``Annotated types from referenced assemblies get no dependencies`` () =
    let lib = compileLibrary "Acme.Lib" true """
using Haya;
namespace Acme;
[Responsibility(Description = "Lib")]
public class LibService { }
"""
    let app = """
using Haya;
[Responsibility(Description = "Own")]
public class Own { public Acme.LibService S = null!; }
"""
    let a = analyseWithMembers true { Roslyn.ReferenceOptions.none with Include = true } [ lib ] app
    let cs = App.ofDescriptors a.Descriptors |> App.withComponents a.Components |> _.Components
    Assert.Equal(2, cs.Length)
    Assert.All(cs, fun c -> Assert.Empty(c.Dependencies))

[<Fact>]
let ``No type graph below members`` () =
    let a = analyseWithMembers false Roslyn.ReferenceOptions.none [] source
    Assert.True(a.Dependencies.IsNone)

// ---------------------------------------------------------------- the example solution

let private exampleComponents =
    lazy (
        task {
            let! sln = "../../../../../examples/dotnet/Example.sln" |> Roslyn.openSolutionWith [ "Configuration", DescriptorTests.configuration ]
            let! a = sln |> Descriptors.getAnalysisWithMembers true Roslyn.ReferenceOptions.none "" Descriptors.attributeNames
            return App.ofDescriptors a.Descriptors |> App.withComponents a.Components |> _.Components
        } |> Async.AwaitTask |> Async.RunSynchronously)

[<Fact>]
let ``The example's dependencies`` () =
    let deps =
        exampleComponents.Value
        |> List.collect (fun c -> c.Dependencies |> List.map (fun d -> c.Name, d.Name, d.Via))
    let expected =
        [ "CreditCardPaymentUsecase", "ShippingService", []
          "CreditCardPaymentUsecase", "VisaPaymentService", [ "CardGateway" ]
          "CustomerPayment", "CreditCardPaymentUsecase", [ "IPaymentFlow"; "CardPaymentFlow" ]
          "PaymentsController", "CreditCardPaymentUsecase", []
          "PaymentsController", "VisaPaymentService", [ "PaymentService"; "CardGateway" ] ]
    Assert.Equal<(string * string * string list) list>(expected, deps |> List.sort)

// ---------------------------------------------------------------- describe

let private app =
    App.ofDescriptors [ Responsibility { Description = "x"; ComponentName = "A"; ComponentSource = "x.cs" } ]
    |> App.withComponents
        [ { comp "A" with Dependencies = [ { Name = "C"; Source = "x.cs"; Via = [] }; { Name = "B"; Source = "x.cs"; Via = [ "P"; "Q" ] } ] }
          comp "B"
          comp "C" ]

[<Theory>]
[<InlineData("x.json")>]
[<InlineData("x.yaml")>]
let ``Describe 0.6 round-trips dependencies`` (file: string) =
    let format = if file.EndsWith ".yaml" then DataFormat.Yaml else DataFormat.Json
    let text = Describe.serialize { InputPaths = []; OutputPath = ""; Format = format; Detail = Detail.Members; CurrentDirectory = "" } [ app ]
    Assert.Contains("0.6", text)
    Assert.Contains("dependencies", text)
    match Decode.apps file text with
    | Ok [ decoded ] -> Assert.Equal(app, decoded)
    | other -> failwith $"%A{other}"

[<Fact>]
let ``Describe 0.5 decodes with no dependencies`` () =
    let doc = """{"apps":[{"collaborators":[],"components":[{"constructors":[],"members":[],"name":"A","source":"x.cs"}],"responsibilities":[{"description":"x","componentName":"A","componentSource":"x.cs"}]}],"detail":"members","version":"0.5"}"""
    match Decode.apps "x.json" doc with
    | Ok [ a ] ->
        let c = a.Components |> List.exactlyOne
        Assert.Equal("A", c.Name)
        Assert.Empty(c.Dependencies)
    | other -> failwith $"%A{other}"

[<Fact>]
let ``Dependencies are left out of describe when empty and below members`` () =
    let at detail = Describe.serialize { InputPaths = []; OutputPath = ""; Format = DataFormat.Json; Detail = detail; CurrentDirectory = "" } [ app ]
    Assert.DoesNotContain("dependencies", at Detail.Components)
    let members = at Detail.Members
    // A has dependencies, B and C have none and leave the field out
    Assert.Equal(1, members.Split("\"dependencies\"").Length - 1)
