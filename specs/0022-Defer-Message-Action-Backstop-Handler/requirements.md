# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Related Specs**: [0002-backstop-error-handler](../0002-backstop-error-handler/), [0005-defer-message-on-error](../0005-defer-message-on-error/), [0020-DontAckAction](../0020-DontAckAction/)

## Problem Statement

As a developer using Brighter's message pump, I would like a declarative attribute — `[DeferMessageOnError]` — that catches unhandled exceptions in my handler pipeline and converts them to a `DeferMessageAction`, so that the message pump requeues the message with a configurable delay without me having to write try/catch boilerplate in every handler.

Currently, to defer a message on error a developer must:
- Wrap handler logic in try/catch blocks
- Explicitly `throw new DeferMessageAction()` when a transient error occurs
- Repeat this boilerplate in every handler that needs deferred retry behavior

Brighter already provides declarative backstop attributes for the other two error handling actions:
- `[RejectMessageOnError]` → catches exceptions → throws `RejectMessageAction` → message sent to DLQ
- `[DontAckOnError]` → catches exceptions → throws `DontAckAction` → message left unacknowledged

The `DeferMessageAction` path is the only one missing a declarative attribute. This spec completes the set.

## Proposed Solution

Create four new types following the established pattern from `RejectMessageOnError` and `DontAckOnError`:

1. **`DeferMessageOnErrorAttribute`** — sync attribute, returns `DeferMessageOnErrorHandler<>`
2. **`DeferMessageOnErrorAsyncAttribute`** — async attribute, returns `DeferMessageOnErrorHandlerAsync<>`
3. **`DeferMessageOnErrorHandler<TRequest>`** — sync handler, catches exceptions and throws `DeferMessageAction`
4. **`DeferMessageOnErrorHandlerAsync<TRequest>`** — async handler, catches exceptions and throws `DeferMessageAction`

The attribute accepts a `delayMilliseconds` parameter in addition to the standard `step` parameter. The delay is passed from the attribute to the handler via the existing `InitializerParams()` / `InitializeFromAttributeParams()` mechanism, and then set on the `DeferMessageAction` when thrown.

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

```csharp
// Async equivalent
[DeferMessageOnErrorAsync(step: 0, delayMilliseconds: 5000)]
[UsePolicy("RetryPolicy", step: 2)]
public override async Task<MyMessage> HandleAsync(MyMessage message, CancellationToken ct = default)
{
    // If this fails after retries, message is requeued with 5s delay
    return await base.HandleAsync(message, ct);
}
```

### Reject vs Defer vs DontAck — Choose One

Users should use **one** of the three backstop attributes as their outermost handler:

| Attribute | Action | Pump Behavior |
|-----------|--------|---------------|
| `[RejectMessageOnError]` | `RejectMessageAction` | Message sent to DLQ |
| `[DeferMessageOnError]` | `DeferMessageAction` | Message requeued with delay |
| `[DontAckOnError]` | `DontAckAction` | Message left unacknowledged for transport re-delivery |

Using more than one on the same handler is a usage error (documented, not runtime-enforced).

## Requirements

### Functional Requirements

1. Provide `DeferMessageOnErrorAttribute` for sync handlers and `DeferMessageOnErrorAsyncAttribute` for async handlers
2. Both attributes accept a `step` parameter (pipeline ordering) and a `delayMilliseconds` parameter (configurable delay, integer, in milliseconds)
3. The delay value is passed from the attribute to the handler via the `InitializerParams()` / `InitializeFromAttributeParams()` pattern (same as `TimeoutPolicyAttribute`)
4. `HandlerTiming` is fixed to `Before` (same as Reject and DontAck)
5. When an unhandled exception is caught by the handler:
   - Log the exception with diagnostic information (request type, exception message) using source-generated logging
   - Throw `DeferMessageAction` with the original exception preserved as `InnerException`
6. When no exception occurs, pass through transparently (just `base.Handle()` / `base.HandleAsync()`)
7. The `DeferMessageAction` class must be extended to support constructors with message and inner exception parameters (currently it is `public class DeferMessageAction : Exception;` with no explicit constructors)

### Non-functional Requirements

- Minimal performance overhead in the happy path (no allocation, just try/catch)
- Follow existing middleware patterns exactly (mirrors the Reject and DontAck handler structure)
- Source-generated logging (`[LoggerMessage]` with `partial class Log`) matching the pattern in `RejectMessageOnErrorHandler`
- MIT license header matching existing files

### Constraints and Assumptions

- Only provides value when running within a message pump (Reactor/Proactor)
- The message pump already catches `DeferMessageAction` and calls `RequeueMessage()` which uses the subscription's `RequeueDelay` — the design phase should determine how the attribute's delay interacts with this existing mechanism
- The message pump's requeue mechanism (with `RequeueCount` limits) still applies — after exhausting requeue attempts, the message is rejected
- The attribute is intended for use as the outermost handler in the pipeline (lowest step number)

### Out of Scope

- Exception type filtering (this is a catch-all backstop)
- Modifying the Reactor/Proactor message pump's catch logic (it already handles `DeferMessageAction`)
- Runtime validation that multiple backstop attributes aren't applied to the same handler
- Changes to the scheduler or transport delay mechanisms

## Acceptance Criteria

1. Applying `[DeferMessageOnError(step: 0, delayMilliseconds: 5000)]` to a sync handler causes unhandled exceptions to throw `DeferMessageAction`
2. Applying `[DeferMessageOnErrorAsync(step: 0, delayMilliseconds: 5000)]` to an async handler causes unhandled exceptions to throw `DeferMessageAction`
3. The original exception is preserved as `InnerException` on the thrown `DeferMessageAction`
4. The exception is logged before being converted to a defer action
5. When no exception occurs, the handler passes through without interference
6. The delay value from the attribute is available to the handler via `InitializeFromAttributeParams`
7. `DeferMessageAction` has constructors matching the pattern from `RejectMessageAction` (parameterless, message-only, message + inner exception)

## File Layout

Following the established folder convention:

```
src/Paramore.Brighter/
  Defer/
    Attributes/
      DeferMessageOnErrorAttribute.cs
      DeferMessageOnErrorAsyncAttribute.cs
    Handlers/
      DeferMessageOnErrorHandler.cs
      DeferMessageOnErrorHandlerAsync.cs
  Actions/
    DeferMessageAction.cs  (modified — add constructors)
```

## Additional Context

- **Structural template**: `DontAckOnError` (spec 0020) and `RejectMessageOnError` (spec 0002) provide the exact pattern to follow
- **InitializerParams pattern**: `TimeoutPolicyAttribute` demonstrates passing a milliseconds value from attribute to handler via `InitializerParams()` / `InitializeFromAttributeParams()`
- **Existing pump handling**: Both `Reactor.cs:306` and `Proactor.cs:337` already catch `DeferMessageAction` and call `RequeueMessage()` — no pump changes needed
- **Design question for ADR**: How should the attribute's `delayMilliseconds` interact with the subscription's `RequeueDelay`? Options include adding a `Delay` property to `DeferMessageAction` that the pump reads, or using the attribute delay to set the handler-level behavior while the pump continues to use `RequeueDelay`
