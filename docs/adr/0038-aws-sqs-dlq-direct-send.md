# 38. AWS SQS DLQ: Replace ChangeMessageVisibility with Direct Send

Date: 2026-02-11

## Status

Accepted

## Context

**Parent Requirement**: [specs/0010-aws-sqs-dead-letter-queue/requirements.md](../../specs/0010-aws-sqs-dead-letter-queue/requirements.md)

**Scope**: This ADR covers SQS-specific implementation decisions for DLQ support. The general DLQ strategy (ADR 0034) and rejection routing logic (ADR 0036) apply unchanged; this ADR addresses only the transport-level concerns unique to SQS.

### Current Behaviour

`SqsMessageConsumer.RejectAsync()` checks a `_hasDlq` flag:
- When true: calls `ChangeMessageVisibilityAsync(receiptHandle, 0)`, making the message immediately visible again and relying on the native SQS redrive policy to eventually move it to the DLQ after `maxReceiveCount` attempts.
- When false: deletes the message.

This is indirect — a `Reject` with `DeliveryError` intent should move the message to the DLQ immediately, not cycle it through re-receives.

### The `_hasDlq` Flag Inversion

The consumer factory currently sets `hasDlq` as:
```csharp
hasDlq: sqsSubscription.QueueAttributes.RedrivePolicy == null
```

This is **inverted**: `_hasDlq` is `true` when there is *no* native redrive policy. The flag actually means "has no native DLQ, so use the visibility trick". This naming is misleading and will be replaced by the new approach.

### Dual Package Constraint

Both `Paramore.Brighter.MessagingGateway.AWSSQS` (v3 SDK) and `Paramore.Brighter.MessagingGateway.AWSSQS.V4` (v4 SDK) have identical reject logic. Both must be updated identically.

## Decision

### 1. Follow the Kafka Pattern

Apply the same pattern established by the Kafka DLQ implementation (spec 0001):

- `SqsSubscription` implements `IUseBrighterDeadLetterSupport` and `IUseBrighterInvalidMessageSupport`
- `SqsMessageConsumerFactory` extracts routing keys and passes them to the consumer
- `SqsMessageConsumer` uses lazy `IAmAMessageProducer` instances for DLQ/Invalid Message channels
- Routing logic follows ADR 0036 (route by `MessageRejectionReason`)
- Messages are enriched with metadata per ADR 0036

### 2. Reject Sends Directly to DLQ Queue, Then Deletes Original

Instead of `ChangeMessageVisibility(0)`, rejection will:

1. Enrich the message with rejection metadata
2. `SendMessage` to the DLQ queue (or invalid message queue, per ADR 0036 routing)
3. `DeleteMessage` from the source queue

This makes rejection immediate and deterministic — no dependency on the redrive policy cycle.

### 3. Coexistence with Native SQS Redrive Policy

Users may have an SQS redrive policy configured independently of Brighter. Two scenarios:

| Brighter DLQ Configured | Native Redrive Policy | Behaviour |
|---|---|---|
| Yes | Any | Brighter sends directly to its configured DLQ queue. Native policy is irrelevant for rejected messages. |
| No | Exists | Message is deleted on reject (current fallback). The native policy only applies to visibility-timeout expiry, not explicit deletes. |
| No | None | Message is deleted on reject, logged as warning. |

**Decision**: Brighter-managed DLQ takes precedence when configured. The `_hasDlq` flag is removed entirely — its role is replaced by the presence or absence of `DeadLetterRoutingKey`.

### 4. DLQ Queue URL Resolution

The DLQ target is an SQS queue identified by a `RoutingKey`. The consumer needs to resolve this to a queue URL. We reuse the existing `EnsureChannel` / `GetQueueUrl` infrastructure that `SqsMessageConsumer` already uses for the source queue. The lazy producer handles this internally.

### 5. Producer Creation

The `SqsMessageConsumer` will create lazy `SqsMessageProducer` instances for DLQ and invalid message channels, following the same pattern as `KafkaMessageConsumer`. 

It needs to use the `SqsMessageProducerFactory` to create a producer, so as to pick up the call to `ConfirmQueueExists` or `ConfirmQueueExistsAsync`. The publication used to create the producer, should inherit the MakeChannels setting from the consumer.  

The producer's sync/async variant matches the consumer per ADR 0034.

### 6. Metadata Enrichment

SQS does not have partition or offset concepts. The enriched metadata will use the subset relevant to SQS:

- `OriginalTopic` — source queue name
- `RejectionTimestamp` — when rejection occurred
- `RejectionReason` — `DeliveryError` or `Unacceptable`
- `RejectionMessage` — description from `MessageRejectionReason`
- `OriginalMessageType` — the message type header

This follows the same `HeaderNames` constants used by the Kafka implementation.

## Consequences

### Positive

- Rejected messages reach the DLQ immediately instead of cycling through re-receives
- Consistent pattern with Kafka — developers familiar with one transport understand the other
- Removes the misleading `_hasDlq` flag
- Enables `IUseBrighterDeadLetterSupport` and `IUseBrighterInvalidMessageSupport` on SQS subscriptions

### Negative

- Users relying on the `ChangeMessageVisibility(0)` + native redrive policy pattern will see a behaviour change when they configure a Brighter DLQ routing key
- Both AWSSQS and AWSSQS.V4 packages must be updated in lockstep

### Risks and Mitigations

**Risk**: Users who depend on native redrive policy counting may be surprised.
- **Mitigation**: Brighter DLQ is opt-in via `DeadLetterRoutingKey`. Without it, behaviour is unchanged (message deleted on reject).

**Risk**: `SendMessage` to DLQ fails, then `DeleteMessage` also fails — message stuck.
- **Mitigation**: Per ADR 0036, log the error and continue. The message becomes visible again after its visibility timeout, which is the same as current behaviour for transient failures.

## Alternatives Considered

### Keep ChangeMessageVisibility(0) and Add Brighter DLQ Alongside

Continue using the visibility trick for native DLQ, and only use direct send for Brighter-managed DLQ.

**Rejected**: The visibility trick is always inferior — it causes re-processing cycles. If a user configures a Brighter DLQ routing key, they want immediate routing.

### Use SQS Message Attributes Instead of Body Enrichment

Put rejection metadata in SQS message attributes rather than the message header bag.

**Rejected**: Keeping metadata in the Brighter `MessageHeader.Bag` is consistent with the Kafka pattern and means DLQ consumers use the same deserialization path. SQS message attributes would require transport-specific handling.

## References

- [ADR 0034: Provide DLQ Where Missing](0034-provide-dlq-where-missing.md) — overall DLQ strategy
- [ADR 0036: Message Rejection Routing Strategy](0036-message-rejection-routing-strategy.md) — routing logic and metadata enrichment
- [Spec 0001: Kafka Dead Letter Queue](../../specs/0001-kafka-dead-letter-queue/) — reference implementation
- Requirements: [specs/0010-aws-sqs-dead-letter-queue/requirements.md](../../specs/0010-aws-sqs-dead-letter-queue/requirements.md)
