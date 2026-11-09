import { AppKind, Collaborator, Direction, Relationship, Responsibility } from "@haya/annotations";
import { CallsPayments, acmeMeta } from "./annotations.ts";

acmeMeta({
  appName: "CheckoutApi",
  description: "Handles checkout for a shopping cart",
  kind: AppKind.Api,
});

@Responsibility({ description: "Takes payment for an order" })
export class PayForOrder {}

// owner, system, technology and appName come from CallsPayments; only what differs is written here.
@CallsPayments({ dataDescription: "Charge a card" })
export class CardCharger {}

// A value written here overrides the default.
@CallsPayments({ protocol: "HTTPS", dataDescription: "Payment confirmation webhook", direction: Direction.Upstream })
export class PaymentWebhook {}

// Plain annotations still work next to custom ones.
@Collaborator({
  appName: "Shipping Service",
  system: "Shipping",
  relationship: Relationship.External,
  description: "Handles shipping of orders",
  dataDescription: "Ship order",
})
export class ShipmentRequester {}
