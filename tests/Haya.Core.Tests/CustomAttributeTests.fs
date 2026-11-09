module CustomAttributeTests

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

// Custom attributes derive from a Haya attribute and bake in defaults with the matching `*Defaults` attribute.
// These tests compile small C# snippets in memory, so no MSBuild is needed.

let private compiled (assemblyName: string) (source: string) = compileLibrary assemblyName true source

/// Analyse `source` as the only document of a C# project that has the given extra references.
let private analyse (extraReferences: MetadataReference list) (source: string) : Descriptor list =
    (analyseWith Roslyn.ReferenceOptions.none extraReferences source).Descriptors

let private collaborators ds = ds |> Descriptor.collaborators
let private responsibilities ds = ds |> Descriptor.responsibilities
let private metas ds = ds |> Descriptor.metas

let private customCollaborator = """
using Haya;
[CollaboratorDefaults(AppName = "Payments", Protocol = "gRPC", System = "Acme", Direction = Direction.Upstream, Relationship = Relationship.External)]
public class CallsPaymentsAttribute : CollaboratorAttribute { }
"""

let private usage = """
[CallsPayments(DataDescription = "charge")]
public class OrderService { }
"""

[<Fact>]
let ``A custom collaborator attribute applies its defaults`` () =
    let c = analyse [] (customCollaborator + usage) |> collaborators |> List.exactlyOne
    Assert.Equal("Payments", c.AppName)
    Assert.Equal("gRPC", c.Protocol)
    Assert.Equal("Acme", c.System)
    Assert.Equal(Direction.Upstream, c.Direction)
    Assert.Equal(Relationship.External, c.Relationship)
    Assert.Equal("charge", c.DataDescription)
    Assert.Equal("OrderService", c.ComponentName)

let private overrideUsage = """
[CallsPayments(AppName = "Refunds", Direction = Direction.Downstream)]
public class RefundService { }
"""

[<Fact>]
let ``Arguments at the usage site override the defaults`` () =
    let c = analyse [] (customCollaborator + overrideUsage) |> collaborators |> List.exactlyOne
    Assert.Equal("Refunds", c.AppName)
    Assert.Equal(Direction.Downstream, c.Direction)
    Assert.Equal("gRPC", c.Protocol)

[<Fact>]
let ``A custom attribute class is not reported as an annotation itself`` () =
    Assert.Empty(analyse [] customCollaborator)

let private noDefaults = """
using Haya;
public class CallsAnythingAttribute : CollaboratorAttribute { }
[CallsAnything(AppName = "Thing")]
public class Client { }
"""

[<Fact>]
let ``A custom attribute without defaults is found and behaves like the base attribute`` () =
    let c = analyse [] noDefaults |> collaborators |> List.exactlyOne
    Assert.Equal("Thing", c.AppName)
    Assert.Equal("Client", c.ComponentName)

let private chained = """
using Haya;
[CollaboratorDefaults(AppName = "Base", Protocol = "AMQP", System = "Acme")]
public class BaseCallsAttribute : CollaboratorAttribute { }
[CollaboratorDefaults(AppName = "Derived")]
public class DerivedCallsAttribute : BaseCallsAttribute { }
[DerivedCalls]
public class Client { }
"""

[<Fact>]
let ``Defaults are inherited down a chain of custom attributes and derived classes override`` () =
    let c = analyse [] chained |> collaborators |> List.exactlyOne
    Assert.Equal("Derived", c.AppName)
    Assert.Equal("AMQP", c.Protocol)
    Assert.Equal("Acme", c.System)

let private customResponsibility = """
using Haya;
[ResponsibilityDefaults(Description = "Place orders")]
public class UseCaseAttribute : ResponsibilityAttribute { }
[UseCase]
public class OrderService { }
"""

[<Fact>]
let ``A custom responsibility attribute applies its defaults`` () =
    let r = analyse [] customResponsibility |> responsibilities |> List.exactlyOne
    Assert.Equal("Place orders", r.Description)
    Assert.Equal("OrderService", r.ComponentName)

let private customMeta = """
using Haya;
[assembly: AcmeMeta(AppName = "Shop")]
[MetaDefaults(Owner = "Acme Team", System = "Acme", Kind = AppKind.Api, Technology = ".NET 10")]
public class AcmeMetaAttribute : MetaAttribute { }
"""

[<Fact>]
let ``A custom meta attribute applies its defaults at assembly level`` () =
    let m = analyse [] customMeta |> metas |> List.exactlyOne
    Assert.Equal("Shop", m.AppName)
    Assert.Equal("Acme Team", m.Owner)
    Assert.Equal("Acme", m.System)
    Assert.Equal(AppKind.Api, m.Kind)
    Assert.Equal(".NET 10", m.Tech)

[<Fact>]
let ``A custom attribute from a compiled reference applies its defaults`` () =
    let library = compiled "Acme.HayaAttributes" customCollaborator
    let c = analyse [ library ] usage |> collaborators |> List.exactlyOne
    Assert.Equal("Payments", c.AppName)
    Assert.Equal("gRPC", c.Protocol)
    Assert.Equal("charge", c.DataDescription)
