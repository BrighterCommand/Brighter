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
   - The `ExternalServiceBus` was injected as a dependency into the `CommandProcessor` instance.
2. Refactoring to Static Singleton
   - The need for a single, application-wide `IAmAnOutbox` became clear to avoid multiple `IAmAnOutbox` instances. This was a particular concern with an `InMemoryOutbox`, where an instance of the `CommandProcessor` would have ended up with its own instance of an `InMemoryOutbox`, and thus `Clear` would not find messages that had been deposited. 
    - As `ExternalServiceBus` (now `OutboxProducerMediator`) held the `IAmAnOutbox` it was refactored to be static. The usage of a singleton `IAmAMessageProducer` also had benefits. 
    - The static singleton pattern was adopted to ensure that only one instance of the`ExternalServiceBus` (now `OutboxProducerMediator`) existed per instance. This change is reflected in the use of private static fields and the double-lock pattern in `InitExtServiceBus`. An alternative would be to have managed this by DI, but this would have added complexity to the setup of Brighter.
3. CommandProcessor Lifetime
   - For producers, originally, the `CommandProcessor` was typically configured as a singleton, but this became problematic when scoping a `DbContext` per request in web applications. 
     - To address this, the `CommandProcessor` was changed to allow it to be scoped per request (or transient), allowing it to work with a `DbContext` that is also scoped per request.
   - For consumers, the `CommandProcessor` is scoped to the `Dispatcher`.
     - This is a change in V10, originally the the Dispatcher took a `Func<IAmACommandProcessorProvider>` instead of a direct IAmACommandProcessor
     - Using a factory (`Func<IAmACommandProcessorProvider>`) allowed the `Dispatcher` to be decoupled from a single instance of the `CommandProcessor`.
     - In scenarios where the `CommandProcessor` has a dependency with a scoped lifetimes (e.g., per message, per request, or per thread), a factory allowed the `Dispatcher` to create a new `CommandProcessor` for each scope. This allows us to support an `IAmABoxTransactionProvider` that manages a `DbContext` scoped per message.
     - This enabled the `Dispatcher` to obtain a fresh or context-specific instance of the `CommandProcessor` when needed, rather than being tied to a single instance for its lifetime.
   - Even where the `CommandProcessor` is created with a scoped or transient lifetime, with scope matching to its calling context, it still relies on the static `OutboxProducerMediator` for outbox and producer operations, ensuring that there is only one outbox.
     - The static fields are resettable via `ClearServiceBus` for testing purposes, but in production, they are set once and used for the lifetime of the application.
4. Transaction Provider
   - The `IAmABoxTransactionProvider`, which manages database transactions for the Outbox, is set as a Singleton in the `CommandProcessor`. This`IAmABoxTransactionProvider` to create an instance of the static OutboxProducerMediator by reflecting on it to determine the type of the transaction provider passed into the CommandProcessor.
   - The singleton `IAmAABoxTransactionProvider` was not otherwised used by the `CommandProcessor`, as it only extracts the transaction type from it to pass to the `OutboxProducerMediator`.
   - The original lifetime for the `IAmABoxTransactionProvider` was that of the `CommandProcessor`.
   - When calling `Post` or `PostAsync` we use `InMemoryOutbox` and we don't support Transactional Messaging. Instead we passed null. This created this problem:  https://github.com/BrighterCommand/Brighter/issues/3112. Our response led to a design approach of using static `IAmABoxTransactionProvider` for the application, set when the first `CommandProcessor` is created that `CommandProcessor` instances could use that `IAmABoxTransactionProvider` if one was not supplied. This provided a valid `IAmABoxTransactionProvider` for the `OutboxProducerMediator` to use, but it meant that the `IAmABoxTransactionProvider` was effectively a singleton for the application, even if the `CommandProcessor` was scoped or transient. 

