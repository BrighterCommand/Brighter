# Tasks: RocketMQ Dead Letter Queue (Spec 0014)

**Requirements**: [requirements.md](requirements.md)
**Design**: [ADR 0034](../../docs/adr/0034-provide-dlq-where-missing.md), [ADR 0036](../../docs/adr/0036-message-rejection-routing-strategy.md), [ADR 0042](../../docs/adr/0042-rocketmq-dlq-brighter-managed.md)
**Reference Implementation**: [Spec 0001: Kafka DLQ](../0001-kafka-dead-letter-queue/), [Spec 0013: PostgreSQL DLQ](../0013-postgres-dead-letter-queue/)

## Package

All tasks apply to:
- `src/Paramore.Brighter.MessagingGateway.RocketMQ/`

Test project:
- `tests/Paramore.Brighter.RocketMQ.Tests/`

## Prerequisites

- [x] Spec 0010 (AWS SQS) complete
- [x] Spec 0011 (Redis) complete
- [x] Spec 0012 (MsSql) complete
- [x] Spec 0013 (PostgreSQL) complete

## Key Difference from PostgreSQL/MsSql

RocketMQ is a **broker-based** transport (not relational). Producer creation is **async** (`Producer.Builder().Build()` connects to the broker over the network). Source message cleanup uses `Ack(MessageView)` rather than SQL DELETE. The `ReceiptHandle` is the `MessageView` object stored in the header bag. See ADR 0042 for full details.

## Tasks

- [x] **TEST + IMPLEMENT: RocketSubscription exposes DLQ and invalid message routing keys**
  - **USE COMMAND**: `/test-first when creating RocketMQ subscription with dead letter and invalid message routing keys should expose properties`
  - Test location: `tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/`
  - Test file: `When_creating_rocket_subscription_with_dlq_routing_keys_should_expose_properties.cs`
  - Test should verify:
    - `RocketSubscription` can be constructed with `deadLetterRoutingKey` and `invalidMessageRoutingKey` parameters
    - The subscription can be cast to `IUseBrighterDeadLetterSupport` and `DeadLetterRoutingKey` returns the configured value
    - The subscription can be cast to `IUseBrighterInvalidMessageSupport` and `InvalidMessageRoutingKey` returns the configured value
    - Both properties are null when not provided (backward compatible)
    - Generic `RocketMqSubscription<T>` also supports the new parameters
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - `RocketSubscription` implements `IUseBrighterDeadLetterSupport` and `IUseBrighterInvalidMessageSupport`
    - Add `RoutingKey? DeadLetterRoutingKey { get; set; }` and `RoutingKey? InvalidMessageRoutingKey { get; set; }` properties
    - Add optional constructor parameters `deadLetterRoutingKey` and `invalidMessageRoutingKey` to both `RocketSubscription` and `RocketMqSubscription<T>`

- [x] **TEST + IMPLEMENT: Consumer factory passes DLQ routing keys and connection from subscription to consumer**
  - **USE COMMAND**: `/test-first when creating RocketMQ consumer from subscription with DLQ routing keys should pass them to consumer`
  - Test location: `tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/`
  - Test file: `When_creating_rocket_consumer_with_dlq_subscription_should_pass_routing_keys.cs`
  - Test should verify:
    - Create a `RocketMqSubscription<T>` with `deadLetterRoutingKey` and `invalidMessageRoutingKey` set
    - Create consumer via `RocketMessageConsumerFactory.Create()`
    - The consumer receives the routing keys and connection (verify via reflection on private fields)
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `RocketMessageConsumerFactory.CreateConsumerAsync()`, extract routing keys from subscription using `IUseBrighterDeadLetterSupport` / `IUseBrighterInvalidMessageSupport` interface checks
    - Pass extracted keys and the `RocketMessagingGatewayConnection` to `RocketMessageConsumer` constructor
    - Add new constructor parameters to `RocketMessageConsumer`: `RocketMessagingGatewayConnection? connection = null`, `RoutingKey? deadLetterRoutingKey = null`, `RoutingKey? invalidMessageRoutingKey = null`

