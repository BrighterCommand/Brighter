# 43. MQTT Dead Letter Queue — Brighter-Managed

Date: 2026-02-21

## Status

Accepted

## Context

**Parent Requirement**: [specs/0015-mqtt-dead-letter-queue/requirements.md](../../specs/0015-mqtt-dead-letter-queue/requirements.md)

**Scope**: This ADR addresses the addition of Brighter-managed DLQ support to the MQTT transport and the creation of missing subscription and consumer factory classes. It follows the pattern established by Kafka (ADR 0034), SQS (ADR 0038), Redis (ADR 0039), MsSql (ADR 0040), PostgreSQL (ADR 0041), and RocketMQ (ADR 0042).

The MQTT message consumer (`MqttMessageConsumer`) currently has `Reject()` not implemented — it returns `false` immediately:

```csharp
public bool Reject(Message message, MessageRejectionReason? reason = null)
    => false;
```

MQTT is a pub/sub protocol with no native dead letter queue support. When a handler throws `RejectMessageAction`, the message is silently discarded. There is no way to inspect or replay rejected messages.

### Message Lifecycle Model

MQTT uses a **fire-and-forget pub/sub** model:

- Messages are delivered to subscribers immediately upon receipt from the broker. The MQTTnet client buffers them into an in-memory `Queue<Message>`.
- `Acknowledge()` is a no-op — once the message is dequeued, it is consumed.
- There is no visibility timeout, no receipt handle, and no broker-side redelivery.

This means at `Reject()` time, the source message **only exists in the consumer's memory**. There is no source message to clean up on the broker — unlike RocketMQ (`Ack(MessageView)`), PostgreSQL (`DELETE`), or SQS (`DeleteMessage`). The DLQ implementation only needs to forward the message to the DLQ topic.

### Missing Infrastructure

MQTT currently lacks two classes that all other transports provide:

1. **No subscription class** — there is no `MqttSubscription` equivalent. Other transports (Redis, RocketMQ, Kafka) have subscription classes that implement `IUseBrighterDeadLetterSupport` and `IUseBrighterInvalidMessageSupport`.
2. **No consumer factory** — there is no `MqttMessageConsumerFactory`. The factory is responsible for extracting routing keys from the subscription and passing them to the consumer.

These must be created as part of this work.

### Producer Creation

Unlike RocketMQ where `Producer.Builder().Build()` is async, MQTT producer creation is **synchronous**. The `MqttMessagePublisher` constructor handles broker connection internally via `ConnectAsync().GetAwaiter().GetResult()`. This means the standard `Lazy<T>` pattern (as used by Redis) works without an async-aware alternative.

### MQTT v3.1.1 Constraints

The MQTT gateway targets v3.1.1 of the protocol (not v5.0). This means:
- No user properties on messages
- No content type header
- Metadata must be carried inside the message payload (in the Brighter `Message.Header.Bag`), which is already how Brighter serializes messages

### Forces

- `Reject()` currently returns `false` — the message is silently lost
- No source message cleanup is needed (fire-and-forget model)
- Producer creation is synchronous — `Lazy<T>` works
- Two infrastructure classes (subscription, consumer factory) must be created
- The existing `MqttMessagingGatewayConfiguration` provides broker connection details
- `MqttMessagePublisher` + `MqttMessageProducer` already handle message serialization and publishing
- The Redis implementation is the closest structural reference (sync `Lazy<T>`, no source cleanup)

## Decision

We will add Brighter-managed DLQ support to the MQTT transport following the Redis pattern, with `Lazy<T>` producers and no source message cleanup. We will also create the missing `MqttSubscription` and `MqttMessageConsumerFactory` classes.

### Roles and Responsibilities

**MqttSubscription** (information holder) — *NEW CLASS*:
- *Knowing*: the dead letter routing key and invalid message routing key for this subscription
- Implements `Subscription`, `IUseBrighterDeadLetterSupport`, and `IUseBrighterInvalidMessageSupport`
- Includes a generic variant `MqttSubscription<T> where T : IRequest`
- Constructor accepts all standard `Subscription` parameters plus optional DLQ/invalid routing keys
- Sets `ChannelFactoryType` to the new `MqttMessageConsumerFactory` type

**MqttMessageConsumerFactory** (coordinator) — *NEW CLASS*:
- *Deciding*: whether the subscription has DLQ/invalid message support configured
- *Doing*: extracting routing keys from the subscription via `IUseBrighterDeadLetterSupport`/`IUseBrighterInvalidMessageSupport` interface checks, then passing them and the configuration to the consumer constructor
- Implements `IAmAMessageConsumerFactory`
- Takes `MqttMessagingGatewayConsumerConfiguration` in its constructor

**MqttMessageConsumer** (service provider) — *MODIFIED*:
- *Deciding*: which rejection route to take based on `MessageRejectionReason` (per ADR 0036)
- *Doing*: enriching message metadata, producing to the appropriate DLQ/invalid message topic
- *Knowing*: the DLQ and invalid message routing keys, lazily-created producers for each

### Rejection Routing (per ADR 0036)

The routing decision tree in `Reject`/`RejectAsync`:

