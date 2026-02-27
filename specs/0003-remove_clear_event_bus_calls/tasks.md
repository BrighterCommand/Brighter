# Tasks: Remove ClearServiceBus Calls

**Spec**: 0003-remove_clear_event_bus_calls
**Design**: [ADR 0038](../../docs/adr/0038-remove-clear-service-bus.md)
**Branch**: `clearbus_remove`

## Overview

This is primarily a structural cleanup (removing dead code and unnecessary attributes) with no behavioral change. Following ADR 0038's phased approach:

- **Phase 1**: Empty the `ClearServiceBus()` body, remove all call sites
- **Phase 2**: Remove `[Collection("CommandProcessor")]` attributes
- **Phase 3**: Validate all tests pass

### Scope Summary

| Change | Count |
|--------|-------|
| `ClearServiceBus()` call sites to remove | ~203 files |
| `[Collection("CommandProcessor")]` attributes to remove | ~178 files |
| Test projects affected | 8 |

### Test Projects Affected (by ClearServiceBus call count)

| Project | ClearServiceBus calls | Collection attributes |
|---------|----------------------|----------------------|
| Paramore.Brighter.Core.Tests | 178 | 169 |
| Paramore.Brighter.InMemory.Tests | 7 | 0 |
| Paramore.Brighter.RMQ.Async.Tests | 4 | 4 |
| Paramore.Brighter.Quartz.Tests | 4 | 0 |
| Paramore.Brighter.Hangfire.Tests | 4 | 0 |
| Paramore.Brighter.RocketMQ.Tests | 3 | 3 |
| Paramore.Brighter.RMQ.Sync.Tests | 2 | 2 |
| Paramore.Brighter.TickerQ.Tests | 1 | 0 |

---

## Phase 1: Make ClearServiceBus() a No-Op and Remove Call Sites

### Task 1.1

- [x] **STRUCTURAL: Make `ClearServiceBus()` an empty no-op method**
  - File: `src/Paramore.Brighter/CommandProcessor.cs` (lines ~1496-1503)
  - Remove the four `.Clear()` calls from the method body
  - Keep the method signature and `[Obsolete]` attribute intact
  - Add a comment: `// No-op: reflection caches are stateless and safe to share. Mediator state is instance-based since ADR 0034.`
  - **Verify**: Solution builds with `dotnet build Brighter.slnx`

### Task 1.2

