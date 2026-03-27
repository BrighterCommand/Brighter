# Tasks: Kafka Nack Fix (#4051)

**Spec**: 0025-nack_fix
**ADR**: [0055 - Kafka Nack Seeks to Offset for Redelivery](../../docs/adr/0055-kafka-nack-seek-to-offset.md)

## Prerequisites

- [x] **TIDY: Update interface docstrings to remove incorrect "no-op" language**
  - Update `IAmAMessageConsumerSync.Nack` in `src/Paramore.Brighter/IAmAMessageConsumerSync.cs` to remove "For stream-based transports, this is a no-op because not committing the offset is sufficient"
  - Update `IAmAMessageConsumerAsync.NackAsync` in `src/Paramore.Brighter/IAmAMessageConsumerAsync.cs` similarly
  - Use transport-neutral language: "Releases the message back to the transport for redelivery"
  - This is a structural change (docstrings only) — commit separately from behavioral changes

## Implementation Tasks

- [x] **TEST + IMPLEMENT: Nacking a Kafka message causes it to be redelivered on the next Receive**
  - **USE COMMAND**: `/test-first when nacking a kafka message it should be redelivered on next receive`
  - Test location: `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Reactor`
  - Test file: `When_nacking_a_message_it_should_be_redelivered.cs`
  - Test should verify:
    - Produce a message to a Kafka topic
    - Consume the message via `Receive`
    - Call `Nack` on the message
    - Call `Receive` again — the same message (same ID, same body) should be returned
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Replace the no-op `Nack` body in `KafkaMessageConsumer.cs` (line 352-355) with seek-back logic per ADR 0055
    - Extract `TopicPartitionOffset` from `message.Header.Bag` using `HeaderNames.PARTITION_OFFSET`
    - Call `_consumer.Seek(topicPartitionOffset)` to rewind the consumer position
    - Add guard clauses matching the `Acknowledge` pattern (null checks, early return)
    - Add source-generated log methods `CannotNackMessage` and `NackingMessage` to `private static partial class Log`
    - Update `NackAsync` (line 363-366) to delegate to `Nack` (matching `AcknowledgeAsync` pattern)
    - Update XML doc comments on `Nack` and `NackAsync` to describe seek-back behavior

- [x] **TEST + IMPLEMENT: Nacking a Kafka message asynchronously causes it to be redelivered on the next ReceiveAsync**
  - **USE COMMAND**: `/test-first when nacking a kafka message async it should be redelivered on next receive`
  - Test location: `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Proactor`
  - Test file: `When_nacking_a_message_it_should_be_redelivered_async.cs`
  - Test should verify:
    - Produce a message to a Kafka topic
    - Consume the message via `ReceiveAsync`
    - Call `NackAsync` on the message
    - Call `ReceiveAsync` again — the same message (same ID, same body) should be returned
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Already done in previous task (`NackAsync` delegates to `Nack`)
    - This task validates the async path works correctly end-to-end

## Verification

- [x] **Existing Kafka tests pass with no regressions**
  - Run: `dotnet test tests/Paramore.Brighter.Kafka.Tests/ --filter "Category=Kafka"`
  - All existing acknowledge, reject, and consumer tests must continue to pass
