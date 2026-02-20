# Tasks: MsSql Dead Letter Queue (Spec 0012)

**Requirements**: [requirements.md](requirements.md)
**Design**: [ADR 0034](../../docs/adr/0034-provide-dlq-where-missing.md), [ADR 0036](../../docs/adr/0036-message-rejection-routing-strategy.md), [ADR 0040](../../docs/adr/0040-mssql-dlq-brighter-managed.md)
**Reference Implementation**: [Spec 0001: Kafka DLQ](../0001-kafka-dead-letter-queue/), [Spec 0010: SQS DLQ](../0010-aws-sqs-dead-letter-queue/), [Spec 0011: Redis DLQ](../0011-redis-dead-letter-queue/)

## Package

All tasks apply to:
- `src/Paramore.Brighter.MessagingGateway.MsSql/`

Test project:
- `tests/Paramore.Brighter.MSSQL.Tests/`

## Prerequisites

- [x] Spec 0010 (AWS SQS) complete
- [x] Spec 0011 (Redis) complete — confirms pattern generalises across transports

## Tasks

- [ ] **TEST + IMPLEMENT: MsSqlSubscription exposes DLQ and invalid message routing keys**
  - **USE COMMAND**: `/test-first when creating MsSql subscription with dead letter and invalid message routing keys should expose properties`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/MessagingGateway/`
  - Test file: `When_creating_mssql_subscription_with_dlq_routing_keys_should_expose_properties.cs`
  - Test should verify:
    - `MsSqlSubscription` can be constructed with `deadLetterRoutingKey` and `invalidMessageRoutingKey` parameters
    - The subscription can be cast to `IUseBrighterDeadLetterSupport` and `DeadLetterRoutingKey` returns the configured value
    - The subscription can be cast to `IUseBrighterInvalidMessageSupport` and `InvalidMessageRoutingKey` returns the configured value
    - Both properties are null when not provided (backward compatible)
    - Generic `MsSqlSubscription<T>` also supports the new parameters
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - `MsSqlSubscription` implements `IUseBrighterDeadLetterSupport` and `IUseBrighterInvalidMessageSupport`
    - Add `RoutingKey? DeadLetterRoutingKey { get; set; }` and `RoutingKey? InvalidMessageRoutingKey { get; set; }` properties
    - Add optional constructor parameters `deadLetterRoutingKey` and `invalidMessageRoutingKey` to both `MsSqlSubscription` and `MsSqlSubscription<T>`

- [ ] **TEST + IMPLEMENT: Consumer factory passes DLQ routing keys from subscription to consumer**
  - **USE COMMAND**: `/test-first when creating MsSql consumer from subscription with DLQ routing keys should pass them to consumer`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/MessagingGateway/`
  - Test file: `When_creating_mssql_consumer_with_dlq_subscription_should_pass_routing_keys.cs`
  - Test should verify:
    - Create an `MsSqlSubscription` with `deadLetterRoutingKey` and `invalidMessageRoutingKey` set
    - Create consumer via `MsSqlMessageConsumerFactory.Create()`
    - The consumer receives the routing keys (verify via reflection on private fields, same pattern as SQS/Redis factory tests)
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `MsSqlMessageConsumerFactory.Create()` and `CreateAsync()`, extract routing keys from subscription using `IUseBrighterDeadLetterSupport` / `IUseBrighterInvalidMessageSupport` interface checks
    - Pass extracted keys to `MsSqlMessageConsumer` constructor
    - Add new constructor parameters to `MsSqlMessageConsumer`: `RoutingKey? deadLetterRoutingKey = null`, `RoutingKey? invalidMessageRoutingKey = null`
    - Store `RelationalDatabaseConfiguration` as a field (currently only passed through to `MsSqlMessageQueue`) for later lazy producer creation

