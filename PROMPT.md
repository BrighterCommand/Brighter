# Current Work: Universal Scheduler Delay

**Branch:** `universal_delay`
**Spec:** `specs/0002-universal_scheduler_delay/`
**Status:** Phases 1-4 complete - ready for Phase 5 (Integration Tests)

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

### Phase 2 Summary - Producer Scheduler Integration
Modified `InMemoryMessageProducer` to use configured scheduler for delayed sends:
- `SendWithDelay()` uses scheduler when configured and delay > 0
- `SendWithDelayAsync()` uses async scheduler when configured and delay > 0
- Zero/null delay sends immediately via `Send()`
- Timer fallback preserved for backward compatibility

**Tests added:**
- `When_sending_with_delay_and_scheduler_configured_should_use_scheduler`
- `When_sending_async_with_delay_and_scheduler_configured_should_use_scheduler`
- `When_sending_with_zero_delay_should_send_immediately_without_scheduler`
- `When_sending_with_delay_and_no_scheduler_should_use_timer_fallback`

### Phase 3 Summary - Consumer Producer Delegation
Modified `InMemoryMessageConsumer` to delegate delayed requeues to producer:
- Added optional `scheduler` parameter to constructor
- `Requeue()` delegates to `producer.SendWithDelay()` when scheduler configured
- `RequeueAsync()` delegates to `producer.SendWithDelayAsync()` when scheduler configured
- Zero/null delay uses direct bus enqueue (existing behavior)
- Lazy producer created on first delayed requeue
- Producer disposed with consumer

**Tests added:**
- `When_requeuing_with_delay_should_delegate_to_producer`
- `When_requeuing_async_with_delay_should_delegate_to_producer`
- `When_requeuing_with_zero_delay_should_use_direct_bus_enqueue`
- `When_disposing_consumer_should_dispose_lazily_created_producer`

### What's Next
1. Continue with Phase 5: InMemory Integration Testing
2. Then transport-specific phases (6-11) can be done as separate PRs

## Implementation Summary

| Phase | Focus | Tasks | Status |
|-------|-------|-------|--------|
| 1 | InMemoryScheduler thread safety | 1 | ✅ Complete |
| 2 | InMemoryProducer scheduler integration | 4 | ✅ Complete |
| 3 | InMemoryConsumer producer delegation | 4 | ✅ Complete |
| 4 | Consumer scheduler injection | 1 | ✅ Complete |
| 5 | InMemory integration tests | 2 | Pending |
| 6 | RabbitMQ consumer updates | 5 | Pending |
| 7 | Kafka consumer updates | 3 | Pending |
| 8 | MQTT consumer updates | 3 | Pending |
| 9 | MsSql consumer updates | 4 | Pending |
| 10 | Redis consumer updates | 4 | Pending |
| 11 | Postgres verification | 1 | Pending |

**Total: 10/32 tasks complete**

### Suggested PR Breakdown

| PR | Phases | Description |
|----|--------|-------------|
| PR 1 | 1-5 | Core: InMemory scheduler/producer/consumer |
| PR 2 | 6 | RabbitMQ consumer updates |
| PR 3 | 7 | Kafka consumer updates |
| PR 4 | 8 | MQTT consumer updates |
| PR 5 | 9 | MsSql consumer updates |
| PR 6 | 10 | Redis consumer updates |
| PR 7 | 11 | Postgres verification |

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

## Transport Analysis

| Transport | Current Delay | Native Support | Change Needed |
|-----------|--------------|----------------|---------------|
| InMemory | Direct timer | No | ✅ Uses scheduler via producer |
| RMQ | `Task.Delay()` fallback | Yes (flag) | Use producer when native unavailable |
| Kafka | No-op | No | Use producer |
| MQTT | Returns false | No | Use producer |
| MsSql | Ignores delay | No | Use producer |
| Postgres | SQL UPDATE | Yes | Verify only |
| Redis | Removed (blocked pump) | No | Use producer |

## Commands Reference

```bash
# Check spec status
/spec:status

# Continue TDD implementation
/spec:implement

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

## Recent Commits (Phase 2-4)

- `851363a8c` - test: verify consumer zero-delay requeue and producer disposal
- `600048c1d` - feat: InMemoryMessageConsumer.RequeueAsync delegates to producer when scheduler configured
- `2adabd4e2` - feat: InMemoryMessageConsumer.Requeue delegates to producer when scheduler configured
- `a44772523` - test: verify timer fallback when no scheduler configured
- `b5ffa2d9a` - feat: SendWithDelay sends immediately when delay is zero
- `d5ec98daa` - feat: InMemoryMessageProducer.SendWithDelayAsync uses scheduler when configured
- `c734dfd64` - feat: InMemoryMessageProducer.SendWithDelay uses scheduler when configured
