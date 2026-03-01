# 41. PostgreSQL Dead Letter Queue — Brighter-Managed

Date: 2026-02-20

## Status

Accepted

## Context

**Parent Requirement**: [specs/0013-postgres-dead-letter-queue/requirements.md](../../specs/0013-postgres-dead-letter-queue/requirements.md)

**Scope**: This ADR addresses the addition of Brighter-managed DLQ support to the PostgreSQL transport. It follows the pattern established by Kafka (ADR 0034), SQS (ADR 0038), Redis (ADR 0039), and MsSql (ADR 0040).

The PostgreSQL message consumer (`PostgresMessageConsumer`) currently handles rejection by deleting the message from the queue table. There is no dead letter queue — rejected messages are permanently lost:

```csharp
public bool Reject(Message message, MessageRejectionReason? reason = null)
{
    // Current: just DELETE from table using ReceiptHandle
    command.CommandText = $"DELETE FROM ... WHERE \"id\" = $1";
}
```

PostgreSQL has no native dead letter queue mechanism for its queue table. All messages are stored in a single table (configured via `RelationalDatabaseConfiguration`) differentiated by a `queue` column.

### Visibility Timeout Model

PostgreSQL uses a **visibility timeout** pattern, which differs from MsSql's atomic read-and-delete:

- `Receive()` issues an `UPDATE` that sets `visible_timeout` to a future timestamp, hiding the message from other consumers. The message row **remains in the table**.
- `Acknowledge()` issues a `DELETE` to remove the message after successful processing.
- `Reject()` currently issues a `DELETE` — the same as acknowledge but without routing.

This means that at `Reject()` time, the message row **still exists** in the source table. The DLQ implementation must:
1. Send the message to the DLQ/invalid message channel
2. **Then delete the original** from the source queue (preserving current delete behaviour)

This is distinct from MsSql/Redis where the message is already gone by the time `Reject()` runs.

### Forces

- The message still exists in the source table at reject time — it must be explicitly deleted after DLQ send
- The existing `Reject()` already demonstrates the delete mechanism (using `ReceiptHandle` from the message bag)
- The `ReceiptHandle` is the database row `id` (int64), set during `Receive()`
- All PostgreSQL queue messages share one table differentiated by the `queue` column, so DLQ "creation" is implicit (insert with a different queue value)
- The Kafka, SQS, Redis, and MsSql implementations established a consistent pattern for Brighter-managed DLQ
- `PostgresMessageProducer` already handles inserting messages with a given topic into the queue table

## Decision

We will add Brighter-managed DLQ support to the PostgreSQL transport following the established pattern, with an additional source-message delete step after DLQ forwarding.

### Roles and Responsibilities

**PostgresSubscription** (information holder):
- *Knowing*: the dead letter routing key and invalid message routing key for this subscription
- Implements `IUseBrighterDeadLetterSupport` and `IUseBrighterInvalidMessageSupport`

**PostgresConsumerFactory** (coordinator):
- *Deciding*: whether the subscription has DLQ/invalid message support configured
- *Doing*: extracting routing keys from the subscription via interface checks and passing them to the consumer constructor

**PostgresMessageConsumer** (service provider):
- *Deciding*: which rejection route to take based on `MessageRejectionReason` (per ADR 0036)
- *Doing*: enriching message metadata, producing to the appropriate DLQ/invalid message channel, and deleting the source message
- *Knowing*: the DLQ and invalid message routing keys, lazily-created producers for each, and the source message's receipt handle

### Rejection Routing (per ADR 0036)

The routing decision tree in `Reject`/`RejectAsync`:

```
Reject(message, reason)
├── No producers configured
│   └── log warning, delete source message, return true
├── DeliveryError
│   ├── _deadLetterProducer exists → send to DLQ, delete source, return true
│   └── no producer → log warning, delete source, return true
├── Unacceptable
│   ├── _invalidMessageProducer exists → send to invalid channel, delete source, return true
│   ├── _deadLetterProducer exists (fallback) → send to DLQ, delete source, return true
│   └── no producer → log warning, delete source, return true
```

Note: Unlike MsSql/Redis (atomic delete on receive), PostgreSQL must **always delete the source message** in `Reject()` regardless of whether DLQ routing succeeds. The `ReceiptHandle` must be extracted from `message.Header.Bag` **before** `RefreshMetadata()` modifies the bag.

### Source Message Deletion

The current `Reject()` deletes the message using the `ReceiptHandle` stored in the message header bag. This behaviour must be preserved:

```
Reject(message, reason):
  1. Extract ReceiptHandle from message.Header.Bag
  2. RefreshMetadata(message, reason)  — enrich bag with rejection metadata
  3. DetermineRejectionRoute(reason)   — select DLQ or invalid channel
  4. Send to rejection channel (if producer available)
  5. Delete source message using ReceiptHandle  — ALWAYS, even if DLQ send fails
```

