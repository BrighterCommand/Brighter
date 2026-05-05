# Review: tasks — 0027-span-based-performance

**Date**: 2026-05-01
**Threshold**: 60
**Verdict**: NEEDS WORK → FIXED

7 findings at or above threshold 60. All addressed in the same session.

## Findings (all fixed)

### 1. ADR RMQ header optimization not covered by any task (Score: 75) — FIXED

Added span-based `GetString(ReadOnlySpan<byte>)` header optimization to the RMQ Async task. Conditional compilation for `net8.0`+ included.

---

### 2. ReadOnlyMemoryStream TIDY task has no test (Score: 72) — FIXED

Converted from TIDY to TEST+IMPLEMENT with unit test covering Read, Length, Position, Seek, CanRead/CanWrite, and edge cases.

---

### 3. RMQ Sync RmqMessageCreator not covered (Score: 70) — FIXED

Added separate TEST+IMPLEMENT task for the Sync `RmqMessageCreator` in Phase 5.

---

### 4. Test directory `MessageBody` does not exist (Score: 65) — FIXED

Added "(new directory — create it)" annotation to the test location.

---

### 5. Benchmark task framed as TEST+IMPLEMENT but isn't behavioral (Score: 65) — FIXED

Changed to SETUP task with separate console project. Removed `/test-first` framing. Verification is "benchmark runs and produces output."

---

### 6. FR-8 mismatch between requirements and ADR (Score: 62) — FIXED

Updated requirements.md FR-8 to align with ADR descoping of `ReadOnlySpan<char>` overloads.

---

### 7. Regression verification only at end (Score: 60) — FIXED

Added per-phase regression check notes. Phase 9 renamed to "Final Regression Verification" and expanded to include transport test suites.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 3 |
| 50-69 (Medium) | 4 |
| 0-49 (Low) | 2 |

**Total findings**: 9
**Findings at or above threshold (60)**: 7 (all fixed)
