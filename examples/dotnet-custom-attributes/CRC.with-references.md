# CRC

This document describes the components, responsibilities, and collaborators for a codebase.

## Responsibilities

|Responsibility|Component|
|---|---|
|Takes payment for an order|[PayForOrder](Checkout/Checkout.cs)|
|Validates delivery addresses|AddressValidator (Acme.Shared::Acme.Shared.AddressValidator)|

## Collaborators

|Collaborator|Component|Description|
|---|---|---|
|Payments|[CardCharger](Checkout/Checkout.cs)||
|Payments|[PaymentWebhook](Checkout/Checkout.cs)||
|Shipping Service|[ShipmentRequester](Checkout/Checkout.cs)|Handles shipping of orders|
|Geocoding|GeocodingClient (Acme.Shared::Acme.Shared.GeocodingClient)|Resolves an address to coordinates|

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
  Container(acme_geocoding, "Geocoding", ".NET 10", "Resolves an address to coordinates")
}
System_Ext(shipping, "Shipping", "Handles shipping of orders")

Rel(acme_checkout_api, acme_payments, "Charge a card", "gRPC")
Rel(acme_payments, acme_checkout_api, "Payment confirmation webhook", "HTTPS")
Rel(acme_checkout_api, shipping, "Ship order")
Rel(acme_checkout_api, acme_geocoding, "Address lookup", "HTTPS")

```
