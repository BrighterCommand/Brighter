# Review: tasks — 0034-failed-delivery-context

**Date**: 2026-06-12
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. (Re-review after fixes; the two sub-threshold nits from this round were folded in — see "Resolution" below.)

## History

- **Round 1 (NEEDS WORK, 3 findings ≥60):** (1) Phase 7 Kafka slices claimed broker-free/unit-level but target `internal`/`private` producer internals only reachable with a live broker [High 84]; (2) coverage tables' "no gaps / no broker needed" claim was false [Medium 66]; (3) Phase 3 async-pump slice bundled four behaviors under one approval gate [Medium 62].
- **User direction (2026-06-12):** rewrite Phase 7/8 as broker-required (broker-specific tests belong in the separate gateway assemblies); split the pump slice; update ADR 0063 to avoid confusing reviewers. **Plus new feedback:** `InMemoryOutboxCircuitBreaker` uses a plain unlocked `Dictionary` — must be `ConcurrentDictionary` for the concurrent `TripTopic` NFR-3/AC-10 require.
- **Round 2 (PASS):** all fixes verified against the working tree; coverage tables accurate; every spot-checked `file:line` citation resolved correctly. Two sub-threshold Medium nits raised and then folded.

## Changes applied

**Phase 7 (Kafka) + Phase 8 (RMQ) → broker-required integration tests.**
- Locked decision #3 added: Kafka/RMQ-specific FRs are broker-required integration tests in `Paramore.Brighter.Kafka.Tests` / `.RMQ.Async.Tests` / `.RMQ.Sync.Tests`; no `InternalsVisibleTo`/seam. Old "unit-level / no broker" framing removed from the locked decisions and the Gaps section.
- Phase 7 slices: placed under `MessagingGateway/Proactor`, decorated `[Trait("Category","Kafka")]` + `[Collection("Kafka")]`; failure induced via oversized message → `ProduceException` `MsgSizeTooLarge` (matches FR-6/AC-6).
- Phase 8 slices: placed under `Proactor`/`Reactor`, `[Trait("Category","RMQ")]`; FR-2 enrichment asserted on the deterministically-reachable ack path (same `PendingConfirmation` feeds the nack raise), with an honest caveat that broker-nack is non-deterministic and the mediator failure path is already covered in-process (Phase 4).

**New Phase 1 `/tidy-first` structural task: `InMemoryOutboxCircuitBreaker` `Dictionary` → `ConcurrentDictionary`.**
- Grounded in `InMemoryOutboxCircuitBreaker.cs:42/:47/:55-64/:70-71`. Framed tidy-first (single-threaded semantics identical; a Dictionary race is non-deterministic and cannot be reliably unit-asserted — the behavioral assertion lives in Phase 6 AC-10).
- Wired into the NFR-3 coverage row, the ADR-decision table, the Phase 6 AC-10 dependency, the Gaps section, and the Critical Files list.
- ADR 0063 updated: "Thread safety (NFR-3)" + Key Components → `IAmAnOutboxCircuitBreaker` + Negative consequences now state the in-memory breaker is **not** currently safe and must move to `ConcurrentDictionary` (no longer implying it is already safe).

**Phase 3 async-pump slice split** into (a) fire-and-forget + single-reader FIFO + Task.Run raise, (b) single-start guard under concurrent first-enqueuers, and (c) batch `SendAsync` fan-out — each its own `/test-first` slice with explicit dependencies.

## Resolution of round-2 sub-threshold findings (folded)

- **(52) Batch fan-out bundled into pump (a):** split into its own slice "async pump (c)" depending on (a).
- **(50) Drain slice carried two divergent implementation mechanisms:** committed to `Task.WhenAll(bag)` in the task; the `Interlocked` count + TCS alternative is left in ADR 0063 "Lifecycle / draining" only.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 0 |
| 0-49 (Low) | 0 |

**Total findings**: 0 (2 sub-threshold nits folded)
**Findings at or above threshold (60)**: 0

## Verification notes (round 2)

- Kafka producer tests confirmed `[Trait("Category","Kafka")]` + `[Collection("Kafka")]`; RMQ confirmation tests `[Trait("Category","RMQ")]` (no `[Collection]`) — slice decorations match.
- `InMemoryOutboxCircuitBreaker.cs:42` is a plain unlocked `Dictionary<RoutingKey,int>`; `:70-71` `TripTopic` indexer-set; `:55-64` `CoolDown` with `.Remove` at `:62` — all citations accurate.
- Spot-checked citations resolved with no method/class drift: `OutboxProducerMediator.cs:741/768/998/1168/1170`, `KafkaMessagePublisher.cs:52/56-61/58-60`, `KafkaMessageProducer.cs:260/333/364-384/373/381`, RMQ.Async `:60/167/187/397/419/440/447/476/480/487`, RMQ.Sync `:58/153/233/243`, `InMemoryMessageProducer.cs:62/69/72/77/82/87/100/112/123/145/167`, `BrighterTracer.cs:106/490/641/700`.
- `BrighterSynchronizationContextsTests.cs:162` correctly flagged verify-only (binds `EventRunner`'s own `Action<bool,int>`, not `ISupportPublishConfirmation`).
- Coverage tables (FR-1..9, NFR-1..6, ADR decisions, AC-1..14) verified accurate; "no gaps" claim is true.
