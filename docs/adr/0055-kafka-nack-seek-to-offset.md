# 55. Kafka Nack Seeks to Offset for Redelivery

Date: 2026-03-23

## Status

Accepted

## Context

**Parent Requirement**: [specs/0025-nack_fix/requirements.md](../../specs/0025-nack_fix/requirements.md)

**Scope**: This ADR addresses the broken `Nack`/`NackAsync` behavior in `KafkaMessageConsumer` where nacked messages are silently lost instead of being redelivered.

### The Problem

`KafkaMessageConsumer.Nack` is currently a no-op with the comment _"not committing the offset is sufficient for redelivery"_. This is incorrect for the following reasons:

1. **The consumer has already advanced past the record.** After `Consume()` returns a record, the consumer's internal position moves to the next offset. Not committing does not rewind the position — it only means the offset won't be persisted. The consumer will still read the next record on the next `Consume()` call.

2. **A subsequent `Acknowledge` commits all prior offsets.** Kafka's offset commit semantics are "up to and including" — committing offset N marks offsets 0 through N-1 as complete. If a message at offset 5 is nacked but offset 6 is later acknowledged, the commit of offset 7 (Brighter commits offset+1) implicitly marks offset 5 as consumed. The nacked message is permanently lost.

3. **Even without a subsequent acknowledge, a consumer group rebalance commits stored offsets.** The `AutoOffsetStore` is disabled, but `_offsetStorage` is flushed on rebalance, which could commit past the nacked offset.

The `IAmAMessageConsumerSync` interface documents that `Nack` should release the message "back to the transport for redelivery", but the Kafka implementation does not fulfil this contract.

### Forces

- Kafka has no per-message selective acknowledgment — offsets are sequential and cumulative.
- The Confluent .NET client's `IConsumer<TKey, TValue>` supports `Seek(TopicPartitionOffset)` to rewind the consumer position.
- The `TopicPartitionOffset` of each consumed record is already stored in the message's header bag under `HeaderNames.PARTITION_OFFSET`.
- Brighter uses batched offset storage (`_offsetStorage`) with periodic flushing, not immediate commits.
- The fix must not break the normal acknowledge path or introduce performance regressions.

## Decision

**When a message is nacked, the `KafkaMessageConsumer` will call `_consumer.Seek(topicPartitionOffset)` to rewind the consumer position to the nacked message's offset.** This causes the next `Consume()` call to return the same record again.

### Responsibilities

The `KafkaMessageConsumer` already has the **"doing"** responsibility of managing the consumer lifecycle and offset tracking. The nack-seek behavior is a natural extension of this responsibility — it is the consumer's job to ensure messages are delivered according to the acknowledgment contract.

No new roles or types are needed. The change is entirely within the existing `KafkaMessageConsumer` class, which implements both `IAmAMessageConsumerSync` and `IAmAMessageConsumerAsync`.

### Implementation Approach

#### Nack Method

```csharp
/// <summary>
/// Nacks the specified message by seeking the consumer back to the message's offset,
/// so that the next <see cref="Receive"/> call will return the same message again.
/// </summary>
/// <param name="message">The message.</param>
public void Nack(Message message)
{
    if (!message.Header.Bag.TryGetValue(HeaderNames.PARTITION_OFFSET, out var bagData))
        return;

    var topicPartitionOffset = bagData as TopicPartitionOffset;
    if (topicPartitionOffset == null)
    {
        Log.CannotNackMessage(s_logger, message.Id);
        return;
    }

    Log.NackingMessage(s_logger, topicPartitionOffset.Offset.Value, topicPartitionOffset.Topic, topicPartitionOffset.Partition.Value);

    _consumer.Seek(topicPartitionOffset);
}
```

The following source-generated log methods should be added to the `private static partial class Log`:

```csharp
[LoggerMessage(LogLevel.Warning, "Cannot nack message {MessageId} as no offset data")]
public static partial void CannotNackMessage(ILogger logger, string messageId);

[LoggerMessage(LogLevel.Information, "Nacking message at offset {Offset} on topic {Topic} partition {Partition} - seeking back for redelivery")]
public static partial void NackingMessage(ILogger logger, long offset, string topic, int partition);
```

#### NackAsync Method

```csharp
/// <summary>
/// Nacks the specified message by seeking the consumer back to the message's offset,
/// so that the next <see cref="ReceiveAsync"/> call will return the same message again.
/// </summary>
/// <param name="message">The message.</param>
/// <param name="cancellationToken">Cancel the nack operation</param>
public Task NackAsync(Message message, CancellationToken cancellationToken = default)
{
    Nack(message);
    return Task.CompletedTask;
}
```

This mirrors the existing pattern where `AcknowledgeAsync` delegates to `Acknowledge`.

#### Docstring Updates

The existing XML doc comments on `Nack` and `NackAsync` state that Kafka nack is a "no-op because not committing the offset is sufficient to allow redelivery." This is incorrect and must be replaced with the new docstrings shown above, which describe the seek-back behavior.

