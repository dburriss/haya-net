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

[<Fact>]
let ``Describe output decodes back to the same descriptors in json and yaml`` () =
    let serialize format descriptors =
        Haya.Core.Describe.serialize { PathToSln = ""; OutputPath = ""; Format = format; CurrentDirectory = "" } descriptors
    let check format fileName =
        let text = serialize format allDescriptors
        match Haya.Core.Decode.descriptors fileName text with
        | Ok decoded -> Assert.Equal(text, serialize format decoded)
        | Error e -> failwith e
    check Haya.Core.DataFormat.Json "haya.json"
    check Haya.Core.DataFormat.Yaml "haya.yaml"

[<Fact>]
let ``Decode rejects documents that are not describe output`` () =
    Assert.True(Result.isError (Haya.Core.Decode.descriptors "x.json" "{\"foo\": 1}"))
    Assert.True(Result.isError (Haya.Core.Decode.descriptors "x.json" "not json"))
