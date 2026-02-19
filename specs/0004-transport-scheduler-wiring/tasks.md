# Tasks: Transport Channel Factory Scheduler Wiring

**Spec**: 0004-transport-scheduler-wiring
**ADR**: [0039-transport-scheduler-wiring](../../docs/adr/0039-transport-scheduler-wiring.md)
**Requirements**: [requirements.md](requirements.md)

## Phase 0: Foundation — Default Scheduler Registration & Interface

These tasks establish the core infrastructure that all subsequent transport tasks depend on.

- [x] **TEST + IMPLEMENT: Default InMemorySchedulerFactory registered when no scheduler configured**
  - **USE COMMAND**: `/test-first when no scheduler configured should default to InMemorySchedulerFactory`
  - Test location: "tests/Paramore.Brighter.Extensions.DependencyInjection.Tests"
  - Test file: `When_no_scheduler_configured_should_default_to_InMemorySchedulerFactory.cs`
  - Test should verify:
    - `AddServiceActivator()` called without `UseScheduler()` or `UseMessageScheduler()`
    - `IAmAMessageSchedulerFactory` resolves from the service provider
    - Resolved factory is an `InMemorySchedulerFactory`
    - `IAmAMessageScheduler` resolves from the service provider
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In the DI registration path, add `TryAddSingleton<IAmAMessageSchedulerFactory>(new InMemorySchedulerFactory())`
    - Add matching `TryAddSingleton` registrations for `IAmAMessageScheduler`, `IAmAMessageSchedulerSync`, `IAmAMessageSchedulerAsync` (mirroring the pattern in `UseMessageScheduler`)
    - `UseScheduler()` / `UseMessageScheduler()` still override the default via `AddSingleton`

- [x] **TEST + IMPLEMENT: User-configured scheduler overrides InMemorySchedulerFactory default**
  - **USE COMMAND**: `/test-first when scheduler explicitly configured should override default InMemorySchedulerFactory`
  - Test location: "tests/Paramore.Brighter.Extensions.DependencyInjection.Tests"
  - Test file: `When_scheduler_explicitly_configured_should_override_default.cs`
  - Test should verify:
    - `UseScheduler()` called with a custom scheduler factory (e.g., a mock/stub)
    - `IAmAMessageSchedulerFactory` resolves to the user-provided factory, not `InMemorySchedulerFactory`
    - `IAmAMessageScheduler` resolves to the scheduler created by the user-provided factory
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Verify that `AddSingleton` in `UseScheduler`/`UseMessageScheduler` takes precedence over `TryAddSingleton` default
    - May require adjusting registration order if `TryAddSingleton` runs after `UseScheduler`

- [x] **IMPLEMENT: Add `IAmAChannelFactoryWithScheduler` interface**
  - File: `src/Paramore.Brighter/IAmAChannelFactoryWithScheduler.cs`
  - Interface with single property: `IAmAMessageScheduler? Scheduler { get; set; }`
  - Follows the `IAmA*` naming convention for role interfaces
  - No test needed — pure interface with no behavior

## Phase 1: Transport Consumer Factory Scheduler Wiring

Each task updates one consumer factory to accept and pass the scheduler. These tasks are independent and can be done in any order.

- [x] **TEST + IMPLEMENT: RMQ Async consumer factory passes scheduler to consumer**
  - **USE COMMAND**: `/test-first when RMQ async consumer factory creates consumer should pass scheduler`
  - Test location: "tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway"
  - Test file: `When_rmq_async_consumer_factory_creates_consumer_should_pass_scheduler.cs`
  - Test should verify:
    - `RmqMessageConsumerFactory` constructed with a spy scheduler
    - `Create()` returns consumer that has the scheduler wired (verifiable via requeue-with-delay behavior)
    - `CreateAsync()` returns consumer that has the scheduler wired
    - Factory constructed without scheduler still works (null default, backward compat)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `IAmAMessageScheduler? scheduler = null` parameter to `RmqMessageConsumerFactory` constructor in `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageConsumerFactory.cs`
    - Store as `_scheduler` field
    - Pass `scheduler: _scheduler` when constructing `RmqMessageConsumer` in both `Create()` and `CreateAsync()`

