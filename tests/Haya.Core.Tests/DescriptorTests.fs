module DescriptorTests

open Example
open Haya
open Haya.Core.Analysis
open Haya.Roslyn
open Xunit

// Open the example in the configuration these tests were built with, so the
// F# Haya project reference resolves to an assembly that exists (e.g. in clean Release CI builds).
let configuration =
#if DEBUG
    "Debug"
#else
    "Release"
#endif

let allDescriptors = task {
                        let! sln = "../../../../../examples/dotnet/Example.sln" |> Roslyn.openSolutionWith ["Configuration", configuration]
                        return! sln |> Descriptors.getDescriptors "" Descriptors.attributeNames
                        } |> Async.AwaitTask |> Async.RunSynchronously

let filterToComponentName (componentName: string) (descriptors: Descriptor list) =
    descriptors |> List.filter (function
                                        | Responsibility r -> r.ComponentName = componentName
                                        | Collaborator c -> c.ComponentName = componentName
                                        | _ -> false)

[<Fact>]
let ``Can find solution for tests`` () =
    let path = "../../../../../examples/dotnet/Example.sln"
    let exists = System.IO.File.Exists(path)
    Assert.True(exists)

[<Fact>]
let ``Descriptors returned for ResponsibilityAttribute`` () = task {
    let descriptor = allDescriptors |> filterToComponentName (nameof(CreditCardPaymentUsecase)) |> Descriptor.responsibilities |> List.head
    Assert.Equal("Handles order processing", descriptor.Description)
    Assert.Equal(nameof(CreditCardPaymentUsecase), descriptor.ComponentName)
    Assert.Contains("HayaEcomm.cs", descriptor.ComponentSource)
}

[<Fact>]
let ``Descriptors returned for CollaboratorAttribute`` () = task {
    let descriptor = allDescriptors |> filterToComponentName (nameof(PaymentsController)) |> Descriptor.collaborators |> List.head
    Assert.Equal(Direction.Upstream, descriptor.Direction)
    Assert.Equal("TypeScript", descriptor.Tech)
    Assert.Equal("HTTPS", descriptor.Protocol)
    Assert.Equal("Request payment", descriptor.DataDescription)
    Assert.Equal("Handles cart checkout for a customer", descriptor.Description)
    Assert.Equal("Checkout", descriptor.AppName)
    Assert.Equal("WebShop", descriptor.System)
    Assert.Equal(Relationship.Internal, descriptor.Relationship)
    Assert.Equal("org/checkout-api", descriptor.Repository)
}

[<Fact>]
let ``Descriptors returned for MetaAttribute`` () = task { 
    let meta = allDescriptors |> Descriptor.metas |> List.head
    Assert.Equal("PaymentsApi", meta.AppName)
    Assert.Equal("Handles processing payment for a shopping cart", meta.Description)
    Assert.Equal("Ordering Team", meta.Owner)
    Assert.Equal("Ordering", meta.System)
    Assert.Equal("org/payment-api", meta.Repository)
}


// Attributes split over several projects of one solution (examples/dotnet-multi):
//   Api      (exe)     [assembly: Meta] + [Responsibility], references Adapters
//   Adapters (library) [Collaborator]
//   Orphan   (library) [Responsibility], in the solution but referenced by nobody
let multiProjectDescriptors = task {
                                let! sln = "../../../../../examples/dotnet-multi/MultiProject.sln" |> Roslyn.openSolutionWith ["Configuration", configuration]
                                return! sln |> Descriptors.getDescriptors "" Descriptors.attributeNames
                                } |> Async.AwaitTask |> Async.RunSynchronously

[<Fact>]
let ``Multi-project: Meta from the program assembly is found`` () =
    let meta = multiProjectDescriptors |> Descriptor.metas |> List.exactlyOne
    Assert.Equal("PaymentsApi", meta.AppName)

[<Fact>]
let ``Multi-project: Collaborator in a referenced adapter project is found`` () =
    let c = multiProjectDescriptors |> filterToComponentName "VisaAdapter" |> Descriptor.collaborators |> List.exactlyOne
    Assert.Equal("Visa", c.AppName)
    Assert.Contains("Adapters.cs", c.ComponentSource)

[<Fact>]
let ``Multi-project: Responsibility in the program project is found`` () =
    let r = multiProjectDescriptors |> filterToComponentName "PayUsecase" |> Descriptor.responsibilities |> List.exactlyOne
    Assert.Contains("Program.cs", r.ComponentSource)

