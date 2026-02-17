# 38. Don't Ack Action

Date: 2026-02-16

## Status

Accepted. Amended 2026-02-17 to add Transport Nack (see Decision §6).

## Context

**Parent Requirement**: [specs/0020-DontAckAction/requirements.md](../../specs/0020-DontAckAction/requirements.md)

**Scope**: This ADR addresses the addition of a `DontAckAction` exception type that signals the message pump to not acknowledge a message, plus two convenience attributes (`FeatureSwitchAttribute.DontAck` and `DontAckOnErrorAttribute`) for common use cases.

### Problem

Brighter's message pump always acknowledges a message after processing, even when an unhandled exception escapes the handler pipeline. The assumption is that errors are either transient (handled by retry policies) or non-transient (acked and investigated from logs). This works for most scenarios, but there are cases where the correct behavior is to **not acknowledge** the message so it remains on the channel:

1. **Feature Switch Off** — A handler is disabled via `FeatureSwitchAttribute`. Currently the message is silently consumed. The user wants it to remain on the channel until the feature is re-enabled.

2. **Stream Processing / Blocking** — The user wants to block on a message indefinitely, re-presenting it on each pump iteration until processing succeeds.

Brighter already supports exception-based flow control to alter pump behavior:

| Action | Purpose | Effect on Message |
|--------|---------|-------------------|
| `DeferMessageAction` | Requeue with delay | Requeued (acknowledged) |
| `RejectMessageAction` | Route to DLQ | Rejected (acknowledged) |
| `InvalidMessageAction` | Route to invalid channel/DLQ | Rejected (acknowledged) |

All three result in the message being acknowledged (consumed or moved). There is no mechanism to leave a message unacknowledged on the channel.

### Forces

- **Consistency**: Must follow the established exception-as-signal pattern used by `DeferMessageAction`, `RejectMessageAction`, and `InvalidMessageAction`
- **Safety**: Must prevent tight-loop CPU burn when a message is repeatedly not acknowledged
- **Observability**: Must log enough context (message ID, channel, inner exception) for operators
- **Flexibility**: Must support both "block forever" (feature switch) and "block until limit" (error recovery) strategies via the existing unacceptable message limit mechanism

## Decision

### 1. DontAckAction Exception

Add `DontAckAction` as a new exception type in `Paramore.Brighter.Actions`, following the pattern established by `RejectMessageAction`:

```csharp
public class DontAckAction : Exception
{
    public DontAckAction() {}
    public DontAckAction(string? reason) : base(reason) {}
    public DontAckAction(string? reason, Exception? innerException) : base(reason, innerException) {}
}
```

**Role**: Signal. **Responsibility**: Knowing the reason for not acknowledging and any causal inner exception. This is a semantic signal thrown from handler code to influence pump behavior, identical in pattern to the other action exceptions.

### 2. Pump Handling (Reactor and Proactor)

Both `Reactor` and `Proactor` catch `DontAckAction` in their exception handling chain. The behavior on catch:

1. **Nack the message** — call `Channel.Nack(message)` (Reactor) or `await Channel.NackAsync(message, ct)` (Proactor) to explicitly release the message back to the transport (see §6)
2. **Increment unacceptable message count** — via existing `IncrementUnacceptableMessageCount()`
3. **Log inner exception** — if `DontAckAction.InnerException` is present, log it at Warning level
4. **Apply delay** — `Thread.Sleep(DontAckDelay)` (Reactor) or `await Task.Delay(DontAckDelay, ct)` (Proactor) before continuing the loop
5. **Continue the loop** — the message will be re-presented from the channel on the next iteration

#### Catch Block Position

`DontAckAction` is caught **after** `DeferMessageAction` and **before** `RejectMessageAction`:

