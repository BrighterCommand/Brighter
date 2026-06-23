# Review: requirements — pipeline-cache-type-key

**Date**: 2026-06-18
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. (Round 3 of review; prior rounds' above-threshold findings all resolved.)

All four sub-threshold findings below were also addressed post-pass (cheap wording tightening to de-risk the tasks/test phase).

## Findings

### 1. AC-6a "timings" overloaded vs AC-8's usage (Score: 52) — ADDRESSED

AC-6a's "timings" meant the `HandlerTiming` Before/After enum, while AC-8 used "timings" for an elapsed concept scoped to handlers. Adjacent ACs overloading the word invited conflation.

**Resolution**: AC-6a now reads "the same decorator types, order, steps, and timing — the `HandlerTiming` Before/After value".

### 2. AC-6b/6c lacked the explicit C-5 hedge carried by AC-6a (Score: 50) — ADDRESSED

"Exactly one entry keyed for that type" is a cache-key-cardinality observable (the crux of the fix's testability and what the existing white-box test asserts), but AC-6b did not carry AC-6a's "internal value representation not constrained (per C-5)" disclaimer.

**Resolution**: AC-6b now carries the parallel C-5 hedge; AC-6c already disclaimed reference-equality.

### 3. AC-10 single-type concurrency clause half-vacuous (Score: 48) — ADDRESSED

For the single-type case, "no thread observes another type's metadata" is vacuous (no other type), and the meaningful convergence guarantee was stated only for the colliding/async case.

**Resolution**: AC-10 now adds "For a single type built concurrently, every thread observes that type's own metadata and the cache converges to exactly one entry for it."

### 4. Reflection-counting test double named with no seam in current code (Score: 32) — ADDRESSED

AC-6a's optional "MAY verify with a reflection-counting test double" suggested a mechanism with no injection seam (`FindHandlerMethod().GetOtherHandlersInPipeline()` is a direct call), which could mislead the tasks phase.

**Resolution**: Dropped the test-double suggestion; AC-6a now states "served from cache" is verified observably via cache entry count + output equivalence.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 2 |
| 0-49 (Low) | 2 |

**Total findings**: 4
**Findings at or above threshold (60)**: 0

## Coverage confirmation (verified by reviewer against source)

- Every FR maps to >=1 AC: FR-1→AC-1/2, FR-2→AC-3, FR-3→AC-4, FR-4→AC-5, FR-5→AC-6a/6b/6c, FR-6→AC-7, FR-7→AC-8/9; NFR-2→AC-10, NFR-3→AC-11. No dangling "AC-6" reference after the split.
- All code-behaviour claims accurate: simple-name keying at the read/write sites; separate static caches per builder; sync `TryGetValue`/`TryAdd` vs async `GetOrAdd`; post-cache `UseInbox` construction from `GetType()`; mis-typed async logger generic (OOS-2).
