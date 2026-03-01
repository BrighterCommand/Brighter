# 51. Expose UnacceptableMessageLimitWindow Through Configuration Pipeline

Date: 2026-02-23

## Status

Accepted

## Context

**Parent Requirement**: [specs/0021-Expose Unacceptable Message Window/requirements.md](../../specs/0021-Expose%20Unacceptable%20Message%20Window/requirements.md)

The `UnacceptableMessageLimitWindow` property was introduced as part of the Universal DLQ work to prevent the unacceptable message count from accumulating indefinitely over an application's lifetime. Without a window, a long-running consumer that encounters occasional poison messages would eventually reach the limit and halt, even if the errors were spread across hours or days with no burst pattern.

The property exists in two places:

1. **`Subscription`** — the user-facing configuration object where consumers are defined (information holder role)
2. **`MessagePump`** (base of `Reactor` and `Proactor`) — the runtime component that evaluates the count and decides whether to stop (controller role)

However, `ConsumerFactory` — the coordinator that creates message pumps from subscriptions — only transfers `UnacceptableMessageLimit` but not `UnacceptableMessageLimitWindow`. This means users can configure the window on their subscription, but it is silently ignored at runtime.

Additionally, all 11 transport-specific subscription types (e.g. `KafkaSubscription`, `SqsSubscription`, `RmqSubscription`) expose `unacceptableMessageLimit` in their constructors but not `unacceptableMessageLimitWindow`, preventing users of those types from setting the window at all.

## Decision

Wire `UnacceptableMessageLimitWindow` through the existing configuration pipeline, following the identical pattern already used by `UnacceptableMessageLimit`. No new roles, responsibilities, or abstractions are introduced.

### Responsibilities (unchanged)

| Role | Component | Responsibility |
|------|-----------|----------------|
| Information Holder | `Subscription` | Knows the configured window value |
| Coordinator | `ConsumerFactory` | Transfers configuration from `Subscription` to `MessagePump` |
| Controller | `MessagePump` | Decides whether to reset the unacceptable message count based on the window |

### Changes

**1. `ConsumerFactory` — wire the missing property**

Add `UnacceptableMessageLimitWindow = _subscription.UnacceptableMessageLimitWindow` to the object initializer in both `CreateReactor()` and `CreateProactor()`, immediately after the existing `UnacceptableMessageLimit` line.

**2. Transport-specific subscriptions — expose the parameter**

For each transport subscription type (both non-generic and generic `<T>` variants):

- Add `TimeSpan? unacceptableMessageLimitWindow = null` as a constructor parameter, positioned immediately after `unacceptableMessageLimit`
- Pass it through to the base `Subscription` constructor call

Affected types:
- `KafkaSubscription` / `KafkaSubscription<T>`
- `SqsSubscription` / `SqsSubscription<T>` (AWSSQS)
- `SqsSubscription` / `SqsSubscription<T>` (AWSSQS.V4)
- `RmqSubscription` / `RmqSubscription<T>` (Sync)
- `RmqSubscription` / `RmqSubscription<T>` (Async)
- `AzureServiceBusSubscription` / `AzureServiceBusSubscription<T>`
- `GcpPubSubSubscription` / `GcpPubSubSubscription<T>`
- `RedisSubscription` / `RedisSubscription<T>`
- `MsSqlSubscription` / `MsSqlSubscription<T>`
- `PostgresSubscription` / `PostgresSubscription<T>`
- `MqttSubscription` / `MqttSubscription<T>`
- `RocketMqSubscription` / `RocketMqSubscription<T>`
- `InMemorySubscription` / `InMemorySubscription<T>`

### Why no new abstractions

This change follows the design principle of "prefer simplicity" and "do not add new types without necessity." The property, the holding role (`Subscription`), and the consuming role (`MessagePump`) all exist already. The only missing piece is the single line of configuration transfer in `ConsumerFactory` and the parameter pass-through in transport subscriptions.

## Consequences

### Positive

- Users can configure `UnacceptableMessageLimitWindow` on any subscription type and have it take effect at runtime
- Follows the established pattern — no new concepts for users to learn
- No breaking changes — the parameter defaults to `null`, preserving existing behavior

### Negative

- Minor increase in constructor parameter count for transport subscriptions (already long parameter lists)

### Risks and Mitigations

- **Risk**: Missing a transport subscription type. **Mitigation**: The requirements enumerate all 11 types explicitly; tasks will cover each one.

## Alternatives Considered

**1. Use a configuration object instead of constructor parameters**

Could introduce a `MessagePumpOptions` or `UnacceptableMessageOptions` class to group related settings. Rejected because this would be a larger refactoring that changes the established pattern for all pump configuration properties, not just the window. That refactoring may be valuable in future but is out of scope here.

**2. Have `MessagePump` read directly from `Subscription`**

Could pass the `Subscription` to the pump and let it read configuration directly. Rejected because `MessagePump` currently has no dependency on `Subscription` — it receives individual property values via its initializer. Adding this coupling would change the pump's role from controller to information holder + controller.

## References

- Requirements: [specs/0021-Expose Unacceptable Message Window/requirements.md](../../specs/0021-Expose%20Unacceptable%20Message%20Window/requirements.md)
- Existing pattern: `ConsumerFactory.cs` lines 109, 131 (how `UnacceptableMessageLimit` is wired)
- Window logic: `MessagePump.IncrementUnacceptableMessageCount()` (already implemented)
- Window tests: `When_an_unacceptable_message_limit_is_reset.cs` (already passing)
