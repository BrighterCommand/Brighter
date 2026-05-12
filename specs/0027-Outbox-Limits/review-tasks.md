# Review: tasks — 0027-Outbox-Limits

**Date**: 2026-05-06
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. Consider addressing lower-scored items.

## Findings

### 1. Phase 3 dependency on TIDY-1c is overstated (Score: 45)

The dependency graph states Phase 3 depends on TIDY-1c ("needs the abstract structure to accept configuration"). However, the configuration task sets existing public properties on `InMemoryOutbox` — it doesn't require the class to be abstract. The dependency is an ordering preference, not a technical requirement.

**Evidence**: Task dependency: "Phase 3 depends only on TIDY-1c (needs the abstract structure to accept configuration)." But configuration plumbing uses settable properties, not abstract methods.

**Recommendation**: Clarify as an ordering preference rather than a technical dependency. No blocking action needed.

---

### 2. TIDY-1c could technically run before TIDY-1b (Score: 40)

TIDY-1c modifies `EnforceCapacityLimit()` in the base class, which is unchanged by TIDY-1a or TIDY-1b. The dependency chain is stricter than necessary but safe.

**Evidence**: `EnforceCapacityLimit()` is a base class method unaffected by the abstract refactoring.

**Recommendation**: No action required — conservative ordering is fine.

---

### 3. Outbox compaction task correctly accounts for updating existing test (Score: 35)

The task explicitly notes "Update existing `When_controlling_cache_size.cs` to mark messages as dispatched." Positive finding.

**Recommendation**: None.

---

### 4. ADR pseudocode uses `_timeProvider` vs actual `timeProvider` (Score: 20)

Cosmetic ADR inaccuracy. Implementer will naturally resolve.

**Recommendation**: None needed for tasks.md.

---

### 5. Both test methods in `When_controlling_cache_size.cs` will need updating (Score: 30)

The file contains two `[Fact]` methods that both exercise compaction. The task refers to the file generically, which implicitly covers both.

**Evidence**: File has `When_max_size_is_exceeded_shrink` and `When_shrinking_evict_oldest_messages_first`.

**Recommendation**: Trust implementer to handle the whole file.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 0 |
| 0-49 (Low) | 5 |

**Total findings**: 5
**Findings at or above threshold (60)**: 0
