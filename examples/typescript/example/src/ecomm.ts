import { AppKind, Collaborator, Direction, meta, Relationship, Responsibility } from "@haya/annotations";

meta({
  appName: "PaymentsApi",
  description: "Handles processing payment for a shopping cart",
  system: "Ordering",
  owner: "Ordering Team",
  repository: "org/payment-api",
  technology: ".NET 10 / ASP.NET Core",
  kind: AppKind.Api,
  lifecycle: "production",
  tier: "1",
});

@Responsibility({ description: "Handles order processing" })
export class CreditCardPaymentUsecase {}

@Collaborator({
  appName: "Checkout",
  relationship: Relationship.Internal,
  direction: Direction.Upstream,
  system: "WebShop",
  description: "Handles cart checkout for a customer",
  technology: "TypeScript",
  protocol: "HTTPS",
  dataDescription: "Request payment",
  repository: "org/checkout-api",
  owner: "Checkout Team",
})
export class PaymentsController {}

@Collaborator({
  appName: "Visa Payment Provider",
  protocol: "HTTPS",
  relationship: Relationship.External,
  direction: Direction.Downstream,
  system: "VISA",
  description: "Handles payment processing of Visa cards",
  dataDescription: "Process payment",
  repository: "org/webshop",
  owner: "Visa",
})
export class VisaPaymentService {}

@Collaborator({
  appName: "Shipping Service",
  relationship: Relationship.Internal,
  direction: Direction.Downstream,
  system: "Shipping",
  description: "Handles shipping of orders",
  technology: "dotnet",
  protocol: "MQTT",
  dataDescription: "Ship order",
  repository: "org/shipping-service",
  owner: "Shipping Team",
})
export class ShippingService {}

@Collaborator({
  appName: "Customer",
  kind: AppKind.Person,
  relationship: Relationship.External,
  direction: Direction.Upstream,
  description: "Pays for the items in their cart",
  dataDescription: "Pays for an order",
})
export class CustomerPayment {}
