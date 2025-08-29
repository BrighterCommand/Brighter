# 33. Lifetime of Command Processor and Mediator

Date: 2025-08-28

## Status

Draft

## Context

See also [ADR #25](0025-use-reactive-programming-for-mediator.md) for context.

The responsibilities for outbox, producer, and transaction provider have shifted from being managed directly in the CommandProcessor to being managed via the OutboxProducerMediator (formerly ExternalServiceBus). The CommandProcessor now delegates outbox and producer operations to the mediator, which acts as a singleton for the application. 

Historical Evolution
1. Early Versions
   - In earlier versions of Brighter, the producer was an instance field of the command processor, but typically the commandprocessor was a singleton.
   Once the Outbox was added to the architecture, to reduce the complexity of the command processor, and centralize the producer code, an ExternalServiceBus (now OutboxProducerMediator) was introduced. The external service bus (now OutboxProducerMediator) took on the responsibility of managing both the outbox and the producer.
   - The ExternalServiceBus was injected as a dependency into the CommandProcessor instance, and not managed as a static singleton.
2. Refactoring to Static Singleton
   - The need for a single, application-wide outbox became clear to avoid multiple outbox instances. This was a particular concern with an in-memory outbox, where an instance of the CommandProcessor would have ended up with its own instance of an Outbox, and thus Clear would not find messages that had been Deposited. 
    - As ExternalServiceBus (now OutboxProducerMediator) held the Outbox it was refactored to be static. The usage of a singleton Producer also had benefits. 
    - The static singleton pattern was adopted to ensure that only one instance of the outbox/producer mediator existed per instance. This change is reflected in the use of private static fields and the double-lock pattern in InitExtServiceBus. An alternative would be to have managed this by DI, but this would have added complexity to the setup of Brighter.
3. CommandProcessor Lifetime
   - Originally, the CommandProcessor was a singleton, but this became problematic when scoping a DbContext per request in web applications. To address this, the CommandProcessor was changed to be scoped per request, allowing it to work with a DbContext that is also scoped per request.
   - This change meant that while the CommandProcessor was created anew for each request, with scope matching to its calling context it still relied on the static OutboxProducerMediator for outbox and producer operations.
   - The static fields are resettable via ClearServiceBus() for testing purposes, but in production, they are set once and used for the lifetime of the application.
4. Transaction Provider
   - The transaction provider, which manages database transactions for the Outbox, to ensure transactional messaging, is also a Singleton.
   - To simplify configuration the type of transaction (which varies from database to database) is now set on the static OutboxProducerMediator, but inspecting the type of the transaction provider passed into the CommandProcessor. As OutboxProducerMediator is static, this means that the transaction provider type is set once for the lifetime of the application. As a consequence of this we made the transaction provider a Singleton as well.

Problems with this Design
- The static singleton pattern can lead to issues in testing, as tests may interfere with each other if they rely on the same static instance. This is mitigated by the ClearServiceBus method, which resets the static fields for testing purposes.
- Because we only have a single OutboxProducerMediator, we cannot have different configurations of the Outbox for different CommandProcessor instances. This is a limitation. It is likely that most applications will only need one configuration for the outbox.
- Because the TransactionProvider is also static, we cannot have different transaction providers for different CommandProcessor instances. This is an issue where the concrete implementation of the TransactionProvider has state. For EntityFramework the recommendation is to scope the DbContext per request, and use a new instance of the CommandProcessor per request. This causes errors because our static TransactionProvider is set once, and holds a reference to the DbContext that was passed in when it was set. This means that if the CommandProcessor is created in a different scope, it will have a different DbContext instance, but the static TransactionProvider will still hold a reference to the old DbContext instance. This can lead to errors when trying to use the TransactionProvider with a different DbContext instance.

## Decision

Given we want to control the scope of V10, we need to remove errors caused by the static TransactionProvider holding a reference to a DbContext that is out of scope, but we will leave the concern about the possibility of multiple outboxes to be addressed in a future version, if we see demand for it. The fix here is to make the TransactionProvider non-static. It is already passed into the OutboxProducerMediator via a method call, rather than being set when the CommandProcessor is created. As such, it should be easy to refactor CommandProcessor to use a scoped instance instead. This means that the TransactionProvider will be scoped to the CommandProcessor instance, and will not hold a reference to a DbContext that is out of scope.

## Consequences

- The OutboxProducerMediator will remain a static singleton, as we want to ensure that there is only one instance of the outbox/producer for the application.
- The TransactionProvider will be scoped to the CommandProcessor instance, allowing it to work with a DbContext that is also scoped per request.
- This change will require updates to the documentation to reflect the new lifetime of the TransactionProvider.
- Testing will still be possible, as the ClearServiceBus method will still be available to reset the static fields for testing purposes.
- We will need to ensure that the TransactionProvider is properly disposed of when the CommandProcessor is disposed of, to avoid memory leaks.