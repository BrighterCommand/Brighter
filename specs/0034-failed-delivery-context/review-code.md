# Review: code — 0034-failed-delivery-context

**Date**: 2026-06-14
**Threshold**: 60
**Reviewed at**: HEAD `5e51bcf03` (base `master` @ `8d2d43ba3`)
**Verdict**: PASS (after reconciliation)

The single at-threshold finding (#1) was reviewed with the author and **resolved as accepted-by-design**: `init` is the correct choice, and ADR 0063 + tasks.md were amended (2026-06-14) to match the code and record the rationale. No remaining findings at or above threshold 60.

## Findings

### 1. InMemory opt-in properties use `init` where the approved ADR + tasks mandate `set` — RESOLVED (docs reconciled to code) (Score: 62 → withdrawn)

**Resolution (2026-06-14):** Accepted-by-design. `init` deliberately enforces the "configure before first send" contract at the type level — flipping `UseAsyncPublishConfirmation` after the pump's channel/worker are live would leave them inconsistent, so `init` forbids it rather than relying on a documented convention, while still giving property-injection-style configuration via object initializer / DI. This is a *better* fit than the originally-written `{ get; set; }`, which would have permitted the unsafe mid-flight mutation the ADR did not anticipate. ADR 0063 (lines 208-209) and tasks.md (lines 80, 93) were amended to specify `init` and record this rationale, so the ADR-fidelity gap is closed. Original finding retained below for the record.

---

#### Original finding

ADR 0063 (Implementation Approach, "Confirmation-capable in-memory producer") specifies the two new InMemory properties as **settable** with an explicit, locked-in rationale:

- ADR line 208: *"A new **settable property** `bool UseAsyncPublishConfirmation { get; set; }` (default false) ... A settable property (not a ctor arg) matches the class's existing property-injection style (`Publication`, `Span`, `Scheduler` are all `{ get; set; }`...)"*
- ADR line 209: *"A **settable property** `Func<Message, bool>? PublishFailurePredicate { get; set; }`"*
- tasks.md line 80: *"Add settable `bool UseAsyncPublishConfirmation { get; set; } = false;`"*
- tasks.md line 93: *"Add settable `Func<Message, bool>? PublishFailurePredicate { get; set; }`"*

The implementation instead declares both as `init`-only, and the XML doc actively re-justifies the *opposite* of the approved decision:

**Evidence** — `src/Paramore.Brighter/InMemoryMessageProducer.cs`:
- Line 106: `public bool UseAsyncPublishConfirmation { get; init; } = false;`
- Line 120: `public Func<Message, bool>? PublishFailurePredicate { get; init; }`
- Lines 90-91 (XML doc): *"This is an init-only property: set it once via an object initializer at construction time. Changing it after the first send would leave the channel and worker in an inconsistent state."*
- Sibling properties verified `{ get; set; }`: `Publication` (`:70`), `Span` (`:77`), `Scheduler` (`:80`).

This is a deliberate design deviation (not a typo — the XML doc argues for it) from a design that was reviewed and marked Accepted, with no corresponding ADR amendment. Two concrete consequences:

1. It breaks the property-injection parity the ADR explicitly invoked as the *reason* for choosing a property over a ctor arg: `Publication`, `Span`, and `Scheduler` on this very class remain `{ get; set; }`, so the new members are now inconsistent with their siblings — the exact inconsistency the ADR's rationale was meant to avoid.
2. `init` blocks the documented usage pattern *"Expected to be set at configuration time, before the first send"* (ADR 208) for any code path that obtains the producer already-constructed (e.g. DI/factory-built instances configured post-construction), which a settable property would allow and `init` forbids.

The functional risk is low (object-initializer configuration still works, and every in-repo test sets these via initializers), which is why this is Medium rather than High — but it is a real, self-aware divergence from the locked contract with its stated rationale inverted, and per the project's "Change Scope" / ADR-fidelity rules it should either follow the ADR or carry an ADR amendment recording why `init` was chosen.

**Recommendation**: Either (a) change both to `{ get; set; }` to match the ADR/tasks and the sibling properties, and adjust the XML doc accordingly; or (b) amend ADR 0063 to record the `init` decision and its "inconsistent channel/worker state" rationale, and reconcile tasks.md lines 80/93.

---

### 2. Observability-fault log emitted at Error level (Score: 35)

The mediator logs `ConfirmationObservabilityError` at `LogLevel.Error` when `CreateConfirmationSpan` throws (`OutboxProducerMediator.cs:1284`, fired from the NFR-4 catch). NFR-1 mandates Warning for confirmation-failure logging and AC-11 asserts no Error. This is defensible because it logs an *observability fault* (NFR-4 path), not the delivery confirmation itself, so NFR-1/AC-11 do not strictly apply, and the AC-11 test (`When_a_confirmation_fails_should_log_warning_with_id_and_topic`) passes because the real tracer doesn't throw on the happy path. Noting it only as a consistency nit: a tracing fault inside the callback is arguably also a "not lost, recoverable" condition that NFR-1's spirit would put at Warning.

**Evidence**: `OutboxProducerMediator.cs:1284` — `[LoggerMessage(LogLevel.Error, "Observability failed while handling a publish confirmation; confirmation handling continued")]`.

**Recommendation**: Consider downgrading to Warning for consistency with NFR-1, or leave as-is (genuinely distinct event class). No blocker.

---

## Verified correct (no findings)

High-risk areas drilled and found correct:

- **ActivityContext capture invariant** — captured as the first synchronous action before any `await` in all three producers: Kafka `SendWithDelay`/`Async` (`KafkaMessageProducer.cs:258, 335`, above `_publisher.PublishMessage`), RMQ.Async (`RmqMessageProducer.cs:151`, above `BeginSend()`/the `EnsureBrokerAsync` await the ADR warned about), RMQ.Sync (`:132`, above the `lock`), InMemory (`:205`, before `TryWrite`).
- **InMemory two-stage drain** — `DisposeAsync` (`:139-144`) completes the writer, awaits the worker, then `Task.WhenAll(_raiseTasks)`. `_raiseTasks` mutated only by the single worker and read only after `await _worker`, so the await barrier makes it race-free (the ADR's preferred `Task.WhenAll`-over-bag mechanism). Covered by `When_disposing_should_drain_all_confirmations_before_returning`.
- **Single-start guard** — `Interlocked.CompareExchange(ref _pumpStarted, ...)` (`:206`) with `SingleReader = true` channel (`:45`); covered by `When_concurrent_first_enqueuers_should_start_one_worker` (20-thread Barrier race).
- **AC-13 explicit re-parenting** — mediator copies the request context and sets `Span = S2`, passing it to `MarkDispatched`; the test asserts `dbSpan.ParentSpanId == confirmation.SpanId` (not merely "has a parent"). Success branch emits no Warning and no trip.
- **Failure branch (AC-12b)** — no MarkDispatched, no awaited Outbox/broker call; only Warning + `TripTopic(result.Topic)`.
- **Kafka FR-8** — id read from report-level `headers` via `TryGetLastBytesIgnoreCase(MESSAGE_ID, ...)` (`KafkaMessageProducer.cs:391`), NOT `result.Message.Headers`; wire topic from captured `message.Header.Topic`, not `report.Topic`. Broker test forces the synthetic NotPersisted path with a 2 MB body.
- **Kafka FR-6** — catch binds `pe`, logs `pe.Error.Code` + `pe.Error.Reason` at Warning, synthetic NotPersisted flow preserved.
- **NFR-4 isolation** — observability wrapped so a throw can't escape; breaker trip still runs (`ThrowingConfirmationTracer` test).
- **NFR-3 breaker** — `Dictionary`→`ConcurrentDictionary`, `Remove`→`TryRemove`; AC-10 forced overlap via `Barrier` in `GatingConfirmationTracer`.
- **Bulk-dispatch change** — dropping `and not ISupportPublishConfirmation` is correct; inner `producer is not ISupportPublishConfirmation && sent` guard defers MarkDispatched to the callback; `!sent => TripTopic(batch.RoutingKey)` and `else throw` preserved.
- **Broker tests genuinely broker-backed and traited** — RMQ `[Trait("Category","RMQ")]` real localhost; Kafka `[Trait("Category","Kafka")][Collection("Kafka")]` real `localhost:9092`. The mandated RMQ "verified indirectly" comment is present (`When_a_confirmation_is_received_should_carry_id_topic_and_context.cs:33-38`).
- **TDD signatures** — Phase 1 (`98053c6bc`) is a genuine behavior-preserving `refactor`; test-first commits precede/accompany impl.
- **XML docs** — `PublishConfirmationResult` type + all 4 members, both new InMemory props, `CreateConfirmationSpan` on impl + interface all documented.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 1 (resolved — docs reconciled to code) |
| 0-49 (Low) | 1 |

**Total findings**: 2
**Findings at or above threshold (60)**: 1, resolved (0 outstanding)
