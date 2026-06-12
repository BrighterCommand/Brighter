# Tasks — 0034 Failed Delivery Context

Source of truth: ADR 0063 (Accepted) + requirements.md. All file/line references below were verified against the working tree on 2026-06-11. Behavioral tasks use the mandatory `/test-first` TDD format and STOP for IDE approval before implementation. The single structural task uses `/tidy-first`.

**Two locked decomposition decisions (PO, 2026-06-11):**
1. The public contract break is a **standalone `/tidy-first` structural task, sequenced FIRST** (behavior-preserving; mediator still success-only).
2. Kafka/RMQ-specific FRs are tested **unit-level where possible** (no broker); the shared mediator behaviors (FR-1..FR-5, AC-10, AC-13) are exercised **in-process via the InMemory confirmation pump**, so the pump (Phase 3) is built before the mediator-behavior slices (Phases 4–6) that depend on it.

---

## Phase 1 — Structural (tidy-first): contract break

- [ ] **TIDY-FIRST (STRUCTURAL, behavior-preserving): Introduce `PublishConfirmationResult` and flip `OnMessagePublished` to `Action<PublishConfirmationResult>`**
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

---

## Phase 2 — BrighterTracer confirmation-span helper

- [ ] **TEST + IMPLEMENT: BrighterTracer creates a standalone confirmation span (S2) with optional link to the publish span and becomes ambient**
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

- [ ] **TEST + IMPLEMENT: `UseAsyncPublishConfirmation` defaults off and preserves today's synchronous write + success confirm**
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

- [ ] **TEST + IMPLEMENT: `PublishFailurePredicate` injects a failed publish (no bus write, failure confirm)**
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

- [ ] **TEST + IMPLEMENT: async pump — fire-and-forget enqueue, single-worker FIFO bus write, concurrent raise, single-start guard**
  - **USE COMMAND**: `/test-first InMemoryMessageProducer with async confirmation on returns before the bus write, drains a single-reader channel in FIFO order on one lazily-and-once-started worker, and dispatches each confirmation raise via Task.Run`
  - Test location: "tests/Paramore.Brighter.InMemory.Tests/Confirmation"
  - Test file: `When_async_confirmation_is_on_should_enqueue_and_pump.cs`
  - Test should verify:
    - With `UseAsyncPublishConfirmation == true`, `Send`/`SendAsync` returns BEFORE the message is on the bus (deferred bus visibility); awaiting/polling the confirmation eventually shows the message written and a success confirmation raised.
    - Messages are written to the bus in FIFO enqueue order (deterministic produce order); batch `SendAsync(IAmAMessageBatch)` enqueues one work-item per message.
    - Concurrent first-enqueuers start exactly one worker (single-start guard); only one reader drains.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - When on, `Send`/`SendAsync` do NOT write inline; capture the work-item (Message + `Id` + `RoutingKey` topic + captured `ActivityContext?`), enqueue onto `Channel.CreateUnbounded<T>(new(){ SingleReader = true, SingleWriter = false })`, return immediately.
    - Lazily start a single long-running worker via `Interlocked.CompareExchange` on a backing `Task?` field (or `LazyInitializer.EnsureInitialized`); worker drains via `ChannelReader.ReadAllAsync` and per item writes the bus (unless failure-injected), then dispatches the raise via `Task.Run` (mirrors Kafka `KafkaMessageProducer.cs:373/381`).
    - Zero/null-delay `SendWithDelay`/`SendWithDelayAsync` delegate to `Send`/`SendAsync` (pump engages); real-delay routes through `Scheduler` (`:72`) and re-enters later.
  - **Depends on**: previous two pump slices.
  - **References**: NFR-3; ADR "Fire-and-forget publish + single-worker pump", "Delayed sends"; Kafka `:373/381`.

