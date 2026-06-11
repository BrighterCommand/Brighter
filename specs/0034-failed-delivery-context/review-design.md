# Review: design — 0034-failed-delivery-context

**Date**: 2026-06-11
**Threshold**: 60
**Verdict**: PASS

**Round 4** (re-review after the `PendingConfirmation` named-record refinement). No findings at or above threshold 60. Rounds 1–3 issues (Critical AC-2 linchpin, optional-param-ordering break, concurrency race) all resolved and verified. The refinement is fully grounded and consistent. Only two cosmetic nits below.

> **Post-round-4 edits (2026-06-11).** Both nits folded in: RMQ touch-points now cite `RemovePendingConfirmations`/`RemoveConfirmationsLocked` (`:419`, lookup `:447`); the resilience-extension path now includes the `Extensions/` segment (`src/Paramore.Brighter/Extensions/ResiliencePipelineRegistryExtensions.cs:57-67`).

## Findings

### 1. `RemovePendingConfirmations` cited at `:447` actually lands inside `RemoveConfirmationsLocked` (Score: 18)

The ADR cited the RMQ-async touch-points as `AddPendingConfirmation`/`RemovePendingConfirmations`/`OnPublishFailed` at `:397, :447, :476`. `AddPendingConfirmation` (`:397`) and `OnPublishFailed` (`:476`) are correct, but `RemovePendingConfirmations`'s header is at `:419`; line `:447` is the `_pendingConfirmations.TryGetValue` inside helper `RemoveConfirmationsLocked` — i.e. the actual line whose value type changes. Not misleading (matches round-3's `:447-449` reference to the same removal logic), just not the method header the prose named. Cosmetic line drift only.

**Evidence**: `RmqMessageProducer.cs:419` `RemovePendingConfirmations(...)`; `:447` `TryGetValue(...)` inside `RemoveConfirmationsLocked`.

**Recommendation**: Cite `:419` (header) and `:447` (value-typed lookup) together. **(Done in post-round-4 edits.)**

---

### 2. Resilience-extension path omitted `Extensions/` subfolder (Score: 8)

The ADR cited `ResiliencePipelineRegistryExtensions.cs:60-67`; the file actually lives at `src/Paramore.Brighter/Extensions/ResiliencePipelineRegistryExtensions.cs`, with `AddBrighterDefault`'s bare `AddRetry` (no telemetry/activity strategy) at ~`:57-67`. Pre-existing across prior rounds; content claim correct, path incomplete.

**Evidence**: `src/Paramore.Brighter/Extensions/ResiliencePipelineRegistryExtensions.cs` — `AddBrighterDefault` `TryAddBuilder(CommandProcessor.OutboxProducer, … AddRetry(…))`, no activity middleware.

**Recommendation**: Add the `Extensions/` segment. **(Done in post-round-4 edits.)**

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 0 |
| 0-49 (Low) | 2 |

**Total findings**: 2
**Findings at or above threshold (60)**: 0

---

### Verified against the live codebase (refinement is grounded)

- **Record types real and coherent.** `Id` (`Id.cs:38`, a `record`) and `RoutingKey` (`RoutingKey.cs:36`, a `class`) are real Brighter types, valid as `readonly record struct` members; `ActivityContext?` is a valid nullable struct member. At the stash site `RmqMessageProducer.cs:187` the producer holds `message`, so `message.Id` (an `Id`, `Message.cs:100`) and `message.Header.Topic` (a non-nullable `RoutingKey`, `MessageHeader.cs:312`) are both available — coherent with the record's strong-typed members. The ADR accurately notes the current map stores `message.Id.Value` (a string).
- **`record struct` choice** defensible (small, short-lived dictionary value); no over-claim.
- **Consistency sweep clean** — all three RMQ mentions (Implementation Approach, Consequences §Negative, Critical Files) describe the named internal `PendingConfirmation` record; no leftover "widen the value type"/loose-tuple phrasing; the internal-vs-public contrast with `PublishConfirmationResult` is explicit and non-contradictory.
- **Capture-ordering caveat** (RMQ-async `await EnsureBrokerAsync` at `:167` precedes per-message tracking → capture at top) remains coherent and explicitly documented.
- **Still-load-bearing claims intact**: `Activity.Current = S1` at `BrighterTracer.cs:700` (AsyncLocal/race-free capture source); OutboxProducer default pipeline has no activity strategy; `OnMessagePublished` `Action<bool,string>` (`:42`) → `Action<PublishConfirmationResult>`, source+binary-breaking scoped to confirmation producers; `PublishConfirmationResult` public type w/ XML-doc + success-branch population; Kafka unbound catch (`:52`); `PublishResults` hardcodes `(false, string.Empty)` (`:381-382`); `TripTopic(RoutingKey?)` guard (`:1168-1171`); non-confirmation trip (`:998`).
