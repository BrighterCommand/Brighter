# 35. Kafka DLQ Producer for Requeue

Date: 2026-01-11

## Status

Proposed

## Context

**Parent Requirement**: [specs/0001-kafka-dead-letter-queue/requirements.md](../../specs/0001-kafka-dead-letter-queue/requirements.md)

**Scope**: This ADR focuses specifically on how the Kafka transport implements the producer for dead letter queue and invalid message channel functionality. It builds on [ADR 0034: Provide DLQ Where Missing](0034-provide-dlq-where-missing.md), which established the high-level decision to use `IAmAMessageProducer` directly for DLQ publishing.

### The Problem

ADR 0034 decided that the `IAmAMessageConsumer` (sync or async) must manage the lifetime of an `IAmAMessageProducer` (sync or async) to send messages to the Dead Letter Channel or Invalid Message Channel. For Kafka, we need to decide:

1. **Producer Creation**: When and how does `KafkaMessageConsumer` create the `IAmAMessageProducer` for DLQ?
2. **Producer Lifecycle**: Who owns the producer lifetime? When is it created and disposed?
3. **Topic Management**: How do we handle DLQ topic creation (create/validate/assume)?
4. **Producer Configuration**: What configuration does the DLQ producer need? Should it match the data topic producer?
5. **Error Handling**: What happens if producing to the DLQ fails? Do we retry? Log and continue?
6. **Offset Management**: When do we commit the offset - before or after DLQ production succeeds?

### Constraints

- Must follow ADR 0034's decision to use `IAmAMessageProducer` directly
- Sync consumer must use sync producer; async consumer must use async producer
- DLQ messages do NOT go through the Outbox (per ADR 0034)
- Must support `IUseBrighterDeadLetterSupport` and `IUseBrighterInvalidMessageSupport` interfaces
- Must handle topic auto-creation matching data topic strategy (create/validate/assume)

## Decision

### Producer Creation and Lifecycle

The `KafkaMessageConsumer` and `KafkaMessageConsumerAsync` will create their own `KafkaMessageProducer` or `KafkaMessageProducerAsync` instances for DLQ/invalid message publishing.

**Creation Timing**: The DLQ producer will be created **lazily** on first rejection:
- Not created in constructor (avoid unnecessary producer if never used)
- Created when first `RejectMessageAction` or `InvalidMessageAction` is encountered
- Cached for subsequent rejections

**Lifetime Management**: The consumer owns the DLQ producer:
- Consumer creates the producer when needed
- Consumer disposes the producer in its own `Dispose()` method
- Producer shares the consumer's lifetime

**Thread Safety**: Uses `Lazy<T>` for thread-safe lazy initialization to handle concurrent first-rejection scenarios.

```csharp
public class KafkaMessageConsumer : IAmAMessageConsumerSync, IDisposable
{
    private readonly IUseBrighterDeadLetterSupport? _deadLetterSupport;
    private readonly IUseBrighterInvalidMessageSupport? _invalidMessageSupport;
    private readonly Lazy<IAmAMessageProducerSync> _deadLetterProducer;
    private readonly Lazy<IAmAMessageProducerSync> _invalidMessageProducer;
    private readonly KafkaMessagingGatewayConfiguration _configuration;

    public KafkaMessageConsumer(
        KafkaMessagingGatewayConfiguration configuration,
        IUseBrighterDeadLetterSupport? deadLetterSupport = null,
        IUseBrighterInvalidMessageSupport? invalidMessageSupport = null)
    {
        _configuration = configuration;
        _deadLetterSupport = deadLetterSupport;
        _invalidMessageSupport = invalidMessageSupport;

        // Thread-safe lazy initialization
        _deadLetterProducer = new Lazy<IAmAMessageProducerSync>(CreateDeadLetterProducer);
        _invalidMessageProducer = new Lazy<IAmAMessageProducerSync>(CreateInvalidMessageProducer);
    }

    private IAmAMessageProducerSync CreateDeadLetterProducer()
    {
        if (_deadLetterSupport?.DeadLetterRoutingKey == null)
            throw new InvalidOperationException("Dead letter routing key not configured");

        var publication = new KafkaPublication
        {
            Topic = _deadLetterSupport.DeadLetterRoutingKey,
            MakeChannels = _configuration.MakeChannels // Match data topic strategy
        };

        return new KafkaMessageProducer(_configuration, publication);
    }

    private IAmAMessageProducerSync CreateInvalidMessageProducer()
    {
        if (_invalidMessageSupport?.InvalidMessageRoutingKey == null)
            throw new InvalidOperationException("Invalid message routing key not configured");

        var publication = new KafkaPublication
        {
            Topic = _invalidMessageSupport.InvalidMessageRoutingKey,
            MakeChannels = _configuration.MakeChannels
        };

        return new KafkaMessageProducer(_configuration, publication);
    }

    // Access via .Value triggers lazy initialization (thread-safe)
    private void RejectMessage(Message message, string? reason = null)
    {
        try
        {
            var enrichedMessage = EnrichMessageWithMetadata(message, reason);
            _deadLetterProducer.Value.Send(enrichedMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to produce message to dead letter queue. " +
                "Original message offset: {Offset}, Topic: {Topic}",
                message.Offset, _topic);
            // Continue - commit offset anyway
        }
    }

    public void Dispose()
    {
        // Only dispose if producer was created
        if (_deadLetterProducer.IsValueCreated)
            _deadLetterProducer.Value.Dispose();

        if (_invalidMessageProducer.IsValueCreated)
            _invalidMessageProducer.Value.Dispose();
    }
}
```