- [x] **TEST + IMPLEMENT: RMQ Sync consumer factory passes scheduler to consumer**
  - **USE COMMAND**: `/test-first when RMQ sync consumer factory creates consumer should pass scheduler`
  - Test location: "tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway"
  - Test file: `When_rmq_sync_consumer_factory_creates_consumer_should_pass_scheduler.cs`
  - Test should verify:
    - `RmqMessageConsumerFactory` (Sync) constructed with a spy scheduler
    - `Create()` returns consumer that has the scheduler wired
    - Factory constructed without scheduler still works (backward compat)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `IAmAMessageScheduler? scheduler = null` parameter to `RmqMessageConsumerFactory` constructor in `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageConsumerFactory.cs`
    - Store as `_scheduler` field
    - Pass `scheduler: _scheduler` when constructing `RmqMessageConsumer` in `Create()`

- [x] **TEST + IMPLEMENT: Kafka consumer factory passes scheduler to consumer**
  - **USE COMMAND**: `/test-first when Kafka consumer factory creates consumer should pass scheduler`
  - Test location: "tests/Paramore.Brighter.Kafka.Tests/MessagingGateway"
  - Test file: `When_kafka_consumer_factory_creates_consumer_should_pass_scheduler.cs`
  - Test should verify:
    - `KafkaMessageConsumerFactory` constructed with a spy scheduler
    - `Create()` returns consumer that has the scheduler wired
    - Factory constructed without scheduler still works (backward compat)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `IAmAMessageScheduler? scheduler = null` parameter to `KafkaMessageConsumerFactory` constructor in `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessageConsumerFactory.cs`
    - Store as `_scheduler` field
    - Pass `scheduler: _scheduler` when constructing `KafkaMessageConsumer` in `Create()` and `CreateAsync()`

- [x] **TEST + IMPLEMENT: MsSql consumer factory passes scheduler to consumer**
  - **USE COMMAND**: `/test-first when MsSql consumer factory creates consumer should pass scheduler`
  - Test location: "tests/Paramore.Brighter.MSSQL.Tests/MessagingGateway"
  - Test file: `When_mssql_consumer_factory_creates_consumer_should_pass_scheduler.cs`
  - Test should verify:
    - `MsSqlMessageConsumerFactory` constructed with a spy scheduler
    - `Create()` returns consumer that has the scheduler wired
    - Factory constructed without scheduler still works (backward compat)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `IAmAMessageScheduler? scheduler = null` parameter to `MsSqlMessageConsumerFactory` constructor in `src/Paramore.Brighter.MessagingGateway.MsSql/MsSqlMessageConsumerFactory.cs`
    - Store as `_scheduler` field
    - Pass `scheduler: _scheduler` when constructing `MsSqlMessageConsumer` in `Create()` and `CreateAsync()`

- [x] **TEST + IMPLEMENT: Redis consumer factory passes scheduler to consumer**
  - **USE COMMAND**: `/test-first when Redis consumer factory creates consumer should pass scheduler`
  - Test location: "tests/Paramore.Brighter.Redis.Tests/MessagingGateway"
  - Test file: `When_redis_consumer_factory_creates_consumer_should_pass_scheduler.cs`
  - Test should verify:
    - `RedisMessageConsumerFactory` constructed with a spy scheduler
    - `Create()` returns consumer that has the scheduler wired
    - `CreateAsync()` returns consumer that has the scheduler wired
    - Factory constructed without scheduler still works (backward compat)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `IAmAMessageScheduler? scheduler = null` parameter to `RedisMessageConsumerFactory` constructor in `src/Paramore.Brighter.MessagingGateway.Redis/RedisMessageConsumerFactory.cs`
    - Store as `_scheduler` field
    - Pass `_scheduler` as 4th parameter when constructing `RedisMessageConsumer` in `Create()` and `CreateAsync()`

## Phase 2: MQTT Factory Infrastructure

MQTT currently has no consumer factory or channel factory. These tasks create them.

