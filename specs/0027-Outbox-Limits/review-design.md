# Review: design — 0027-Outbox-Limits

**Date**: 2026-05-05
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. Consider addressing lower-scored items.

## Findings

### 1. Pseudocode uses `_timeProvider` but codebase uses `timeProvider` primary constructor parameter (Score: 25)

The `EnforceCapacityLimit` pseudocode references `_timeProvider.GetUtcNow()`, but the actual `InMemoryBox<T>` class uses a primary constructor parameter named `timeProvider` (no underscore prefix). Cosmetic inconsistency in illustrative pseudocode.

**Evidence**: ADR pseudocode: `var now = _timeProvider.GetUtcNow();` vs `InMemoryBox.cs` line 48: `public class InMemoryBox<T>(TimeProvider timeProvider)` and line 88: `var now = timeProvider.GetUtcNow();`

**Recommendation**: Update pseudocode to use `timeProvider` (no underscore) to match. Low priority.

---

### 2. EnforceCapacityLimit pseudocode reads EntryCount twice without caching (Score: 20)

The existing `EnforceCapacityLimit()` caches `EntryCount` into a local before using it, because the value may change concurrently. The ADR's pseudocode reads `EntryCount` directly in both the condition and the calculation.

**Evidence**: ADR pseudocode: `if (EntryCount >= EntryLimit) { ... int newSize = (int)(EntryCount * CompactionPercentage);` vs existing code (lines 133-139) which caches to a local.

**Recommendation**: Match the existing caching pattern in pseudocode for correctness signaling.

---

### 3. Architecture diagram has minor box-drawing alignment issue (Score: 10)

The ASCII diagram's `InMemoryOutbox` and `InMemoryInbox` boxes have slightly inconsistent widths. Purely cosmetic.

**Evidence**: Lines 127-139 of the ADR.

**Recommendation**: Optional — align box widths for visual consistency.

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
