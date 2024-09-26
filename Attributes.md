# Attributes

Haya has 3 attributes that can be used to add metadata to your codebase. 
These attributes are used to generate CRC descriptions and diagrams.

## Meta

The `MetaAttribute` attribute is used to define metadata about the application represented by the codebase containing the attributes.

The `MetaAttribute` attribute has the following properties:

- `AppName` - The name of the application.
- `Description` - A description of the application.
- `System` - The system that the application belongs to.
- `Repository` - The VCS repository where the codebase is stored.

```csharp
using Haya;
[assembly:Meta(
    AppName = "CheckoutApi", 
    Description = "Handles processing payment for a shopping cart", 
    System = "HayaEcomm"
    Repository = "dburriss/haya-net")]
```
## Responsibility

The `ResponsibilityAttribute` attribute is used to define a responsibility that an application has.
Also often defined as a usecase, feature, or capability.

The `ResponsibilityAttribute` attribute has the following properties:

- `Description` - A description of the responsibility.

```csharp
[Responsibility(Description = "Handles order processing")]
public class CreditCardPaymentUsecase
{ }
```

## Collaborator

The `CollaboratorAttribute` attribute is used to define a collaborator that an application has.
A collaborator is another application or system that the application interacts with.

The `CollaboratorAttribute` attribute has the following properties:

- `Direction` - The direction of the interaction. Can be `Upstream` or `Downstream`.
- `Relationship` - The relationship the team has with the collaborator. Can be `Owned`, `Shared`, `Internal` or `External`.
- `Description` - A description of the collaborator.
- `Technology` - The technology used by the collaborator.
- `Protocol` - The protocol/technology used to communicate with the collaborator.
- `DataDescription` - A description of the data that is exchanged with the collaborator.
- `AppName` - The name of the collaborator application.
- `System` - The system that the collaborator belongs to.
- `Repository` - The VCS repository where the collaborator codebase is stored.

```csharp
[Collaborator(AppName = "Shipping Service",
    Relationship = Relationship.Internal,
    Direction = Direction.Downstream,
    System = "Shipping",
    Description = "Handles shipping of orders",
    DataDescription = "Ship order")]
public class ShippingService
{ }
```
### Relationships

- `Owned` - The team owns the collaborator.
- `Shared` - The team shares the collaborator with other teams.
- `Internal` - The collaborator is part of the same system.
- `External` - The collaborator is part of a different system or organisation.

### Directions

- `Upstream` - The collaborator sends data to the application.
- `Downstream` - The application sends data to the collaborator.
