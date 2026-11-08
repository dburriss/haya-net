using Haya;
namespace Adapters;
[Collaborator(AppName = "Visa", Relationship = Relationship.External, Direction = Direction.Downstream, System = "VISA", Description = "Card payments", Technology = "HTTP", Protocol = "HTTPS", DataDescription = "Charge")]
public class VisaAdapter { }
