# Implementation Tasks: Universal Scheduler Delay Support

**Spec**: 0002-universal_scheduler_delay
**ADR**: [0037-universal-scheduler-delay.md](../../docs/adr/0037-universal-scheduler-delay.md)

## Overview

This task list implements universal scheduler delay support as defined in ADR-0037. The implementation follows TDD and is organized in phases to enable incremental development across multiple PRs.

### Transport Summary

| Transport | Current Delay Approach | Native Support | Required Change |
|-----------|----------------------|----------------|-----------------|
| InMemory | Direct timer | No | Use scheduler via producer |
| RMQ Async | `Task.Delay()` fallback | Yes (DelaySupported flag) | Use producer when native unavailable |
| RMQ Sync | `Task.Delay().Wait()` fallback | Yes (DelaySupported flag) | Use producer when native unavailable |
| Kafka | No-op (immutable stream) | No | Use producer for delayed send |
| MQTT | Returns false | No | Use producer for delayed send |
| MsSql | Ignores delay | No | Use producer for delayed send |
| Postgres | Native SQL UPDATE | Yes | No changes needed |
| Redis | Delay removed (TODO) | No | Use producer for delayed send |

---

## Phase 1: Fix InMemoryScheduler Thread Safety

Before using the scheduler more broadly, we need to fix the race condition identified in the ADR.

- [x] **TEST + IMPLEMENT: InMemoryScheduler uses atomic operations for timer management**
  - **USE COMMAND**: `/test-first when scheduling message with existing id should atomically replace timer`
  - Test location: `tests/Paramore.Brighter.Core.Tests/Scheduler`
  - Test file: `When_scheduling_message_with_existing_id_should_atomically_replace_timer.cs`
  - Test should verify:
    - Concurrent Schedule calls with same ID don't cause race conditions
    - Timer is properly disposed before replacement
    - No orphaned timers remain after concurrent operations
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Replace check-then-act pattern with `AddOrUpdate` in `InMemoryScheduler.cs:67-79`
    - Ensure timer disposal is atomic with replacement
    - Apply same fix to async variant at lines 186-198

---

## Phase 2: InMemoryMessageProducer Scheduler Integration

Modify the producer to use the configured scheduler instead of direct timers.

- [x] **TEST + IMPLEMENT: SendWithDelay uses scheduler when delay is greater than zero**
  - **USE COMMAND**: `/test-first when sending with delay and scheduler configured should use scheduler`
  - Test location: `tests/Paramore.Brighter.Core.Tests/MessageDispatch`
  - Test file: `When_sending_with_delay_and_scheduler_configured_should_use_scheduler.cs`
  - Test should verify:
    - Message is scheduled via `IAmAMessageSchedulerSync.Schedule()`
    - Message is NOT sent immediately to the bus
    - Scheduler receives correct delay value
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Modify `InMemoryMessageProducer.SendWithDelay()` at lines 166-177
    - Check if `Scheduler` is set and delay > TimeSpan.Zero
    - If scheduler available, call `Scheduler.Schedule(message, delay)`
    - Keep direct timer as fallback when no scheduler configured

- [x] **TEST + IMPLEMENT: SendWithDelayAsync uses scheduler when delay is greater than zero**
  - **USE COMMAND**: `/test-first when sending async with delay and scheduler configured should use scheduler`
  - Test location: `tests/Paramore.Brighter.Core.Tests/MessageDispatch`
  - Test file: `When_sending_async_with_delay_and_scheduler_configured_should_use_scheduler.cs`
  - Test should verify:
    - Message is scheduled via `IAmAMessageSchedulerAsync.ScheduleAsync()`
    - Message is NOT sent immediately to the bus
    - Scheduler receives correct delay value
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Modify `InMemoryMessageProducer.SendWithDelayAsync()` at lines 186-199
    - Check if `Scheduler` is `IAmAMessageSchedulerAsync` and delay > TimeSpan.Zero
    - If async scheduler available, call `await Scheduler.ScheduleAsync(message, delay)`
    - Keep direct timer as fallback when no scheduler configured