- [x] **TEST + IMPLEMENT: MQTT consumer factory creates consumer with configuration and scheduler**
  - **USE COMMAND**: `/test-first when MQTT consumer factory creates consumer should pass configuration and scheduler`
  - Test location: "tests/Paramore.Brighter.MQTT.Tests/MessagingGateway"
  - Test file: `When_mqtt_consumer_factory_creates_consumer_should_pass_scheduler.cs`
  - Test should verify:
    - `MqttMessageConsumerFactory` constructed with `MqttMessagingGatewayConsumerConfiguration` and a spy scheduler
    - `Create()` returns an `MqttMessageConsumer` that has the scheduler wired
    - `CreateAsync()` returns an `MqttMessageConsumer` that has the scheduler wired
    - Factory constructed without scheduler still works (backward compat)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create `src/Paramore.Brighter.MessagingGateway.MQTT/MqttMessageConsumerFactory.cs`
    - Implement `IAmAMessageConsumerFactory`
    - Constructor takes `MqttMessagingGatewayConsumerConfiguration` and `IAmAMessageScheduler? scheduler = null`
    - `Create()` and `CreateAsync()` return `new MqttMessageConsumer(configuration, _scheduler)`

- [x] **TEST + IMPLEMENT: MQTT channel factory creates channels via consumer factory**
  - **USE COMMAND**: `/test-first when MQTT channel factory creates channel should use consumer factory and pass scheduler`
  - Test location: "tests/Paramore.Brighter.MQTT.Tests/MessagingGateway"
  - Test file: `When_mqtt_channel_factory_creates_channel_should_use_consumer_factory.cs`
  - Test should verify:
    - `MqttChannelFactory` constructed with `MqttMessageConsumerFactory`
    - `CreateSyncChannel()` returns a `Channel` wrapping an `MqttMessageConsumer`
    - `CreateAsyncChannel()` returns a `ChannelAsync` wrapping an `MqttMessageConsumer`
    - Implements `IAmAChannelFactoryWithScheduler` — setting `Scheduler` property propagates to consumers
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Create `src/Paramore.Brighter.MessagingGateway.MQTT/MqttChannelFactory.cs`
    - Implement `IAmAChannelFactory` and `IAmAChannelFactoryWithScheduler`
    - Follow the same pattern as other transports' `ChannelFactory` classes

## Phase 3: Channel Factory Scheduler Wiring

Each task updates one transport's channel factory to accept scheduler and implement `IAmAChannelFactoryWithScheduler`. Depends on Phase 1 (consumer factories must accept scheduler first).

- [x] **TEST + IMPLEMENT: RMQ Async channel factory implements IAmAChannelFactoryWithScheduler**
  - **USE COMMAND**: `/test-first when RMQ async channel factory has scheduler set should pass it to consumers`
  - Test location: "tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway"
  - Test file: `When_rmq_async_channel_factory_has_scheduler_should_pass_to_consumers.cs`
  - Test should verify:
    - `ChannelFactory` implements `IAmAChannelFactoryWithScheduler`
    - Setting `Scheduler` property and calling `CreateAsyncChannel()` results in consumer having the scheduler
    - Works without setting `Scheduler` (backward compat)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Update `src/Paramore.Brighter.MessagingGateway.RMQ.Async/ChannelFactory.cs`
    - Implement `IAmAChannelFactoryWithScheduler`
    - Add `Scheduler` property; pass it to consumer factory when creating consumers

- [x] **TEST + IMPLEMENT: RMQ Sync channel factory implements IAmAChannelFactoryWithScheduler**
  - **USE COMMAND**: `/test-first when RMQ sync channel factory has scheduler set should pass it to consumers`
  - Test location: "tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway"
  - Test file: `When_rmq_sync_channel_factory_has_scheduler_should_pass_to_consumers.cs`
  - Test should verify:
    - `ChannelFactory` implements `IAmAChannelFactoryWithScheduler`
    - Setting `Scheduler` property and calling `CreateSyncChannel()` results in consumer having the scheduler
    - Works without setting `Scheduler` (backward compat)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Update `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/ChannelFactory.cs`
    - Implement `IAmAChannelFactoryWithScheduler`
    - Add `Scheduler` property; pass it to consumer factory when creating consumers

