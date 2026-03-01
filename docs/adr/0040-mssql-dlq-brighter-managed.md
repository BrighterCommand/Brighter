# 40. MsSql Dead Letter Queue — Brighter-Managed

Date: 2026-02-14

## Status

Accepted

## Context

**Parent Requirement**: [specs/0012-mssql-dead-letter-queue/requirements.md](../../specs/0012-mssql-dead-letter-queue/requirements.md)

**Scope**: This ADR addresses the addition of Brighter-managed DLQ support to the MsSql transport. It follows the pattern established by Kafka (ADR 0034), SQS (ADR 0038), and Redis (ADR 0039).

The MsSql message consumer (`MsSqlMessageConsumer`) currently has `Reject()` and `RejectAsync()` marked as not implemented. They log a warning ("NOT IMPLEMENTED") and return `false`, silently discarding the message. SQL Server has no native dead letter queue mechanism for its queue table — all messages are stored in a single table (`QueueStoreTable`) differentiated by a `Topic` column.

MsSql uses an atomic read-and-delete pattern: `Receive()` issues a CTE with `DELETE...OUTPUT` using `READPAST` row locks. By the time `Reject()` is called, the message is already removed from the source queue. This means there is no separate "acknowledge" or "delete" step needed — the message simply needs to be forwarded to the DLQ topic.

This is the same scenario as Redis (ADR 0039): a transport lacking native DLQ support where Brighter must manage the DLQ itself.

### Forces

- Messages are atomically deleted on read — there is no visibility timeout or redelivery mechanism
- The message is already gone from the source queue by the time `Reject()` is called
- The existing `Requeue()` method already demonstrates sending messages to the same queue table with `_sqlMessageQueue.Send(message, topic)` — proving the mechanism works
- The Kafka, SQS, and Redis implementations established a consistent pattern for Brighter-managed DLQ
- All MsSql queue topics share the same database table, so DLQ "queue creation" is implicit (just insert with a different topic value)

## Decision

We will add Brighter-managed DLQ support to the MsSql transport following the established pattern from Kafka, SQS, and Redis.

### Roles and Responsibilities

**MsSqlSubscription** (information holder):
- *Knowing*: the dead letter routing key and invalid message routing key for this subscription
- Implements `IUseBrighterDeadLetterSupport` and `IUseBrighterInvalidMessageSupport`

**MsSqlMessageConsumerFactory** (coordinator):
- *Deciding*: whether the subscription has DLQ/invalid message support configured
- *Doing*: extracting routing keys from the subscription via interface checks and passing them to the consumer constructor

**MsSqlMessageConsumer** (service provider):
- *Deciding*: which rejection route to take based on `MessageRejectionReason` (per ADR 0036)
- *Doing*: enriching message metadata and producing to the appropriate DLQ/invalid message channel
- *Knowing*: the DLQ and invalid message routing keys, and lazily-created producers for each

### Rejection Routing (per ADR 0036)

The routing decision tree in `Reject`/`RejectAsync`:

```
Reject(message, reason)
├── DeliveryError
│   ├── _deadLetterProducer exists → send to DLQ, return true
│   └── no producer → log warning, return true
├── Unacceptable
│   ├── _invalidMessageProducer exists → send to invalid channel, return true
│   ├── _deadLetterProducer exists (fallback) → send to DLQ, return true
│   └── no producer → log warning, return true
```

Note: Unlike Redis, there is no `_inflight` dictionary to manage. The message was already deleted from the source queue during `Receive()`. `Reject()` only needs to forward the message and return `true`.

### Producer Creation

DLQ producers are created lazily on first rejection. The consumer creates an `MsSqlMessageProducer` directly, using the same `RelationalDatabaseConfiguration` as the consumer (which requires storing it as a field). This ensures the producer writes to the same database and queue table, but with the DLQ/invalid message routing key as the topic.