- [ ] **TEST + IMPLEMENT: pump captures the publish `ActivityContext` synchronously before enqueue**
  - **USE COMMAND**: `/test-first InMemoryMessageProducer captures Activity.Current's context inside Send before enqueuing so the confirmation carries the publish span context`
  - Test location: "tests/Paramore.Brighter.InMemory.Tests/Confirmation"
  - Test file: `When_sending_should_capture_publish_context_before_enqueue.cs`
  - Test should verify:
    - With an active `Activity` at send time, the raised `PublishConfirmationResult.PublishSpanContext` equals that activity's `Context`.
    - With no active `Activity`, `PublishSpanContext` is `null`/`default` (degradation).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Read `Activity.Current?.Context` synchronously inside `Send`/`SendAsync`, before enqueuing (the channel hand-off is the boundary past which `Activity.Current` is no longer S1); carry it on the work-item; populate `PublishSpanContext` from it.
  - **Depends on**: async pump slice.
  - **References**: FR-2, AC-2, AC-2b, C-7; ADR "Same capture invariant".

- [ ] **TEST + IMPLEMENT: two-stage drain on dispose — no confirmation lost on shutdown**
  - **USE COMMAND**: `/test-first InMemoryMessageProducer Dispose/DisposeAsync completes the writer, awaits the worker, then awaits all in-flight raise tasks so no confirmation is dropped on shutdown`
  - Test location: "tests/Paramore.Brighter.InMemory.Tests/Confirmation"
  - Test file: `When_disposing_with_pending_confirmations_should_drain_all.cs`
  - Test should verify:
    - Enqueue N items, then `Dispose`/`DisposeAsync`: all N confirmations are observed (none dropped), including raises spawned right before shutdown.
    - When the switch was never turned on, dispose stays a no-op (no worker started) — matching `:82/:87` today.
    - The already-quiescent case (all raises decremented before writer completed) does not block forever.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - On dispose: `writer.Complete()`, await the worker, then await the in-flight raise tasks. **Preferred:** collect raise `Task` handles into a thread-safe bag and `await Task.WhenAll(bag)` (immune to increment-ordering and quiescent-transition hazards).
    - If using the `Interlocked` count + TCS alternative instead: increment the count in the worker **immediately before** each `Task.Run` spawn (count-before-spawn) and decrement in the raise's `finally`; after `writer.Complete()` + await worker, re-check `count == 0` directly (quiescent re-check) rather than waiting only on a transition.
    - Wrap each raise so a throwing callback still runs its `finally` (count decrement) and cannot kill the worker or strand the drain (NFR-4).
  - **Depends on**: async pump slice.
  - **References**: NFR-3, NFR-4; ADR "Lifecycle / draining" rules (i) count-before-spawn, (ii) re-check-zero, preferred `Task.WhenAll`; InMemory `:82/:87`.

---

## Phase 4 — Shared mediator FAILURE branch (tested in-process via InMemory pump)

> All Phase 4 slices: the mediator callback creates S2 FIRST on every invocation (via the Phase 2 helper), sets `Activity.Current = S2`, then branches on `result.Success`. Rewrite both `ConfigureAsyncPublisherCallbackMaybe` (`:741`) and `ConfigurePublisherCallbackMaybe` (`:768`).

- [ ] **TEST + IMPLEMENT: confirmation failure logs at Warning with id and wire topic (FR-1)**
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

- [ ] **TEST + IMPLEMENT: confirmation (success and failure) emits S2 linked to the publish span, S1 unmutated (FR-2)**
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

- [ ] **TEST + IMPLEMENT: failure trips the breaker on the wire topic (FR-3)**
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

- [ ] **TEST + IMPLEMENT: failure does not mark dispatched, does not bubble, message stays Sweeper-eligible (FR-4)**
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

- [ ] **TEST + IMPLEMENT: empty/null id — FR-1/2/3 still occur with an explicit unknown marker, no crash (FR-5)**
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

