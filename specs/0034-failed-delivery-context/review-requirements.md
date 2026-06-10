# Review: requirements — 0034-failed-delivery-context

**Date**: 2026-06-10
**Threshold**: 60
**Verdict**: NEEDS WORK

2 findings at or above threshold 60. Address these before approving.

_(Round 3 — verifying the wire-topic ripple from round 2. Trajectory: 7 → 4 → 2 findings ≥ threshold. The wire-topic decision converged cleanly across the glossary, FR-1/2/3/5, NFR-6, C-8, and AC-3/AC-3b; FR-2's earlier ActivityLink contradiction is fully resolved. Two residual issues remain, both localized.)_

## Findings

### 1. C-6 still claims the topic is "available from the captured producer" — contradicts the round-3 wire-topic decision in C-8 (Score: 82)

Round 3 changed the breaker/log topic to the per-message **wire topic** `message.Header.Topic`. That rippled into C-8, FR-3, NFR-6, the glossary, and AC-3b — but **C-6 was not updated** and still carries the pre-change claim that the topic is reachable from the closure's captured `producer`.

C-6 (line 108): *"The topic **is** available because the closure also captures the `producer` (see C-8)."*

This is now false and self-contradictory:
- The captured `producer` only yields the **static** `Publication.Topic`. The round-3 decision (C-8, FR-3, NFR-6, AC-3b) explicitly **rejects** `Publication.Topic` because it diverges from the wire topic for reply/rewritten-topic messages.
- The required value, `message.Header.Topic`, is **per-message**. The closure captures `producer` and a construction-time `RequestContext` but **not** the per-message `message` (verified: `OnMessagePublished += delegate(bool success, string id)` at `OutboxProducerMediator.cs:741, 765` — no `message` in scope).
- C-8 itself states the opposite of C-6: *"the wire topic is NOT carried by the current `OnMessagePublished` delegate (`Action<bool, string>`) nor by the captured construction-time `RequestContext` (C-6), so it must be plumbed to the callback."*

So C-6 says "topic is available (from the captured producer)" while C-8 — which C-6 cross-references with "(see C-8)" — says the topic is NOT available and must be plumbed in. Two developers reading C-6 vs C-8 reach opposite conclusions about whether plumbing is needed (the entire premise of FR-10/C-8).

**Evidence**: requirements.md:108 (C-6) vs requirements.md:112 (C-8); callback signature `delegate(bool success, string id)` at `OutboxProducerMediator.cs:741`, closure captures `producer`+`requestContext` only; `Publication.Topic` (static) distinct from `message.Header.Topic` for reply messages (`GetProducerLookupTopic`, `OutboxProducerMediator.cs:786`).

**Recommendation**: Update C-6 to align with C-8: replace "The topic *is* available because the closure also captures the `producer`" with "The captured `producer` yields only the **static** `Publication.Topic`, which the wire-topic decision rejects (C-8); the per-message wire topic `message.Header.Topic` is **not** reachable from the captured closure state and must be plumbed to the callback (see C-8), alongside the trace context (C-7)."

---

### 2. NFR-4 has no acceptance criterion (orphan requirement) (Score: 66)

NFR-4 ("No swallowed errors in the observability code") is concrete and testable: *"Failure within the observability handling itself ... MUST NOT throw out of the callback ... it must degrade gracefully (the breaker trip and Warning log are the priority)."* No AC asserts it. Round-2 fixes added per-NFR ACs (AC-11→NFR-1, AC-12/AC-12b→NFR-2, AC-10→NFR-3), but NFR-4 was left uncovered. This error-isolation guarantee is easy to regress and currently has no test obligation.

**Evidence**: requirements.md:97 (NFR-4); AC↔requirement scan finds no AC referencing NFR-4.

**Recommendation**: Add an AC, e.g. "AC-15 (NFR-4): *Given* the telemetry/span emission throws inside the callback, *when* `OnMessagePublished(false, ...)` fires, *then* the exception does not propagate out of the callback, the breaker is still tripped, and the Warning log is still emitted."

---

### 3. C-8 mis-attributes the `RoutingKey?` (nullable) signature to `IAmAnOutboxCircuitBreaker.cs:44` (Score: 55, below threshold)

C-8 (line 112): *"`TripTopic`, which takes a `RoutingKey?` — `IAmAnOutboxCircuitBreaker.cs:44`; mediator private overload `OutboxProducerMediator.cs:1168`"*. The interface method at `:44` is `TripTopic(RoutingKey topic)` — **non-nullable**. The `RoutingKey?` signature belongs only to the mediator private overload at `:1168`. Cosmetic — the callback path goes through the nullable mediator overload anyway.

**Evidence**: `IAmAnOutboxCircuitBreaker.cs:44` = `void TripTopic(RoutingKey topic);`; `OutboxProducerMediator.cs:1168` = `private void TripTopic(RoutingKey? routingKey)`.

**Recommendation**: Reword to "the interface overload takes `RoutingKey` (`:44`); the mediator's private overload takes `RoutingKey?` (`:1168`)."

---

### 4. FR-10 wording overstates what the `Send` parameter alone delivers (Score: 50, below threshold)

FR-10 adds only the `RequestContext?` param to `Send`/`SendAsync` — getting data **into** the producer. Surfacing the wire topic + context **out** to the mediator's `OnMessagePublished` callback requires extending the `Action<bool, string>` delegate or a correlation map. C-8 owns and defers that mechanism to the ADR, so it is not a contradiction, but FR-10's "the same callback enrichment carries both" reads as if the `Send` param alone suffices.

**Evidence**: requirements.md:89 (FR-10) vs requirements.md:112 (C-8); `OnMessagePublished` is `Action<bool, string>`.

**Recommendation**: Add a cross-reference clause: "Delivering the captured context and wire topic to the `OnMessagePublished` callback requires extending the callback contract or a correlation map (C-8) — the `Send` parameter alone only makes them available inside the producer."

---

### 5. AC-10 says "on background threads," softly contradicting NFR-3's deliberate correction (Score: 30, nit)

NFR-3 states the callback is "a synchronous delegate running on the invoking thread," "**not** ... a 'background thread'." AC-10 says "concurrent ... calls on background threads." Minor terminological drift.

**Evidence**: requirements.md:96 (NFR-3) vs requirements.md:147 (AC-10).

**Recommendation**: Change "on background threads" to "concurrently (on gateway-dependent threads)."

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 1 |
| 50-69 (Medium) | 3 |
| 0-49 (Low) | 1 |

**Total findings**: 5
**Findings at or above threshold (60)**: 2

---

_Converged cleanly (verified, no finding): wire-topic decision consistent across glossary, FR-1/2/3/5, NFR-6, C-8, AC-3/AC-3b — no section asserts `Publication.Topic` as the breaker/log source. FR-2's "MUST carry ActivityLink" contradiction fully resolved (Link contingent on C-7; standalone span/log/breaker unconditional). AC-3b, AC-12b well-formed. Codebase refs verified: `OutboxProducerMediator.cs:998` trips `message.Header.Topic`, `:933` trips `batch.RoutingKey`, `GetProducerLookupTopic` `:786`, `Publication.Topic` is `RoutingKey?` `Publication.cs:86`, `ProducerKey` composite `ProducerRegistry.cs:29/52`, Kafka FR-6/FR-8 claims, RMQ `:480`, OOS-2 dead catches `:262/:336` — all accurate except the C-8 `:44` nullable mis-attribution (finding 3)._
