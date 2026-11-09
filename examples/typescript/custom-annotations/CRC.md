# CRC

This document describes the components, responsibilities, and collaborators for a codebase.

## Responsibilities

|Responsibility|Component|
|---|---|
|Takes payment for an order|[PayForOrder](src/checkout.ts)|

## Collaborators

|Collaborator|Component|Description|
|---|---|---|
|Payments|[CardCharger](src/checkout.ts)||
|Payments|[PaymentWebhook](src/checkout.ts)||
|Shipping Service|[ShipmentRequester](src/checkout.ts)|Handles shipping of orders|

```mermaid
C4Context
title C4 System Diagram for Acme

Enterprise_Boundary(Enterprise, Enterprise, "The enterprise boundary") {
  System(acme, Acme, "Handles checkout for a shopping cart")
}
System_Ext(shipping, Shipping, "Handles shipping of orders")

Rel(acme, shipping, "Ship order")

```

```mermaid
C4Container
title C4 Container Diagram for Acme

System_Boundary(acme, "Acme") {
  Container(acme_checkout_api, "CheckoutApi", ".NET 10 / ASP.NET Core", "Handles checkout for a shopping cart")
  Container(acme_payments, "Payments", ".NET 10", "")
}
System_Ext(shipping, "Shipping", "Handles shipping of orders")

Rel(acme_checkout_api, acme_payments, "Charge a card", "gRPC")
Rel(acme_payments, acme_checkout_api, "Payment confirmation webhook", "HTTPS")
Rel(acme_checkout_api, shipping, "Ship order")

```
