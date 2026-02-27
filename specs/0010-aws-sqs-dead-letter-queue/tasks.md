# Tasks: AWS SQS Dead Letter Queue (Spec 0010)

**Requirements**: [requirements.md](requirements.md)
**Design**: [ADR 0034](../../docs/adr/0034-provide-dlq-where-missing.md), [ADR 0036](../../docs/adr/0036-message-rejection-routing-strategy.md), [ADR 0038](../../docs/adr/0038-aws-sqs-dlq-direct-send.md)
**Reference Implementation**: [Spec 0001: Kafka DLQ](../0001-kafka-dead-letter-queue/)

## Packages

Every task applies to **both** packages identically:
- `Paramore.Brighter.MessagingGateway.AWSSQS` (AWS SDK v3)
- `Paramore.Brighter.MessagingGateway.AWSSQS.V4` (AWS SDK v4)

Test projects:
- `tests/Paramore.Brighter.AWS.Tests/`
- `tests/Paramore.Brighter.AWS.V4.Tests/`

## Prerequisites

- [x] **TIDY: Remove `_hasDlq` flag and prepare consumer for DLQ routing keys**
  - **USE COMMAND**: `/tidy-first Remove _hasDlq flag from SqsMessageConsumer and replace with routing key parameters`
  - This is a structural change — no behaviour change yet
  - In both `SqsMessageConsumer` constructors:
    - Replace `bool hasDlq = false` parameter with `RoutingKey? deadLetterRoutingKey = null` and `RoutingKey? invalidMessageRoutingKey = null`
    - Replace `private readonly bool _hasDlq` field with `private readonly RoutingKey? _deadLetterRoutingKey` and `private readonly RoutingKey? _invalidMessageRoutingKey`
    - Add `AWSMessagingGatewayConnection connection` parameter and store it — needed later for creating DLQ producers via `SqsMessageProducerFactory`
    - Add `OnMissingChannel makeChannels = OnMissingChannel.Create` parameter and store it — DLQ publication inherits this setting (per ADR 0038 §5)
    - In `RejectAsync()`: replace `if (_hasDlq)` with `if (_deadLetterRoutingKey != null)` — keep the existing `ChangeMessageVisibility` behaviour for now so tests still pass
  - In both `SqsMessageConsumerFactory.CreateImpl()`:
    - Replace `hasDlq: sqsSubscription.QueueAttributes.RedrivePolicy == null` with `deadLetterRoutingKey: null, invalidMessageRoutingKey: null` (wired up in next task)
    - Pass `_awsConnection` through to the consumer constructor
    - Pass `sqsSubscription.MakeChannels` through to the consumer constructor
  - All existing tests must continue to pass — this is a pure structural refactor

## Tasks

- [x] **TEST + IMPLEMENT: SqsSubscription exposes DLQ and invalid message routing keys**
  - **USE COMMAND**: `/test-first when creating SQS subscription with dead letter and invalid message routing keys should expose properties`
  - Test location: `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/Sqs/`
  - Test file: `When_creating_sqs_subscription_with_dlq_routing_keys_should_expose_properties.cs`
  - Test should verify:
    - `SqsSubscription` can be constructed with `deadLetterRoutingKey` and `invalidMessageRoutingKey` parameters
    - The subscription can be cast to `IUseBrighterDeadLetterSupport` and `DeadLetterRoutingKey` returns the configured value
    - The subscription can be cast to `IUseBrighterInvalidMessageSupport` and `InvalidMessageRoutingKey` returns the configured value
    - Both properties are null when not provided (backward compatible)
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - `SqsSubscription` implements `IUseBrighterDeadLetterSupport` and `IUseBrighterInvalidMessageSupport`
    - Add `RoutingKey? DeadLetterRoutingKey { get; set; }` and `RoutingKey? InvalidMessageRoutingKey { get; set; }` properties
    - Add optional constructor parameters `deadLetterRoutingKey` and `invalidMessageRoutingKey`
    - Apply identically to both AWSSQS and AWSSQS.V4 packages
  - Duplicate test for V4: `tests/Paramore.Brighter.AWS.V4.Tests/MessagingGateway/Sqs/`

- [x] **TEST + IMPLEMENT: Consumer factory passes DLQ routing keys from subscription to consumer**
  - **USE COMMAND**: `/test-first when creating SQS consumer from subscription with DLQ routing keys should pass them to consumer`
  - Test location: `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/Sqs/`
  - Test file: `When_creating_sqs_consumer_with_dlq_subscription_should_pass_routing_keys.cs`
  - Test should verify:
    - Create an `SqsSubscription` with `deadLetterRoutingKey` set
    - Create consumer via `SqsMessageConsumerFactory.Create()`
    - The consumer receives the routing key (may need to verify via behaviour in later tasks, or via reflection/internal state if necessary)
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `SqsMessageConsumerFactory.CreateImpl()`, extract routing keys from subscription using `IUseBrighterDeadLetterSupport` / `IUseBrighterInvalidMessageSupport` interface checks (same pattern as `KafkaMessageConsumerFactory`)
    - Pass extracted keys, `_awsConnection`, and `sqsSubscription.MakeChannels` to `SqsMessageConsumer` constructor
    - Apply identically to both AWSSQS and AWSSQS.V4 packages
  - Duplicate test for V4: `tests/Paramore.Brighter.AWS.V4.Tests/MessagingGateway/Sqs/`

