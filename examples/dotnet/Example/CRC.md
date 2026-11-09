# CRC

This document describes the components, responsibilities, and collaborators for a codebase.

## Responsibilities

|Responsibility|Component|
|---|---|
|Handles order processing|[CreditCardPaymentUsecase](HayaEcomm.cs)|

## Collaborators

|Collaborator|Component|Description|
|---|---|---|
|Checkout|[PaymentsController](HayaEcomm.cs)|Handles cart checkout for a customer|
|Visa Payment Provider|[VisaPaymentService](HayaEcomm.cs)|Handles payment processing of Visa cards|
|Shipping Service|[ShippingService](HayaEcomm.cs)|Handles shipping of orders|

```mermaid
C4Context
title C4 System Diagram for Ordering

Enterprise_Boundary(Enterprise, Enterprise, "The enterprise boundary") {
  System(webshop, WebShop, "Handles cart checkout for a customer")
  System(shipping, Shipping, "Handles shipping of orders")
  System(ordering, Ordering, "Handles processing payment for a shopping cart")
}
System_Ext(visa, VISA, "Handles payment processing of Visa cards")

Rel(webshop, ordering, "Request payment")
Rel(ordering, visa, "Process payment")
Rel(ordering, shipping, "Ship order")

```

```mermaid
C4Container
title C4 Container Diagram for Ordering

System_Boundary(ordering, "Ordering") {
  Container(ordering_payments_api, "PaymentsApi", ".NET 10 / ASP.NET Core", "Handles processing payment for a shopping cart")
}
System(web_shop, "WebShop", "Handles cart checkout for a customer")
System_Ext(visa, "VISA", "Handles payment processing of Visa cards")
System(shipping, "Shipping", "Handles shipping of orders")

Rel(web_shop, ordering_payments_api, "Request payment", "HTTPS")
Rel(ordering_payments_api, visa, "Process payment", "HTTPS")
Rel(ordering_payments_api, shipping, "Ship order", "MQTT")

```
