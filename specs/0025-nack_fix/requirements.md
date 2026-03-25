# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Linked Issue**: #4051

## Problem Statement

As a developer using Brighter with Kafka, I would like the `Nack` operation on `KafkaMessageConsumer` to make the rejected message available for reprocessing, so that messages that cannot be handled (e.g. via `DeferMessageAction` or `DontAckAction`) are retried rather than silently skipped.

Currently, calling `Nack`/`NackAsync` on a Kafka message is effectively a no-op because:

1. The record has already been read from the partition, so the consumer will move on to the next record regardless of whether the offset is committed.
2. If the next record is subsequently acknowledged, Kafka's offset commit semantics mark **all prior offsets** (including the nacked one) as complete, permanently losing the unacknowledged message.

## Proposed Solution

When a message is nacked, the Kafka consumer should seek back to the offset of the nacked message so that it will be presented to the handler again on the next receive call. This ensures the message is reprocessed until it is explicitly acknowledged.

## Requirements

### Functional Requirements

- **FR1**: When `Nack` is called on a message, the Kafka consumer must seek back to that message's offset so it is re-delivered on the next `Receive` call.
- **FR2**: When `NackAsync` is called on a message, the same seek-back behavior must apply.
- **FR3**: A nacked message must continue to be redelivered until it is explicitly acknowledged.
- **FR4**: Acknowledging a subsequent message must NOT implicitly acknowledge a previously nacked message (i.e. the seek must prevent offset progression past the nacked message).

### Non-functional Requirements

- The fix must not introduce measurable performance regression for the normal (acknowledge) path.
- The fix must be compatible with Kafka consumer group rebalancing (seek state should survive rebalance where possible).

### Constraints and Assumptions

- Kafka's offset commit model commits "up to and including" a given offset — there is no per-message selective ack.
- The `TopicPartitionOffset` of the message is available in the message header bag under `HeaderNames.PARTITION_OFFSET`.
- We are using the Confluent Kafka .NET client (`IConsumer<string, string>`), which supports `Seek(TopicPartitionOffset)`.

### Out of Scope

- Dead letter queue (DLQ) behavior for messages that are nacked repeatedly (covered by separate specs).
- Nack behavior for other transports (RabbitMQ, SQS, etc.) — this spec is Kafka-only.
- Retry limits or circuit-breaking on nacked messages.

## Acceptance Criteria

- **AC1**: A unit/integration test demonstrates that after calling `Nack` on a message, the same message is returned by the next `Receive` call.
- **AC2**: A unit/integration test demonstrates that acknowledging a later message does not skip a previously nacked message.
- **AC3**: The existing Kafka consumer tests continue to pass (no regressions).
- **AC4**: Both synchronous `Nack` and asynchronous `NackAsync` paths are covered by tests.

## Additional Context

The issue provides a suggested fix using `_consumer.Seek(topicPartitionOffset)` to rewind the consumer to the nacked record's offset. The technical design (ADR) should evaluate this approach and address error handling, edge cases (e.g. missing offset in header bag, consumer closed), and interaction with consumer group rebalancing.
