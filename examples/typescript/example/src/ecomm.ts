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
export class CreditCardPaymentUsecase {
  constructor(
    private readonly gateway: CardGateway,
    private readonly shipping: ShippingService,
  ) {}

  async pay(orderId: string, cardToken: string, amountInCents: number): Promise<boolean> {
    const paid = await this.gateway.charge(cardToken, amountInCents);
    if (paid) await this.shipping.ship(orderId);
    return paid;
  }
}

// Not annotated: walked through when tracing dependencies between annotated classes (ADR018).
export class PaymentService {
  constructor(private readonly gateway: CardGateway) {}

  authorise(cardToken: string, amountInCents: number): Promise<boolean> {
    return this.gateway.charge(cardToken, amountInCents);
  }
}

export class CardGateway {
  constructor(private readonly visa: VisaPaymentService) {}

  charge(cardToken: string, amountInCents: number): Promise<boolean> {
    return this.visa.charge(cardToken, amountInCents);
  }

  retryWith(service: PaymentService, cardToken: string, amountInCents: number): Promise<boolean> {
    return service.authorise(cardToken, amountInCents);
  }
}

export interface IPaymentFlow {
  start(orderId: string, cardToken: string, amountInCents: number): Promise<boolean>;
}

export class CardPaymentFlow implements IPaymentFlow {
  constructor(private readonly usecase: CreditCardPaymentUsecase) {}

  start(orderId: string, cardToken: string, amountInCents: number): Promise<boolean> {
    return this.usecase.pay(orderId, cardToken, amountInCents);
  }
}

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
export class PaymentsController {
  constructor(
    private readonly usecase: CreditCardPaymentUsecase,
    private readonly payments: PaymentService,
  ) {}

  post(orderId: string, cardToken: string, amountInCents: number): Promise<boolean> {
    return this.usecase.pay(orderId, cardToken, amountInCents);
  }

  authorise(cardToken: string, amountInCents: number): Promise<boolean> {
    return this.payments.authorise(cardToken, amountInCents);
  }
}

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
export class VisaPaymentService {
  readonly endpoint: string = "https://visa.example/pay";

  charge(cardToken: string, amountInCents: number): Promise<boolean> {
    return Promise.resolve(amountInCents > 0);
  }
}

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
export class ShippingService {
  ship(orderId: string): Promise<void> {
    return Promise.resolve();
  }
}

@Collaborator({
  appName: "Customer",
  kind: AppKind.Person,
  relationship: Relationship.External,
  direction: Direction.Upstream,
  description: "Pays for the items in their cart",
  dataDescription: "Pays for an order",
})
export class CustomerPayment {
  constructor(private readonly flow: IPaymentFlow) {}

  pay(orderId: string, cardToken: string, amountInCents: number): Promise<boolean> {
    return this.flow.start(orderId, cardToken, amountInCents);
  }
}
