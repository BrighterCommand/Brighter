# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Linked ADR**: [ADR 0034: Provide DLQ Where Missing](../../docs/adr/0034-provide-dlq-where-missing.md)
**Reference Implementation**: [Spec 0001: Kafka Dead Letter Queue](../0001-kafka-dead-letter-queue/)

## Problem Statement

The AWS SQS message consumer (`SqsMessageConsumer`) currently handles message rejection by calling `ChangeMessageVisibilityAsync` with a timeout of 0 when a DLQ is configured. This makes the message immediately visible again, relying on SQS's native redrive policy to eventually move the message to the DLQ after enough receive attempts.

This approach is indirect and has problems:

1. When a handler throws `RejectMessageAction`, the intent is to move the message to the DLQ **immediately**, not after more receive cycles.
2. The `ChangeMessageVisibility(0)` approach causes the message to be re-received and re-processed before the redrive policy kicks in.
3. There is no support for `IUseBrighterDeadLetterSupport` or `IUseBrighterInvalidMessageSupport` interfaces, so there is no way to configure Brighter-managed DLQ/Invalid Message routing keys.
4. There is no message metadata enrichment (original topic, rejection reason, timestamp) as provided by the Kafka implementation.
5. There is no support for an Invalid Message Channel separate from the DLQ.

Both `Paramore.Brighter.MessagingGateway.AWSSQS` and `Paramore.Brighter.MessagingGateway.AWSSQS.V4` have identical behaviour and both need the same changes.

## Proposed Solution

Replace the `ChangeMessageVisibility` approach with direct `SendMessage` to the DLQ queue, then delete the original message. Follow the same pattern established by the Kafka DLQ implementation:

- `SqsSubscription` implements `IUseBrighterDeadLetterSupport` and `IUseBrighterInvalidMessageSupport`
- `SqsMessageConsumerFactory` extracts routing keys from the subscription and passes them to the consumer
- `SqsMessageConsumer.Reject()` and `RejectAsync()` send messages directly to the DLQ/Invalid Message queue using a lazy `IAmAMessageProducer`, then delete the original message
- Messages are enriched with metadata before being sent to DLQ

## Requirements

### Functional Requirements

- When `Reject()` is called with a `DeliveryError` reason, the message should be sent directly to the configured dead letter queue using `SendMessage`
- When `Reject()` is called with an `Unacceptable` reason, the message should be sent to the configured invalid message queue, falling back to the DLQ if no invalid message queue is configured
- After sending to DLQ/Invalid Message queue, the original message must be deleted from the source queue
- The dead letter message should include metadata: original topic, rejection reason, rejection timestamp, original message type
- DLQ routing key should be configurable per subscription via `IUseBrighterDeadLetterSupport`
- Invalid Message routing key should be configurable per subscription via `IUseBrighterInvalidMessageSupport`
- Both `AWSSQS` and `AWSSQS.V4` packages must be updated identically
- If no DLQ or Invalid Message queue is configured, rejection should acknowledge (delete) the message and log a warning (matching Kafka behaviour)
- All new constructor parameters must be optional to maintain backward compatibility

### Non-functional Requirements

- **Performance**: Lazy producer initialization to avoid overhead when DLQ is not used
- **Reliability**: If DLQ production fails, log the error and delete the original message to prevent blocking the consumer
- **Observability**: DLQ operations should be logged with structured logging (message ID, rejection reason)
- **Consistency**: Follow the same pattern as Kafka DLQ implementation for developer familiarity

### Out of Scope

- Modifying the existing SQS native redrive policy configuration
- Automatic replay/retry from DLQ
- DLQ message format transformation

## Acceptance Criteria

1. When a handler throws `RejectMessageAction`, the message is sent directly to the configured DLQ queue
2. The DLQ message contains the original message payload and rejection metadata
3. Users can configure DLQ and Invalid Message queue names per subscription
4. The original message is deleted from the source queue after DLQ send
5. Existing tests pass and new tests cover DLQ scenarios
6. Both AWSSQS and AWSSQS.V4 packages are updated
7. All new parameters are optional (backward compatible)

## Testing Approach

- Integration tests with actual SQS (or LocalStack) for end-to-end DLQ flow
- Tests for rejection routing (DeliveryError → DLQ, Unacceptable → Invalid Message)
- Tests for fallback behaviour (Unacceptable with no Invalid Message queue → DLQ)
- Tests for no-channel-configured behaviour (acknowledge and log)
- Regression tests to verify existing SQS functionality is not broken
