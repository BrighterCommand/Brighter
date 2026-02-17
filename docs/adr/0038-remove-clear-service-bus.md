# 38. Remove ClearServiceBus

Date: 2026-02-07

## Status

Accepted

## Context

**Parent Requirement**: [specs/0003-remove_clear_event_bus_calls/requirements.md](../../specs/0003-remove_clear_event_bus_calls/requirements.md)

**Scope**: This ADR addresses the removal of `CommandProcessor.ClearServiceBus()` call sites and the `[Collection("CommandProcessor")]` xUnit serialization attributes from the test suite, following the completion of ADR 0034's instance-based mediator changes.

### Background

ADR 0033 introduced `ClearServiceBus()` as a testing workaround when `OutboxProducerMediator` was a static singleton. Tests shared the same static mediator, outbox, and producer state. Without explicit cleanup, state leaked between tests, causing flaky failures. The `[Collection("CommandProcessor")]` attribute was also required to serialize test execution and prevent concurrent access to that shared static state.

ADR 0034 resolved the root cause by making the mediator an instance field (`_mediator`) on `CommandProcessor`. Each `CommandProcessor` now owns its own `OutboxProducerMediator`, `InMemoryOutbox`, and producer instances. The method was marked `[Obsolete]` but retained for backward compatibility.

### Current State of `ClearServiceBus()`

The method currently clears four static `ConcurrentDictionary<string, MethodInfo>` reflection caches:

```csharp
[Obsolete("No longer needed - each CommandProcessor instance has its own mediator. Will be removed in next major version.")]
public static void ClearServiceBus()
{
    s_boundDepositCalls.Clear();
    s_boundDepositCallsAsync.Clear();
    s_boundBulkDepositCalls.Clear();
    s_boundBulkDepositCallsAsync.Clear();
}
```

These caches store `MethodInfo` objects for reflected generic method calls (e.g., `DepositPost<TRequest, TTransaction>`), keyed by `"{RequestTypeName}:{TransactionTypeName}"`. They are **stateless performance optimizations** — the same key always produces the same `MethodInfo`. Clearing them has no correctness impact; it only forces re-reflection.

A fifth cache, `s_boundMediatorMethods`, is not cleared by `ClearServiceBus()` and has never caused issues.

### Forces

- **~201 files** call `ClearServiceBus()` — almost entirely test files
- **~178 test files** use `[Collection("CommandProcessor")]` to serialize execution
- Both patterns exist solely because of the **former** static mediator design
- The `[Obsolete]` attribute already signals to external consumers that the method is deprecated
- External consumers calling `ClearServiceBus()` should not break — the method should remain but become a no-op
- Tests serialized by `[Collection("CommandProcessor")]` run slower than necessary, as they cannot execute in parallel

## Decision

### 1. Make `ClearServiceBus()` an Empty No-Op

Remove the body of `ClearServiceBus()` but retain the method signature and `[Obsolete]` attribute. This preserves backward compatibility for external consumers while removing the unnecessary cache clearing:

```csharp
[Obsolete("No longer needed - each CommandProcessor instance has its own mediator. Will be removed in next major version.")]
public static void ClearServiceBus()
{
    // No-op: reflection caches are stateless and safe to share.
    // Mediator state is instance-based since ADR 0034.
}
```

**Rationale**: The method is already `[Obsolete]`. Making it an empty no-op is the least disruptive path — external consumers get a compile warning but no runtime error. Full removal can happen in the next major version (V11).

### 2. Remove All Internal Call Sites

Remove all calls to `ClearServiceBus()` from the codebase (~201 files). These calls are in:

- **`IDisposable.Dispose()` methods** (~90%): Remove the call, and if `Dispose()` becomes empty, remove the `IDisposable` implementation entirely
- **Constructor setup** (~5%): Remove the call from test constructors
- **Base fixture classes** (~3%): Remove the call from shared base fixtures
- **Inline in test methods** (~2%): Remove the call from individual tests

### 3. Remove `[Collection("CommandProcessor")]` Attributes

Remove `[Collection("CommandProcessor")]` from ~178 test files. This attribute serialized test execution to prevent concurrent access to the former static mediator. With the mediator now instance-based, tests can safely run in parallel.

**Phased approach**: If any test fails after removal, investigate whether the collection attribute was needed for a reason other than static mediator state (e.g., shared infrastructure like a database or message broker). Re-add the attribute only for tests with genuine shared-resource constraints.

