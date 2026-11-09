using Haya;

// Its Meta describes the library, not the app that uses it, so haya ignores it.
[assembly: Meta(AppName = "Acme.Shared", Description = "Shared library")]

namespace Acme.Shared;

[Responsibility(Description = "Validates delivery addresses")]
public class AddressValidator
{ }

[Collaborator(AppName = "Geocoding", System = "Acme", Relationship = Relationship.Internal,
    Description = "Resolves an address to coordinates", Technology = ".NET 10", Protocol = "HTTPS",
    DataDescription = "Address lookup")]
public class GeocodingClient
{ }

// Internal types are not read from a referenced assembly.
[Responsibility(Description = "Not reported")]
internal class Cache
{ }
