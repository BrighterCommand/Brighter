# Review: design — 0034-failed-delivery-context

**Date**: 2026-06-11
**Threshold**: 60
**Verdict**: PASS

**Round 5** (re-review after two material post-round-4 changes: the InMemory confirmation-producer expansion — now a fire-and-forget pump — and the symmetric success/failure confirmation-span decision). No findings at or above threshold 60. Consider addressing the lower-scored items below. Rounds 1–4 issues all resolved and verified.

## Findings

### 1. InMemory pump under-specification: switch name, failure-hook signature, worker count/ordering, and channel boundedness are unspecified (Score: 58)

The ADR's "Confirmation-capable in-memory producer" section describes a fire-and-forget `Channel`+worker pump but leaves several behaviour-shaping decisions open. The current `InMemoryMessageProducer` is `sealed` with a 3-arg ctor `(IAmABus, Publication?, InstrumentationOptions)` and empty `Dispose`/`DisposeAsync` (verified `InMemoryMessageProducer.cs:39,51-57,82,87`), so the pump is genuinely new surface. The ADR says only: a "new option (default off)", a hook "e.g. `Func<Message,bool>`/topic-or-id predicate", and a worker that drains on dispose. It does not pin: (a) the exact option/switch name or where it is set (ctor param vs property); (b) the exact failure-hook delegate type; (c) single vs multiple worker threads and whether per-topic FIFO ordering is guaranteed; (d) bounded vs unbounded channel / back-pressure. These are the kind of gaps where two developers build it differently — though for a test/local-dev affordance the blast radius is lower than a production contract, which is why this sits just under threshold rather than High.

**Evidence**: ADR `:200-204`: "A new option (default off)... A configurable hook (e.g. `Func<Message,bool>`...)... drained/completed on Dispose/DisposeAsync". No worker-count, ordering, or channel-capacity decision is stated. Compare the precision the ADR applies to `PublishConfirmationResult` (exact record shape, member types) — the pump gets none of that.

**Recommendation**: Name the switch and the hook delegate type explicitly; state single-worker + FIFO (or explicitly disclaim ordering); state the channel is unbounded (or bounded with the chosen drop/wait policy); state whether the switch is a ctor arg or settable property.

---

### 2. `SendWithDelay`/scheduler interaction with the pump is not addressed (Score: 55)

The fire-and-forget design says `Send`/`SendAsync` enqueue onto the pump, but `SendWithDelay`/`SendWithDelayAsync` either delegate to `Send`/`SendAsync` (zero delay) or hand off to a configured `Scheduler` (`InMemoryMessageProducer.cs:157-204`). When a scheduler is used, the scheduler eventually calls back into `Send`, which would then enqueue onto the pump — so the capture-at-`Send` invariant would capture the *scheduler's* `Activity.Current`, not the original dispatch span. The ADR never mentions `SendWithDelay` for InMemory.

**Evidence**: ADR InMemory section (`:198-204`) discusses only `Send`/`SendAsync`. `SendWithDelay` routes through `Scheduler.Schedule(...)` (`InMemoryMessageProducer.cs:167-171`), a path the pump design does not account for.

**Recommendation**: State that the pump engages only on the immediate `Send`/`SendAsync` path (delayed sends route through the scheduler and re-enter `Send` later), or explicitly scope delayed sends out of the confirmation-capable behaviour.

---

### 3. Ordering/visibility consequence of fire-and-forget (consumer cannot see message until the worker writes the bus) is not called out as a behavioural consequence (Score: 50)

With the switch on, `Send` returns *before* the `InternalBus` is written, so a test that calls `Send` then immediately inspects the bus will not see the message until the worker runs. The ADR frames this positively ("faithfully emulates a real confirmation-based producer") but does not list the negative consequence that existing in-process test patterns relying on synchronous bus visibility would need to await/poll. Acknowledged implicitly via "default off preserves today's behaviour", so it is non-blocking, but the Consequences section should state it.

**Evidence**: ADR `:201`: "does **not** write to the bus inline... returns immediately". Negative consequences list (`:234`) mentions lifecycle/draining and the event-type break but not the read-after-`Send` visibility change.

**Recommendation**: Add one bullet to Negative consequences noting that, when on, bus visibility is deferred to the worker, so tests must await/poll rather than read synchronously after `Send`.

---

### 4. Alternatives claim "those four `Send` methods already end in `CancellationToken cancellationToken = default`" is inaccurate for the two sync methods (Score: 45)

The Alternatives rejection of the `RequestContext?`-on-`Send` mechanism argues a positional-insertion compile break because "those four `Send` methods already end in `CancellationToken cancellationToken = default`". Only the two **async** methods do; the two **sync** methods take no `CancellationToken`.

