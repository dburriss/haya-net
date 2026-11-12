module DescriptorTests

open Example
open Haya
open Haya.Core
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
let ``The example solution has a person collaborator`` () =
    let customer = allDescriptors |> Descriptor.collaborators |> List.find (fun c -> c.AppName = "Customer")
    Assert.Equal(AppKind.Person, customer.Kind)
    Assert.Equal(Relationship.External, customer.Relationship)
    Assert.Equal(Direction.Upstream, customer.Direction)

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
    { InputPaths = []; OutputPath = ""; Format = format; Detail = Detail.Apps; CurrentDirectory = "" }

let private app name system (collaborators: (string * string * Relationship) list) : App =
    { Meta = Some { AppName = name; Description = $"{name} description"; System = system; Repository = ""; Owner = "Team"; Tech = ""; Kind = AppKind.Unspecified; Lifecycle = ""; Tier = "" }
      Language = Language.Unknown
      Components = []
      Responsibilities = []
      Collaborators =
        collaborators
        |> List.map (fun (cName, cSystem, rel) ->
            { Direction = Direction.Downstream; Tech = ""; Protocol = ""; DataDescription = "uses"; Description = ""
              AppName = cName; System = cSystem; Relationship = rel; ComponentName = ""; ComponentSource = ""
              Repository = ""; Owner = ""; Kind = AppKind.Unspecified }) }

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
    let anon = { Components = []; Meta = None; Language = Language.Unknown; Responsibilities = []; Collaborators = [] }
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
    let anon = { Components = []; Meta = None; Language = Language.Unknown; Responsibilities = []; Collaborators = (app "X" "Y" [ "Visa", "Visa", Relationship.External ]).Collaborators }
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
    let anon = { Components = []; Meta = None; Language = Language.Unknown; Responsibilities = []; Collaborators = (app "X" "Y" [ "Visa", "Visa", Relationship.External ]).Collaborators }
    let text = Haya.Core.Diagram.sprintMermaidC4L2 [ anon ]
    Assert.DoesNotContain("Rel(", text)
    Assert.DoesNotContain("Container(", text)

// --- Component diagram (level 3) ---

let private onClass (name: string) (source: string) (a: App) =
    { a with
        Responsibilities = a.Responsibilities |> List.map (fun r -> { r with ComponentName = name; ComponentSource = source })
        Collaborators = a.Collaborators |> List.map (fun c -> { c with ComponentName = name; ComponentSource = source }) }

[<Fact>]
let ``Component diagram draws each app's classes in its boundary with the technology of their file`` () =
    let api =
        { (app "Api" "Ordering" [ "Visa", "Visa", Relationship.External ]) with
            Responsibilities = [ { Description = "Takes orders"; ComponentName = ""; ComponentSource = "" }; { Description = "Checks stock"; ComponentName = ""; ComponentSource = "" } ] }
        |> onClass "OrdersController" "src/Orders.cs"
    let text = Haya.Core.Diagram.sprintMermaidC4L3 Detail.Components [ api ]
    Assert.StartsWith("C4Component", text)
    Assert.Contains("title C4 Component Diagram for Api", text)
    Assert.Contains("System_Boundary(ordering, \"Ordering\") {", text)
    Assert.Contains("Container_Boundary(ordering_api, \"Api\") {", text)
    Assert.Contains("Component(ordering_api__orders_controller, \"OrdersController\", \"C#\", \"Takes orders; Checks stock\")", text)
    Assert.Contains("System_Ext(visa, \"Visa\",", text)
    Assert.Contains("Rel(ordering_api__orders_controller, visa, \"uses\")", text)
    Assert.DoesNotContain("ordering_api__app", text) // every relationship has a class

[<Fact>]
let ``Component diagram links classes that name each other, and falls back to the app's container`` () =
    let api = app "Api" "Ordering" [ "Payments", "Ordering", Relationship.Internal; "Db", "Ordering", Relationship.Internal ] |> onClass "PaymentsClient" "src/a.ts"
    let payments = app "Payments" "Ordering" [ "Api", "Ordering", Relationship.Internal ] |> onClass "ApiCallback" "Lib::Payments.ApiCallback"
    let db = app "Db" "Ordering" [] |> withMeta (fun m -> { m with Kind = AppKind.Database; Tech = "PostgreSQL" })
    let lonely = app "Lonely" "Ordering" [ "Api", "Ordering", Relationship.Internal ] // no class names: falls back on both ends
    let text = Haya.Core.Diagram.sprintMermaidC4L3 Detail.Components [ api; payments; db; lonely ]
    Assert.Contains("title C4 Component Diagram for Ordering", text)
    Assert.Contains("Component(ordering_api__payments_client, \"PaymentsClient\", \"TypeScript\",", text)
    Assert.Contains("Component(ordering_payments__api_callback, \"ApiCallback\", \"\",", text) // referenced type: no technology
    Assert.Contains("Rel(ordering_api__payments_client, ordering_payments__api_callback, \"uses\")", text)
    Assert.Contains("Rel(ordering_payments__api_callback, ordering_api__payments_client, \"uses\")", text)
    Assert.Contains("ContainerDb(ordering_db, \"Db\", \"PostgreSQL\",", text) // no classes: a container, as at level 2
    Assert.Contains("Rel(ordering_api__payments_client, ordering_db, \"uses\")", text)
    Assert.Contains("Container(ordering_lonely, \"Lonely\",", text)
    Assert.Contains("Rel(ordering_lonely, ordering_api__app, \"uses\")", text)
    Assert.Contains("Container(ordering_api__app, \"Api\",", text) // drawn inside the Api boundary, because a line needs it

