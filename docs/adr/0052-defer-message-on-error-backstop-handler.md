# 52. Defer Message On Error Backstop Handler

Date: 2026-02-23

## Status

Accepted

## Context

**Parent Requirement**: [specs/0022-Defer-Message-Action-Backstop-Handler/requirements.md](../../specs/0022-Defer-Message-Action-Backstop-Handler/requirements.md)

**Scope**: This ADR covers the design of the `DeferMessageOnError` attribute/handler pair and changes to `DeferMessageAction` needed to carry a delay value from the handler pipeline to the message pump.

Brighter's handler pipeline uses exception-based flow control to signal the message pump how to dispose of a message after handler execution. Three action exceptions exist:

| Exception | Pump Behavior |
|-----------|---------------|
| `RejectMessageAction` | Reject message → DLQ |
| `DontAckAction` | Leave message unacknowledged → transport re-delivers |
| `DeferMessageAction` | Requeue message with delay → retry later |

Declarative backstop attributes already exist for `RejectMessageAction` (`RejectMessageOnErrorAttribute`, spec 0002) and `DontAckAction` (`DontAckOnErrorAttribute`, spec 0020). These attributes wrap the inner pipeline in a try/catch and throw the corresponding action exception when any unhandled exception escapes.

`DeferMessageAction` is the only action that lacks a declarative backstop attribute. Developers must currently write manual try/catch blocks in every handler that needs deferred retry behavior. This ADR addresses that gap and also resolves a design question: how to carry a configurable delay from the attribute through to the pump's requeue call.

### Current Delay Mechanism

When the pump catches `DeferMessageAction`, it calls `RequeueMessage()`, which uses the subscription-level `RequeueDelay`:

```
catch (DeferMessageAction)
{
    if (await RequeueMessage(message)) continue;
}

// RequeueMessage ultimately calls:
Channel.RequeueAsync(message, RequeueDelay);  // RequeueDelay from subscription config
```

Today, `DeferMessageAction` is a bare exception with no properties:

```csharp
public class DeferMessageAction : Exception;
```

This means all deferred messages use the same delay regardless of handler context. The attribute should allow per-handler delay configuration.

## Decision

### 1. Follow the Established Backstop Attribute Pattern

Create four new types mirroring the structure of `RejectMessageOnError` and `DontAckOnError`:

```
src/Paramore.Brighter/
  Defer/
    Attributes/
      DeferMessageOnErrorAttribute.cs        → GetHandlerType() returns DeferMessageOnErrorHandler<>
      DeferMessageOnErrorAsyncAttribute.cs    → GetHandlerType() returns DeferMessageOnErrorHandlerAsync<>
    Handlers/
      DeferMessageOnErrorHandler.cs           → catch (Exception) → throw DeferMessageAction
      DeferMessageOnErrorHandlerAsync.cs      → catch (Exception) → throw DeferMessageAction
```

**Roles and responsibilities:**

- **`DeferMessageOnErrorAttribute`** (and async variant): *Coordinator* — declares pipeline position and delay configuration; passes delay to handler via `InitializerParams()`
- **`DeferMessageOnErrorHandler<TRequest>`** (and async variant): *Service Provider* — wraps inner pipeline in try/catch; on exception, logs diagnostics and throws `DeferMessageAction` with the configured delay and original exception

### 2. Attribute Accepts `delayMilliseconds` Parameter

The attribute accepts `step` (pipeline position) and `delayMilliseconds` (delay before requeue):

```csharp
[AttributeUsage(AttributeTargets.Method)]
public class DeferMessageOnErrorAttribute : RequestHandlerAttribute
{
    private readonly int _delayMilliseconds;

    public DeferMessageOnErrorAttribute(int step, int delayMilliseconds = 0)
        : base(step, HandlerTiming.Before)
    {
        _delayMilliseconds = delayMilliseconds;
    }

    public override object[] InitializerParams()
        => [_delayMilliseconds];

    public override Type GetHandlerType()
        => typeof(DeferMessageOnErrorHandler<>);
}
```

The handler receives the delay via `InitializeFromAttributeParams`:

```csharp
public partial class DeferMessageOnErrorHandler<TRequest> : RequestHandler<TRequest>
    where TRequest : class, IRequest
{
    private int _delayMilliseconds;

    public override void InitializeFromAttributeParams(params object?[] initializerList)
    {
        _delayMilliseconds = (int?)initializerList[0] ?? 0;
    }

    public override TRequest Handle(TRequest request)
    {
        try
        {
            return base.Handle(request);
        }
        catch (Exception ex)
        {
            Log.UnhandledExceptionDeferringMessage(s_logger, ex, typeof(TRequest).Name, ex.Message);
            throw new DeferMessageAction(ex.Message, ex, _delayMilliseconds);
        }
    }
}
```

