# Tasks: Redis Dead Letter Queue (Spec 0011)

**Requirements**: [requirements.md](requirements.md)
**Design**: [ADR 0034](../../docs/adr/0034-provide-dlq-where-missing.md), [ADR 0036](../../docs/adr/0036-message-rejection-routing-strategy.md), [ADR 0039](../../docs/adr/0039-redis-dlq-brighter-managed.md)
**Reference Implementation**: [Spec 0001: Kafka DLQ](../0001-kafka-dead-letter-queue/), [Spec 0010: SQS DLQ](../0010-aws-sqs-dead-letter-queue/)

## Package

All tasks apply to:
- `src/Paramore.Brighter.MessagingGateway.Redis/`

Test project:
- `tests/Paramore.Brighter.Redis.Tests/`

## Prerequisites

- [x] Spec 0010 (AWS SQS) complete — confirms pattern generalises across transports

## Tasks

- [x] **TEST + IMPLEMENT: RedisSubscription exposes DLQ and invalid message routing keys**
  - **USE COMMAND**: `/test-first when creating Redis subscription with dead letter and invalid message routing keys should expose properties`
  - Test location: `tests/Paramore.Brighter.Redis.Tests/MessagingGateway/`
  - Test file: `When_creating_redis_subscription_with_dlq_routing_keys_should_expose_properties.cs`
  - Test should verify:
    - `RedisSubscription` can be constructed with `deadLetterRoutingKey` and `invalidMessageRoutingKey` parameters
    - The subscription can be cast to `IUseBrighterDeadLetterSupport` and `DeadLetterRoutingKey` returns the configured value
    - The subscription can be cast to `IUseBrighterInvalidMessageSupport` and `InvalidMessageRoutingKey` returns the configured value
    - Both properties are null when not provided (backward compatible)
    - Generic `RedisSubscription<T>` also supports the new parameters
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - `RedisSubscription` implements `IUseBrighterDeadLetterSupport` and `IUseBrighterInvalidMessageSupport`
    - Add `RoutingKey? DeadLetterRoutingKey { get; set; }` and `RoutingKey? InvalidMessageRoutingKey { get; set; }` properties
    - Add optional constructor parameters `deadLetterRoutingKey` and `invalidMessageRoutingKey` to both `RedisSubscription` and `RedisSubscription<T>`

- [x] **TEST + IMPLEMENT: Consumer factory passes DLQ routing keys from subscription to consumer**
  - **USE COMMAND**: `/test-first when creating Redis consumer from subscription with DLQ routing keys should pass them to consumer`
  - Test location: `tests/Paramore.Brighter.Redis.Tests/MessagingGateway/`
  - Test file: `When_creating_redis_consumer_with_dlq_subscription_should_pass_routing_keys.cs`
  - Test should verify:
    - Create a `RedisSubscription` with `deadLetterRoutingKey` and `invalidMessageRoutingKey` set
    - Create consumer via `RedisMessageConsumerFactory.Create()`
    - The consumer receives the routing keys (verify via reflection on private fields, same pattern as Kafka/SQS factory tests)
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `RedisMessageConsumerFactory.Create()` and `CreateAsync()`, extract routing keys from subscription using `IUseBrighterDeadLetterSupport` / `IUseBrighterInvalidMessageSupport` interface checks
    - Pass `_configuration` and extracted keys to `RedisMessageConsumer` constructor
    - Add new constructor parameters to `RedisMessageConsumer`: `RedisMessagingGatewayConfiguration configuration` (stored for DLQ producer creation), `RoutingKey? deadLetterRoutingKey = null`, `RoutingKey? invalidMessageRoutingKey = null`

