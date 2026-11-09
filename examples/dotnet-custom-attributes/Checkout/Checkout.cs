using Acme.Attributes;
using Haya;
[assembly: AcmeMeta(
    AppName = "CheckoutApi",
    Description = "Handles checkout for a shopping cart",
    Kind = AppKind.Api)]

namespace Checkout;

[Responsibility(Description = "Takes payment for an order")]
public class PayForOrder
{ }

// Owner, System, Technology and AppName come from [CallsPayments]; only what differs is written here.
[CallsPayments(DataDescription = "Charge a card")]
public class CardCharger
{ }

// A value written here overrides the default.
[CallsPayments(Protocol = "HTTPS", DataDescription = "Payment confirmation webhook", Direction = Direction.Upstream)]
public class PaymentWebhook
{ }

// Plain attributes still work next to custom ones.
[Collaborator(AppName = "Shipping Service",
    System = "Shipping",
    Relationship = Relationship.External,
    Description = "Handles shipping of orders",
    DataDescription = "Ship order")]
public class ShipmentRequester
{ }
