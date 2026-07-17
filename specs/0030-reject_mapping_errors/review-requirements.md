# Review: requirements — 0030-reject_mapping_errors (RE-REVIEW after scope reduction)

**Date**: 2026-06-01
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. Consider addressing lower-scored items.

This is the **re-review after the post-approval scope reduction** (mapping-only; catch-all `Exception` deliberately unchanged per the handler-failure policy). This phase was already approved; findings below are informational. None warrant re-opening the phase. All load-bearing source references independently re-verified accurate against the current code.

## Findings (re-review)

### 1. FR-8 cross-referenced "see AC-13" but AC-13 does not exist (Score: 55)

FR-8's example text ended with "(Regression guard — verifies this change did not accidentally alter the catch-all; see AC-13.)" There is no AC-13 in the document. The catch-all regression guards are AC-2 (async) and AC-4 (sync). This was a stale cross-reference — a leftover from the scope-reduction renumbering when AC-2/AC-4 were repurposed.

**Evidence**: FR-8 example referenced "AC-13"; ACs run AC-1 through AC-12.

**Recommendation**: Change "see AC-13" to "see AC-2 (async) / AC-4 (sync)."

**Status: FIXED** (2026-06-01) — FR-8 example now reads "see AC-2 (async) / AC-4 (sync)."

---

### 2. Latent `FailedToMapMessage` signature asymmetry between Proactor and Reactor (Score: 35)

A latent pre-existing asymmetry the doc does not mention: Proactor's `Log.FailedToMapMessage` takes a strongly-typed `MessageMappingException` parameter (Proactor.cs:606) while Reactor's takes a base `Exception` (Reactor.cs:595). It does not affect the requirement (outside the changed block body; the description string is identical) and A-1 already broadly disclaims out-of-block Proactor/Reactor differences, so it is correctly out of scope — but a strict reading of NFR-1 "behaviourally equivalent" could momentarily confuse. Low impact.

**Evidence**: Proactor.cs:606 vs Reactor.cs:595.

**Recommendation**: Optional — A-1 already covers this; could list it as another example alongside the `ConfigurationException`-increment one. Left as-is.

---

### 3. FR-8 / AC-2 / AC-4 do not explicitly state the catch-all does NOT `continue` (Score: 30)

FR-8 and AC-2/AC-4 assert the catch-all "falls through to the unconditional acknowledge." The observable assertion (one acknowledge, zero rejects) is testable and correct against source. Minor: it never states the *mechanism* (absence of `continue`) the way AC-7 explicitly does for the mapping path. The acknowledge-count assertion fully pins the behavior, so this is stylistic, not a coverage gap.

**Evidence**: AC-7 asserts `continue` for the mapping path; AC-2/AC-4 assert acknowledge-count for the catch-all. Source confirms catch-all has no `continue`.

**Recommendation**: Optional — sufficient as-is. Left as-is.

---

### 4. Coverage of LIVE FRs by ACs — complete, no gap (Score: 0, informational)

Every live FR maps to ≥1 AC: FR-1 → AC-1/7/8/10/11/12; FR-3 → AC-3/7/8/10/11/12; FR-5 → AC-5/6; FR-6 → AC-1/3; FR-7 → AC-8/9; FR-8 → AC-2/4. Tombstoned FR-2/FR-4 correctly have no live ACs. No coverage gap.

---

### 5. Handler-failure-policy rationale stated unambiguously (Score: 0, informational)

The "do not re-add the catch-all rejection" guardrail is stated in five mutually reinforcing places (Problem Statement, Proposed Solution, FR-8, C-2, Out of Scope). The contrast between framework-raised `MessageMappingException` (→ reject) and application dispatch exceptions (→ ack) is drawn crisply. A developer is very unlikely to re-add the rejection. Strong.

## Summary (re-review)

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 1 |
| 0-49 (Low) | 4 |

**Total findings**: 5
**Findings at or above threshold (60)**: 0

The only substantive issue was the stale "see AC-13" reference in FR-8 (Score 55, below threshold), now FIXED. All source references verified: mapping/catch-all catch blocks and thread-id expressions, fall-through acknowledges, the inner `AggregateException` handler with no `MessageMappingException` arm (A-2), the A-1 `ConfigurationException` increment asymmetry, SQS V4 `RejectAsync` routing, the `RejectionReason`/`MessageRejectionReason` shapes, and `UnacceptableMessageLimit` default-0 semantics.

---
---

# PRIOR REVIEW HISTORY (pre–scope-reduction; superseded by the re-review above)

# Review: requirements — 0030-reject_mapping_errors

**Date**: 2026-06-01
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. Consider addressing lower-scored items.

> Review history: Round 1 found 3 blockers (68/64/60) — fixed. Round 2 found 1 blocker (62, an AC-8 over-correction) — fixed. This Round 3 is a fresh, full adversarial pass on the current document and finds no blockers. All load-bearing codebase claims independently re-verified accurate.

