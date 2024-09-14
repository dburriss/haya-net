using Haya;
[assembly:Meta(AppName = "ExampleApp", Description = "Example of using Haya", System = "Haya")]

namespace Example;

[Responsibility(Description = "Handles order processing")]
public class CreditCardPaymentUsecase
{ }

[Collaborator(AppName = "Checkout",
    Relationship = Relationship.Internal,
    Direction = Direction.Upstream,
    System = "WebShop",
    Description = "Handles cart checkout for a customer",
    DataDescription = "Request payment")]
public class PaymentsController
{ }

[Collaborator(AppName = "Visa Payment Provider",
    Relationship = Relationship.External,
    Direction = Direction.Downstream,
    System = "VISA",
    Description = "Handles payment processing of Visa cards",
    DataDescription = "Process payment")]
public class VisaPaymentService
{ }
