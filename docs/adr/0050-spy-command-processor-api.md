# 50. Spy Command Processor API

Date: 2026-02-05

## Status

Proposed

## Context

**Parent Requirement**: [specs/0002-testing-support-for-command-processor-handlers/requirements.md](../../specs/0002-testing-support-for-command-processor-handlers/requirements.md)

**Scope**: This ADR focuses specifically on the public API design of `SpyCommandProcessor` - the methods users will call to verify handler interactions. See [ADR 0049](0049-testing-assembly-structure.md) for the overall assembly structure.

### Problem

The `IAmACommandProcessor` interface has 30+ methods spanning multiple concerns:
- Basic operations: `Send`, `Publish`, `Post` (sync and async)
- Scheduled operations: Send/Publish/Post with `DateTimeOffset` or `TimeSpan` delays
- Outbox pattern: `DepositPost` (4 overloads sync, 4 async), `ClearOutbox` (sync and async)
- Request-Reply: `Call`

Users testing handlers need a clear, intuitive API to:
1. Verify which methods were called
2. Inspect the requests that were passed
3. Assert on call sequences and counts
4. Optionally customize behavior (e.g., throw exceptions)

### Forces

1. **Discoverability**: API should be intuitive without reading extensive docs
2. **Common cases easy**: Most users just need "was Publish called with X?"
3. **Advanced cases possible**: Power users need detailed call inspection
4. **Consistency**: Follow established patterns from testing libraries (Moq, NSubstitute)
5. **Type safety**: Leverage generics to provide compile-time safety

## Decision

Design a layered API with three access patterns, from simplest to most detailed:

### API Design

```
┌─────────────────────────────────────────────────────────────────┐
│                    SpyCommandProcessor API                       │
├─────────────────────────────────────────────────────────────────┤
│  Layer 1: Quick Checks (most common)                            │
│  ─────────────────────────────────────                          │
│  • WasCalled(CommandType) → bool                                │
│  • CallCount(CommandType) → int                                 │
│  • Observe<T>() → T (dequeue pattern)                           │
├─────────────────────────────────────────────────────────────────┤
│  Layer 2: Request Inspection                                    │
│  ────────────────────────────                                   │
│  • GetRequests<T>() → IEnumerable<T>                           │
│  • GetCalls(CommandType) → IEnumerable<RecordedCall>           │
│  • Commands → IReadOnlyList<CommandType>                        │
├─────────────────────────────────────────────────────────────────┤
│  Layer 3: Full Detail Access                                    │
│  ───────────────────────────                                    │
│  • RecordedCalls → IReadOnlyList<RecordedCall>                 │
│  • DepositedRequests → IReadOnlyDictionary<Id, IRequest>       │
└─────────────────────────────────────────────────────────────────┘
```

### Layer 1: Quick Checks

**Role**: Service Provider - answering simple verification questions

```csharp
/// <summary>
/// Check if a specific method type was called at least once.
/// </summary>
/// <example>
/// Assert.True(spy.WasCalled(CommandType.Publish));
/// </example>
public bool WasCalled(CommandType type);

/// <summary>
/// Get the number of times a specific method type was called.
/// </summary>
/// <example>
/// Assert.Equal(2, spy.CallCount(CommandType.Send));
/// </example>
public int CallCount(CommandType type);

/// <summary>
/// Dequeue the next captured request in FIFO order.
/// Useful for sequential verification of multiple calls.
/// </summary>
/// <example>
/// var event1 = spy.Observe<OrderCreatedEvent>();
/// var event2 = spy.Observe<OrderShippedEvent>();
/// </example>
public T Observe<T>() where T : class, IRequest;
```

### Layer 2: Request Inspection

**Role**: Information Holder - providing filtered access to recorded data

```csharp
/// <summary>
/// Get all captured requests of a specific type (non-destructive).
/// </summary>
/// <example>
/// var events = spy.GetRequests<OrderCreatedEvent>();
/// Assert.Single(events);
/// Assert.Equal("ORD-123", events.First().OrderId);
/// </example>
public IEnumerable<T> GetRequests<T>() where T : class, IRequest;

/// <summary>
/// Get all recorded calls of a specific command type.
/// </summary>
/// <example>
/// var depositCalls = spy.GetCalls(CommandType.Deposit);
/// Assert.Equal(3, depositCalls.Count());
/// </example>
public IEnumerable<RecordedCall> GetCalls(CommandType type);

/// <summary>
/// Simple list of command types in call order.
/// </summary>
/// <example>
/// Assert.Equal(new[] { CommandType.Deposit, CommandType.Clear }, spy.Commands);
/// </example>
public IReadOnlyList<CommandType> Commands { get; }
```

### Layer 3: Full Detail Access

**Role**: Information Holder - providing complete recorded state

