# Review: design — 0034-failed-delivery-context

**Date**: 2026-06-11
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. Consider addressing lower-scored items.

> Round 7 — re-review confirming the five round-6 findings were folded into ADR 0063 and that the edits introduced no new contradictions. All five resolved; the rewritten InMemory pump section is internally coherent; every load-bearing source reference re-verified against HEAD.

## Round-6 resolution check
- **R6 #1 (was 64) — InMemory two-stage drain**: **RESOLVED**. The "Lifecycle / draining" bullet now pins both ordering rules: (i) "Count before spawn" — `Interlocked.Increment` in the worker immediately before each `Task.Run`, decrement in the raise's `finally`, with the explicit rationale that an increment inside the spawned task would let `await worker` return with an uncounted raise (lost confirmation); (ii) "Re-check the already-zero case" — disposer re-evaluates `count == 0` after `writer.Complete()` + `await worker` rather than relying on a transition that never re-fires. The preferred `Task.WhenAll`-over-a-bag alternative is offered. Self-consistent within the bullet.
- **R6 #2 (was 63) — Kafka closure "already closes over"**: **RESOLVED**. Now states the current lambda references only `report`, so it does not yet capture `message`; the mechanism is to rewrite the closure body to close over `message`. Matches `KafkaMessageProducer.cs:260, 333`. No surviving "already closes over" assertion (grep clean).
- **R6 #3 (was 57) — Lazy worker single-start guard**: **RESOLVED**. Names `Interlocked.CompareExchange` (or `LazyInitializer.EnsureInitialized`); explains `SingleReader=true` is an unenforced contract and the guard is load-bearing. Consistent with single-worker/FIFO claims.
- **R6 #4 (was 46) — Task.Run AC-10 concurrency**: **RESOLVED**. Softened to best-effort ("`Task.Run` only *queues*"); requires the AC-10 test to force overlap with a synchronization gate (`Barrier`/`ManualResetEventSlim`).
- **R6 #5 (was 28) — capture-site prose**: **RESOLVED**. Data-path and capture-ordering paragraphs now agree on `SendWithDelay`/`SendWithDelayAsync`, with `Send`/`SendAsync` noted as thin delegators (`:200, :215`).

## Findings

### 1. Negative-consequence bullet still describes the drain as TCS-only, contradicting the body's now-preferred bag/`Task.WhenAll` mechanism (Score: 48)

The body was rewritten so the `TaskCompletionSource` is framed as the hazard-prone path and "drop the TCS entirely … `await Task.WhenAll(bag)`" is marked **(preferred)**. But the Negative consequence still summarizes the drain as "await the outstanding raise tasks **via an `Interlocked` count + `TaskCompletionSource`**" — presenting the TCS as *the* mechanism, the exact thing R6 #1 flagged. An implementer reading only Consequences would pick the discouraged design.

**Evidence**: ADR Negative consequence ("InMemory scope increase"): "a **two-stage drain** on dispose (await the worker, then await the outstanding raise tasks via an `Interlocked` count + `TaskCompletionSource`)" vs. Lifecycle bullet: "**Simpler equivalent (preferred):** drop the TCS entirely — collect the raise `Task` handles into a thread-safe bag and `await Task.WhenAll(bag)`".

**Recommendation**: Reword the consequence to "(await the worker, then await the outstanding raise tasks — e.g. via `Task.WhenAll` over the raise handles, or an `Interlocked` count + TCS)" so it does not re-assert the TCS as canonical.

---

### 2. InMemory property/batch line references have drifted (Score: 30)

The ADR cites InMemory property-injection lines `:62, :76, :79` and the batch path at `:114`, and `Scheduler (:79)`. Verified actual: `Publication` is `:62` (correct), but `Span` is `:69` (not `:76`), `Scheduler` is `:72` (not `:79`), and `SendAsync(IAmAMessageBatch …)` is `:112` (not `:114`). The cited symbols exist and the prose is correct; only the numbers are stale.

**Evidence**: `InMemoryMessageProducer.cs:62` Publication, `:69` Span, `:72` Scheduler, `:112` batch `SendAsync`. (Raise sites `:100/:123/:145`, event `Action<bool, Id>` `:77`, dispose `:82/:87` all verified correct.)

**Recommendation**: Update `:76→:69`, `:79→:72` (twice — property list and Scheduler ref), `:114→:112`.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 0 |
| 0-49 (Low) | 2 |

**Total findings**: 2
**Findings at or above threshold (60)**: 0
