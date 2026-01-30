# 37. Reject Message On Error Handler

Date: 2026-01-30

## Status

Proposed

## Context

**Parent Requirement**: [specs/0002-backstop-error-handler/requirements.md](../../specs/0002-backstop-error-handler/requirements.md)

**Scope**: This ADR addresses how to provide a declarative middleware attribute for catching unhandled exceptions and converting them to message rejections for DLQ routing.

### Problem

When using Brighter's message pump (Reactor or Proactor), developers need a simple way to ensure unhandled exceptions cause messages to be rejected and routed to a Dead Letter Queue. Currently, achieving this requires:

1. Implementing a custom `FallbackPolicy` handler with catch-all logic
2. Manually wrapping handler code in try/catch blocks
3. Throwing `RejectMessageAction` explicitly

This is boilerplate that most message-driven applications need, and Brighter should provide it declaratively.

### Forces

- **Simplicity**: Developers want a single attribute to enable DLQ routing on failure
- **Consistency**: Must follow existing middleware patterns (`RequestHandlerAttribute` → `RequestHandler`)
- **Observability**: The original exception must be logged before being replaced
- **Flexibility**: Should work with other middleware (retry policies, circuit breakers) when positioned correctly

### Existing Pattern

Brighter uses an attribute-based middleware pattern:

```
┌─────────────────────────────────┐
│     RequestHandlerAttribute     │  ← Applied to Handle() method
│  - GetHandlerType()             │  ← Returns handler type
│  - InitializerParams()          │  ← Passes config to handler
│  - Step, Timing                 │  ← Controls pipeline position
└─────────────────────────────────┘
                │
                ▼
┌─────────────────────────────────┐
│      RequestHandler<T>          │  ← Implements behavior
│  - Handle(request)              │  ← Wraps base.Handle()
│  - InitializeFromAttributeParams│  ← Receives config
└─────────────────────────────────┘
```

Examples: `UsePolicyAttribute` → `ExceptionPolicyHandler`, `FallbackPolicyAttribute` → `FallbackPolicyHandler`

## Decision

Introduce two attribute/handler pairs following the existing middleware pattern:

### Naming

- **Attributes**: `RejectMessageOnErrorAttribute`, `RejectMessageOnErrorAsyncAttribute`
- **Handlers**: `RejectMessageOnErrorHandler<TRequest>`, `RejectMessageOnErrorHandlerAsync<TRequest>`

The name "RejectMessageOnError" was chosen because:
- Describes the action taken (reject message)
- Indicates the trigger (on error)
- Aligns with `RejectMessageAction` terminology

### Architecture Overview

```
Pipeline with RejectMessageOnError at step 1 (outermost):

    ┌──────────────────────────────────────────────────────────┐
    │  RejectMessageOnErrorHandler (step 1)                    │
    │  ┌────────────────────────────────────────────────────┐  │
    │  │  try {                                             │  │
    │  │    ┌──────────────────────────────────────────┐    │  │
    │  │    │  ExceptionPolicyHandler (step 2)         │    │  │
    │  │    │  ┌────────────────────────────────────┐  │    │  │
    │  │    │  │  Target Handler                    │  │    │  │
    │  │    │  │  - Business logic                  │  │    │  │
    │  │    │  └────────────────────────────────────┘  │    │  │
    │  │    └──────────────────────────────────────────┘    │  │
    │  │  } catch (Exception ex) {                          │  │
    │  │    Log(ex);                                        │  │
    │  │    throw new RejectMessageAction(ex.Message, ex);  │  │
    │  │  }                                                 │  │
    │  └────────────────────────────────────────────────────┘  │
    └──────────────────────────────────────────────────────────┘
```

### Key Components

#### Responsibilities

| Component | Role | Responsibilities |
|-----------|------|------------------|
| `RejectMessageOnErrorAttribute` | Interfacer | **Knowing**: Handler type to create. **Deciding**: Pipeline position (timing, step) |
| `RejectMessageOnErrorHandler<T>` | Service Provider | **Doing**: Catch exceptions, log, throw `RejectMessageAction` |

#### RejectMessageOnErrorAttribute

```csharp
[AttributeUsage(AttributeTargets.Method)]
public class RejectMessageOnErrorAttribute : RequestHandlerAttribute
{
    public RejectMessageOnErrorAttribute(int step)
        : base(step, HandlerTiming.Before) { }

    public override Type GetHandlerType()
        => typeof(RejectMessageOnErrorHandler<>);
}
```