- [x] **STRUCTURAL: Remove all `ClearServiceBus()` calls from Paramore.Brighter.Core.Tests**
  - ~178 files in `tests/Paramore.Brighter.Core.Tests/`
  - Remove `CommandProcessor.ClearServiceBus();` calls from Dispose() methods, constructors, and inline usage
  - Where `Dispose()` becomes empty after removal, remove the entire `Dispose()` method and `IDisposable` implementation (including `: IDisposable` from the class declaration)
  - **Verify**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --no-build` passes

### Task 1.3

- [x] **STRUCTURAL: Remove all `ClearServiceBus()` calls from Paramore.Brighter.InMemory.Tests**
  - 7 files in `tests/Paramore.Brighter.InMemory.Tests/`
  - Same approach as Task 1.2
  - **Verify**: `dotnet test tests/Paramore.Brighter.InMemory.Tests/ --no-build` passes

### Task 1.4

- [x] **STRUCTURAL: Remove all `ClearServiceBus()` calls from transport and scheduler test projects**
  - 4 files in `tests/Paramore.Brighter.RMQ.Async.Tests/`
  - 2 files in `tests/Paramore.Brighter.RMQ.Sync.Tests/`
  - 3 files in `tests/Paramore.Brighter.RocketMQ.Tests/`
  - 4 files in `tests/Paramore.Brighter.Quartz.Tests/`
  - 4 files in `tests/Paramore.Brighter.Hangfire.Tests/`
  - 1 file in `tests/Paramore.Brighter.TickerQ.Tests/`
  - Same approach as Task 1.2 — remove calls and clean up empty Dispose methods
  - **Verify**: `dotnet build Brighter.slnx` compiles cleanly (integration tests require infrastructure)

### Task 1.5

- [x] **VALIDATION: Run full unit test suite and confirm no regressions**
  - Run: `dotnet test tests/Paramore.Brighter.Core.Tests/`
  - Run: `dotnet test tests/Paramore.Brighter.InMemory.Tests/`
  - Run:  `dotnet test tests/Paramore.Brighter.Extensions.Tests`
  - All tests must pass
  - If any test fails, investigate root cause — do NOT re-add `ClearServiceBus()` calls without understanding why

### Task 1.6 

- [ ] **VALIDATION: Run full integration test suite and confirm no regressions**
  - Run `docker compose -f docker-compose-rmq.yaml up -d`
  - Run: `dotnet test tests/Paramore.Brighter.RMQ.Async.Tests/` 
  - Run: `dotnet test tests/Paramore.Brighter.RMQ.Sync.Tests/`
  - Run: `dotnet test Paramore.Brighter.RMQ.Sync.Tests`
  - Run `docker compose -f docker-compose-rmq.yaml down`
  - Run `docker compose -f docker-compose-rocketmq.yaml up -d`
  - Run: `dotnet test testsParamore.Brighter.RocketMQ.Tests/`
  - Run `docker compose -f docker-compose-rocketmq.yaml down`
---

## Phase 2: Remove `[Collection("CommandProcessor")]` Attributes

### Task 2.1

- [x] **STRUCTURAL: Remove `[Collection("CommandProcessor")]` from Paramore.Brighter.Core.Tests**
  - ~169 files in `tests/Paramore.Brighter.Core.Tests/`
  - Remove the `[Collection("CommandProcessor")]` attribute from each test class
  - If it was the only usage of `Xunit` collection, check if the `using` statement can be removed (unlikely — xUnit `[Fact]`/`[Theory]` will still need it)
  - **Verify**: `dotnet test tests/Paramore.Brighter.Core.Tests/` passes
  - **Risk mitigation**: If any test fails, investigate whether the Collection attribute was needed for a reason other than static mediator state. Re-add only for tests with genuine shared-resource constraints.

### Task 2.2

- [x] **STRUCTURAL: Remove `[Collection("CommandProcessor")]` from transport test projects**
  - 4 files in `tests/Paramore.Brighter.RMQ.Async.Tests/`
  - 2 files in `tests/Paramore.Brighter.RMQ.Sync.Tests/`
  - 3 files in `tests/Paramore.Brighter.RocketMQ.Tests/`
  - Same approach as Task 2.1
  - **Verify**: `dotnet build Brighter.slnx` compiles cleanly
  - **Note**: These transport tests may require infrastructure (RabbitMQ, RocketMQ) to run. Verify at build level; full test execution may need Docker.

---

## Phase 3: Validation

### Task 3.1

- [ ] **VALIDATION: Run full unit test suite and confirm no regressions**
  - Run: `dotnet test tests/Paramore.Brighter.Core.Tests/`
  - Run: `dotnet test tests/Paramore.Brighter.InMemory.Tests/`
  - Run `docker compose -f docker-compose-rmq.yaml up -d`
  - Run: `dotnet test tests/Paramore.Brighter.RMQ.Async.Tests/`
  - Run: `dotnet test tests/Paramore.Brighter.RMQ.Sync.Tests/`
  - Run: `dotnet test Paramore.Brighter.RMQ.Sync.Tests`
  - Run `docker compose -f docker-compose-rmq.yaml down`
  - All tests must pass
  - If any test fails, investigate root cause — do NOT re-add `ClearServiceBus()` calls without understanding why

### Task 3.2

- [x] **VALIDATION: Verify no remaining references to ClearServiceBus in test code**
  - Search for `ClearServiceBus` across the entire codebase
  - Only the method definition in `CommandProcessor.cs` and documentation (ADR, requirements) should remain
  - Search for `ClearEventBus` — should have zero references
  - Search for `Collection("CommandProcessor")` — should have zero references in test files

---

## Task Dependencies

```
Task 1.1 (no-op method) ──→ Task 1.2 (Core.Tests calls)
                          ├─→ Task 1.3 (InMemory.Tests calls)
                          └─→ Task 1.4 (transport/scheduler calls)

Task 1.2 ──→ Task 2.1 (Core.Tests Collection attrs)
Task 1.4 ──→ Task 2.2 (transport Collection attrs)

Task 2.1 ─┬─→ Task 3.1 (full test validation)
Task 2.2 ─┘
Task 3.1 ──→ Task 3.2 (final verification)
```

## Notes

- **No TDD tasks**: This spec is entirely structural cleanup (removing dead code). There are no new behaviors to test. The existing test suite serves as the regression safety net.
- **Batch operations**: Tasks 1.2-1.4 and 2.1-2.2 involve bulk find-and-replace across many files. Use scripted approaches where possible.
- **Empty Dispose cleanup**: When removing `ClearServiceBus()` from `Dispose()`, check if any other statements remain. Only remove the `IDisposable` implementation if `Dispose()` is completely empty after the removal.
