# 40. Add the Specification Pattern

Date: 2024-11-09

## Status

Proposed

## Context

The Specification Pattern is a software design pattern that is used to define business rules that can be combined to create complex rules. It is used to encapsulate business rules that can be used to determine if an object meets a certain criteria. The pattern was described by Eric Evans and Martin Fowler in [this article](https://martinfowler.com/apsupp/spec.pdf).

Brighter needs the addition of the specification pattern, for two reasons:

1. For use with its Mediator. The Mediator allows Brighter to execute a workflow that has a branching condition. The Specification Pattern can be used to define the branching conditions. See [ADR-0022](0022-use-the-mediator-pattern.md).
2. For use when implementing the [Agreement Dispatcher](https://martinfowler.com/eaaDev/AgreementDispatcher.html) pattern from Martin Fowler. The Agreement Dispatcher pattern is used to dispatch a message to a handler based on a set of criteria. The Specification Pattern can be used to define the criteria.

## Decision
Add the Specification Pattern to Brighter. We could have taken a dependency on an off-the-shelf implementation. Many of the Brighter team worked at Huddle Engineering, and worked on [this](https://github.com/HuddleEng/Specification) implementation of the Specification Pattern. However, this forces Brighter to take a dependency on another project, and we would like to keep Brighter as self-contained as possible. So, whilst we may be inspired by Huddle's implementation, we will write our own. 

In this version, we don't need some of the complexity of Huddle's usage of the Visitor pattern, as we only need to control branching. In addition, Huddle's version was written before the wide usage of lambda expressions via delegates in C#, so we can simplify the implementation.

## Consequences

Brighter will provide an implementation of the Specification pattern.