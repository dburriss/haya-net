module DescriptorTests

open Example
open Haya
open Haya.Core.Analysis
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
                        return! sln |> Descriptor.getDescriptors "" Descriptor.attributeNames
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

