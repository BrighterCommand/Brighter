# 15. Push Button API for all DSLs 

Date: 2024-07-16

## Status

Accepted

## Context

It has become a common paradigm for setup code to use a [Fluent Interface](https://martinfowler.com/bliki/FluentInterface.html). A Fluent Interface uses [Method Chaining](https://martinfowler.com/dslCatalog/methodChaining.html) to help with the discovery of an API when used with Intellisense because the IDE can prompt with the available operations for that current context. As a consequence it only supports a fixed set of flows. This frequently does not matter in setup, where what matters it that all the steps are completed.

A Fluent API is in contrast to a push-button API or command-query API [Fowler] where the methods on an object are likened to buttons that can be pushed to invoke a particular behavior. 

>In the early days of objects one of the biggest influences on me, and many others, was Bertrand Meyers book "Object-Oriented Software Construction". One of the analogies he used to talk about objects was to treat them as machines. In this view an object was a black box with its interface as a series of buttons that you could press -effectively offerring a menu of different things you can do with the object. 
><div align="right">Fowler, Martin. Domain Specific Languages</div>

A push-button API has more control, buttons may be pushed independently, but is less discoverable and the user may have to rely on documentation if particular sequences of method calls are required to obtain certain behaviors.

Fowler recommends that a Fluent Interface should be built as an [Expression Builder](https://martinfowler.com/dslCatalog/expressionBuilder.html) over the [Semantic Model](https://martinfowler.com/dslCatalog/semanticModel.html) of the underlying command-query API. This allows the constraints of the fluent API to be ameliorated, as you can always break out to the push button API. It also means that push button API can be used to test the functionality of the API, which allows more incremental development of behaviors than the Fluent API.

## Decision

We will use a command-query API under any Fluent API as described by Fowler's [Expression Builder](https://martinfowler.com/bliki/ExpressionBuilder.html) pattern. There should be tests to confirm that the Expression Builder works, but there should also be tests for the underlying command-query API.

## Consequences

All Fluent APIs are backed by an underlying command-query API which they compose.
