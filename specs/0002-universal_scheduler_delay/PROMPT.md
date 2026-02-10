# Universal Scheduler Delay - Implementation Progress

**Spec**: 0002-universal_scheduler_delay
**Branch**: `universal_delay`
**ADR**: [0037-universal-scheduler-delay.md](../../docs/adr/0037-universal-scheduler-delay.md)

## Current Status

Use `/spec:implement` to continue implementation from the next incomplete task.

### Phase 1: InMemoryScheduler Thread Safety - COMPLETE
- [x] InMemoryScheduler uses atomic operations for timer management

### Phase 2: InMemoryMessageProducer Scheduler Integration - COMPLETE
- [x] SendWithDelay uses scheduler when delay > zero
- [x] SendWithDelayAsync uses scheduler when delay > zero
- [x] SendWithDelay sends immediately when delay is zero
- [x] SendWithDelay falls back to timer when no scheduler configured

### Phase 3: InMemoryMessageConsumer Producer Delegation - COMPLETE
- [x] Requeue with delay delegates to producer SendWithDelay
- [x] RequeueAsync with delay delegates to producer SendWithDelayAsync
- [x] Requeue with zero/null delay uses direct bus enqueue
- [x] Consumer disposes producer on disposal

### Phase 4: Consumer Scheduler Injection - COMPLETE
- [x] Consumer passes scheduler to lazily created producer

### Phase 5: InMemory Integration Testing - COMPLETE
- [x] End-to-end delayed requeue via scheduler
- [x] Backward compatibility without explicit scheduler

### Phase 6: RabbitMQ Consumer Updates - COMPLETE
- [x] RMQ async consumer uses producer SendWithDelayAsync when native delay not supported
- [x] RMQ async consumer creates producer lazily with correct configuration
- [x] RMQ async consumer disposes lazily created producer
- [x] RMQ sync consumer uses producer SendWithDelay when native delay not supported
- [x] RMQ sync consumer creates producer lazily and disposes it

### Phase 7: Kafka Consumer Updates - NOT STARTED
- [ ] Kafka consumer uses producer for delayed requeue
- [ ] Kafka consumer RequeueAsync uses producer for delayed requeue
- [ ] Kafka consumer creates producer with correct configuration

### Phase 8: MQTT Consumer Updates - COMPLETE
- [x] MQTT consumer uses producer for delayed requeue
- [x] MQTT consumer RequeueAsync uses producer for delayed requeue
- [x] MQTT consumer creates producer with correct configuration and disposes it

### Phase 9: MsSql Consumer Updates - NOT STARTED
- [ ] MsSql consumer uses producer for delayed requeue
- [ ] MsSql consumer RequeueAsync uses producer for delayed requeue
- [ ] MsSql consumer preserves immediate requeue behavior
- [ ] MsSql consumer creates producer with correct configuration and disposes it

### Phase 10: Redis Consumer Updates - NOT STARTED
- [ ] Redis consumer uses producer for delayed requeue
- [ ] Redis consumer RequeueAsync uses producer for delayed requeue
- [ ] Redis consumer preserves immediate requeue behavior
- [ ] Redis consumer creates producer with correct configuration and disposes it

### Phase 11: Postgres Consumer (Verification Only) - NOT STARTED
- [ ] Verify Postgres consumer delay works correctly with native SQL

## Implementation Pattern

All transport consumers follow the same pattern (established in Phase 3, applied in Phase 6):

1. Add `_producer` field (nullable, lazily created) and `_scheduler` field (readonly)
2. Add optional `IAmAMessageScheduler? scheduler` parameter to constructor
3. Add `EnsureProducer()` method using `Interlocked.CompareExchange` for thread safety
4. In `Requeue`/`RequeueAsync`: when native delay not supported and timeout > zero, use `_producer.SendWithDelay` instead of blocking
5. Dispose `_producer` in `Dispose()`/`DisposeAsync()`

## Notes

- Phases 7-11 can be done in parallel (independent transports)
- Pre-existing test failures in RMQ: mTLS cert tests (missing certs) and delayed message tests (need DLX exchange) are unrelated
- The sync RMQ consumer used `Task.Delay().Wait()` which blocked the pump; now uses producer
- The async RMQ consumer used `Task.Delay()` which blocked the pump; now uses producer