[<Fact>]
let ``Multi-project: projects in the solution are analysed even when nothing references them`` () =
    let r = multiProjectDescriptors |> filterToComponentName "Lonely" |> Descriptor.responsibilities |> List.exactlyOne
    Assert.Contains("Orphan.cs", r.ComponentSource)

let private cmd format : Haya.Core.DescribeCommand =
    { InputPaths = []; OutputPath = ""; Format = format; CurrentDirectory = "" }

let private app name system (collaborators: (string * string * Relationship) list) : App =
    { Meta = Some { AppName = name; Description = $"{name} description"; System = system; Repository = ""; Owner = "Team" }
      Responsibilities = []
      Collaborators =
        collaborators
        |> List.map (fun (cName, cSystem, rel) ->
            { Direction = Direction.Downstream; Tech = ""; Protocol = ""; DataDescription = "uses"; Description = ""
              AppName = cName; System = cSystem; Relationship = rel; ComponentName = ""; ComponentSource = ""
              Repository = ""; Owner = "" }) }

[<Fact>]
let ``Describe output decodes back to the same apps in json and yaml`` () =
    let apps = [ App.ofDescriptors allDescriptors; app "Other" "Billing" [ "Visa", "Visa", Relationship.External ] ]
    let check format fileName =
        let text = Haya.Core.Describe.serialize (cmd format) apps
        match Haya.Core.Decode.apps fileName text with
        | Ok decoded ->
            Assert.True((apps = decoded))
            Assert.Equal(text, Haya.Core.Describe.serialize (cmd format) decoded)
        | Error e -> failwith e
    check Haya.Core.DataFormat.Json "haya.json"
    check Haya.Core.DataFormat.Yaml "haya.yaml"

[<Fact>]
let ``Decode reads the version 0.0 single app document`` () =
    let legacy = """{"collaborators":[],"meta":{"appName":"A","description":"d","system":"S","repository":"","owner":"o"},"responsibilities":[{"description":"r","componentName":"C","componentSource":"c.cs"}],"version":"0.0"}"""
    match Haya.Core.Decode.apps "x.json" legacy with
    | Ok [ a ] ->
        Assert.Equal("A", (App.meta a).AppName)
        Assert.Equal(1, a.Responsibilities.Length)
    | other -> failwithf "unexpected %A" other

[<Fact>]
let ``Decode rejects documents that are not describe output`` () =
    Assert.True(Result.isError (Haya.Core.Decode.apps "x.json" "{\"foo\": 1}"))
    Assert.True(Result.isError (Haya.Core.Decode.apps "x.json" "{\"apps\": [{\"foo\": 1}]}"))
    Assert.True(Result.isError (Haya.Core.Decode.apps "x.json" "not json"))

[<Fact>]
let ``Merge combines apps with the same system and name and warns on conflicts`` () =
    let a = app "Api" "Ordering" [ "Visa", "Visa", Relationship.External ]
    let b = { app "api" "ordering" [ "Visa", "Visa", Relationship.External; "Db", "Ordering", Relationship.Internal ] with
                Meta = Some { (App.meta (app "api" "ordering" [])) with Description = "different" } }
    let other = app "Web" "Ordering" []
    let merged, warnings = Haya.Core.Merge.apps [ a; b; other ]
    Assert.Equal(2, merged.Length)
    Assert.Equal(2, merged.Head.Collaborators.Length)
    Assert.Equal("Api", (App.meta merged.Head).AppName)
    Assert.Single(warnings) |> ignore

[<Fact>]
let ``Merge never merges apps without a meta`` () =
    let anon = { Meta = None; Responsibilities = []; Collaborators = [] }
    let merged, _ = Haya.Core.Merge.apps [ anon; anon ]
    Assert.Equal(2, merged.Length)

[<Fact>]
let ``Backstage links apps of one system instead of emitting stubs`` () =
    let api = app "Api" "Ordering" [ "Payments", "Billing", Relationship.Internal; "Visa", "Visa", Relationship.External ]
    let payments = app "Payments" "Billing" []
    let yaml = Haya.Core.Backstage.generateCatalog { InputPaths = []; OutputPath = ""; Format = Haya.Core.DataFormat.Yaml; CurrentDirectory = "" } [ api; payments ]
    let docs = yaml.Split("---\n")
    Assert.Equal(3, docs.Length) // api, payments, Visa stub (no Payments stub)
    Assert.Contains("billing-payments", docs[1])
    Assert.Contains("ordering-api", docs[1]) // payments is a dependency of api

