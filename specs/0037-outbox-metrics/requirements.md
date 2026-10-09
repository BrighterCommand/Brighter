# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Linked Issue**: RLOI-4185

## Glossary

These terms are used with precise meaning throughout this document. Where a term names existing code, the file is given so the term cannot be re-interpreted.

- **Outbox add ("added")**: a message is *added* when Brighter has successfully written it to the Outbox store. Concretely, when `OutboxProducerMediator.AddToOutbox` / `AddToOutboxAsync` (`src/Paramore.Brighter/OutboxProducerMediator.cs`) complete without throwing `ChannelFailureException`, or when `EndBatchAddToOutbox` / `EndBatchAddToOutboxAsync` complete without throwing (in which case every message in that batch is added). A message buffered by `AddToOutbox(..., batchId: "x")` is **not** added until the matching `EndBatchAddToOutbox*` succeeds.
- **Dispatch attempt**: one call by the mediator to a producer's `Send` / `SendAsync` for a message (or to the bulk producer's send for a batch), made from `Dispatch`, `DispatchAsync`, or `BulkDispatchAsync`.
- **Cleared ("published")**: a message is *cleared* at the instant Brighter records it as dispatched in the Outbox — i.e. the instant a `MarkDispatched` / `MarkDispatchedAsync` call for that message id succeeds. This single rule covers every transport:
  - **Non-confirmation producer** (producer does not implement `ISupportPublishConfirmation`): the send returned success (`sent == true`) and the mediator then marks the message dispatched inline, in `Dispatch`, `DispatchAsync` or `BulkDispatchAsync`.
  - **Confirmation-based producer** (implements publish confirmation): the send call returning is *not* "cleared". The message is cleared only when the broker confirmation arrives reporting success and `HandleAsyncPublishConfirmation` / the `ConfigurePublisherCallbackMaybe` delegate marks it dispatched. A confirmation reporting failure never clears the message.

  "Cleared" and "published" are used interchangeably in this document and always mean the above. "Published" does **not** mean "consumed" or "processed by the downstream service".
- **Explicit clear**: a clear initiated by application code calling `ClearOutbox(Id[] posts, ...)` or `ClearOutboxAsync(IEnumerable<Id> posts, ...)` for named message ids.
- **Sweeper clear**: a clear initiated by `OutboxSweeper.SweepAsync()` (`src/Paramore.Brighter/OutboxSweeper.cs`) -> `ClearOutstandingFromOutboxAsync(amountToClear, minimumAge, useBulk, ...)` -> `BackgroundDispatchUsingAsync`, which selects messages via `OutstandingMessagesAsync` rather than by id. Both clear kinds converge on the same `DispatchAsync` / `BulkDispatchAsync` methods, so the two are **not** distinguishable at the dispatch point today.
- **Creation time**: `message.Header.TimeStamp` (`src/Paramore.Brighter/MessageHeader.cs`, `DateTimeOffset`, defaulting to `DateTimeOffset.UtcNow` at header construction). It is set on the add path, persisted by the Outbox stores, and read back with the message, including by a different process after a restart.
- **Add-to-publish latency ("latency")**: `dispatch time - creation time`, where *dispatch time* is the same `_timeProvider.GetUtcNow()` instant that is passed to the `MarkDispatched` call which clears the message. This basis is a decided requirement, not an option (see C-1).
- **Meter enabled / disabled**: Brighter's existing meters expose `bool Enabled` (`IAmABrighterMessagingMeter`, `IAmABrighterDbMeter` in `src/Paramore.Brighter/Observability/`), which is false when no OpenTelemetry `MeterListener` is attached to any of the meter's instruments — i.e. when the host application has not called `AddBrighterInstrumentation()` on a `MeterProviderBuilder` (`src/Paramore.Brighter.Extensions.Diagnostics/BrighterMetricsBuilderExtensions.cs`).
- **Brighter meter**: the meter named by `BrighterSemanticConventions.MeterName` (`"Paramore.Brighter"`), the only meter name registered by `AddBrighterInstrumentation()`.
- **Operator**: a person running a Brighter application in production (SRE / on-call engineer) who consumes metrics via a dashboard or alert, and who cannot read traces because Brighter's Outbox spans are unsampled in that environment.

## Problem Statement

As an **operator of a service that publishes through a Brighter Outbox**, I need **metrics for how many messages enter the Outbox, how many leave it, how many of those were rescued by the sweeper rather than dispatched inline, and how long messages wait between being written and being published**, so that **I can see, dashboard and alert on Outbox publish lag and Outbox backlog growth instead of discovering them from downstream business failures.**

Today the only Outbox observability signals are trace spans and span events: `BrighterTracer.WriteOutboxEvent(BoxDbOperation.Add, ...)` on the add path, per-message clear spans on the explicit clear path, and one clear span plus a `BoxDbOperation.OutStandingMessages` event on the sweeper path. These are inadequate for three reasons:

1. **They are unsampled in production.** Operators run tail/ratio sampling, so Outbox spans are effectively absent. There is no aggregate signal at all.
2. **The add-path event is silently dropped when there is no ambient span.** `BrighterTracer.WriteOutboxEvent` writes an `ActivityEvent` onto `requestContext.Span` rather than creating its own span, so adds made outside a sampled trace leave no record whatsoever.
3. **Spans cannot answer rate/ratio questions.** "What fraction of my messages are being left to the sweeper?" and "what is my p99 add-to-publish latency?" are aggregate questions that require counters and a histogram.