```
catch (AggregateException)     — check for DontAckAction in inner exceptions
catch (ConfigurationException) — stop pump
catch (DeferMessageAction)     — requeue
catch (DontAckAction)          — NEW: don't ack, delay, continue
catch (RejectMessageAction)    — reject to DLQ
catch (InvalidMessageAction)   — reject as unacceptable
catch (MessageMappingException)— mapping failure
catch (Exception)              — generic fallback
```

This ordering ensures `DontAckAction` is handled before the more drastic reject/invalid actions, but after defer (which has different requeue semantics).

#### AggregateException Handling

A `dontAck` boolean flag is added alongside the existing `defer`, `reject`, and `invalidMessage` flags:

```csharp
if (exception is DontAckAction dontAckAction)
{
    dontAck = true;
    dontAckInnerException = dontAckAction.InnerException;
    continue;
}
```

When `dontAck` is true, the handler logs, increments the count, applies the delay, and continues — same as the standalone catch block.

#### TranslateMessage Unwrapping

Add `DontAckAction` to the `TargetInvocationException` unwrapping in `TranslateMessage` (Reactor) and `TranslateMessageAsync` (Proactor):

```csharp
if (innerException is InvalidMessageAction or RejectMessageAction or DeferMessageAction or DontAckAction)
    throw innerException;
```

### 3. DontAckDelay Configuration

Add a `DontAckDelay` property to the `MessagePump` base class:

```csharp
/// <summary>
/// The delay to wait before the next pump iteration after a DontAckAction.
/// Prevents tight-loop CPU burn when a message is repeatedly not acknowledged.
/// </summary>
public TimeSpan DontAckDelay { get; set; } = TimeSpan.FromSeconds(1);
```

This follows the existing pattern of `ChannelFailureDelay`, `EmptyChannelDelay`, and `RequeueDelay`. The default of 1 second provides a reasonable balance between responsiveness and CPU usage.

The delay is applied **inside** the catch block before `continue`, ensuring it only affects DontAckAction scenarios and does not alter normal pump timing.

**Role**: The pump acts as **Controller** — it decides the timing of re-presentation based on this configuration.

### 4. FeatureSwitchAttribute DontAck Option

Add a `DontAck` boolean property to `FeatureSwitchAttribute` and `FeatureSwitchAsyncAttribute`:

```csharp
public class FeatureSwitchAttribute : RequestHandlerAttribute
{
    private readonly Type _handler;
    private readonly FeatureSwitchStatus _status;
    private readonly bool _dontAck;

    public FeatureSwitchAttribute(Type handler, FeatureSwitchStatus status, int step,
        bool dontAck = false, HandlerTiming timing = HandlerTiming.Before)
        : base(step, timing)
    {
        _handler = handler;
        _status = status;
        _dontAck = dontAck;
    }

    public override object[] InitializerParams()
        => [_handler, _status, _dontAck];
}
```

The `FeatureSwitchHandler` and `FeatureSwitchHandlerAsync` are updated to receive and use this parameter:

```csharp
public override TRequest Handle(TRequest request)
{
    var featureEnabled = _status;
    if (featureEnabled is FeatureSwitchStatus.Config)
        featureEnabled = Context?.FeatureSwitches?.StatusOf(_handler!) ?? FeatureSwitchStatus.On;

    if (featureEnabled is FeatureSwitchStatus.Off)
    {
        if (_dontAck)
            throw new DontAckAction($"Feature switch off for {_handler?.Name}, not acknowledging message");
        return request;
    }

    return base.Handle(request);
}
```

**Role**: The `FeatureSwitchHandler` acts as **Service Provider** with a **Deciding** responsibility — it decides whether to throw `DontAckAction` or silently skip, based on the `_dontAck` configuration.

#### Usage

```csharp
[FeatureSwitch(typeof(MyHandler), FeatureSwitchStatus.Config, step: 1, dontAck: true)]
public override MyMessage Handle(MyMessage message)
{
    // When feature is off: message stays on channel
    // When feature is on: normal processing
    return base.Handle(message);
}
```

### 5. DontAckOnErrorAttribute / Handler