[<Fact>]
let ``Diagram declares each system once and relates them`` () =
    let api = app "Api" "Ordering" [ "Payments", "Billing", Relationship.Internal ]
    let payments = app "Payments" "Billing" []
    let web = app "Web" "Ordering" []
    let text =
        Haya.Core.Diagram.sprintMermaidC4L1 [ api; payments; web ]
    Assert.Contains("System(ordering, Ordering,", text)
    Assert.Contains("System(billing, Billing,", text)
    Assert.Contains("Rel(ordering, billing, \"uses\")", text)
    Assert.Equal(1, text.Split("System(ordering,").Length - 1)

[<Fact>]
let ``A class with both Responsibility and Collaborator yields one of each`` () =
    let ds = multiProjectDescriptors |> filterToComponentName "Both"
    let r = ds |> Descriptor.responsibilities |> List.exactlyOne
    let c = ds |> Descriptor.collaborators |> List.exactlyOne
    Assert.Equal("Does both", r.Description)
    Assert.Equal("Elsewhere", c.AppName)

[<Fact>]
let ``Backstage without a meta catalogues only the collaborators`` () =
    let anon = { Meta = None; Responsibilities = []; Collaborators = (app "X" "Y" [ "Visa", "Visa", Relationship.External ]).Collaborators }
    let yaml = Haya.Core.Backstage.generateCatalog { InputPaths = []; OutputPath = ""; Format = Haya.Core.DataFormat.Yaml; CurrentDirectory = "" } [ anon ]
    Assert.Contains("visa-visa", yaml)

[<Fact>]
let ``Backstage defaults a collaborator system to the owner, or External`` () =
    let api = app "Api" "Ordering" [ "Local", "", Relationship.Internal; "Stripe", "", Relationship.External ]
    let yaml = Haya.Core.Backstage.generateCatalog { InputPaths = []; OutputPath = ""; Format = Haya.Core.DataFormat.Yaml; CurrentDirectory = "" } [ api ]
    Assert.Contains("ordering-local", yaml)
    Assert.Contains("external-stripe", yaml)
    Assert.DoesNotContain("'-", yaml)

[<Fact>]
let ``Backstage dependencyOf only lists apps that depend on it`` () =
    let api = app "Api" "Ordering" [ "Payments", "Billing", Relationship.Internal ]
    let payments = app "Payments" "Billing" []
    let docs = (Haya.Core.Backstage.generateCatalog { InputPaths = []; OutputPath = ""; Format = Haya.Core.DataFormat.Yaml; CurrentDirectory = "" } [ api; payments ]).Split("---\n")
    Assert.Contains("dependencyOf: []", docs[0])
    Assert.Contains("ordering-api", docs[1])

[<Fact>]
let ``Backstage splits collaborators by direction`` () =
    let a = app "Payments" "Billing" [ "Checkout", "WebShop", Relationship.Internal; "Visa", "Visa", Relationship.External ]
    let a = { a with Collaborators = a.Collaborators |> List.mapi (fun i c -> if i = 0 then { c with Direction = Direction.Upstream } else c) }
    let docs = (Haya.Core.Backstage.generateCatalog { InputPaths = []; OutputPath = ""; Format = Haya.Core.DataFormat.Yaml; CurrentDirectory = "" } [ a ]).Split("---\n")
    Assert.Contains("- web-shop-checkout", docs[0].Split("dependsOn:")[0]) // dependencyOf
    Assert.Contains("- visa-visa", docs[0].Split("dependsOn:")[1])
    Assert.DoesNotContain("web-shop-checkout", docs[0].Split("dependsOn:")[1])

[<Fact>]
let ``Backstage honours the json format`` () =
    let api = app "Api" "Ordering" []
    let json = Haya.Core.Backstage.generateCatalog { InputPaths = []; OutputPath = ""; Format = Haya.Core.DataFormat.Json; CurrentDirectory = "" } [ api ]
    Assert.StartsWith("[", json.TrimStart())
    Assert.Contains("\"apiVersion\"", json)
