module DescriptorTests

open FSharpPlus
open Haya.Core.Analysis
open Haya.Core.Tests
open Xunit

let responsibilityAttr = Haya.ResponsibilityAttribute(Description = "Handles order processing")
let collaboratorAttr = Haya.CollaboratorAttribute(Direction = Haya.Direction.Upstream, Protocol = "HTTP", DataDescription = "JSON", Description = "Handles order processing", AppName = "OrderService", System = "Order", Relationship = Haya.Relationship.Shared, Repository = "OrderRepository")
let metaAttr = Haya.MetaAttribute(AppName = "OrderService", Description = "Handles order processing", Team = "OrderTeam", System = "Order", Repository = "OrderRepository")

[<Fact>]
let ``Descriptors returned for ResponsibilityAttribute`` () = task {
    let code = [
                Code.attrString responsibilityAttr
                Code.classString "CreditCardPaymentUsecase"
               ] |> Code.init |> string
    let sln = code |> SolutionBuilder.code |> SolutionBuilder.createSolution
    let! descriptor = sln
                       |> Describe.getDescriptors Describe.attributeNames
                       |> Task.map(fun ds -> ds |> Describe.responsibilities)
                       // |> Task.map(fun rs -> rs |> List.tryPick (fun r -> if r.Description = responsibilityAttr.Description then Some r else None))
    // Assert.True(descriptor.IsSome)
    Assert.True(false)
}

[<Fact>]
let ``Descriptors returned for CollaboratorAttribute`` () = task {
    let code = [
                Code.attrString collaboratorAttr
                Code.classString "OrderService"
               ] |> Code.init |> string
    let sln = code |> SolutionBuilder.code |> SolutionBuilder.createSolution
    let! descriptors = sln |> Describe.getDescriptors Describe.attributeNames
    Assert.Equal(1, descriptors |> List.length)
}

[<Fact>]
let ``Descriptors returned for MetaAttribute`` () = task {
    let code = [
                Code.attrString metaAttr
               ] |> Code.init |> string
    let sln = code |> SolutionBuilder.code |> SolutionBuilder.createSolution
    let! descriptors = sln |> Describe.getDescriptors Describe.attributeNames
    Assert.Equal(1, descriptors |> List.length)
    let meta = descriptors |> Describe.metas |> List.head
    Assert.Equal("OrderService", meta.AppName)
}
