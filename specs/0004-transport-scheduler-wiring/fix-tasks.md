# Fix Tasks: Channel Factory Scheduler Forwarding

**Spec**: 0004-transport-scheduler-wiring
**Bug**: Scheduler property on non-InMemory ChannelFactories is never forwarded to consumers

## Problem

`BuildDispatcher` sets the `Scheduler` property on each transport channel factory via the `IAmAChannelFactoryWithScheduler` interface. But every transport channel factory implements this as a plain auto-property (`{ get; set; }`) that is never read by `CreateSyncChannel` / `CreateAsyncChannel`. The consumer factories hold the scheduler as a `readonly` field set at construction time — before `BuildDispatcher` runs.

Result: the scheduler from DI never reaches the consumers created by Kafka, RMQ, MQTT, MsSql, or Redis channel factories. Only `InMemoryChannelFactory` works correctly because it creates consumers directly.

## Fix Approach (Option 1)

Add a mutable `Scheduler` property to each consumer factory. Have each channel factory's `Scheduler` setter forward the value to its consumer factory. The consumer factory's `Create`/`CreateAsync` methods already read `_scheduler` — making it settable is the minimal change.

## TDD Workflow

Each task follows: RED → APPROVAL → GREEN → REFACTOR

## Task List

### Phase 6A: Consumer Factory Mutable Scheduler Property

Each consumer factory currently has `private readonly IAmAMessageScheduler? _scheduler`. Change to a mutable property so the channel factory can update it after construction.

- [ ] **TEST + IMPLEMENT: Kafka consumer factory scheduler can be updated after construction**
  - **USE COMMAND**: `/test-first when Kafka consumer factory scheduler is set after construction should use updated scheduler`
  - Test location: `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway`
  - Test file: `When_kafka_consumer_factory_scheduler_set_after_construction.cs`
  - Test should verify:
    - `KafkaMessageConsumerFactory` constructed **without** a scheduler
    - `Scheduler` property set to a spy scheduler after construction
    - `Create()` returns consumer that uses the updated scheduler (not null)
    - Constructor-provided scheduler still works (backward compat)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `KafkaMessageConsumerFactory`, change `private readonly IAmAMessageScheduler? _scheduler` to `private IAmAMessageScheduler? _scheduler`
    - Add `public IAmAMessageScheduler? Scheduler { get => _scheduler; set => _scheduler = value; }`
    - No change to `Create()` / `CreateAsync()` — they already read `_scheduler`

- [ ] **TEST + IMPLEMENT: RMQ Async consumer factory scheduler can be updated after construction**
  - **USE COMMAND**: `/test-first when RMQ async consumer factory scheduler is set after construction should use updated scheduler`
  - Test location: `tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway`
  - Test file: `When_rmq_async_consumer_factory_scheduler_set_after_construction.cs`
  - Test should verify:
    - `RmqMessageConsumerFactory` (Async) constructed **without** a scheduler
    - `Scheduler` property set after construction
    - `Create()` / `CreateAsync()` returns consumer that uses the updated scheduler
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Same pattern: remove `readonly`, add `Scheduler` property

- [ ] **TEST + IMPLEMENT: RMQ Sync consumer factory scheduler can be updated after construction**
  - **USE COMMAND**: `/test-first when RMQ sync consumer factory scheduler is set after construction should use updated scheduler`
  - Test location: `tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway`
  - Test file: `When_rmq_sync_consumer_factory_scheduler_set_after_construction.cs`
  - Test should verify:
    - `RmqMessageConsumerFactory` (Sync) constructed **without** a scheduler
    - `Scheduler` property set after construction
    - `Create()` returns consumer that uses the updated scheduler
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Same pattern: remove `readonly`, add `Scheduler` property

