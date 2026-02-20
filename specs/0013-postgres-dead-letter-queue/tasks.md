# Tasks: PostgreSQL Dead Letter Queue (Spec 0013)

**Requirements**: [requirements.md](requirements.md)
**Design**: [ADR 0034](../../docs/adr/0034-provide-dlq-where-missing.md), [ADR 0036](../../docs/adr/0036-message-rejection-routing-strategy.md), [ADR 0041](../../docs/adr/0041-postgres-dlq-brighter-managed.md)
**Reference Implementation**: [Spec 0001: Kafka DLQ](../0001-kafka-dead-letter-queue/), [Spec 0012: MsSql DLQ](../0012-mssql-dead-letter-queue/)

## Package

All tasks apply to:
- `src/Paramore.Brighter.MessagingGateway.Postgres/`

Test project:
- `tests/Paramore.Brighter.PostgresSQL.Tests/`

## Prerequisites

- [x] Spec 0010 (AWS SQS) complete
- [x] Spec 0011 (Redis) complete
- [x] Spec 0012 (MsSql) complete — confirms pattern generalises to relational transports

## Key Difference from MsSql

PostgreSQL uses **visibility timeout** on `Receive()` — the message row stays in the table. `Reject()` must forward to DLQ **then delete the source message** (using the `ReceiptHandle` stored in `message.Header.Bag`). The `ReceiptHandle` must be extracted before `RefreshMetadata()` modifies the bag. See ADR 0041 for full details.

## Tasks

- [ ] **TEST + IMPLEMENT: PostgresSubscription exposes DLQ and invalid message routing keys**
  - **USE COMMAND**: `/test-first when creating Postgres subscription with dead letter and invalid message routing keys should expose properties`
  - Test location: `tests/Paramore.Brighter.PostgresSQL.Tests/MessagingGateway/`
  - Test file: `When_creating_postgres_subscription_with_dlq_routing_keys_should_expose_properties.cs`
  - Test should verify:
    - `PostgresSubscription` can be constructed with `deadLetterRoutingKey` and `invalidMessageRoutingKey` parameters
    - The subscription can be cast to `IUseBrighterDeadLetterSupport` and `DeadLetterRoutingKey` returns the configured value
    - The subscription can be cast to `IUseBrighterInvalidMessageSupport` and `InvalidMessageRoutingKey` returns the configured value
    - Both properties are null when not provided (backward compatible)
    - Generic `PostgresSubscription<T>` also supports the new parameters
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - `PostgresSubscription` implements `IUseBrighterDeadLetterSupport` and `IUseBrighterInvalidMessageSupport`
    - Add `RoutingKey? DeadLetterRoutingKey { get; set; }` and `RoutingKey? InvalidMessageRoutingKey { get; set; }` properties
    - Add optional constructor parameters `deadLetterRoutingKey` and `invalidMessageRoutingKey` to both `PostgresSubscription` and `PostgresSubscription<T>`

- [ ] **TEST + IMPLEMENT: Consumer factory passes DLQ routing keys from subscription to consumer**
  - **USE COMMAND**: `/test-first when creating Postgres consumer from subscription with DLQ routing keys should pass them to consumer`
  - Test location: `tests/Paramore.Brighter.PostgresSQL.Tests/MessagingGateway/`
  - Test file: `When_creating_postgres_consumer_with_dlq_subscription_should_pass_routing_keys.cs`
  - Test should verify:
    - Create a `PostgresSubscription` with `deadLetterRoutingKey` and `invalidMessageRoutingKey` set
    - Create consumer via `PostgresConsumerFactory.Create()`
    - The consumer receives the routing keys (verify via reflection on private fields, same pattern as MsSql/Redis factory tests)
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `PostgresConsumerFactory.CreateMessageConsumer()`, extract routing keys from subscription using `IUseBrighterDeadLetterSupport` / `IUseBrighterInvalidMessageSupport` interface checks
    - Pass extracted keys to `PostgresMessageConsumer` constructor
    - Add new constructor parameters to `PostgresMessageConsumer`: `RoutingKey? deadLetterRoutingKey = null`, `RoutingKey? invalidMessageRoutingKey = null`
    - Store `RelationalDatabaseConfiguration` as a field for later lazy producer creation (currently accessed only via the subscription)