- [x] **TEST + IMPLEMENT: SendWithDelay sends immediately when delay is zero**
  - **USE COMMAND**: `/test-first when sending with zero delay should send immediately without scheduler`
  - Test location: `tests/Paramore.Brighter.Core.Tests/MessageDispatch`
  - Test file: `When_sending_with_zero_delay_should_send_immediately_without_scheduler.cs`
  - Test should verify:
    - Message is sent directly to bus (not via scheduler)
    - Scheduler is NOT invoked
    - Works regardless of whether scheduler is configured
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add early return in `SendWithDelay()` when delay == TimeSpan.Zero
    - Call `Send(message)` directly

- [x] **TEST + IMPLEMENT: SendWithDelay falls back to timer when no scheduler configured**
  - **USE COMMAND**: `/test-first when sending with delay and no scheduler should use timer fallback`
  - Test location: `tests/Paramore.Brighter.Core.Tests/MessageDispatch`
  - Test file: `When_sending_with_delay_and_no_scheduler_should_use_timer_fallback.cs`
  - Test should verify:
    - Direct timer is created via TimeProvider
    - Message is delivered after delay (using FakeTimeProvider)
    - Backward compatibility is maintained
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Preserve existing timer-based fallback logic
    - Only use fallback when `Scheduler` is null

---

## Phase 3: InMemoryMessageConsumer Producer Delegation

Modify the consumer to delegate delayed requeues to the producer.

- [x] **TEST + IMPLEMENT: Requeue with delay delegates to producer SendWithDelay**
  - **USE COMMAND**: `/test-first when requeuing with delay should delegate to producer`
  - Test location: `tests/Paramore.Brighter.Core.Tests/MessageDispatch`
  - Test file: `When_requeuing_with_delay_should_delegate_to_producer.cs`
  - Test should verify:
    - `InMemoryMessageProducer.SendWithDelay()` is called
    - Producer uses message's topic (not consumer's subscription topic)
    - Message is removed from locked messages
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add lazy `InMemoryMessageProducer?` field to `InMemoryMessageConsumer`
    - Add `EnsureProducer()` method that creates producer on first use
    - Producer should use `message.Header.Topic` for its topic
    - Modify `Requeue()` to call `_producer.SendWithDelay()` when timeout > 0

- [x] **TEST + IMPLEMENT: RequeueAsync with delay delegates to producer SendWithDelayAsync**
  - **USE COMMAND**: `/test-first when requeuing async with delay should delegate to producer`
  - Test location: `tests/Paramore.Brighter.Core.Tests/MessageDispatch`
  - Test file: `When_requeuing_async_with_delay_should_delegate_to_producer.cs`
  - Test should verify:
    - `InMemoryMessageProducer.SendWithDelayAsync()` is called
    - Producer uses message's topic
    - Message is removed from locked messages
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Modify `RequeueAsync()` to call `_producer.SendWithDelayAsync()` when timeout > 0
    - Reuse the lazy producer created in `EnsureProducer()`

- [x] **TEST + IMPLEMENT: Requeue with zero or null delay uses direct bus enqueue**
  - **USE COMMAND**: `/test-first when requeuing with zero delay should use direct bus enqueue`
  - Test location: `tests/Paramore.Brighter.Core.Tests/MessageDispatch`
  - Test file: `When_requeuing_with_zero_delay_should_use_direct_bus_enqueue.cs`
  - Test should verify:
    - Message is enqueued directly to bus (existing behavior)
    - Producer is NOT created
    - Works for both null and TimeSpan.Zero
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Preserve existing `RequeueNoDelay()` logic
    - Only delegate to producer when timeout > TimeSpan.Zero

- [x] **TEST + IMPLEMENT: Consumer disposes producer on disposal**
  - **USE COMMAND**: `/test-first when disposing consumer should dispose lazily created producer`
  - Test location: `tests/Paramore.Brighter.Core.Tests/MessageDispatch`
  - Test file: `When_disposing_consumer_should_dispose_lazily_created_producer.cs`
  - Test should verify:
    - If producer was created, it is disposed with consumer
    - If producer was never created, disposal succeeds without error
    - Both sync and async disposal paths work correctly
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Modify `DisposeCore()` to dispose `_producer` if not null
    - Modify `DisposeAsyncCore()` to dispose `_producer` asynchronously if not null

