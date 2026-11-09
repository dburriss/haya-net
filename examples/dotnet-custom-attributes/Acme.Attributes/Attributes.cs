using Haya;

namespace Acme.Attributes;

// Organisation-wide attributes: each derives from a Haya attribute and bakes in values with the matching *Defaults
// attribute. Values written where the attribute is used override these defaults.

[MetaDefaults(Owner = "Acme Team", System = "Acme", Technology = ".NET 10 / ASP.NET Core")]
public class AcmeMetaAttribute : MetaAttribute
{ }

[CollaboratorDefaults(AppName = "Payments", System = "Acme", Protocol = "gRPC", Technology = ".NET 10")]
public class CallsPaymentsAttribute : CollaboratorAttribute
{ }
