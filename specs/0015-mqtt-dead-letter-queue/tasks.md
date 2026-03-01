# Implementation Tasks — MQTT Dead Letter Queue

**Spec**: 0015
**ADR**: [0043 — MQTT DLQ Brighter-Managed](../../docs/adr/0043-mqtt-dlq-brighter-managed.md)
**Reference**: Redis DLQ (spec 0011) — closest structural pattern
**Package**: `Paramore.Brighter.MessagingGateway.MQTT`
**Test project**: `Paramore.Brighter.MQTT.Tests`
**Note**: MQTT tests use an in-process `MqttTestServer` — no Docker broker needed

---

## Tasks

- [x] **1. TEST + IMPLEMENT: MqttSubscription exposes DLQ and invalid message routing keys**
  - **USE COMMAND**: `/test-first when creating MQTT subscription with dead letter and invalid message routing keys should expose properties`
  - Test location: `tests/Paramore.Brighter.MQTT.Tests/`
  - Test file: `When_creating_mqtt_subscription_with_dlq_routing_keys_should_expose_properties.cs`
  - Test should verify:
    - `MqttSubscription` can be constructed with `deadLetterRoutingKey` and `invalidMessageRoutingKey` parameters
    - `DeadLetterRoutingKey` property returns the configured value
    - `InvalidMessageRoutingKey` property returns the configured value
    - Default values are null when not specified
    - Generic `MqttSubscription<T>` variant works the same way
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create `MqttSubscription.cs` in `src/Paramore.Brighter.MessagingGateway.MQTT/`
    - Inherit from `Subscription`, implement `IUseBrighterDeadLetterSupport` and `IUseBrighterInvalidMessageSupport`
    - Add optional `deadLetterRoutingKey` and `invalidMessageRoutingKey` constructor parameters
    - Create generic `MqttSubscription<T> where T : IRequest` variant
    - Follow Redis `RedisSubscription` pattern

- [x] **2. TEST + IMPLEMENT: Consumer factory passes DLQ routing keys from subscription to consumer**
  - **USE COMMAND**: `/test-first when creating MQTT consumer from subscription with DLQ routing keys should pass them to consumer`
  - Test location: `tests/Paramore.Brighter.MQTT.Tests/`
  - Test file: `When_creating_mqtt_consumer_with_dlq_subscription_should_pass_routing_keys.cs`
  - Test should verify:
    - `MqttMessageConsumerFactory.Create()` extracts routing keys via `IUseBrighterDeadLetterSupport`/`IUseBrighterInvalidMessageSupport` interface checks
    - `MqttMessageConsumerFactory.CreateAsync()` does the same
    - Routing keys are passed through to the consumer constructor
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create `MqttMessageConsumerFactory.cs` in `src/Paramore.Brighter.MessagingGateway.MQTT/`
    - Implement `IAmAMessageConsumerFactory`
    - Constructor takes `MqttMessagingGatewayConsumerConfiguration`
    - `Create()`/`CreateAsync()` cast subscription to DLQ interfaces and extract routing keys
    - Pass routing keys to `MqttMessageConsumer` constructor
    - Add optional `deadLetterRoutingKey` and `invalidMessageRoutingKey` parameters to `MqttMessageConsumer` constructor
    - Store the full config (not just `MqttClientOptions`) for DLQ producer creation