---

## Phase 4: Consumer Scheduler Injection

The consumer's lazily-created producer needs access to a scheduler.

- [x] **TEST + IMPLEMENT: Consumer passes scheduler to lazily created producer**
  - **USE COMMAND**: `/test-first when consumer creates producer should configure scheduler`
  - Test location: `tests/Paramore.Brighter.Core.Tests/MessageDispatch`
  - Test file: `When_consumer_creates_producer_should_configure_scheduler.cs`
  - Test should verify:
    - Lazily created producer has scheduler set
    - Scheduler is same instance passed to consumer
    - Producer uses scheduler for delayed sends
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add optional `IAmAMessageScheduler? scheduler` parameter to `InMemoryMessageConsumer` constructor
    - Store scheduler in field
    - When creating lazy producer, set `producer.Scheduler = _scheduler`

---

## Phase 5: InMemory Integration Testing

End-to-end tests verifying the complete flow for in-memory transport.

- [x] **TEST + IMPLEMENT: End-to-end delayed requeue via scheduler**
  - **USE COMMAND**: `/test-first when handler defers message should requeue via scheduler after delay`
  - Test location: `tests/Paramore.Brighter.InMemory.Tests/Consumer`
  - Test file: `When_handler_defers_message_should_requeue_via_scheduler_after_delay.cs`
  - Test should verify:
    - Message is NOT immediately available after deferral
    - After advancing FakeTimeProvider, message becomes available
    - Message can be received and processed on retry
    - Message content is preserved through the scheduler flow
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - This is a pure test task - no implementation changes needed
    - Uses FakeTimeProvider to control time
    - Verifies consumer → producer → scheduler → bus flow

- [x] **TEST + IMPLEMENT: Backward compatibility without explicit scheduler**
  - **USE COMMAND**: `/test-first when no scheduler configured should use timer fallback for backward compatibility`
  - Test location: `tests/Paramore.Brighter.InMemory.Tests/Consumer`
  - Test file: `When_no_scheduler_configured_should_use_timer_fallback_for_backward_compatibility.cs`
  - Test should verify:
    - Existing tests without scheduler configuration continue to work
    - Direct timer mechanism is used as fallback
    - Delayed delivery still works via FakeTimeProvider
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - This is a pure test task - no implementation changes needed
    - Ensures existing behavior is preserved

---

## Phase 6: RabbitMQ Consumer Updates

RabbitMQ currently uses `Task.Delay()` when native delay is not supported, which blocks the message pump. Replace with producer-based delay.

### RMQ Async Consumer

- [x] **TEST + IMPLEMENT: RMQ async consumer uses producer SendWithDelayAsync when native delay not supported**
  - **USE COMMAND**: `/test-first when rmq async consumer requeues without native delay should use producer`
  - Test location: `tests/Paramore.Brighter.RMQ.Tests/MessagingGateway`
  - Test file: `When_rmq_async_consumer_requeues_without_native_delay_should_use_producer.cs`
  - Test should verify:
    - When `DelaySupported` is false, `Task.Delay()` is NOT called
    - `RmqMessageProducer.SendWithDelayAsync()` is called instead
    - Message is acknowledged after producer schedules it
    - Original message is removed from queue
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Modify `RmqMessageConsumer.RequeueAsync()` in `Paramore.Brighter.MessagingGateway.RMQ.Async`
    - Add lazy `RmqMessageProducer?` field
    - Replace `Task.Delay(timeout.Value, cancellationToken)` block with producer call
    - Producer needs scheduler injected (add to constructor or property)

- [x] **TEST + IMPLEMENT: RMQ async consumer creates producer lazily with correct configuration**
  - **USE COMMAND**: `/test-first when rmq async consumer creates producer should use message topic and scheduler`
  - Test location: `tests/Paramore.Brighter.RMQ.Tests/MessagingGateway`
  - Test file: `When_rmq_async_consumer_creates_producer_should_use_message_topic_and_scheduler.cs`
  - Test should verify:
    - Producer is only created on first delayed requeue
    - Producer uses message's topic for routing
    - Producer has scheduler configured
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `EnsureProducerAsync()` method
    - Create producer with message topic
    - Set scheduler on producer