Following the exact pattern established by `RejectMessageOnErrorAttribute` / `RejectMessageOnErrorHandler` (ADR 0037), introduce:

- `DontAckOnErrorAttribute` / `DontAckOnErrorAsyncAttribute`
- `DontAckOnErrorHandler<TRequest>` / `DontAckOnErrorHandlerAsync<TRequest>`

```csharp
public class DontAckOnErrorHandler<TRequest> : RequestHandler<TRequest>
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
            throw new DontAckAction(ex.Message, ex);
        }
    }
}
```

**Role**: **Service Provider**. **Responsibility**: Doing — catch exceptions and wrap in `DontAckAction`, preserving the original as inner exception. This is structurally identical to `RejectMessageOnErrorHandler` but produces a different signal.

#### Usage

```csharp
[DontAckOnError(step: 0)]              // Outermost - catches anything
[UsePolicy("RetryPolicy", step: 2)]    // Retries first
public override MyMessage Handle(MyMessage message)
{
    // If this fails after retries, message stays on channel
    return base.Handle(message);
}
```

### 6. Transport Nack (Amendment 2026-02-17)

The initial design (§2) relied on simply skipping the `AcknowledgeMessage` call. While this works, it has a significant drawback on queue-based transports: the message remains invisible to other consumers until the transport's visibility timeout expires. For RabbitMQ this could be the consumer timeout (30 minutes by default); for SQS, the visibility timeout (30 seconds default); for Azure Service Bus, the lock duration (30 seconds to 5 minutes).

An explicit nack tells the transport to **immediately release** the message so another consumer can pick it up. This is the correct semantic for queue-based transports. For stream-based transports (Kafka, etc.), nack is a no-op because the existing behavior (not committing the offset) is already sufficient.

#### Nack vs Reject vs Requeue

| Operation | Semantics | Message State After |
|-----------|-----------|---------------------|
| **Acknowledge** | Consumed successfully | Removed from queue/offset committed |
| **Reject** | Consumed as failed | Routed to DLQ (removed from main queue) |
| **Requeue** | Re-enqueue with delay | Original acked, new copy enqueued |
| **Nack** | Release without consuming | Immediately available to any consumer |

Nack is the only operation that neither consumes nor copies the message. It simply releases the transport's lock, returning the message to its original state.

#### Interface Changes

Add `Nack` to the consumer and channel interfaces:

```csharp
// IAmAMessageConsumerSync
void Nack(Message message);

// IAmAMessageConsumerAsync
Task NackAsync(Message message, CancellationToken cancellationToken = default);

// IAmAChannelSync
void Nack(Message message);

// IAmAChannelAsync
Task NackAsync(Message message, CancellationToken cancellationToken = default);
```

`Channel` and `ChannelAsync` delegate to their respective consumer, following the same pattern as `Acknowledge`, `Reject`, and `Requeue`.

#### Transport Implementations

