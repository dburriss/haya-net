using Haya;
[assembly:Meta(
    AppName = "PaymentsApi", 
    Description = "Handles processing payment for a shopping cart", 
    System = "Ordering",
    Owner = "Ordering Team",
    Repository = "org/payment-api",
    Technology = ".NET 10 / ASP.NET Core",
    Kind = AppKind.Api,
    Lifecycle = "production",
    Tier = "1")]

namespace Example;

[Responsibility(Description = "Handles order processing")]
public class CreditCardPaymentUsecase(VisaPaymentService visa, ShippingService shipping)
{
    public async Task<bool> Pay(string orderId, string cardToken, int amountInCents)
    {
        var paid = await visa.Charge(cardToken, amountInCents);
        if (paid) await shipping.Ship(orderId);
        return paid;
    }
}

[Collaborator(AppName = "Checkout",
    Relationship = Relationship.Internal,
    Direction = Direction.Upstream,
    System = "WebShop",
    Description = "Handles cart checkout for a customer",
    Technology = "TypeScript",
    Protocol = "HTTPS",
    DataDescription = "Request payment",
    Repository = "org/checkout-api",
    Owner = "Checkout Team")]
public class PaymentsController(CreditCardPaymentUsecase usecase)
{
    public Task<bool> Post(string orderId, string cardToken, int amountInCents) => usecase.Pay(orderId, cardToken, amountInCents);
}

[Collaborator(AppName = "Visa Payment Provider",
    Protocol = "HTTPS",
    Relationship = Relationship.External,
    Direction = Direction.Downstream,
    System = "VISA",
    Description = "Handles payment processing of Visa cards",
    DataDescription = "Process payment",
    Repository = "org/webshop",
    Owner = "Visa")]
public class VisaPaymentService
{
    public string Endpoint { get; } = "https://visa.example/pay";

    public Task<bool> Charge(string cardToken, int amountInCents) => Task.FromResult(amountInCents > 0);
}

[Collaborator(AppName = "Shipping Service",
    Relationship = Relationship.Internal,
    Direction = Direction.Downstream,
    System = "Shipping",
    Description = "Handles shipping of orders",
    Technology = "dotnet",
    Protocol = "MQTT",
    DataDescription = "Ship order",
    Repository = "org/shipping-service",
    Owner = "Shipping Team")]
public class ShippingService
{
    public Task Ship(string orderId) => Task.CompletedTask;
}

[Collaborator(AppName = "Customer",
    Kind = AppKind.Person,
    Relationship = Relationship.External,
    Direction = Direction.Upstream,
    Description = "Pays for the items in their cart",
    DataDescription = "Pays for an order")]
public class CustomerPayment
{ }
