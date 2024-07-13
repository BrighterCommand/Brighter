# 12. Provide In-Memory and Simple Implementations 

Date: 2019-08-01

## Status

Accepted

## Context

Brighter provides interfaces to:

* Transports such as `IAmAProducer` and `IAmAMessageConsumer` 
* Outboxes such as `IAmAnOutbox` or Inboxes such as `IAmAnInbox`
* Factories such as `IAmAHandlerFactory` 

All of these abstractions allow us or end users to extend Brighter.

When writing tests it is useful to have implementations of these that provide a simple, or in-memory, version of the functionality. A typical option is to provide a Test Double, such as a Fake, Stub or Mock version within our test suite, with simple functionality tailored to that test, for example a FakeProducer; we also might provide a Simple version of a factory such that a test does not need to use a full IoC container to run. 

But we have a number of problems from this strategy:

* These types are not exposed to end users, who might wish to use them in their own tests
* We do not have fully functioning in memory versions of these, with their own tests, so they may alter the behavior of types for whom they are a dependency when under test.

## Decision

We should provide in-memory or simple versions of libraries that intend to use to extend Brighter. These should have tests in `paramore.brighter.inmemorytests` that prove they provide standard functionality. They should be usable by end users for writing their own tests.

An incomplete list of these include:

* `SimpleHandlerFactoryAsync`
* `SimpleHandlerFactorySync`
* `SimpleMessageMapperFactory`
* `SimpleMessagemapperFactoryAsync`
* `SimpleMessageTransformerFactory`
* `SimpleMessageTransformerFactoryAsync`
* `InMemoryOutbox`
* `InMemoryInbox`
* `InMemoryProducer`
* `InMemoryConsumer`
* `InMemoryArchiveProvider`

As we review tests we should look for where we might want to promote useful test doubles into lightweight implementations that can be used for testing.

Similar examples in dotnet include `FakeTimeProvider` which helps with Time related tests. I would tend to avoid the `Fake` name over In-Memory or Simple as this is our branding for those helpers.

## Consequences

In some cases it may be appropriate to use the in-memory or simple versions as an internal default when no override is provided, as this enables parts of Brighter to rely on their existence, even if the user does not provide them.

In tests always prefer the usage of these in-memory or simple classes over new Test Doubles. Only use a Test Double where you want to simulate error conditions, or would have to adjust the normal behavior of the in-memory or simple version to test a specific case. The purpose of the in-memory or simple versions is to provide a standard, simple, implementation so it would subvert that to force it to behave differently just to accomodate tests.

This means we have removed some common test doubles such as FakeProducer and FakeOutbox for their in-memory or simple versions.

We occassionally have large PRs because a change has to flow through all the transports or outbox/inboxes. With this strategy we can use the in-memory or simple versions in the core tests to 'prove' the approach, with only signature changes needed for others (which may be fake). We can then merge that and proceed to the concrete implementations, one-by-one.