Problems with this Design
- The`OutBoxProducerMediator` singleton pattern can lead to issues in testing, as tests may interfere with each other if they rely on the same static instance. 
- Because we only have a single `OutboxProducerMediator`, we cannot have different configurations of the Outbox for different `CommandProcessor `instances in the same application. 
- There is an issue with the static `IAmABoxTransactionProvider` when the concrete implementation of the`IAmABoxTransactionProvider` has state. This workef for scenarios like DynamoDb or Dapper, but not for EF Core. For EF Core the recommendation is to scope the `DbContext` per request, and use a new instance of the `CommandProcessor` per request, because the concrete `DbContext` has scope. This causes errors because our static `TransactionProvider` is set once, and holds a reference to the `DbContext` that was passed in when it was set. This means that if the `CommandProcessor` is created in a different scope, it will have a different `DbContext` instance to the static `IAmABoxTransactionProvider`. Whilst this is not an issue for stateless transaction providers, it is an issue for stateful ones like EntityFramework.
- Removing the `Func<IAmACommandProcessorProvider>` from the `Dispatcher` means that we cannot create a new `CommandProcessor` for each message, which could be an issue if the `CommandProcessor` has dependencies with scoped lifetimes. 

## Decision

Given we want to control the scope of V10, we need to remove errors caused by the static `IAmABoxTransactionProvider` holding a reference to a `DbContext` that is out of scope.
- The fix to the singleton `IAmABoxTransactionProvider` is to remove it. It is already passed into the `OutboxProducerMediator` via a method call, so we do not need the one set when the `CommandProcessor` is created. This means that the `IAmABoxTransactionProvider` can be scoped to the`IHandleRequests` derived class, which typically takes it in its constructor as a "unit of work" and users should configure Brighter to use the appropriate handler lifetime, which we will honor from our factory. 
- The `IAmABoxTransactionProvider` parameter that is passed into the `CommandProcessor` via its constructor is confusing as it implies we set the transaction provider for the `CommandProcessor` lifetime. As we only extract the transaction type we will change this to pass a `Type`, the transaction type, to the constructor. We can default it to CommitableTransaction (we need a default for `OutboxProducerMediator`). In our hostbuilder services, when constructing a command processor instance, we need to pull the type from the `IAmABoxTransactionProvider` given to us in configuration. Because an `IAmABoxTransactionProvider` may have a scoped dependency, we need to create a scope for it during construction of our seervices, in order to grab its type. (Ultimately it may make more sense to have a separate configuration option for the transaction type, but this is a bigger change and we want to minimize the impact of this change).
- We will expose a helper method to CommandProcessor to extract the transaction type from an `IAmABoxTransactionProvider` to simplify changing existing calls.
- When we call `CommandProcessor` `Post` or `PostAsync` we will pass null for the `IAmABoxTransactionProvider`. By using `Post` or `PostAsync` you are not using a transactional outbox, so the `IAmABoxTransactionProvider` is not needed.
- We will not fix the issue about the possibility of multiple `IAmAnOutbox` that `OutboxProducerMediator` cannot support. This is a limitation but it is likely that most applications will only need one configuration for the `IAmAnOutbox`. It may need to be addressed in a future version, if we see demand for it.
- The testing issues around `CommandProcessor` is mitigated by the `ClearServiceBus` method, which resets the static fields for testing purposes.

Because we remove the static `IAmABoxTransactionProvider`, we can also safely still remove the `Func<IAmACommandProcessorProvider>` from the `Dispatcher`. A singletone instance of `CommandProcessor' does not cause an issue as the dependency on the scoped transaction provider is only in the `IHandleRequests` type. This simplifies the design and makes it easier to understand. 
These changes allow us to revert to a model where the `CommandProcessor` can be a Singleton, because it no longer holds a reference to a scoped `IAmABoxTransactionProvider`. All of the other dependencies of `CommandProcessor` are either a factory or can have a singleton lifetime.
Because we have moved CommandProcessor to be a singleton we can also removed the `CommandProcessorLifetime` from `BrighterOptions`, as it is no longer needed, and in fact would cause confusion if it was set to something other than singleton.


## Consequences

- The `OutboxProducerMediator` will remain a static singleton, as we want to ensure that there is only one instance of the outbox/producer for the application.
- The `IAmABoxTransactionProvider`will be scoped to the `IHandleRequests` instance, allowing it to work with a `DbContext` that is also scoped per request.
- The `CommandProcessor` can be configured as a singleton, as it no longer holds a reference to a scoped `IAmABoxTransactionProvider`.
- Testing will still be possible, as the `ClearServiceBus` method will still be available to reset the static fields for testing purposes.