- [ ] **TEST + IMPLEMENT: MsSql consumer factory scheduler can be updated after construction**
  - **USE COMMAND**: `/test-first when MsSql consumer factory scheduler is set after construction should use updated scheduler`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/MessagingGateway`
  - Test file: `When_mssql_consumer_factory_scheduler_set_after_construction.cs`
  - Test should verify:
    - `MsSqlMessageConsumerFactory` constructed **without** a scheduler
    - `Scheduler` property set after construction
    - `Create()` / `CreateAsync()` returns consumer that uses the updated scheduler
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Same pattern: remove `readonly`, add `Scheduler` property

- [ ] **TEST + IMPLEMENT: Redis consumer factory scheduler can be updated after construction**
  - **USE COMMAND**: `/test-first when Redis consumer factory scheduler is set after construction should use updated scheduler`
  - Test location: `tests/Paramore.Brighter.Redis.Tests/MessagingGateway`
  - Test file: `When_redis_consumer_factory_scheduler_set_after_construction.cs`
  - Test should verify:
    - `RedisMessageConsumerFactory` constructed **without** a scheduler
    - `Scheduler` property set after construction
    - `Create()` / `CreateAsync()` returns consumer that uses the updated scheduler
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Same pattern: remove `readonly`, add `Scheduler` property

- [ ] **TEST + IMPLEMENT: MQTT consumer factory scheduler can be updated after construction**
  - **USE COMMAND**: `/test-first when MQTT consumer factory scheduler is set after construction should use updated scheduler`
  - Test location: `tests/Paramore.Brighter.MQTT.Tests/MessagingGateway`
  - Test file: `When_mqtt_consumer_factory_scheduler_set_after_construction.cs`
  - Test should verify:
    - `MqttMessageConsumerFactory` constructed **without** a scheduler
    - `Scheduler` property set after construction
    - `Create()` / `CreateAsync()` returns consumer that uses the updated scheduler
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Same pattern: remove `readonly`, add `Scheduler` property

### Phase 6B: Channel Factory Scheduler Forwarding

Each channel factory's `Scheduler` property setter must forward the value to its consumer factory. Change the auto-property to a backing field + explicit setter.

- [ ] **TEST + IMPLEMENT: Kafka channel factory forwards scheduler to consumer factory**
  - **USE COMMAND**: `/test-first when Kafka channel factory scheduler is set should forward to consumer factory and consumers`
  - Test location: `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway`
  - Test file: `When_kafka_channel_factory_forwards_scheduler_to_consumers.cs`
  - Test should verify:
    - `ChannelFactory` constructed with a `KafkaMessageConsumerFactory` (no scheduler in constructor)
    - Set `Scheduler` on the channel factory
    - Call `CreateSyncChannel()` — the returned channel's consumer has the scheduler
    - Call `CreateAsyncChannel()` — same verification
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `Paramore.Brighter.MessagingGateway.Kafka/ChannelFactory.cs`:
    - Replace auto-property with:
      ```csharp
      public IAmAMessageScheduler? Scheduler
      {
          get => _kafkaMessageConsumerFactory.Scheduler;
          set => _kafkaMessageConsumerFactory.Scheduler = value;
      }
      ```

- [ ] **TEST + IMPLEMENT: RMQ Async channel factory forwards scheduler to consumer factory**
  - **USE COMMAND**: `/test-first when RMQ async channel factory scheduler is set should forward to consumer factory and consumers`
  - Test location: `tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway`
  - Test file: `When_rmq_async_channel_factory_forwards_scheduler_to_consumers.cs`
  - Test should verify:
    - Same pattern as Kafka: set scheduler on channel factory, verify consumers get it
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Same pattern: delegate `Scheduler` property to consumer factory

- [ ] **TEST + IMPLEMENT: RMQ Sync channel factory forwards scheduler to consumer factory**
  - **USE COMMAND**: `/test-first when RMQ sync channel factory scheduler is set should forward to consumer factory and consumers`
  - Test location: `tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway`
  - Test file: `When_rmq_sync_channel_factory_forwards_scheduler_to_consumers.cs`
  - Test should verify:
    - Same pattern as Kafka
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Same pattern: delegate `Scheduler` property to consumer factory

- [ ] **TEST + IMPLEMENT: MsSql channel factory forwards scheduler to consumer factory**
  - **USE COMMAND**: `/test-first when MsSql channel factory scheduler is set should forward to consumer factory and consumers`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/MessagingGateway`
  - Test file: `When_mssql_channel_factory_forwards_scheduler_to_consumers.cs`
  - Test should verify:
    - Same pattern as Kafka
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Same pattern: delegate `Scheduler` property to consumer factory

- [ ] **TEST + IMPLEMENT: Redis channel factory forwards scheduler to consumer factory**
  - **USE COMMAND**: `/test-first when Redis channel factory scheduler is set should forward to consumer factory and consumers`
  - Test location: `tests/Paramore.Brighter.Redis.Tests/MessagingGateway`
  - Test file: `When_redis_channel_factory_forwards_scheduler_to_consumers.cs`
  - Test should verify:
    - Same pattern as Kafka
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Same pattern: delegate `Scheduler` property to consumer factory

- [ ] **TEST + IMPLEMENT: MQTT channel factory forwards scheduler to consumer factory**
  - **USE COMMAND**: `/test-first when MQTT channel factory scheduler is set should forward to consumer factory and consumers`
  - Test location: `tests/Paramore.Brighter.MQTT.Tests/MessagingGateway`
  - Test file: `When_mqtt_channel_factory_forwards_scheduler_to_consumers.cs`
  - Test should verify:
    - Same pattern as Kafka
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Same pattern: delegate `Scheduler` property to consumer factory

### Phase 6C: End-to-End Verification

- [ ] **TEST: BuildDispatcher scheduler reaches consumers through channel factory**
  - **USE COMMAND**: `/test-first when BuildDispatcher sets scheduler on channel factory should reach consumers created by that factory`
  - Test location: `tests/Paramore.Brighter.Extensions.Tests`
  - Test file: `When_building_dispatcher_scheduler_reaches_consumers.cs`
  - Test should verify:
    - Configure `AddServiceActivator` with a scheduler and a channel factory (e.g., InMemory with a spy)
    - Build the dispatcher
    - Create a channel from the factory
    - Verify the channel's consumer has the scheduler set
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - This may require no new production code — just validates the full wiring path

### Phase 6D: Regression

- [ ] **Run existing test suites for all affected transports**
  - `dotnet test tests/Paramore.Brighter.Core.Tests/`
  - `dotnet test tests/Paramore.Brighter.Extensions.Tests/`
  - Verify no regressions from making consumer factory scheduler fields mutable

## Task Dependencies

```
Phase 6A: Consumer Factory Mutable Property (all 6 tasks independent)
    ↓
Phase 6B: Channel Factory Forwarding (each depends on corresponding 6A task)
    ↓
Phase 6C: End-to-End Verification (depends on all of 6B)
    ↓
Phase 6D: Regression (depends on 6C)
```

## Risk Mitigation

- **Risk**: Breaking backward compatibility for users who pass scheduler via constructor
  - **Mitigation**: Constructor parameter remains; the property is an additional path. Tests verify both work.

- **Risk**: Thread safety — scheduler set on one thread, consumers created on another
  - **Mitigation**: `BuildDispatcher` sets the scheduler during startup before any consumers are created. This is the same pattern as `InMemoryChannelFactory` which already works this way.