This follows the same `InitializerParams` pattern used by `TimeoutPolicyAttribute` (which passes `_milliseconds` to `TimeoutPolicyHandler`).

### 3. Extend `DeferMessageAction` with Constructors and `Delay` Property

`DeferMessageAction` gains explicit constructors (matching `RejectMessageAction` and `DontAckAction`) and a `Delay` property:

```csharp
public class DeferMessageAction : Exception
{
    public DeferMessageAction() { }
    public DeferMessageAction(string? reason) : base(reason) { }
    public DeferMessageAction(string? reason, Exception? innerException) : base(reason, innerException) { }
    public DeferMessageAction(string? reason, Exception? innerException, int delayMilliseconds)
        : base(reason, innerException)
    {
        Delay = TimeSpan.FromMilliseconds(delayMilliseconds);
    }

    /// <summary>
    /// The requested delay before requeue. If null, the pump uses the subscription's RequeueDelay.
    /// </summary>
    public TimeSpan? Delay { get; }
}
```

Design choices:
- **`TimeSpan?` not `int`**: Avoids primitive obsession; `TimeSpan` is the idiomatic .NET type for durations and matches `RequeueDelay` on the subscription and pump which are already `TimeSpan`
- **Nullable**: `null` means "use the subscription default"; a non-null value overrides it. This preserves backward compatibility — existing code that throws `new DeferMessageAction()` without a delay continues to use the subscription's `RequeueDelay`
- **Constructor accepts `int delayMilliseconds`**: Matches the attribute parameter type (attributes can only use compile-time constants, so `TimeSpan` is not allowed as an attribute parameter); conversion to `TimeSpan` happens at construction time

### 4. Pump Reads `Delay` from Exception (Minimal Change)

The Reactor and Proactor already catch `DeferMessageAction`. A small change to `RequeueMessage` allows the exception's delay to override the subscription default. Two approaches were considered:

**Option A — Pass delay through to RequeueMessage**: The pump's `catch (DeferMessageAction)` block extracts the delay and passes it:

```csharp
catch (DeferMessageAction deferAction)
{
    Log.DeferringMessage2(...);
    span?.SetStatus(...);
    var delay = deferAction.Delay ?? RequeueDelay;
    if (await RequeueMessage(message, delay)) continue;
}
```

**Option B — No pump change**: Ignore the delay on the exception; always use subscription `RequeueDelay`.

**We choose Option A** because:
- The whole point of the `delayMilliseconds` attribute parameter is per-handler delay control
- The change is minimal (one line to extract the delay, pass it through)
- Backward compatible: existing `throw new DeferMessageAction()` has `Delay = null`, so the pump falls back to subscription default
- Both Reactor and Proactor need the same small change