[<Fact>]
let ``Component diagram numbers classes of the same name in one app`` () =
    let a = app "Api" "Ordering" [] |> withMeta id
    let a =
        { a with
            Responsibilities =
                [ { Description = "one"; ComponentName = "Handler"; ComponentSource = "a/Handler.cs" }
                  { Description = "two"; ComponentName = "Handler"; ComponentSource = "b/Handler.cs" } ] }
    let text = Haya.Core.Diagram.sprintMermaidC4L3 Detail.Components [ a ]
    Assert.Contains("Component(ordering_api__handler, \"Handler\", \"C#\", \"one\")", text)
    Assert.Contains("Component(ordering_api__handler_2, \"Handler\", \"C#\", \"two\")", text)

[<Theory>]
[<InlineData("src/a.cs", "C#")>]
[<InlineData("src/A.FS", "F#")>]
[<InlineData("src/a.tsx", "TypeScript")>]
[<InlineData("lib/a.mjs", "JavaScript")>]
[<InlineData("Acme.Shared::Acme.Shared.Client", "")>]
[<InlineData("src.v2/Makefile", "")>]
[<InlineData("", "")>]
let ``Component technology comes from the file extension`` (source: string, expected: string) =
    Assert.Equal(expected, Haya.Core.Diagram.componentTech source)

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
    Assert.Contains("\"version\": \"0.6\"", text)
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

let private datadogCmd format : Haya.Core.DatadogCommand =
    { InputPaths = []; OutputPath = ""; Format = format; CurrentDirectory = "" }

let private entity (kind: string) (name: string) (entities: Haya.Core.Encode.DatadogEntity list) =
    entities |> List.find (fun e -> e.Kind = kind && e.Name = name)

[<Fact>]
let ``Datadog maps the app kind to an entity kind and type`` () =
    let ofKind kind = app "A" "S" [] |> withMeta (fun m -> { m with Kind = kind })
    let kindAndType kind =
        let e = Haya.Core.Datadog.genEntities [ ofKind kind ] |> List.find (fun e -> e.Kind <> "system")
        e.Kind, e.Type
    Assert.Equal(("service", ""), kindAndType AppKind.Unspecified)
    Assert.Equal(("service", ""), kindAndType AppKind.Service)
    Assert.Equal(("service", ""), kindAndType AppKind.Worker)
    Assert.Equal(("service", "web"), kindAndType AppKind.WebApp)
    // the api kind cannot depend on anything, so an API is a service of type rest
    Assert.Equal(("service", "rest"), kindAndType AppKind.Api)
    Assert.Equal(("datastore", ""), kindAndType AppKind.Database)
    Assert.Equal(("queue", ""), kindAndType AppKind.Queue)

[<Fact>]
let ``Datadog writes a relation on the entity that calls, with the kind of what it calls`` () =
    let api = app "Api" "Ordering" [ "Db", "Ordering", Relationship.Internal; "Visa", "Visa", Relationship.External ]
    let db = app "Db" "Ordering" [] |> withMeta (fun m -> { m with Kind = AppKind.Database })
    let entities = Haya.Core.Datadog.genEntities [ api; db ]
    Assert.Equal<string list>([ "datastore:ordering-db"; "service:visa-visa" ], (entity "service" "ordering-api" entities).DependsOn)
    Assert.Empty((entity "datastore" "ordering-db" entities).DependsOn)

[<Fact>]
let ``Datadog turns an upstream collaborator into a dependency of the caller`` () =
    // Web calls Api, and only Api says so
    let api =
        { app "Api" "Ordering" [ "Web", "Shop", Relationship.Internal; "Mobile", "Shop", Relationship.External ] with
            Collaborators =
                (app "Api" "Ordering" [ "Web", "Shop", Relationship.Internal; "Mobile", "Shop", Relationship.External ]).Collaborators
                |> List.map (fun c -> { c with Direction = Direction.Upstream }) }
    let web = app "Web" "Shop" []
    let entities = Haya.Core.Datadog.genEntities [ api; web ]
    // an app of the catalog
    Assert.Equal<string list>([ "service:ordering-api" ], (entity "service" "shop-web" entities).DependsOn)
    // a stub
    Assert.Equal<string list>([ "service:ordering-api" ], (entity "service" "shop-mobile" entities).DependsOn)
    Assert.Empty((entity "service" "ordering-api" entities).DependsOn)

