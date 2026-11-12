module MemberTests

open Haya.Core
open Haya.Core.Analysis
open Haya.Roslyn
open InMemoryAnalysis
open Xunit

// `--detail members` (ADR017): the constructors and public members of each annotated class.

let private source = """
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Haya;

namespace Shop;

public class Base
{
    public void Inherited() { }
}

public record Order(string Id, int Total);

[Responsibility(Description = "Takes payments")]
public class Payments : Base
{
    private readonly Repo _repo;
    public Payments(Repo repo) { _repo = repo; }
    public Payments(Repo repo, string name) : this(repo) { }
    internal Payments() { }
    protected Payments(int x) { }

    public string Name { get; set; } = "";
    public int Count;
    public event EventHandler? Paid;
    public string this[int index] => "";
    public Task<Order> Pay(Order order, int amount) => Task.FromResult(order);
    public static Payments Create(Repo repo) => new Payments(repo);
    public void Pay(string id) { }
    private void Hidden() { }
    internal void AlsoHidden() { }
    public override string ToString() => "";
    public override bool Equals(object? obj) => false;
    public override int GetHashCode() => 0;
    public static Payments operator +(Payments a, Payments b) => a;
    public class Nested { }
}

[Collaborator(AppName = "Db", Description = "stores")]
public class Repo
{
}

[Responsibility(Description = "A record")]
public record Receipt(string Id);

public class NotAnnotated
{
    public NotAnnotated(Repo repo) { }
}
"""

let private analysis members = analyseWithMembers members Roslyn.ReferenceOptions.none [] source

let private find name (cs: ComponentDetail list) = cs |> List.find (fun c -> c.Name = name)

let private normalised members =
    (App.ofDescriptors (analysis members).Descriptors |> App.withComponents (analysis members).Components).Components

[<Fact>]
let ``Below members no class details are collected`` () =
    Assert.Empty((analysis false).Components)

[<Fact>]
let ``Only annotated classes get details`` () =
    let names = normalised true |> List.map (_.Name) |> List.sort
    Assert.Equal<string list>([ "Payments"; "Repo" ], names)

[<Fact>]
let ``Public constructors with their parameters in order, by parameter count`` () =
    let p = normalised true |> find "Payments"
    let ctors = p.Constructors |> List.map (List.map (fun x -> x.Name, x.Type))
    Assert.Equal<(string * string) list list>([ [ "repo", "Repo" ]; [ "repo", "Repo"; "name", "string" ] ], ctors)

[<Fact>]
let ``The implicit constructor is left out`` () =
    let repo = normalised true |> find "Repo"
    Assert.Empty(repo.Constructors)
    Assert.Empty(repo.Members)

[<Fact>]
let ``Public members declared on the class, in kind, name, signature order`` () =
    let p = normalised true |> find "Payments"
    let members = p.Members |> List.map (fun m -> m.Kind, m.Name, m.Type)
    let expected =
        [ MemberKind.Method, "Create", "Payments"
          // same name: by signature, `id: string` before `order: Order, …`
          MemberKind.Method, "Pay", "void"
          MemberKind.Method, "Pay", "Task<Order>"
          MemberKind.Property, "Name", "string"
          MemberKind.Property, "this[]", "string"
          MemberKind.Field, "Count", "int"
          MemberKind.Event, "Paid", "EventHandler?" ]
    Assert.Equal<(MemberKind * string * string) list>(expected, members)
    let pay = p.Members |> List.find (fun m -> m.Type = "Task<Order>")
    Assert.Equal<(string * string) list>([ "order", "Order"; "amount", "int" ], pay.Parameters |> List.map (fun x -> x.Name, x.Type))

[<Fact>]
let ``Record members the compiler writes are left out`` () =
    // the analyser finds classes only, so read the record's symbol straight from a compilation
    let compilation =
        Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(
            "Records",
            [ Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText "public record Receipt(string Id);" ],
            platformReferences,
            Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary))
    let r = Roslyn.componentDetail "Receipt" "x.cs" (compilation.GetTypeByMetadataName "Receipt") |> ComponentDetail.normalise
    Assert.Equal<(string * string) list list>([ [ "Id", "string" ] ], r.Constructors |> List.map (List.map (fun x -> x.Name, x.Type)))
    Assert.Equal<string list>([ "Id" ], r.Members |> List.map (_.Name))

