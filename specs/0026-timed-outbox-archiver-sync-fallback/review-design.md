# Review: design — 0026-timed-outbox-archiver-sync-fallback

**Date**: 2026-04-15
**Threshold**: 60
**Verdict**: NEEDS WORK

3 findings at or above threshold 60. Address these before approving.

## Findings

### 1. Naming inconsistency: ADR claims to follow `HasOutbox()` pattern but introduces `HasSyncOutbox()` (Score: 72)

The ADR states it follows "the same pattern used by `OutboxProducerMediator`" and references "the established `HasAsyncOutbox()` / `HasOutbox()` pattern." However, the proposed method is named `HasSyncOutbox()`, not `HasOutbox()`. This contradicts the stated rationale (consistency with existing convention).

**Evidence**: ADR proposes `HasSyncOutbox()`. `OutboxProducerMediator` at line 595 uses `public bool HasOutbox()`.

**Recommendation**: Either rename to `HasOutbox()` to truly follow the existing pattern, or update the rationale to explain why a different name was deliberately chosen.

---

### 2. FR3 not explicitly addressed: "neither sync nor async" case (Score: 70)

FR3 requires: "When neither sync nor async outbox is available, the error is logged." The ADR's code sample branches only on `HasAsyncOutbox()` true/false — there is no explicit handling for the case where both are false. In practice the sync `Archive` call would throw and get caught, but the ADR never discusses this.

**Evidence**: Code sample shows `if (HasAsyncOutbox()) ... else ...` with no neither-available branch. FR3 is not mentioned in the ADR.

**Recommendation**: Add an explicit branch: if neither outbox is available, log a warning and return. Reference FR3 in the ADR.

---

### 3. Code sample has duplicate `OutboxSweeperSleeping` log call (Score: 60)

The proposed `Archive` method calls `Log.OutboxSweeperSleeping` in both the `try` block and the `finally` block, meaning the "sleeping" message is logged twice on the success path. This is a pre-existing bug in the current code that the ADR perpetuates.

**Evidence**: `Log.OutboxSweeperSleeping` appears at two points in the method — inside `try` after the inner `finally`, and in the outer `finally`.

**Recommendation**: Remove the duplicate call, or acknowledge it as a pre-existing issue and note it as out of scope.

---

### 4. ADR and spec numbering are independent (Score: 45)

The ADR file is `0056` but references `specs/0026-...`. Not wrong (independent numbering schemes) but could cause brief confusion.

**Evidence**: `docs/adr/0056-...` links to `specs/0026-...`.

**Recommendation**: No action required.

---

### 5. Sync-only outbox example not cited (Score: 35)

Alternative 2 argues "the framework explicitly supports sync-only outboxes through `IAmAnOutboxSync`," but all in-tree outbox implementations implement both interfaces. No concrete sync-only example is cited.

**Evidence**: `InMemoryOutbox`, `RelationDatabaseOutbox` etc. all implement both sync and async.

**Recommendation**: Acknowledge that in-tree outboxes implement both, but the interface design permits third-party/custom sync-only implementations.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 2 |
| 50-69 (Medium) | 1 |
| 0-49 (Low) | 2 |

**Total findings**: 5
**Findings at or above threshold (60)**: 3