- [x] **TEST + IMPLEMENT: Kafka channel factory implements IAmAChannelFactoryWithScheduler**
  - **USE COMMAND**: `/test-first when Kafka channel factory has scheduler set should pass it to consumers`
  - Test location: "tests/Paramore.Brighter.Kafka.Tests/MessagingGateway"
  - Test file: `When_kafka_channel_factory_has_scheduler_should_pass_to_consumers.cs`
  - Test should verify:
    - `ChannelFactory` implements `IAmAChannelFactoryWithScheduler`
    - Setting `Scheduler` property and calling `CreateSyncChannel()` / `CreateAsyncChannel()` results in consumer having the scheduler
    - Works without setting `Scheduler` (backward compat)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Update `src/Paramore.Brighter.MessagingGateway.Kafka/ChannelFactory.cs`
    - Implement `IAmAChannelFactoryWithScheduler`
    - Add `Scheduler` property; pass it to consumer factory when creating consumers

- [x] **TEST + IMPLEMENT: MsSql channel factory implements IAmAChannelFactoryWithScheduler**
  - **USE COMMAND**: `/test-first when MsSql channel factory has scheduler set should pass it to consumers`
  - Test location: "tests/Paramore.Brighter.MSSQL.Tests/MessagingGateway"
  - Test file: `When_mssql_channel_factory_has_scheduler_should_pass_to_consumers.cs`
  - Test should verify:
    - `ChannelFactory` implements `IAmAChannelFactoryWithScheduler`
    - Setting `Scheduler` property and calling `CreateSyncChannel()` / `CreateAsyncChannel()` results in consumer having the scheduler
    - Works without setting `Scheduler` (backward compat)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Update `src/Paramore.Brighter.MessagingGateway.MsSql/ChannelFactory.cs`
    - Implement `IAmAChannelFactoryWithScheduler`
    - Add `Scheduler` property; pass it to consumer factory when creating consumers

- [x] **TEST + IMPLEMENT: Redis channel factory implements IAmAChannelFactoryWithScheduler**
  - **USE COMMAND**: `/test-first when Redis channel factory has scheduler set should pass it to consumers`
  - Test location: "tests/Paramore.Brighter.Redis.Tests/MessagingGateway"
  - Test file: `When_redis_channel_factory_has_scheduler_should_pass_to_consumers.cs`
  - Test should verify:
    - `ChannelFactory` implements `IAmAChannelFactoryWithScheduler`
    - Setting `Scheduler` property and calling `CreateSyncChannel()` / `CreateAsyncChannel()` results in consumer having the scheduler
    - Works without setting `Scheduler` (backward compat)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Update `src/Paramore.Brighter.MessagingGateway.Redis/ChannelFactory.cs`
    - Implement `IAmAChannelFactoryWithScheduler`
    - Add `Scheduler` property; pass it to consumer factory when creating consumers

- [x] **IMPLEMENT: Update InMemoryChannelFactory to implement IAmAChannelFactoryWithScheduler**
  - Update `src/Paramore.Brighter/InMemoryChannelFactory.cs`
  - Add `IAmAChannelFactoryWithScheduler` to the implements list
  - The existing `_scheduler` field and constructor parameter already provide the backing — add a `Scheduler` property that gets/sets the field
  - No new test needed — existing InMemory tests already verify scheduler pass-through; this just adds the interface marker

## Phase 4: DI Integration — BuildDispatcher Wiring

Depends on Phase 0 (default registration) and Phase 3 (channel factories implement `IAmAChannelFactoryWithScheduler`).