This gap caused a production incident (Jira PI-36093, UK): NOFR events were emitted after significant lag, so `IgnoredOrders` were raised before Partners / OrderPad / JetConnect could "Accept" the orders. The Outbox publish lag that caused it was invisible — there was no metric to dashboard, no metric to alert on, and no way to confirm after the fact how long messages had sat in the Outbox. The consuming service work (Jira RLOI-4186, "[OrderDispatchWorker] Improve outbox observability") is blocked on this spec, because it intends to build dashboards and alerts directly on these metrics.

Aggravating factor: the Outbox circuit breaker (`IAmAnOutboxCircuitBreaker`) can trip a topic so that `OutstandingMessagesAsync` skips its messages entirely. When that happens the Outbox quietly stops draining for that topic, and today nothing in metrics moves.

## Proposed Solution

Emit OpenTelemetry **metrics** for Outbox activity from the `Paramore.Brighter` meter, so that an operator of any Brighter application that already calls `AddBrighterInstrumentation()` gains four capabilities with no further opt-in:

1. **Added volume** — the number of messages written to the Outbox, per destination.
2. **Cleared volume** — the number of messages recorded as dispatched from the Outbox, per destination.
3. **Sweeper attribution** — the ability to tell, from the cleared volume, which clears were sweeper clears and which were explicit clears, so "how much is my sweeper rescuing?" becomes a ratio an operator can chart and alert on.
4. **Add-to-publish latency distribution** — a distribution of `dispatch time - message.Header.TimeStamp` per cleared message, so p50/p95/p99 publish lag is chartable, including for messages dispatched by a sweeper in a different process instance or after a restart.

Because the latency basis is the *persisted message creation time* rather than an in-process stopwatch, the measurement survives the exact scenario that caused PI-36093: the process that wrote the message is gone and a later sweeper publishes it.

These metrics must be independent of tracing and sampling: they must be recorded on the add and clear code paths regardless of whether an ambient `Activity` exists, whether a tracer is registered, or what the sampler decides. That is a behavioural requirement on the design, not a suggestion — the existing trace-derived pipeline (`BrighterMetricsFromTracesProcessor`, which reads ended Brighter activities keyed off `instrumentation.domain`) is only acceptable as a mechanism to the extent it satisfies it.

Exact metric names, instrument types, units and attribute keys are **not** fixed by this document. They are an ADR proposal to be agreed with the Brighter maintainers in `#interest-oss-brighter` before the upstream PR (see C-7). Where this document shows a name, it is illustrative and marked as ADR-owned.

## Requirements

Requirements are stated as capabilities an operator must gain, plus the constraints those capabilities place on the design. Illustrative names appear as `[ADR-owned: paramore.brighter.outbox....]` and are **not** requirements.

### Functional Requirements

**FR-1 — Count messages added to the Outbox (single add).**
When a message is *added* via `AddToOutbox` (sync Outbox) or `AddToOutboxAsync` (async Outbox) without a `batchId`, Brighter MUST record exactly one "added" unit for that message. The recording MUST identify the message's destination (`message.Header.Topic`). No unit may be recorded when the add fails (see FR-8). `[ADR-owned: paramore.brighter.outbox.added.messages]`
*Example*: one `AddToOutboxAsync` call for a message with `Header.Topic == "order.created"` that returns without throwing results in exactly one added unit attributed to destination `order.created`. A second identical call results in a cumulative total of 2.

**FR-2 — Count messages added to the Outbox (batched add).**
When messages are buffered via `AddToOutbox(..., batchId)` / `AddToOutboxAsync(..., batchId)` and later written by a successful `EndBatchAddToOutbox` / `EndBatchAddToOutboxAsync`, Brighter MUST record one added unit **per message in the batch**, at the point the batch write succeeds — not at the point each message was buffered.
*Example A*: `StartBatchAddToOutbox()` then three `AddToOutboxAsync(..., batchId)` calls (topics `a`, `a`, `b`) then a successful `EndBatchAddToOutboxAsync` results in 3 added units: 2 attributed to destination `a`, 1 to destination `b`.
*Example B*: the same three buffered calls with **no** `EndBatchAddToOutbox*` call result in 0 added units, because no message was written to the store.
*Example C*: the same three buffered calls where `EndBatchAddToOutboxAsync` throws result in 0 added units (see FR-8).

