# 37. Universal Scheduler Delay Support

Date: 2026-01-28

## Status

Accepted

## Context

**Parent Requirement**: [specs/0002-universal_scheduler_delay/requirements.md](../../specs/0002-universal_scheduler_delay/requirements.md)

**Scope**: This ADR addresses how to make delayed message delivery consistent across all Brighter transports, with particular focus on the in-memory transport implementation.

### Problem

When a handler throws a `DeferMessageException` its intent is that we requeue the message with a delay to allow for a transient fault to clear. The message pump - `Proactor` or `Reactor` will call `RequeueWithDelay` or `RequeueWithDelayAsync` as appropriate on the `IAmAMessageConsumerSync` or `IAmAMessageConsumerAsync`.

As Brighter is a single-threaded message pump it is important not to block the pump with a delay, so we need to hand off the responsibility for that delay, either to the transport (for example RabbitMQ will handle requeue with delay for us) or a scheduler that will raise the message after that interval.

Prior to V10 we had no scheduler, and so could not support `DeferMessageAction` on transports that did not support requeue with delay natively (for example Kafka).

Brighter V10 introduced message scheduling support via:
- `IAmAMessageSchedulerSync` / `IAmAMessageSchedulerAsync` interfaces
- `InMemoryScheduler` implementation
- `MessageSchedulerFactory` configuration in `IAmProducersConfiguration`

We added support for using this scheduler to the `IAmAMessageProducerSync` and `IAmAMessageProducerAsync` so that when we call `SendWithDelay` or `SendWithDelayAsync` we use a scheduler to delay writing the message to the transport.

For a consumer that does not natively support requeue with delay, we can lean on the producer to requeue the message. Note that we **must** use the producer because that is the only way for us to write to the queue or stream, in this circumstance. (Concerns about a consumer knowing how to produce are not valid here; knowing how to requeue implies that you must know how to produce).

To produce, the consumer must use its native producer.

However, the current implementations are inconsistent:

| Transport | Delay Mechanism | Has Native Support | Requires Producer                          |
|-----------|----------------|--------------------|--------------------------------------------|
| RabbitMQ | Native DLX/TTL  | Yes                | Fallback if native not configured          |
| Azure Service Bus | Native | Yes                | No                                         |
| AWS SQS | Native (15min max) + Scheduler | Yes                | No                                         |
| Kafka | None | No                 | Yes, should use KafkaMessageProducer       |
| Redis | None | No                 | Yes, should use RedisMessageProducer       |
| **InMemoryConsumer** | Direct `TimeProvider.CreateTimer()` | **No**             | Yes should swap to InMemoryMessageProducer |

In addition, the `InMemoryProducer` uses a `Timer` to queue with a delay and should use the `InMemoryScheduler` instead.

This inconsistency creates several problems:
1. Users cannot rely on uniform delay behavior across transports
2. Testing with in-memory transport doesn't reflect scheduler behavior
3. The `InMemoryProducer` has a `Scheduler` property but never uses it
4. `InMemoryConsumer.Requeue()` bypasses the producer entirely for delays

### Forces

- **Testability**: In-memory transport is primarily used for testing; it should mirror production behavior
- **Consistency**: Users expect `SendWithDelay` to work on transports without native support
- **Performance**: Scheduler overhead for immediate sends must be negligible

### Constraints

- Must use existing `IAmAMessageScheduler` interface hierarchy
- Must not modify `IAmACommandProcessor` interface
- Must maintain compatibility with all supported .NET versions

## Decision

### Configuring Schedulers

- The scheduler is injectable via `MessageSchedulerFactory` in `IAmProducersConfiguration`
- It MUST default to `InMemorySchedulerFactory`
- All message producers currently have an optional `IAmAMessageScheduler` property. It MUST be set from the `MessageSchedulerFactory` on the `IAmProducersConfiguration`

### In Memory Scheduler