- [x] **3. TEST + IMPLEMENT: Rejecting a message with DeliveryError sends it to the DLQ topic**
  - **USE COMMAND**: `/test-first when rejecting MQTT message with delivery error should send to dead letter queue`
  - Test location: `tests/Paramore.Brighter.MQTT.Tests/MessagingGateway/Reactor/`
  - Test file: `When_rejecting_message_with_delivery_error_should_send_to_dlq.cs`
  - Test should verify:
    - Source producer sends a message to source topic
    - Source consumer (with DLQ routing key) receives the message
    - `Reject(message, DeliveryError)` returns true
    - A DLQ consumer on the DLQ topic receives the rejected message
    - The DLQ message contains metadata: `originalTopic`, `rejectionReason`, `rejectionTimestamp`, `originalMessageType`
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Implement `RejectAsync()` in `MqttMessageConsumer` as the primary path
    - `Reject()` delegates to `RejectAsync()` via `BrighterAsyncContext.Run()`
    - Add `RefreshMetadata()` helper (same pattern as Redis)
    - Add `DetermineRejectionRoute()` helper (per ADR 0036)
    - Add `Lazy<MqttMessageProducer?>` fields for DLQ/invalid producers
    - Add `CreateDeadLetterProducer()`/`CreateInvalidMessageProducer()` factory methods
    - Each producer creates an `MqttMessagePublisher` with the consumer's config and wraps it in `MqttMessageProducer`
    - On DLQ production failure, log error and return true (don't block the consumer)

- [x] **4. TEST + IMPLEMENT: Rejecting a message with Unacceptable reason sends it to the invalid message channel**
  - **USE COMMAND**: `/test-first when rejecting MQTT message with unacceptable reason should send to invalid message channel`
  - Test location: `tests/Paramore.Brighter.MQTT.Tests/MessagingGateway/Reactor/`
  - Test file: `When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel.cs`
  - Test should verify:
    - Consumer configured with both DLQ and invalid message routing keys
    - `Reject(message, Unacceptable)` returns true
    - Message arrives on the invalid message topic (not DLQ)
    - DLQ topic does not receive the message
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Already covered by task 3's `DetermineRejectionRoute()` — this task validates the Unacceptable routing path

- [x] **5. TEST + IMPLEMENT: Rejecting with Unacceptable reason falls back to DLQ when no invalid message channel configured**
  - **USE COMMAND**: `/test-first when rejecting MQTT message with unacceptable reason and no invalid channel should fallback to dlq`
  - Test location: `tests/Paramore.Brighter.MQTT.Tests/MessagingGateway/Reactor/`
  - Test file: `When_rejecting_message_with_unacceptable_and_no_invalid_channel_should_fallback_to_dlq.cs`
  - Test should verify:
    - Consumer configured with DLQ routing key only (no invalid message routing key)
    - `Reject(message, Unacceptable)` returns true
    - Message arrives on the DLQ topic (fallback)
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Already covered by task 3's routing logic — this task validates the fallback path

- [x] **6. TEST + IMPLEMENT: Rejecting a message with no DLQ or invalid message channel configured logs warning and returns true**
  - **USE COMMAND**: `/test-first when rejecting MQTT message with no channels configured should log warning and return true`
  - Test location: `tests/Paramore.Brighter.MQTT.Tests/MessagingGateway/Reactor/`
  - Test file: `When_rejecting_message_with_no_channels_configured_should_return_true.cs`
  - Test should verify:
    - Consumer created without DLQ or invalid message routing keys
    - `Reject(message, DeliveryError)` returns true (not false as before)
    - Warning is logged
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Early return path in `RejectAsync()` when no producers are configured
    - Log warning via source-generated `[LoggerMessage]`
    - Return true (breaking change from current `false` — intentional per requirements)

- [x] **7. TEST + IMPLEMENT: Rejecting a message with DeliveryError sends to DLQ (async/Proactor)**
  - **USE COMMAND**: `/test-first when rejecting MQTT message async with delivery error should send to dead letter queue`
  - Test location: `tests/Paramore.Brighter.MQTT.Tests/MessagingGateway/Proactor/`
  - Test file: `When_rejecting_message_with_delivery_error_should_send_to_dlq_async.cs`
  - Test should verify:
    - Same as task 3 but using `RejectAsync()` directly
    - DLQ consumer receives the rejected message with metadata
  - **⛔ STOP HERE — WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Already implemented in task 3 (`RejectAsync` is the primary path) — this task validates the async API directly

---

## Verification

- [x] **8. Full regression — run all existing MQTT tests to verify no regressions**
  - Run: `dotnet test tests/Paramore.Brighter.MQTT.Tests/ --no-build`
  - All existing tests (posting messages, purging queues) must still pass
  - New DLQ tests must pass
  - Verify backward compatibility: existing `MqttMessageConsumer` constructor (without DLQ params) still works

---

## Task Dependencies

```
Task 1 (Subscription) ──┐
                         ├──→ Task 3 (DeliveryError → DLQ)
Task 2 (Factory)     ───┘        │
                                 ├──→ Task 4 (Unacceptable → invalid)
                                 ├──→ Task 5 (Unacceptable fallback)
                                 ├──→ Task 6 (No channels configured)
                                 └──→ Task 7 (Async variant)
                                          │
                                          └──→ Task 8 (Regression)
```

- Tasks 1–2: Create infrastructure (subscription + factory). Can be done in parallel.
- Task 3: Core DLQ implementation. Depends on tasks 1–2.
- Tasks 4–7: Validate specific routing paths. Depend on task 3.
- Task 8: Final regression. Depends on all prior tasks.
