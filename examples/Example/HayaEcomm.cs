using Haya;
[assembly:Meta(
    AppName = "PaymentsApi", 
    Description = "Handles processing payment for a shopping cart", 
    System = "Ordering",
    Owner = "Ordering Team",
    Repository = "org/payment-api")]

namespace Example;

[Responsibility(Description = "Handles order processing")]
public class CreditCardPaymentUsecase
{ }

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
public class PaymentsController
{ }

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
{ }

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
{ }