- [ ] **TEST + IMPLEMENT: success emits S2 (linked) and nests MarkDispatched under it; no Warning, no breaker (FR-2 success / AC-13 / C-6 orphan fix)**
  - **USE COMMAND**: `/test-first a successful confirmation keeps the sent log and mark-dispatched, emits a linked confirmation span, nests the MarkDispatched DB span under it, and never warns or trips the breaker`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Confirmation"
  - Test file: `When_a_confirmation_succeeds_should_nest_dispatch_under_span.cs`
  - Test should verify:
    - On success: existing Info "sent" log preserved and `MarkDispatched(Async)` called (delivery semantics frozen).
    - S2 emitted with link to S1; the `MarkDispatched` DB span's parent is S2 (no longer an orphaned root span — C-6 fix).
    - No Warning, no `TripTopic` on success (AC-13).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Because S2 is created first on every invocation and sets `Activity.Current = S2` (Phase 2 helper), the success branch's existing `MarkDispatched`/`MarkDispatchedAsync` (`:749/:776`) DB span now nests under S2. Keep the "sent" log (`Log.SentMessage`) and mark-dispatched bodies otherwise unchanged.
  - **Depends on**: Phase 2; Phase 4 FR-2 slice; Phase 3 (success-path pump).
  - **References**: FR-2 (success), AC-13, OOS-4 (delivery frozen), C-6; mediator `:745/:749/:772/:776`.

---

## Phase 6 — Error isolation + concurrency

- [ ] **TEST + IMPLEMENT: observability throw is isolated; breaker trip + log still happen (NFR-4 / AC-14)**
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

- [ ] **TEST + IMPLEMENT: concurrent same-topic failures — no race loss, topic ends tripped (NFR-3 / AC-10)**
  - **USE COMMAND**: `/test-first concurrent same-topic confirmation failures each warn and trip without race-induced loss and the topic ends tripped, with overlap forced by a synchronization gate`
  - Test location: "tests/Paramore.Brighter.Core.Tests/Confirmation"
  - Test file: `When_concurrent_same_topic_confirmations_fail_should_not_lose_trips.cs`
  - Test should verify:
    - Multiple concurrent failure confirmations for one topic each produce a Warning + trip; no loss; topic ends tripped (order-independent end state).
    - Overlap is FORCED via a `Barrier`/`ManualResetEventSlim` released inside the injected callback or `PublishFailurePredicate` (not reliant on the scheduler interleaving short raises).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - No new mediator code expected beyond ensuring thread-safety holds (breaker safe under concurrent `TripTopic`, source-gen logger thread-safe, `ActivitySource.StartActivity` thread-safe). The InMemory pump's `Task.Run`-per-raise (Phase 3) supplies the concurrency; the test's gate forces overlap.
  - **Depends on**: Phase 4 (failure branch); Phase 3 async pump.
  - **References**: NFR-3, AC-10; ADR "Concurrency scope", "Thread safety".

---

## Phase 7 — Kafka unit slices

- [ ] **TEST + IMPLEMENT: Kafka publisher logs the swallowed ProduceException at Warning and preserves the synthetic NotPersisted flow (FR-6 / FR-7)**
  - **USE COMMAND**: `/test-first KafkaMessagePublisher.PublishMessageAsync binds the ProduceException, logs its reason and code at Warning, and still produces the synthetic NotPersisted delivery result to the callback`
  - Test location: "tests/Paramore.Brighter.Kafka.Tests/MessagingGateway"
  - Test file: `When_an_async_produce_throws_should_warn_and_synthesize_not_persisted.cs`
  - Test should verify:
    - On a thrown `ProduceException<string, byte[]>`, a Warning containing `pe.Error.Reason` + `pe.Error.Code` is logged (NFR-1) and the synthetic `NotPersisted` `DeliveryResult` (with `MESSAGE_ID` in `deliveryResult.Headers`) is still routed to the callback (no bubble) (AC-6).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - At `KafkaMessagePublisher.cs:52` bind the catch var: `catch (ProduceException<string, byte[]> pe)`; add a Warning `Log` message with reason+code. Leave the synthetic `NotPersisted` body (`:56-61`) + `deliveryReport(...)` unchanged (FR-7). Leave the wrong-typed `<string,string>` catches at `KafkaMessageProducer.cs:262/336` untouched (OOS-2).
  - **Depends on**: Phase 1.
  - **References**: FR-6, FR-7, AC-6, NFR-1, OOS-2; `KafkaMessagePublisher.cs:52-62`.