[<Fact>]
let ``Datadog groups the apps and the stubs of a system`` () =
    let entities =
        Haya.Core.Datadog.genEntities
            [ app "Api" "Ordering" [ "Visa", "Visa", Relationship.External ]; app "Db" "Ordering" [] ]
    let ordering = entity "system" "ordering" entities
    Assert.Equal<string list>([ "service:ordering-api"; "service:ordering-db" ], ordering.Components)
    Assert.Empty(ordering.DependsOn)
    Assert.Equal<string list>([ "service:visa-visa" ], (entity "system" "visa" entities).Components)

[<Fact>]
let ``Datadog gives languages only to a service, and only when the analyser said what it is`` () =
    let known = app "Api" "Ordering" [] |> App.withLanguage Language.JavaScript
    let db = app "Db" "Ordering" [] |> withMeta (fun m -> { m with Kind = AppKind.Database }) |> App.withLanguage Language.DotNet
    let unknown = app "Other" "Ordering" []
    let entities = Haya.Core.Datadog.genEntities [ known; db; unknown ]
    Assert.Equal<string list>([ "js" ], (entity "service" "ordering-api" entities).Languages)
    Assert.Empty((entity "datastore" "ordering-db" entities).Languages)
    Assert.Empty((entity "service" "ordering-other" entities).Languages)
    Assert.Equal<string list>([ "dotnet" ], (Haya.Core.Datadog.genEntities [ known |> App.withLanguage Language.DotNet ] |> entity "service" "ordering-api").Languages)

[<Fact>]
let ``Datadog carries owner, lifecycle and tier, and a code location only for a URL`` () =
    let a = app "Api" "Ordering" [] |> withMeta (fun m -> { m with Lifecycle = "production"; Tier = "1"; Repository = "https://github.com/org/api" })
    let slug = app "Slug" "Ordering" [] |> withMeta (fun m -> { m with Repository = "org/slug" })
    let entities = Haya.Core.Datadog.genEntities [ a; slug ]
    let e = entity "service" "ordering-api" entities
    Assert.Equal(("Team", "production", "1"), (e.Owner, e.Lifecycle, e.Tier))
    Assert.Equal("https://github.com/org/api", e.RepositoryUrl)
    Assert.Equal("", (entity "service" "ordering-slug" entities).RepositoryUrl)

[<Fact>]
let ``Datadog without a meta catalogues only the collaborators, and nothing for nothing`` () =
    let anon = { Components = []; Meta = None; Language = Language.Unknown; Responsibilities = []; Collaborators = (app "X" "Y" [ "Visa", "Visa", Relationship.External ]).Collaborators }
    let yaml = Haya.Core.Datadog.generateCatalog (datadogCmd Haya.Core.DataFormat.Yaml) [ anon ]
    Assert.Contains("name: visa-visa", yaml)
    Assert.DoesNotContain("name: y-x", yaml)
    let empty = { Components = []; Meta = None; Language = Language.Unknown; Responsibilities = []; Collaborators = [] }
    Assert.Equal("", Haya.Core.Datadog.generateCatalog (datadogCmd Haya.Core.DataFormat.Yaml) [ empty ])

[<Fact>]
let ``Datadog writes yaml documents separated by dashes, or a json array`` () =
    let apps = [ app "Api" "Ordering" [ "Visa", "Visa", Relationship.External ] ]
    let yaml = Haya.Core.Datadog.generateCatalog (datadogCmd Haya.Core.DataFormat.Yaml) apps
    Assert.StartsWith("apiVersion: v3\n", yaml)
    Assert.Equal(1 + 2 + 1, yaml.Split("---\n").Length) // two systems, the api and the stub
    let json = Haya.Core.Datadog.generateCatalog (datadogCmd Haya.Core.DataFormat.Json) apps
    Assert.StartsWith("[", json.TrimStart())
    Assert.Contains("\"apiVersion\": \"v3\"", json)

// --- Graph model (basis of the interactive html explorer) ---

