# Review: tasks — pipeline-cache-type-key

**Date**: 2026-06-18
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. (Round 2; round-1's 2 above-threshold findings resolved.)

## Findings

### 1. AC-6a per-closed-generic static scoping not called out (Score: 42) — ADDRESSED

The handler mementos are static fields on the *closed generic* `PipelineBuilder<TRequest>`, so the `Count == 1` isolation holds only if the single-handler fact uses a request type no sibling fact also builds. The existing "build nothing else" constraint largely covered it; the per-`TRequest` scoping is now called out explicitly.

**Evidence**: `PipelineBuilder.cs:49-50` — mementos declared inside `PipelineBuilder<TRequest>`; `ClearPipelineCache()` (`:253`) clears the same closed-generic statics.

**Resolution**: AC-6a now notes the mementos are static per closed generic `PipelineBuilder<TRequest>` and the fact must use a request type unique to it.

### 2. Task 4 async `Count == 1` under the C-6 race (Score: 31) — NO ACTION (confirmation only)

`GetOrAdd` retains exactly one entry per key even if the factory runs twice, so `Count == 1` is correct under single-threaded build. Task 4 correctly scopes this to single-threaded/post-warmup and disclaims reference-equality per C-6. No defect.

**Evidence**: `TransformPipelineBuilderAsync.cs:202/210` `GetOrAdd` converges to one entry per key; Task 4 AC-6c wording matches C-6.

**Resolution**: None required.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 0 |
| 0-49 (Low) | 2 |

**Total findings**: 2
**Findings at or above threshold (60)**: 0

## Round-1 resolution + grounding notes (verified by reviewer)

- **AC-7 reword (round-1 #1, High)** verified sound against source: cache `TryAdd` (PipelineBuilder.cs 284/301/327/342) runs *before* `AddGlobalInboxAttributes`/`...Async` (287/330); `PushOntoAttributeList` (`:438`) builds a new list and reassigns `ref preAttributes` — it never mutates the cached `IOrderedEnumerable`. So "cached value contains no `UseInbox`" holds.
- AC-6a isolation constraint present + mirrored to AC-6b/AC-6c; Task 2 line citations corrected (signature 58, cast 66; assertions 33/55 exact); Task 7 hard-states no public-API harness exists (edits are private-only, AC-9 structurally guaranteed).
- All production-code citations verified exact across the three builders. All cited test files / tracer patterns exist. Coverage matrix is honest; no uncovered FR/NFR/AC/ADR decision point; no scope creep.