Note: Other collection attributes (e.g., `[Collection("MQTT")]`, `[Collection("Kafka")]`, `[Collection("Scheduler SQS")]`) are **not** affected — they serialize tests that share external infrastructure.

### 4. Leave Static Reflection Caches As-Is

The five static `ConcurrentDictionary<string, MethodInfo>` caches remain static:

- `s_boundDepositCalls`
- `s_boundDepositCallsAsync`
- `s_boundBulkDepositCalls`
- `s_boundBulkDepositCallsAsync`
- `s_boundMediatorMethods`

**Rationale**: These caches are:
- **Stateless** — same input always produces same output
- **Thread-safe** — backed by `ConcurrentDictionary`
- **Performance-beneficial** — shared across `CommandProcessor` instances to avoid repeated reflection
- **No correctness impact** — clearing them only forces re-reflection, never causes wrong behavior

Making them instance-based would reduce cache effectiveness (especially when `CommandProcessor` has scoped/transient lifetime) with no testability benefit.

### Implementation Approach

The work is primarily a **structural change** (removing dead code and unnecessary attributes) with no behavioral change. Following Beck's "Tidy First" approach:

1. **Phase 1 — Structural**: Empty the `ClearServiceBus()` body, remove all call sites, remove `[Collection("CommandProcessor")]` attributes
2. **Phase 2 — Validation**: Run all unit tests to confirm no regressions
3. **Phase 3 — Cleanup**: Remove empty `Dispose()` methods and `IDisposable` implementations where `ClearServiceBus()` was the only statement

## Consequences

### Positive

- **Faster test execution**: Removing `[Collection("CommandProcessor")]` allows ~178 test classes to run in parallel instead of sequentially
- **Simpler test code**: Tests no longer need boilerplate `Dispose()` methods just to call `ClearServiceBus()`
- **Clearer architecture**: The codebase no longer contains vestiges of the former static singleton design
- **Reduced confusion**: New contributors won't wonder why `ClearServiceBus()` is called everywhere or what happens if they forget it

### Negative

- **Large diff**: ~201 files modified to remove call sites, ~178 files to remove collection attributes — high risk of merge conflicts with concurrent work
- **External consumer impact**: Any external code calling `ClearServiceBus()` will still compile (method exists, just empty) but the `[Obsolete]` warning remains

### Risks and Mitigations

- **Risk**: A test may rely on `[Collection("CommandProcessor")]` for a reason other than static mediator state (e.g., shared test infrastructure)
  - **Mitigation**: Phased approach — run tests after removal, investigate and re-add the attribute only where genuinely needed
- **Risk**: Large number of file changes could introduce merge conflicts
  - **Mitigation**: Perform as a focused cleanup on a dedicated branch, merge promptly
- **Risk**: External consumers may depend on the cache-clearing behavior
  - **Mitigation**: The caches are stateless performance optimizations — clearing them was always a no-op in terms of correctness. No external consumer should depend on this behavior

## Alternatives Considered

### 1. Remove `ClearServiceBus()` Entirely

Deleting the method would be a breaking API change. Since it is already `[Obsolete]`, full removal is appropriate for the next major version (V11) but unnecessarily disruptive now.

### 2. Make Reflection Caches Instance-Based

Moving the `ConcurrentDictionary` caches to instance fields would eliminate the need for any static clearing. However, this would reduce cache effectiveness in production (each `CommandProcessor` instance would maintain its own cache) with no correctness benefit. The caches are already deterministic and safe to share.

### 3. Keep `ClearServiceBus()` Functional But Remove Call Sites

Leaving the cache-clearing logic in the method body while removing call sites would have no practical difference (since no one calls it). Making it an explicit no-op is clearer about intent.

## References

- Requirements: [specs/0003-remove_clear_event_bus_calls/requirements.md](../../specs/0003-remove_clear_event_bus_calls/requirements.md)
- [ADR 0033: Lifetime of Command Processor and Mediator](0033-lifetime-of-command-processor-and-mediator.md) — established the static mediator and `ClearServiceBus()` workaround
- [ADR 0034: Instance-Based Mediator for Testability](0034-instance-based-mediator-for-testability.md) — made mediator instance-based, deprecated `ClearServiceBus()`