[<Fact>]
let ``Order does not depend on the order the analyser found things in`` () =
    let p = normalised true |> find "Payments"
    let shuffled = { p with Constructors = List.rev p.Constructors; Members = List.rev p.Members } |> ComponentDetail.normalise
    Assert.Equal(p, shuffled)

// ---------------------------------------------------------------- describe

let private meta : MetaDescriptor =
    { AppName = "Api"; Description = ""; System = "Shop"; Repository = ""; Owner = ""; Tech = ""; Kind = AppKind.Unspecified; Lifecycle = ""; Tier = "" }

let private responsibility name : ResponsibilityDescriptor = { Description = $"{name} work"; ComponentName = name; ComponentSource = "src/x.cs" }

let private detail name (ctor: (string * string) list) : ComponentDetail =
    { Name = name
      Source = "src/x.cs"
      Constructors = if ctor.IsEmpty then [] else [ ctor |> List.map (fun (n, t) -> { Name = n; Type = t }) ]
      Members =
        [ { Name = "Run"; Kind = MemberKind.Method; Type = "Task<bool>"; Parameters = [ { Name = "id"; Type = "string" } ] }
          { Name = "Size"; Kind = MemberKind.Property; Type = "int"; Parameters = [] } ]
      Dependencies = [] }

let private dependsOn name via : ComponentDependency = { Name = name; Source = "src/x.cs"; Via = via }

let private shopApp =
    App.ofDescriptors [ Meta meta; Responsibility (responsibility "Controller"); Responsibility (responsibility "Usecase"); Responsibility (responsibility "Repo") ]
    |> App.withComponents
        [ { detail "Controller" [ "usecase", "Usecase"; "self", "Controller" ] with Dependencies = [ dependsOn "Usecase" [] ] }
          { detail "Usecase" [ "repo", "IRepo" ] with Dependencies = [ dependsOn "Repo" [ "IRepo"; "SqlRepo" ]; dependsOn "Missing" [] ] }
          detail "Repo" [] ]

let private describe format detail apps =
    Describe.serialize { InputPaths = []; OutputPath = ""; Format = format; Detail = detail; CurrentDirectory = "" } apps

[<Theory>]
[<InlineData("x.json")>]
[<InlineData("x.yaml")>]
let ``Describe at members round-trips the classes`` (file: string) =
    let format = if file.EndsWith ".yaml" then DataFormat.Yaml else DataFormat.Json
    let text = describe format Detail.Members [ shopApp ]
    match Decode.apps file text with
    | Ok [ decoded ] -> Assert.Equal(shopApp, decoded)
    | other -> failwith $"%A{other}"

[<Fact>]
let ``Describe writes detail and leaves the classes out below members`` () =
    let apps = describe DataFormat.Json Detail.Apps [ shopApp ]
    Assert.Contains("\"detail\": \"apps\"", apps)
    Assert.Contains("\"version\": \"0.6\"", apps)
    Assert.DoesNotContain("\"components\"", apps)
    Assert.Contains("\"detail\": \"components\"", describe DataFormat.Json Detail.Components [ shopApp ])
    let members = describe DataFormat.Json Detail.Members [ shopApp ]
    Assert.Contains("\"detail\": \"members\"", members)
    Assert.Contains("\"components\"", members)

[<Theory>]
[<InlineData("""{"collaborators":[],"meta":{"appName":"A","description":"","system":"S","repository":"","owner":""},"responsibilities":[]}""")>]
[<InlineData("""{"apps":[{"collaborators":[],"meta":{"appName":"A","description":"","system":"S","repository":"","owner":""},"responsibilities":[]}],"version":"0.1"}""")>]
[<InlineData("""{"apps":[{"collaborators":[],"meta":{"appName":"A","description":"","system":"S","repository":"","owner":"","tech":"x","kind":"Api"},"responsibilities":[]}],"version":"0.2"}""")>]
[<InlineData("""{"apps":[{"collaborators":[],"language":"dotnet","meta":{"appName":"A","description":"","system":"S","repository":"","owner":"","tier":"1"},"responsibilities":[]}],"version":"0.3"}""")>]
[<InlineData("""{"apps":[{"collaborators":[{"direction":"Upstream","appName":"Customer","kind":"Person","relationship":"External"}],"meta":{"appName":"A","description":"","system":"S","repository":"","owner":""},"responsibilities":[]}],"version":"0.4"}""")>]
let ``Older describe documents decode without classes`` (doc: string) =
    match Decode.apps "x.json" doc with
    | Ok [ a ] ->
        Assert.Equal("A", (App.meta a).AppName)
        Assert.Empty(a.Components)
    | other -> failwith $"%A{other}"