The `RequeueMessage` method gains an optional `delay` parameter (defaulting to the subscription's `RequeueDelay`) to avoid breaking the existing call from any other code path:

```csharp
// Proactor
private Task<bool> RequeueMessage(Message message, TimeSpan? delay = null)
{
    // ... existing handled count logic ...
    return Channel.RequeueAsync(message, delay ?? RequeueDelay);
}

// Reactor
private bool RequeueMessage(Message message, TimeSpan? delay = null)
{
    // ... existing handled count logic ...
    return Channel.Requeue(message, delay ?? RequeueDelay);
}
```

### 5. Source-Generated Logging

The handler uses source-generated logging matching the pattern from `RejectMessageOnErrorHandler`:

```csharp
private static partial class Log
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Error,
        Message = "Unhandled exception caught by backstop error handler, deferring message for request {RequestType}: {ExceptionMessage}")]
    public static partial void UnhandledExceptionDeferringMessage(
        ILogger logger, Exception ex, string requestType, string exceptionMessage);
}
```

### Architecture Overview

```
  Handler Pipeline (user code)
  ┌─────────────────────────────────────────────────────┐
  │  [DeferMessageOnError(step:0, delayMilliseconds:5000)]  │
  │  ┌─────────────────────────────────────────────────┐│
  │  │  [UsePolicy("Retry", step:2)]                   ││
  │  │  ┌─────────────────────────────────────────────┐││
  │  │  │  MyHandler.Handle(request)                  │││
  │  │  │  throws SomeException                       │││
  │  │  └─────────────────────────────────────────────┘││
  │  │  Retry catches, retries, eventually rethrows    ││
  │  └─────────────────────────────────────────────────┘│
  │  DeferMessageOnErrorHandler catches SomeException   │
  │  logs, throws DeferMessageAction(delay: 5000ms)     │
  └─────────────────────────────────────────────────────┘
                        │
                        ▼
  Message Pump (Reactor / Proactor)
  ┌─────────────────────────────────────────────────────┐
  │  catch (DeferMessageAction deferAction)             │
  │    delay = deferAction.Delay ?? RequeueDelay        │
  │    RequeueMessage(message, delay)                   │
  │      → Channel.Requeue(message, delay: 5000ms)      │
  └─────────────────────────────────────────────────────┘
```

### Key Components

| Component | Role | Responsibility |
|-----------|------|----------------|
| `DeferMessageOnErrorAttribute` | Coordinator | Declares pipeline position; passes `delayMilliseconds` to handler via `InitializerParams()` |
| `DeferMessageOnErrorAsyncAttribute` | Coordinator | Async equivalent of above |
| `DeferMessageOnErrorHandler<T>` | Service Provider | Wraps pipeline in try/catch; converts exceptions to `DeferMessageAction` with delay |
| `DeferMessageOnErrorHandlerAsync<T>` | Service Provider | Async equivalent of above |
| `DeferMessageAction` | Information Holder | Carries delay value and original exception from handler to pump |
| `Reactor.RequeueMessage` | Service Provider | Accepts optional delay override, falls back to subscription `RequeueDelay` |
| `Proactor.RequeueMessage` | Service Provider | Async equivalent of above |

### Implementation Approach

1. **Structural change (tidy first)**: Add constructors and `Delay` property to `DeferMessageAction` — no behavioral change, existing code continues to work
2. **Behavioral change**: Create `Defer/Attributes/` and `Defer/Handlers/` with the four new types
3. **Behavioral change**: Update Reactor and Proactor `catch (DeferMessageAction)` blocks and `RequeueMessage` methods to read and pass the delay
4. **Tests**: Unit tests for each handler (sync/async), verifying exception conversion, logging, delay propagation, and happy-path passthrough

## Consequences

### Positive

- Completes the set of three declarative backstop attributes — consistent API for all error handling actions
- Per-handler delay configuration without modifying subscription-level settings
- Fully backward compatible: existing `throw new DeferMessageAction()` without delay continues to use subscription default
- Minimal code: each handler is ~20 lines following an established pattern
- Pump changes are small and isolated (one extraction + one parameter pass)

### Negative

- Small change to Reactor and Proactor (low risk but touches core pump code)
- `delayMilliseconds` uses `int` in the attribute (attribute limitation) but `TimeSpan` everywhere else — minor ergonomic asymmetry
- If both the attribute delay and subscription `RequeueDelay` are set, the attribute wins — users must understand the override semantics

### Risks and Mitigations

- **Risk**: Changing `RequeueMessage` signature could break other callers
  - **Mitigation**: The `delay` parameter is optional with `null` default; existing callers are unaffected
- **Risk**: Pump changes introduce regressions
  - **Mitigation**: Existing pump tests for `DeferMessageAction` verify the current behavior; new tests verify the delay override path

## Alternatives Considered

### A. No `Delay` Property on `DeferMessageAction` — Always Use Subscription `RequeueDelay`

Simpler (no pump changes), but makes the attribute's `delayMilliseconds` parameter misleading — it would exist on the attribute but have no effect on actual requeue delay. Rejected because it violates the principle of least surprise.

### B. Store Delay in `Context.Bag` Instead of on the Exception

The handler could write the delay to `Context.Bag` and the pump could read it from there. Rejected because:
- The context bag is a general-purpose dictionary with stringly-typed keys — fragile
- The exception is the natural carrier for this information (it flows directly from handler to pump)
- The other action exceptions (`RejectMessageAction`, `DontAckAction`) carry data as exception properties, not via context bag

### C. Use `TimeSpan` as the Attribute Parameter

Not possible — C# attributes only allow compile-time constant types (primitives, strings, Type, enums). `TimeSpan` is a struct and cannot be used as an attribute constructor parameter. The conversion from `int delayMilliseconds` to `TimeSpan` happens inside the handler/exception constructor.

## References

- Requirements: [specs/0022-Defer-Message-Action-Backstop-Handler/requirements.md](../../specs/0022-Defer-Message-Action-Backstop-Handler/requirements.md)
- Related ADRs:
  - ADR 0006: Blocking and Non-Blocking Retries
  - ADR 0037: Universal Scheduler Delay
  - ADR 0038: Don't Ack Action
- Existing patterns:
  - `RejectMessageOnErrorAttribute` / `RejectMessageOnErrorHandler` (spec 0002)
  - `DontAckOnErrorAttribute` / `DontAckOnErrorHandler` (spec 0020)
  - `TimeoutPolicyAttribute` / `TimeoutPolicyHandler` (InitializerParams pattern)