- [x] **TEST + IMPLEMENT: RMQ async consumer disposes lazily created producer**
  - **USE COMMAND**: `/test-first when rmq async consumer disposes should dispose producer`
  - Test location: `tests/Paramore.Brighter.RMQ.Tests/MessagingGateway`
  - Test file: `When_rmq_async_consumer_disposes_should_dispose_producer.cs`
  - Test should verify:
    - Producer is disposed when consumer is disposed
    - No resource leaks
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Modify `DisposeAsync()` to dispose producer

### RMQ Sync Consumer

- [x] **TEST + IMPLEMENT: RMQ sync consumer uses producer SendWithDelay when native delay not supported**
  - **USE COMMAND**: `/test-first when rmq sync consumer requeues without native delay should use producer`
  - Test location: `tests/Paramore.Brighter.RMQ.Tests/MessagingGateway`
  - Test file: `When_rmq_sync_consumer_requeues_without_native_delay_should_use_producer.cs`
  - Test should verify:
    - When `DelaySupported` is false, `Task.Delay().Wait()` is NOT called
    - `RmqMessageProducer.SendWithDelay()` is called instead
    - Message is acknowledged after producer schedules it
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Modify `RmqMessageConsumer.Requeue()` in `Paramore.Brighter.MessagingGateway.RMQ.Sync`
    - Add lazy `RmqMessageProducer?` field
    - Replace `Task.Delay(timeout.Value).Wait()` block with producer call

- [x] **TEST + IMPLEMENT: RMQ sync consumer creates producer lazily and disposes it**
  - **USE COMMAND**: `/test-first when rmq sync consumer creates and disposes producer correctly`
  - Test location: `tests/Paramore.Brighter.RMQ.Tests/MessagingGateway`
  - Test file: `When_rmq_sync_consumer_creates_and_disposes_producer_correctly.cs`
  - Test should verify:
    - Producer created on first delayed requeue
    - Producer disposed when consumer disposed
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `EnsureProducer()` method
    - Modify `Dispose()` to dispose producer

---

## Phase 7: Kafka Consumer Updates

Kafka streams are immutable so traditional requeue is not possible. However, for delayed retry we can use the producer to send a new message.

- [x] **TEST + IMPLEMENT: Kafka consumer uses producer for delayed requeue**
  - **USE COMMAND**: `/test-first when kafka consumer requeues with delay should use producer`
  - Test location: `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway`
  - Test file: `When_kafka_consumer_requeues_with_delay_should_use_producer.cs`
  - Test should verify:
    - `KafkaMessageProducer.SendWithDelay()` is called when delay > 0
    - Message is sent to same topic
    - HandledCount is incremented
    - Returns true to indicate success
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Modify `KafkaMessageConsumer.Requeue()` at lines 655-658
    - Add lazy `KafkaMessageProducer?` field
    - When delay > 0, create producer and call `SendWithDelay()`
    - Keep no-op behavior when delay is null or zero

- [x] **TEST + IMPLEMENT: Kafka consumer RequeueAsync uses producer for delayed requeue**
  - **USE COMMAND**: `/test-first when kafka consumer requeues async with delay should use producer`
  - Test location: `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway`
  - Test file: `When_kafka_consumer_requeues_async_with_delay_should_use_producer.cs`
  - Test should verify:
    - `KafkaMessageProducer.SendWithDelayAsync()` is called when delay > 0
    - Returns true to indicate success
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Modify `KafkaMessageConsumer.RequeueAsync()` at lines 667-670
    - Use same lazy producer
    - Call `SendWithDelayAsync()` when delay > 0

- [x] **TEST + IMPLEMENT: Kafka consumer creates producer with correct configuration**
  - **USE COMMAND**: `/test-first when kafka consumer creates producer should configure correctly`
  - Test location: `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway`
  - Test file: `When_kafka_consumer_creates_producer_should_configure_correctly.cs`
  - Test should verify:
    - Producer uses message topic
    - Producer has scheduler configured
    - Producer is disposed with consumer
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add scheduler parameter to consumer constructor
    - Add `EnsureProducer()` method
    - Add producer disposal to `Dispose()` and `DisposeAsync()`

