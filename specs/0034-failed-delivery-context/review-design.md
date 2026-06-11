# Review: design — 0034-failed-delivery-context

**Date**: 2026-06-11
**Threshold**: 60
**Verdict**: PASS

**Round 3** (re-review after the `Activity.Current` capture-source change). No findings at or above threshold 60. Round 1 (4 findings, 1 Critical) and round 2 (1 High concurrency race) are all resolved and verified against the live codebase. Consider addressing the lower-scored nits below.

> **Post-round-3 edits (2026-06-11).** Nits #1 and #2 folded into the ADR (RMQ-async capture-ordering caveat added to the RMQ paragraph; the "plain Polly" claim re-cited to `ResiliencePipelineRegistryExtensions.cs:60-67`). #3 is optional and left as-is.

## Findings

### 1. Capture-before-await invariant is correct but understates the RMQ-async ordering it must displace (Score: 42)

The ADR's invariant — capture `Activity.Current?.Context` "as the **first action** inside `Send`/`SendAsync`, before any `await`" — is sound and implementable. But in the RMQ async producer the existing `SendWithDelayAsync` body does `BeginSend()` then `await EnsureBrokerAsync(...)` at `RmqMessageProducer.cs:167` **before** it ever touches `Span`/`WriteProducerEvent` (`:178`) or `AddPendingConfirmation` (`:187`). So the capture line must be inserted above `:167`. An implementer who places the capture next to the existing `AddPendingConfirmation`/`WriteProducerEvent` site (the natural "where the per-message tracking happens" spot) would place it *after* the `EnsureBrokerAsync` await, violating the invariant. Documentation-precision gap, not a design defect; the Kafka path has no equivalent tension (`WriteProducerEvent` merely `AddEvent`s, starts no child activity — `BrighterTracer.cs:951`).

**Evidence**: `RmqMessageProducer.cs:151` `BeginSend()`, `:167` `await EnsureBrokerAsync(...)`, `:178` `WriteProducerEvent`, `:187` `AddPendingConfirmation`.

**Recommendation**: Add a sentence to the RMQ paragraph noting the capture must precede the broker-ensure await. **(Done in post-round-3 edits.)**

---

### 2. "Plain Polly, no activity-creating strategy" cited the executor, not the pipeline definition (Score: 30)

The "plain Polly with no activity-creating strategy" claim cited `OutboxProducerMediator.cs:1111-1134` (`ExecuteWithResiliencePipeline`), which only fetches and runs the pipeline. The actual strategy set is in `ResiliencePipelineRegistryExtensions.AddBrighterDefault` (`:60-67`) — a single `AddRetry`, no telemetry middleware — so the claim is *true* but cited against the wrong file. The pipeline is registry-keyed and user-overridable; the claim holds for the Brighter default.

**Evidence**: `OutboxProducerMediator.cs:1113` `GetPipeline(CommandProcessor.OutboxProducer)`; `ResiliencePipelineRegistryExtensions.cs:60-67`.

**Recommendation**: Cite `ResiliencePipelineRegistryExtensions.cs:60-67`. **(Done in post-round-3 edits.)**

---

### 3. RMQ map-widening touch-points not fully enumerated (Score: 18)

The value-type widening also ripples to `RemovePendingConfirmations` (`:447-449`) and the sync project's add (`:153`) / raise (`:233-236`) sites, which the ADR folds into "analogous"/"touching the map" rather than enumerating. Implementable as stated.

**Evidence**: async `:480` raise, `:397` add, `:447-449` remove; sync `RmqMessageProducer.cs:58` (`ConcurrentDictionary<ulong,string>`), `:153` add, `:233-236` raise.

**Recommendation**: None required (optional enumeration partially added in post-round-3 edits).

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 0 |
| 0-49 (Low) | 3 |

**Total findings**: 3
**Findings at or above threshold (60)**: 0

---

### Verified against the live codebase (round-3 linchpin holds)

- **`Activity.Current = S1` at `BrighterTracer.cs:700`** — the activity `CreateProducerSpan` creates/returns IS the same S1 the mediator assigns to `producer.Span` (`OutboxProducerMediator.cs:825-827`/`:970-972`), so AC-2's "link == S1" is satisfiable.
- **Race genuinely resolved** — `Activity.Current` is BCL `AsyncLocal`; the OutboxProducer default pipeline (`ResiliencePipelineRegistryExtensions.cs:60-67`) is a bare `AddRetry` with no telemetry/activity strategy, so `Activity.Current` is not perturbed between span creation and the `Send` body on either sync or async path.
- **Capture-ordering invariant** implementable; `WriteProducerEvent` only `AddEvent`s (`BrighterTracer.cs:951`). RMQ-async tension is the documentation nit #1 (now addressed).
- **Consistency sweep clean** — every section sources from `Activity.Current`; `producer.Span`/`requestContext.Span` appear only as rejected options. No "flow the RequestContext"-as-current-design leftover. Tidy-first correctly states the construction-time `RequestContext` cleanup is NOT enabled.
- **RMQ map widening + async-null note** present and accurate (`Dictionary<ulong,string>`/`ConcurrentDictionary<ulong,string>`; `producer.Span = null` only at `:858`, absent in `DispatchAsync`).
- **Core unchanged claims verified**: `OnMessagePublished` is `Action<bool,string>` (`:42`); event change documented source+binary-breaking scoped to confirmation producers; `PublishConfirmationResult` documented as public type needing XML docs + success-branch population; `CreateSpan` takes `ActivityLink[]?` (`:106`); Kafka unbound catch (`:52`) with `MESSAGE_ID` to `deliveryResult.Headers`/`Message.Headers=[]`; `PublishResults` hardcodes `(false, string.Empty)` (`:381-382`); `TripTopic(RoutingKey?)` guard (`:1168-1171`); non-confirmation trip `:998`; OOS-2 `<string,string>` catches `:262`/`:336`.
