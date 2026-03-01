# 42. RocketMQ Dead Letter Queue — Brighter-Managed

Date: 2026-02-20

## Status

Accepted

## Context

**Parent Requirement**: [specs/0014-rocketmq-dead-letter-queue/requirements.md](../../specs/0014-rocketmq-dead-letter-queue/requirements.md)

**Scope**: This ADR addresses the addition of Brighter-managed DLQ support to the RocketMQ transport. It follows the pattern established by Kafka (ADR 0034), SQS (ADR 0038), Redis (ADR 0039), MsSql (ADR 0040), and PostgreSQL (ADR 0041).

The RocketMQ message consumer (`RocketMessageConsumer`) currently handles rejection by delegating to `Requeue()`:

```csharp
public bool Reject(Message message, MessageRejectionReason? reason) => Requeue(message);
```

However, `Requeue()` itself is effectively a no-op — the `ChangeInvisibleDuration` SDK call is commented out pending the next RocketMQ C# SDK version. This means rejected messages simply time out after the invisibility period and reappear on the queue, creating an infinite retry loop for messages that will never succeed.

### Message Lifecycle Model

RocketMQ uses a **visibility timeout** pattern (similar to PostgreSQL and SQS):

- `Receive()` fetches messages from the broker with an `invisibilityTimeout`. The message remains on the broker but is invisible to other consumers during this period.
- `Ack(MessageView)` confirms successful processing — the broker permanently removes the message.
- If not acknowledged before the timeout, the message becomes visible again (implicit requeue).

This means at `Reject()` time, the source message **still exists on the broker**. The DLQ implementation must forward to DLQ and then `Ack()` the source message to prevent re-delivery.

### ReceiptHandle

RocketMQ's receipt handle is the `MessageView` object itself, stored in `message.Header.Bag["ReceiptHandle"]`. This is the same object used by `Ack()` and must be extracted **before** `RefreshMetadata()` modifies the header bag.

### Async Producer Creation

Unlike relational transports (MsSql, PostgreSQL) where producer creation is synchronous, RocketMQ producer creation is **asynchronous** — `Producer.Builder().Build()` establishes a network connection to the broker. This affects the lazy producer pattern:

- DLQ producers cannot use simple `Lazy<T>` with a synchronous factory
- The producer requires the topic name at build time via `SetTopics()`
- The `RocketMessagingGatewayConnection` provides the `ClientConfig` needed for connection

### Forces

- The source message exists on the broker at reject time — it must be explicitly `Ack()`'d after DLQ send
- Current `Reject()` creates an infinite requeue loop — must be replaced, not supplemented
- Producer creation is async (network I/O to broker) — requires async-aware lazy pattern
- Producer needs `ClientConfig` from `RocketMessagingGatewayConnection` and topic at build time
- DLQ is a separate RocketMQ topic — the producer sends to a different topic
- The Kafka, SQS, Redis, MsSql, and PostgreSQL implementations established a consistent pattern
- `RocketMqMessageProducer` already handles all header mapping and message construction
- `RejectAsync()` currently delegates to sync `Reject()` — should be properly async

## Decision

We will add Brighter-managed DLQ support to the RocketMQ transport following the established pattern, with `Ack()` for source message cleanup and async lazy producer creation.

### Roles and Responsibilities

**RocketSubscription** (information holder):
- *Knowing*: the dead letter routing key and invalid message routing key for this subscription
- Implements `IUseBrighterDeadLetterSupport` and `IUseBrighterInvalidMessageSupport`

**RocketMessageConsumerFactory** (coordinator):
- *Deciding*: whether the subscription has DLQ/invalid message support configured
- *Doing*: extracting routing keys from the subscription via interface checks and passing them to the consumer constructor, along with the `RocketMessagingGatewayConnection`

**RocketMessageConsumer** (service provider):
- *Deciding*: which rejection route to take based on `MessageRejectionReason` (per ADR 0036)
- *Doing*: enriching message metadata, producing to the appropriate DLQ/invalid message channel, and acknowledging the source message
- *Knowing*: the DLQ and invalid message routing keys, lazily-created producers for each, and the source message's `MessageView`

### Rejection Routing (per ADR 0036)

The routing decision tree in `Reject`/`RejectAsync`:

```
Reject(message, reason)
├── No producers configured
│   └── log warning, Ack source message, return true
├── DeliveryError
│   ├── _deadLetterProducer exists → send to DLQ, Ack source, return true
│   └── no producer → log warning, Ack source, return true
├── Unacceptable
│   ├── _invalidMessageProducer exists → send to invalid channel, Ack source, return true
│   ├── _deadLetterProducer exists (fallback) → send to DLQ, Ack source, return true
│   └── no producer → log warning, Ack source, return true
```

The source message is always `Ack()`'d in a `finally` block, regardless of whether DLQ routing succeeds. This breaks the infinite requeue loop even when DLQ production fails.

### Async Lazy Producer Creation

Because RocketMQ producer creation involves async network I/O, we use a nullable field with an async factory method rather than `Lazy<T>`:

```
private RocketMqMessageProducer? _deadLetterProducer;

async Task<RocketMqMessageProducer?> GetDeadLetterProducerAsync()
    if _deadLetterProducer != null → return cached
    if _deadLetterRoutingKey == null → return null
    Build Producer via Producer.Builder() (async)
    Create RocketMqMessageProducer with RocketMqPublication { Topic = dlqTopic }
    Cache and return
```