- No `InitializerParams()` needed - handler has no configuration
- Always `HandlerTiming.Before` - must wrap subsequent handlers

#### RejectMessageOnErrorHandler<TRequest>

```csharp
public class RejectMessageOnErrorHandler<TRequest> : RequestHandler<TRequest>
    where TRequest : class, IRequest
{
    public override TRequest Handle(TRequest request)
    {
        try
        {
            return base.Handle(request);
        }
        catch (Exception ex)
        {
            Log.UnhandledExceptionRejectingMessage(_logger, ex, ...);
            throw new RejectMessageAction(ex.Message, ex);
        }
    }
}
```

- Wraps `base.Handle()` in try/catch
- Logs exception at Error level before throwing
- Preserves original exception as `InnerException`

#### Async Variants

`RejectMessageOnErrorAsyncAttribute` and `RejectMessageOnErrorHandlerAsync<TRequest>` follow the same pattern using `HandleAsync()`.

### Recommended Usage

```csharp
public class MyMessageHandler : RequestHandler<MyMessage>
{
    [RejectMessageOnError(step: 0)]           // Outermost - catches anything
    [UsePolicy("RetryPolicy", step: 2)]       // Retries first
    public override MyMessage Handle(MyMessage message)
    {
        // Business logic - if this fails after retries, message goes to DLQ
        return base.Handle(message);
    }
}
```

Documentation should emphasize:
- Use step 0 (or lowest step number) to ensure it's the outermost wrapper
- Place retry/circuit breaker policies at higher step numbers
- This is a "last resort" - other policies should handle transient failures

### File Locations

Following existing conventions:
- `src/Paramore.Brighter/RejectMessageOnErrorAttribute.cs`
- `src/Paramore.Brighter/RejectMessageOnErrorAsyncAttribute.cs`
- `src/Paramore.Brighter/Handlers/RejectMessageOnErrorHandler.cs`
- `src/Paramore.Brighter/Handlers/RejectMessageOnErrorHandlerAsync.cs`

## Consequences

### Positive

- **Simple**: Single attribute provides DLQ routing on failure
- **Declarative**: No boilerplate try/catch in user code
- **Consistent**: Follows established middleware patterns
- **Observable**: Exception is logged before conversion
- **Composable**: Works with existing middleware when ordered correctly

### Negative

- **Context-specific**: Only useful within message pump; outside a pump, `RejectMessageAction` propagates to caller
- **Step ordering**: Users must understand pipeline ordering to position correctly
- **No filtering**: Catches all exceptions; users needing selective handling should use `FallbackPolicy`

### Risks and Mitigations

| Risk | Mitigation                                                  |
|------|-------------------------------------------------------------|
| Users place handler at wrong step | Documentation with clear examples; recommend step 0         |
| Confusion with FallbackPolicy | Document when to use each; this is simpler but less flexible |
| Users expect it to work outside pump | Document that it's designed for Reactor/Proactor            |

## Alternatives Considered

### 1. Extend FallbackPolicyHandler

Add a mode to `FallbackPolicyAttribute` like `[FallbackPolicy(rejectOnError: true, ...)]`.

**Rejected because**:
- `FallbackPolicy` has a different purpose (call fallback method)
- Would complicate an already-complex handler
- New attribute is clearer in intent

### 2. Configure at Message Pump Level

Add a pump-level option to reject on unhandled exceptions.

**Rejected because**:
- Less granular control (all-or-nothing)
- Different handlers may want different behavior
- Attribute approach is consistent with existing patterns

### 3. Generic Exception-to-Action Handler

A configurable handler that maps exception types to actions (reject, defer, etc.).

**Rejected because**:
- Over-engineered for the common case
- Adds configuration complexity
- Users needing this flexibility can implement custom handlers

## References

- Requirements: [specs/0002-backstop-error-handler/requirements.md](../../specs/0002-backstop-error-handler/requirements.md)
- Existing pattern: `FallbackPolicyAttribute` / `FallbackPolicyHandler` in `src/Paramore.Brighter/Policies/`
- `RejectMessageAction`: `src/Paramore.Brighter/Actions/RejectMessageAction.cs`
- Message pump handling: `src/Paramore.Brighter.ServiceActivator/Reactor.cs`, `Proactor.cs`