- As a consequence, if no scheduler is explicitly configured, `InMemoryScheduler` SHOULD be used as the default - `InMemoryMessageProducer` MUST use the configured scheduler for `SendWithDelay`/`SendWithDelayAsync`
- MUST replace the current direct `TimeProvider.CreateTimer()` implementation
- MUST throw a `ConfigurationException` if a `SendWithDelay` is called with a `timeOut` that is above zero but no Scheduler has been set. Do not fall back to a timer as a single timer will be overwritten by subsequent calls, leading to erratic behavior. 
- The `Requeue(Message message, TimeSpan? timeOut)` method SHOULD delegate to the `InMemoryMessageProducer` `SendWithDelay` when `timeOut` is specified
- The `RequeueAsync(Message message, TimeSpan? timeOut)` method SHOULD delegate to the `InMemoryMessageProducer`
  `SendWithDelayAsync` when `timeOut` is specified
- SHOULD maintain backward compatibility with existing behavior, when a scheduler is set, but should not delay if `timeout` is `TimeSpan.Zero`
- The `InMemoryMessageConsumer` needs to create the `InMemoryMessageProducer` lazily, in case delay is never used.
  It should use the topic for the message to be sent as the topic for the producer that it creates.

### Other Transports

- Within each transport - file pattern: Paramore.Messaging.Gateway.*
  - Each producer MUST use the configured scheduler for `SendWithDelay`/`SendWithDelayAsync`
  - Each consumer's `Requeue`/`RequeueAsync` MUST separate immediate and delayed paths:
    - **Delayed requeue** (`delay > TimeSpan.Zero`): The consumer lazily creates an instance of its native producer (i.e. `KafkaMessageConsumer` creates a `KafkaMessageProducer`) on first delayed requeue. The producer's `Scheduler` property is set so that `SendWithDelay`/`SendWithDelayAsync` delegates to the scheduler.
      - If using a producer, we create lazily in case delay is never used
      - Use the topic from the message when creating the producer. Use sensible defaults over forcing configuration
    - **Immediate requeue** (`delay` is null or `TimeSpan.Zero`): Send immediately, preferring native transport mechanisms over the scheduler-aware producer. The producer SHOULD NOT be created solely for an immediate requeue.
  - Transports with native delay support MUST use native delay over producer (configurable)

#### Immediate Requeue by Transport Category

Transports fall into two categories for immediate requeue:

**Category 1 — Transports with native immediate requeue**: These transports can requeue without creating a producer, using native operations directly on the underlying transport connection.

| Transport | Immediate Requeue Mechanism |
|-----------|---------------------------|
| Redis | `AddItemToList` on the Redis queue |
| MSSQL | `_sqlMessageQueue.Send` direct SQL insert |
| RabbitMQ | `RmqMessagePublisher.RequeueMessage` on the existing channel |
| InMemory | Direct `_bus.Enqueue` |

**Category 2 — Transports where requeue requires producing**: Kafka streams are immutable and MQTT is pub/sub — neither has a native "put the message back" operation. Immediate requeue MUST produce a new copy of the message to the topic, which requires a producer. For these transports:
- The producer is still lazily created via `EnsureRequeueProducer`
- Immediate requeue calls `Send`/`SendAsync` directly (bypassing the scheduler), followed by `Flush` where required (Kafka)
- Delayed requeue calls `SendWithDelay`/`SendWithDelayAsync` which delegates to the scheduler

| Transport | Immediate Requeue Mechanism | Notes |
|-----------|---------------------------|-------|
| Kafka | `_requeueProducer.Send` + `Flush` | Must produce new message; original offset is acknowledged |
| MQTT | `_requeueProducer.Send` | Must publish back to topic |

The code structure for Category 2 transports mirrors Category 1 — the `if (delay > TimeSpan.Zero)` guard is present and the two paths are explicit — even though both paths use the lazily-created producer.

### Notes