Since all MsSql queue messages share one table differentiated by `Topic`, no table creation or `MakeChannels` logic is needed for the DLQ producer. A message sent with topic `my-dlq` simply appears as a row with `Topic = 'my-dlq'` in the same `QueueStoreTable`.

### Why Use a Lazy Producer (Not Direct Queue Access)

The consumer already has `_sqlMessageQueue` which can send to any topic. We could call `_sqlMessageQueue.Send(message, dlqTopic)` directly, similar to how `Requeue()` works. However, we use a lazy `MsSqlMessageProducer` instead because:

1. **Consistency**: Matches the Redis, SQS, and Kafka DLQ patterns
2. **Observability**: The producer writes OTel `BrighterTracer` events for DLQ sends
3. **Encapsulation**: The consumer delegates production responsibility to the producer role

### Message Metadata Enrichment

Before sending to the DLQ, the message header bag is enriched with (using `HeaderNames` constants):
- `OriginalTopic` — the source routing key
- `RejectionReason` — `DeliveryError` or `Unacceptable`
- `RejectionTimestamp` — UTC timestamp of rejection
- `OriginalMessageType` — the original `MessageType`

### Constructor Changes

`MsSqlMessageConsumer` gains optional parameters:

```
MsSqlMessageConsumer(
    RelationalDatabaseConfiguration msSqlConfiguration,
    string topic,
    RelationalDbConnectionProvider connectionProvider,
    RoutingKey? deadLetterRoutingKey = null,        // NEW
    RoutingKey? invalidMessageRoutingKey = null      // NEW
)
```

The `msSqlConfiguration` must also be stored as a field (currently only passed through to `MsSqlMessageQueue`) so it can be used to create lazy DLQ producers.

## Consequences

### Positive

- Rejected messages are no longer silently lost
- Users can configure DLQ per subscription, consistent with all other Brighter-managed DLQ transports
- Existing code with no DLQ configured continues to work unchanged (backward compatible)
- Lazy producer creation means zero overhead when DLQ is not used
- No additional database tables or schema changes — DLQ messages use the same queue table with a different topic
- `Reject()` now returns `true`, correctly indicating the message was handled

### Negative

- DLQ messages are not transactionally guaranteed with the original delete (consistent with ADR 0034's decision to not use the Outbox for error paths)
- If the database connection fails during DLQ production, the message may be lost (mitigated by logging the full message content at error level)

### Risks and Mitigations

- **Risk**: DLQ production fails after original message already deleted → **Mitigation**: Log the error at warning level; this is no worse than the current behaviour where `Reject()` discards the message entirely
- **Risk**: DLQ topic accumulates messages indefinitely → **Mitigation**: Users are responsible for consuming or purging DLQ topics (same as all other transports)

## Alternatives Considered

### 1. Use Direct Queue Access Instead of Lazy Producer

Call `_sqlMessageQueue.Send(message, dlqTopic)` directly in `Reject()`, similar to `Requeue()`. This is simpler but loses OTel tracing for DLQ sends and breaks the pattern established by other transports.

### 2. Use a Separate Table for DLQ Messages

Create a dedicated DLQ table with additional columns for rejection metadata. This would provide better query isolation but adds schema management complexity and breaks the simplicity of the single-table-with-topics model that MsSql queues use.

### 3. Do Nothing

Rejected messages continue to be lost. This is unacceptable for users who need observability into message failures.

## References

- Requirements: [specs/0012-mssql-dead-letter-queue/requirements.md](../../specs/0012-mssql-dead-letter-queue/requirements.md)
- ADR 0034: [Provide DLQ Where Missing](0034-provide-dlq-where-missing.md)
- ADR 0036: [Message Rejection Routing Strategy](0036-message-rejection-routing-strategy.md)
- ADR 0038: [AWS SQS DLQ Direct Send](0038-aws-sqs-dlq-direct-send.md)
- ADR 0039: [Redis DLQ Brighter-Managed](0039-redis-dlq-brighter-managed.md)
- Kafka DLQ implementation: [Spec 0001](../../specs/0001-kafka-dead-letter-queue/)
