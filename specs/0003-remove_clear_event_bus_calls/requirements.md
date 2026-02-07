# Requirements: Remove ClearServiceBus Calls

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

## Problem Statement

As a Brighter maintainer, I would like to remove the obsolete `CommandProcessor.ClearServiceBus()` method and all calls to it, so that the codebase reflects the current instance-based architecture and tests do not rely on clearing shared static state.

### Background

`CommandProcessor.ClearServiceBus()` was originally needed when `OutboxProducerMediator` was a static singleton (ADR 0033). Tests had to call it to avoid state leaking between test runs. ADR 0034 moved the mediator to an instance field (`_mediator`), making each `CommandProcessor` own its own mediator. The method was marked `[Obsolete]` but retained for backward compatibility.

Currently, `ClearServiceBus()` only clears four static `ConcurrentDictionary<string, MethodInfo>` reflection caches:

- `s_boundDepositCalls`
- `s_boundDepositCallsAsync`
- `s_boundBulkDepositCalls`
- `s_boundBulkDepositCallsAsync`

These caches store bound generic `MethodInfo` objects keyed by `"{RequestTypeName}:{TransactionTypeName}"`. They are stateless performance optimizations — clearing them has no correctness impact, it only forces re-reflection on the next call.

A fifth cache, `s_boundMediatorMethods`, is **not** cleared by `ClearServiceBus()`.

There are **~201 files** (almost entirely tests) that call `ClearServiceBus()` or its alias `ClearEventBus()`.

## Proposed Solution

### Investigation Questions

1. **Are the remaining static reflection caches needed as static?** The caches (`s_boundDepositCalls`, etc.) store `MethodInfo` keyed by type name + transaction type. They are stateless and produce identical results regardless of when they are populated. In production, sharing them across `CommandProcessor` instances is beneficial (avoids repeated reflection). Making them instance-based would reduce cache effectiveness when `CommandProcessor` has scoped/transient lifetime.

2. **Do tests need to call `ClearServiceBus()` for correctness?** No. Since the caches are keyed by fully-qualified type names and the cached `MethodInfo` is deterministic, clearing them between tests has no correctness impact. Tests call it out of historical caution from when the method also cleared the static mediator singleton. With the mediator now instance-based, the only effect of clearing is forcing unnecessary re-reflection.

3. **Can `ClearServiceBus()` be removed entirely?** Yes. The static reflection caches are safe to share and never need clearing. The method can be removed along with all ~201 call sites.

### Proposed Approach

- Make `ClearServiceBus()` an empty method that does nothing
  - Keep the `[Obsolete]` attribute (since the method itself will not be removed until V11)  
- Remove all calls to these methods from the test suite (~201 files)
- Leave the static reflection caches as-is (they are beneficial for performance and safe to share)

## Requirements

### Functional Requirements

1. Make the `ClearServiceBus()` static method from `CommandProcessor` do nothing
2. Remove all calls to `ClearServiceBus()` across the codebase (~201 files)
3. Ensure no test relies on cache clearing for correctness (all tests must pass without the calls)
4. Static reflection caches (`s_boundDepositCalls`, etc.) remain static — they are performance caches and safe to share
5. Remove `[Collection("CommandProcessor")]` attributes from tests 
  - It is possible these are needed for other reasons, so take a phased approach to this, remove them and put them back if a test subsequently fails.

### Non-functional Requirements

- All existing tests must continue to pass after removal
- No performance regression — reflection caches remain static for production benefit
- No behavioral change — only removal of a no-op cleanup step

### Constraints and Assumptions

- **Assumption**: The mediator is fully instance-based per ADR 0034, so clearing static state for mediator isolation is no longer needed
- **Assumption**: The reflection caches produce deterministic results (same type names always map to same `MethodInfo`) — no test can pollute them with incorrect entries
- **Constraint**: This is a breaking change for any external consumers calling `ClearServiceBus()`. Since it is already marked `[Obsolete]`, this is expected for the next major version

### Out of Scope

- Making the static reflection caches instance-based (unnecessary — they are stateless performance caches)
- Changing the `OutboxProducerMediator` semaphores (they must remain static for global concurrency coordination)
- Refactoring the reflection-based dispatch pattern itself

## Acceptance Criteria

1. `CommandProcessor.ClearServiceBus()` method is empty 
2. No references to `ClearServiceBus()` remain in the codebase
3. All unit tests pass without the clearing calls
4. All integration tests pass without the clearing calls
5. No new test failures introduced by the removal
6. Static reflection caches (`s_boundDepositCalls`, `s_boundDepositCallsAsync`, `s_boundBulkDepositCalls`, `s_boundBulkDepositCallsAsync`, `s_boundMediatorMethods`) remain as static `ConcurrentDictionary` fields
7.  Remove `[Collection("CommandProcessor")]` attributes from tests
8. Unit tests pass without the Collection attribute
4. Integration tests pass without the Collection attribute
5. No new test failures introduced by the removal of the attribute

## Additional Context

### Related ADRs
- [ADR 0033: Lifetime of Command Processor and Mediator](../../docs/adr/0033-lifetime-of-command-processor-and-mediator.md) — established the static mediator pattern and `ClearServiceBus()` as testing workaround
- [ADR 0034: Instance-Based Mediator for Testability](../../docs/adr/0034-instance-based-mediator-for-testability.md) — moved mediator to instance-based, deprecated `ClearServiceBus()`

### Files Affected
- `src/Paramore.Brighter/CommandProcessor.cs` — method definition (lines ~1493-1503) and static field declarations (lines ~134-138)
- ~201 test files across multiple test projects calling `ClearServiceBus()` or `ClearEventBus()`

### Test Usage Patterns Observed
- **Dispose pattern** (~90%): Called in `IDisposable.Dispose()` for teardown
- **Constructor pattern** (~5%): Called before creating `CommandProcessor` in test setup
- **Base fixture pattern** (~3%): Called in shared base test fixture constructors
- **Inline pattern** (~2%): Called within individual test methods
