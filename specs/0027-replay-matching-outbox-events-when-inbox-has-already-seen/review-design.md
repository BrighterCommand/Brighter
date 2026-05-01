# Review: design — 0027-replay-matching-outbox-events-when-inbox-has-already-seen

**Date**: 2026-04-17
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. Consider addressing lower-scored items.

## Findings

### 1. InboxItem shown with primary constructor syntax but actual class uses traditional constructor (Score: 45)

The ADR's code example shows `InboxItem` using C# primary constructor syntax, but the actual `InboxItem` class at `src/Paramore.Brighter/InMemoryInbox.cs` uses a traditional class with a regular constructor and explicit property assignments. An implementer following the ADR example might incorrectly refactor the class to use a primary constructor.

**Evidence**: ADR line 201 vs actual `InMemoryInbox.cs` lines 39-54.

**Recommendation**: Update the ADR example to show the `CausationId` property addition against the actual class structure, or note that the example is illustrative.

---

### 2. ADR does not explicitly reference FR-N identifiers from requirements (Score: 40)

The requirements document enumerates functional requirements as numbered items (1-6) and acceptance criteria (1-8). The ADR references the requirements document by path but never cites specific requirement numbers. While coverage is complete on inspection, explicit cross-referencing would improve traceability.

**Evidence**: The ADR's "Parent Requirement" link is the only reference to the requirements document; no individual FR-N or AC-N citations appear.

**Recommendation**: Consider adding a brief traceability note mapping key design decisions to specific requirements.

---

### 3. `DescribePipelines()` also needs `InboxConfiguration` for consistency (Score: 35)

The ADR correctly identifies that `ValidatePipelines()` creates `PipelineBuilder` without `InboxConfiguration` and proposes fixing this. However, `DescribePipelines()` also creates `PipelineBuilder` without `InboxConfiguration`. The ADR only mentions the `ValidatePipelines()` path. This is minor since `DescribePipelines()` is diagnostic only.

**Evidence**: `BrighterPipelineValidationExtensions.cs` lines 59 and 89 both create `PipelineBuilder` without `InboxConfiguration`.

**Recommendation**: Note that `DescribePipelines()` should also receive `InboxConfiguration` for consistency.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 0 |
| 0-49 (Low) | 3 |

**Total findings**: 3
**Findings at or above threshold (60)**: 0