[<Fact>]
let ``Graph makes own systems boundaries and other systems single nodes`` () =
    let api = app "Api" "Ordering" [ "Payments", "Billing", Relationship.Internal; "Visa", "Visa", Relationship.External; "Shipping", "Logistics", Relationship.Internal ]
    let api = { api with Collaborators = api.Collaborators |> List.map (fun c -> { c with Protocol = "HTTPS" }) }
    let payments = app "Payments" "Billing" []
    let g = Haya.Core.Graph.ofApps Detail.Apps [ api; payments ]
    let node id = g.Nodes |> List.find (fun n -> n.Id = id)
    Assert.True((node "ordering").OwnSystem)
    Assert.True((node "billing").OwnSystem)
    Assert.False((node "visa").OwnSystem)
    Assert.Equal(Relationship.External, (node "visa").Relationship)
    Assert.Equal(Some "ordering", (node "ordering_api").Parent)
    Assert.True((node "ordering_api").HasMeta)
    Assert.Contains(g.Edges, fun e -> e.From = "ordering_api" && e.To = "billing_payments" && e.Protocol = "HTTPS")
    Assert.Contains(g.Edges, fun e -> e.From = "ordering_api" && e.To = "logistics") // unknown app in another system: the system

[<Fact>]
let ``Graph declares a collaborator in an own system, flags it as without meta, and honours direction`` () =
    let api = app "Api" "Ordering" [ "Cache", "", Relationship.Internal ]
    let api = { api with Collaborators = api.Collaborators |> List.map (fun c -> { c with Direction = Direction.Upstream; Tech = "Redis"; Owner = "team-x" }) }
    let g = Haya.Core.Graph.ofApps Detail.Apps [ api ]
    let cache = g.Nodes |> List.find (fun n -> n.Id = "ordering_cache")
    Assert.False(cache.HasMeta)
    Assert.Equal("Redis", cache.Tech)
    Assert.Equal("team-x", cache.Owner)
    Assert.Equal(Some "ordering", cache.Parent)
    Assert.Contains(g.Edges, fun e -> e.From = "ordering_cache" && e.To = "ordering_api")

[<Fact>]
let ``Graph prefers an app's own meta over a collaborator declaration, drops self edges and is ordered by id`` () =
    let api = app "Api" "Ordering" [ "Payments", "Ordering", Relationship.Internal; "Api", "Ordering", Relationship.Internal ]
    let payments = app "Payments" "Ordering" [] |> withMeta (fun m -> { m with Description = "from meta"; Tech = "PostgreSQL" })
    let g = Haya.Core.Graph.ofApps Detail.Apps [ api; payments ]
    let p = g.Nodes |> List.find (fun n -> n.Id = "ordering_payments")
    Assert.True(p.HasMeta)
    Assert.Equal("from meta", p.Description)
    Assert.DoesNotContain(g.Edges, fun e -> e.From = e.To)
    Assert.Equal<string list>((g.Nodes |> List.map (_.Id)), (g.Nodes |> List.map (_.Id) |> List.sort))

[<Fact>]
let ``Graph skips apps without a meta`` () =
    let anon = { Components = []; Meta = None; Language = Language.Unknown; Responsibilities = []; Collaborators = (app "X" "Y" [ "Visa", "Visa", Relationship.External ]).Collaborators }
    let g = Haya.Core.Graph.ofApps Detail.Apps [ anon ]
    Assert.Empty(g.Nodes)
    Assert.Empty(g.Edges)

[<Fact>]
let ``Graph json lists nodes and edges, leaves out empty values and is valid json`` () =
    let api = app "Api" "Ordering" [ "Payments", "Billing", Relationship.Internal ]
    let api = { api with Collaborators = api.Collaborators |> List.map (fun c -> { c with Protocol = "HTTPS" }) }
    let json = Haya.Core.Graph.ofApps Detail.Apps [ api; app "Payments" "Billing" [] ] |> Haya.Core.Encode.graph |> Haya.Core.Serializer.toScriptSafeJson
    use doc = System.Text.Json.JsonDocument.Parse json
    let root = doc.RootElement
    Assert.Equal(Haya.Core.Encode.GraphVersion, root.GetProperty("version").GetString())
    let nodes = root.GetProperty("nodes").EnumerateArray() |> Seq.toList
    let apiNode = nodes |> List.find (fun n -> n.GetProperty("id").GetString() = "ordering_api")
    Assert.Equal("ordering", apiNode.GetProperty("parent").GetString())
    Assert.True(apiNode.GetProperty("hasMeta").GetBoolean())
    let mutable ignored = Unchecked.defaultof<System.Text.Json.JsonElement>
    Assert.False(apiNode.TryGetProperty("tech", &ignored)) // empty values are omitted
    let system = nodes |> List.find (fun n -> n.GetProperty("id").GetString() = "billing")
    Assert.True(system.GetProperty("ownSystem").GetBoolean())
    let edge = root.GetProperty("edges").EnumerateArray() |> Seq.head
    Assert.Equal("HTTPS", edge.GetProperty("protocol").GetString())
    Assert.DoesNotContain("language", json)