- [ ] **TEST + IMPLEMENT: Kafka NotPersisted reads MESSAGE_ID from report-level headers and raises failure with the id (FR-8 / AC-7 / AC-8)**
  - **USE COMMAND**: `/test-first KafkaMessageProducer.PublishResults on the NotPersisted branch reads MESSAGE_ID from the report-level headers and raises a failure confirmation carrying that id, falling back to empty when absent`
  - Test location: "tests/Paramore.Brighter.Kafka.Tests/MessagingGateway"
  - Test file: `When_publish_results_not_persisted_should_raise_failure_with_id.cs`
  - Test should verify:
    - `NotPersisted` with `MESSAGE_ID` present in the report-level `headers` raises `Success == false` with that id (AC-7), NOT from `result.Message.Headers`.
    - `MESSAGE_ID` absent → id falls back to empty (AC-8).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `PublishResults` (`KafkaMessageProducer.cs:381-383`), on the `NotPersisted` fall-through read `MESSAGE_ID` from the report-level `headers` collection (same one the success branch uses at `:368`), fall back to empty; raise the failure record with that id.
  - **Depends on**: Phase 1.
  - **References**: FR-8, AC-7, AC-8; `KafkaMessageProducer.cs:364-384`; ADR FR-8 risk/mitigation.

- [ ] **TEST + IMPLEMENT: Kafka closure rewrite — confirmation carries `message.Header.Topic`, `message.Id`, and the captured publish context (FR-2 Kafka data path)**
  - **USE COMMAND**: `/test-first KafkaMessageProducer captures the publish ActivityContext at send and closes the delivery-report closure over message so the confirmation carries the wire topic from message.Header.Topic, the id, and the publish context even on the synthetic NotPersisted path`
  - Test location: "tests/Paramore.Brighter.Kafka.Tests/MessagingGateway"
  - Test file: `When_a_kafka_confirmation_fires_should_carry_topic_and_link_from_message.cs`
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

## Phase 8 — RMQ slices

- [ ] **TEST + IMPLEMENT: RMQ.Async — `PendingConfirmation` record carries id/topic/context; nack raise enriched (FR-9 / FR-2)**
  - **USE COMMAND**: `/test-first RmqMessageProducer (Async) captures the publish context and wire topic at the top of Send, tracks them in a PendingConfirmation record keyed by delivery tag, and on nack raises a failure confirmation carrying the id, topic, and context`
  - Test location: "tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway"
  - Test file: `When_a_nack_is_received_should_raise_failure_with_context.cs`
  - Test should verify:
    - On nack, the raised `PublishConfirmationResult` carries the message id (AC-9), `message.Header.Topic`, and the captured `ActivityContext` (link present when an `Activity` was active at send; null otherwise).
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `internal readonly record struct PendingConfirmation(Id MessageId, RoutingKey Topic, ActivityContext? Context)` (internal — no public-API/XML-doc obligation).
    - Change `_pendingConfirmations` from `Dictionary<ulong, string>` (`:60`) to `Dictionary<ulong, PendingConfirmation>`; update `AddPendingConfirmation` (`:397`), `RemovePendingConfirmations` (`:419`)/`RemoveConfirmationsLocked` (`:440`, lookup `:447`), `OnPublishFailed` (`:476-482`) and `OnPublishSucceeded` (`:487+`).
    - Capture `Activity.Current?.Context` at the very top of `SendWithDelay(Async)` — above `BeginSend()`/`EnsureBrokerAsync` (the `:167` await) — and stash with topic in `AddPendingConfirmation` (`:187`). Build the enriched result in `OnPublishFailed`/`OnPublishSucceeded`. No `Send`-signature change.
    - If a real broker is genuinely required for any assertion, call it out; otherwise drive the nack handler with existing unit patterns.
  - **Depends on**: Phase 1.
  - **References**: FR-9, FR-2 (RMQ not verify-only), AC-9, C-7, C-8; ADR "RMQ FR-9 + FR-2"; RMQ.Async `:60/:167/:187/:397/:419/:440/:447/:476/:480/:487`.