Why don't we inject the producer into the consumer instead of lazily creating? The answer is that we will generally not have created a producer for that topic in a worker that acts as a consumer. It's job is to send, not to produce. We will only need to produce to that topic if we have an error case, where we wish to delay to clear a transient error. By delaying using the queue we free up the pump thread, and avail ourselves of the scheduler instead.

There are no real concerns that our consumer can now produce—the consumer's role may include message rejection, which can only be actioned by either native support (usually unlocking after a delay) or use of a scheduler to delay a repost of the message to the queue.

### Handled Count

A repost always updates the handled count. Once the handled count exceeds a user-defined threshold, policy can force an automatic rejection. If there is a DLQ configured, rejection typically forwards the message to the DLQ. This prevents a poison pill message being eternally retried.


### Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           Message Flow with Delay                            │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌──────────────────┐    Requeue(msg, delay)    ┌───────────────────────┐   │
│  │                  │ ────────────────────────► │                       │   │
│  │ InMemoryConsumer │                           │  InMemoryProducer     │   │
│  │                  │ ◄──────────────────────── │  (lazily created)     │   │
│  └──────────────────┘                           └───────────┬───────────┘   │
│                                                              │               │
│                                                              │               │
│                                             SendWithDelay(msg, delay)       │
│                                                              │               │
│                                                              ▼               │
│                               ┌──────────────────────────────────────────┐  │
│                               │          Decision Point                   │  │
│                               │                                          │  │
│                               │  if (delay == TimeSpan.Zero)             │  │
│                               │     → Send immediately to bus            │  │
│                               │  else if (Scheduler != null)             │  │
│                               │     → Scheduler.Schedule(msg, delay)     │  │
│                               │  else                                    │  │
│                               │     → throw new ConfigurationException() │  │
│                               └──────────────────────────────────────────┘  │
│                                                              │               │
│                                                              ▼               │
│                               ┌──────────────────────────────────────────┐  │
│                               │        IAmAMessageScheduler              │  │
│                               │                                          │  │
│                               │  Schedule(message, delay)                │  │
│                               │    → Creates timer via TimeProvider      │  │
│                               │    → On timer fire: sends via            │  │
│                               │      CommandProcessor → Producer → Bus   │  │
│                               └──────────────────────────────────────────┘  │
│                                                              │               │
│                                                              ▼               │
│                               ┌──────────────────────────────────────────┐  │
│                               │           InternalBus                    │  │
│                               │                                          │  │
│                               │  Message delivered after delay           │  │
│                               └──────────────────────────────────────────┘  │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Key Components

#### 1. InMemoryMessageProducer (Modified)

**Responsibilities:**
- **Knowing**: The configured scheduler, the bus to publish to
- **Doing**: Sending messages immediately or delegating to scheduler for delays
- **Deciding**: Whether to use scheduler based on delay value and scheduler availability

**Changes:**
- `SendWithDelay()` / `SendWithDelayAsync()` will use configured `Scheduler` when delay > 0
- Fall back to direct timer only if no scheduler is configured (backward compatibility)
- Immediate sends (delay = 0) bypass scheduler entirely

```
SendWithDelay(message, delay):
    if delay == TimeSpan.Zero:
        Send(message)  // immediate, no scheduler
    else if Scheduler is IAmAMessageSchedulerAsync:
        await Scheduler.ScheduleAsync(message, delay)
    else if Scheduler is IAmAMessageSchedulerSync:
        Scheduler.Schedule(message, delay)
    else:
        // Inform the user a scheduler was not configuree
        throw new ConfigurationException($"Cannot requeue {message.Id} with delay; no scheduler is configured. Configure a scheduler via MessageSchedulerFactory in IAmProducersConfiguration."); 
```

#### 2. InMemoryMessageConsumer (Modified)

**Responsibilities:**
- **Knowing**: The bus to consume from, how to create a producer
- **Doing**: Reading messages, acknowledging, rejecting, requeuing
- **Deciding**: Whether to use producer for delayed requeue