let private withComponents (api: App) =
    let resp name source description : ResponsibilityDescriptor = { Description = description; ComponentName = name; ComponentSource = source }
    let collabs =
        api.Collaborators
        |> List.mapi (fun i c ->
            match i with
            | 0 -> { c with ComponentName = "Usecase"; ComponentSource = "src/Usecase.cs" } // also has a responsibility
            | 1 -> { c with ComponentName = "Client"; ComponentSource = "src/Client.cs" }
            | _ -> { c with ComponentName = ""; ComponentSource = "" }) // no class: not a component
    { api with
        Responsibilities =
            [ resp "Usecase" "src/Usecase.cs" "Does one thing"
              resp "Usecase" "src/Usecase.cs" "Does another thing" // same class twice
              resp "Controller" "src/b/Controller.cs" "Takes requests"
              resp "Controller" "src/a/Controller.cs" "Takes other requests" // same name, other source
              resp "Ref" "" "From a class without a source" ]
        Collaborators = collabs }

[<Fact>]
let ``Graph lists each class of an app once, sorted by name then source`` () =
    let api = app "Api" "Ordering" [ "Payments", "Ordering", Relationship.Internal; "Visa", "Visa", Relationship.External; "Shipping", "Logistics", Relationship.Internal ] |> withComponents
    let g = Haya.Core.Graph.ofApps Detail.Components [ api ]
    let node id = g.Nodes |> List.find (fun n -> n.Id = id)
    let components = (node "ordering_api").Components |> List.map (fun c -> c.Name, c.Source)
    Assert.Equal<(string * string) list>(
        [ "Client", "src/Client.cs"
          "Controller", "src/a/Controller.cs"
          "Controller", "src/b/Controller.cs"
          "Ref", ""
          "Usecase", "src/Usecase.cs" ],
        components)
    Assert.Equal<string list>([ "Does one thing"; "Does another thing"; "Takes requests"; "Takes other requests"; "From a class without a source" ], (node "ordering_api").Responsibilities)
    Assert.Empty((node "ordering_payments").Components) // declared from a collaborator: no classes of ours
    Assert.Empty((node "ordering").Components)
    Assert.Empty((node "visa").Components)

[<Fact>]
let ``Graph gives classes ids and puts them at the ends of edges, naming back when the other app does`` () =
    let classes name source (a: App) =
        { a with Collaborators = a.Collaborators |> List.map (fun c -> { c with ComponentName = name; ComponentSource = source }) }
    let api = app "Api" "Ordering" [ "Payments", "Ordering", Relationship.Internal; "Db", "Ordering", Relationship.Internal ] |> classes "Client" "a.cs"
    let payments = app "Payments" "Ordering" [ "Api", "Ordering", Relationship.Internal ] |> classes "Callback" "b.cs"
    let g = Haya.Core.Graph.ofApps Detail.Components [ api; payments ]
    let node id = g.Nodes |> List.find (fun n -> n.Id = id)
    Assert.Equal<string list>([ "ordering_api__client" ], (node "ordering_api").Components |> List.map (_.Id))
    let edge from to_ = g.Edges |> List.find (fun e -> e.From = from && e.To = to_)
    Assert.Equal<string list>([ "ordering_api__client" ], (edge "ordering_api" "ordering_payments").FromComponents)
    Assert.Equal<string list>([ "ordering_payments__callback" ], (edge "ordering_api" "ordering_payments").ToComponents)
    Assert.Equal<string list>([ "ordering_api__client" ], (edge "ordering_api" "ordering_db").FromComponents)
    Assert.Empty((edge "ordering_api" "ordering_db").ToComponents) // the db has no classes: the app as a whole
    Assert.Equal(3, g.Edges.Length) // still one edge per app pair and label

[<Fact>]
let ``Graph json lists components with name and source, and leaves out empty ones`` () =
    let api = app "Api" "Ordering" [ "Payments", "Ordering", Relationship.Internal; "Visa", "Visa", Relationship.External ] |> withComponents
    let json = Haya.Core.Graph.ofApps Detail.Components [ api ] |> Haya.Core.Encode.graph |> Haya.Core.Serializer.toScriptSafeJson
    use doc = System.Text.Json.JsonDocument.Parse json
    let nodes = doc.RootElement.GetProperty("nodes").EnumerateArray() |> Seq.toList
    let byId id = nodes |> List.find (fun n -> n.GetProperty("id").GetString() = id)
    let components = (byId "ordering_api").GetProperty("components").EnumerateArray() |> Seq.toList
    Assert.Equal("ordering_api__client", components.Head.GetProperty("id").GetString())
    Assert.Equal("Client", components.Head.GetProperty("name").GetString())
    Assert.Equal("src/Client.cs", components.Head.GetProperty("source").GetString())
    let mutable ignored = Unchecked.defaultof<System.Text.Json.JsonElement>
    let noSource = components |> List.find (fun c -> c.GetProperty("name").GetString() = "Ref")
    Assert.False(noSource.TryGetProperty("source", &ignored)) // empty source is omitted
    Assert.False((byId "ordering_payments").TryGetProperty("components", &ignored)) // empty list is omitted
    let edges = doc.RootElement.GetProperty("edges").EnumerateArray() |> Seq.toList
    let toPayments = edges |> List.find (fun e -> e.GetProperty("to").GetString() = "ordering_payments")
    Assert.Equal("ordering_api__usecase", (toPayments.GetProperty("fromComponents").EnumerateArray() |> Seq.head).GetString())
    Assert.False(toPayments.TryGetProperty("toComponents", &ignored)) // the other end has no classes

