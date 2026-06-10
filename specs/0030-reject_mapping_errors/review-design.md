# Review: design — 0030-reject_mapping_errors (ADR 0061)

**Date**: 2026-06-01
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. Consider addressing lower-scored items.

This review covers `docs/adr/0061-reject_mapping_errors.md` after the post-approval scope reduction (mapping-only; catch-all `Exception` deliberately unchanged per the handler-failure policy). The adversarial reviewer ran a full grounding pass against the real source and a scope-reduction consistency check.

## Findings

### 1. Mild overstatement that the Description reaches SQS V4 "rejection metadata" (Score: 35)

The ADR (Technology Choices) said: "`RejectMessage` persists `reason.Description` into the header bag at `Message.RejectionReasonHeaderName` ... and on SQS V4 the reason is additionally surfaced in rejection metadata." The grounding is mostly right but slightly conflated: the pump's `RejectMessage` writes the *Description-bearing* string to the `RejectionReason` header bag key (Proactor.cs:478 / Reactor.cs:411), whereas SQS V4 `RefreshMetadata` (SqsMessageConsumer.cs:501) writes only `reason.RejectionReason.ToString()` (the enum value, not the Description) into the separate `rejectionReason` bag key. The Description does still travel to the IMQ/DLQ — but via the unmodified `RejectionReason` header carried on the message, not via `RefreshMetadata`. The wording was loose but not false, and it mirrored C-5's own phrasing verbatim, so the design remained faithful to the requirement.

**Evidence**: SqsMessageConsumer.cs:501 `message.Header.Bag["rejectionReason"] = reason.RejectionReason.ToString();` (no Description); Proactor.cs:478 / Reactor.cs:411 write the Description-bearing string under `Message.RejectionReasonHeaderName` (= `"RejectionReason"`, Message.cs:51).

**Recommendation**: Tighten the sentence to distinguish the two bag keys.

**Status: FIXED** (2026-06-01) — the Technology Choices paragraph now distinguishes the Description-bearing `RejectionReason` header (rides on the message unmodified) from the enum-only `rejectionReason` metadata key written by `RefreshMetadata` on SQS V4.

---

### 2. "Verified" line citations occasionally off by ~2 lines (Score: 20)

A few cited line numbers are a couple of lines past the actual member: `RejectMessage` is at Proactor.cs:474 (ADR ✓) but `UnacceptableMessageLimitReached` is at Proactor.cs:554 while the ADR cites `556` (the `<= 0` body line) and Reactor.cs:546 vs ADR `548`. All within the "few lines" tolerance and the code shapes match precisely.

**Evidence**: Proactor.cs:554 `private bool UnacceptableMessageLimitReached()` with `:556 if (UnacceptableMessageLimit <= 0) return false;`; Reactor.cs:546 method decl.

**Recommendation**: None required; citations are within tolerance and the described shapes are correct. Left as-is.

---

## Verification notes (all grounded by the reviewer, no findings)

- Mapping block `catch (MessageMappingException ...)` Proactor.cs:374-379 / Reactor.cs:339-344 — log + increment + `processSpan?.SetStatus(Error, ...)`, no reject, no continue, falls through. ✓
- Catch-all `catch (Exception e)` Proactor.cs:380-385 / Reactor.cs:345-350 — `FailedToDispatchMessage2` + increment + SetStatus + fall-through. ✓
- Fall-through `await Acknowledge(message)` Proactor.cs:391; `AcknowledgeMessage(message)` Reactor.cs:356. ✓
- Thread-id expressions: mapping `Thread.CurrentThread.ManagedThreadId` (Proactor.cs:378 / Reactor.cs:343); catch-all `Environment.CurrentManagedThreadId` (Proactor.cs:384 / Reactor.cs:349). ✓
- Mapping description literal matches source verbatim. ✓
- `RejectMessage` signatures `Task<bool>` (Proactor.cs:474) / `bool` (Reactor.cs:407); both write `Message.RejectionReasonHeaderName` (Message.cs:51, `const "RejectionReason"`). ✓
- Inner `catch (AggregateException)` Proactor.cs:237-330 / Reactor.cs:196-295 — classifies inner exceptions against the action exceptions, NO `MessageMappingException` arm (A-2 grounding holds). ✓
- `TranslateMessage` directly awaited: Proactor.cs:231 (`await`), Reactor.cs:190 (direct). ✓
- A-1 asymmetry: Reactor `ConfigurationException` increments at Reactor.cs:300; Proactor's block (331-339) does not. ✓
- Reference reject blocks (`RejectMessageAction`/`InvalidMessageAction`) have the claimed `IncrementUnacceptableMessageCount(); RejectMessage(...); continue;` shape (Proactor.cs:359-372, Reactor.cs:325-338). ✓
- `RejectionReason { None, Unacceptable, DeliveryError }` and `record MessageRejectionReason(RejectionReason, string? Description = null)` — MessageRejectionReason.cs:30-51. ✓
- `UnacceptableMessageLimit` (int, default 0) MessagePump.cs:135; `IncrementUnacceptableMessageCount()` :176; `<= 0` guardrail Proactor.cs:556 / Reactor.cs:548. ✓
- SQS V4 `RejectAsync` at :149; Unacceptable→IMQ→DLQ-fallback in `DetermineRejectionRoute`; `RefreshMetadata` writes `rejectionReason` key at :501. Not changed (NFR-2). ✓
- Catch chain order (Architecture Overview) matches source exactly. ✓
- Scope-reduction consistency: title, Scope line, Decision, Key Components `[changed]`/`[unchanged]` tags, Implementation, Alternatives all agree only the mapping block changes. No stale "four paths"/"two blocks" language; every `DeliveryError` mention is contextual (enum listing, "not used by this change", or the rejected alternative). Alternative #1 rejects routing the catch-all on a coherent handler-failure-policy basis that does not contradict the Decision. ✓
- "Related ADRs: none" accurate; Parent-requirement relative path `../../specs/0030-reject_mapping_errors/requirements.md` from `docs/adr/` resolves. ✓
- Live requirements all honored (FR-1/3/5/6/7/8, NFR-1..4, C-1..C-6 incl. C-6 unguarded reject, A-1, A-2 captured as the headline risk with AC-12 real-`TranslateMessage` mitigation). No scope creep beyond requirements. ✓

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 0 |
| 0-49 (Low) | 2 |

**Total findings**: 2
**Findings at or above threshold (60)**: 0

The ADR is fully grounded against the real source, internally consistent after the scope reduction, addresses every live requirement, introduces no out-of-scope changes, and carries honest negative consequences plus the A-2 wrapping risk with its AC-12 mitigation. Both findings are cosmetic (sub-50); Finding 1 has been fixed. Verdict: PASS.