**FR-3 — Count messages cleared from the Outbox.**
Whenever a message becomes *cleared* (per the Glossary: a `MarkDispatched` / `MarkDispatchedAsync` call for that message id succeeds), Brighter MUST record exactly one "cleared" unit, identifying the destination. This MUST hold for all four dispatch paths: `Dispatch` (sync), `DispatchAsync`, `BulkDispatchAsync`, and the publish-confirmation callbacks (`HandleAsyncPublishConfirmation`, the `ConfigurePublisherCallbackMaybe` delegate). `[ADR-owned: paramore.brighter.outbox.cleared.messages]`
The counter counts **clear events, not distinct messages**: if the same message id is cleared twice (for example a sweeper re-dispatches a message whose broker confirmation arrived late), two units are recorded. This is intended and consistent with Brighter's at-least-once delivery.
*Example A (non-confirmation, single)*: `ClearOutboxAsync([id1, id2])` where both sends succeed and both `MarkDispatchedAsync` calls succeed results in 2 cleared units.
*Example B (bulk)*: `BulkDispatchAsync` sends one batch of 4 messages for topic `t` with a non-confirmation producer and all 4 are marked dispatched, resulting in 4 cleared units attributed to destination `t` — not 1 unit for the batch.
*Example C (confirmation-based)*: `DispatchAsync` sends a message through a producer implementing publish confirmation. At the moment the send returns, 0 cleared units have been recorded. When the confirmation later fires reporting success and `MarkDispatchedAsync` succeeds, exactly 1 cleared unit is recorded.
*Example D (confirmation failure)*: the same producer fires a confirmation reporting failure. 0 cleared units are recorded; the message stays un-dispatched for the sweeper.

**FR-4 — Attribute cleared messages to sweeper clears versus explicit clears.**
The cleared volume of FR-3 MUST be decomposable by an operator into **sweeper clears** and **explicit clears**, using a bounded, low-cardinality dimension with exactly those two distinguishable states (the ADR may name a third state only if it also bounds it). It MUST be possible to build the ratio "sweeper clears / all clears" from the emitted metric alone, without joining to traces or logs.
Because both clear paths converge on the same `DispatchAsync` / `BulkDispatchAsync` methods (C-5), satisfying this requires the initiating context to be carried to the point of recording. Doing so MUST NOT change dispatch behaviour, ordering, producer lookup, `MarkDispatched` arguments, or circuit-breaker behaviour.
*Example A*: `OutboxSweeper.SweepAsync()` finds and successfully dispatches 5 messages on topic `t`. Result: 5 cleared units attributed as sweeper clears; the count of explicit clears is unchanged.
*Example B*: application code calls `ClearOutboxAsync([id1])` and it succeeds. Result: 1 cleared unit attributed as an explicit clear; the count of sweeper clears is unchanged.
*Example C*: in a process where both happen — 1 explicit clear then a sweep that clears 5 — an operator reading the metric can compute 5/6 sweeper-cleared.

**FR-5 — Record add-to-publish latency per cleared message.**
For every cleared unit recorded under FR-3, Brighter MUST also record one latency sample equal to `dispatch time - message.Header.TimeStamp`, where *dispatch time* is the same instant passed to the `MarkDispatched` call that cleared the message. The sample MUST be recorded as a distribution (so percentiles are derivable), MUST identify the destination, and MUST carry the same sweeper-versus-explicit dimension as FR-4. `[ADR-owned: paramore.brighter.outbox.publish.latency]`
*Example*: a message whose `Header.TimeStamp` is `2026-09-18T10:00:00.000Z` is cleared at `2026-09-18T10:00:00.250Z` (per an injected `TimeProvider`). Result: exactly one latency sample of 250 ms is recorded, attributed to the message's destination.

**FR-6 — Latency must survive process restart and cross-process dispatch.**
Because latency is computed from the persisted `message.Header.TimeStamp` and not from in-process state, a latency sample MUST be recorded even when the process that added the message no longer exists, and the sample MUST reflect the full wait, not the sweeper's own dispatch duration.
*Example*: process A adds a message at `10:00:00.000Z` and is then killed before dispatching it. Process B's sweeper picks the message up and clears it at `10:05:00.000Z`. Result: process B records exactly one latency sample of 300,000 ms (5 minutes), attributed as a sweeper clear — not a sample of the few milliseconds process B spent sending.

**FR-7 — Deterministic handling of unusable latency inputs.**
Latency inputs can be unusable in two, and only two, ways; each has one defined outcome:
- **(a) Negative computed latency** (dispatch time is earlier than creation time, e.g. clock skew between the host that created the message and the host that dispatched it): the sample MUST be recorded as **zero**, never as a negative value. Rationale: keeps the number of latency samples aligned with the cleared count and avoids negative values that metric backends reject or render nonsensically.
- **(b) No creation time attributable to the cleared message** (for example a confirmation callback that reports an empty message id, so Brighter cannot tie the confirmation to a known message): **no latency sample** is recorded. The cleared unit of FR-3 is still recorded if and only if `MarkDispatched` succeeded. Consequently the latency sample count MAY be lower than the cleared count; the ADR MUST document this divergence.
No other input is permitted to produce a skipped, negative, or synthetic sample.
*Example (a)*: creation time `10:00:01.000Z` (fast clock on the adding host), dispatch time `10:00:00.500Z`. Result: exactly one sample of 0 ms; no negative value is recorded; no exception is thrown.
*Example (b)*: a confirmation fires reporting success with an empty message id, and no message can be resolved. Result: cleared units increase by 1 if the mark-dispatched succeeded, latency samples increase by 0, and no exception is thrown.

