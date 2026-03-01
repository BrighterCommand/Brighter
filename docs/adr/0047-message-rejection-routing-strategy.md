# 47. Message Rejection Routing Strategy

Date: 2026-01-11

## Status

Accepted

## Context

**Parent Requirement**: [specs/0001-kafka-dead-letter-queue/requirements.md](../../specs/0001-kafka-dead-letter-queue/requirements.md)

**Scope**: This ADR focuses on how the message consumer's `Reject()` method routes rejected messages to the appropriate channel (dead letter queue vs invalid message channel) based on the rejection reason.

### The Problem

ADR 0034 introduced `RejectMessageAction` and `InvalidMessageAction` exceptions, and ADR 0035 established that producers for both DLQ and invalid message channels can exist. Now we need to decide:

1. **Routing Logic**: How does `Reject()` decide which channel to use?
2. **Rejection Reasons**: What are the different rejection reasons and what do they mean?
3. **Fallback Behavior**: What happens if invalid message channel doesn't exist but DLQ does?
4. **Message Enrichment**: What metadata should be added to rejected messages?
5. **Acknowledgment**: When do we acknowledge the message (before or after channel production)?
6. **No Channels**: What happens if neither channel is configured?

### Requirements Context

From the requirements:
- "When a handler throws a custom exception, the message should be sent to a configured dead letter topic"
- "When a message mapper cannot deserialize a message, the message should be sent to either a configured invalid message topic, or if that is not present any dead letter topic"
- Failed messages should include error details and metadata

### Constraints

- Must use producers created in ADR 0035 (lazy `Lazy<T>` instances)
- Must support both sync and async paths
- Must handle the case where either or both channels are missing
- Must maintain compatibility with ADR 0034's exception types

## Decision

### Rejecting a Message with a DLQ or Invalid Message Channel

We will use the existing `MessageRejectionReason` enum to classify why a message is being rejected.:

- `MessageRejectionReason.DeliveryError` in which case we should reject to the `_deadLetterProducer`
- `MessageRejectionReason.Unacceptable` in which case we should reject to the `_invalidMessageProducer`

We should then reject to the appropriate producer. Note that if there is no `_invalidMessageProducer` and the reason
is `MessageRejectionReason.Unacceptable` but there is a  `_deadLetterProducer` then we should use the  
`_deadLetterProducer` for that message instead.

### Routing Decision Logic

The `Reject()` method will route messages based on this decision tree:

```
┌─────────────────────────────────────────────────────────────┐
│           Message Rejected (Reject() called)                 │
└─────────────────────────────────────────────────────────────┘
                          │
                          ▼
            ┌─────────────────────────────┐
            │ Check if any channel exists │
            └─────────────────────────────┘
                          │
        ┌─────────────────┴─────────────────┐
        ▼                                   ▼
   [No Channels]                    [Channels Exist]
        │                                   │
        ▼                                   ▼
   Acknowledge()              ┌─────────────────────────┐
   return true                │ Check RejectionReason   │
                              └─────────────────────────┘
                                          │
              ┌───────────────────────────┼───────────────────────────┐
              ▼                           ▼                           ▼
    [Unacceptable]              [DeliveryError]                [Unknown]
              │                           │                           │
              ▼                           ▼                           ▼
   ┌──────────────────┐      ┌──────────────────┐      ┌──────────────────┐
   │ Invalid channel  │      │ DLQ channel      │      │ Treat as         │
   │ configured?      │      │ configured?      │      │ DeliveryError    │
   └──────────────────┘      └──────────────────┘      └──────────────────┘
       │           │              │           │                    │
       ▼           ▼              ▼           ▼                    ▼
    [Yes]       [No]           [Yes]       [No]            [DLQ or Ack]
       │           │              │           │
       ▼           ▼              ▼           ▼
   Send to    Fallback      Send to      Acknowledge
   Invalid    to DLQ?       DLQ          (no-op)
   Channel        │          Channel
       │          ▼          │
       │      [DLQ exists?]  │
       │          │          │
       │     ┌────┴────┐     │
       │     ▼         ▼     │
       │  [Yes]     [No]     │
       │     │         │     │
       │     ▼         ▼     │
       │  Send to   Ack      │
       │  DLQ              │
       │     │         │     │
       └─────┴─────────┴─────┘
                │
                ▼
         Acknowledge()
         return true
```

