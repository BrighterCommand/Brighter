# 20. Refactor External Service Bus 

Date: 2019-08-01

## Status

Proposed

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
- MessageDispatcher - responsible for dispatching messages via a producer
- Outbox - responsible for guaranteeing delivery of messages

For now, we will keep ExternalServiceBus as a mediator between these two classes; it may be possible to remove this class in the future. We will create seperate Async, Bulk and Sync versions of these classes. We may be able to factor out a base class from these Sync and Async classes to hold common functionality.

## Consequences

- We will have a more maintainable and easier to work with `ExternalServiceBus`