**FR-8 — Failures must not be counted as successes.**
- A failed Outbox **add** MUST record 0 added units. Specifically, when `AddToOutbox` / `AddToOutboxAsync` throw `ChannelFailureException` because the resilience pipeline did not write the message, or `EndBatchAddToOutbox*` fails, nothing is counted.
- A failed **dispatch attempt** MUST record 0 cleared units and 0 latency samples. This covers: a send that returns `sent == false`, a send that throws, a bulk send that fails, a confirmation reporting failure, and a topic tripped by `IAmAnOutboxCircuitBreaker` so the sweeper never selected the message.
- A **failed mark-dispatched** MUST record 0 cleared units and 0 latency samples for that message: the metric follows the store, not the send. If the send succeeded but the `MarkDispatched` call did not succeed, nothing is counted (the message will be re-attempted by the sweeper and counted then).
*Example A*: an Outbox whose `AddAsync` always throws -> `AddToOutboxAsync` throws `ChannelFailureException` -> added units = 0.
*Example B*: a producer whose `SendAsync` throws -> `DispatchAsync` records cleared units = 0 and latency samples = 0, and existing behaviour (topic trip, log) is unchanged.
*Example C*: a sweep where `OutstandingMessagesAsync` returns 0 messages -> added units unchanged, cleared units = 0, latency samples = 0, and **no** zero-valued recordings are made at all.

**FR-9 — Metrics must not depend on tracing or sampling.**
Added, cleared and latency recordings MUST occur when: no `IAmABrighterTracer` is registered; `requestContext.Span` is null; `Activity.Current` is null; the configured sampler drops the trace; or `InstrumentationOptions` is `None`. No recording may be conditional on a span having been created or sampled. This is the requirement that distinguishes this work from the existing span events, which are dropped outright when the ambient `Activity?` is null.
*Example*: a process configured with a `MeterProvider` (via `AddBrighterInstrumentation()`) but **no** `TracerProvider` at all adds one message and clears it. Result: 1 added unit, 1 cleared unit and 1 latency sample are recorded.

**FR-10 — Attribute content must be safe and bounded.**
Recordings MUST NOT carry message-identifying or payload data as attributes — specifically not message id, message body, serialized headers, correlation id, partition key, or any application-supplied header-bag value. Attributes are limited to values with a bounded domain known from configuration (e.g. destination topic, the sweeper/explicit dimension, and existing service-resource attributes as already applied by `MeterProviderExtensions.GetServiceAttributes()` in `MessagingMeter`). Attribute keys MUST follow the existing conventions in `BrighterSemanticConventions` (OpenTelemetry semantic-convention keys where one exists, `paramore.brighter.*` otherwise).
*Example*: a recording for a message with id `9f8c9114-...` on topic `order.created` carries destination `order.created` and the clear-kind dimension, and carries no attribute whose value is `9f8c9114-...`.

**FR-11 — Available through the existing registration path.**
An application that already calls `AddBrighterInstrumentation()` on its `MeterProviderBuilder` MUST receive these metrics with no additional API call, no new package reference, and no configuration flag. The instruments MUST be created on the `Paramore.Brighter` meter (`BrighterSemanticConventions.MeterName`), which is the meter that call already registers.
*Example*: an application whose only observability setup is `Sdk.CreateMeterProviderBuilder().AddBrighterInstrumentation().AddInMemoryExporter(...)` sees the added, cleared and latency instruments in the exported metric set after one add and one clear, without the source being edited.

### Non-functional Requirements

**NFR-1 — Zero cost when no listener is attached.**
When no meter listener is attached (the `Enabled` property of the relevant meter abstraction is `false`), the Outbox add and clear paths MUST perform no metric-related work beyond the guard check itself: no attribute/tag array construction, no string formatting, no serialization, no additional Outbox or database read, and no additional `Activity` creation. The guard MUST be observable in a test (a meter double reporting `Enabled == false` receives zero record calls).

**NFR-2 — No behavioural change to the Outbox.**
Adding these metrics MUST NOT change: whether a message is written, sent, marked dispatched, or left for the sweeper; message ordering; producer lookup (`GetProducerLookupTopic`); circuit-breaker trip behaviour; exception types or messages; existing log entries; or existing spans and span events. The existing Outbox, sweeper and observability test suites MUST pass without modification to their assertions.

**NFR-3 — Instrumentation faults must be isolated.**
An exception originating in metric recording MUST NOT propagate into the Outbox add path, the dispatch path, the sweeper, or a producer's confirmation callback thread, and MUST NOT prevent a message from being added, sent, or marked dispatched. Confirmation callbacks in particular run on a broker/thread-pool thread where an escaping exception can tear the thread down.

**NFR-4 — Thread safety.**
Recording MUST be safe when the sweeper's background dispatch (`BackgroundDispatchUsingAsync`, which holds `_backgroundClearSemaphore`) runs concurrently with request-thread adds and with confirmation callbacks arriving on producer threads. No shared mutable state introduced for metrics may require callers to serialize access.

**NFR-5 — Target-framework compatibility.**
The implementation MUST compile and behave identically across all TFMs that `Paramore.Brighter` targets, not only `net8.0+`. APIs available only on newer frameworks (e.g. `FrozenSet`, as already handled with `#if NET8_0_OR_GREATER` in `MessagingMeter`) MUST be conditionally compiled with an equivalent fallback.

