# 20. Reduce External Service Bus Complexity

Date: 2024-08-01

## Status

Retired

## Context

We have turned on CodeScene reporting for Brighter; it has confirmed that the External Service Bus is a hotspot.
We need to refactor the External Service Bus to make it more maintainable and easier to work with.

The specific areas that CodeScene has identified are:

- The `ExternalServiceBus` class  suffers from [Bumpy Road](https://codescene.com/engineering-blog/bumpy-road-code-complexity-in-context/). A Bumpy Road is a function that: "contains multiple chunks of nested conditional logic inside the same function. The deeper the nesting and the more bumps, the lower the code health".  The Key problems are:
    - `Dispatch` (Bumps = 3)
    - `DispatchAsync` (Bumps = 3)
    - `BulkDispatchAsync` (Bumps = 2)
- The fix for a Bumpy Road is usually related to better encapsulation and separation of concerns.
- Related to this, CodeScene identifies that we suffer from Deep, Nested Complexity. Deep Nested Complexity occurs because we: "have control structures like if-statements or loops inside other control structures. Deep nested logic increases the cognitive load on the programmer reading the code."
    - `Dispatch` (Nesting depth = 4 conditionals))
    - `DispatchAsync` Nesting depth = 4 conditionals)
    - `BulkDispatchAsync` (Nesting depth = 4 conditionals)
- There is also significant duplication across sync and async implementations
    - `ClearOutbox`
    - `ClearOutboxAsync`
    - `EndBatchAddToOutbox`
    - `EndBatchAddToOutboxAsync`
    - `ConfigureAsyncPublisherCallbackMaybe`
    - `ConfigurePublisherCallbackMaybe`
    - `MapMessage`
    - `MapMessageAsync`
- `ExternalServiceBus` has 4 functions that exceed the maximum number of arguments.
    - AddToOutboxAsync(Arguments = 6)
        - ClearOutboxAsync(Arguments = 5)
        - ClearOutstandingFromOutbox(Arguments = 5)
        - BackgroundDispatchUsingAsync(Arguments = 5)
- Constructor Over-Injection there are 16 arguments to `ExternalServiceBus`

Part of the problem here is that `ExternalServiceBus` has two responsibilities: the producer and the outbox. The producer and outbox are coupled because: we write to the outbox when we dispatch a message; we send from the outbox when a message has not been dispatched within a set timeframe. Originally these responsibilities were all part of the `CommandProcessor`. Refactoring them out of the `CommandProcessor` improved the design of `CommandProcessor` (though it is still a hotspot) but has left us with an `ExternalServiceBus` that has too many responsibilities.

## Decision

We need to refactor two classes out of `ExternalServiceBus`:
- MessagePoster - responsible for dispatching messages via a producer; in essence this wraps the producer for the External Service Bus.
- Outbox - responsible for guaranteeing delivery of messages; in essence this wraps the outbox for the External Service Bus.

We will create seperate Async, Bulk and Sync versions of these classes. We may be able to factor out a base class from these Sync and Async classes to hold common functionality.

For now, we will keep ExternalServiceBus as a Mediator between these classes; it also provides scope management for the bus as a singleton. It may be possible to remove this class in the future, but this ADR believes it will be out-of-scope for this change.

We will seek to lower the code complexity concerns identified by CodeScene by as a result of this change.

## Consequences

In practice this did not work. Why?

- The `ExternalServiceBus` is a [Mediator](https://en.wikipedia.org/wiki/Mediator_pattern) that manages the complex interaction between a producer and the outbox
- Attempting to move functionality from the Mediator back out to the Outbox or MessagePoster classes resulted in a cyclic dependency between the classes as each class needed to call the other. The only way around that would be to lift the functionality to a higher level in a Mediator between the two -  which would be the reason for the `ExternalServiceBus` class in the first place.
- This situation is exacerbated by the role of `ExternalServiceBus` in providing a semaphore to control the number of checks of the outbox that can be made at any one time. This is a cross-cutting concern that is difficult to factor out. 
- Because `ExternalServiceBus` is a singleton the creation of the Outbox and MessagePoster need to happen within `ExternalServiceBus` which itself limits the opportunities for these flowing back to `CommandProcessor`.
- `ExternalServiceBus` was first extracted from `CommandProcessor` to reduce the complexity of `CommandProcessor`. There is a danger that naive attempts to simplify `ExternalServiceBus` will result in the complexity returning to `CommandProcessor`. 
- This is the second attempt at this style of refactoring - pushing functionality back out of the mediator into co-operating classes. This attempt was more organized and structured than the first attempt, but it still failed. The learning is probably that we need a Mediator here, and future attempts to reduce the structural complexity should instead focus on the complexity within the Mediator itself.