## Findings

### 1. A-2's "caught by the dedicated `MessageMappingException` block because it precedes the catch-all" rests on fragile reasoning (Score: 52)

A-2 asserts a `MessageMappingException` thrown anywhere in the per-message `try` is caught by the dedicated `catch (MessageMappingException)` "because it precedes the catch-all." The control flow is not a flat sequence of sibling catches: there is an **inner** `try` whose **first** catch is `catch (AggregateException)` (Proactor.cs:237-330), which classifies `InnerExceptions` against `ConfigurationException`/`DeferMessageAction`/`DontAckAction`/`RejectMessageAction`/`InvalidMessageAction` — but **not** `MessageMappingException`. A `MessageMappingException` wrapped in an `AggregateException` would hit the unmatched fall-through (`Log.FailedToDispatchMessage` ~line 282), set no flags, and exit to the unconditional `await Acknowledge(message)` (line 391) — the exact silent-delete the feature exists to remove — never reaching the dedicated catch at 374.

In practice it works because `TranslateMessage` (line 510) is directly `await`ed (line 231) and throws a **bare** `MessageMappingException` (lines 517/541/550), which `await` surfaces unwrapped. So A-2's *conclusion* is correct, but its *justification* ("because it precedes the catch-all") is wrong — the real basis is "TranslateMessage is directly awaited and throws unwrapped." Stating it as a structural ordering guarantee could mislead the design phase into over-trusting catch ordering.

**Evidence**: Proactor.cs:237-283 (AggregateException loop has no `MessageMappingException` arm), :329 (fall-through SetStatus), :391 (`await Acknowledge`), vs. dedicated catch at :374. A-2 text.

**Recommendation**: Reword A-2 to state the real basis (mapping exception propagates unwrapped from the directly-awaited `TranslateMessage`, so it reaches the dedicated catch), and note that wrapping it in an `AggregateException` would misroute it — so the design must not introduce such wrapping. Optionally add an AC that exercises the mapping path through the *real* `TranslateMessage` (not a hand-thrown bare exception), so a future refactor that wraps the exception is caught by tests.

---

### 2. C-5 / AC-10 tension between "must be the same description string" and "exact wording not asserted" (Score: 40)

C-5 mandates the precise interpolated strings and requires the rejection `Description` to be "the same description string used for the existing `processSpan?.SetStatus(...)` call ... extracted to a shared local." AC-10 then asserts only "non-empty and contains the substring `<id>` ... and is the same description string passed to `SetStatus`," while C-5's rationale states "the exact thread-id token value is not asserted." These are reconcilable (C-5 = implementation, AC-10 = test strategy), but a developer could read C-5's mandated literal as requiring a literal-string assertion while AC-10 forbids asserting the literal — a genuine two-developer divergence, bounded by C-5's own rationale paragraph.

**Evidence**: C-5 "MUST be the same description string ... extracted to a shared local" vs. C-5 "The exact thread-id token value is not asserted" and AC-10.

**Recommendation**: Add one sentence to AC-10 fixing the assertion strategy: "The test captures both the `Description` and the string passed to `SetStatus` on the same path and asserts they are equal; it does NOT assert any literal containing the thread id."

---

### 3. C-6 throwing-reject path is out-of-scope but no AC pins the no-throw assumption (Score: 32)

C-6 and the Out-of-Scope bullet cleanly exclude guarding `RejectMessage` against a throwing consumer, consistent with FR-1..FR-4 (which mirror the unguarded existing reject blocks) and NFR-2. No contradiction. Minor gap: every FR/AC assumes `RejectMessage` returns normally and there is no negative AC documenting the accepted propagation behaviour. Defensible to omit given the explicit out-of-scope declaration.

**Evidence**: C-6; no AC references the throwing-reject path.

**Recommendation**: Optional — add a one-line note that a throwing reject propagates out of the catch with no new guard, or leave as documented out-of-scope.

---

### 4. AC-5 termination timing does not state where the limit is evaluated (Score: 28)

AC-5 and FR-5's example state that with `UnacceptableMessageLimit = 3`, after the third failure "on the next loop iteration the pump stops." `UnacceptableMessageLimitReached()` is checked at the **top** of the loop (Proactor.cs:142, Reactor.cs:101) and stops when count `>=` limit (line 557). After 3 increments, count == 3 == limit, so the check at the start of the 4th iteration trips — the AC's claim is correct, but it doesn't state *where* the check occurs, leaving a tester to guess whether a 4th message is received before termination.

**Evidence**: Proactor.cs:142 / 554-562; Reactor.cs:101 / 546.