The `ReceiptHandle` must be extracted early (step 1) because `RefreshMetadata` adds entries to the bag but should not remove the receipt handle. The source delete (step 5) executes in a `finally` block to ensure the message is cleaned up regardless of DLQ send outcome.

### Producer Creation

DLQ producers are created lazily on first rejection. The consumer creates a `PostgresMessageProducer` directly, using the stored `RelationalDatabaseConfiguration`. This ensures the producer writes to the same database and queue table, but with the DLQ/invalid message routing key as the queue value.

Since all PostgreSQL queue messages share one table differentiated by `queue`, no table creation logic is needed for the DLQ producer. A message sent with topic `my-dlq` simply appears as a row with `queue = 'my-dlq'` in the same table.

The consumer must store the `RelationalDatabaseConfiguration` as a field (it currently accesses configuration only through the subscription) so it can create lazy DLQ producers.

### Why Use a Lazy Producer (Not Direct SQL)

The consumer could execute direct INSERT SQL to the queue table. We use a lazy `PostgresMessageProducer` instead because:

1. **Consistency**: Matches the Redis, SQS, MsSql, and Kafka DLQ patterns
2. **Observability**: The producer writes OTel `BrighterTracer` events for DLQ sends
3. **Encapsulation**: The consumer delegates production responsibility to the producer role

### Message Metadata Enrichment

Before sending to the DLQ, the message header bag is enriched with:
- `originalTopic` — the source routing key
- `rejectionReason` — `DeliveryError` or `Unacceptable`
- `rejectionTimestamp` — UTC ISO-8601 timestamp of rejection
- `originalMessageType` — the original `MessageType`
- `rejectionMessage` — description text (if provided)

### Constructor Changes

`PostgresMessageConsumer` gains optional parameters:

```
PostgresMessageConsumer(
    RelationalDatabaseConfiguration configuration,
    PostgresSubscription subscription,
    RoutingKey? deadLetterRoutingKey = null,        // NEW
    RoutingKey? invalidMessageRoutingKey = null      // NEW
)
```

The `RelationalDatabaseConfiguration` is already passed to the constructor. It must also be stored as a field for lazy producer creation.

## Consequences

### Positive

- Rejected messages are no longer silently lost
- Users can configure DLQ per subscription, consistent with all other Brighter-managed DLQ transports
- Existing code with no DLQ configured continues to work unchanged (backward compatible)
- Lazy producer creation means zero overhead when DLQ is not used
- No additional database tables or schema changes — DLQ messages use the same queue table with a different queue value
- Source message cleanup is guaranteed regardless of DLQ send outcome

### Negative

- DLQ send and source delete are not transactional (consistent with ADR 0034's decision to not use the Outbox for error paths)
- If the database connection fails during DLQ production, the source message is still deleted — the message content is logged at error level but may be lost from the queue
- Slightly more complex than MsSql because of the two-step (forward + delete) pattern

### Risks and Mitigations

- **Risk**: DLQ production fails, then source delete also fails (message becomes visible again after timeout and may be reprocessed) → **Mitigation**: The finally-block delete ensures best-effort cleanup; if both fail, visibility timeout re-exposure is actually a safety net allowing retry
- **Risk**: DLQ topic accumulates messages indefinitely → **Mitigation**: Users are responsible for consuming or purging DLQ topics (same as all other transports)

## Alternatives Considered

### 1. Transactional DLQ Send + Source Delete

Wrap the DLQ INSERT and source DELETE in a single PostgreSQL transaction. This guarantees atomicity but couples the DLQ producer to the consumer's connection lifecycle and breaks the lazy-producer pattern used by all other transports.

### 2. Use Direct SQL Instead of Lazy Producer

Execute `INSERT INTO queue_table` directly in `Reject()` without creating a producer. This is simpler but loses OTel tracing for DLQ sends and breaks the pattern established by other transports.

### 3. Do Nothing

Rejected messages continue to be deleted and lost. This is unacceptable for users who need observability into message failures.

## References

- Requirements: [specs/0013-postgres-dead-letter-queue/requirements.md](../../specs/0013-postgres-dead-letter-queue/requirements.md)
- ADR 0034: [Provide DLQ Where Missing](0034-provide-dlq-where-missing.md)
- ADR 0036: [Message Rejection Routing Strategy](0036-message-rejection-routing-strategy.md)
- ADR 0038: [AWS SQS DLQ Direct Send](0038-aws-sqs-dlq-direct-send.md)
- ADR 0039: [Redis DLQ Brighter-Managed](0039-redis-dlq-brighter-managed.md)
- ADR 0040: [MsSql DLQ Brighter-Managed](0040-mssql-dlq-brighter-managed.md)
- Kafka DLQ implementation: [Spec 0001](../../specs/0001-kafka-dead-letter-queue/)
