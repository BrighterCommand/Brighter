# Review: tasks — 0027-replay-matching-outbox-events-when-inbox-has-already-seen

**Date**: 2026-04-17
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. Consider addressing lower-scored items.

## Findings

### 1. Task 10/11 constructor injection may conflict with existing DI resolution pattern (Score: 55)

Tasks 10/11 add optional `IAmACausationTrackingOutbox?` constructor parameter. The ADR explains this works with `ActivatorUtilities` optional parameters, and Task 17 handles DI registration. Task 10's tests use manually-constructed handlers, so Task 17 is not a dependency for unit testing.

**Evidence**: Task 10 deps: "6, 7, 8"; Task 17 deps: "5, 6, 7". No circular dependency.

**Recommendation**: No change needed. Task ordering is correct for unit-test-first approach.

---

### 2. Task 14 test verifies 7 scenarios in one test file (Score: 50)

Task 14 lists 7 verification scenarios for one validation rule. These are different inputs to the same specification.

**Evidence**: Task 14 test verification list has 7 bullet points.

**Recommendation**: Implementing agent should use `[Theory]`/`[InlineData]` or separate `[Fact]` methods. No task change needed.

---

### 3. Task 8/9 CausationId generation timing relative to inbox Add (Score: 45)

CausationId is set before `base.Handle()`, then inbox `Add()` is called after. Outbox `Add` (in `DepositPost`) runs during `base.Handle()`. Both correctly read CausationId from the Bag.

**Evidence**: `UseInboxHandler.Handle()` lines 102-106.

**Recommendation**: No change needed. The ordering is correct.

---

### 4. FR4 has no dedicated task (Score: 45)

FR4 (Sweeper re-dispatch) is existing functionality. Tasks 10/11 verify messages are marked for re-dispatch by checking `TimeFlushed` is cleared.

**Evidence**: FR Coverage table correctly notes existing behavior.

**Recommendation**: No change needed.

---

### 5. Task 16 depends on Task 15 unnecessarily (Score: 40)

Both are independent telemetry additions to different code branches. Serial ordering is conservative but not harmful.

**Evidence**: Task summary: Task 16 depends on "1, 15".

**Recommendation**: Could remove Task 15 dependency to allow parallelism, but not required.

---

### 6. UseInboxHandlerAsync has duplicate InitializeFromAttributeParams call (Score: 35)

`UseInboxHandlerAsync.cs` calls `base.InitializeFromAttributeParams()` twice. Task 1 should fix this as part of the tidy-first pass.

**Evidence**: `UseInboxHandlerAsync.cs` lines 69-71.

**Recommendation**: Agent implementing Task 1 should notice and fix. Minor omission.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 2 |
| 0-49 (Low) | 4 |

**Total findings**: 6
**Findings at or above threshold (60)**: 0
