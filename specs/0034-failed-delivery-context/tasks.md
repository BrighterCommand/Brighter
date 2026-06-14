# Tasks — 0034 Failed Delivery Context

Source of truth: ADR 0063 (Accepted) + requirements.md. All file/line references below were verified against the working tree on 2026-06-11. Behavioral tasks use the mandatory `/test-first` TDD format and STOP for IDE approval before implementation. The single structural task uses `/tidy-first`.

**Locked decomposition decisions (PO, 2026-06-11; broker-test classification corrected 2026-06-12):**
1. The public contract break is a **standalone `/tidy-first` structural task, sequenced FIRST** (behavior-preserving; mediator still success-only). A second `/tidy-first` structural task in Phase 1 makes `InMemoryOutboxCircuitBreaker` thread-safe (`Dictionary`→`ConcurrentDictionary`) — a prerequisite for the concurrent `TripTopic` that NFR-3/AC-10 require.
2. The shared mediator behaviors (FR-1..FR-5, AC-10, AC-13) are exercised **in-process via the InMemory confirmation pump (no broker)**, so the pump (Phase 3) is built before the mediator-behavior slices (Phases 4–6) that depend on it.
3. The **Kafka- and RMQ-specific FRs are broker-required integration tests** (FR-6/FR-7/FR-8 Kafka; FR-9 + FR-2-RMQ). The producer/publisher internals they target are not reachable from the public API without a live broker (`KafkaMessagePublisher` is `internal sealed`; `KafkaMessageProducer.PublishResults` is `private`; both build a real Confluent `IProducer` in `Init()`; RMQ's nack arrives only from a broker confirm round-trip). These tests live in the existing gateway test assemblies (`Paramore.Brighter.Kafka.Tests`, `.RMQ.Async.Tests`, `.RMQ.Sync.Tests`), which exist precisely to host broker-backed tests and already carry `[Trait("Category", …)]` + `[Collection(…)]`. We accept that broker-specific behavior needs a broker — that is why these assemblies are separate. No `InternalsVisibleTo` / production seam is introduced (testing.md:73-82).

---

## Phase 1 — Structural (tidy-first)

- [x] **TIDY-FIRST (STRUCTURAL, behavior-preserving): Introduce `PublishConfirmationResult` and flip `OnMessagePublished` to `Action<PublishConfirmationResult>`** — done `98053c6bc`
  - **USE COMMAND**: `/tidy-first` (structural change only — separate it from all behavioral commits; the suite MUST stay green)
  - This is NOT a `/test-first` task: it adds no behavior. The mediator still handles success-only; the new `Topic`/`PublishSpanContext` fields may be populated by producers but are NOT yet consumed by the mediator.
  - **Create** `src/Paramore.Brighter/PublishConfirmationResult.cs`: public sealed record `PublishConfirmationResult(bool Success, Id MessageId, RoutingKey? Topic, ActivityContext? PublishSpanContext)` in namespace `Paramore.Brighter`. Full XML docs on type + every member (public API; ADR "Choosing the enriched shape"). `MessageId` typed `Id` (so `Id.Empty` is the FR-5 marker and implicit `string↔Id` conversions let Kafka/RMQ keep passing raw string ids).
  - **Flip the contract** at `src/Paramore.Brighter/ISupportPublishConfirmation.cs:42`: `event Action<bool, string>` → `event Action<PublishConfirmationResult>`. Update the XML summary (stale `bool =>`/`Guid =>` doc lines).
  - **Migrate every raise site (behavior-preserving — wrap into the record; success raises stay success; `Topic`/`PublishSpanContext` may be `default`/`null` at this stage — the closure/capture rewrites that populate them are behavioral Phases 3/7/8):**
    - Kafka `KafkaMessageProducer.PublishResults` (`:373-374` success, `:381-382` NotPersisted).
    - RMQ.Async `RmqMessageProducer.cs:480` (false) + the success raise in `OnPublishSucceeded` (`:487+`).
    - RMQ.Sync `RmqMessageProducer.cs:235` (false), `:245` (true).
    - InMemory `InMemoryMessageProducer.cs:100, :123, :145`; change the event decl at `:77` from `Action<bool, Id>?` to `Action<PublishConfirmationResult>?` (no external subscribers).
  - **Migrate the mediator's two handlers** to the new delegate shape, behavior-preserving — still success-only:
    - `OutboxProducerMediator.cs:741` `async delegate(bool success, string id)` → `async delegate(PublishConfirmationResult result)`, reading `result.Success`/`result.MessageId`; keep the `MarkDispatchedAsync` success-only body unchanged (`:745-752`).
    - `OutboxProducerMediator.cs:768` `delegate(bool success, string id)` → `delegate(PublishConfirmationResult result)`; keep `MarkDispatched` success-only body unchanged (`:772-777`).
  - **Migrate the in-repo test subscriber sites** to the record shape, asserting the same things they assert today:
    - RMQ.Async: `When_disposing_async_after_sending_should_publish_confirmation.cs:67`, `When_disposing_after_sending_should_publish_confirmation.cs:67`, `When_confirming_posting_a_message_via_the_messaging_gateway_async.cs:54`, `When_confirming_multiple_messages_via_the_messaging_gateway_async.cs:72`, `When_confirming_multiple_messages_via_the_messaging_gateway.cs:73` (Reactor), `When_confirming_posting_a_message_via_the_messaging_gateway.cs:55` (Reactor).
    - RMQ.Sync: `When_confirming_posting_a_message_via_the_messaging_gateway.cs:55`.
    - Kafka: `When_a_message_is_acknowledged_update_offset_async.cs:56`, `When_posting_a_message_async.cs:93`, `When_consumer_assumes_topic_but_missing_async.cs:63`, `When_consumer_assumes_topic_but_missing.cs:66` (Reactor), `When_posting_a_message.cs:117` (Reactor).
  - **Verify, do NOT assume:** `tests/Paramore.Brighter.Core.Tests/Tasks/BrighterSynchronizationContextsTests.cs:162` (`runner.OnMessagePublished += ...`) — confirm whether it binds `ISupportPublishConfirmation`'s event or a custom runner type's own event. Migrate ONLY if it resolves to the changed delegate; otherwise leave untouched and note it does not.
  - **Verification:** full solution builds; entire test suite stays green with no behavioral assertions changed (only the delegate shape at call/subscribe sites). No new behavior, no new tags, no mediator branching.
  - **Depends on**: none (sequenced FIRST — prerequisite for all behavioral slices).
  - **References**: ADR "Choosing the enriched `OnMessagePublished` shape", "Contract before/after" (`ISupportPublishConfirmation.cs:42`); Negative consequence "Source- and binary-breaking"; raise sites Kafka `:373/:381`, RMQ.Async `:480`, RMQ.Sync `:235/:245`, InMemory `:77/:100/:123/:145`; mediator `:741/:768`.

- [x] **TIDY-FIRST (STRUCTURAL, behavior-preserving): make `InMemoryOutboxCircuitBreaker` thread-safe (`Dictionary` → `ConcurrentDictionary`)** — done `4d4dab9cb`
  - **USE COMMAND**: `/tidy-first` (structural change only — separate it from all behavioral commits; the suite MUST stay green)
  - This is NOT a `/test-first` task: single-threaded semantics are identical. It removes a latent data race so the concurrent-`TripTopic` behavior NFR-3/AC-10 require (driven by the Phase 6 AC-10 test) holds. The behavioral assertion lives in Phase 6 (a plain-`Dictionary` corruption is non-deterministic and cannot be reliably unit-asserted; the deterministic, reviewable artifact is this structural swap).
  - **Why needed (grounded):** `InMemoryOutboxCircuitBreaker._trippedTopics` is a plain `Dictionary<RoutingKey, int>` with no lock (`InMemoryOutboxCircuitBreaker.cs:42`); `TripTopic` writes it via indexer (`:70-71`) and `TrippedTopics` reads `.Keys` (`:47`). The Phase 3 pump raises confirmations concurrently via `Task.Run`, so confirmation failures call `TripTopic` concurrently (NFR-3) — concurrent writes to a plain `Dictionary` can corrupt its internal state. The prior tasks/ADR wording wrongly *assumed* the breaker was already safe.
  - **Change**: `private readonly ConcurrentDictionary<RoutingKey, int> _trippedTopics = new();`. `TripTopic` indexer-set stays valid (atomic on `ConcurrentDictionary`). In `CoolDown` (`:55-64`) replace `_trippedTopics.Remove(key)` with `_trippedTopics.TryRemove(key, out _)`; the `foreach` over `.Keys` already takes a snapshot. Add `using System.Collections.Concurrent;`.
  - **Verification:** full solution builds; entire circuit-breaker + outbox test suite stays green with no behavioral assertions changed.
  - **Depends on**: none (structural; may land alongside the contract break).
  - **References**: NFR-3, AC-10; ADR "Thread safety (NFR-3)", Key Components → `IAmAnOutboxCircuitBreaker`; `InMemoryOutboxCircuitBreaker.cs:42/:47/:55-64/:70-71`.

---

## Phase 2 — BrighterTracer confirmation-span helper

- [x] **TEST + IMPLEMENT: BrighterTracer creates a standalone confirmation span (S2) with optional link to the publish span and becomes ambient** — done `cfdeb6d0f`
  - **USE COMMAND**: `/test-first BrighterTracer creates a standalone confirmation span carrying outcome, message id, wire topic, an ActivityLink to the supplied ActivityContext when present, sets Activity.Current to it, and degrades to no link when the context is absent`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Observability"
  - Test file: `When_creating_a_confirmation_span_should_link_and_set_ambient.cs`
  - Test should verify:
    - With a supplied `ActivityContext`, S2 has exactly one `ActivityLink` whose `Context` equals the supplied context; with `null`/`default` context, S2 has no links (AC-2 vs AC-2b).
    - S2 carries message id (or "unknown" marker for empty id), wire topic, and an outcome marker — `error.type` set on the failure branch, success status / no error marker on success.
    - After creation, `Activity.Current` == S2 (so subsequent work nests under it); span kind is `Producer`.
    - S2 is standalone — it does NOT mutate/reopen the linked span; the linked span is unchanged.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add a helper to `src/Paramore.Brighter/Observability/BrighterTracer.cs` near `CreateProducerSpan` (`:641`) / `CreateDbSpan` (`:490`), e.g. `CreateConfirmationSpan(Id id, RoutingKey? topic, bool success, ActivityLink[]? links, InstrumentationOptions options = ...)`.
    - Build tags from `BrighterSemanticConventions` (`MessageId`, `MessagingDestination`, `MessagingOperationType`, outcome marker `ErrorType` on failure). Use `ActivitySource.StartActivity(..., kind: Producer, links: links, ...)` (source already passes `ActivityLink[]?` at `:106/:132`).
    - Set `Activity.Current = <new span>` per the `CreateProducerSpan` (`:700`) / `CreateDbSpan` convention so the success-branch `MarkDispatched` DB span nests under it. Empty id → "unknown" tag value.
  - **Depends on**: Phase 1.
  - **References**: FR-2, AC-2, AC-2b, AC-13; ADR "Key Components → BrighterTracer", Architecture Overview; `BrighterTracer.cs:490/641/700`, `:106/:132`.

---

## Phase 3 — InMemory confirmation pump (the in-process test vehicle)

- [x] **TEST + IMPLEMENT: `UseAsyncPublishConfirmation` defaults off and preserves today's synchronous write + success confirm** — done `1c3441c4f`
  - **USE COMMAND**: `/test-first InMemoryMessageProducer with UseAsyncPublishConfirmation off writes the message to the bus inline and raises a success confirmation synchronously exactly as today`
  - Test location: "tests/Paramore.Brighter.InMemory.Tests/Confirmation"
  - Test file: `When_async_confirmation_is_off_should_write_and_confirm_synchronously.cs`
  - Test should verify:
    - Default `UseAsyncPublishConfirmation == false`.
    - After `Send`/`SendAsync` returns, the message is already in the `InternalBus` (synchronous read-after-send still works) and exactly one `PublishConfirmationResult` with `Success == true` and the message id was raised.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Implement `ISupportPublishConfirmation` on `InMemoryMessageProducer` (event already flipped in Phase 1).
    - Add settable `bool UseAsyncPublishConfirmation { get; set; } = false;` (property-injection style matching `Publication`/`Span`/`Scheduler` at `:62/:69/:72`). When false, keep the existing inline `WriteProducerEvent` + success raise at `:100/:123/:145` (now raising the record).
  - **Depends on**: Phase 1.
  - **References**: ADR "Confirmation-capable in-memory producer → Opt-in switch"; NFR-5 (switch toggles in-memory timing only, not mediator behavior); InMemory `:62/:69/:72/:100/:123/:145`.

- [x] **TEST + IMPLEMENT: `PublishFailurePredicate` injects a failed publish (no bus write, failure confirm)** — done `bdb106104`
  - **USE COMMAND**: `/test-first InMemoryMessageProducer with a PublishFailurePredicate returning true does not write the bus and raises a failure confirmation`
  - Test location: "tests/Paramore.Brighter.InMemory.Tests/Confirmation"
  - Test file: `When_publish_failure_predicate_returns_true_should_raise_failure.cs`
  - Test should verify:
    - With `PublishFailurePredicate => true`, the message is NOT written to the bus and exactly one confirmation with `Success == false` carrying the message id and wire topic is raised.
    - `null` predicate (default) and a predicate returning `false` both behave as success.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add settable `Func<Message, bool>? PublishFailurePredicate { get; set; }` (default `null` = never fail). Evaluate per work-item: `true` ⇒ skip bus write, raise `Success: false`; else write bus, raise `Success: true`.
    - Build the `PublishConfirmationResult` with `Topic = message.Header.Topic`.
  - **Depends on**: Phase 1; previous pump slice (switch).
  - **References**: FR-4 (failed = not delivered), FR-5; ADR "Failure-injection hook"; InMemory raise sites.

- [x] **TEST + IMPLEMENT: async pump (a) — fire-and-forget enqueue, single-reader FIFO bus write, concurrent raise** — done `1a1a84fbf`
  - **USE COMMAND**: `/test-first InMemoryMessageProducer with async confirmation on returns before the bus write, drains a single-reader channel in FIFO enqueue order on one worker writing the bus then raising each confirmation via Task.Run`
  - Test location: "tests/Paramore.Brighter.InMemory.Tests/Confirmation"
  - Test file: `When_async_confirmation_is_on_should_enqueue_and_pump.cs`
  - Test should verify:
    - With `UseAsyncPublishConfirmation == true`, `Send`/`SendAsync` returns BEFORE the message is on the bus (deferred bus visibility); awaiting/polling the confirmation eventually shows the message written and a success confirmation raised.
    - Messages are written to the bus in FIFO enqueue order (deterministic produce order).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - When on, `Send`/`SendAsync` do NOT write inline; capture the work-item (Message + `Id` + `RoutingKey` topic + captured `ActivityContext?`), enqueue onto `Channel.CreateUnbounded<T>(new(){ SingleReader = true, SingleWriter = false })`, return immediately.
    - Start a single long-running worker that drains via `ChannelReader.ReadAllAsync` and per item writes the bus (unless failure-injected), then dispatches the raise via `Task.Run` (mirrors Kafka `KafkaMessageProducer.cs:373/381`). (For THIS slice a straightforward lazy start is sufficient; the concurrent-first-enqueuer single-start guard is the next slice.)
    - Zero/null-delay `SendWithDelay`/`SendWithDelayAsync` delegate to `Send`/`SendAsync` (pump engages); real-delay routes through `Scheduler` (`:72`) and re-enters later.
  - **Depends on**: previous two pump slices.
  - **References**: NFR-3; ADR "Fire-and-forget publish + single-worker pump", "Delayed sends"; Kafka `:373/381`.

- [x] **TEST + IMPLEMENT: async pump (c) — batch `SendAsync(IAmAMessageBatch)` enqueues one work-item per message** — done `a11e35c30`
  - **USE COMMAND**: `/test-first InMemoryMessageProducer with async confirmation on enqueues one pump work-item per message in a batch SendAsync, each producing its own bus write and confirmation`
  - Test location: "tests/Paramore.Brighter.InMemory.Tests/Confirmation"
  - Test file: `When_async_confirmation_is_on_should_fan_out_a_batch.cs`
  - Test should verify:
    - `SendAsync(IAmAMessageBatch)` with N messages enqueues N work-items; awaiting/polling shows N bus writes and N confirmations, FIFO by batch order.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - The batch `SendAsync(IAmAMessageBatch)` path (`:112`) enqueues one work-item per message onto the same channel; the worker handles them identically to single sends.
  - **Depends on**: async pump (a) slice.
  - **References**: ADR "Fire-and-forget publish + single-worker pump" (batch fan-out); InMemory `:112`.

- [x] **TEST + IMPLEMENT: async pump (b) — single-start guard: concurrent first-enqueuers start exactly one worker / one reader** — done `af0501b35`
  - **USE COMMAND**: `/test-first InMemoryMessageProducer with async confirmation on starts exactly one draining worker even when many threads race to be the first enqueuer, so only one reader drains the channel`
  - Test location: "tests/Paramore.Brighter.InMemory.Tests/Confirmation"
  - Test file: `When_concurrent_first_enqueuers_should_start_one_worker.cs`
  - Test should verify:
    - Many threads calling `Send`/`SendAsync` concurrently as the first sends start the worker exactly once (single-start guard); only one reader drains (FIFO/drain bookkeeping intact — no duplicated or dropped bus writes / confirmations).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Gate worker start with `Interlocked.CompareExchange` on a backing `Task?` field (or `LazyInitializer.EnsureInitialized`) so two concurrent first-enqueuers cannot each spin up a reader. `SingleReader = true` is a caller-asserted contract the channel does not enforce; two `ReadAllAsync` readers would break FIFO + drain bookkeeping, so the guard is load-bearing, not an optimisation (ADR).
  - **Depends on**: async pump (a) slice.
  - **References**: NFR-3; ADR "Fire-and-forget publish + single-worker pump" (single-start guard "load-bearing, not an optimisation").

- [x] **TEST + IMPLEMENT: pump captures the publish `ActivityContext` synchronously before enqueue** — done `d264a6cea`
  - **USE COMMAND**: `/test-first InMemoryMessageProducer captures Activity.Current's context inside Send before enqueuing so the confirmation carries the publish span context`
  - Test location: "tests/Paramore.Brighter.InMemory.Tests/Confirmation"
  - Test file: `When_sending_should_capture_publish_context_before_enqueue.cs`
  - Test should verify:
    - With an active `Activity` at send time, the raised `PublishConfirmationResult.PublishSpanContext` equals that activity's `Context`.
    - With no active `Activity`, `PublishSpanContext` is `null`/`default` (degradation).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Read `Activity.Current?.Context` synchronously inside `Send`/`SendAsync`, before enqueuing (the channel hand-off is the boundary past which `Activity.Current` is no longer S1); carry it on the work-item; populate `PublishSpanContext` from it.
  - **Depends on**: async pump (a) slice.
  - **References**: FR-2, AC-2, AC-2b, C-7; ADR "Same capture invariant".

- [x] **TEST + IMPLEMENT: two-stage drain on dispose — no confirmation lost on shutdown** — done `bd715f571`
  - **USE COMMAND**: `/test-first InMemoryMessageProducer Dispose/DisposeAsync completes the writer, awaits the worker, then awaits all in-flight raise tasks so no confirmation is dropped on shutdown`
  - Test location: "tests/Paramore.Brighter.InMemory.Tests/Confirmation"
  - Test file: `When_disposing_with_pending_confirmations_should_drain_all.cs`
  - Test should verify:
    - Enqueue N items, then `Dispose`/`DisposeAsync`: all N confirmations are observed (none dropped), including raises spawned right before shutdown.
    - When the switch was never turned on, dispose stays a no-op (no worker started) — matching `:82/:87` today.
    - The already-quiescent case (all raises decremented before writer completed) does not block forever.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - On dispose: `writer.Complete()`, await the worker, then await the in-flight raise tasks by collecting the raise `Task` handles into a thread-safe bag and `await Task.WhenAll(bag)` (immune to the increment-ordering and quiescent-transition hazards). This is the committed mechanism for this slice; the `Interlocked` count + TCS alternative (count-before-spawn + quiescent re-check) is documented in ADR 0063 "Lifecycle / draining" but is NOT the chosen approach here.
    - Wrap each raise so a throwing callback still runs its drain bookkeeping (the raise `Task` still completes and is awaited) and cannot kill the worker or strand the drain (NFR-4).
  - **Depends on**: async pump (a) slice.
  - **References**: NFR-3, NFR-4; ADR "Lifecycle / draining" rules (i) count-before-spawn, (ii) re-check-zero, preferred `Task.WhenAll`; InMemory `:82/:87`.

---

## Phase 4 — Shared mediator FAILURE branch (tested in-process via InMemory pump)

> All Phase 4 slices: the mediator callback creates S2 FIRST on every invocation (via the Phase 2 helper), sets `Activity.Current = S2`, then branches on `result.Success`. Rewrite both `ConfigureAsyncPublisherCallbackMaybe` (`:741`) and `ConfigurePublisherCallbackMaybe` (`:768`).

- [x] **TEST + IMPLEMENT: confirmation failure logs at Warning with id and wire topic (FR-1)**
  - **USE COMMAND**: `/test-first the mediator logs a confirmation failure at Warning including the message id and the wire topic and never at Error or above`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Confirmation"
  - Test file: `When_a_confirmation_fails_should_log_warning_with_id_and_topic.cs`
  - Test should verify:
    - Exactly one Warning log on a failure confirmation, containing the message id and `message.Header.Topic`; no Error/Critical (AC-11).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In both handlers' failure branch add a Warning log (new source-generated `Log` message) with id + `result.Topic`.
  - **Depends on**: Phase 2; Phase 3 (failure-injection + pump).
  - **References**: FR-1, NFR-1, AC-1, AC-11; mediator `:741/:768`.

- [x] **TEST + IMPLEMENT: confirmation (success and failure) emits S2 linked to the publish span, S1 unmutated (FR-2)**
  - **USE COMMAND**: `/test-first every confirmation emits a standalone confirmation span linked to the original publish span without mutating it, degrading to no link when the publish context is absent`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Confirmation"
  - Test file: `When_a_confirmation_is_received_should_emit_linked_span.cs`
  - Test should verify:
    - On both success and failure, S2 is emitted with an `ActivityLink` whose context equals S1's context (AC-2); S1 is not mutated/reopened; S2 is not attributed to an unrelated ambient activity.
    - With no captured context, S2 still emitted with no link (AC-2b).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In both handlers, build `links = result.PublishSpanContext is { } ctx ? new[]{ new ActivityLink(ctx) } : null` and call the Phase 2 helper FIRST on every invocation; `using` so it starts+stops synchronously in-callback (NFR-2).
  - **Depends on**: Phase 2; Phase 3 (context capture + pump).
  - **References**: FR-2, AC-2, AC-2b, NFR-2; mediator `:741/:768`.

- [x] **TEST + IMPLEMENT: failure trips the breaker on the wire topic (FR-3)**
  - **USE COMMAND**: `/test-first a confirmation failure trips the circuit breaker using the wire topic message.Header.Topic with exact parity to the non-confirmation send-failure path`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Confirmation"
  - Test file: `When_a_confirmation_fails_should_trip_topic_on_wire_topic.cs`
  - Test should verify:
    - Failure calls `TripTopic(result.Topic)` with the wire topic, NOT `Publication.Topic`.
    - Reply/rewritten-topic message trips the rewritten wire address (AC-3b).
    - Null/empty topic is a safe no-op (TripTopic guard `:1170`).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In the failure branch call the existing private `TripTopic(RoutingKey?)` (`:1168`) with `result.Topic`, mirroring `:998`.
  - **Depends on**: Phase 2, Phase 3.
  - **References**: FR-3, NFR-6, AC-3, AC-3b, C-8; mediator `:1168/:1170`, parity `:998`.

- [x] **TEST + IMPLEMENT: failure does not mark dispatched, does not bubble, message stays Sweeper-eligible (FR-4)**
  - **USE COMMAND**: `/test-first a confirmation failure does not mark the message dispatched, does not throw, and adds no awaited broker or outbox call`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Confirmation"
  - Test file: `When_a_confirmation_fails_should_not_dispatch_or_bubble.cs`
  - Test should verify:
    - No `MarkDispatched`/`MarkDispatchedAsync` on failure; message remains un-dispatched (Sweeper-eligible); no exception escapes the callback (AC-4, AC-12b).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Failure branch performs only Warning log + `TripTopic` (no Outbox/broker await, no `MarkDispatched`).
  - **Depends on**: Phase 4 FR-1/FR-3 slices.
  - **References**: FR-4, AC-4, AC-12b, OOS-1; mediator `:741/:768`.

- [x] **TEST + IMPLEMENT: empty/null id — FR-1/2/3 still occur with an explicit unknown marker, no crash (FR-5)**
  - **USE COMMAND**: `/test-first a confirmation failure with an empty message id still logs at Warning, emits the span, and trips the topic, recording the id as an explicit unknown marker without crashing`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Confirmation"
  - Test file: `When_a_confirmation_fails_with_empty_id_should_still_observe.cs`
  - Test should verify:
    - With `Id.Empty`, Warning log + S2 + `TripTopic(topic)` all still occur; id recorded as explicit empty/"unknown" marker; no exception (AC-5).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Ensure the failure branch treats `Id.Empty` as the unknown marker for the log/span and still trips on the (present) wire topic.
  - **Depends on**: Phase 4 FR-1/FR-2/FR-3 slices; Phase 3 failure-injection.
  - **References**: FR-5, AC-5; ADR "Degradation path".

---

## Phase 5 — Shared mediator SUCCESS branch (symmetric span + orphan fix)

- [x] **TEST + IMPLEMENT: success emits S2 (linked) and nests MarkDispatched under it; no Warning, no breaker (FR-2 success / AC-13 / C-6 orphan fix)**
  - **USE COMMAND**: `/test-first a successful confirmation keeps the sent log and mark-dispatched, emits a linked confirmation span, nests the MarkDispatched DB span under it, and never warns or trips the breaker`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Confirmation"
  - Test file: `When_a_confirmation_succeeds_should_nest_dispatch_under_span.cs`
  - Test should verify:
    - On success: existing Info "sent" log preserved and `MarkDispatched(Async)` called (delivery semantics frozen).
    - S2 emitted with link to S1; the `MarkDispatched` DB span's **parent id equals S2's id** (no longer an orphaned root span — C-6 fix). Assert on the parent id specifically — NOT merely that *some* parent exists — so the test pins the explicit re-parenting and would fail if the DB span reparented elsewhere.
    - No Warning, no `TripTopic` on success (AC-13).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - **Explicitly re-parent the DB span to S2** — do NOT rely on the null-parent ambient fallback. `CreateDbSpan` (`BrighterTracer.cs:490`) parents from the explicitly-passed `requestContext.Span` (`parentId = parentActivity?.Id`, `:496`), read **synchronously at `MarkDispatched` entry** before any await (e.g. `RelationDatabaseOutbox.AddAsync` builds the span at the top). So in the success branch build a **per-callback `RequestContext` copy** (`requestContext.CreateCopy()`) and set its `Span = S2`, then pass that copy to `MarkDispatched`/`MarkDispatchedAsync` (`:749/:776`) and to the resilience wrapper. `CreateDbSpan` then parents the DB span to S2 by its explicit id.
    - Use a **copy**, not the shared construction-time `requestContext`: `RequestContext.Span` is thread-keyed (`RequestContext.cs:113-125`) and its setter **ignores `null`** (cannot unset), so mutating the shared, reused context would leave a lingering thread-keyed entry. Thread-keying also means concurrent same-producer success confirmations each set their own S2 with no cross-talk.
    - Keep `Activity.Current = S2` (set by the Phase 2 helper) for ambient hygiene, but the DB-span parent is now explicit, not contingent on `requestContext.Span` being null. Keep the "sent" log (`Log.SentMessage`) and the mark-dispatched body otherwise unchanged.
  - **Depends on**: Phase 2; Phase 4 FR-2 slice; Phase 3 (success-path pump).
  - **References**: FR-2 (success), AC-13, OOS-4 (delivery frozen), C-6; mediator `:745/:749/:772/:776`; `BrighterTracer.cs:490/:496` (CreateDbSpan parents from passed `parentActivity`); `RelationDatabaseOutbox.cs:201-204` (reads `requestContext?.Span` synchronously at entry); `RequestContext.cs:113-125` (thread-keyed `Span`, setter ignores null, `CreateCopy`); ADR "Re-parent the `MarkDispatched` DB span EXPLICITLY".

---

## Phase 6 — Error isolation + concurrency

- [x] **TEST + IMPLEMENT: observability throw is isolated; breaker trip + log still happen (NFR-4 / AC-14)**
  - **USE COMMAND**: `/test-first when the observability work throws in the confirmation callback the exception is caught and logged and does not escape, while the breaker trip still runs`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Confirmation"
  - Test file: `When_observability_throws_should_isolate_and_still_trip.cs`
  - Test should verify:
    - An induced throw in the span/observability path is swallowed (callback does not bubble; producer thread not destabilised); the failure-branch breaker trip + Warning are still observed.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Wrap the observability block in both handlers in try/catch that logs and swallows; order/guard so `TripTopic` (FR-3) still runs.
  - **Depends on**: Phase 4 (failure branch).
  - **References**: NFR-4, AC-14; ADR "Error isolation".

- [x] **TEST + IMPLEMENT: concurrent same-topic failures — no race loss, topic ends tripped (NFR-3 / AC-10)**
  - **USE COMMAND**: `/test-first concurrent same-topic confirmation failures each warn and trip without race-induced loss and the topic ends tripped, with overlap forced by a synchronization gate`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Confirmation"
  - Test file: `When_concurrent_same_topic_confirmations_fail_should_not_lose_trips.cs`
  - Test should verify:
    - Multiple concurrent failure confirmations for one topic each produce a Warning + trip; no loss; topic ends tripped (order-independent end state).
    - Overlap is FORCED via a `Barrier`/`ManualResetEventSlim` released inside the injected callback or `PublishFailurePredicate` (not reliant on the scheduler interleaving short raises).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - No new mediator code expected. Breaker thread-safety is supplied by the Phase 1 `ConcurrentDictionary` structural task (the prior "breaker safe under concurrent `TripTopic`" was an unverified assumption — it is now an explicit prerequisite, not a given). Remaining thread-safe collaborators: source-gen logger, `ActivitySource.StartActivity`. The InMemory pump's `Task.Run`-per-raise (Phase 3) supplies the concurrency; the test's gate forces overlap.
  - **Depends on**: Phase 1 breaker `ConcurrentDictionary` structural task; Phase 4 (failure branch); Phase 3 async pump.
  - **References**: NFR-3, AC-10; ADR "Concurrency scope", "Thread safety"; `InMemoryOutboxCircuitBreaker.cs:42/:70-71`.

---

## Phase 7 — Kafka slices (broker-required integration tests)

> All Phase 7 tests are **broker-required integration tests** in `Paramore.Brighter.Kafka.Tests` (the internals — `internal sealed KafkaMessagePublisher`, `private PublishResults`, real Confluent `IProducer` built in `Init()` — are not reachable without a live broker; no `InternalsVisibleTo`/seam is introduced). Place under `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Proactor` (async) / `…/Reactor` (sync), and decorate each class `[Trait("Category", "Kafka")]` + `[Collection("Kafka")]`, matching the existing producer tests (e.g. `Proactor/When_posting_a_message_async.cs`). **Failure induction:** drive the `ProduceException` → synthetic `NotPersisted` path by configuring a small client-side `MessageMaxBytes` (or low topic `max.message.bytes`) and producing an oversized message — the Confluent client throws `ProduceException<string, byte[]>` with `Error.Code = MsgSizeTooLarge` / reason "Message size too large", exactly the FR-6/AC-6 example. (`run-tests` / `test-infra` brings up the Kafka container.)

- [x] **TEST + IMPLEMENT: Kafka publisher logs the swallowed ProduceException at Warning and preserves the synthetic NotPersisted flow (FR-6 / FR-7)**
  - **USE COMMAND**: `/test-first against a live Kafka broker an oversized produce raises a ProduceException whose reason and code are logged at Warning while the synthetic NotPersisted delivery result still reaches the callback`
  - Test location: "tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Proactor"
  - Test file: `When_an_async_produce_throws_should_warn_and_synthesize_not_persisted.cs`
  - **Broker-required** (`[Trait("Category","Kafka")]` + `[Collection("Kafka")]`).
  - Test should verify:
    - Producing an oversized message to the broker yields a Warning containing the broker `Error.Reason` + `Error.Code` (NFR-1), and the synthetic `NotPersisted` `DeliveryResult` (with `MESSAGE_ID` in `deliveryResult.Headers`) is still routed to the callback (no bubble to the caller) (AC-6).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - At `KafkaMessagePublisher.cs:52` bind the catch var: `catch (ProduceException<string, byte[]> pe)`; add a **new** `LoggerMessage` at **`LogLevel.Warning`** (NFR-1) with reason+code. Do NOT reuse the existing **Error**-level `FatalProducerError`/`NonFatalProducerError` (`KafkaMessageProducer.cs:389/392`) — folding the confirmation-failure log into an Error-level message would violate NFR-1/AC-11. Leave the synthetic `NotPersisted` body (`:56-61`) + `deliveryReport(...)` unchanged (FR-7). Leave the wrong-typed `<string,string>` catches at `KafkaMessageProducer.cs:262/336` untouched (OOS-2).
  - **Depends on**: Phase 1.
  - **References**: FR-6, FR-7, AC-6, NFR-1, OOS-2; `KafkaMessagePublisher.cs:52-62`.

- [x] **TEST + IMPLEMENT: Kafka NotPersisted reads MESSAGE_ID from report-level headers and raises failure with the id (FR-8 / AC-7 / AC-8)**
  - **USE COMMAND**: `/test-first against a live Kafka broker a NotPersisted confirmation reads MESSAGE_ID from the report-level headers and raises a failure confirmation carrying that id`
  - Test location: "tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Proactor"
  - Test file: `When_publish_results_not_persisted_should_raise_failure_with_id.cs`
  - **Broker-required** (`[Trait("Category","Kafka")]` + `[Collection("Kafka")]`). Reach `PublishResults`'s `NotPersisted` branch via the same oversized-message induction as the FR-6 slice (the only public route to that private branch); subscribe to `OnMessagePublished` and assert on the raised `PublishConfirmationResult`.
  - Test should verify:
    - `NotPersisted` with `MESSAGE_ID` present in the report-level `headers` raises `Success == false` with that id (AC-7), NOT from `result.Message.Headers`.
    - `MESSAGE_ID` absent → id falls back to empty (AC-8).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `PublishResults` (`KafkaMessageProducer.cs:381-383`), on the `NotPersisted` fall-through read `MESSAGE_ID` from the report-level `headers` collection (same one the success branch uses at `:368`), fall back to empty; raise the failure record with that id.
  - **Depends on**: Phase 1.
  - **References**: FR-8, AC-7, AC-8; `KafkaMessageProducer.cs:364-384`; ADR FR-8 risk/mitigation.

- [x] **TEST + IMPLEMENT: Kafka closure rewrite — confirmation carries `message.Header.Topic`, `message.Id`, and the captured publish context (FR-2 Kafka data path)**
  - **USE COMMAND**: `/test-first against a live Kafka broker the confirmation carries the wire topic from message.Header.Topic, the id, and the captured publish context even on the synthetic NotPersisted path`
  - Test location: "tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Proactor"
  - Test file: `When_a_kafka_confirmation_fires_should_carry_topic_and_link_from_message.cs`
  - **Broker-required** (`[Trait("Category","Kafka")]` + `[Collection("Kafka")]`). Drive the synthetic `NotPersisted` path via the oversized-message induction; start an `Activity` before the produce so the captured context is non-default.
  - Test should verify:
    - The raised `PublishConfirmationResult.Topic` equals `message.Header.Topic` (NOT `report.Topic`) — specifically on the synthetic `NotPersisted` path where the `DeliveryResult.Topic` is never set (the failure path this spec exists to observe).
    - With an active publish `Activity` at send, `PublishSpanContext` equals its context; with none, it is null (degrade).
    - This is the Kafka-specific link/topic assertion the in-memory AC-2/AC-2b/AC-3b do NOT exercise.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Capture `Activity.Current?.Context` into a local at the top of `SendWithDelay`/`SendWithDelayAsync` (before `_publisher.PublishMessage(Async)`).
    - Rewrite the delivery-report closures at `KafkaMessageProducer.cs:260` (sync) and `:333` (async) to close over `message` and the captured context, building the enriched result from `message.Header.Topic` + `message.Id` + captured context; widen `PublishResults` to receive `(RoutingKey topic, ActivityContext? publishContext)` (or build the result inline and hand to a thin raise helper). Keep the `Task.Run` marshalling (`:373/381`).
    - Do NOT source topic from `report.Topic` (empty on the synthetic `NotPersisted` case).
  - **Depends on**: Phase 1; Kafka FR-8 slice.
  - **References**: FR-2 (Kafka), C-7, C-8, AC-2/AC-2b/AC-3b (Kafka analogue); ADR "Kafka FR-8 + FR-2 (data path)", "Capture-ordering (Kafka)", topic-source risk; `KafkaMessageProducer.cs:260/333/364/373/381`.

---

## Phase 8 — RMQ slices (broker-required integration tests)

> All Phase 8 tests are **broker-required integration tests** in the RMQ gateway assemblies; the nack/ack confirmation handlers fire only from a real broker confirm round-trip and the producer is not seam-injectable. Place under `…/MessagingGateway/Proactor` (Async) / `…/Reactor` (Sync) and decorate each class `[Trait("Category", "RMQ")]` (matching the existing `RmqMessageProducerConfirmations…` tests; RMQ tests carry the Trait but no `[Collection]`). The `test-infra`/`run-tests` harness brings up the RabbitMQ container.
>
> **Confirmation-data verification strategy (honest):** FR-2's enrichment (`Id` + `message.Header.Topic` + captured `ActivityContext`) is populated **identically** on both `OnPublishSucceeded` and `OnPublishFailed` (the `PendingConfirmation` lookup feeds both). The **ack path is deterministically reachable** against a real broker (publish → ack), so the enriched `PublishConfirmationResult` is asserted there — extending the existing `RmqMessageProducerConfirmations…` ack tests. Forcing a real broker **nack** is non-deterministic; the mediator-level *failure* behavior (FR-1/FR-3/FR-4 on `Success == false`) is already covered in-process via the InMemory pump (Phase 4). These RMQ slices therefore verify the **producer-side enrichment** (the listable RMQ code change) on the reachable confirmation path; if a deterministic broker-nack fixture proves feasible, assert the nack raise directly too. **Each test MUST carry an explicit comment** stating that the failure-branch (nack) enrichment is verified indirectly — the same `PendingConfirmation` populates both `OnPublishSucceeded` and `OnPublishFailed`, and only the ack path is deterministically reachable — so a future reader knows the nack raise itself is not directly asserted here.

- [x] **TEST + IMPLEMENT: RMQ.Async — `PendingConfirmation` record carries id/topic/context into the enriched confirmation raise (FR-9 / FR-2)**
  - **USE COMMAND**: `/test-first against a live RabbitMQ broker RmqMessageProducer (Async) captures the publish context and wire topic at the top of Send, tracks them in a PendingConfirmation keyed by delivery tag, and raises an enriched confirmation carrying the id, topic, and context`
  - Test location: "tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway/Proactor"
  - Test file: `When_a_confirmation_is_received_should_carry_id_topic_and_context.cs`
  - **Broker-required** (`[Trait("Category","RMQ")]`).
  - Test should verify:
    - On the broker confirmation, the raised `PublishConfirmationResult` carries the message id (AC-9), `message.Header.Topic`, and the captured `ActivityContext` (link present when an `Activity` was active at send; null otherwise) — asserted on the deterministically-reachable ack path; the same `PendingConfirmation` feeds the nack raise.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `internal readonly record struct PendingConfirmation(Id MessageId, RoutingKey Topic, ActivityContext? Context)` (internal — no public-API/XML-doc obligation).
    - Change `_pendingConfirmations` from `Dictionary<ulong, string>` (`:60`) to `Dictionary<ulong, PendingConfirmation>`; update `AddPendingConfirmation` (`:397`), `RemovePendingConfirmations` (`:419`)/`RemoveConfirmationsLocked` (`:440`, lookup `:447`), `OnPublishFailed` (`:476-482`) and `OnPublishSucceeded` (`:487+`).
    - Capture `Activity.Current?.Context` at the very top of `SendWithDelay(Async)` — above `BeginSend()`/`EnsureBrokerAsync` (the `:167` await) — and stash with topic in `AddPendingConfirmation` (`:187`). Build the enriched result in `OnPublishFailed`/`OnPublishSucceeded`. No `Send`-signature change.
  - **Depends on**: Phase 1.
  - **References**: FR-9, FR-2 (RMQ not verify-only), AC-9, C-7, C-8; ADR "RMQ FR-9 + FR-2"; RMQ.Async `:60/:167/:187/:397/:419/:440/:447/:476/:480/:487`.

- [ ] **TEST + IMPLEMENT: RMQ.Sync — same `PendingConfirmation` enrichment on the confirmation path (FR-9 / FR-2)**
  - **USE COMMAND**: `/test-first against a live RabbitMQ broker RmqMessageProducer (Sync) captures the publish context and wire topic at send, tracks them per delivery tag, and raises an enriched confirmation carrying id, topic, and context`
  - Test location: "tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/Reactor"
  - Test file: `When_a_confirmation_is_received_should_carry_id_topic_and_context.cs`
  - **Broker-required** (`[Trait("Category","RMQ")]`).
  - Test should verify:
    - On the broker confirmation, the raised result carries id (AC-9), `message.Header.Topic`, and captured context (or null degrade) — asserted on the reachable ack path; the same `PendingConfirmation` feeds the nack raise.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Mirror the Async change: `ConcurrentDictionary<ulong, string>` (`:58`) → `ConcurrentDictionary<ulong, PendingConfirmation>`; update the add at `:153`, and the nack/ack handlers at `:233-236` / `:243-246`. Capture `Activity.Current?.Context` at top of `Send`/`SendWithDelay`. No `Send`-signature change.
  - **Depends on**: Phase 1; RMQ.Async slice (shared record shape).
  - **References**: FR-9, FR-2 (RMQ), AC-9, C-7, C-8; RMQ.Sync `:58/:153/:233/:243`.

---

## Phase 9 — Documentation

- [ ] **DOC: XML docs for new/changed public surface + operator-facing notes**
  - (documentation task — no `/test-first`; verify build/XML-doc warnings stay clean)
  - Work:
    - Confirm `PublishConfirmationResult` carries complete XML docs on type + all four members (delivered in Phase 1; verify nothing regressed).
    - XML-doc the new InMemory public members: `UseAsyncPublishConfirmation` and `PublishFailurePredicate` (note: opt-in, test/local-dev only; with the switch on, `Send` returns before the bus write — deferred bus visibility, so tests must await/poll, not read immediately).
    - Add an operator note: the InMemory pump is opt-in/test-only (does NOT toggle mediator observability/breaker behavior — NFR-5/OOS-3); and the Kafka topic-source trap (wire topic must come from `message.Header.Topic`, never `report.Topic`, because the synthetic `NotPersisted` `DeliveryResult` never sets `.Topic`).
    - Release-notes call-out: `OnMessagePublished` delegate change is source- and binary-breaking for `ISupportPublishConfirmation` implementors.
  - **Depends on**: Phases 1, 3, 7.
  - **References**: ADR Negative consequences (public type to document; breaking change), "Deferred bus visibility", FR-8 topic-source risk; NFR-5/OOS-3.

---

## Coverage cross-reference

### FR → task

| FR | Task(s) |
|----|---------|
| FR-1 (Warning + id + wire topic) | Phase 4 "logs at Warning with id and topic" |
| FR-2 (S2 + link, both branches, degrade) | Phase 2 (helper); Phase 4 "emit linked span"; Phase 5 (success S2); Phase 7 "Kafka closure rewrite"; Phase 8 (RMQ Async + Sync) |
| FR-3 (TripTopic wire topic) | Phase 4 "trip topic on wire topic" |
| FR-4 (no dispatch / no bubble) | Phase 4 "not dispatch or bubble" |
| FR-5 (empty id) | Phase 4 "empty id should still observe" |
| FR-6 (Kafka log ProduceException) | Phase 7 "warn and synthesize not persisted" |
| FR-7 (preserve synthetic NotPersisted) | Phase 7 "warn and synthesize not persisted" |
| FR-8 (Kafka MESSAGE_ID from report headers) | Phase 7 "not persisted should raise failure with id" |
| FR-9 (RMQ id verify + FR-2 capture) | Phase 8 (RMQ Async + Sync) |

### NFR → task

| NFR | Task(s) |
|-----|---------|
| NFR-1 (Warning never Error+) | Phase 4 FR-1; Phase 7 FR-6 |
| NFR-2 (non-blocking; S2 sync in-callback) | Phase 2 (ambient/sync helper); Phase 4 FR-2; Phase 4 FR-4 (no extra await) |
| NFR-3 (thread safety) | Phase 1 breaker `ConcurrentDictionary` structural task; Phase 3 async pump + drain; Phase 6 AC-10 concurrency |
| NFR-4 (observability throw isolated) | Phase 6 "observability throws should isolate"; Phase 3 drain (raise wrapped) |
| NFR-5 (always-on; InMemory switch ≠ mediator toggle) | Phase 3 switch slice (default off, preserves behavior); Phase 9 DOC note |
| NFR-6 (breaker trip wire-topic parity) | Phase 4 "trip topic on wire topic" (parity with `:998`) |

### ADR 0063 decision → task

| ADR decision | Task(s) |
|--------------|---------|
| Contract enrichment (`PublishConfirmationResult` + delegate flip + raise/subscriber migration) | Phase 1 (tidy-first) |
| Breaker thread-safety under concurrent `TripTopic` (NFR-3) — `InMemoryOutboxCircuitBreaker` `Dictionary`→`ConcurrentDictionary` | Phase 1 (tidy-first, breaker) |
| Producer self-sources span from `Activity.Current` (no `Send` sig change) | Phase 3 (InMemory capture), Phase 7 (Kafka capture), Phase 8 (RMQ capture) |
| BrighterTracer confirmation-span helper (outcome flag, link, sets `Activity.Current`) | Phase 2 |
| Mediator both-branch S2 first + branch | Phase 4 (failure), Phase 5 (success) |
| Orphan-fix: MarkDispatched nests under S2 | Phase 5 |
| TripTopic wire-topic | Phase 4 "trip topic on wire topic" |
| Kafka FR-6 logging | Phase 7 FR-6/FR-7 |
| Kafka FR-8 id propagation | Phase 7 FR-8 |
| Kafka closure rewrite / data path + topic-source trap | Phase 7 "closure rewrite" |
| RMQ `PendingConfirmation` internal record + raise enrichment | Phase 8 (Async + Sync) |
| InMemory pump (switch + failure hook + Channel/single-worker + Task.Run raise) | Phase 3 (switch, failure-injection, async pump) |
| InMemory two-stage drain + single-start guard | Phase 3 (pump single-start guard; drain slice) |
| InMemory capture before enqueue | Phase 3 "capture publish context before enqueue" |
| Error isolation (NFR-4 / AC-14) | Phase 6 "observability throws"; Phase 3 drain (raise wrapped) |
| Concurrency (NFR-3 / AC-10) with forced overlap | Phase 6 AC-10 |
| New public type XML docs / release notes | Phase 1 (docs authored), Phase 9 (verify + operator notes) |

### AC → task (load-bearing ACs)

AC-1→P4 FR-1; AC-2/AC-2b→P2 + P4 FR-2 + P7 (Kafka) + P8 (RMQ); AC-3/AC-3b→P4 FR-3; AC-4→P4 FR-4; AC-5→P4 FR-5; AC-6→P7 FR-6/7; AC-7/AC-8→P7 FR-8; AC-9→P8; AC-10→P6 concurrency; AC-11→P4 FR-1; AC-12/AC-12b→P2 + P4 FR-2/FR-4; AC-13→P5; AC-14→P6 isolation.

### Gaps / scope-creep flags

- **No FR/NFR gaps.** Every FR-1..FR-9 and NFR-1..NFR-6 maps to at least one task. (NFR-3's breaker requirement is now an explicit Phase 1 structural task, not an unverified assumption — see below.)
- **No ADR-decision gaps.** Every Decision/Implementation-Approach mechanism maps to a task.
- **No scope creep.** Every task traces to an FR/NFR/AC or an explicit ADR decision. Phase 9 (DOC) traces to the ADR's stated public-API XML-doc / release-note / operator-note obligations and NFR-5/OOS-3.
- **Note (not a gap):** `BrighterSynchronizationContextsTests.cs:162` is listed in Phase 1 as **verify-then-migrate-only-if-it-binds** — it may be a custom runner's own event rather than `ISupportPublishConfirmation.OnMessagePublished`; the implementer must confirm before touching it.
- **Breaker thread-safety (corrected 2026-06-12).** `InMemoryOutboxCircuitBreaker` currently backs `_trippedTopics` with a plain unlocked `Dictionary` (`:42`); NFR-3/AC-10 require `TripTopic` to be safe under the concurrent raises the Phase 3 pump produces. This is now an explicit Phase 1 `/tidy-first` structural task (`Dictionary`→`ConcurrentDictionary`), with the behavioral assertion in Phase 6 AC-10. (Previously the tasks/ADR wrongly assumed the breaker was already thread-safe.)
- **Broker-test classification (corrected 2026-06-12).** Phase 7 (Kafka FR-6/7/8/FR-2-Kafka) and Phase 8 (RMQ FR-9/FR-2) are **broker-required integration tests** in their gateway test assemblies — the producer/publisher internals are not reachable without a live broker and no `InternalsVisibleTo`/seam is introduced (testing.md:73-82). The shared mediator behaviors (FR-1..FR-5, AC-10, AC-13) remain broker-free, exercised in-process via the InMemory pump (Phases 3–6). The earlier "tested unit-level where possible / no other task needs broker infrastructure" framing was inaccurate and has been removed.

### Critical Files for Implementation
- `src/Paramore.Brighter/OutboxProducerMediator.cs` (callback handlers `:741`/`:768`, `TripTopic :1168`, parity `:998`)
- `src/Paramore.Brighter/CircuitBreaker/InMemoryOutboxCircuitBreaker.cs` (`:42`/`:47`/`:55-64`/`:70-71` — `Dictionary`→`ConcurrentDictionary` for NFR-3/AC-10)
- `src/Paramore.Brighter/Observability/BrighterTracer.cs` (`:490`/`:641`/`:700` — new confirmation-span helper)
- `src/Paramore.Brighter/InMemoryMessageProducer.cs` (`:77`/`:100`/`:123`/`:145`/`:82`/`:87` — pump + switch + drain)
- `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessageProducer.cs` (`:260`/`:333`/`:364-384`) + `KafkaMessagePublisher.cs` (`:52-62`)
- `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageProducer.cs` (`:60`/`:397`/`:419`/`:476`) + `.RMQ.Sync/RmqMessageProducer.cs` (`:58`/`:153`/`:233`)