**NFR-6 — Bounded cardinality.**
The number of distinct attribute combinations per instrument MUST be bounded by `O(number of configured destinations x small constant)` — where the small constant is the product of the enumerated dimensions (clear kind, and any outcome dimension the ADR adds). No dimension may be derived from per-message data (see FR-10).

**NFR-7 — Test determinism.**
Latency assertions MUST be deterministic and MUST NOT depend on wall-clock timing or `Thread.Sleep`. The mediator already accepts a `TimeProvider` (`_timeProvider`), and `MessageHeader.TimeStamp` is settable, so tests MUST control both creation time and dispatch time explicitly. Tests live under `tests/` (e.g. `tests/Paramore.Brighter.Core.Tests/Observability/Metrics/`) and follow the repo's `When_...` naming convention, using meter doubles in the style of the existing `SpyMessagingMeter` / `DisabledDbMeter`.

**NFR-8 — Upstream acceptability.**
The change MUST be shaped to be acceptable as an upstream Brighter PR: MIT licence headers on new files, XML doc comments on new public members, naming and attribute style consistent with `BrighterSemanticConventions` and `MessagingMeter`/`DbMeter`, and no breaking change to public interfaces (see C-11).

### Constraints and Assumptions

- **C-1 (decided) — Latency basis is `message.Header.TimeStamp`.** Latency is `dispatch time - Header.TimeStamp`, not the duration of the dispatch call and not the time since the message was read from the Outbox. This is deliberate: it is the only basis that still works when the dispatching process is not the adding process — which is exactly the PI-36093 case. **Accepted imprecision:** the header timestamp is stamped during message construction, so the measured latency includes message mapping/transform time that occurs immediately before the Outbox write, and it is subject to clock skew between hosts (handled by FR-7a). These inaccuracies are accepted for this spec and MUST be documented in the ADR and in user-facing docs so operators do not misread the metric as a pure store-to-broker duration.
- **C-2 (assumption) — Creation time round-trips through the Outbox stores.** `MessageHeader.TimeStamp` is persisted and read back with the message. Assumed for all shipped Outbox implementations. Store-level precision may truncate sub-microsecond components; this is immaterial at the millisecond/second resolution operators care about.
- **C-3 — Existing metric infrastructure is trace-derived.** `MessagingMeter`, `DbMeter` and `BrighterMetricsFromTracesProcessor` (a `BaseProcessor<Activity>` keyed off the `instrumentation.domain` tag) derive metrics from *ended* Brighter activities. Reusing that mechanism for Outbox metrics is permitted only if FR-9 still holds; if it cannot, the ADR must choose a direct-instrument approach. Mechanism choice is an ADR decision, FR-9 is the constraint on it.
- **C-4 — Confirmation is asynchronous and carries limited data.** For publish-confirmation producers, `MarkDispatched` happens later, in a callback, and the callback receives a `PublishConfirmationResult` rather than the `Message` — so it does not carry `Header.TimeStamp`. The ADR must therefore decide how creation time reaches the clear-recording point for these transports; FR-7b defines the fallback when it cannot.
- **C-5 — Both clear kinds share one dispatch path.** `ClearOutboxAsync` (explicit) and `BackgroundDispatchUsingAsync` (sweeper) both call `DispatchAsync` / `BulkDispatchAsync`, so FR-4 cannot be satisfied at the dispatch point without plumbing the initiating context down. How that context travels is an ADR decision, subject to NFR-2.
- **C-6 — The circuit breaker can hide backlog.** `IAmAnOutboxCircuitBreaker` supplies `TrippedTopics` to `OutstandingMessagesAsync`, so a tripped topic's messages are never selected by the sweeper. With an outstanding-count gauge out of scope (OOS-1), an operator infers a stalled topic from added volume continuing while cleared volume for that destination goes to zero. The ADR/docs should state this explicitly so RLOI-4186 can build the right alert.
- **C-7 — Names and instrument types are not settled.** Final metric names, instrument types, units and attribute keys require a proposal agreed with the Brighter maintainers via the `#interest-oss-brighter` Slack channel, recorded in the ADR. Any name appearing in this document is illustrative. Requirements here are deliberately written as capabilities so that agreeing a different name does not invalidate them.
- **C-8 — Bulk dispatch marks dispatched by id.** `BulkDispatchAsync` marks dispatched from the batch's ids, while the `Message` objects for the batch are in scope one level up. FR-3/FR-5 require per-message recording in this path, which constrains where the recording can be made.
- **C-9 — Time is injectable.** `OutboxProducerMediator` already holds a `TimeProvider` used for the `MarkDispatched` timestamp, so FR-5's "same instant" rule and NFR-7's determinism are achievable without new plumbing.
- **C-10 — Process constraint: strict TDD.** Each behaviour must have a test written and approved before its implementation, per repo convention. Acceptance criteria below are written so each maps to at least one such test.
- **C-11 — No breaking public API changes and no Outbox-implementation changes.** The work MUST NOT change the shape of `IAmAnOutbox` / `IAmAnOutboxSync` / `IAmAnOutboxAsync` or require the `Paramore.Brighter.Outbox.*` packages to be edited, and MUST NOT introduce a breaking change to `IAmAnOutboxProducerMediator`. Instrumentation sits above the Outbox abstraction, in the mediator.
- **C-12 — No new storage or schema.** No Outbox table/column additions, no extra queries against the Outbox, and no new persisted state are permitted to satisfy these requirements.