### Producer Configuration

The DLQ producer will inherit most configuration from the consumer's `KafkaMessagingGatewayConfiguration`:
- **Bootstrap servers**: Same as consumer
- **Security settings**: Same as consumer (SASL, SSL, etc.)
- **Serialization**: Same serializer as data messages
- **Make channels strategy**: Matches the data topic strategy (create/validate/assume)

The DLQ producer will use **different** settings for:
- **Topic**: The DLQ or invalid message routing key (not the data topic)
- **Partitioning**: Will use default partitioning (no partition key preservation)
- **Acks**: Will use `acks=all` for reliability (even if data topic uses `acks=1`)

### Topic Management and Auto-Creation

The DLQ topic creation behavior must match the data topic's `MakeChannels` setting:

| MakeChannels | Behavior |
|--------------|----------|
| `Create` | Create DLQ topic if it doesn't exist (with default configs) |
| `Validate` | Verify DLQ topic exists; throw if missing |
| `Assume` | Assume DLQ topic exists; let Kafka fail if missing |

This ensures consistency: if a user has opted into automatic topic creation for their data topics, DLQ topics will also be created automatically.

```csharp
public class KafkaPublication
{
    public RoutingKey Topic { get; set; }
    public OnMissingChannel MakeChannels { get; set; } = OnMissingChannel.Create;
    // ... other properties
}
```

### Error Handling for DLQ Production Failures

If producing to the DLQ fails, we have a critical decision: what do we do with the original message?

**Decision**: Log the error and **continue processing** (commit offset for streams, ack for queues).

**Rationale**:
1. The original message has already failed processing
2. Blocking the consumer on DLQ failure creates a cascading failure
3. Logging + alerting on DLQ production failures is sufficient
4. The message is "lost" but the system continues

**Implementation**:
```csharp
try
{
    // Accessing .Value triggers thread-safe lazy initialization
    var enrichedMessage = EnrichMessageWithMetadata(messageToReject, rejectionReason);
    _deadLetterProducer.Value.Send(enrichedMessage);
}
catch (Exception ex)
{
    _logger.LogError(ex,
        "Failed to produce message to dead letter queue. " +
        "Original message offset: {Offset}, Topic: {Topic}",
        messageToReject.Offset, _topic);
    // Continue - commit offset anyway
}
```

### Offset/Acknowledgment Management

**For Kafka Streams** (offset-based):
1. Produce message to DLQ
2. If DLQ production succeeds OR fails: Commit offset
3. This ensures we don't reprocess the failed message

**For Kafka Queues** (when they arrive):
1. Produce message to DLQ
2. If DLQ production succeeds OR fails: Acknowledge message
3. This ensures we don't reprocess the failed message

The key insight: **rejection means "done with this message"** - whether DLQ production succeeds or fails, we move on.

### Message Metadata for DLQ

When producing to DLQ, we will enrich the message headers with failure metadata:

```csharp
var enrichedMessage = new Message(
    new MessageHeader(
        originalMessage.Header.MessageId,
        originalMessage.Header.Topic,
        originalMessage.Header.MessageType,
        originalMessage.Header.TimeStamp,
        originalMessage.Header.HandledCount,
        originalMessage.Header.DelayedMilliseconds)
    {
        Bag = new Dictionary<string, object>
        {
            ["OriginalTopic"] = _topic,
            ["OriginalPartition"] = message.Partition,
            ["OriginalOffset"] = message.Offset,
            ["RejectionTimestamp"] = DateTimeOffset.UtcNow,
            ["RejectionReason"] = rejectionReason ?? "Unknown",
            ["ConsumerGroup"] = _groupId,
            ["HandlerName"] = GetCurrentHandlerName() // if available
        }
    },
    originalMessage.Body
);
```

### Sync vs Async Producers

Following ADR 0034:
- `KafkaMessageConsumer` (sync) uses `KafkaMessageProducer` (sync)
- `KafkaMessageConsumerAsync` (async) uses `KafkaMessageProducerAsync` (async)