```
Reject(message, reason)
├── No producers configured
│   └── log warning, return true
├── DeliveryError
│   ├── _deadLetterProducer exists → send to DLQ, return true
│   └── no producer → log warning, return true
├── Unacceptable
│   ├── _invalidMessageProducer exists → send to invalid channel, return true
│   ├── _deadLetterProducer exists (fallback) → send to DLQ, return true
│   └── no producer → log warning, return true
```

Note the absence of source cleanup compared to other transports — MQTT's fire-and-forget model means there is nothing to acknowledge or delete after forwarding.

### Lazy Producer Creation

Because MQTT producer creation is synchronous, we use `Lazy<T>` (same as Redis):

```
private Lazy<MqttMessageProducer?>? _deadLetterProducer;
private Lazy<MqttMessageProducer?>? _invalidMessageProducer;

// In constructor:
if (deadLetterRoutingKey != null)
    _deadLetterProducer = new Lazy<MqttMessageProducer?>(CreateDeadLetterProducer);
```

Each producer factory method creates an `MqttMessagePublisher` using the consumer's configuration (hostname, port, credentials) and wraps it in an `MqttMessageProducer` with a `Publication` targeting the DLQ/invalid topic.

### Constructor Changes

`MqttMessageConsumer` gains optional parameters:

```
MqttMessageConsumer(
    MqttMessagingGatewayConsumerConfiguration configuration,    // existing
    RoutingKey? deadLetterRoutingKey = null,                     // NEW
    RoutingKey? invalidMessageRoutingKey = null                  // NEW
)
```

The configuration is already available (stored as `_mqttClientOptions` today but will also be stored as the full config object for DLQ producer creation).

### Message Metadata Enrichment

Before sending to the DLQ, the message header bag is enriched with:
- `originalTopic` — the source routing key
- `rejectionReason` — `DeliveryError` or `Unacceptable`
- `rejectionTimestamp` — UTC ISO-8601 timestamp
- `originalMessageType` — the original `MessageType`
- `rejectionMessage` — description text (if provided)

This is identical to all other transports.

### Sync/Async Pattern

`Reject()` calls `RejectAsync()` via `BrighterAsyncContext.Run()` (async-first design, consistent with Brighter conventions). The async path is the primary implementation.

## Consequences

### Positive

- Rejected messages are no longer silently lost — they are forwarded to a configurable DLQ topic
- MQTT gains subscription and consumer factory classes, bringing it in line with all other transports
- Users can configure DLQ per subscription via the standard `IUseBrighterDeadLetterSupport` interface
- Lazy producer creation means zero overhead when DLQ is not used
- Simplest DLQ implementation of all transports — no source cleanup, synchronous producers
- `Reject()` now returns `true` (not `false`) — callers can distinguish success from failure

### Negative

- If DLQ production fails, the rejected message is logged at error level but may be lost (same trade-off as all other transports per ADR 0034)
- Two new classes (`MqttSubscription`, `MqttMessageConsumerFactory`) increase the surface area of the MQTT package
- The consumer must retain the full configuration object (not just `MqttClientOptions`) for DLQ producer creation

### Risks and Mitigations

- **Risk**: DLQ topic does not exist on the broker → **Mitigation**: MQTT brokers typically auto-create topics on first publish; if not, the publish will fail and be caught/logged
- **Risk**: DLQ topic accumulates messages indefinitely → **Mitigation**: Users are responsible for consuming or purging DLQ topics (same as all other transports)
- **Risk**: New subscription/factory classes break existing MQTT users → **Mitigation**: All new constructor parameters are optional; existing `MqttMessageConsumer` constructor remains backward compatible

## Alternatives Considered

### 1. Skip Subscription and Factory Classes

Pass DLQ routing keys directly to the consumer without creating `MqttSubscription` or `MqttMessageConsumerFactory`. This would make MQTT structurally inconsistent with every other transport, complicating the framework's configuration model and preventing use of the standard `IUseBrighterDeadLetterSupport`/`IUseBrighterInvalidMessageSupport` interfaces.

### 2. Async Producer Creation (RocketMQ Pattern)

Use nullable fields with async factory methods instead of `Lazy<T>`. Unnecessary complexity — MQTT producer creation is synchronous, and the RocketMQ pattern exists specifically because its producer builder is async.

### 3. Requeue Instead of DLQ

Have `Reject()` requeue the message (re-publish to the source topic). This creates an infinite retry loop for messages that will never succeed — the specific problem DLQ solves.

## References

- Requirements: [specs/0015-mqtt-dead-letter-queue/requirements.md](../../specs/0015-mqtt-dead-letter-queue/requirements.md)
- ADR 0034: [Provide DLQ Where Missing](0034-provide-dlq-where-missing.md)
- ADR 0036: [Message Rejection Routing Strategy](0036-message-rejection-routing-strategy.md)
- ADR 0039: [Redis DLQ Brighter-Managed](0039-redis-dlq-brighter-managed.md) — closest structural reference
- ADR 0042: [RocketMQ DLQ Brighter-Managed](0042-rocketmq-dlq-brighter-managed.md)