- [ ] **TEST + IMPLEMENT: Rejecting a message with DeliveryError sends it to the DLQ channel and deletes the source**
  - **USE COMMAND**: `/test-first when rejecting Postgres message with delivery error should send to dead letter queue`
  - Test location: `tests/Paramore.Brighter.PostgresSQL.Tests/MessagingGateway/`
  - Test file: `When_rejecting_message_with_delivery_error_should_send_to_dlq.cs`
  - Test should verify:
    - Create source topic consumer with `deadLetterRoutingKey` pointing to a DLQ topic
    - Create a separate DLQ consumer to read the DLQ topic
    - Send a message to source topic, consume it
    - Reject with `MessageRejectionReason` of `DeliveryError`
    - Consume from DLQ topic — the rejected message should appear there
    - The DLQ message should contain rejection metadata in the header bag: `originalTopic`, `rejectionReason`, `rejectionTimestamp`, `originalMessageType`
    - `Reject()` returns `true`
    - Source message is deleted (re-reading from source returns MT_NONE)
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `_deadLetterRoutingKey`, `_invalidMessageRoutingKey` fields and `_configuration` field for lazy producer creation
    - Add `Lazy<PostgresMessageProducer?>` fields for DLQ and invalid message producers
    - Add `CreateDeadLetterProducer()` / `CreateInvalidMessageProducer()` methods that create `PostgresMessageProducer` with a `PostgresPublication` using the DLQ/invalid routing key as topic
    - Add `RefreshMetadata()` method adding `originalTopic`, `rejectionTimestamp`, `originalMessageType`, `rejectionReason`, `rejectionMessage` to the message bag
    - Add `DetermineRejectionRoute()` method implementing ADR 0036 routing decision tree
    - Add `DeleteSourceMessage()` helper that executes the existing DELETE SQL using the `ReceiptHandle`
    - In `Reject()`: extract `ReceiptHandle` first, enrich metadata, send to DLQ via producer, then delete source message in `finally` block, return `true`

- [ ] **TEST + IMPLEMENT: Rejecting a message with Unacceptable reason sends it to the invalid message channel**
  - **USE COMMAND**: `/test-first when rejecting Postgres message with unacceptable reason should send to invalid message channel`
  - Test location: `tests/Paramore.Brighter.PostgresSQL.Tests/MessagingGateway/`
  - Test file: `When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel.cs`
  - Test should verify:
    - Configure consumer with both `deadLetterRoutingKey` and `invalidMessageRoutingKey`
    - Send a message, consume it, reject with `Unacceptable` reason
    - Message should appear on the invalid message channel (not the DLQ)
    - DLQ channel should be empty
    - Source message is deleted
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `DetermineRejectionRoute()`, route `Unacceptable` to `_invalidMessageProducer` when available (per ADR 0036)

- [ ] **TEST + IMPLEMENT: Rejecting with Unacceptable reason falls back to DLQ when no invalid message channel configured**
  - **USE COMMAND**: `/test-first when rejecting Postgres message with unacceptable reason and no invalid channel should fallback to dlq`
  - Test location: `tests/Paramore.Brighter.PostgresSQL.Tests/MessagingGateway/`
  - Test file: `When_rejecting_message_with_unacceptable_and_no_invalid_channel_should_fallback_to_dlq.cs`
  - Test should verify:
    - Configure consumer with `deadLetterRoutingKey` only (no `invalidMessageRoutingKey`)
    - Send a message, consume it, reject with `Unacceptable` reason
    - Message should appear on the DLQ (fallback)
    - Source message is deleted
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `DetermineRejectionRoute()`, when reason is `Unacceptable` and `_invalidMessageProducer` is null, fall back to `_deadLetterProducer`

- [ ] **TEST + IMPLEMENT: Rejecting a message with no DLQ or invalid message channel configured deletes source and logs warning**
  - **USE COMMAND**: `/test-first when rejecting Postgres message with no channels configured should delete source and log warning`
  - Test location: `tests/Paramore.Brighter.PostgresSQL.Tests/MessagingGateway/`
  - Test file: `When_rejecting_message_with_no_channels_configured_should_delete_and_log_warning.cs`
  - Test should verify:
    - Configure consumer with no `deadLetterRoutingKey` and no `invalidMessageRoutingKey`
    - Send a message, consume it, reject with `DeliveryError` reason
    - Reject returns `true`
    - Source message is deleted (current behaviour preserved)
    - Consumer can continue to receive subsequent messages
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `Reject()`/`RejectAsync()`, when both producers are null, log warning, delete source message, return `true`

- [ ] **TEST + IMPLEMENT: Rejecting a message with DeliveryError sends to DLQ (async/Proactor)**
  - **USE COMMAND**: `/test-first when rejecting Postgres message async with delivery error should send to dead letter queue`
  - Test location: `tests/Paramore.Brighter.PostgresSQL.Tests/MessagingGateway/`
  - Test file: `When_rejecting_message_with_delivery_error_should_send_to_dlq_async.cs`
  - Test should verify:
    - Same as the sync DeliveryError test but using async consumer path (`ReceiveAsync`, `RejectAsync`)
    - Message appears on DLQ with metadata
    - Source message is deleted
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Implement `RejectAsync()` with full async path (not delegating to sync `Reject()`)
    - Use `SendAsync` on the lazy producer
    - Use `DeleteSourceMessageAsync()` helper for the source delete

## Verification

- [ ] **Run full PostgreSQL test suite to verify no regressions**
  - Run all existing tests in `Paramore.Brighter.PostgresSQL.Tests`
  - Verify existing requeue/purge/order tests still pass
  - Verify new DLQ tests pass
