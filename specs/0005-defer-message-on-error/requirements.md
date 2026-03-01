# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Related Specs**: [0002-universal_scheduler_delay](../0002-universal_scheduler_delay/), [0004-transport-scheduler-wiring](../0004-transport-scheduler-wiring/)

## Problem Statement

As a developer using Brighter's message pump, I would like a declarative way to automatically defer messages with a configurable delay when an unhandled exception escapes my handler pipeline, so that transient failures are retried later without requiring me to explicitly catch exceptions and throw `DeferMessageAction` in every handler.

Currently, to defer a message on error a developer must:
- Wrap handler logic in try/catch blocks
- Explicitly throw `DeferMessageAction` when a transient error occurs
- Repeat this boilerplate in every handler that needs deferred retry behavior

This is the "defer" counterpart to the existing `[RejectMessageOnError]` middleware (spec 0002), which catches unhandled exceptions and sends messages to the Dead Letter Queue. Where Reject gives up on the message, Defer retries it later with a delay.

## Proposed Solution

A declarative middleware attribute — `[DeferMessageOnError]` — that acts as a catch-all exception backstop in the handler pipeline. When any unhandled exception escapes the inner pipeline, the handler catches it and throws `DeferMessageAction`, causing the message pump to requeue the message with a delay.

The attribute accepts a configurable delay (in milliseconds) that is passed to the handler via `InitializerParams()` / `InitializeFromAttributeParams()`. The handler then sets the delay on the `DeferMessageAction` (or relies on the message pump's existing `RequeueDelay` mechanism).

### Usage Example

```csharp
// Defer: retry later on any unhandled error (outermost handler)
[DeferMessageOnError(step: 0, delayMilliseconds: 5000)]
[UsePolicy("RetryPolicy", step: 2)]
public override MyMessage Handle(MyMessage message)
{
    // If this fails after retries, message is requeued with 5s delay
    return base.Handle(message);
}
```

### Reject vs Defer — Choose One

Users should use **either** `[RejectMessageOnError]` **or** `[DeferMessageOnError]` as their outermost backstop, **not both**:

- **Reject**: "Give up — send to DLQ." Use when the error is likely permanent and the message should not be retried.
- **Defer**: "Try again later." Use when the error is likely transient and the message should be retried after a delay.

Using both on the same handler is not supported and should be documented as a usage error.

## Requirements

### Functional Requirements

1. Provide a `[DeferMessageOnError]` attribute for sync handlers and a `[DeferMessageOnErrorAsync]` attribute for async handlers
2. The attribute accepts a `step` parameter (pipeline ordering) and a `delayMilliseconds` parameter (configurable delay)
3. When an unhandled exception is caught:
   - Log the exception with full diagnostic information
   - Throw `DeferMessageAction` so the message pump requeues the message with delay
   - Preserve the original exception as `InnerException` on the `DeferMessageAction`
4. When no exception occurs, pass through transparently with no overhead
5. The delay parameter is passed from the attribute to the handler via the existing `InitializerParams()` / `InitializeFromAttributeParams()` pattern
6. Should be used as the outermost handler in the pipeline (lowest step number)

### Non-functional Requirements

- Minimal performance overhead in the happy path (no allocation, just try/catch)
- Follow existing middleware patterns (mirrors the Reject handler structure from spec 0002)
- Clear documentation that Reject and Defer are mutually exclusive backstops

### Constraints and Assumptions

- Only provides value when running within a message pump (Reactor/Proactor)
- The actual delay behavior depends on the transport and scheduler configuration (specs 0002 and 0004)
- `DeferMessageAction` already exists and is handled by both Reactor and Proactor message pumps
- The message pump's requeue mechanism (with `RequeueCount` limits) still applies — after exhausting retries, the message will be rejected to the DLQ

### Out of Scope

- Exception type filtering (this is a catch-all backstop)
- Changing the existing `DeferMessageAction` class
- Modifying the message pump's requeue/delay behavior (that's specs 0002/0004)
- Runtime validation that Reject and Defer aren't both applied (documentation only)

## Acceptance Criteria

1. Applying `[DeferMessageOnError(step: 0, delayMilliseconds: 5000)]` to a handler causes unhandled exceptions to result in `DeferMessageAction` being thrown
2. The original exception message and exception are preserved as `InnerException` on the `DeferMessageAction`
3. The exception is logged before being converted to a defer action
4. Works with both sync (`[DeferMessageOnError]`) and async (`[DeferMessageOnErrorAsync]`) handler patterns
5. When no exception occurs, the handler passes through without interference
6. The delay value from the attribute is available to the handler via `InitializeFromAttributeParams`
7. When used in a message pump, failed messages are requeued with the configured delay

## Additional Context

- The Reject handler (spec 0002) provides the structural template: same folder layout (`src/Paramore.Brighter/Defer/Attributes/` and `src/Paramore.Brighter/Defer/Handlers/`), same attribute/handler separation, same `HandlerTiming.Before` approach
- The key difference is that Reject throws `RejectMessageAction` (DLQ) while Defer throws `DeferMessageAction` (requeue with delay)
- The `InitializerParams()` pattern is well-established in the codebase (e.g., `UseInboxAttribute` passes `onceOnly`, `contextKey`, `onceOnlyAction`)
- Pipeline documentation: https://brightercommand.gitbook.io/paramore-brighter-documentation/brighter-request-handlers-and-middleware-pipelines/buildinganasyncpipeline