- [x] **TEST + IMPLEMENT: Rejecting a message with DeliveryError sends it to the DLQ channel and Acks the source**
  - **USE COMMAND**: `/test-first when rejecting RocketMQ message with delivery error should send to dead letter queue`
  - Test location: `tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/`
  - Test file: `When_rejecting_message_with_delivery_error_should_send_to_dlq.cs`
  - Test should verify:
    - Create source topic consumer with `deadLetterRoutingKey` pointing to a DLQ topic
    - Create a separate DLQ consumer to read the DLQ topic
    - Send a message to source topic, consume it
    - Reject with `MessageRejectionReason` of `DeliveryError`
    - Consume from DLQ topic — the rejected message should appear there
    - The DLQ message should contain rejection metadata in the header bag: `originalTopic`, `rejectionReason`, `rejectionTimestamp`, `originalMessageType`
    - `Reject()` returns `true`
    - Source message is no longer available (Ack'd)
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `_deadLetterRoutingKey`, `_invalidMessageRoutingKey`, `_connection` fields
    - Add nullable `_deadLetterProducer` and `_invalidMessageProducer` fields
    - Add async `GetDeadLetterProducerAsync()` / `GetInvalidMessageProducerAsync()` methods that lazily create `RocketMqMessageProducer` instances via `Producer.Builder().Build()`
    - Add `RefreshMetadata()` method adding `originalTopic`, `rejectionTimestamp`, `originalMessageType`, `rejectionReason`, `rejectionMessage` to the message bag
    - Add `DetermineRejectionRoute()` method implementing ADR 0036 routing decision tree
    - Add `AckSourceMessage()` / `AckSourceMessageAsync()` helpers that call `consumer.Ack(view)`
    - In `Reject()`: extract `MessageView` first, enrich metadata, get lazy producer, send to DLQ, then `Ack` source in `finally` block, return `true`
    - In `RejectAsync()`: fully async path using `SendAsync` and `AckSourceMessageAsync`

- [x] **TEST + IMPLEMENT: Rejecting a message with Unacceptable reason sends it to the invalid message channel**
  - **USE COMMAND**: `/test-first when rejecting RocketMQ message with unacceptable reason should send to invalid message channel`
  - Test location: `tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/`
  - Test file: `When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel.cs`
  - Test should verify:
    - Configure consumer with both `deadLetterRoutingKey` and `invalidMessageRoutingKey`
    - Send a message, consume it, reject with `Unacceptable` reason
    - Message should appear on the invalid message channel (not the DLQ)
    - DLQ channel should be empty
    - Source message is Ack'd
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `DetermineRejectionRoute()`, route `Unacceptable` to `_invalidMessageProducer` when available (per ADR 0036)

- [x] **TEST + IMPLEMENT: Rejecting with Unacceptable reason falls back to DLQ when no invalid message channel configured**
  - **USE COMMAND**: `/test-first when rejecting RocketMQ message with unacceptable reason and no invalid channel should fallback to dlq`
  - Test location: `tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/`
  - Test file: `When_rejecting_message_with_unacceptable_and_no_invalid_channel_should_fallback_to_dlq.cs`
  - Test should verify:
    - Configure consumer with `deadLetterRoutingKey` only (no `invalidMessageRoutingKey`)
    - Send a message, consume it, reject with `Unacceptable` reason
    - Message should appear on the DLQ (fallback)
    - Source message is Ack'd
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `DetermineRejectionRoute()`, when reason is `Unacceptable` and `_invalidMessageProducer` is null, fall back to `_deadLetterProducer`

- [x] **TEST + IMPLEMENT: Rejecting a message with no DLQ or invalid message channel configured Acks source and logs warning**
  - **USE COMMAND**: `/test-first when rejecting RocketMQ message with no channels configured should ack source and log warning`
  - Test location: `tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/`
  - Test file: `When_rejecting_message_with_no_channels_configured_should_ack_and_log_warning.cs`
  - Test should verify:
    - Configure consumer with no `deadLetterRoutingKey` and no `invalidMessageRoutingKey`
    - Send a message, consume it, reject with `DeliveryError` reason
    - Reject returns `true`
    - Source message is Ack'd (no longer redelivered — breaking the requeue loop)
    - Consumer can continue to receive subsequent messages
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `Reject()`/`RejectAsync()`, when both producers are null, log warning, Ack source message, return `true`

- [x] **TEST + IMPLEMENT: Rejecting a message with DeliveryError sends to DLQ (async/Proactor)**
  - **USE COMMAND**: `/test-first when rejecting RocketMQ message async with delivery error should send to dead letter queue`
  - Test location: `tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/`
  - Test file: `When_rejecting_message_with_delivery_error_should_send_to_dlq_async.cs`
  - Test should verify:
    - Same as the sync DeliveryError test but using async consumer path (`ReceiveAsync`, `RejectAsync`)
    - Message appears on DLQ with metadata
    - Source message is Ack'd
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Verify `RejectAsync()` uses full async path (not delegating to sync `Reject()`)
    - Use `SendAsync` on the lazy producer
    - Use `AckSourceMessageAsync()` for the source acknowledgement

## Verification

- [ ] **Run full RocketMQ test suite to verify no regressions**
  - Run all existing tests in `Paramore.Brighter.RocketMQ.Tests`
  - Verify existing requeue/purge/send tests still pass
  - Verify new DLQ tests pass