- [x] **TEST + IMPLEMENT: Rejecting a message with DeliveryError sends it to the DLQ channel**
  - **USE COMMAND**: `/test-first when rejecting Redis message with delivery error should send to dead letter queue`
  - Test location: `tests/Paramore.Brighter.Redis.Tests/MessagingGateway/Reactor/`
  - Test file: `When_rejecting_message_with_delivery_error_should_send_to_dlq.cs`
  - Test should verify:
    - Create source topic consumer with `deadLetterRoutingKey` pointing to a DLQ topic
    - Create a separate DLQ consumer to read the DLQ topic
    - Send a message to source topic, consume it
    - Reject with `MessageRejectionReason` of `DeliveryError`
    - Consume from DLQ topic — the rejected message should appear there
    - The DLQ message should contain rejection metadata in the header bag: `OriginalTopic`, `RejectionReason`, `RejectionTimestamp`, `OriginalMessageType`
    - The source topic should be empty after rejection (message was already popped)
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `RedisMessageConsumer`, add `_configuration`, `_deadLetterRoutingKey`, `_invalidMessageRoutingKey` fields
    - Add lazy `RedisMessageProducer` fields for DLQ and invalid message producers
    - Add `CreateDeadLetterProducer()` / `CreateInvalidMessageProducer()` methods that create `RedisMessageProducer` with `RedisMessagePublication { Topic = routingKey }`
    - Add `RefreshMetadata()` method using `HeaderNames` constants (same as Kafka/SQS)
    - Add `DetermineRejectionRoute()` method implementing ADR 0036 routing decision tree
    - In `Reject()`: if DLQ producer available, enrich metadata → send to DLQ → remove from `_inflight`
    - In `RejectAsync()`: same logic using async producer path

- [x] **TEST + IMPLEMENT: Rejecting a message with Unacceptable reason sends it to the invalid message channel**
  - **USE COMMAND**: `/test-first when rejecting Redis message with unacceptable reason should send to invalid message channel`
  - Test location: `tests/Paramore.Brighter.Redis.Tests/MessagingGateway/Reactor/`
  - Test file: `When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel.cs`
  - Test should verify:
    - Configure consumer with both `deadLetterRoutingKey` and `invalidMessageRoutingKey`
    - Send a message, consume it, reject with `Unacceptable` reason
    - Message should appear on the invalid message channel (not the DLQ)
    - DLQ channel should be empty
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `DetermineRejectionRoute()`, route `Unacceptable` to `_invalidMessageProducer` when available (per ADR 0036)

- [x] **TEST + IMPLEMENT: Rejecting with Unacceptable reason falls back to DLQ when no invalid message channel configured**
  - **USE COMMAND**: `/test-first when rejecting Redis message with unacceptable reason and no invalid channel should fallback to dlq`
  - Test location: `tests/Paramore.Brighter.Redis.Tests/MessagingGateway/Reactor/`
  - Test file: `When_rejecting_message_with_unacceptable_and_no_invalid_channel_should_fallback_to_dlq.cs`
  - Test should verify:
    - Configure consumer with `deadLetterRoutingKey` only (no `invalidMessageRoutingKey`)
    - Send a message, consume it, reject with `Unacceptable` reason
    - Message should appear on the DLQ (fallback)
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `DetermineRejectionRoute()`, when reason is `Unacceptable` and `_invalidMessageProducer` is null, fall back to `_deadLetterProducer`

- [x] **TEST + IMPLEMENT: Rejecting a message with no DLQ or invalid message channel configured removes from inflight**
  - **USE COMMAND**: `/test-first when rejecting Redis message with no channels configured should remove from inflight`
  - Test location: `tests/Paramore.Brighter.Redis.Tests/MessagingGateway/Reactor/`
  - Test file: `When_rejecting_message_with_no_channels_configured_should_remove_from_inflight.cs`
  - Test should verify:
    - Configure consumer with no `deadLetterRoutingKey` and no `invalidMessageRoutingKey`
    - Send a message, consume it, reject with `DeliveryError` reason
    - Reject returns true
    - Consumer can receive next message without "unacked message in flight" error
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `Reject()`/`RejectAsync()`, when both producers are null, log warning and remove from `_inflight` (current behaviour preserved)

- [x] **TEST + IMPLEMENT: Rejecting a message with DeliveryError sends to DLQ (async/Proactor)**
  - **USE COMMAND**: `/test-first when rejecting Redis message async with delivery error should send to dead letter queue`
  - Test location: `tests/Paramore.Brighter.Redis.Tests/MessagingGateway/Proactor/`
  - Test file: `When_rejecting_message_with_delivery_error_should_send_to_dlq_async.cs`
  - Test should verify:
    - Same as the Reactor DeliveryError test but using async consumer path (`ReceiveAsync`, `RejectAsync`)
    - Message appears on DLQ with metadata
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Verify the async path in `RejectAsync()` works end-to-end (should already work since `Reject` delegates to shared logic)

## Verification

- [x] **Run full Redis test suite to verify no regressions**
  - Run all existing tests in `Paramore.Brighter.Redis.Tests`
  - Verify existing requeue/post tests still pass
  - Verify new DLQ tests pass