---

## Phase 8: MQTT Consumer Updates

MQTT currently returns false for requeue. Implement using producer for delayed retry.

- [x] **TEST + IMPLEMENT: MQTT consumer uses producer for delayed requeue**
  - **USE COMMAND**: `/test-first when mqtt consumer requeues with delay should use producer`
  - Test location: `tests/Paramore.Brighter.MQTT.Tests/MessagingGateway`
  - Test file: `When_mqtt_consumer_requeues_with_delay_should_use_producer.cs`
  - Test should verify:
    - `MQTTMessageProducer.SendWithDelay()` is called when delay > 0
    - Returns true to indicate success (was returning false)
    - HandledCount is incremented
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Modify `MQTTMessageConsumer.Requeue()` at lines 198-201
    - Add lazy `MQTTMessageProducer?` field
    - When delay > 0, create producer and call `SendWithDelay()`
    - Return true on success

- [x] **TEST + IMPLEMENT: MQTT consumer RequeueAsync uses producer for delayed requeue**
  - **USE COMMAND**: `/test-first when mqtt consumer requeues async with delay should use producer`
  - Test location: `tests/Paramore.Brighter.MQTT.Tests/MessagingGateway`
  - Test file: `When_mqtt_consumer_requeues_async_with_delay_should_use_producer.cs`
  - Test should verify:
    - `MQTTMessageProducer.SendWithDelayAsync()` is called when delay > 0
    - Returns true to indicate success
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Modify `MQTTMessageConsumer.RequeueAsync()` at lines 203-207
    - Use same lazy producer
    - Call `SendWithDelayAsync()` when delay > 0

- [x] **TEST + IMPLEMENT: MQTT consumer creates producer with correct configuration and disposes it**
  - **USE COMMAND**: `/test-first when mqtt consumer creates producer should configure and dispose correctly`
  - Test location: `tests/Paramore.Brighter.MQTT.Tests/MessagingGateway`
  - Test file: `When_mqtt_consumer_creates_producer_should_configure_and_dispose_correctly.cs`
  - Test should verify:
    - Producer uses message topic
    - Producer has scheduler configured
    - Producer is disposed with consumer
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add scheduler parameter to consumer constructor
    - Add `EnsureProducer()` method
    - Add producer disposal

---

## Phase 9: MsSql Consumer Updates

MsSql currently ignores the delay parameter. Implement using producer for delayed retry.

- [x] **TEST + IMPLEMENT: MsSql consumer uses producer for delayed requeue**
  - **USE COMMAND**: `/test-first when mssql consumer requeues with delay should use producer`
  - Test location: `tests/Paramore.Brighter.MsSql.Tests/MessagingGateway`
  - Test file: `When_mssql_consumer_requeues_with_delay_should_use_producer.cs`
  - Test should verify:
    - `MsSqlMessageProducer.SendWithDelay()` is called when delay > 0
    - Message is NOT sent immediately to queue
    - Returns true to indicate success
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Modify `MsSqlMessageConsumer.Requeue()` at lines 129-140
    - Add lazy producer field
    - When delay > 0, use producer's `SendWithDelay()` instead of `_sqlMessageQueue.Send()`

- [x] **TEST + IMPLEMENT: MsSql consumer RequeueAsync uses producer for delayed requeue**
  - **USE COMMAND**: `/test-first when mssql consumer requeues async with delay should use producer`
  - Test location: `tests/Paramore.Brighter.MsSql.Tests/MessagingGateway`
  - Test file: `When_mssql_consumer_requeues_async_with_delay_should_use_producer.cs`
  - Test should verify:
    - `MsSqlMessageProducer.SendWithDelayAsync()` is called when delay > 0
    - Returns true to indicate success
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Modify `MsSqlMessageConsumer.RequeueAsync()` at lines 148-159
    - Use same lazy producer
    - Call `SendWithDelayAsync()` when delay > 0