[<Theory>]
[<InlineData("apps")>]
[<InlineData("systems")>]
let ``Graph leaves out the classes below Detail.Components`` (level: string) =
    let detail = if level = "apps" then Detail.Apps else Detail.Systems
    let api = app "Api" "Ordering" [ "Payments", "Ordering", Relationship.Internal; "Visa", "Visa", Relationship.External ] |> withComponents
    let payments = app "Payments" "Ordering" [ "Api", "Ordering", Relationship.Internal ] |> withComponents
    let g = Haya.Core.Graph.ofApps detail [ api; payments ]
    Assert.All(g.Nodes, fun n -> Assert.Empty(n.Components))
    Assert.All(g.Edges, fun e -> Assert.Empty(e.FromComponents); Assert.Empty(e.ToComponents))
    // the same nodes and edges as with classes, only without them
    let full = Haya.Core.Graph.ofApps Detail.Components [ api; payments ]
    Assert.Equal<string list>(full.Nodes |> List.map (_.Id), g.Nodes |> List.map (_.Id))
    Assert.Equal<(string * string) list>(full.Edges |> List.map (fun e -> e.From, e.To), g.Edges |> List.map (fun e -> e.From, e.To))
    let json = g |> Haya.Core.Encode.graph |> Haya.Core.Serializer.toScriptSafeJson
    Assert.DoesNotContain("\"components\"", json)
    Assert.DoesNotContain("fromComponents", json)
    Assert.DoesNotContain("toComponents", json)

[<Fact>]
let ``Explore page has classes only at Detail.Components`` () =
    let api = app "Api" "Ordering" [ "Payments", "Ordering", Relationship.Internal ] |> withComponents
    Assert.DoesNotContain("ordering_api__usecase", Haya.Core.Explore.sprintHtml ViewerSettings.none Detail.Apps [ api ])
    Assert.Contains("ordering_api__usecase", Haya.Core.Explore.sprintHtml ViewerSettings.none Detail.Components [ api ])

[<Fact>]
let ``Diagram detail picks the mermaid diagram, and html is the explorer at that detail`` () =
    let api = app "Api" "Ordering" [ "Payments", "Ordering", Relationship.Internal ] |> withComponents
    let gen detail format =
        Haya.Core.Diagram.generateDiagram
            { InputPaths = []; OutputPath = ""; Detail = detail; DiagramFormat = format; Viewer = ViewerSettings.none; CurrentDirectory = "" } [ api ]
    Assert.StartsWith("C4Context", gen Detail.Systems DiagramFormat.Mermaid)
    Assert.StartsWith("C4Container", gen Detail.Apps DiagramFormat.Mermaid)
    Assert.StartsWith("C4Component", gen Detail.Components DiagramFormat.Mermaid)
    Assert.Equal(Haya.Core.Explore.sprintHtml ViewerSettings.none Detail.Components [ api ], gen Detail.Components DiagramFormat.Html)
    Assert.Equal(Haya.Core.Explore.sprintHtml ViewerSettings.none Detail.Systems [ api ], gen Detail.Systems DiagramFormat.Html)

[<Fact>]
let ``Graph json is safe inside a script element`` () =
    let nasty = "</script><!-- \"q\" &    "
    let api = app "Api" "Ordering" [] |> withMeta (fun m -> { m with Description = nasty })
    let json = Haya.Core.Graph.ofApps Detail.Apps [ api ] |> Haya.Core.Encode.graph |> Haya.Core.Serializer.toScriptSafeJson
    Assert.DoesNotContain("<", json)
    Assert.DoesNotContain(" ", json)
    Assert.DoesNotContain(" ", json)
    use doc = System.Text.Json.JsonDocument.Parse json
    let node = doc.RootElement.GetProperty("nodes").EnumerateArray() |> Seq.find (fun n -> n.GetProperty("id").GetString() = "ordering_api")
    Assert.Equal(nasty, node.GetProperty("description").GetString()) // round-trips

// --- Interactive html explorer ---