The sync `Reject()` wraps this via `BrighterAsyncContext.Run()`, consistent with the existing RocketMQ codebase pattern (e.g., `RocketMessageConsumerFactory.Create()`).

Message pumps are single-threaded per consumer, so the null-check caching pattern is thread-safe for our use case.

### Source Message Cleanup

```
Reject(message, reason):
  1. Extract MessageView from message.Header.Bag["ReceiptHandle"]
  2. RefreshMetadata(message, reason)  — enrich bag with rejection metadata
  3. DetermineRejectionRoute(reason)   — select DLQ or invalid channel
  4. Get/create lazy producer (async)
  5. Send to rejection channel (if producer available)
  6. Ack source message using MessageView  — ALWAYS, in finally block
```

The `MessageView` must be extracted at step 1 before `RefreshMetadata` adds entries to the bag. Step 6 uses `consumer.Ack(view)` (the same mechanism already used in `AcknowledgeAsync()`).

### Constructor Changes

`RocketMessageConsumer` gains parameters:

```
RocketMessageConsumer(
    SimpleConsumer consumer,
    int bufferSize,
    TimeSpan invisibilityTimeout,
    RocketMessagingGatewayConnection? connection = null,        // NEW
    RoutingKey? deadLetterRoutingKey = null,                     // NEW
    RoutingKey? invalidMessageRoutingKey = null                  // NEW
)
```

The `RocketMessagingGatewayConnection` is needed for lazy producer creation (provides `ClientConfig` and `MaxAttempts`). It is optional to maintain backward compatibility — if null and DLQ routing keys are set, DLQ production will be skipped with a warning.

### Message Metadata Enrichment

Before sending to the DLQ, the message header bag is enriched with:
- `originalTopic` — the source routing key
- `rejectionReason` — `DeliveryError` or `Unacceptable`
- `rejectionTimestamp` — UTC ISO-8601 timestamp of rejection
- `originalMessageType` — the original `MessageType`
- `rejectionMessage` — description text (if provided)

## Consequences

### Positive

- Rejected messages are no longer stuck in an infinite requeue loop
- Users can configure DLQ per subscription, consistent with all other Brighter-managed DLQ transports
- Existing code with no DLQ configured now `Ack()`'s on reject instead of requeue-looping (improvement even without DLQ)
- Lazy producer creation means zero overhead when DLQ is not used
- Properly async `RejectAsync()` instead of delegating to sync `Reject()`

### Negative

- DLQ send and source `Ack()` are not transactional (consistent with ADR 0034's decision to not use the Outbox for error paths)
- If the broker connection fails during DLQ production, the source message is still `Ack()`'d — the message content is logged at error level but may be lost
- Async producer creation adds complexity compared to relational transport DLQ implementations
- The `RocketMessagingGatewayConnection` must be threaded through from the factory to the consumer

### Risks and Mitigations

- **Risk**: DLQ producer creation fails (broker unreachable), then `Ack()` also fails (source message reappears after timeout) → **Mitigation**: The finally-block `Ack()` ensures best-effort cleanup; if both fail, visibility timeout re-exposure allows retry, which is actually a safety net
- **Risk**: DLQ topic accumulates messages indefinitely → **Mitigation**: Users are responsible for consuming or purging DLQ topics (same as all other transports)
- **Risk**: Multiple rejections trigger concurrent async producer creation → **Mitigation**: Message pumps are single-threaded per consumer; the null-check caching pattern is safe

## Alternatives Considered

### 1. Use Lazy<Task<Producer>> for Thread-Safe Async Init

Wrap producer creation in `Lazy<Task<RocketMqMessageProducer>>` for thread-safe lazy initialization. This adds complexity (async lazy patterns in C# require care to avoid deadlocks) without benefit since message pumps are single-threaded.

### 2. Create Producer Eagerly in Factory

Build the DLQ producer during `CreateConsumerAsync()` and pass it to the consumer. This avoids lazy creation complexity but adds broker connection overhead even when DLQ is never used, and connects to a topic that may not exist.

### 3. Keep Requeue Behaviour

Leave `Reject()` delegating to `Requeue()`. This perpetuates the infinite loop for messages that will never succeed and is the specific problem this spec aims to solve.

## References

- Requirements: [specs/0014-rocketmq-dead-letter-queue/requirements.md](../../specs/0014-rocketmq-dead-letter-queue/requirements.md)
- ADR 0034: [Provide DLQ Where Missing](0034-provide-dlq-where-missing.md)
- ADR 0036: [Message Rejection Routing Strategy](0036-message-rejection-routing-strategy.md)
- ADR 0038: [AWS SQS DLQ Direct Send](0038-aws-sqs-dlq-direct-send.md)
- ADR 0039: [Redis DLQ Brighter-Managed](0039-redis-dlq-brighter-managed.md)
- ADR 0040: [MsSql DLQ Brighter-Managed](0040-mssql-dlq-brighter-managed.md)
- ADR 0041: [PostgreSQL DLQ Brighter-Managed](0041-postgres-dlq-brighter-managed.md)
