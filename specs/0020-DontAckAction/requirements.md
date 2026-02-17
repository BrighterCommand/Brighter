# Requirements: DontAckAction

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

## Problem Statement

As a **Brighter consumer**, I would like the ability to **not acknowledge a message when an error occurs**, so that the message remains on the channel and is presented again on the next pump iteration.

Brighter's default behavior is to acknowledge a message when an error leaves the handler pipeline. This is based on the principle that errors are either:

- **Transient** — retried a configurable number of times via `UsePolicyAttribute` or `UseResilienceAttribute`
- **Non-transient** — the message is acknowledged (consumed) and the operator investigates from logs

Brighter already supports actions that change this flow (e.g., `DeferMessageAction` requeues with delay, `RejectMessageAction` moves to DLQ). However, there is no mechanism to **block on a message** — refusing to acknowledge it so it is presented again on the next loop iteration.

### Scenarios

1. **Feature Switch Off**: A `FeatureSwitchAttribute` turns off a handler. The current behavior short-circuits the pipeline and acknowledges the message, effectively consuming it. Instead, the user wants the message to remain unconsumed so it can be processed once the feature is re-enabled.

2. **Stream Processing / Blocking**: In stream processing scenarios, the user wants to block indefinitely on a message that cannot currently be processed, retrying forever rather than consuming it.

3. **Queue Nack / Immediate Redelivery**: On queue-based transports (RabbitMQ, SQS, Azure Service Bus), simply not acknowledging a message leaves it invisible until the transport's visibility timeout expires (which could be 30 seconds to 30 minutes). The user wants an explicit nack that immediately unlocks the message so another consumer can pick it up, rather than waiting for the timeout.

## Proposed Solution

Add a new `DontAckAction` exception that signals the message pump (both Reactor and Proactor) to **not acknowledge** the current message, increment the unacceptable message count, log any inner exception, and continue the pump loop. The message stays on the channel and will be presented again on the next iteration.

A configurable delay on the pump controls how long to wait before re-presenting the message, preventing tight-loop CPU burn.

Additionally, provide two convenience mechanisms for common use cases:

1. A `DontAck` option on `FeatureSwitchAttribute` — when a feature is switched off, throw `DontAckAction` instead of silently consuming the message.
2. A new `DontAckOnErrorAttribute` — catches unhandled exceptions bubbling out of the handler pipeline and wraps them in a `DontAckAction`, preserving the original error as an inner exception.

## Requirements

### Functional Requirements

1. **FR-1: DontAckAction exception** — A new exception type `DontAckAction` (following the pattern of `DeferMessageAction`, `RejectMessageAction`, `InvalidMessageAction`) that signals the pump to not acknowledge the current message.

2. **FR-2: Reactor handling** — When `Reactor` catches a `DontAckAction`:
   - Do NOT acknowledge the message
   - Increment the unacceptable message count
   - Log any inner exception from the `DontAckAction` at an appropriate level
   - Apply a configurable delay before continuing the pump loop
   - Continue the loop (message will be re-presented from the channel)

3. **FR-3: Proactor handling** — When `Proactor` catches a `DontAckAction`:
   - Same behavior as FR-2, but async

4. **FR-4: Configurable delay** — A delay property on the message pump configuration that controls how long to wait before the next loop iteration after a `DontAckAction`. This prevents tight-loop CPU burn. Should have a sensible default.

5. **FR-5: Unacceptable message count integration** — `DontAckAction` increments the unacceptable message count to allow a strategy that eventually exits if a blocking retry fails enough times. Setting the unacceptable message limit to `-1` (or `0` per existing behavior) disables limit-based termination, supporting infinite blocking.

6. **FR-6: FeatureSwitchAttribute DontAck option** — Add a `DontAck` boolean property to `FeatureSwitchAttribute`. When `true` and the feature is switched off, the `FeatureSwitchHandler` throws a `DontAckAction` instead of silently returning the request. This causes the pump to block on the message until the feature is re-enabled.

7. **FR-7: DontAckOnErrorAttribute** — A new pipeline attribute/handler that catches exceptions bubbling out of the handler pipeline and throws a `DontAckAction` with the original exception as the inner exception. This allows any handler error to trigger don't-ack behavior.

8. **FR-8: AggregateException support** — When a `DontAckAction` is contained within an `AggregateException`, the pump should detect and handle it (following the existing pattern for `DeferMessageAction` and other actions).

9. **FR-9: TranslateMessage unwrapping** — When a `DontAckAction` is thrown during message translation (wrapped in `TargetInvocationException`), it should be unwrapped and rethrown (following the existing pattern).

10. **FR-10: Channel Nack operation** — Add a `Nack(Message)` method to `IAmAChannelSync` and `NackAsync(Message, CancellationToken)` to `IAmAChannelAsync`. The Channel delegates to the consumer, following the same pattern as Acknowledge, Reject, and Requeue.

11. **FR-11: Consumer Nack operation** — Add a `Nack(Message)` method to `IAmAMessageConsumerSync` and `NackAsync(Message, CancellationToken)` to `IAmAMessageConsumerAsync`. The semantics are:
    - **Queue transports**: Unlock/release the message so it is immediately available to other consumers. This is distinct from Reject (which routes to DLQ) and Requeue (which re-enqueues with a delay).
    - **Stream transports**: No-op. The existing behavior of not committing the offset is sufficient.
    - **InMemoryMessageConsumer**: Behaves as a queue — remove from locked messages and re-enqueue to the bus.