### Out of Scope

- **OOS-1 — Outstanding/backlog count gauge.** The RLOI-4185 *background* text mentions wanting to know "how many messages currently exist in the outbox", but the ticket's stated goal lists only the four metrics above. A backlog gauge is deferred because it requires either polling the Outbox on a timer or an observable callback that queries the store — a cost, correctness (multi-instance double counting) and API question deserving its own spec. Operators can approximate backlog growth from added minus cleared volume in the interim (C-6).
- **OOS-2 — Archiver metrics.** Counts/latency for the archive provider / Outbox archiver and for Outbox deletion/compaction.
- **OOS-3 — New publish-failure counters.** Failed sends are already observable via the existing `messaging.client.sent.messages` / `messaging.client.operation.duration` instruments with `error.type`, and via logs and circuit-breaker trips. FR-8 only requires that failures are *not* miscounted as successes.
- **OOS-4 — Inbox, message-pump, handler or mapper metrics.** Unchanged by this spec.
- **OOS-5 — Dashboards, alerts and SLOs.** Built by the consuming service under RLOI-4186.
- **OOS-6 — Changes to individual Outbox implementations** (`Paramore.Brighter.Outbox.*`) and to Outbox schemas (C-11, C-12).
- **OOS-7 — End-to-end delivery latency.** Broker-to-consumer and produce-to-process latency are not covered; "published" stops at Brighter recording the message as dispatched (Glossary).
- **OOS-8 — Exporter, collector or backend configuration.** Host application concern.
- **OOS-9 — Backfill.** No metric values are produced for messages added before the upgrade; messages already sitting in an Outbox at upgrade time will produce cleared units and latency samples when they are eventually cleared, and those samples will legitimately be large.

## Acceptance Criteria

Each criterion is a Given/When/Then that can be asserted directly. "Metric recorder" means a test double or in-memory exporter attached to the `Paramore.Brighter` meter; "added units", "cleared units" and "latency samples" mean the quantities defined in FR-1/FR-3/FR-5.

**AC-1 — Single add is counted.**
*Given* a Brighter Outbox with a metric recorder attached, *when* a message with `Header.Topic == "order.created"` is added via `AddToOutboxAsync` (and again via `AddToOutbox` for the sync Outbox) and the add succeeds, *then* added units total exactly 1 per call, attributed to destination `order.created`, and no cleared unit or latency sample is recorded.

**AC-2 — Batched add is counted per message, at flush.**
*Given* a batch started with `StartBatchAddToOutbox()` and three messages buffered with that `batchId` (topics `a`, `a`, `b`), *when* the batch has been buffered but `EndBatchAddToOutboxAsync` has not yet been called, *then* added units total 0; *and when* `EndBatchAddToOutboxAsync` succeeds, *then* added units total 3 — 2 for destination `a` and 1 for destination `b`.

**AC-3 — Failed add is not counted.**
*Given* an Outbox whose write always fails, *when* `AddToOutboxAsync` throws `ChannelFailureException` (and likewise when `EndBatchAddToOutboxAsync` fails for a 3-message batch), *then* added units total 0 and the exception type and message are unchanged from current behaviour.

**AC-4 — Explicit clear is counted.**
*Given* two messages in the Outbox and a non-confirmation producer that sends successfully, *when* `ClearOutboxAsync([id1, id2])` completes, *then* cleared units total exactly 2, each attributed to its destination, and each attributed as an **explicit** clear.

**AC-5 — Sweeper clear is counted and attributed to the sweeper.**
*Given* 5 outstanding messages on topic `t` and a non-confirmation producer that sends successfully, *when* `OutboxSweeper.SweepAsync()` runs to completion, *then* cleared units total exactly 5, all attributed to destination `t` and all attributed as **sweeper** clears, and the explicit-clear total is 0.

**AC-6 — Sweeper versus explicit ratio is derivable.**
*Given* a recorder attached, *when* one explicit clear of 1 message and one sweep clearing 5 messages have both completed in the same process, *then* the recorded values allow the ratio 5 sweeper / 6 total to be computed from the cleared metric alone, with no reference to traces or logs.

**AC-7 — Bulk dispatch counts per message.**
*Given* a bulk-capable non-confirmation producer and 4 outstanding messages on topic `t`, *when* a sweep runs with `useBulk: true` and all 4 are marked dispatched in one send, *then* cleared units total exactly 4 (not 1) and latency samples total exactly 4.

**AC-8 — Confirmation-based producer counts at confirmation, not at send.**
*Given* a producer implementing publish confirmation, *when* the send has returned but no confirmation has arrived, *then* cleared units total 0; *and when* the confirmation fires reporting success and the mark-dispatched succeeds, *then* cleared units total exactly 1.

**AC-9 — Failed confirmation is not counted.**
*Given* the same confirmation-based producer, *when* the confirmation fires reporting failure, *then* cleared units total 0, latency samples total 0, existing behaviour (warning log, topic trip, message left un-dispatched) is unchanged, and no exception escapes the callback.