**Changes:**
- `Requeue()` / `RequeueAsync()` with non-zero timeout will delegate to `InMemoryMessageProducer.SendWithDelay()`
- Producer created lazily on first delayed requeue
- Immediate requeue (timeout = 0) continues to use direct bus enqueue

**New Dependencies:**
- Needs access to `IAmABus` (already has via `InternalBus`)
- Needs `TimeProvider` (already has)
- Needs to create `InMemoryMessageProducer` lazily
- Producer uses message's topic, not consumer's subscription topic

```
Requeue(message, timeout):
    if timeout == null or timeout <= TimeSpan.Zero:
        RequeueNoDelay(message)  // existing behavior
    else:
        EnsureProducer()
        _producer.SendWithDelay(message, timeout)
```

#### 3. IAmAMessageScheduler (Unchanged)

The existing scheduler interfaces remain unchanged:
- `IAmAMessageScheduler` - marker interface
- `IAmAMessageSchedulerSync` - `Schedule(message, delay)`, `Cancel(id)`
- `IAmAMessageSchedulerAsync` - `ScheduleAsync(message, delay)`, `CancelAsync(id)`

#### 4. InMemoryScheduler (Race Condition Requires Fixing)

The existing `InMemoryScheduler` implementation is sufficient:
- Uses `TimeProvider` for testable timers
- Fires `FireSchedulerMessage` command through `IAmACommandProcessor`
- Thread-safe via `ConcurrentDictionary<string, ITimer>`

However, there are thread safety concerns in InMemoryScheduler

Location: InMemoryScheduler.cs:56

Issue: The InMemoryScheduler uses a static ConcurrentDictionary<string, ITimer>:

private static readonly ConcurrentDictionary<string, ITimer> s_timers = new();

Problems:

    Static state shared across all scheduler instances is risky
    Timer disposal race conditions in lines 67-75 and 186-194
    The check-then-act pattern (TryGetValue + dispose + assign) isn't atomic

Code Pattern:

```csharp
if (s_timers.TryGetValue(id, out var timer))
{
    if (onConflict == OnSchedulerConflict.Throw)
{
    throw new InvalidOperationException($"scheduler with '{id}' id already exists");
}
    timer.Dispose();  // ⚠️ Race condition: another thread could be accessing this timer
}
s_timers[id] = timeProvider.CreateTimer(...);  // ⚠️ Not atomic with above
```

Recommendation: We should use AddOrUpdate or similar atomic operations to ensure thread safety.

#### 5. Paramore.Brighter.MessagingGateway.*

Within each transport that we support, there is a consumer that implements `IAmAMessageConsumerSync` and `IAmAMessageConsumerAsync`. This consumer must either have native support for delayed requeue, or use a lazily created producer.

The following consumers require producer support

| Transport                                | Consumer               |  
|------------------------------------------|------------------------|
| Paramore.Brighter.MessagingGateway.Kafka | `KafkaMessageConsumer` |
| Paramore.Brighter.MessagingGateway.MQTT | `MQTTMessageConsumer` |
| Paramore.Brighter.MessagingGateway.MsSql | `MsSqlMessageConsumer `|
| Paramore.Brighter.MessagingGateway.Postgres | `PostgresMessageConsumer`|
| Paramore.Brighter.MessagingGateway.Redis | `RedisMessageConsumer` |

In addition, Paramore.Brighter.MessaingGateway.RMQ.Async `RmqMessageConsumer` in `Reqeueue` we should replace the code using a `Task.Delay` with usage of the `RmqProducer` and its `SendWithDelayAsync` method.

```csharp
if (timeout > TimeSpan.Zero)
{
    await Task.Delay(timeout.Value, cancellationToken);
}

```

Similarly, in Paramore.Brighter.MessagingGateway.RMQ.Syc `RmqMessageConsumer` in `Requeue` we should replace using a `Task.Delay` with with usage of the `RmqProducer` and its `SendWithDelay` method. 

