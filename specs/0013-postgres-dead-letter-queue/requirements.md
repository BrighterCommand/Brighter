# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Linked ADR**: [ADR 0034: Provide DLQ Where Missing](../../docs/adr/0034-provide-dlq-where-missing.md)
**Reference Implementation**: [Spec 0001: Kafka Dead Letter Queue](../0001-kafka-dead-letter-queue/)

## Problem Statement

The PostgreSQL message consumer (`PostgresMessageConsumer`) currently handles message rejection by deleting the message from the queue table. There is no dead letter queue support - rejected messages are permanently lost.

When a handler throws `RejectMessageAction`, the message is deleted with no way to inspect or replay it. There is no support for routing rejected messages to a dead letter channel or invalid message channel.

## Proposed Solution

Add Brighter-managed DLQ support following the Kafka pattern:

- `PostgresSubscription` implements `IUseBrighterDeadLetterSupport` and `IUseBrighterInvalidMessageSupport`
- `PostgresConsumerFactory` extracts routing keys from the subscription and passes them to the consumer
- `PostgresMessageConsumer.Reject()` and `RejectAsync()` send messages to DLQ/Invalid Message channels using a lazy `IAmAMessageProducer`, then delete the original
- Messages are enriched with metadata before being sent to DLQ

## Requirements

### Functional Requirements

- When `Reject()` is called with a `DeliveryError` reason, the message should be sent to the configured dead letter channel (a separate PostgreSQL queue topic) before deleting the original
- When `Reject()` is called with an `Unacceptable` reason, the message should be sent to the configured invalid message channel, falling back to the DLQ if no invalid message channel is configured
- After sending to DLQ, the original message must be deleted from the source queue (current behaviour preserved)
- The dead letter message should include metadata: original topic, rejection reason, rejection timestamp, original message type
- DLQ routing key should be configurable per subscription via `IUseBrighterDeadLetterSupport`
- Invalid Message routing key should be configurable per subscription via `IUseBrighterInvalidMessageSupport`
- If no DLQ or Invalid Message channel is configured, rejection should delete the message (current behaviour) and log a warning
- All new constructor parameters must be optional to maintain backward compatibility

### Non-functional Requirements

- **Performance**: Lazy producer initialization to avoid overhead when DLQ is not used
- **Reliability**: If DLQ production fails, log the error and delete the original message to prevent blocking the consumer
- **Observability**: DLQ operations should be logged with structured logging
- **Consistency**: Follow the same pattern as Kafka DLQ implementation

### Out of Scope

- Automatic replay/retry from DLQ
- DLQ message format transformation

## Acceptance Criteria

1. When a handler throws `RejectMessageAction`, the message is sent to the configured DLQ channel before deletion
2. The DLQ message contains the original message payload and rejection metadata
3. Users can configure DLQ and Invalid Message channel names per subscription
4. The original message is still deleted from the source queue after DLQ send
5. Existing tests pass and new tests cover DLQ scenarios
6. All new parameters are optional (backward compatible)

## Testing Approach

- Integration tests with PostgreSQL for end-to-end DLQ flow
- Tests for rejection routing (DeliveryError → DLQ, Unacceptable → Invalid Message)
- Tests for fallback behaviour (Unacceptable with no Invalid Message channel → DLQ)
- Tests for no-channel-configured behaviour (delete and log)