**AC-10 — Failed send is not counted.**
*Given* a producer whose send returns failure (and, in a second case, throws), *when* `DispatchAsync` completes, *then* cleared units total 0 and latency samples total 0, while the existing topic trip and log behaviour are unchanged.

**AC-11 — Failed mark-dispatched is not counted.**
*Given* a producer that sends successfully but an Outbox whose `MarkDispatchedAsync` fails, *when* `DispatchAsync` completes, *then* cleared units total 0 and latency samples total 0.

**AC-12 — Empty sweep records nothing.**
*Given* an Outbox with no outstanding messages, *when* `SweepAsync()` runs, *then* the recorder receives no recordings at all for added units, cleared units or latency samples — including no zero-valued recordings.

**AC-13 — Latency sample value is exact and uses the dispatch instant.**
*Given* a message whose `Header.TimeStamp` is `2026-09-18T10:00:00.000Z` and a fake `TimeProvider` set to `2026-09-18T10:00:00.250Z` at dispatch, *when* the message is cleared, *then* exactly one latency sample is recorded with value 250 ms, attributed to the message's destination and to the correct clear kind.

**AC-14 — Latency survives cross-process dispatch.**
*Given* a message persisted in a shared Outbox with `Header.TimeStamp == 10:00:00.000Z`, written by a mediator instance that is then disposed, *when* a **second**, independently constructed mediator's sweeper clears that message with its `TimeProvider` at `10:05:00.000Z`, *then* exactly one latency sample of 300,000 ms is recorded by the second instance, attributed as a sweeper clear.

**AC-15 — Negative latency is recorded as zero.**
*Given* a message whose `Header.TimeStamp` is `10:00:01.000Z` and a dispatch-time `TimeProvider` at `10:00:00.500Z`, *when* the message is cleared, *then* exactly one latency sample of 0 is recorded, no negative value is recorded, and no exception is thrown.

**AC-16 — Unattributable creation time skips only the latency sample.**
*Given* a confirmation-based producer that fires a successful confirmation with an empty message id such that no creation time can be resolved, *when* the callback completes, *then* latency samples total 0, the cleared unit is recorded if and only if the mark-dispatched succeeded, and no exception escapes the callback.

**AC-17 — Metrics are recorded with tracing absent.**
*Given* a host with a `MeterProvider` built via `AddBrighterInstrumentation()` and **no** `TracerProvider`, a mediator constructed with a null tracer, `InstrumentationOptions.None`, and a `RequestContext` whose `Span` is null (and `Activity.Current == null`), *when* a message is added and then cleared, *then* added units = 1, cleared units = 1 and latency samples = 1.

**AC-18 — Nothing is recorded and nothing is computed when no listener is attached.**
*Given* a meter double whose `Enabled` is `false` and no `MeterListener` attached, *when* 100 messages are added and cleared, *then* the double receives zero record calls, and the instrumentation code path performs no tag construction and issues no additional Outbox or database read (asserted via the double's call counters and the Outbox double's operation counts).

**AC-19 — No message-identifying data leaks into attributes.**
*Given* a message with a known id, a body, a partition key, a correlation id and a populated header bag, *when* it is added and cleared with a recorder attached, *then* no recorded attribute key or value contains the message id, body content, partition key, correlation id or any header-bag value, and every recorded attribute key matches an existing `BrighterSemanticConventions` constant or the `paramore.brighter.*` prefix.

**AC-20 — Cardinality is bounded.**
*Given* 3 configured destinations and 1,000 messages spread across them, cleared by both a sweeper and explicit calls, *when* all recordings are collected, *then* the number of distinct attribute combinations per instrument is at most `3 x (number of enumerated dimension values)` and does not grow with the number of messages.

**AC-21 — Available through the existing registration call only.**
*Given* an application whose only metrics setup is `Sdk.CreateMeterProviderBuilder().AddBrighterInstrumentation().AddInMemoryExporter(...)`, *when* one message is added and cleared, *then* the added, cleared and latency instruments appear in the exported metric set, all emitted from the meter named `Paramore.Brighter`, with no additional registration call in the application.

**AC-22 — Instrumentation faults do not break the Outbox.**
*Given* a meter double whose record methods throw, *when* a message is added, dispatched, marked dispatched and confirmed, *then* the message is still added, still sent, still marked dispatched, no exception is observed by the caller of the add/clear API, and no exception escapes the confirmation callback thread.

**AC-23 — Existing behaviour is unchanged.**
*Given* the pre-existing Outbox, sweeper, circuit-breaker and observability test suites, *when* the change is applied, *then* those suites pass with no assertion modified — in particular the existing Outbox span events, clear spans, confirmation spans, logs and topic-trip behaviour are unchanged.

**AC-24 — Duplicate clears are counted per clear event.**
*Given* a message that is cleared once by an explicit clear and then, because a confirmation arrived late, cleared again by a sweeper, *when* both mark-dispatched calls succeed, *then* cleared units total 2 (1 explicit, 1 sweeper) and latency samples total 2, documenting at-least-once semantics rather than de-duplicating.

