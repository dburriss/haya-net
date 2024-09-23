module DescriptorTests

open Haya.Core.Analysis
open Haya.Core.Tests
open Xunit
open SolutionBuilder

[<Fact>]
let ``Descriptors returned for ResponsibilityAttribute`` () = task {
    let sln =
        // [Haya.ResponsibilityAttribute(Description = "Handles order processing")]
        // public class CreditCardPaymentUsecase { }
        [
            Code.attrString (Haya.ResponsibilityAttribute(Description = "Handles order processing"))
            Code.classString "CreditCardPaymentUsecase"
        ] |> Code.append |> string |> code
        |> createSolution
        
    let! descriptors = sln |> Describe.getDescriptors Describe.attributeNames
    Assert.NotEmpty(descriptors |> List.ofSeq)
}