Both implement the same logic, but with appropriate sync/async semantics.

### Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│                  KafkaMessageConsumer                    │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │ Message Processing Loop                            │ │
│  │                                                    │ │
│  │  1. Poll() from Kafka                             │ │
│  │  2. Deserialize message                           │ │
│  │     └─ Failure? → InvalidMessageAction            │ │
│  │  3. Pass to handler                               │ │
│  │     └─ RejectMessageAction thrown?                │ │
│  │        └─ Yes: Produce to DLQ                     │ │
│  │  4. Commit offset                                 │ │
│  └────────────────────────────────────────────────────┘ │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │ DLQ Producer (lazy)                                │ │
│  │                                                    │ │
│  │  _deadLetterProducer: KafkaMessageProducer        │ │
│  │  _invalidMessageProducer: KafkaMessageProducer    │ │
│  │                                                    │ │
│  │  Created on first rejection                       │ │
│  │  Shares consumer configuration                    │ │
│  │  Uses acks=all for reliability                    │ │
│  └────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
                         │
                         ├─ Produce to DLQ Topic
                         ├─ Produce to Invalid Message Topic
                         └─ (bypasses Outbox)
```

## Consequences

### Positive

- **Simple lifecycle**: Consumer owns producer, no complex factory patterns needed
- **Lazy creation**: No overhead if DLQ never used (via `Lazy<T>`)
- **Thread-safe**: `Lazy<T>` ensures thread-safe initialization without explicit locking
- **Configuration consistency**: DLQ producer inherits consumer settings
- **Robustness**: DLQ production failures don't block message processing
- **Topic management**: Auto-creation matches data topic behavior
- **Clean disposal**: `IsValueCreated` check ensures we only dispose created producers

### Negative

- **No retry on DLQ failure**: If DLQ production fails, message metadata is lost
- **Additional connections**: Each consumer creates its own DLQ producer (more connections to Kafka)
- **Resource usage**: DLQ producers consume memory/threads even if rarely used
- **Offset committed regardless**: Even if DLQ production fails, we move on (potential data loss)

### Risks and Mitigations

**Risk**: DLQ topic doesn't exist and `MakeChannels = Validate/Assume`
- **Mitigation**: Fail fast on first rejection with clear error message

**Risk**: DLQ producer creation fails (network, auth)
- **Mitigation**: Log error, let exception propagate to message pump for UnacceptableMessageCount handling

**Risk**: DLQ production consistently fails (topic full, broker down)
- **Mitigation**: Alerting on DLQ production failures via logging/metrics

**Risk**: Too many DLQ producers (one per consumer)
- **Mitigation**: Acceptable tradeoff for simplicity; Kafka handles many connections well

**Risk**: Message loss if DLQ production fails
- **Mitigation**: Log full message content in error log; add OpenTelemetry trace (per ADR 0034)

## Alternatives Considered

### Alternative 1: Shared DLQ Producer via Factory

Create a shared `IAmAMessageProducer` instance via a factory or registry, shared by all consumers.

**Rejected because**:
- Adds complexity (factory, registry, thread-safety)
- Lifetime management becomes unclear (who disposes?)
- Sync/async mismatch issues (factory needs to know sync vs async)
- Minimal benefit (Kafka handles many connections efficiently)

### Alternative 2: Use IAmACommandProcessor for DLQ

Send via `IAmACommandProcessor.Post()` instead of `IAmAMessageProducer`.

**Rejected because**:
- ADR 0034 already decided against this
- Requires deserialize → serialize round-trip
- Cannot handle InvalidMessageAction (deserialization already failed)
- More complex lookup via producer registry

### Alternative 3: Retry DLQ Production on Failure

If DLQ production fails, retry N times before giving up.

**Rejected because**:
- Blocks message processing during retries
- If DLQ is down, blocking makes problem worse
- Error path should fail fast, not block
- Can be added later if needed without breaking changes

### Alternative 4: Write Failed DLQ Messages to Local File

If DLQ production fails, write message to local file for later replay.

**Rejected because**:
- Adds complexity (file I/O, rotation, replay mechanism)
- Operational burden (monitoring disk space, manual replay)
- Out of scope for initial implementation
- Can be added later as enhancement

## References

- Requirements: [specs/0001-kafka-dead-letter-queue/requirements.md](../../specs/0001-kafka-dead-letter-queue/requirements.md)
- Related ADRs:
  - [ADR 0034: Provide DLQ Where Missing](0034-provide-dlq-where-missing.md) - Establishes overall DLQ strategy
- External references:
  - [Kafka Producer API](https://kafka.apache.org/documentation/#producerapi)
  - [Dead Letter Queue Pattern](https://www.enterpriseintegrationpatterns.com/patterns/messaging/DeadLetterChannel.html)
