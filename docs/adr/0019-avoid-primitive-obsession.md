# 19. Avoid Primitive Obsession

Date: 2024-07-21

## Status

Accepted

## Context

CSharp is a strongly-typed OOP language and as such we want to rely on the type system to eliminate classes of errors caused by using a primitive (frequently a `string` or `int`) instead of a type. Overuse of primitives is a code smell called [Primitive Obsession](https://refactoring.guru/smells/primitive-obsession).

## Decision

We should use types throughout our code when representing a domain concept such as `ChannelName` or `RoutingKey` and only use primitives when the type has no domain meaning beyond being an instance of a primitive. This will help to ensure correctness by making it clear what the semantics behind a particular parameter, return value, or property is.

It removes classes of errors with parameter ordering or return types.

At some point we create an atomic type, that cannot itself be implemented without primitive types, as have to manage the state of that type and we may need primitives to store it's state, otherwise it would be user defined types "all the way down". So when do we replace a primitive with a user-defined type?

- Anything `internal` or `public` should prefer on their interface (methods or properties) to use user-defined types to represent domain concepts instead of primitives.
- Anything that represents a domain concept where type safety can be enforced, even if only used as a local variable.

By domain concept we don't mean concepts like a loop counter etc. but ideas within our domain. Some examples within our codebase include `RoutingKey`, `ChannelName` and `SubscriptionName`. These store their state internally as strings, but we can enforce type safety and thus remove classes of errors, which would not be possible if we used strings instead.

## Consequences

When we first started using user-defined types to replace primitives, record types did not exist. `record struct` based user-defined types may be a better choice for value objects - we compare for equality by state - as opposed to entities - we compare for equality by identity. This would also reduce the memory allocation costs of replacing primitives with user-defined types.
