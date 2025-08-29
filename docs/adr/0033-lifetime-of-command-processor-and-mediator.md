# 33. Lifetime of Command Processor and Mediator

Date: 2025-08-28

## Status

Draft

## Context

See also [ADR #25](0025-use-reactive-programming-for-mediator.md) for context.

The responsibilities for outbox, producer, and transaction provider have shifted from being managed directly in the `CommandProcessor` to being managed via the `OutboxProducerMediator` (formerly `ExternalServiceBus`). The `CommandProcessor` now delegates `IAmAnOutbox` and `IAmAMessageProducer` operations to the `OutboxProducerMediator`, which acts as a singleton for the application. 

Historical Evolution
1. Early Versions
   - In earlier versions of Brighter, the producer was an instance field of the `CommandProcessor`, but typically the `CommandProcessor` was a singleton.
   Once the `IAmAnOutbox` was added to the architecture, to reduce the complexity of the `CommandProcessor`, and centralize the `IAmAMessageProducer` code, an `ExternalServiceBus` (now `OutboxProducerMediator`) was introduced. The`ExternalServiceBus` (now `OutboxProducerMediator`) took on the responsibility of managing both the `IAmAnOutbox` and the `IAmAMessageProducer`.
   - The `ExternalServiceBus` was injected as a dependency into the `CommandProcessor` instance, and not managed as a static singleton.
2. Refactoring to Static Singleton
   - The need for a single, application-wide `IAmAnOutbox` became clear to avoid multiple `IAmAnOutbox` instances. This was a particular concern with an `InMemoryOutbox`, where an instance of the `CommandProcessor` would have ended up with its own instance of an `InMemoryOutbox`, and thus `Clear` would not find messages that had been deposited. 
    - As `ExternalServiceBus` (now `OutboxProducerMediator`) held the `IAmAnOutbox` it was refactored to be static. The usage of a singleton `IAmAMessageProducer` also had benefits. 
    - The static singleton pattern was adopted to ensure that only one instance of the`ExternalServiceBus` (now `OutboxProducerMediator`) existed per instance. This change is reflected in the use of private static fields and the double-lock pattern in `InitExtServiceBus`. An alternative would be to have managed this by DI, but this would have added complexity to the setup of Brighter.
3. CommandProcessor Lifetime
   - For producers, originally, the `CommandProcessor` was typically configured as a singleton, but this became problematic when scoping a `DbContext` per request in web applications. 
     - To address this, the `CommandProcessor` was changed to allow it to be scoped per request (or transient), allowing it to work with a `DbContext` that is also scoped per request.
   - For consumers, the `CommandProcessor` is scoped to the `Dispatcher`.
     - This is a change, originally the the Dispatcher took a `Func<IAmACommandProcessorProvider>` instead of a direct IAmACommandProcessor
     - Using a factory (`Func<IAmACommandProcessorProvider>`) allowed the `Dispatcher` to be decoupled from a single instance of the `CommandProcessor`.
     - In scenarios where the `CommandProcessor` has a dependency with a scoped lifetimes (e.g., per message, per request, or per thread), a factory allowed the `Dispatcher` to create a new `CommandProcessor` for each scope. This allows us to support an `IAmABoxTransactionProvider` that manages a `DbContext` scoped per message.
     - This enabled the `Dispatcher` to obtain a fresh or context-specific instance of the `CommandProcessor` when needed, rather than being tied to a single instance for its lifetime.
   - Even where the `CommandProcessor` is created with a scoped or transient lifetime, with scope matching to its calling context, it still relies on the static `OutboxProducerMediator` for outbox and producer operations, ensuring that there is only one outbox.
     - The static fields are resettable via `ClearServiceBus` for testing purposes, but in production, they are set once and used for the lifetime of the application.
4. Transaction Provider
   - The `IAmABoxTransactionProvider`, which manages database transactions for the Outbox, to ensure transactional messaging, is also a Singleton.
   - To simplify configuration the type of transaction (which varies from database to database) is now set on the static OutboxProducerMediator, but inspecting the type of the transaction provider passed into the CommandProcessor. As OutboxProducerMediator is static, this means that the transaction provider type is set once for the lifetime of the application. As a consequence of this we made the transaction provider a Singleton as well.

Problems with this Design
- The static singleton pattern can lead to issues in testing, as tests may interfere with each other if they rely on the same static instance. This is mitigated by the `ClearServiceBus` method, which resets the static fields for testing purposes.
- Because we only have a single `OutboxProducerMediator`, we cannot have different configurations of the Outbox for different `CommandProcessor `instances. This is a limitation but it is likely that most applications will only need one configuration for the `IAmAnOutbox`.
- Because the `IAmABoxTransactionProvider` is also static, we cannot have different transaction providers for different `CommandProcessor` instances. This is an issue where the concrete implementation of the`IAmABoxTransactionProvider` has state. For EntityFramework the recommendation is to scope the `DbContext` per request, and use a new instance of the `CommandProcessor` per request. This causes errors because our static `TransactionProvider` is set once, and holds a reference to the `DbContext` that was passed in when it was set. This means that if the `CommandProcessor` is created in a different scope, it will have a different `DbContex`t instance, but the static `IAmABoxTransactionProvider` will still hold a reference to the old `DbContext` instance. This can lead to errors when trying to use the `IAmABoxTransactionProvider` with a different `DbContext` instance.
- Removing the `Func<IAmACommandProcessorProvider>` from the `Dispatcher` means that we cannot create a new `CommandProcessor` for each message, which could be an issue if the `CommandProcessor` has dependencies with scoped lifetimes. However, this is mitigated by the fact that the `CommandProcessor` can still be scoped to the `Dispatcher`, allowing it to work with a `DbContext` that is also scoped per message.

## Decision

Given we want to control the scope of V10, we need to remove errors caused by the static `IAmABoxTransactionProvider` holding a reference to a `DbContext` that is out of scope.
- we will not fix the issue about the possibility of multiple `IAmAnOutbox`. It may need to be addressed in a future version, if we see demand for it. 
- The fix here is to make the `IAmABoxTransactionProvider` non-static. It is already passed into the `OutboxProducerMediator` via a method call, rather than being set when the `CommandProcessor` is created. 
- As such, it should be easy to refactor `CommandProcessor` to use a scoped instance instead. This means that the `IAmABoxTransactionProvider` will be scoped to the `CommandProcessor` instance, and will not hold a reference to a `DbContext` that is out of scope.

We need to restore the use of a `Func<IAmACommandProcessorProvider>` in the `Dispatcher` to allow the `Dispatcher` to create a new `CommandProcessor` for each message, allowing it to work with a `DbContext` that is also scoped per message.

- Replace the direct `IAmACommandProcessor` parameter with a Func<IAmACommandProcessorProvider>.
- Wherever the `Dispatcher` currently uses the `CommandProcessor` field, change it to invoke the factory.
- If any code that creates a `Dispatcher` expects to pass an instance, update it to pass a factory instead.
- Update XML documentation comments to reflect the new constructor parameter and its purpose.


## Consequences

- The `OutboxProducerMediator` will remain a static singleton, as we want to ensure that there is only one instance of the outbox/producer for the application.
- The `IAmABoxTransactionProvider`will be scoped to the `CommandProcessor` instance, allowing it to work with a `DbContext` that is also scoped per request.
- This change will require updates to the documentation to reflect the new lifetime of the `IAmABoxTransactionProvider`.
- Testing will still be possible, as the `ClearServiceBus` method will still be available to reset the static fields for testing purposes.
- We will need to ensure that the `IAmABoxTransactionProvider` is properly disposed of when the `CommandProcessor` is disposed of, to avoid memory leaks.