| Transport | Type | Nack Implementation |
|-----------|------|---------------------|
| **RabbitMQ** | Queue | `BasicNack(deliveryTag, multiple: false, requeue: true)` |
| **AWS SQS** | Queue | `ChangeMessageVisibility(receiptHandle, visibilityTimeout: 0)` |
| **Azure Service Bus** | Queue | `AbandonMessageAsync(lockToken)` |
| **Kafka** | Stream | No-op (don't commit offset) |
| **Redis** | Queue-like | No-op (LPOP is destructive; cannot un-pop) |
| **MQTT** | Pub/Sub | No-op (no acknowledgment concept) |
| **GCP Pub/Sub** | Stream | No-op (don't acknowledge) |
| **InMemoryMessageConsumer** | Queue | Remove from `_lockedMessages` and re-enqueue to bus |

The InMemoryMessageConsumer behaves as a queue: nack removes the message from `_lockedMessages` and re-enqueues it to the `InternalBus`, making it immediately available.

#### Pump Integration

The `DontAckAction` catch block in both Reactor and Proactor is updated to call `Channel.Nack(message)` / `await Channel.NackAsync(message, ct)` before the delay and continue:

```csharp
// Reactor
catch (DontAckAction dontAckAction)
{
    Log.NotAcknowledgingMessage(...);
    if (dontAckAction.InnerException != null)
        Log.DontAckActionInnerException(...);
    span?.SetStatus(ActivityStatusCode.Error, ...);
    Channel.Nack(message);
    IncrementUnacceptableMessageCount();
    Task.Delay(DontAckDelay).GetAwaiter().GetResult();
    continue;
}
```

The delay still applies to the *current* consumer to prevent tight loops. However, the message is immediately available to *other* consumers after the nack.

#### Why This Amends the Original Decision

The original decision (§2) rejected "Nack via Channel" (see Alternatives §1) because not all transports support nack with requeue. This amendment resolves that concern by making Nack a **no-op for streams** where the existing behavior is sufficient, while providing **active unlock for queues** where waiting for visibility timeout is suboptimal. The interface is uniform; the semantics vary by transport type.

### Architecture Overview

```
Message Pump Loop (Reactor/Proactor)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

  Receive message from channel
         │
         ▼
  TranslateMessage ──(unwrap DontAckAction from TIE)──┐
         │                                             │
         ▼                                             │
  DispatchRequest                                      │
    ┌─────────────────────────────────────────┐        │
    │ Handler Pipeline                        │        │
    │  ┌───────────────────────────────────┐  │        │
    │  │ DontAckOnErrorHandler (step 0)    │  │        │
    │  │  ┌─────────────────────────────┐  │  │        │
    │  │  │ FeatureSwitchHandler        │──┼──┼── throw DontAckAction (if off + dontAck)
    │  │  │  ┌───────────────────────┐  │  │  │        │
    │  │  │  │ RetryPolicy (step 2)  │  │  │  │        │
    │  │  │  │  ┌─────────────────┐  │  │  │  │        │
    │  │  │  │  │ Target Handler  │  │  │  │  │        │
    │  │  │  │  └─────────────────┘  │  │  │  │        │
    │  │  │  └───────────────────────┘  │  │  │        │
    │  │  └─────────────────────────────┘  │  │        │
    │  │  catch (Exception) →              │  │        │
    │  │    throw DontAckAction(ex)  ──────┼──┼────────┤
    │  └───────────────────────────────────┘  │        │
    └─────────────────────────────────────────┘        │
         │                                             │
         ▼                                             │
  Exception Handling ◄─────────────────────────────────┘
    catch (DontAckAction dontAckAction)
    {
      Channel.Nack(message);           ─── releases message to transport
      IncrementUnacceptableMessageCount();
      Log inner exception (if any);
      Sleep/Delay(DontAckDelay);
      continue;  ─── skips AcknowledgeMessage ──┐
    }                                           │
         │                                      │
         ▼                                      │
  AcknowledgeMessage(message)  ◄── NOT reached ─┘
         │
         ▼
  Loop continues (message re-presented)

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### Key Components

| Component | Role | Responsibilities |
|-----------|------|------------------|
| `DontAckAction` | Signal | **Knowing**: reason, inner exception |
| `Reactor` / `Proactor` | Controller | **Deciding**: nack + delay + continue loop. **Doing**: increment count, log |
| `MessagePump` (base) | Information Holder | **Knowing**: `DontAckDelay` configuration |
| `Channel` / `ChannelAsync` | Interfacer | **Doing**: delegate Nack to consumer |
| `IAmAMessageConsumer*` | Service Provider | **Doing**: transport-specific nack (unlock/release or no-op) |
| `FeatureSwitchHandler` | Service Provider | **Deciding**: throw `DontAckAction` when off + dontAck, else skip silently |
| `DontAckOnErrorHandler` | Service Provider | **Doing**: catch exceptions, wrap in `DontAckAction` |
| `FeatureSwitchAttribute` | Interfacer | **Knowing**: handler type, status, dontAck flag |
| `DontAckOnErrorAttribute` | Interfacer | **Knowing**: handler type for pipeline injection |

### File Locations

Following existing conventions:

**Action exception:**
- `src/Paramore.Brighter/Actions/DontAckAction.cs`

**DontAckOnError (following Reject/ pattern from ADR 0037):**
- `src/Paramore.Brighter/DontAck/Attributes/DontAckOnErrorAttribute.cs`
- `src/Paramore.Brighter/DontAck/Attributes/DontAckOnErrorAsyncAttribute.cs`
- `src/Paramore.Brighter/DontAck/Handlers/DontAckOnErrorHandler.cs`
- `src/Paramore.Brighter/DontAck/Handlers/DontAckOnErrorHandlerAsync.cs`

**Modified interfaces (Nack):**
- `src/Paramore.Brighter/IAmAMessageConsumerSync.cs` (add `Nack`)
- `src/Paramore.Brighter/IAmAMessageConsumerAsync.cs` (add `NackAsync`)
- `src/Paramore.Brighter/IAmAChannelSync.cs` (add `Nack`)
- `src/Paramore.Brighter/IAmAChannelAsync.cs` (add `NackAsync`)
- `src/Paramore.Brighter/Channel.cs` (delegate to consumer)
- `src/Paramore.Brighter/ChannelAsync.cs` (delegate to consumer)
- `src/Paramore.Brighter/InMemoryMessageConsumer.cs` (unlock + re-enqueue)

**Modified transport consumers (Nack):**
- `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageConsumer.cs` (BasicNack)
- `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageConsumer.cs` (BasicNackAsync)
- `src/Paramore.Brighter.MessagingGateway.AWSSQS/SqsMessageConsumer.cs` (ChangeMessageVisibility)
- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusConsumer.cs` (AbandonMessage)
- `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessageConsumer.cs` (no-op)
- `src/Paramore.Brighter.MessagingGateway.Redis/RedisMessageConsumer.cs` (no-op)
- `src/Paramore.Brighter.MessagingGateway.MQTT/MQTTMessageConsumer.cs` (no-op)
- `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPubSubStreamMessageConsumer.cs` (no-op)

**Modified files (original phases):**
- `src/Paramore.Brighter/FeatureSwitch/Attributes/FeatureSwitchAttribute.cs`
- `src/Paramore.Brighter/FeatureSwitch/Attributes/FeatureSwitchAsyncAttribute.cs`
- `src/Paramore.Brighter/FeatureSwitch/Handlers/FeatureSwitchHandler.cs`
- `src/Paramore.Brighter/FeatureSwitch/Handlers/FeatureSwitchHandlerAsync.cs`
- `src/Paramore.Brighter.ServiceActivator/MessagePump.cs` (DontAckDelay property)
- `src/Paramore.Brighter.ServiceActivator/Reactor.cs` (catch block calls Nack + AggregateException + TranslateMessage)
- `src/Paramore.Brighter.ServiceActivator/Proactor.cs` (catch block calls NackAsync + AggregateException + TranslateMessageAsync)

**Tests:**
- `tests/Paramore.Brighter.Core.Tests/DontAck/` (DontAckOnError handler tests)
- `tests/Paramore.Brighter.Core.Tests/FeatureSwitch/` (DontAck option tests)
- `tests/Paramore.Brighter.Core.Tests/MessageDispatch/Reactor/` (pump behavior tests)
- `tests/Paramore.Brighter.Core.Tests/MessageDispatch/Proactor/` (async pump behavior tests)

## Consequences

### Positive

- **Fills a gap**: Provides the missing "don't consume" option alongside defer/reject/invalid
- **Consistent**: Follows established exception-as-signal, attribute/handler, and pump catch-block patterns exactly
- **Safe**: Configurable delay prevents tight loops; unacceptable message limit provides an escape hatch
- **Simple**: No new abstractions — extends existing patterns with minimal new types
- **Composable**: `DontAckOnErrorAttribute` composes with retry policies and circuit breakers, just like `RejectMessageOnErrorAttribute`

### Negative

- **Potential for stuck consumers**: A message that always triggers `DontAckAction` with the limit disabled (`<= 0`) will block that consumer forever. This is by design for the feature switch use case but could be surprising in error scenarios.
- **FeatureSwitchAttribute parameter growth**: Adding `dontAck` to the constructor increases parameter count. This is acceptable given the attribute already has 4 parameters and `dontAck` defaults to `false`.
- **Interface expansion**: Adding `Nack`/`NackAsync` to consumer and channel interfaces requires implementation across all transports. Mitigated by the fact that most stream transports are no-ops, and the queue transport implementations are trivial (1-3 lines each).

### Risks and Mitigations

| Risk | Mitigation |
|------|------------|
| Tight-loop CPU burn | `DontAckDelay` with 1s default; logged at Warning level for operator visibility |
| Permanently stuck consumer | Unacceptable message limit acts as safety valve; set to -1 only deliberately |
| Breaking change to FeatureSwitchAttribute constructor | `dontAck` defaults to `false`; existing call sites unaffected |
| Nack not supported by transport | Stream/pub-sub transports implement Nack as no-op; only queue transports perform active unlock |
| Message ping-pong (rapid redelivery after nack) | Pump delay on current consumer prevents tight loop; other consumers apply their own processing/delay |

## Alternatives Considered

### 1. Nack via Channel (transport-level nack)

Call `Channel.Nack(message)` or `Channel.Reject(message, requeue: true)` explicitly.

**Originally rejected because**: Not all transports support nack with requeue. The pump-level approach (just don't ack) works uniformly across all transports.

**Reconsidered and adopted (Amendment §6)**: The concern about non-uniform support is resolved by making Nack a no-op for stream transports. Queue transports benefit significantly from explicit nack (immediate redelivery vs waiting for visibility timeout). The interface is uniform; only the semantics vary by transport type.

### 2. Reuse DeferMessageAction with special configuration

Configure `DeferMessageAction` to requeue to the same position.

**Rejected because**: Defer has requeue semantics (message is acknowledged and re-enqueued). This changes message ordering and involves the transport's requeue mechanism. DontAckAction is fundamentally different — the message is never acknowledged.

### 3. Pump-level configuration instead of exception

Add a pump-level "don't ack on error" flag rather than an exception-based signal.

**Rejected because**: Less granular — applies to all errors rather than specific handlers. The exception-based approach allows per-handler control via attributes, consistent with existing patterns.

### 4. Extend FallbackPolicyHandler

Add a "don't ack" mode to the existing fallback policy.

**Rejected because**: FallbackPolicy has a different purpose (call a fallback method). Mixing concerns would complicate the handler. A dedicated `DontAckOnErrorAttribute` is clearer in intent, following the same reasoning applied in ADR 0037 for `RejectMessageOnErrorAttribute`.

## References

- Requirements: [specs/0020-DontAckAction/requirements.md](../../specs/0020-DontAckAction/requirements.md)
- Related ADR: [0037-reject-message-on-error-handler.md](0037-reject-message-on-error-handler.md) — same pattern for `RejectMessageOnError`
- `DeferMessageAction`: `src/Paramore.Brighter/Actions/DeferMessageAction.cs`
- `RejectMessageAction`: `src/Paramore.Brighter/Actions/RejectMessageAction.cs`
- `RejectMessageOnErrorHandler`: `src/Paramore.Brighter/Reject/Handlers/RejectMessageOnErrorHandler.cs`
- Message pump: `src/Paramore.Brighter.ServiceActivator/Reactor.cs`, `Proactor.cs`
- Feature switch: `src/Paramore.Brighter/FeatureSwitch/`
