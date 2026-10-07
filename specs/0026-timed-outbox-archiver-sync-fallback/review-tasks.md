# Review: tasks — 0026-timed-outbox-archiver-sync-fallback

**Date**: 2026-04-15
**Threshold**: 60
**Verdict**: NEEDS WORK

3 findings at or above threshold 60. Address these before approving.

## Findings

### 1. Implementation file path for TimedOutboxArchiver not specified (Score: 72)

Task 2 describes implementing a three-way branch in `TimedOutboxArchiver.Archive()`, adding a `NoOutboxConfigured` log message, and removing a duplicate `OutboxSweeperSleeping` call. However, the task only specifies the file path for the TIDY step (`src/Paramore.Brighter/OutboxArchiver.cs`). The actual implementation changes are in a different project/file: `src/Paramore.Brighter.Outbox.Hosting/TimedOutboxArchiver.cs`.

**Evidence**: Task 2 implementation bullet points reference `TimedOutboxArchiver.Archive()` and `Log` partial class changes but never state which file to edit. Task 1 explicitly lists `File: src/Paramore.Brighter/OutboxArchiver.cs` but no equivalent line appears for Tasks 2-4.

**Recommendation**: Add `File: src/Paramore.Brighter.Outbox.Hosting/TimedOutboxArchiver.cs` to the implementation section of Task 2.

---

### 2. FR1 (async path unchanged) has no dedicated test task (Score: 65)

FR1 states "When an async outbox is available, `TimedOutboxArchiver` calls `ArchiveAsync` (current behavior, unchanged)." Task 3 partially covers this with AC3 (prefers async when both are available), but there is no test for the scenario where ONLY an async outbox is registered (no sync). The three-way branch introduces a regression risk for async-only outboxes.

**Evidence**: FR1 says "current behavior, unchanged." Task 3 tests "both sync and async" but not "async only via TimedOutboxArchiver."

**Recommendation**: Either add a test task for async-only outbox through `TimedOutboxArchiver`, or explicitly document why existing coverage is sufficient.

---

### 3. NoOutboxConfigured log message location ambiguous (Score: 62)

Task 2 says "Add `NoOutboxConfigured` log message to the `Log` partial class" but both `TimedOutboxArchiver` and `OutboxArchiver` have their own `Log` partial class. The call site is in `TimedOutboxArchiver` but this is not stated explicitly.

**Evidence**: Task 2 implementation says `Log.NoOutboxConfigured(s_logger)` but `s_logger` exists in both classes.

**Recommendation**: Specify: "Add `NoOutboxConfigured` log message to the `Log` partial class in `TimedOutboxArchiver.cs`."

---

### 4. OutboxArchiver is generic but test setup doesn't mention type parameters (Score: 55)

`OutboxArchiver<TMessage, TTransaction>` requires two generic type parameters. The test descriptions don't mention what concrete types to use.

**Evidence**: `public partial class OutboxArchiver<TMessage, TTransaction> where TMessage : Message` at line 40 of `OutboxArchiver.cs`.

**Recommendation**: Specify the concrete generic types to use in tests based on existing archiving test patterns.

---

### 5. ADR decision to remove duplicate OutboxSweeperSleeping is bundled into behavioral task (Score: 45)

The duplicate `OutboxSweeperSleeping` log removal is a structural/tidy change bundled into behavioral Task 2, contrary to tidy-first philosophy.

**Evidence**: Task 2 implementation says "Remove duplicate `Log.OutboxSweeperSleeping` call (keep only the one in `finally`)."

**Recommendation**: Move to Step 1 as a separate TIDY task.

---

### 6. Task numbering inconsistency in dependency chain (Score: 40)

The dependency chain references "Task 1 → Task 2 → Task 3 → Task 4" but there are only 3 bullet-pointed tasks in Step 2.

**Evidence**: `Task 1 (TIDY) → Task 2 (sync fallback) → Task 3 (async preference) → Task 4 (no outbox warning)` but document has Step 1 (1 task) + Step 2 (3 tasks).

**Recommendation**: Number tasks explicitly or fix the dependency chain.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 1 |
| 50-69 (Medium) | 3 |
| 0-49 (Low) | 2 |

**Total findings**: 6
**Findings at or above threshold (60)**: 3
