# Current Work: Universal Scheduler Delay

**Branch:** `universal_delay`
**Spec:** `specs/0002-universal_scheduler_delay/`
**Status:** Phase 8 (MQTT) in progress - task 1 of 3 complete

## Quick Context

We're adding universal scheduler support for delayed message delivery across all Brighter transports. The goal is to make all consumers use the configurable `IAmAMessageScheduler` system (via producer delegation) instead of the current inconsistent mix of native delays, direct timers, `Task.Delay()`, and no-ops.

## Current State

### What's Done
- [x] Created spec directory `specs/0002-universal_scheduler_delay/`
- [x] Set as current spec in `specs/.current-spec`
- [x] Created `requirements.md` with full requirements analysis
- [x] Approved requirements (`.requirements-approved`)
- [x] Created ADR `docs/adr/0037-universal-scheduler-delay.md`
- [x] Approved design - ADR status set to "Accepted" (`.design-approved`)
- [x] Created comprehensive `tasks.md` with 32 tasks across 11 phases
- [x] Approved tasks (`.tasks-approved`)
- [x] **Phase 1: InMemoryScheduler thread safety fix**
- [x] **Phase 2: InMemoryProducer scheduler integration** (4 tasks)
- [x] **Phase 3: InMemoryConsumer producer delegation** (4 tasks)
- [x] **Phase 4: Consumer scheduler injection** (1 task - combined with Phase 3)
- [x] **Phase 5: InMemory integration testing** (2 tasks)
- [x] **Phase 6: RabbitMQ consumer updates** (5 tasks)
- [x] **Phase 7: Kafka consumer updates** (3 tasks + refactored producer creation to deduplicate 5 methods into 1)
- [x] **Phase 8, Task 1: MQTT sync consumer requeues via producer**

### Phase 8 Progress - MQTT Consumer Updates

MQTT previously returned `false` for requeue (not implemented). Now implementing requeue via lazily-created producer, same pattern as Kafka.

**Task 1 complete - sync `Requeue()`:**
- `Requeue()` increments `HandledCount`, calls `EnsureRequeueProducer()`, then `SendWithDelay()`
- `EnsureRequeueProducer()` creates a `MqttMessageProducer` with a **separate config** (no ClientID to avoid broker disconnecting the consumer)
- Uses `Interlocked.CompareExchange` for thread-safe lazy creation
- Producer disposed in `Dispose()` and `DisposeAsync()`

**Key gotcha:** The requeue producer config must NOT copy the consumer's `ClientID`. MQTT brokers disconnect the first client when a second connects with the same ID. Omitting the ClientID lets MQTTnet generate a unique one.

**Remaining Phase 8 tasks:**
- [ ] Task 2: `RequeueAsync()` delegates to producer `SendWithDelayAsync()`
- [ ] Task 3: Producer configuration (scheduler injection) and disposal verification

**Test file added:**
- `tests/Paramore.Brighter.MQTT.Tests/MessagingGateway/Reactor/When_mqtt_consumer_requeues_with_delay_should_use_producer.cs`

### What's Next
1. Continue Phase 8: MQTT async requeue (task 2), then config/dispose (task 3)
2. After Phase 8: MsSql (9), Redis (10), Postgres verification (11)

## Implementation Summary

| Phase | Focus | Tasks | Status |
|-------|-------|-------|--------|
| 1 | InMemoryScheduler thread safety | 1 | ✅ Complete |
| 2 | InMemoryProducer scheduler integration | 4 | ✅ Complete |
| 3 | InMemoryConsumer producer delegation | 4 | ✅ Complete |
| 4 | Consumer scheduler injection | 1 | ✅ Complete |
| 5 | InMemory integration tests | 2 | ✅ Complete |
| 9 | MsSql consumer updates | 4 | Pending |
| 10 | Redis consumer updates | 4 | Pending |
| 11 | Postgres verification | 1 | Pending |

**Total: 21/32 tasks complete**

### Suggested PR Breakdown

| PR | Phases | Description | Status |
|----|--------|-------------|--------|
| PR 1 | 1-5 | Core: InMemory scheduler/producer/consumer | Ready for review |
| PR 2 | 6 | RabbitMQ consumer updates | Ready for review |
| PR 3 | 7 | Kafka consumer updates | Ready for review |
| PR 4 | 8 | MQTT consumer updates | In progress |
| PR 5 | 9 | MsSql consumer updates | Pending |
| PR 6 | 10 | Redis consumer updates | Pending |
| PR 7 | 11 | Postgres verification | Pending |

