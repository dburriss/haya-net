# CRC

This document describes the components, responsibilities, and collaborators for a codebase.

## Storefront

### Responsibilities

|Responsibility|Component|
|---|---|
|Shows the catalog and takes checkout requests|[CheckoutPage](Storefront/Storefront.cs)|

### Collaborators

|Collaborator|Component|Description|
|---|---|---|
|Orders|[OrdersClient](Storefront/Storefront.cs)|Accepts and tracks customer orders|

## Orders

### Responsibilities

|Responsibility|Component|
|---|---|
|Creates and tracks orders|[OrderService](Orders/Orders.cs)|

### Collaborators

|Collaborator|Component|Description|
|---|---|---|
|Storefront|[OrdersController](Orders/Orders.cs)|Customer facing web shop|
|Payments|[PaymentsClient](Orders/Orders.cs)|Takes payments for orders|
|Shipping Service|[ShippingPublisher](Orders/Orders.cs)|Handles shipping of orders|

## Payments

### Responsibilities

|Responsibility|Component|
|---|---|
|Authorises card payments|[PaymentService](Payments/Payments.cs)|

### Collaborators

|Collaborator|Component|Description|
|---|---|---|
|Orders|[PaymentsController](Payments/Payments.cs)|Accepts and tracks customer orders|
|Visa Payment Provider|[VisaAdapter](Payments/Payments.cs)|Handles payment processing of Visa cards|

```mermaid
C4Context
title C4 System Diagram for WebShop, Ordering, Billing

Enterprise_Boundary(Enterprise, Enterprise, "The enterprise boundary") {
  System(logistics, Logistics, "Handles shipping of orders")
  System(webshop, WebShop, "Customer facing web shop")
  System(ordering, Ordering, "Accepts and tracks customer orders")
  System(billing, Billing, "Takes payments for orders")
}
System_Ext(visa, VISA, "Handles payment processing of Visa cards")

Rel(webshop, ordering, "Place order")
Rel(ordering, billing, "Request payment")
Rel(ordering, logistics, "Ship order")
Rel(billing, visa, "Process payment")

```

```mermaid
C4Container
title C4 Container Diagram for WebShop, Ordering, Billing

System_Boundary(web_shop, "WebShop") {
  Container(web_shop_storefront, "Storefront", "TypeScript / React", "Customer facing web shop")
}
System_Boundary(ordering, "Ordering") {
  Container(ordering_orders, "Orders", ".NET 10 / ASP.NET Core", "Accepts and tracks customer orders")
}
System_Boundary(billing, "Billing") {
  Container(billing_payments, "Payments", ".NET 10 / ASP.NET Core", "Takes payments for orders")
}
System(logistics, "Logistics", "Handles shipping of orders")
System_Ext(visa, "VISA", "Handles payment processing of Visa cards")

Rel(web_shop_storefront, ordering_orders, "Place order", "HTTPS")
Rel(ordering_orders, billing_payments, "Request payment", "HTTPS")
Rel(ordering_orders, logistics, "Ship order", "MQTT")
Rel(billing_payments, visa, "Process payment", "HTTPS")

```