- [x] **TEST + IMPLEMENT: MsSql consumer preserves immediate requeue behavior**
  - **USE COMMAND**: `/test-first when mssql consumer requeues with zero delay should use direct queue`
  - Test location: `tests/Paramore.Brighter.MsSql.Tests/MessagingGateway`
  - Test file: `When_mssql_consumer_requeues_with_zero_delay_should_use_direct_queue.cs`
  - Test should verify:
    - When delay is null or zero, direct `_sqlMessageQueue.Send()` is used
    - Producer is NOT created for immediate requeue
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Preserve existing direct queue behavior for zero/null delay
    - Only use producer when delay > 0

- [x] **TEST + IMPLEMENT: MsSql consumer creates producer with correct configuration and disposes it**
  - **USE COMMAND**: `/test-first when mssql consumer creates producer should configure and dispose correctly`
  - Test location: `tests/Paramore.Brighter.MsSql.Tests/MessagingGateway`
  - Test file: `When_mssql_consumer_creates_producer_should_configure_and_dispose_correctly.cs`
  - Test should verify:
    - Create consumer directly (not via factory) with a `SpySchedulerSync` injected
    - Requeue with a **non-zero delay** (e.g. `TimeSpan.FromSeconds(5)`) to exercise the scheduler path
    - Assert the spy scheduler's `Schedule()` was called with the correct delay — this proves the scheduler was wired through to the lazily-created producer
    - Producer is disposed with consumer (dispose after requeue should not throw)
    - Dispose without requeue should also not throw
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add scheduler parameter to consumer constructor
    - Store in `_scheduler` field
    - Set `Scheduler = _scheduler` on producer in `EnsureProducer()`
    - Add producer disposal
  - **Lesson from Kafka/MQTT**: Tests that only requeue with null/zero delay never hit the scheduler cast in `SendWithDelay` — a spy with non-zero delay is required to catch missing wiring

---

## Phase 10: Redis Consumer Updates

Redis previously had delay support but it was removed because it blocked the pump. Re-implement using producer.

- [x] **TEST + IMPLEMENT: Redis consumer uses producer for delayed requeue**
  - **USE COMMAND**: `/test-first when redis consumer requeues with delay should use producer`
  - Test location: `tests/Paramore.Brighter.Redis.Tests/MessagingGateway`
  - Test file: `When_redis_consumer_requeues_with_delay_should_use_producer.cs`
  - Test should verify:
    - `RedisMessageProducer.SendWithDelay()` is called when delay > 0
    - Message is NOT immediately added back to list
    - Returns true to indicate success
    - TODO comment is removed/addressed
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Modify `RedisMessageConsumer.Requeue()` at lines 282-305
    - Add lazy `RedisMessageProducer?` field
    - When delay > 0, use producer's `SendWithDelay()` instead of direct list add

- [x] **TEST + IMPLEMENT: Redis consumer RequeueAsync uses producer for delayed requeue**
  - **USE COMMAND**: `/test-first when redis consumer requeues async with delay should use producer`
  - Test location: `tests/Paramore.Brighter.Redis.Tests/MessagingGateway`
  - Test file: `When_redis_consumer_requeues_async_with_delay_should_use_producer.cs`
  - Test should verify:
    - `RedisMessageProducer.SendWithDelayAsync()` is called when delay > 0
    - Returns true to indicate success
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Modify `RedisMessageConsumer.RequeueAsync()` at lines 314-337
    - Use same lazy producer
    - Call `SendWithDelayAsync()` when delay > 0

- [x] **TEST + IMPLEMENT: Redis consumer preserves immediate requeue behavior**
  - **USE COMMAND**: `/test-first when redis consumer requeues with zero delay should use direct list`
  - Test location: `tests/Paramore.Brighter.Redis.Tests/MessagingGateway`
  - Test file: `When_redis_consumer_requeues_with_zero_delay_should_use_direct_list.cs`
  - Test should verify:
    - When delay is null or zero, direct list operations are used
    - Producer is NOT created for immediate requeue
    - Existing behavior is preserved
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Preserve existing direct list behavior for zero/null delay
    - Only use producer when delay > 0

