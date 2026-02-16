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

### Non-functional Requirements

- **NFR-1: No tight loops** — The delay mechanism must prevent CPU-burning tight loops when a message is repeatedly not acknowledged.
- **NFR-2: Observability** — All DontAckAction occurrences must be logged with sufficient context (message ID, channel, inner exception) for operator investigation.
- **NFR-3: Consistency** — Follow existing patterns established by `DeferMessageAction`, `RejectMessageAction`, and `InvalidMessageAction` for exception structure, catch ordering, and pump integration.

### Constraints and Assumptions

- The `DontAckAction` relies on the underlying transport's behavior when a message is not acknowledged. For most transports (RabbitMQ, SQS, Kafka), an unacknowledged message will be re-delivered after a visibility timeout or on the next poll.
- The delay in the pump is in addition to any transport-level redelivery delay.
- The unacceptable message count mechanism already supports disabling limits (limit <= 0 returns false for limit reached), so infinite blocking is already supported without code changes to the limit check.

### Out of Scope

- Transport-specific nack/reject semantics (each transport already has its own acknowledgment behavior)
- Changes to the requeue mechanism (`DeferMessageAction` continues to handle requeue-with-delay)
- Changes to DLQ routing (handled by `RejectMessageAction`)
- Backpressure or rate-limiting mechanisms beyond the simple configurable delay

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

## Additional Context

### Existing Action Types for Reference

| Action | Behavior | Ack? |
|--------|----------|------|
| `DeferMessageAction` | Requeue with delay | Yes (requeued) |
| `RejectMessageAction` | Move to DLQ | Yes (rejected) |
| `InvalidMessageAction` | Move to DLQ/invalid channel | Yes (rejected) |
| **`DontAckAction` (new)** | **Stay on channel, loop** | **No** |

### Current Acknowledgement Flow

Currently, `AcknowledgeMessage` is called unconditionally after all exception handling in both Reactor and Proactor. The `DontAckAction` handling must conditionally skip this acknowledgement call.