- [ ] **TEST + IMPLEMENT: RMQ.Sync — same `PendingConfirmation` enrichment on the nack path (FR-9 / FR-2)**
  - **USE COMMAND**: `/test-first RmqMessageProducer (Sync) captures the publish context and wire topic at send, tracks them per delivery tag, and on nack raises a failure confirmation carrying id, topic, and context`
  - Test location: "tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway"
  - Test file: `When_a_nack_is_received_should_raise_failure_with_context.cs`
  - Test should verify:
    - On nack, the raised result carries id (AC-9), `message.Header.Topic`, and captured context (or null degrade).
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
| NFR-3 (thread safety) | Phase 3 async pump + drain; Phase 6 AC-10 concurrency |
| NFR-4 (observability throw isolated) | Phase 6 "observability throws should isolate"; Phase 3 drain (raise wrapped) |
| NFR-5 (always-on; InMemory switch ≠ mediator toggle) | Phase 3 switch slice (default off, preserves behavior); Phase 9 DOC note |
| NFR-6 (breaker trip wire-topic parity) | Phase 4 "trip topic on wire topic" (parity with `:998`) |

### ADR 0063 decision → task

| ADR decision | Task(s) |
|--------------|---------|
| Contract enrichment (`PublishConfirmationResult` + delegate flip + raise/subscriber migration) | Phase 1 (tidy-first) |
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

- **No FR/NFR gaps.** Every FR-1..FR-9 and NFR-1..NFR-6 maps to at least one task.
- **No ADR-decision gaps.** Every Decision/Implementation-Approach mechanism maps to a task.
- **No scope creep.** Every task traces to an FR/NFR/AC or an explicit ADR decision. Phase 9 (DOC) traces to the ADR's stated public-API XML-doc / release-note / operator-note obligations and NFR-5/OOS-3.
- **Note (not a gap):** `BrighterSynchronizationContextsTests.cs:162` is listed in Phase 1 as **verify-then-migrate-only-if-it-binds** — it may be a custom runner's own event rather than `ISupportPublishConfirmation.OnMessagePublished`; the implementer must confirm before touching it.
- **Real-broker call-out:** Phase 8 (RMQ) prefers existing unit nack-path patterns; flagged to call out explicitly if any assertion genuinely requires a live broker. No other task needs broker infrastructure.

### Critical Files for Implementation
- `src/Paramore.Brighter/OutboxProducerMediator.cs` (callback handlers `:741`/`:768`, `TripTopic :1168`, parity `:998`)
- `src/Paramore.Brighter/Observability/BrighterTracer.cs` (`:490`/`:641`/`:700` — new confirmation-span helper)
- `src/Paramore.Brighter/InMemoryMessageProducer.cs` (`:77`/`:100`/`:123`/`:145`/`:82`/`:87` — pump + switch + drain)
- `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessageProducer.cs` (`:260`/`:333`/`:364-384`) + `KafkaMessagePublisher.cs` (`:52-62`)
- `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageProducer.cs` (`:60`/`:397`/`:419`/`:476`) + `.RMQ.Sync/RmqMessageProducer.cs` (`:58`/`:153`/`:233`)