- [x] **TEST + IMPLEMENT: BuildDispatcher sets scheduler on channel factory from DI**
  - **USE COMMAND**: `/test-first when BuildDispatcher creates dispatcher should set scheduler on channel factory from DI`
  - Test location: "tests/Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection.Tests"
  - Test file: `When_building_dispatcher_should_set_scheduler_on_channel_factory.cs`
  - Test should verify:
    - `AddServiceActivator` configured with a channel factory implementing `IAmAChannelFactoryWithScheduler`
    - Scheduler registered via `UseScheduler()` (or default `InMemorySchedulerFactory`)
    - After `BuildDispatcher`, the channel factory's `Scheduler` property is set to the resolved scheduler
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `BuildDispatcher` in `src/Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs`
    - Resolve `IAmAMessageScheduler` from `serviceProvider`
    - Check if `options.DefaultChannelFactory` (or fallback `InMemoryChannelFactory`) implements `IAmAChannelFactoryWithScheduler`
    - If so, set `Scheduler` property to the resolved scheduler

- [x] **TEST + IMPLEMENT: BuildDispatcher works with channel factory that does not implement IAmAChannelFactoryWithScheduler**
  - **USE COMMAND**: `/test-first when BuildDispatcher has channel factory without scheduler interface should still work`
  - Test location: "tests/Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection.Tests"
  - Test file: `When_building_dispatcher_with_non_scheduler_channel_factory_should_work.cs`
  - Test should verify:
    - `AddServiceActivator` configured with a custom `IAmAChannelFactory` that does NOT implement `IAmAChannelFactoryWithScheduler`
    - Dispatcher builds successfully without errors
    - No scheduler-related exceptions thrown
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Ensure the `is IAmAChannelFactoryWithScheduler` check in `BuildDispatcher` gracefully skips when interface is not implemented

## Phase 5: Sample Updates — Explicit Scheduler Registration