[<Fact>]
let ``Explore page embeds the graph once, escapes the title and keeps the viewer`` () =
    let api = app "Api" "Ordering" [ "Visa", "Visa", Relationship.External ] |> withMeta (fun m -> { m with Description = "</script><b>" })
    let html = Haya.Core.Explore.render ViewerSettings.none "T <&> \"x\"" (Haya.Core.Graph.ofApps Detail.Apps [ api ])
    Assert.StartsWith("<!doctype html>", html)
    Assert.Contains("<title>T &lt;&amp;&gt; &quot;x&quot;</title>", html)
    Assert.Equal(1, html.Split("id=\"haya-data\"").Length - 1)
    Assert.DoesNotContain("@@HAYA_", html) // every token was filled in
    Assert.Contains("\\u003c/script>\\u003cb>", html) // description is escaped inside the data block
    // the data block is the only place the nasty description appears, and it cannot end the script element
    let data = html.Substring(html.IndexOf "id=\"haya-data\"")
    Assert.DoesNotContain("</script><b>", data)
    Assert.Contains("Content-Security-Policy", html)

[<Fact>]
let ``Explore page has a short fixed title however many systems there are`` () =
    let html = Haya.Core.Explore.sprintHtml ViewerSettings.none Detail.Apps [ app "Api" "Ordering" []; app "Payments" "Billing" [] ]
    Assert.Contains("<title>Haya Explorer</title>", html)
    Assert.DoesNotContain("Billing", html.Substring(0, html.IndexOf "</title>"))

// --- people: a collaborator whose kind is Person ---

/// `app` with the collaborators in `people` (name, relationship, direction) added as persons.
let private appWithPeople name system (people: (string * Relationship * Direction) list) : App =
    let a = app name system []
    { a with
        Collaborators =
            people
            |> List.map (fun (pName, rel, dir) ->
                { Direction = dir; Tech = ""; Protocol = ""; DataDescription = "uses"; Description = $"{pName} description"
                  AppName = pName; System = ""; Relationship = rel; ComponentName = ""; ComponentSource = ""
                  Repository = ""; Owner = ""; Kind = AppKind.Person }) }

[<Fact>]
let ``A collaborator's kind round-trips through describe and is left out when unset`` () =
    let apps = [ appWithPeople "Web" "Shop" [ "Customer", Relationship.External, Direction.Upstream ]; app "Api" "Shop" [ "Db", "Shop", Relationship.Internal ] ]
    let text = Haya.Core.Describe.serialize (cmd Haya.Core.DataFormat.Json) apps
    Assert.Equal(1, text.Split("\"kind\": \"Person\"").Length - 1)
    Assert.Equal(1, text.Split("\"kind\"").Length - 1) // the unset collaborator writes none
    match Haya.Core.Decode.apps "haya.json" text with
    | Ok decoded -> Assert.True((apps = decoded))
    | Error e -> failwith e

[<Fact>]
let ``Decode rejects the removed relationship values and unknown ones`` () =
    let doc rel = $"""{{"apps":[{{"collaborators":[{{"appName":"X","relationship":"{rel}"}}],"responsibilities":[]}}],"version":"0.4"}}"""
    for rel in [ "InternalUser"; "ExternalUser"; "Nonsense" ] do
        match Haya.Core.Decode.apps "x.json" (doc rel) with
        | Error e -> Assert.Contains(rel, e)
        | Ok _ -> failwith $"{rel} should not decode"

[<Fact>]
let ``Decode reads a collaborator without a relationship as internal`` () =
    let text = """{"apps":[{"collaborators":[{"appName":"X"}],"responsibilities":[]}],"version":"0.3"}"""
    match Haya.Core.Decode.apps "x.json" text with
    | Ok [ a ] -> Assert.Equal(Relationship.Internal, (List.exactlyOne a.Collaborators).Relationship)
    | other -> failwithf "unexpected %A" other

[<Fact>]
let ``Graph draws a person once by name, with no parent, and edges in the direction of use`` () =
    let web = appWithPeople "Web" "Shop" [ "Customer", Relationship.External, Direction.Upstream ]
    let api = appWithPeople "Api" "Shop" [ "customer", Relationship.External, Direction.Upstream; "Pager", Relationship.Internal, Direction.Downstream ]
    let g = Haya.Core.Graph.ofApps Detail.Apps [ web; api ]
    let customer = g.Nodes |> List.find (fun n -> n.Id = "person_customer")
    Assert.Equal(Haya.Core.Graph.NodeKind.Person, customer.Kind)
    Assert.Equal(None, customer.Parent)
    Assert.Equal(Relationship.External, customer.Relationship)
    Assert.Equal(1, g.Nodes |> List.filter (fun n -> n.Kind = Haya.Core.Graph.NodeKind.Person && n.Name.ToLower() = "customer") |> List.length)
    Assert.Equal(2, g.Nodes |> List.filter (fun n -> n.Kind = Haya.Core.Graph.NodeKind.Person) |> List.length)
    // a person is neither an app nor a system
    Assert.DoesNotContain(g.Nodes, fun n -> n.Kind = Haya.Core.Graph.NodeKind.System && n.Name = "Customer")
    Assert.DoesNotContain(g.Nodes, fun n -> n.Id = "shop_customer")
    Assert.Contains(g.Edges, fun e -> e.From = "person_customer" && e.To = "shop_web")  // upstream: the person uses the app
    Assert.Contains(g.Edges, fun e -> e.From = "shop_api" && e.To = "person_pager")     // downstream: the app notifies them