The `IAmAMessageConsumerSync.Nack` and `IAmAMessageConsumerAsync.NackAsync` interface docstrings should also be updated to remove the phrase "For stream-based transports, this is a no-op because not committing the offset is sufficient" and instead use transport-neutral language such as "releases the message back to the transport for redelivery."

### Key Design Choices

1. **Seek, not Pause/Resume.** `Seek` is the simplest and most direct mechanism — it rewinds the consumer position to the exact offset. Pause/Resume would require tracking paused partitions and is designed for flow control, not redelivery.

2. **No changes to Acknowledge.** The `Acknowledge` method already correctly stores offsets and flushes them in batches. After a `Seek`, the consumer position is rewound, so the next `Consume()` returns the nacked record. The nacked record's offset will never reach `_offsetStorage` unless it is eventually acknowledged.

3. **Guard clauses match Acknowledge pattern.** The `Nack` method uses the same guard pattern as `Acknowledge` — check for `PARTITION_OFFSET` in the bag, cast to `TopicPartitionOffset`, and bail out gracefully if missing. This is consistent and predictable.

4. **Log, don't throw, on missing offset.** If the header bag doesn't contain the partition offset, the method returns silently (matching `Acknowledge` behavior). A log warning is added to aid debugging.

5. **No offset storage removal.** We do not need to remove the nacked offset from `_offsetStorage` because `Acknowledge` stores `offset + 1`, and the Seek ensures the consumer will re-read from the nacked offset. If a batch flush happens before the nacked message is re-acknowledged, the committed offset will be for a *later* message, but the Seek has already repositioned the consumer. On restart, the consumer resumes from the committed offset, which is correct because the nacked message's offset was never committed.

### Interaction with Consumer Group Rebalancing

When a rebalance occurs, Kafka calls the revoke/assign handlers. The current implementation flushes offsets on revoke. Since the nacked message's offset was never stored in `_offsetStorage`, it won't be committed during rebalance. After reassignment, the consumer resumes from the last committed offset, which will be before the nacked message — so the message will be redelivered. This is the correct behavior.

## Consequences

### Positive

- Fulfils the `IAmAMessageConsumerSync.Nack` contract — nacked messages are genuinely redelivered.
- Minimal code change — two methods updated, no new types or abstractions.
- Consistent with the existing code patterns (`Acknowledge` guard clauses, async delegation).
- Works correctly with consumer group rebalancing.
- The `Acknowledge` method's guard clause for missing offset data was updated to log a warning (via `CannotAcknowledgeMessage`) and upgraded from `Information` to `Warning` level, making both `Acknowledge` and `Nack` consistent in how they report missing offset data.

### Negative

- A nacked message will block processing of subsequent messages on the same partition until it is acknowledged. This is inherent to Kafka's sequential offset model and is the correct semantic for `Nack` (the message should be retried).
- If a message is permanently unprocessable and no DLQ is configured, it will loop indefinitely. This is out of scope — DLQ handling is addressed by separate specs (0001, 0010).

### Risks and Mitigations

- **Risk**: `Seek` throws if the consumer is closed or the partition is unassigned.
  **Mitigation**: The `Nack` method wraps the `Seek` call in a catch filter for `KafkaException` and `InvalidOperationException` (which Seek throws when the consumer is not subscribed or the partition is unassigned during a rebalance). Both methods should be called only while the consumer is active.

- **Risk**: Seek could cause an infinite loop if the handler always throws `DontAckAction`.
  **Mitigation**: This is by design — `Nack` means "try again". Retry limits and DLQ are handled by other components (backstop handler, DLQ specs). The nack mechanism should not impose its own limits.

## Alternatives Considered

### 1. Pause and Resume the Partition

Pause the partition after nack, then resume on next `Receive`. This is more complex (requires tracking paused state), designed for flow control rather than redelivery, and doesn't actually rewind the consumer position — it only stops fetching new records. The message would still need to be re-queued somehow. Rejected as overly complex for this use case.

### 2. Create a New Consumer Instance

Destroy and recreate the `IConsumer` to force a re-read from the last committed offset. This is extremely expensive (new TCP connections, consumer group rejoin, rebalance) and would disrupt other partitions. Rejected as disproportionate.

### 3. Buffer the Message Internally

Store the nacked message in an internal buffer and return it on the next `Receive` call without involving Kafka. This avoids the Seek call but introduces state management complexity, doesn't survive process restarts, and diverges from the transport-level redelivery semantics that other transports provide. Rejected as inconsistent with the abstraction.

## References

- Requirements: [specs/0025-nack_fix/requirements.md](../../specs/0025-nack_fix/requirements.md)
- GitHub Issue: [#4051](https://github.com/BrighterCommand/Brighter/issues/4051)
- Confluent Kafka .NET `IConsumer.Seek`: [Confluent documentation](https://docs.confluent.io/platform/current/clients/confluent-kafka-dotnet/_site/api/Confluent.Kafka.IConsumer-2.html#Confluent_Kafka_IConsumer_2_Seek_Confluent_Kafka_TopicPartitionOffset_)
- Related ADRs: None (single ADR for this spec)