### Error Handling for DLQ Production Failures

If producing to the DLQ fails, we have a critical decision: what do we do with the original message?

**Decision**: Log the error and **continue processing** (commit offset for streams, ack for queues).

**Rationale**:
1. The original message has already failed processing
2. Blocking the consumer on DLQ failure creates a cascading failure
3. Logging + alerting on DLQ production failures is sufficient
4. The message is "lost" but the system continues

**Note**: Factory methods in ADR 0035 can return null when routing key not configured, so .Value? null-conditional 
is intentional

### Implementation

```csharp
public bool Reject(Message message, MessageRejectionReason? reason = null)
{
    // No channels configured - just acknowledge
    if (_deadLetterProducer is null && _invalidMessageProducer is null)
    {
        _logger.LogWarning(
            "Message rejected but no DLQ or invalid message channel configured. " +
            "Message will be acknowledged and lost. Offset: {Offset}, Topic: {Topic}",
            message.Offset, _topic);
        Acknowledge(message);
        return true;
    }

    var enrichedMessage = EnrichMessageWithMetadata(message, reason);
    var rejectionReason = reason ?? MessageRejectionReason.Unknown;

    try
    {
        // Route based on rejection reason
        if (rejectionReason == MessageRejectionReason.Unacceptable)
        {
            // Try invalid message channel first
            if (_invalidMessageProducer is not null)
            {
                _invalidMessageProducer.Value?.Send(enrichedMessage);
                _logger.LogInformation(
                    "Message sent to invalid message channel. " +
                    "Offset: {Offset}, Topic: {Topic}, Reason: {Reason}",
                    message.Offset, _topic, reason);
                Acknowledge(message);
                return true;
            }

            // Fallback to DLQ if invalid channel not configured
            if (_deadLetterProducer is not null)
            {
                _deadLetterProducer.Value?.Send(enrichedMessage);
                _logger.LogInformation(
                    "Invalid message sent to DLQ (no invalid message channel configured). " +
                    "Offset: {Offset}, Topic: {Topic}",
                    message.Offset, _topic);
                Acknowledge(message);
                return true;
            }
        }
        else // DeliveryError or Unknown
        {
            if (_deadLetterProducer is not null)
            {
                _deadLetterProducer.Value?.Send(enrichedMessage);
                _logger.LogInformation(
                    "Message sent to dead letter queue. " +
                    "Offset: {Offset}, Topic: {Topic}, Reason: {Reason}",
                    message.Offset, _topic, reason);
                Acknowledge(message);
                return true;
            }
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex,
            "Failed to produce message to error channel. " +
            "Message will be acknowledged anyway. " +
            "Offset: {Offset}, Topic: {Topic}, Reason: {Reason}",
            message.Offset, _topic, reason);
    }

    // If we get here, no appropriate channel was available
    // Acknowledge anyway to avoid reprocessing
    Acknowledge(message);
    return true;
}
```

### Message Metadata Enrichment

When rejecting a message, we enrich it with metadata about the rejection:

```csharp
private Message EnrichMessageWithMetadata(Message message, MessageRejectionReason? reason)
{
    var enrichedHeader = new MessageHeader(
        message.Header.MessageId,
        message.Header.Topic,
        message.Header.MessageType,
        message.Header.TimeStamp,
        message.Header.HandledCount,
        message.Header.DelayedMilliseconds)
    {
        Bag = new Dictionary<string, object>(message.Header.Bag)
        {
            ["OriginalTopic"] = _topic.Value,
            ["OriginalPartition"] = GetCurrentPartition(),
            ["OriginalOffset"] = GetCurrentOffset(),
            ["RejectionTimestamp"] = DateTimeOffset.UtcNow,
            ["RejectionReason"] = reason?.ToString() ?? "Unknown",
            ["ConsumerGroup"] = _groupId,
            ["RedeliveryCount"] = message.Header.HandledCount
        }
    };

    return new Message(enrichedHeader, message.Body);
}
```

**Metadata Fields**:
- `OriginalTopic`: Where the message came from
- `OriginalPartition`: Original partition (for Kafka)
- `OriginalOffset`: Original offset (for Kafka)
- `RejectionTimestamp`: When rejection occurred
- `RejectionReason`: Why message was rejected (DeliveryError/Unacceptable/Unknown)
- `ConsumerGroup`: Which consumer rejected it
- `RedeliveryCount`: How many times message was attempted (from `HandledCount`)