- [x] **TEST + IMPLEMENT: Redis consumer creates producer with correct configuration and disposes it**
  - **USE COMMAND**: `/test-first when redis consumer creates producer should configure and dispose correctly`
  - Test location: `tests/Paramore.Brighter.Redis.Tests/MessagingGateway`
  - Test file: `When_redis_consumer_creates_producer_should_configure_and_dispose_correctly.cs`
  - Test should verify:
    - Create consumer directly (not via factory) with a `SpySchedulerSync` injected
    - Requeue with a **non-zero delay** (e.g. `TimeSpan.FromSeconds(5)`) to exercise the scheduler path
    - Assert the spy scheduler's `Schedule()` was called with the correct delay — this proves the scheduler was wired through to the lazily-created producer
    - Producer is disposed with consumer (dispose after requeue should not throw)
    - Dispose without requeue should also not throw
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add scheduler parameter to consumer constructor
    - Store in `_scheduler` field
    - Set `Scheduler = _scheduler` on producer in `EnsureProducer()`
    - Add producer disposal
  - **Lesson from Kafka/MQTT**: Tests that only requeue with null/zero delay never hit the scheduler cast in `SendWithDelay` — a spy with non-zero delay is required to catch missing wiring

---

## Phase 11: Postgres Consumer (Verification Only)

Postgres has native delay support via SQL UPDATE with `visible_timeout`. Verify it works correctly.

- [x] **TEST: Verify Postgres consumer delay works correctly with native SQL**
  - **USE COMMAND**: `/test-first when postgres consumer requeues with delay should use native sql`
  - Test location: `tests/Paramore.Brighter.Postgres.Tests/MessagingGateway`
  - Test file: `When_postgres_consumer_requeues_with_delay_should_use_native_sql.cs`
  - Test should verify:
    - SQL UPDATE sets `visible_timeout` correctly
    - Message is not visible until after delay
    - No producer is needed
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - This is a pure test task - verify existing behavior
    - No changes needed if native support works correctly

---

## Dependencies

```
Phase 1 (InMemoryScheduler Thread Safety)
    ↓
Phase 2 (InMemoryProducer Scheduler)
    ↓
Phase 3 (InMemoryConsumer Producer Delegation)
    ↓
Phase 4 (InMemoryConsumer Scheduler Injection)
    ↓
Phase 5 (InMemory Integration Tests)
    ↓
    ├── Phase 6 (RabbitMQ) ─────────────────────────┐
    ├── Phase 7 (Kafka) ────────────────────────────┤
    ├── Phase 8 (MQTT) ─────────────────────────────┤ Can be done in parallel
    ├── Phase 9 (MsSql) ────────────────────────────┤
    ├── Phase 10 (Redis) ───────────────────────────┤
    └── Phase 11 (Postgres - verification only) ────┘
```

---

## Suggested PR Breakdown

| PR | Phases | Description |
|----|--------|-------------|
| PR 1 | 1-5 | Core: InMemory scheduler/producer/consumer |
| PR 2 | 6 | RabbitMQ consumer updates |
| PR 3 | 7 | Kafka consumer updates |
| PR 4 | 8 | MQTT consumer updates |
| PR 5 | 9 | MsSql consumer updates |
| PR 6 | 10 | Redis consumer updates |
| PR 7 | 11 | Postgres verification |

Alternatively, all transport updates (Phases 6-11) could be combined into a single PR after PR 1.

---

## Risk Mitigations

| Risk | Mitigation Task |
|------|-----------------|
| Breaking existing tests | Phase 2 task: "falls back to timer when no scheduler" |
| Performance regression | Phase 2 task: "sends immediately when delay is zero" |
| Lazy producer not disposed | Each phase includes disposal task |
| Race conditions in scheduler | Phase 1 task: "atomic operations for timer management" |
| Blocking message pump | All transports use non-blocking producer approach |

---

## Notes

- All tests should use `FakeTimeProvider` for deterministic time control
- Run existing test suite after each phase to catch regressions early
- Each transport's test infrastructure may need Docker containers (RMQ, Kafka, etc.)
- Postgres already has native support - verify and document, no changes expected