**Recommendation**: Add "(the limit is evaluated at the top of the loop, before the next receive, so the 4th message is not dispatched)" to AC-5.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 2 |
| 0-49 (Low) | 2 |

**Total findings**: 4
**Findings at or above threshold (60)**: 0

## Post-review fixes (applied 2026-06-01, after Round 3 — sub-threshold improvements)

The PASS verdict above stands; the following non-blocking findings were nonetheless applied at the user's request:

- **Finding 1 (52) — FIXED**: A-2 reworded to state the real basis (mapping exception propagates *unwrapped* from the directly-awaited `TranslateMessage`, reaching the dedicated catch — NOT catch-block ordering), to warn that wrapping it in an `AggregateException` would misroute it to the catch-all silent-delete, and to forbid introducing such wrapping. New **AC-12** added: exercises the mapping path through the real `TranslateMessage` (not a hand-thrown bare exception) so a future wrapping refactor is caught by tests.
- **Finding 2 (40) — FIXED**: AC-10 now fixes the assertion strategy — capture both `Description` and the `SetStatus` string and assert equality; do NOT assert any literal containing the thread id.
- **Finding 4 (28) — FIXED**: AC-5 now notes the limit is evaluated at the top of the loop, before the next receive, so the 4th message is not dispatched.
- **Finding 3 (32)** — left as documented out-of-scope (C-6).

---

All load-bearing codebase claims verified accurate against source: both C-5 description strings including the differing thread-id expressions (`Thread.CurrentThread.ManagedThreadId` mapping at Proactor.cs:378/Reactor.cs:343; `Environment.CurrentManagedThreadId` dispatch at Proactor.cs:384/Reactor.cs:349); the fall-through `await Acknowledge(message)` (Proactor.cs:391) and `AcknowledgeMessage(message)` (Reactor.cs:356); `IncrementUnacceptableMessageCount` (MessagePump.cs:176); `UnacceptableMessageLimit <= 0` no-limit semantics (Proactor.cs:556, Reactor.cs:548); `RejectionReason` members `None`/`Unacceptable`/`DeliveryError` (MessageRejectionReason.cs:30-44); SQS V4 routing (Unacceptable→IMQ→DLQ fallback, DeliveryError/None→DLQ, no-channel→AcknowledgeAsync/delete at SqsMessageConsumer.cs:161-169, 534-555); the A-1 Reactor-vs-Proactor ConfigurationException increment asymmetry (Reactor.cs:300 vs Proactor.cs:331-339); and the FR-6 separate receive-span vs process-span finallys. No factually broken references found.

---

## Post-approval scope reduction (2026-06-01, user-directed)

**After** requirements were APPROVED (`.requirements-approved` present) and ADR 0061 was first drafted, the user reduced scope: **drop the catch-all `Exception` → Reject requirement; change only the `MessageMappingException` path.**

**Rationale (user):** Brighter's [handler-failure policy](https://brightercommand.gitbook.io/paramore-brighter-documentation/using-an-external-bus/handlerfailure) is that a general application exception is logged and the message acknowledged; DLQ/defer/reject is driven only by explicit means (the handler throwing an action exception, or an attribute/policy) — the pump must not reinterpret an arbitrary unhandled exception as a rejection. A `MessageMappingException` is different: it is raised by the framework's own translate step before any handler runs, so rejecting it as `Unacceptable` (→ IMQ) is correct and was simply omitted.

**Changes applied to `requirements.md`:**
- Problem Statement / Proposed Solution reframed to mapping-only; catch-all exclusion + policy link added.
- **FR-2 and FR-4 (catch-all → `DeliveryError`) WITHDRAWN** (IDs retained as tombstones for traceability).
- **FR-8 ADDED**: catch-all `Exception` body left unchanged (regression-guarded).
- FR-5/FR-6/FR-7, NFR-1, C-1/C-2/C-4/C-5/C-6, A-1/A-2 reworded from "four paths"/"two blocks" to the single mapping path.
- **AC-2 and AC-4 REPURPOSED** from "catch-all rejects `DeliveryError`" into regression guards asserting the catch-all still *acknowledges* (covers FR-8). AC-7/AC-8/AC-9/AC-10/AC-11/AC-12 reworded to mapping-only.
- Out of Scope: explicit entry recording the catch-all routing was considered and dropped, with the policy rationale.

**`docs/adr/0061-reject_mapping_errors.md`** rewritten to match: title now "Route Mapping Failures Through RejectMessage"; new Context subsection "Why the catch-all `Exception` path is deliberately out of scope"; Decision/RDD/Implementation/Consequences/Risks updated; Alternative 1 is now "Also route the catch-all through Reject" with the policy rejection rationale.

**Approval status:** This is a material change to an approved baseline. The reduced-scope requirements should be re-confirmed (re-run `/spec:review requirements` or re-approve) before proceeding to `/spec:review design`.