We will need to implement `GetCurrentPartition()` and `GetCurrentOffset` to complete the metadata.

### Acknowledgment Timing

**Decision**: Acknowledge **after** successful channel production, but acknowledge **anyway** on production failure.
The key insight: **rejection means "done with this message"** - whether DLQ production succeeds or fails, we move on.

**Rationale**:
1. Try to produce to error channel
2. If successful: acknowledge (message is safe)
3. If failed: acknowledge anyway (avoid infinite reprocessing)
4. Log production failures for alerting

This ensures we don't reprocess failed messages, even if error channel production fails.

### No Channels Configured

If neither DLQ nor invalid message channel is configured:
- Log warning with message details
- Acknowledge the message
- Message is "lost" (but logged for investigation)

**Rationale**: Better to continue processing than to block on rejected messages when no error handling is configured.

## Consequences

### Positive

- **Clear routing logic**: Explicit rules for which channel to use
- **Fallback behavior**: Invalid messages can go to DLQ if no invalid channel
- **Rich metadata**: Rejected messages include context for investigation
- **Resilient**: Production failures don't block message processing
- **Auditable**: All rejections are logged with reason and metadata

### Negative

- **Message loss possible**: If error channel production fails, message is lost (but logged)
- **No retry**: Error channel production is not retried on failure
- **Fallback may be unexpected**: Users might not realize invalid messages can go to DLQ

### Risks and Mitigations

**Risk**: Users don't realize invalid messages fallback to DLQ
- **Mitigation**: Log info message when fallback occurs; document behavior clearly

**Risk**: Production failures cause message loss
- **Mitigation**: Log full message content; add OpenTelemetry traces; alert on production failures

**Risk**: Unknown rejection reason is treated as DeliveryError
- **Mitigation**: Log warning when reason is unknown; document to always provide reason

**Risk**: No channels configured leads to silent message loss
- **Mitigation**: Log warning with message details; consider making DLQ channel mandatory in future

## Alternatives Considered

### Alternative 1: Mandatory Invalid Message Channel

Require both DLQ and invalid message channels to be configured.

**Rejected because**:
- Reduces flexibility
- Some users may want single error channel
- Fallback to DLQ is reasonable default
- Can add validation warning in future without breaking change

### Alternative 2: Retry Error Channel Production

If production to error channel fails, retry N times before giving up.

**Rejected because**:
- ADR 0035 decided against retries (blocks processing)
- Error path should fail fast
- Logging + alerting is sufficient
- Can be added later if needed

### Alternative 3: Different Acknowledgment Strategy

Acknowledge before producing to error channel.

**Rejected because**:
- If production succeeds, message is safe to acknowledge
- Acknowledging before production could lose message on crash
- Current approach (after production) is safer

### Alternative 4: Throw Exception on No Channels

Throw exception if rejecting but no channels configured.

**Rejected because**:
- Would block message processing
- Better to log warning and continue
- Users may intentionally not configure channels
- Avoids "poison pill" blocking entire consumer

### Alternative 5: Always Use DLQ, Never Invalid Channel

Remove invalid message channel concept; use DLQ for everything.

**Rejected because**:
- ADR 0034 established both channels
- Separate channels allow different retention/monitoring
- Deserialization failures are different from processing failures
- Requirements explicitly mention invalid message channel

## References

- Requirements: [specs/0001-kafka-dead-letter-queue/requirements.md](../../specs/0001-kafka-dead-letter-queue/requirements.md)
- Related ADRs:
  - [ADR 0045: Provide DLQ Where Missing](0045-provide-dlq-where-missing.md) - Establishes DLQ strategy and exception types
  - [ADR 0046: Kafka DLQ Producer for Requeue](0046-kafka-dlq-producer-for-requeue.md) - Producer lifecycle and topic creation
- External references:
  - [Dead Letter Channel Pattern](https://www.enterpriseintegrationpatterns.com/patterns/messaging/DeadLetterChannel.html)
  - [Invalid Message Channel Pattern](https://www.enterpriseintegrationpatterns.com/patterns/messaging/InvalidMessageChannel.html)
