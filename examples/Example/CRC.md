# CRC

This document describes the components, responsibilities, and collaborators for a codebase.

## Responsibilities

|Responsibility|Component|
|---|---|
|Handles order processing|[CreditCardPaymentUsecase](/Users/devon.burriss/Documents/GitHub/haya-net/examples/Example/HayaEcomm.cs)|

## Collaborators

|Collaborator|Component|Description|
|---|---|---|
|Checkout|[PaymentsController](/Users/devon.burriss/Documents/GitHub/haya-net/examples/Example/HayaEcomm.cs)|Handles cart checkout for a customer|
|Visa Payment Provider|[VisaPaymentService](/Users/devon.burriss/Documents/GitHub/haya-net/examples/Example/HayaEcomm.cs)|Handles payment processing of Visa cards|
|Shipping Service|[ShippingService](/Users/devon.burriss/Documents/GitHub/haya-net/examples/Example/HayaEcomm.cs)|Handles shipping of orders|

```mermaid
C4Context
title C4 System Diagram for HayaEcomm
System_Ext(visa, VISA, "Handles payment processing of Visa cards")

Enterprise_Boundary(Enterprise, Enterprise, "The enterprise boundary") {
  System(webshop, WebShop, "Handles cart checkout for a customer")
  System(shipping, Shipping, "Handles shipping of orders")
  System(hayaecomm, HayaEcomm, "Handles processing payment for a shopping cart")
}

Rel(webshop, hayaecomm, "Request payment")
Rel(hayaecomm, visa, "Process payment")
Rel(hayaecomm, shipping, "Ship order")

```
