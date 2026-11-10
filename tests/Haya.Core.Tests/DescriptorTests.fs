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
    Assert.Equal(".NET 10 / ASP.NET Core", meta.Tech)
    Assert.Equal(AppKind.Api, meta.Kind)
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
    { Meta = Some { AppName = name; Description = $"{name} description"; System = system; Repository = ""; Owner = "Team"; Tech = ""; Kind = AppKind.Unspecified; Lifecycle = ""; Tier = "" }
      Language = Language.Unknown
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
    let anon = { Meta = None; Language = Language.Unknown; Responsibilities = []; Collaborators = [] }
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
    let anon = { Meta = None; Language = Language.Unknown; Responsibilities = []; Collaborators = (app "X" "Y" [ "Visa", "Visa", Relationship.External ]).Collaborators }
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
let ``Backstage links an app to an in-catalog app that lists it as upstream`` () =
    // Billing lists Ordering as upstream (Ordering calls Billing) and Ordering does not mention Billing
    let ordering = app "Api" "Ordering" []
    let billing = app "Payments" "Billing" [ "Api", "Ordering", Relationship.Internal ]
    let billing = { billing with Collaborators = billing.Collaborators |> List.map (fun c -> { c with Direction = Direction.Upstream }) }
    let docs = (Haya.Core.Backstage.generateCatalog { InputPaths = []; OutputPath = ""; Format = Haya.Core.DataFormat.Yaml; CurrentDirectory = "" } [ ordering; billing ]).Split("---\n")
    Assert.Contains("billing-payments", docs[0].Split("dependsOn:")[1]) // ordering-api dependsOn billing-payments
    Assert.Contains("ordering-api", docs[1].Split("dependsOn:")[0])     // billing-payments dependencyOf ordering-api

[<Fact>]
let ``Diagram places a collaborator without a system in the owner system or External`` () =
    let api = app "Api" "Ordering" [ "Local", "", Relationship.Internal; "Stripe", "", Relationship.External ]
    let text = Haya.Core.Diagram.sprintMermaidC4L1 [ api ]
    Assert.Contains("System_Ext(external, External,", text)
    Assert.Contains("Rel(ordering, external,", text)
    Assert.DoesNotContain("Rel(ordering, ,", text)
    Assert.Equal(1, text.Split("Rel(").Length - 1) // Local is in Ordering: no relationship to itself

[<Fact>]
let ``Backstage honours the json format`` () =
    let api = app "Api" "Ordering" []
    let json = Haya.Core.Backstage.generateCatalog { InputPaths = []; OutputPath = ""; Format = Haya.Core.DataFormat.Json; CurrentDirectory = "" } [ api ]
    Assert.StartsWith("[", json.TrimStart())
    Assert.Contains("\"apiVersion\"", json)

// --- C4 container diagram (crc -cc, diagram -t c4container) and app tech/kind ---

let private withMeta f (a: App) = { a with Meta = a.Meta |> Option.map f }

[<Fact>]
let ``Container diagram draws apps inside their system boundary with tech and kind`` () =
    let api = app "Api" "Ordering" [ "Payments", "Ordering", Relationship.Internal ] |> withMeta (fun m -> { m with Tech = "ASP.NET Core"; Kind = AppKind.Api })
    let payments = app "Payments" "Ordering" [] |> withMeta (fun m -> { m with Kind = AppKind.Database; Tech = "PostgreSQL" })
    let events = app "Events" "Ordering" [] |> withMeta (fun m -> { m with Kind = AppKind.Queue })
    let text = Haya.Core.Diagram.sprintMermaidC4L2 [ api; payments; events ]
    Assert.StartsWith("C4Container", text)
    Assert.Contains("System_Boundary(ordering, \"Ordering\") {", text)
    Assert.Contains("Container(ordering_api, \"Api\", \"ASP.NET Core\", \"Api description\")", text)
    Assert.Contains("ContainerDb(ordering_payments, \"Payments\", \"PostgreSQL\",", text)
    Assert.Contains("ContainerQueue(ordering_events, \"Events\", \"\",", text)
    Assert.Contains("Rel(ordering_api, ordering_payments, \"uses\")", text) // empty protocol: no technology argument
    Assert.Equal(1, text.Split("System_Boundary(").Length - 1)

[<Fact>]
let ``Container diagram relates apps across systems and puts other systems outside the boundaries`` () =
    let api = app "Api" "Ordering" [ "Payments", "Billing", Relationship.Internal; "Visa", "Visa", Relationship.External; "Shipping", "Logistics", Relationship.Internal ]
    let api = { api with Collaborators = api.Collaborators |> List.map (fun c -> { c with Protocol = "HTTPS" }) }
    let payments = app "Payments" "Billing" []
    let text = Haya.Core.Diagram.sprintMermaidC4L2 [ api; payments ]
    Assert.Contains("System_Boundary(ordering,", text)
    Assert.Contains("System_Boundary(billing,", text)
    Assert.Contains("Rel(ordering_api, billing_payments, \"uses\", \"HTTPS\")", text) // known app: container to container
    Assert.Contains("System_Ext(visa, \"Visa\",", text)
    Assert.Contains("System(logistics, \"Logistics\",", text)
    Assert.Contains("Rel(ordering_api, logistics, \"uses\", \"HTTPS\")", text) // unknown app in another system: the system

[<Fact>]
let ``Container diagram declares a collaborator in an own system without a meta, and honours direction`` () =
    let api = app "Api" "Ordering" [ "Cache", "", Relationship.Internal ]
    let api = { api with Collaborators = api.Collaborators |> List.map (fun c -> { c with Direction = Direction.Upstream; Tech = "Redis" }) }
    let text = Haya.Core.Diagram.sprintMermaidC4L2 [ api ]
    Assert.Contains("Container(ordering_cache, \"Cache\", \"Redis\",", text)
    Assert.Contains("Rel(ordering_cache, ordering_api,", text)

[<Fact>]
let ``Container diagram skips apps without a meta`` () =
    let anon = { Meta = None; Language = Language.Unknown; Responsibilities = []; Collaborators = (app "X" "Y" [ "Visa", "Visa", Relationship.External ]).Collaborators }
    let text = Haya.Core.Diagram.sprintMermaidC4L2 [ anon ]
    Assert.DoesNotContain("Rel(", text)
    Assert.DoesNotContain("Container(", text)

[<Fact>]
let ``Crc renders the container diagram only for -cc, after the system diagram`` () =
    let crc l1 l2 : Haya.Core.CrcCommand = { InputPaths = []; OutputPath = ""; IncludeL1Diagram = l1; IncludeL2Diagram = l2; CurrentDirectory = "" }
    let apps = [ app "Api" "Ordering" [ "Visa", "Visa", Relationship.External ] ]
    let none = Haya.Core.Crc.sprintMarkdown (crc false false) apps
    let l1 = Haya.Core.Crc.sprintMarkdown (crc true false) apps
    let l2 = Haya.Core.Crc.sprintMarkdown (crc true true) apps
    Assert.DoesNotContain("C4Context", none)
    Assert.Contains("C4Context", l1)
    Assert.DoesNotContain("C4Container", l1)
    Assert.True(l2.IndexOf("C4Context") < l2.IndexOf("C4Container"))

[<Fact>]
let ``Describe round-trips tech and kind, leaves them out when unset, and reads documents without them`` () =
    let set = app "Api" "Ordering" [] |> withMeta (fun m -> { m with Tech = "Bun"; Kind = AppKind.WebApp })
    let unset = app "Plain" "Ordering" []
    let text = Haya.Core.Describe.serialize (cmd Haya.Core.DataFormat.Json) [ set; unset ]
    Assert.Contains("\"tech\": \"Bun\"", text)
    Assert.Contains("\"kind\": \"WebApp\"", text)
    Assert.Equal(1, text.Split("\"kind\"").Length - 1)
    Assert.Contains("\"version\": \"0.3\"", text)
    match Haya.Core.Decode.apps "x.json" text with
    | Ok decoded -> Assert.True(([ set; unset ] = decoded))
    | Error e -> failwith e
    let legacy = """{"apps":[{"collaborators":[],"meta":{"appName":"A","description":"","system":"S","repository":"","owner":""},"responsibilities":[]}],"version":"0.1"}"""
    match Haya.Core.Decode.apps "x.json" legacy with
    | Ok [ a ] ->
        Assert.Equal("", (App.meta a).Tech)
        Assert.Equal(AppKind.Unspecified, (App.meta a).Kind)
    | _ -> failwith "expected one app"

[<Fact>]
let ``Merge warns when tech or kind conflict`` () =
    let a = app "Api" "Ordering" [] |> withMeta (fun m -> { m with Tech = "Bun"; Kind = AppKind.Api })
    let b = app "Api" "Ordering" [] |> withMeta (fun m -> { m with Tech = "Node"; Kind = AppKind.Worker })
    let merged, warnings = Haya.Core.Merge.apps [ a; b ]
    Assert.Equal("Bun", (App.meta merged.Head).Tech)
    Assert.Equal(2, warnings.Length)

[<Fact>]
let ``Describe round-trips lifecycle, tier and language, and leaves them out when unset`` () =
    let set =
        app "Api" "Ordering" []
        |> withMeta (fun m -> { m with Lifecycle = "production"; Tier = "1" })
        |> App.withLanguage Language.DotNet
    let unset = app "Plain" "Ordering" []
    let text = Haya.Core.Describe.serialize (cmd Haya.Core.DataFormat.Json) [ set; unset ]
    Assert.Contains("\"lifecycle\": \"production\"", text)
    Assert.Contains("\"tier\": \"1\"", text)
    Assert.Contains("\"language\": \"dotnet\"", text)
    Assert.Equal(1, text.Split("\"lifecycle\"").Length - 1)
    Assert.Equal(1, text.Split("\"language\"").Length - 1)
    for format, name in [ Haya.Core.DataFormat.Json, "x.json"; Haya.Core.DataFormat.Yaml, "x.yaml" ] do
        let text = Haya.Core.Describe.serialize (cmd format) [ set; unset ]
        match Haya.Core.Decode.apps name text with
        | Ok decoded -> Assert.True(([ set; unset ] = decoded))
        | Error e -> failwith e
    // documents written before 0.3 have none of them
    let legacy = """{"apps":[{"collaborators":[],"meta":{"appName":"A","description":"","system":"S","repository":"","owner":""},"responsibilities":[]}],"version":"0.2"}"""
    match Haya.Core.Decode.apps "x.json" legacy with
    | Ok [ a ] ->
        Assert.Equal("", (App.meta a).Lifecycle)
        Assert.Equal("", (App.meta a).Tier)
        Assert.Equal(Language.Unknown, a.Language)
    | _ -> failwith "expected one app"

[<Fact>]
let ``Merge warns when lifecycle or tier conflict and keeps the first known language`` () =
    let a = app "Api" "Ordering" [] |> withMeta (fun m -> { m with Lifecycle = "production"; Tier = "1" })
    let b =
        app "Api" "Ordering" []
        |> withMeta (fun m -> { m with Lifecycle = "deprecated"; Tier = "2" })
        |> App.withLanguage Language.JavaScript
    let merged, warnings = Haya.Core.Merge.apps [ a; b ]
    Assert.Equal("production", (App.meta merged.Head).Lifecycle)
    Assert.Equal(2, warnings.Length)
    Assert.Equal(Language.JavaScript, merged.Head.Language)