## Key Files

| Purpose | Location |
|---------|----------|
| Requirements | `specs/0002-universal_scheduler_delay/requirements.md` |
| ADR (Accepted) | `docs/adr/0037-universal-scheduler-delay.md` |
| Tasks | `specs/0002-universal_scheduler_delay/tasks.md` |
| Scheduler Interfaces | `src/Paramore.Brighter/IAmAMessageScheduler*.cs` |
| InMemoryScheduler | `src/Paramore.Brighter/InMemoryScheduler.cs` |
| InMemoryProducer | `src/Paramore.Brighter/InMemoryMessageProducer.cs` |
| InMemoryConsumer | `src/Paramore.Brighter/InMemoryMessageConsumer.cs` |
| RMQ Async Consumer | `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageConsumer.cs` |
| RMQ Sync Consumer | `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageConsumer.cs` |
| Kafka Consumer | `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessageConsumer.cs` |
| MQTT Consumer | `src/Paramore.Brighter.MessagingGateway.MQTT/MQTTMessageConsumer.cs` |

## Transport Analysis

| Transport | Current Delay | Native Support | Change Needed |
|-----------|--------------|----------------|---------------|
| InMemory | Direct timer | No | ✅ Uses scheduler via producer |
| RMQ | `Task.Delay()` fallback | Yes (flag) | ✅ Uses producer when native unavailable |
| Kafka | No-op | No | ✅ Uses producer for requeue |
| MQTT | Returns false | No | 🔧 Sync done, async pending |
| MsSql | Ignores delay | No | Use producer |
| Postgres | SQL UPDATE | Yes | Verify only |
| Redis | Removed (blocked pump) | No | Use producer |

## Commands Reference

```bash
# Check spec status
/spec:status

# Continue TDD implementation
/spec:implement

# Run MQTT tests
dotnet test tests/Paramore.Brighter.MQTT.Tests/Paramore.Brighter.MQTT.Tests.csproj

# Run Kafka tests
dotnet test tests/Paramore.Brighter.Kafka.Tests/Paramore.Brighter.Kafka.Tests.csproj

# Run RMQ Async tests
dotnet test tests/Paramore.Brighter.RMQ.Async.Tests/Paramore.Brighter.RMQ.Async.Tests.csproj

# Run RMQ Sync tests
dotnet test tests/Paramore.Brighter.RMQ.Sync.Tests/Paramore.Brighter.RMQ.Sync.Tests.csproj

# Run InMemory tests
dotnet test tests/Paramore.Brighter.InMemory.Tests/Paramore.Brighter.InMemory.Tests.csproj
```

## Build & Test

```bash
# Build
dotnet build Brighter.sln

# Run all tests
dotnet test Brighter.sln

# Run specific test project
dotnet test tests/Paramore.Brighter.Core.Tests/Paramore.Brighter.Core.Tests.csproj

# Run InMemory tests (Producer + Consumer + Scheduler)
dotnet test tests/Paramore.Brighter.InMemory.Tests/Paramore.Brighter.InMemory.Tests.csproj --filter "FullyQualifiedName~Producer|FullyQualifiedName~Consumer|FullyQualifiedName~Scheduler"
```

## Recent Commits (Phases 2-5)

- `823eeafd6` - test: add Phase 5 InMemory integration tests for scheduler delay
- `851363a8c` - test: verify consumer zero-delay requeue and producer disposal
- `600048c1d` - feat: InMemoryMessageConsumer.RequeueAsync delegates to producer when scheduler configured
- `2adabd4e2` - feat: InMemoryMessageConsumer.Requeue delegates to producer when scheduler configured
- `a44772523` - test: verify timer fallback when no scheduler configured
- `b5ffa2d9a` - feat: SendWithDelay sends immediately when delay is zero
- `d5ec98daa` - feat: InMemoryMessageProducer.SendWithDelayAsync uses scheduler when configured
- `c734dfd64` - feat: InMemoryMessageProducer.SendWithDelay uses scheduler when configured
