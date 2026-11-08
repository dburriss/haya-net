using Haya;
[assembly: Meta(AppName = "PaymentsApi", Description = "Pay", System = "Ordering", Owner = "Team", Repository = "org/pay")]
namespace Api;
[Responsibility(Description = "Orchestrates payments")]
public class PayUsecase { public static void Main() { new Adapters.VisaAdapter(); } }