12. **FR-12: Pump calls Nack on DontAckAction** — When the Reactor or Proactor catches a `DontAckAction`, it should call `Channel.Nack(message)` (or `NackAsync`) before continuing the loop. This replaces the current behavior of simply skipping the Acknowledge call. The delay, logging, and unacceptable message count behavior remain unchanged.

13. **FR-13: Transport Nack implementations** — Each transport consumer must implement the Nack method:
    - **RabbitMQ**: `BasicNack(deliveryTag, multiple: false, requeue: true)` — immediately requeues the message
    - **AWS SQS**: `ChangeMessageVisibility(receiptHandle, timeout: 0)` — makes the message immediately visible
    - **Azure Service Bus**: `AbandonMessageAsync(lockToken)` — releases the message lock
    - **Kafka**: No-op — offset is simply not committed
    - **Redis**: No-op — LPOP is destructive, message cannot be un-popped
    - **MQTT**: No-op — no acknowledgment concept
    - **GCP Pub/Sub**: No-op — don't acknowledge

### Non-functional Requirements

- **NFR-1: No tight loops** — The delay mechanism must prevent CPU-burning tight loops when a message is repeatedly not acknowledged.
- **NFR-2: Observability** — All DontAckAction occurrences must be logged with sufficient context (message ID, channel, inner exception) for operator investigation.
- **NFR-3: Consistency** — Follow existing patterns established by `DeferMessageAction`, `RejectMessageAction`, and `InvalidMessageAction` for exception structure, catch ordering, and pump integration.

### Constraints and Assumptions

- For queue transports, Nack provides explicit unlock/requeue so the message is immediately available to other consumers. For stream transports, Nack is a no-op because not committing the offset is sufficient.
- The pump delay (`DontAckDelay`) is still applied after calling Nack, preventing tight-loop CPU burn on the current consumer. However, the message is available to *other* consumers immediately.
- The unacceptable message count mechanism already supports disabling limits (limit <= 0 returns false for limit reached), so infinite blocking is already supported without code changes to the limit check.
- Nack is distinct from Requeue: Requeue acknowledges the original and creates a new delivery (potentially with delay). Nack releases the lock on the original message without acknowledging it.

### Out of Scope

- Changes to the requeue mechanism (`DeferMessageAction` continues to handle requeue-with-delay)
- Changes to DLQ routing (handled by `RejectMessageAction`)
- Backpressure or rate-limiting mechanisms beyond the simple configurable delay
- Transport-specific nack configuration (e.g., configurable redelivery counts) — each transport uses its native nack with default behavior

## Acceptance Criteria

1. **AC-1**: Throwing `DontAckAction` from a handler causes the message to NOT be acknowledged and the pump continues its loop.
2. **AC-2**: After a `DontAckAction`, the unacceptable message count is incremented.
3. **AC-3**: If `DontAckAction` has an inner exception, that exception is logged.
4. **AC-4**: The configurable delay is applied after a `DontAckAction` before the next loop iteration.
5. **AC-5**: A `FeatureSwitchAttribute` with `DontAck = true` throws `DontAckAction` when the feature is off.
6. **AC-6**: A `DontAckOnErrorAttribute` in the pipeline catches handler exceptions and throws `DontAckAction` with the original as inner.
7. **AC-7**: Both Reactor and Proactor handle `DontAckAction` identically (sync vs async).
8. **AC-8**: The pump eventually terminates if unacceptable message limit is reached (unless limit is disabled).
9. **AC-9**: `DontAckAction` within `AggregateException` is handled correctly.
10. **AC-10**: `IAmAChannelSync` and `IAmAChannelAsync` expose a Nack/NackAsync method.
11. **AC-11**: `IAmAMessageConsumerSync` and `IAmAMessageConsumerAsync` expose a Nack/NackAsync method.
12. **AC-12**: The Reactor and Proactor call `Channel.Nack(message)` / `Channel.NackAsync(message)` in the `DontAckAction` catch block.
13. **AC-13**: On RabbitMQ, Nack calls `BasicNack` with `requeue: true`, making the message immediately available.
14. **AC-14**: On SQS, Nack sets visibility timeout to 0, making the message immediately visible.
15. **AC-15**: On Azure Service Bus, Nack calls `AbandonMessageAsync`, releasing the message lock.
16. **AC-16**: On stream transports (Kafka, Redis, MQTT, GCP Pub/Sub), Nack is a no-op.
17. **AC-17**: On InMemoryMessageConsumer, Nack removes from locked messages and re-enqueues to the bus.

## Additional Context

### Existing Action Types for Reference

| Action | Behavior | Ack? |
|--------|----------|------|
| `DeferMessageAction` | Requeue with delay | Yes (requeued) |
| `RejectMessageAction` | Move to DLQ | Yes (rejected) |
| `InvalidMessageAction` | Move to DLQ/invalid channel | Yes (rejected) |
| **`DontAckAction` (new)** | **Nack + stay on channel, loop** | **No (Nack)** |

### Current Acknowledgement Flow

Currently, `AcknowledgeMessage` is called unconditionally after all exception handling in both Reactor and Proactor. The `DontAckAction` handling must conditionally skip this acknowledgement call.