[<Fact>]
let ``Graph ignores the system of a person and skips one without a name`` () =
    let web = appWithPeople "Web" "Shop" [ "Customer", Relationship.External, Direction.Upstream; "", Relationship.External, Direction.Upstream ]
    let web = { web with Collaborators = web.Collaborators |> List.map (fun c -> { c with System = "Elsewhere" }) }
    let g = Haya.Core.Graph.ofApps Detail.Apps [ web ]
    Assert.DoesNotContain(g.Nodes, fun n -> n.Id = "elsewhere") // no system node from the person
    Assert.Equal(1, g.Nodes |> List.filter (fun n -> n.Kind = Haya.Core.Graph.NodeKind.Person) |> List.length)
    Assert.Equal(1, g.Edges.Length)

[<Fact>]
let ``Graph json marks a person with the person kind and no hasMeta`` () =
    let json = Haya.Core.Graph.ofApps Detail.Apps [ appWithPeople "Web" "Shop" [ "Customer", Relationship.External, Direction.Upstream ] ] |> Haya.Core.Encode.graph |> Haya.Core.Serializer.toScriptSafeJson
    use doc = System.Text.Json.JsonDocument.Parse json
    let person = doc.RootElement.GetProperty("nodes").EnumerateArray() |> Seq.find (fun n -> n.GetProperty("id").GetString() = "person_customer")
    Assert.Equal("person", person.GetProperty("kind").GetString())
    Assert.Equal("External", person.GetProperty("relationship").GetString())
    let mutable ignored = Unchecked.defaultof<System.Text.Json.JsonElement>
    Assert.False(person.TryGetProperty("hasMeta", &ignored))
    Assert.False(person.TryGetProperty("ownSystem", &ignored))

[<Fact>]
let ``System diagram draws people inside or outside the enterprise and relates them to the system`` () =
    let web = appWithPeople "Web" "Shop" [ "Customer", Relationship.External, Direction.Upstream; "Support", Relationship.Internal, Direction.Upstream; "Pager", Relationship.Internal, Direction.Downstream ]
    let text = Haya.Core.Diagram.sprintMermaidC4L1 [ web ]
    Assert.Contains("Person_Ext(person_customer, Customer, \"Customer description\")", text)
    Assert.Contains("  Person(person_support, Support, \"Support description\")", text) // indented: inside the boundary
    Assert.Contains("Rel(person_customer, shop, \"uses\")", text)
    Assert.Contains("Rel(shop, person_pager, \"uses\")", text)
    Assert.DoesNotContain("System_Ext", text) // a person is not an external system
    Assert.DoesNotContain("System(customer", text)

[<Fact>]
let ``Container diagram draws a person outside the boundaries and relates it to the app`` () =
    let web = appWithPeople "Web" "Shop" [ "Customer", Relationship.External, Direction.Upstream; "Support", Relationship.Internal, Direction.Upstream ]
    let text = Haya.Core.Diagram.sprintMermaidC4L2 [ web ]
    Assert.Contains("Person_Ext(person_customer, \"Customer\", \"Customer description\")", text)
    Assert.Contains("Person(person_support, \"Support\", \"Support description\")", text)
    Assert.Contains("Rel(person_customer, shop_web, \"uses\")", text)
    Assert.DoesNotContain("Container(person", text)
    Assert.Equal(1, text.Split("System_Boundary(").Length - 1) // no boundary for the person

[<Fact>]
let ``Backstage and Datadog leave people out`` () =
    let web = appWithPeople "Web" "Shop" [ "Customer", Relationship.External, Direction.Upstream ]
    let backstage = Haya.Core.Backstage.generateCatalog { InputPaths = []; OutputPath = ""; Format = Haya.Core.DataFormat.Yaml; CurrentDirectory = "" } [ web ]
    Assert.DoesNotContain("ustomer", backstage)
    let datadog = Haya.Core.Datadog.generateCatalog { InputPaths = []; OutputPath = ""; Format = Haya.Core.DataFormat.Yaml; CurrentDirectory = "" } [ web ]
    Assert.DoesNotContain("ustomer", datadog)
    Assert.Contains("shop-web", datadog)
