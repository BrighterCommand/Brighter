# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Linked ADR**: [ADR 0034: Provide DLQ Where Missing](../../docs/adr/0034-provide-dlq-where-missing.md)
**Reference Implementation**: [Spec 0001: Kafka Dead Letter Queue](../0001-kafka-dead-letter-queue/)

## Problem Statement

The Redis message consumer (`RedisMessageConsumer`) currently handles message rejection as a no-op: it removes the message from the in-flight list and returns `true`, silently discarding the message. Redis has no native dead letter queue support.

When a handler throws `RejectMessageAction`, the message is lost with no way to inspect or replay it. There is no support for routing rejected messages to a dead letter channel or invalid message channel.

## Proposed Solution

Add Brighter-managed DLQ support following the Kafka pattern:

- `RedisSubscription` implements `IUseBrighterDeadLetterSupport` and `IUseBrighterInvalidMessageSupport`
- `RedisMessageConsumerFactory` extracts routing keys from the subscription and passes them to the consumer
- `RedisMessageConsumer.Reject()` and `RejectAsync()` send messages to DLQ/Invalid Message channels using a lazy `IAmAMessageProducer`
- Messages are enriched with metadata before being sent to DLQ

## Requirements

### Functional Requirements

- When `Reject()` is called with a `DeliveryError` reason, the message should be sent to the configured dead letter channel (a separate Redis topic/list)
- When `Reject()` is called with an `Unacceptable` reason, the message should be sent to the configured invalid message channel, falling back to the DLQ if no invalid message channel is configured
- The dead letter message should include metadata: original topic, rejection reason, rejection timestamp, original message type
- DLQ routing key should be configurable per subscription via `IUseBrighterDeadLetterSupport`
- Invalid Message routing key should be configurable per subscription via `IUseBrighterInvalidMessageSupport`
- If no DLQ or Invalid Message channel is configured, rejection should remove from in-flight (current behaviour) and log a warning
- All new constructor parameters must be optional to maintain backward compatibility

### Non-functional Requirements

- **Performance**: Lazy producer initialization to avoid overhead when DLQ is not used
- **Reliability**: If DLQ production fails, log the error and acknowledge the message to prevent blocking the consumer
- **Observability**: DLQ operations should be logged with structured logging
- **Consistency**: Follow the same pattern as Kafka DLQ implementation

### Out of Scope

- Automatic replay/retry from DLQ
- DLQ message format transformation

## Acceptance Criteria

1. When a handler throws `RejectMessageAction`, the message is sent to the configured DLQ channel
2. The DLQ message contains the original message payload and rejection metadata
3. Users can configure DLQ and Invalid Message channel names per subscription
4. Existing tests pass and new tests cover DLQ scenarios
5. All new parameters are optional (backward compatible)

## Testing Approach

- Integration tests with Redis for end-to-end DLQ flow
- Tests for rejection routing (DeliveryError → DLQ, Unacceptable → Invalid Message)
- Tests for fallback behaviour (Unacceptable with no Invalid Message channel → DLQ)
- Tests for no-channel-configured behaviour (acknowledge and log)