**AC-25 — Concurrent add and sweep are safe.**
*Given* a sweep running on a background task while 50 adds run concurrently on other threads, *when* all operations complete, *then* added units total exactly 50, cleared units equal the number of messages actually marked dispatched, and no exception is thrown by any thread.

### Traceability: FR -> AC

| FR | Covered by |
|---|---|
| FR-1 Count single adds | AC-1, AC-17, AC-18, AC-25 |
| FR-2 Count batched adds | AC-2, AC-3 |
| FR-3 Count cleared messages | AC-4, AC-5, AC-7, AC-8, AC-17, AC-24 |
| FR-4 Sweeper vs explicit attribution | AC-4, AC-5, AC-6, AC-24 |
| FR-5 Latency per cleared message | AC-7, AC-13, AC-17 |
| FR-6 Latency across process restart | AC-14 |
| FR-7 Unusable latency inputs | AC-15 (negative -> 0), AC-16 (unattributable -> skip) |
| FR-8 Failures not counted | AC-3, AC-9, AC-10, AC-11, AC-12 |
| FR-9 Independent of tracing/sampling | AC-17 |
| FR-10 Safe, bounded attribute content | AC-19, AC-20 |
| FR-11 Existing registration path | AC-21 |
| NFR-1 Zero cost when disabled | AC-18 |
| NFR-2 No behavioural change | AC-3, AC-9, AC-10, AC-23 |
| NFR-3 Faults isolated | AC-22 |
| NFR-4 Thread safety | AC-25 |
| NFR-5 TFM compatibility | Verified by the suite building and passing on every target TFM (no dedicated AC) |
| NFR-6 Bounded cardinality | AC-20 |
| NFR-7 Test determinism | AC-13, AC-14, AC-15 (all use an injected `TimeProvider`) |
| NFR-8 Upstream acceptability | AC-19 (convention-conformant keys), AC-21 (meter name), AC-23 (no breakage) |

## Additional Context

**Why now / motivating incident.** Jira PI-36093 (UK): NOFR events were emitted after significant lag, causing `IgnoredOrders` before Partners / OrderPad / JetConnect could "Accept" orders. The Outbox publish lag responsible was invisible — no metric existed to dashboard or alert on, and the unsampled Outbox spans provided nothing after the fact. FR-5/FR-6 exist specifically so that this failure mode is visible next time, including when the lag is attributable to a sweeper in a different process instance.

**Downstream consumer.** Jira RLOI-4186 ("[OrderDispatchWorker] Improve outbox observability") is blocked by this work and will build dashboards and alerts directly on these metrics. That is why FR-4 (sweeper attribution), FR-11 (no new opt-in) and NFR-6 (bounded cardinality) matter: the consuming team needs a ratio they can alert on, available from the registration call they already make, at a cardinality their metrics backend will accept.

**Upstream collaboration.** Metric naming and instrument types need a proposal acceptable to the Brighter maintainers, coordinated in the `#interest-oss-brighter` Slack channel. This document therefore fixes *capabilities and constraints* only; names, instrument types, units and attribute keys belong in the ADR, which should present the proposal and the open questions and be updated when agreement lands (C-7).

**Existing code the ADR will have to reconcile.**
- `src/Paramore.Brighter/OutboxProducerMediator.cs` — `AddToOutbox` / `AddToOutboxAsync`, `StartBatchAddToOutbox` / `EndBatchAddToOutbox` / `EndBatchAddToOutboxAsync`, `ClearOutbox` / `ClearOutboxAsync`, `ClearOutstandingFromOutboxAsync` -> `BackgroundDispatchUsingAsync`, `Dispatch` / `DispatchAsync` / `BulkDispatchAsync`, `HandleAsyncPublishConfirmation` / `ConfigurePublisherCallbackMaybe`, `TripTopic`, and the existing `_timeProvider` / `_tracer` / `_instrumentationOptions` fields.
- `src/Paramore.Brighter/OutboxSweeper.cs` — the sole caller of `ClearOutstandingFromOutboxAsync`, and the natural place for sweeper attribution to originate.
- `src/Paramore.Brighter/Observability/` — `MessagingMeter` / `IAmABrighterMessagingMeter`, `DbMeter` / `IAmABrighterDbMeter`, `BrighterMetricsFromTracesProcessor`, `BrighterSemanticConventions` (including `MeterName == "Paramore.Brighter"`), `BrighterTracer.WriteOutboxEvent` (the add-path event that is dropped when the span is null), and `InstrumentationOptions`.
- `src/Paramore.Brighter.Extensions.Diagnostics/BrighterMetricsBuilderExtensions.cs` and `BrighterTracerBuilderExtensions.cs` — the registration surface that FR-11 pins.
- The publish-confirmation payload (`PublishConfirmationResult`) — carries no creation time today (C-4).

**Test landing zone.** `tests/Paramore.Brighter.Core.Tests/Observability/Metrics/` already holds metric tests (`When_ending_a_send_span_should_still_record_a_client_operation.cs`, `When_ending_a_message_pump_begin_span_should_not_record_a_client_operation.cs`) with doubles in `tests/Paramore.Brighter.Core.Tests/Observability/TestDoubles/` (`SpyMessagingMeter`, `DisabledDbMeter`) — the pattern AC-18 and AC-22 rely on.