Update the TaskQueue samples to explicitly register `InMemorySchedulerFactory` via `UseScheduler()`. While this is a no-op (it's the default), it demonstrates to users how to configure a scheduler and makes the samples self-documenting.

- [x] **IMPLEMENT: Update RMQ TaskQueue samples to explicitly register InMemoryScheduler**
  - Files:
    - `samples/TaskQueue/RMQTaskQueue/GreetingsSender/Program.cs`
    - `samples/TaskQueue/RMQTaskQueue/GreetingsReceiverConsole/Program.cs`
    - `samples/TaskQueue/RMQRequestReply/GreetingsClient/Program.cs`
    - `samples/TaskQueue/RMQRequestReply/GreetingsServer/Program.cs`
  - Add `.UseScheduler(new InMemorySchedulerFactory())` to the `AddBrighter()` / `AddServiceActivator()` call chain
  - Add comment: `// InMemorySchedulerFactory is the default — shown here explicitly to demonstrate scheduler configuration. Replace with HangfireMessageSchedulerFactory or QuartzSchedulerFactory for durable scheduling.`
  - No test needed — samples are not tested

- [x] **IMPLEMENT: Update Kafka TaskQueue samples to explicitly register InMemoryScheduler**
  - Files:
    - `samples/TaskQueue/KafkaTaskQueue/GreetingsSender/Program.cs`
    - `samples/TaskQueue/KafkaTaskQueue/GreetingsReceiverConsole/Program.cs`
    - `samples/TaskQueue/KafkaSchemaRegistry/GreetingsSender/Program.cs`
    - `samples/TaskQueue/KafkaSchemaRegistry/GreetingsReceiverConsole/Program.cs`
    - `samples/TaskQueue/KafkaDynamicEventStream/TaskStatusSender/Program.cs`
    - `samples/TaskQueue/KafkaDynamicEventStream/TaskReceiverConsole/Program.cs`
    - `samples/TaskQueue/KafkaTaskQueueWithDLQ/GreetingsSender/Program.cs`
    - `samples/TaskQueue/KafkaTaskQueueWithDLQ/GreetingsReceiverConsole/Program.cs`
    - `samples/TaskQueue/KafkaTaskQueueWithDLQ/DlqConsole/Program.cs`
  - Add `.UseScheduler(new InMemorySchedulerFactory())` to the `AddBrighter()` / `AddServiceActivator()` call chain
  - Add comment: `// InMemorySchedulerFactory is the default — shown here explicitly to demonstrate scheduler configuration. Replace with HangfireMessageSchedulerFactory or QuartzSchedulerFactory for durable scheduling.`
  - No test needed — samples are not tested

- [x] **IMPLEMENT: Update MsSql TaskQueue samples to explicitly register InMemoryScheduler**
  - Files:
    - `samples/TaskQueue/MsSqlMessagingGateway/GreetingsSender/Program.cs`
    - `samples/TaskQueue/MsSqlMessagingGateway/GreetingsReceiverConsole/Program.cs`
    - `samples/TaskQueue/MsSqlMessagingGateway/CompetingSender/Program.cs`
    - `samples/TaskQueue/MsSqlMessagingGateway/CompetingReceiverConsole/Program.cs`
  - Add `.UseScheduler(new InMemorySchedulerFactory())` to the `AddBrighter()` / `AddServiceActivator()` call chain
  - Add comment: `// InMemorySchedulerFactory is the default — shown here explicitly to demonstrate scheduler configuration. Replace with HangfireMessageSchedulerFactory or QuartzSchedulerFactory for durable scheduling.`
  - No test needed — samples are not tested

- [x] **IMPLEMENT: Update Redis TaskQueue samples to explicitly register InMemoryScheduler**
  - Files:
    - `samples/TaskQueue/RedisTaskQueue/GreetingsSender/Program.cs`
    - `samples/TaskQueue/RedisTaskQueue/GreetingsReceiver/Program.cs`
  - Add `.UseScheduler(new InMemorySchedulerFactory())` to the `AddBrighter()` / `AddServiceActivator()` call chain
  - Add comment: `// InMemorySchedulerFactory is the default — shown here explicitly to demonstrate scheduler configuration. Replace with HangfireMessageSchedulerFactory or QuartzSchedulerFactory for durable scheduling.`
  - No test needed — samples are not tested

- [x] **IMPLEMENT: Update MultiBus TaskQueue samples to explicitly register InMemoryScheduler**
  - Files:
    - `samples/TaskQueue/MultiBus/GreetingsSender/Program.cs`
    - `samples/TaskQueue/MultiBus/GreetingsReceiverConsole/Program.cs`
  - Add `.UseScheduler(new InMemorySchedulerFactory())` to the `AddBrighter()` / `AddServiceActivator()` call chain
  - Add comment: `// InMemorySchedulerFactory is the default — shown here explicitly to demonstrate scheduler configuration. Replace with HangfireMessageSchedulerFactory or QuartzSchedulerFactory for durable scheduling.`
  - No test needed — samples are not tested

## Task Dependencies

```
Phase 0: Foundation
  ├── Default scheduler registration (Task 1)
  ├── Override test (Task 2) — depends on Task 1
  └── IAmAChannelFactoryWithScheduler interface (Task 3)

Phase 1: Consumer Factory Wiring (depends on Phase 0, Task 3)
  ├── RMQ Async consumer factory (Task 4)
  ├── RMQ Sync consumer factory (Task 5)
  ├── Kafka consumer factory (Task 6)
  ├── MsSql consumer factory (Task 7)
  └── Redis consumer factory (Task 8)

Phase 2: MQTT Factory Infrastructure (depends on Phase 0, Task 3)
  ├── MQTT consumer factory (Task 9)
  └── MQTT channel factory (Task 10) — depends on Task 9

Phase 3: Channel Factory Wiring (depends on respective Phase 1 tasks)
  ├── RMQ Async channel factory (Task 11) — depends on Task 4
  ├── RMQ Sync channel factory (Task 12) — depends on Task 5
  ├── Kafka channel factory (Task 13) — depends on Task 6
  ├── MsSql channel factory (Task 14) — depends on Task 7
  ├── Redis channel factory (Task 15) — depends on Task 8
  └── InMemoryChannelFactory update (Task 16) — depends on Task 3 only

Phase 4: DI Integration (depends on Phase 0 + Phase 3)
  ├── BuildDispatcher scheduler wiring (Task 17)
  └── BuildDispatcher backward compat (Task 18)

Phase 5: Sample Updates (depends on Phase 0, Task 1)
  ├── RMQ samples (Task 19)
  ├── Kafka samples (Task 20)
  ├── MsSql samples (Task 21)
  ├── Redis samples (Task 22)
  └── MultiBus samples (Task 23)
```