- [x] **TEST + IMPLEMENT: Rejecting a message with DeliveryError sends it to the DLQ queue**
  - **USE COMMAND**: `/test-first when rejecting SQS message with delivery error should send to dead letter queue`
  - Test location: `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/Sqs/Standard/Reactor/`
  - Test file: `When_rejecting_message_with_delivery_error_should_send_to_dlq.cs`
  - Test should verify:
    - Create source queue and DLQ queue
    - Configure `SqsSubscription` with `deadLetterRoutingKey` pointing to DLQ queue
    - Send a message to source queue, consume it
    - Reject with `MessageRejectionReason` of `DeliveryError`
    - Consume from DLQ queue — the rejected message should appear there
    - The DLQ message should contain rejection metadata: `OriginalTopic`, `RejectionReason`, `RejectionTimestamp`, `OriginalMessageType`
    - The original message should be deleted from the source queue
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `SqsMessageConsumer`, add lazy `IAmAMessageProducer` fields for DLQ and invalid message (following Kafka pattern)
    - Add `CreateDeadLetterProducer()` / `CreateInvalidMessageProducer()` methods that use `SqsMessageProducerFactory` to create producers — this ensures `ConfirmQueueExists` is called (per ADR 0038 §5)
    - Build an `SqsPublication` for the DLQ with `MakeChannels` inherited from the consumer's stored setting
    - In `RejectAsync()`, replace `ChangeMessageVisibility(0)` with: enrich metadata → send to DLQ producer → delete original message
    - Add `RefreshMetadata()` method using `HeaderNames` constants (same as Kafka)
    - Apply identically to both AWSSQS and AWSSQS.V4 packages
  - Duplicate test for V4: `tests/Paramore.Brighter.AWS.V4.Tests/MessagingGateway/Sqs/Standard/Reactor/`

- [x] **TEST + IMPLEMENT: Rejecting a message with Unacceptable reason sends it to the invalid message queue**
  - **USE COMMAND**: `/test-first when rejecting SQS message with unacceptable reason should send to invalid message queue`
  - Test location: `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/Sqs/Standard/Reactor/`
  - Test file: `When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel.cs`
  - Test should verify:
    - Configure `SqsSubscription` with both `deadLetterRoutingKey` and `invalidMessageRoutingKey`
    - Send a message, consume it, reject with `Unacceptable` reason
    - Message should appear on the invalid message queue (not the DLQ)
    - Original message should be deleted from source queue
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In the routing logic of `RejectAsync()`, check rejection reason and route `Unacceptable` to `_invalidMessageProducer` (per ADR 0036)
    - Apply identically to both AWSSQS and AWSSQS.V4 packages
  - Duplicate test for V4: `tests/Paramore.Brighter.AWS.V4.Tests/MessagingGateway/Sqs/Standard/Reactor/`

- [x] **TEST + IMPLEMENT: Rejecting with Unacceptable reason falls back to DLQ when no invalid message queue configured**
  - **USE COMMAND**: `/test-first when rejecting SQS message with unacceptable reason and no invalid channel should fallback to dlq`
  - Test location: `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/Sqs/Standard/Reactor/`
  - Test file: `When_rejecting_message_with_unacceptable_and_no_invalid_channel_should_fallback_to_dlq.cs`
  - Test should verify:
    - Configure `SqsSubscription` with `deadLetterRoutingKey` only (no `invalidMessageRoutingKey`)
    - Send a message, consume it, reject with `Unacceptable` reason
    - Message should appear on the DLQ (fallback)
    - Original message should be deleted from source queue
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `RejectAsync()`, when reason is `Unacceptable` and `_invalidMessageProducer` is null, fall back to `_deadLetterProducer` (per ADR 0036 routing decision tree)
    - Apply identically to both AWSSQS and AWSSQS.V4 packages
  - Duplicate test for V4: `tests/Paramore.Brighter.AWS.V4.Tests/MessagingGateway/Sqs/Standard/Reactor/`

- [x] **TEST + IMPLEMENT: Rejecting a message with no DLQ or invalid message queue configured acknowledges and logs**
  - **USE COMMAND**: `/test-first when rejecting SQS message with no channels configured should acknowledge and log warning`
  - Test location: `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/Sqs/Standard/Reactor/`
  - Test file: `When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log.cs`
  - Test should verify:
    - Configure `SqsSubscription` with no `deadLetterRoutingKey` and no `invalidMessageRoutingKey`
    - Send a message, consume it, reject with `DeliveryError` reason
    - Reject returns true
    - Original message is deleted from the source queue (acknowledged)
    - No message appears on any DLQ
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `RejectAsync()`, when both producers are null, log warning and delete the original message
    - Apply identically to both AWSSQS and AWSSQS.V4 packages
  - Duplicate test for V4: `tests/Paramore.Brighter.AWS.V4.Tests/MessagingGateway/Sqs/Standard/Reactor/`

- [x] **TEST + IMPLEMENT: Rejecting a message with DeliveryError sends to DLQ (async/Proactor)**
  - **USE COMMAND**: `/test-first when rejecting SQS message async with delivery error should send to dead letter queue`
  - Test location: `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/Sqs/Standard/Proactor/`
  - Test file: `When_rejecting_message_with_delivery_error_should_send_to_dlq_async.cs`
  - Test should verify:
    - Same as the Reactor DeliveryError test but using async consumer path
    - Message appears on DLQ with metadata
    - Original message deleted
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Verify the async path in `RejectAsync()` works end-to-end (should already work from earlier tasks since `SqsMessageConsumer` uses a single `RejectAsync` for both sync and async paths)
    - Apply identically to both AWSSQS and AWSSQS.V4 packages
  - Duplicate test for V4: `tests/Paramore.Brighter.AWS.V4.Tests/MessagingGateway/Sqs/Standard/Proactor/`

## Verification

- [x] **Run full SQS test suite to verify no regressions**
  - Run all existing tests in `Paramore.Brighter.AWS.Tests` and `Paramore.Brighter.AWS.V4.Tests`
  - Verify existing requeue/redrive tests still pass
  - Verify new DLQ tests pass for both packages