- [ ] **TEST + IMPLEMENT: Rejecting a message with DeliveryError sends it to the DLQ channel**
  - **USE COMMAND**: `/test-first when rejecting MsSql message with delivery error should send to dead letter queue`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/MessagingGateway/`
  - Test file: `When_rejecting_message_with_delivery_error_should_send_to_dlq.cs`
  - Test should verify:
    - Create source topic consumer with `deadLetterRoutingKey` pointing to a DLQ topic
    - Create a separate DLQ consumer to read the DLQ topic
    - Send a message to source topic, consume it
    - Reject with `MessageRejectionReason` of `DeliveryError`
    - Consume from DLQ topic — the rejected message should appear there
    - The DLQ message should contain rejection metadata in the header bag: `OriginalTopic`, `RejectionReason`, `RejectionTimestamp`, `OriginalMessageType`
    - `Reject()` returns `true`
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `MsSqlMessageConsumer`, add `_msSqlConfiguration`, `_deadLetterRoutingKey`, `_invalidMessageRoutingKey` fields
    - Add lazy `MsSqlMessageProducer` fields for DLQ and invalid message producers
    - Add `CreateDeadLetterProducer()` / `CreateInvalidMessageProducer()` methods that create `MsSqlMessageProducer` with the DLQ/invalid routing key as topic
    - Add `RefreshMetadata()` method using `HeaderNames` constants (same as Kafka/SQS/Redis)
    - Add `DetermineRejectionRoute()` method implementing ADR 0036 routing decision tree
    - In `Reject()`: if DLQ producer available, enrich metadata, send to DLQ, return `true`

- [ ] **TEST + IMPLEMENT: Rejecting a message with Unacceptable reason sends it to the invalid message channel**
  - **USE COMMAND**: `/test-first when rejecting MsSql message with unacceptable reason should send to invalid message channel`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/MessagingGateway/`
  - Test file: `When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel.cs`
  - Test should verify:
    - Configure consumer with both `deadLetterRoutingKey` and `invalidMessageRoutingKey`
    - Send a message, consume it, reject with `Unacceptable` reason
    - Message should appear on the invalid message channel (not the DLQ)
    - DLQ channel should be empty
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `DetermineRejectionRoute()`, route `Unacceptable` to `_invalidMessageProducer` when available (per ADR 0036)

- [ ] **TEST + IMPLEMENT: Rejecting with Unacceptable reason falls back to DLQ when no invalid message channel configured**
  - **USE COMMAND**: `/test-first when rejecting MsSql message with unacceptable reason and no invalid channel should fallback to dlq`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/MessagingGateway/`
  - Test file: `When_rejecting_message_with_unacceptable_and_no_invalid_channel_should_fallback_to_dlq.cs`
  - Test should verify:
    - Configure consumer with `deadLetterRoutingKey` only (no `invalidMessageRoutingKey`)
    - Send a message, consume it, reject with `Unacceptable` reason
    - Message should appear on the DLQ (fallback)
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `DetermineRejectionRoute()`, when reason is `Unacceptable` and `_invalidMessageProducer` is null, fall back to `_deadLetterProducer`

- [ ] **TEST + IMPLEMENT: Rejecting a message with no DLQ or invalid message channel configured logs warning**
  - **USE COMMAND**: `/test-first when rejecting MsSql message with no channels configured should log warning and return true`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/MessagingGateway/`
  - Test file: `When_rejecting_message_with_no_channels_configured_should_log_warning.cs`
  - Test should verify:
    - Configure consumer with no `deadLetterRoutingKey` and no `invalidMessageRoutingKey`
    - Send a message, consume it, reject with `DeliveryError` reason
    - Reject returns `true` (changed from current `false`)
    - Consumer can continue to receive subsequent messages
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `Reject()`/`RejectAsync()`, when both producers are null, log warning and return `true`

- [x] **TEST + IMPLEMENT: Rejecting a message with DeliveryError sends to DLQ (async/Proactor)**
  - **USE COMMAND**: `/test-first when rejecting MsSql message async with delivery error should send to dead letter queue`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/MessagingGateway/`
  - Test file: `When_rejecting_message_with_delivery_error_should_send_to_dlq_async.cs`
  - Test should verify:
    - Same as the sync DeliveryError test but using async consumer path (`ReceiveAsync`, `RejectAsync`)
    - Message appears on DLQ with metadata
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Implement `RejectAsync()` with full async path (not delegating to sync `Reject()` as currently)
    - Use `SendAsync` on the lazy producer

## Verification

- [x] **Run full MsSql test suite to verify no regressions**
  - Run all existing tests in `Paramore.Brighter.MSSQL.Tests`
  - Verify existing requeue/post/order tests still pass
  - Verify new DLQ tests pass
