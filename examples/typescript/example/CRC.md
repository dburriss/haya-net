# CRC

This document describes the components, responsibilities, and collaborators for a codebase.

## Responsibilities

|Responsibility|Component|
|---|---|
|Handles order processing|[CreditCardPaymentUsecase](src/ecomm.ts)|

## Collaborators

|Collaborator|Component|Description|
|---|---|---|
|Checkout|[PaymentsController](src/ecomm.ts)|Handles cart checkout for a customer|
|Visa Payment Provider|[VisaPaymentService](src/ecomm.ts)|Handles payment processing of Visa cards|
|Shipping Service|[ShippingService](src/ecomm.ts)|Handles shipping of orders|

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