```csharp
else
{
    if (timeout > TimeSpan.Zero) Task.Delay(timeout.Value).Wait();
    rmqMessagePublisher.RequeueMessage(message, _queueName, TimeSpan.Zero);
}

```

### Technology Choices

| Choice | Rationale |
|--------|-----------|
| Lazy producer creation | Avoids overhead when delay is never used |
| Producer per consumer | Simpler than sharing; low overhead for in-memory |
| Use message topic | Respects routing; avoids configuration burden |
| Preserve direct timer fallback | Backward compatibility for tests without scheduler |
| Separate immediate/delayed paths | Immediate requeue uses native mechanism or `Send` directly; delayed requeue uses `SendWithDelay` with scheduler. Avoids scheduler overhead for zero-delay requeue and keeps code paths explicit. |

### Implementation Approach

**Phase 1: InMemoryMessageProducer**
1. Modify `SendWithDelay()` to check for and use scheduler
2. Modify `SendWithDelayAsync()` similarly
3. Add tests verifying scheduler integration

**Phase 2: InMemoryMessageConsumer**
1. Add lazy `InMemoryMessageProducer` field
2. Modify `Requeue()` to delegate delayed requeues to producer
3. Modify `RequeueAsync()` similarly
4. Dispose producer in `Dispose()` / `DisposeAsync()`
5. Add tests verifying producer delegation

**Phase 3: Integration Tests**
1. Test end-to-end delay via scheduler with `FakeTimeProvider`
2. Test backward compatibility without explicit scheduler
3. Test consumer requeue → producer → scheduler → bus flow

## Consequences

### Positive

- **Consistent behavior**: In-memory transport behaves like production transports
- **Better testability**: Tests can verify scheduler behavior using `TimeProvider`
- **Flexibility**: Users can swap schedulers without changing transport code
- **Follows established pattern**: Mirrors `RmqMessageProducer` implementation

### Negative

- **Slight complexity increase**: Consumer now has optional dependency on producer
- **Indirect message path**: Delayed requeues go through scheduler → CommandProcessor → producer → bus instead of direct to bus

### Risks and Mitigations

| Risk | Mitigation |
|------|------------|
| Breaking existing tests | Preserve direct timer fallback when no scheduler configured |
| Performance regression | Bypass scheduler for immediate sends (delay = 0) |
| Test isolation issues | Ensure `InMemoryScheduler` timers are properly disposed; consider instance-based timer storage |
| Lazy producer not disposed | Add producer disposal in consumer's `Dispose()` methods |

## Alternatives Considered

### 1. Add Scheduler to InMemoryMessageConsumer Directly

Have the consumer use scheduler directly instead of delegating to producer.

**Rejected because:**
- Duplicates the delay logic already in producer
- Breaks the responsibility pattern (consumers consume, producers produce)
- Would need to inject scheduler factory into consumer

### 2. Make InMemoryScheduler the Only Delay Mechanism

Remove all direct timer usage, require scheduler for any delay.

**Rejected because:**
- Breaking change for existing code
- Adds configuration burden for simple use cases
- Scheduler requires `IAmACommandProcessor` which consumers don't have

### 3. Inject Producer into Consumer via Constructor

Pass producer as constructor dependency instead of lazy creation.

**Rejected because:**
- Adds mandatory dependency even when delay is never used
- Complicates consumer construction
- Topic mismatch: consumer subscribes to one topic but may requeue to different topic

## References

- Requirements: [specs/0002-universal_scheduler_delay/requirements.md](../../specs/0002-universal_scheduler_delay/requirements.md)
- Existing RMQ implementation: `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageProducer.cs`
- Scheduler interfaces: `src/Paramore.Brighter/IAmAMessageScheduler*.cs`
- InMemoryScheduler: `src/Paramore.Brighter/InMemoryScheduler.cs`
