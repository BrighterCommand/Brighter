# 39. Redis Dead Letter Queue — Brighter-Managed

Date: 2026-02-13

## Status

Accepted

## Context

**Parent Requirement**: [specs/0011-redis-dead-letter-queue/requirements.md](../../specs/0011-redis-dead-letter-queue/requirements.md)

**Scope**: This ADR addresses the addition of Brighter-managed DLQ support to the Redis transport. It follows the pattern established by the Kafka implementation (ADR 0034) and the rejection routing strategy (ADR 0036).

Redis has no native dead letter queue mechanism. The current `RedisMessageConsumer.Reject()` is a no-op: it removes the message from the `_inflight` dictionary and returns `true`, silently discarding the message. Because Redis uses BLPOP (blocking list pop), messages are removed from the queue immediately upon read. When a handler rejects a message, it is lost with no way to inspect or replay it.

This is the exact scenario ADR 0034 was designed for: a transport lacking native DLQ support where Brighter must manage the DLQ itself.

### Forces

- Redis messages are popped on read (BLPOP) — there is no visibility timeout or redelivery mechanism
- The message is already gone from the source queue by the time `Reject()` is called
- The Kafka DLQ implementation (spec 0001) established the reference pattern for Brighter-managed DLQ
- The SQS implementation (spec 0010) confirmed the pattern generalises across transports
- Consistency across transports reduces cognitive load for users

## Decision

We will add Brighter-managed DLQ support to the Redis transport following the established pattern from Kafka and SQS.

### Roles and Responsibilities

**RedisSubscription** (information holder):
- *Knowing*: the dead letter routing key and invalid message routing key for this subscription
- Implements `IUseBrighterDeadLetterSupport` and `IUseBrighterInvalidMessageSupport`

**RedisMessageConsumerFactory** (coordinator):
- *Deciding*: whether the subscription has DLQ/invalid message support configured
- *Doing*: extracting routing keys from the subscription via interface checks and passing them to the consumer constructor

**RedisMessageConsumer** (service provider):
- *Deciding*: which rejection route to take based on `MessageRejectionReason` (per ADR 0036)
- *Doing*: enriching message metadata and producing to the appropriate DLQ/invalid message channel
- *Knowing*: the DLQ and invalid message routing keys, and lazily-created producers for each

### Rejection Routing (per ADR 0036)

The routing decision tree in `Reject`/`RejectAsync`:

```
Reject(message, reason)
├── DeliveryError
│   ├── _deadLetterProducer exists → send to DLQ, remove from _inflight
│   └── no producer → remove from _inflight, log warning
├── Unacceptable
│   ├── _invalidMessageProducer exists → send to invalid channel, remove from _inflight
│   ├── _deadLetterProducer exists (fallback) → send to DLQ, remove from _inflight
│   └── no producer → remove from _inflight, log warning
```

### Producer Creation

DLQ producers are created lazily on first rejection. The consumer creates a `RedisMessageProducer` directly, configured with:
- The same `RedisMessagingGatewayConfiguration` as the consumer (ensures same Redis connection settings)
- A `RedisMessagePublication` with the DLQ/invalid message routing key as its topic

This mirrors the Kafka/SQS pattern where the consumer owns the DLQ producer lifecycle. The producer is lightweight — it shares the static `RedisManagerPool` managed by `RedisMessageGateway`.

### Message Metadata Enrichment

Before sending to the DLQ, the message header bag is enriched with (using `HeaderNames` constants):
- `OriginalTopic` — the source routing key
- `RejectionReason` — `DeliveryError` or `Unacceptable`
- `RejectionTimestamp` — UTC timestamp of rejection
- `OriginalMessageType` — the original `MessageType`

### Key Simplification vs SQS

Unlike SQS where `ConfirmQueueExists` and `MakeChannels` were concerns (ADR 0038), Redis queue creation is implicit — queues are Redis lists that are created on first push. There is no need to pass `MakeChannels` or call any queue creation method. This simplifies the producer setup.

## Consequences

### Positive

- Rejected messages are no longer silently lost
- Users can configure DLQ per subscription, consistent with Kafka and SQS
- Existing code with no DLQ configured continues to work unchanged (backward compatible)
- Lazy producer creation means zero overhead when DLQ is not used
- Shared `RedisManagerPool` means no additional Redis connections for DLQ producers

### Negative

- DLQ messages are not transactionally guaranteed (consistent with ADR 0034's decision to not use the Outbox for error paths)
- If the Redis connection fails during DLQ production, the message may be lost (mitigated by logging the full message content at error level)

### Risks and Mitigations

- **Risk**: DLQ production fails silently → **Mitigation**: Log the error at warning level and remove from `_inflight` (same as current no-op behaviour, no worse than today)
- **Risk**: DLQ topic accumulates messages without TTL → **Mitigation**: DLQ messages inherit the standard Redis message TTL from `RedisMessagingGatewayConfiguration.MessageTimeToLive`

## Alternatives Considered

### 1. Use Redis Streams with XACK/XCLAIM

Redis Streams (`XADD`/`XREADGROUP`/`XACK`) have a pending entries list (PEL) that could function as a native retry/DLQ mechanism. However, the Brighter Redis transport is built on Redis Lists (LPUSH/BLPOP), not Streams. Migrating to Streams would be a much larger change affecting all Redis users and is out of scope for this ADR.

### 2. Do Nothing

Rejected messages continue to be lost. This is unacceptable for users who need observability into message failures.

## References

- Requirements: [specs/0011-redis-dead-letter-queue/requirements.md](../../specs/0011-redis-dead-letter-queue/requirements.md)
- ADR 0034: [Provide DLQ Where Missing](0034-provide-dlq-where-missing.md)
- ADR 0036: [Message Rejection Routing Strategy](0036-message-rejection-routing-strategy.md)
- Kafka DLQ implementation: [Spec 0001](../../specs/0001-kafka-dead-letter-queue/)
- SQS DLQ implementation: [Spec 0010](../../specs/0010-aws-sqs-dead-letter-queue/)
