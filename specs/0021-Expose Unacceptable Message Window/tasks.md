# Tasks: Expose UnacceptableMessageLimitWindow

**Spec**: 0021-Expose Unacceptable Message Window
**ADR**: [0051 - Expose UnacceptableMessageLimitWindow Through Configuration Pipeline](../../docs/adr/0051-expose-unacceptable-message-limit-window.md)
**Branch**: `UnacceptableMessageLimit`

## Overview

Wire `UnacceptableMessageLimitWindow` through the configuration pipeline so it flows from `Subscription` → `ConsumerFactory` → `MessagePump`. Then expose the parameter on all transport-specific subscription constructors.

## Tasks

### Task 1: Wire ConsumerFactory → Reactor

- [x] **TEST + IMPLEMENT: ConsumerFactory passes UnacceptableMessageLimitWindow to Reactor**
  - **USE COMMAND**: `/test-first when consumer factory creates reactor with unacceptable message limit window it should be set on the pump`
  - Test location: `tests/Paramore.Brighter.Core.Tests/MessageDispatch/Reactor`
  - Test file: `When_consumer_factory_creates_reactor_with_unacceptable_message_limit_window.cs`
  - Test should verify:
    - Create a `Subscription` with `unacceptableMessageLimitWindow: TimeSpan.FromMinutes(5)`
    - Build a `ConsumerFactory` with that subscription
    - Call `Create()` to get a `Consumer`
    - Assert `consumer.Performer` (the message pump) has `UnacceptableMessageLimitWindow == TimeSpan.FromMinutes(5)`
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `ConsumerFactory.CreateReactor()` (`src/Paramore.Brighter.ServiceActivator/ConsumerFactory.cs` line 109), add `UnacceptableMessageLimitWindow = _subscription.UnacceptableMessageLimitWindow` to the object initializer, immediately after `UnacceptableMessageLimit`

### Task 2: Wire ConsumerFactory → Proactor

- [x] **TEST + IMPLEMENT: ConsumerFactory passes UnacceptableMessageLimitWindow to Proactor**
  - **USE COMMAND**: `/test-first when consumer factory creates proactor with unacceptable message limit window it should be set on the pump`
  - Test location: `tests/Paramore.Brighter.Core.Tests/MessageDispatch/Proactor`
  - Test file: `When_consumer_factory_creates_proactor_with_unacceptable_message_limit_window.cs`
  - Test should verify:
    - Create a `Subscription` with `messagePumpType: MessagePumpType.Proactor` and `unacceptableMessageLimitWindow: TimeSpan.FromMinutes(5)`
    - Build a `ConsumerFactory` with that subscription
    - Call `Create()` to get a `Consumer`
    - Assert `consumer.Performer` (the message pump) has `UnacceptableMessageLimitWindow == TimeSpan.FromMinutes(5)`
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `ConsumerFactory.CreateProactor()` (`src/Paramore.Brighter.ServiceActivator/ConsumerFactory.cs` line 131), add `UnacceptableMessageLimitWindow = _subscription.UnacceptableMessageLimitWindow` to the object initializer, immediately after `UnacceptableMessageLimit`

### Task 3: Add parameter to transport-specific subscriptions

- [x] **IMPLEMENT: Add `unacceptableMessageLimitWindow` constructor parameter to all transport subscription types**
  - No separate test needed — this is pure constructor parameter pass-through to the base `Subscription` class, which already stores the value. Tasks 1 and 2 verify end-to-end wiring.
  - For each type below, add `TimeSpan? unacceptableMessageLimitWindow = null` as a constructor parameter immediately after `unacceptableMessageLimit`, and pass it through in the base constructor call.
  - Follow the exact pattern used by `unacceptableMessageLimit` in each file.
  - Types to update (non-generic + generic `<T>` variant for each):
    1. `KafkaSubscription` — `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaSubscription.cs`
    2. `SqsSubscription` (AWSSQS) — `src/Paramore.Brighter.MessagingGateway.AWSSQS/SqsSubscription.cs`
    3. `SqsSubscription` (AWSSQS.V4) — `src/Paramore.Brighter.MessagingGateway.AWSSQS.V4/SqsSubscription.cs`
    4. `RmqSubscription` (Sync) — `src/Paramore.Brighter.MessagingGateway.RMQ/RmqSubscription.cs`
    5. `RmqSubscription` (Async) — `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqSubscription.cs`
    6. `AzureServiceBusSubscription` — `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusSubscription.cs`
    7. `GcpPubSubSubscription` — `src/Paramore.Brighter.MessagingGateway.GcpPubSub/GcpPubSubSubscription.cs`
    8. `RedisSubscription` — `src/Paramore.Brighter.MessagingGateway.Redis/RedisSubscription.cs`
    9. `MsSqlSubscription` — `src/Paramore.Brighter.MessagingGateway.MsSql/MsSqlSubscription.cs`
    10. `PostgresSubscription` — `src/Paramore.Brighter.MessagingGateway.Postgres/PostgresSubscription.cs`
    11. `MqttSubscription` — `src/Paramore.Brighter.MessagingGateway.MQTT/MqttSubscription.cs`
    12. `RocketMqSubscription` — `src/Paramore.Brighter.MessagingGateway.RocketMQ/RocketMqSubscription.cs`
    13. `InMemorySubscription` — `src/Paramore.Brighter/InMemorySubscription.cs`

### Task 4: Regression

- [x] **VERIFY: All existing core tests pass** (533 passed, 0 failed, 7 skipped — net9.0 + net10.0)
  - Run: `dotnet test tests/Paramore.Brighter.Core.Tests/ --no-restore`
  - All existing tests must continue to pass (no breaking changes)

## Dependencies

- Task 2 is independent of Task 1 (both wire different methods)
- Task 3 is independent of Tasks 1 and 2 (constructor plumbing vs. factory wiring)
- Task 4 depends on Tasks 1, 2, and 3 being complete