**Evidence**: `IAmAMessageProducerAsync.cs:49,57` end in `CancellationToken cancellationToken = default` (true), but `IAmAMessageProducerSync.cs:45` is `void Send(Message message)` and `:52` is `void SendWithDelay(Message message, TimeSpan? delay)` — neither has a trailing `CancellationToken`. So the "all four" framing is wrong; the positional-break argument holds only for the async pair.

**Recommendation**: Scope the claim to the two async methods, or restate the rejection on its other (valid) grounds (all ~10 gateways absorb an unused param; self-sourcing makes it unnecessary). The decision is unaffected.

---

### 5. Async `EndSpans` line reference `:945` points at the batch path, not the per-message async dispatch (Score: 30)

C-7 cites the producer span being ended "via `EndSpans(producerSpans)` (`:863`, `:945`...)". `:863` is the sync path (correct) and `:1006` is the per-message `DispatchAsync` EndSpans; `:945` is the **batch** dispatch EndSpans. Minor cosmetic mismatch; the surrounding reasoning (`:857/:858` sync reset, `:1007` async restore) is all accurate.

**Evidence**: `OutboxProducerMediator.cs`: `:863` (sync EndSpans), `:945` (batch EndSpans), `:1006` (async-message EndSpans), `:1007` (`requestContext.Span = parentSpan`).

**Recommendation**: Change `:945` to `:1006` (or note `:945` is the batch path) in C-7.

---

## Verified load-bearing claims (no finding — central correctness confirmed)

- **Orphan-FIX mechanism WORKS (the round-5 crux).** Confirmed by direct .NET probe: `CreateDbSpan` calls `ActivitySource.StartActivity(name, kind, string parentId, ...)` (`BrighterTracer.cs:525-530`) with `parentId = parentActivity?.Id` (`:496`); when that string `parentId` is null, the overload **falls back to ambient `Activity.Current`** — the produced span's `ParentId` equals S2's Id and its `Parent` is S2 by reference. So setting `Activity.Current = S2` before the success branch's `MarkDispatched` (whose `requestContext?.Span` is the null construction-time span, `OutboxProducerMediator.cs:749,776`; `RelationDatabaseOutbox.cs:770-773`) genuinely re-parents the DB span under S2. The ADR's central new claim is correct, not false.
- **Orphan CLAIM is true.** Construction-time `RequestContext` (`:162`) has an empty thread-keyed `_spans` (`RequestContext.cs:113-125`) ⇒ `.Span` null on any thread ⇒ `parentId` null; on the Kafka `Task.Run` callback thread (`KafkaMessageProducer.cs:373,381`) `Activity.Current` is unset ⇒ today's MarkDispatched span is a disconnected root.
- `CreateProducerSpan` sets `Activity.Current = S1` at `BrighterTracer.cs:700`; `CreateSpan` accepts `ActivityLink[]? links` at `:106`. Confirmed.
- InMemory: synchronous inline `(true, message.Id)` at `:100,123,145`; event is `Action<bool, Id>` at `:77`; InMemory does **not** implement `ISupportPublishConfirmation` and has **no external source subscribers** (grep clean except `bin/` XML-doc artifacts) — "no migration cost" accurate.
- Kafka `:52` unbound catch; `PublishResults` hardcodes `(false, string.Empty)` fall-through; `result.Message.Headers = []` empty (FR-8 read-collection caveat valid). RMQ async raise `(false, messageId)` at `:480` within `OnPublishFailed`, `_pendingConfirmations` is `Dictionary<ulong,string>` (`:60`), `AddPendingConfirmation` (`:397`), pre-existing Debug log `:481`. `TripTopic(RoutingKey?)` at `:1168` with `IsNullOrEmpty` guard at `:1170`; non-confirmation trips at `:998`/`:933`. `Id.Empty` `:53`, conversions `:95,:102`. `AddBrighterDefault` is a bare `AddRetry` with no telemetry strategy (`:57-67`). All match the ADR.
- Requirements↔ADR alignment: FR-10 gone from requirements.md; FR-2 symmetric; OOS-4 narrowed to delivery semantics; AC-13 rewritten symmetric; ground-truth item #5 present (`requirements.md:164`). The InMemory switch toggles only the in-memory provider's confirm timing/outcome, not mediator behaviour, so it does not contradict OOS-3/NFR-5. No drift found.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 3 |
| 0-49 (Low) | 2 |

**Total findings**: 5
**Findings at or above threshold (60)**: 0