```csharp
/// <summary>
/// All recorded calls with full details (type, request, timestamp, context).
/// </summary>
public IReadOnlyList<RecordedCall> RecordedCalls { get; }

/// <summary>
/// Requests deposited via DepositPost, keyed by their Id.
/// Useful for verifying outbox pattern usage.
/// </summary>
/// <example>
/// var depositedId = handler.Handle(command);
/// Assert.True(spy.DepositedRequests.ContainsKey(depositedId));
/// </example>
public IReadOnlyDictionary<Id, IRequest> DepositedRequests { get; }
```

### State Management

```csharp
/// <summary>
/// Clear all recorded state. Useful for test isolation
/// when reusing a spy across multiple test cases.
/// </summary>
public void Reset();
```

### RecordedCall Record

```csharp
/// <summary>
/// Captures complete details of a single IAmACommandProcessor method call.
/// </summary>
/// <param name="Type">Which method was called</param>
/// <param name="Request">The request object passed to the method</param>
/// <param name="Timestamp">When the call occurred</param>
/// <param name="Context">The RequestContext if provided</param>
public record RecordedCall(
    CommandType Type,
    IRequest Request,
    DateTime Timestamp,
    RequestContext? Context = null
);
```

### Outbox Pattern Support

The outbox pattern (`DepositPost` + `ClearOutbox`) is a common use case requiring special handling:

```csharp
// Internal tracking for outbox simulation
private readonly Dictionary<Id, IRequest> _depositedRequests = new();

public virtual Id DepositPost<TRequest>(TRequest request, ...)
    where TRequest : class, IRequest
{
    var id = request.Id;
    _depositedRequests[id] = request;
    RecordCall(CommandType.Deposit, request);
    return id;
}

public virtual void ClearOutbox(Id[] ids, ...)
{
    foreach (var id in ids)
    {
        if (_depositedRequests.TryGetValue(id, out var request))
        {
            // Move from deposited to "sent" queue for Observe<T>()
            _requests.Enqueue(request);
        }
    }
    RecordCall(CommandType.Clear, /* synthetic request with ids */);
}
```

### Extension Points

All interface methods are `virtual` to allow subclassing for special scenarios:

```csharp
/// <summary>
/// Spy that throws on Send to test error handling.
/// </summary>
public class ThrowingSpyCommandProcessor : SpyCommandProcessor
{
    public override void Send<T>(T command, RequestContext? context = null)
    {
        base.Send(command, context);  // Record the call first
        throw new BrokerUnreachableException("Simulated failure");
    }
}
```

## Consequences

### Positive

- **Progressive disclosure**: Simple cases use simple API; complex needs have full access
- **Familiar patterns**: `Observe<T>()` follows established spy/mock patterns
- **Type-safe**: Generic methods provide compile-time checking
- **Testable outbox**: Dedicated support for the common outbox pattern
- **Extensible**: Virtual methods allow custom spy behavior

### Negative

- **Learning curve**: Three layers may initially confuse users (mitigated by docs)
- **State accumulation**: Users must remember to `Reset()` between tests if reusing
- **Memory usage**: Stores all requests (acceptable for test scenarios)

### Risks and Mitigations

| Risk | Mitigation |
|------|------------|
| Users confused by `Observe` vs `GetRequests` | Clear XML docs; `Observe` mutates, `GetRequests` doesn't |
| Forgetting `Reset()` causes test pollution | Document in quick-start; consider auto-reset option |
| `ClearOutbox` tracking complexity | Test thoroughly; document behavior |

## Alternatives Considered

### 1. Fluent Assertion API

```csharp
spy.Should().HavePublished<OrderCreatedEvent>()
   .WithProperty(e => e.OrderId, "ORD-123");
```

**Rejected because**:
- Adds dependency on assertion library (Shouldly, FluentAssertions)
- Can be built on top of our basic API by users
- Out of scope for initial release

### 2. Callback-Based Verification

```csharp
spy.OnPublish<OrderCreatedEvent>(e => Assert.Equal("ORD-123", e.OrderId));
```

**Rejected because**:
- More complex implementation
- Less intuitive than inspect-after-the-fact
- Harder to debug test failures

### 3. Single Method API

```csharp
var calls = spy.GetAllCalls();
// User filters and inspects manually
```

**Rejected because**:
- Puts too much burden on users
- Common cases should be one-liners
- Violates progressive disclosure principle

## References

- Requirements: [specs/0002-testing-support-for-command-processor-handlers/requirements.md](../../specs/0002-testing-support-for-command-processor-handlers/requirements.md)
- Related ADRs: [ADR 0049: Testing Assembly Structure](0049-testing-assembly-structure.md)
- IAmACommandProcessor: [src/Paramore.Brighter/IAmACommandProcessor.cs](../../src/Paramore.Brighter/IAmACommandProcessor.cs)
- Moq verification patterns: https://github.com/moq/moq4
- NSubstitute received calls: https://nsubstitute.github.io/help/received-calls/