[<Fact>]
let ``Merge keeps the classes of both inputs once`` () =
    let merged, _ = Merge.apps [ shopApp; shopApp ]
    Assert.Equal(3, merged.Head.Components.Length)

// ---------------------------------------------------------------- graph

let private componentsAt detail =
    (Graph.ofApps detail [ shopApp ]).Nodes |> List.collect (_.Components)

[<Fact>]
let ``DependsOn resolves to the classes of the app and UsedBy is its inverse`` () =
    let cs = componentsAt Detail.Members
    let get name = cs |> List.find (fun c -> c.Name = name)
    Assert.Equal<Graph.ComponentLink list>([ ({ Id = "shop_api__usecase"; Via = [] } : Graph.ComponentLink) ], (get "Controller").DependsOn)
    // a dependency on a class that is not a component of the app is dropped
    Assert.Equal<Graph.ComponentLink list>([ ({ Id = "shop_api__repo"; Via = [ "IRepo"; "SqlRepo" ] } : Graph.ComponentLink) ], (get "Usecase").DependsOn)
    Assert.Empty((get "Repo").DependsOn)
    Assert.Equal<string list>([ "shop_api__usecase" ], (get "Repo").UsedBy)
    Assert.Equal<string list>([ "shop_api__controller" ], (get "Usecase").UsedBy)
    Assert.Empty((get "Controller").UsedBy)

[<Fact>]
let ``Graph fills classes from components and their members only at members`` () =
    Assert.Empty(componentsAt Detail.Systems)
    Assert.Empty(componentsAt Detail.Apps)
    let components = componentsAt Detail.Components
    Assert.Equal(3, components.Length)
    Assert.All(components, fun c ->
        Assert.Empty(c.Constructors)
        Assert.Empty(c.Members)
        Assert.Empty(c.DependsOn)
        Assert.Empty(c.UsedBy))
    let members = componentsAt Detail.Members
    Assert.Equal(3, members.Length)
    Assert.All(members, fun c -> Assert.Equal(2, c.Members.Length))

[<Fact>]
let ``Graph document carries constructors, dependencies and members only at members`` () =
    let json detail = Graph.ofApps detail [ shopApp ] |> Encode.graph |> Serializer.toScriptSafeJson
    Assert.DoesNotContain("\"members\"", json Detail.Components)
    Assert.DoesNotContain("\"dependsOn\"", json Detail.Components)
    Assert.DoesNotContain("\"usedBy\"", json Detail.Components)
    let full = json Detail.Members
    Assert.Contains("\"members\"", full)
    Assert.Contains("\"constructors\"", full)
    Assert.Contains("\"dependsOn\"", full)
    Assert.Contains("\"usedBy\"", full)
    Assert.Contains("\"version\": \"0.5\"", full)

[<Fact>]
let ``The component diagram draws dependencies between classes only at members`` () =
    let gen detail = Diagram.generateDiagram { InputPaths = []; OutputPath = ""; Detail = detail; DiagramFormat = DiagramFormat.Mermaid; Viewer = ViewerSettings.none; CurrentDirectory = "" } [ shopApp ]
    let uses = [ "Rel(shop_api__controller, shop_api__usecase, \"uses\")"; "Rel(shop_api__usecase, shop_api__repo, \"uses\")" ]
    Assert.DoesNotContain("\"uses\"", gen Detail.Components)
    let members = gen Detail.Members
    for line in uses do Assert.Contains(line, members)
    // nothing else changes
    Assert.Equal(gen Detail.Components, members.Split('\n') |> Array.filter (fun l -> not (List.contains l uses)) |> String.concat "\n")
