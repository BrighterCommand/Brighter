# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

## Problem Statement

As a Brighter user configuring message consumers, I would like to set the `UnacceptableMessageLimitWindow` on my `Subscription` and have it flow through to the message pump, so that I can control the time window over which unacceptable message counts are evaluated without the count accumulating indefinitely over the application's lifetime.

Currently, `UnacceptableMessageLimitWindow` exists on both `Subscription` (added in the Universal DLQ work) and `MessagePump`, but it is **never wired** from `Subscription` to `MessagePump` in `ConsumerFactory`. Additionally, transport-specific subscriptions (e.g. `KafkaSubscription`, `SqsSubscription`, `RmqSubscription`, etc.) do not expose the `unacceptableMessageLimitWindow` constructor parameter, so users of those types cannot set it at all.

## Proposed Solution

Wire `UnacceptableMessageLimitWindow` through the full configuration pipeline, following the same pattern already established by `UnacceptableMessageLimit`:

1. **ConsumerFactory** — pass `Subscription.UnacceptableMessageLimitWindow` to the message pump in both `CreateReactor()` and `CreateProactor()`
2. **Transport-specific subscriptions** — add `unacceptableMessageLimitWindow` as a constructor parameter on all transport subscriptions and pass it through to the base `Subscription` constructor

## Requirements

### Functional Requirements
- `ConsumerFactory.CreateReactor()` must set `UnacceptableMessageLimitWindow` on the `Reactor` from the `Subscription`
- `ConsumerFactory.CreateProactor()` must set `UnacceptableMessageLimitWindow` on the `Proactor` from the `Subscription`
- All transport-specific subscription types must accept an optional `unacceptableMessageLimitWindow` parameter and pass it to the base constructor:
  - `KafkaSubscription` / `KafkaSubscription<T>`
  - `SqsSubscription` / `SqsSubscription<T>` (both AWSSQS and AWSSQS.V4)
  - `RmqSubscription` / `RmqSubscription<T>` (both Sync and Async)
  - `AzureServiceBusSubscription` / `AzureServiceBusSubscription<T>`
  - `GcpPubSubSubscription` / `GcpPubSubSubscription<T>`
  - `RedisSubscription` / `RedisSubscription<T>`
  - `MsSqlSubscription` / `MsSqlSubscription<T>`
  - `PostgresSubscription` / `PostgresSubscription<T>`
  - `MqttSubscription` / `MqttSubscription<T>`
  - `RocketMqSubscription` / `RocketMqSubscription<T>`
  - `InMemorySubscription` / `InMemorySubscription<T>`

### Non-functional Requirements
- No breaking changes — the new parameter must default to `null` (matching base `Subscription` default)
- No performance impact — this is purely configuration plumbing

### Constraints and Assumptions
- The `UnacceptableMessageLimitWindow` property already exists on `Subscription` and `MessagePump` — no new types or properties are needed
- The parameter should follow the same position/ordering convention as `unacceptableMessageLimit` in each constructor

### Out of Scope
- Changing the semantics of how `UnacceptableMessageLimitWindow` is evaluated in `MessagePump` (already implemented)
- Adding new tests for the window reset behavior (already tested in `When_an_unacceptable_message_limit_is_reset.cs`)
- Configuration via appsettings.json or other config sources (separate concern)

## Acceptance Criteria

- When a user creates a `Subscription` (or any transport-specific subscription) with `unacceptableMessageLimitWindow: TimeSpan.FromMinutes(5)`, and the consumer is started, the resulting message pump's `UnacceptableMessageLimitWindow` is `TimeSpan.FromMinutes(5)`
- When `unacceptableMessageLimitWindow` is not specified (defaults to `null`), behavior is unchanged from today
- All existing tests continue to pass
- The `ConsumerFactory` passes the window value for both Reactor and Proactor paths

## Additional Context

- `UnacceptableMessageLimit` was wired correctly through `ConsumerFactory` but `UnacceptableMessageLimitWindow` was missed during the Universal DLQ implementation
- Existing tests (`When_an_unacceptable_message_limit_is_reset.cs`) set the window directly on the pump, confirming the pump-side logic works correctly
- This is a straightforward plumbing fix that follows an established pattern
