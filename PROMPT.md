# Resume State — Spec 0034 failed-delivery-context

**Last updated:** 2026-06-10
**Branch:** `issue-4179-failed-delivery-context`  ·  **Spec dir:** `specs/0034-failed-delivery-context/`
**Issue:** #4179  ·  **PR:** #4180 (open)

## ✅ STATUS: ADR 0063 DRAFTED (uncommitted) — next is review/approval then `/spec:tasks`

Spec artifacts only so far; **no production code yet**. Workflow position:
Issue → **Requirements ✅** → **ADR ✅ (drafted, uncommitted)** ◀ HERE → review → Tasks → Tests → Code → Review.

| Phase | State |
|---|---|
| Requirements | ✅ Approved (`.requirements-approved`), committed `d7c997512`, pushed, PR #4180 |
| Design | ✅ ADR 0063 `docs/adr/0063-failed-delivery-context.md`, in `.adr-list`, **committed + pushed** (HEAD has RMQ `PendingConfirmation` record refinement + R4 nit fixes uncommitted). 4 review rounds: R1 4 findings (1 Critical)→revised; R2 1 High (concurrency)→`Activity.Current`; R3 PASS; **R4 PASS** (named-record refinement, 2 cosmetic nits folded in). Ready for `/spec:approve design`. |
| Tasks | Not started |
| Code | Not written |

## ▶️ FIRST THING NEXT SESSION

ADR 0063 is drafted but **not yet committed**. Options:
1. Commit it: `git add docs/adr/0063-failed-delivery-context.md specs/0034-failed-delivery-context/.adr-list && git commit` (first ADR = first commit on branch).
2. Run `/spec:review design` for an adversarial review round before approval.
3. When design is settled: `/spec:approve design` → then `/spec:tasks`.

ADR central decision (locked + revised): **mechanism (a) — extend ONLY the `OnMessagePublished` contract**.
New `PublishConfirmationResult` record (Success, MessageId, Topic, PublishSpanContext) carried by
`Action<PublishConfirmationResult>` on `ISupportPublishConfirmation`. **REVISED 2026-06-11 (PO-approved):
NO `Send`-signature change.** The producer self-sources the publish span from **`Activity.Current`**
(set to S1 by `CreateProducerSpan` at `BrighterTracer.cs:700`, `AsyncLocal` so race-free), capturing
the `ActivityContext` *value* synchronously as the FIRST action inside `Send`/`SendAsync` (before any
await) and carrying it per-message via existing confirmation machinery (Kafka delivery-handler closure /
RMQ delivery-tag map, whose value type widens). This **meets FR-10's intent without the all-gateways
fan-out** — only Kafka + RMQ change; ~10 non-confirmation gateways untouched; sidesteps the positional-CT
param-ordering break. (b) correlation map + (c) header propagation documented as rejected.
KEY FACTS: source from `Activity.Current` (==S1 during send, AsyncLocal=race-free), NOT the shared
`producer.Span` field (races under concurrent same-topic dispatch → R2 finding #1), NOT `requestContext.Span`
(=parent span). Capture MUST be inside the send call before any await/child-activity, never at callback
time (S1 ended + Activity.Current reset by then). Verified Polly OutboxProducer pipeline
(`src/Paramore.Brighter/Extensions/ResiliencePipelineRegistryExtensions.cs:57-67`, bare AddRetry) does not perturb `Activity.Current`.

## 📌 TASKS-PHASE carry-forward notes (from PR #4180 wider review — fold into `/spec:tasks`)
These were deferred from the design review to the tasks phase (PO decision 2026-06-11):
1. **Kafka context channel** — the captured `ActivityContext` rides the **delivery-report closure created inside `Send`/`SendAsync`** (`KafkaMessageProducer.cs:260, 333` → `PublishResults`); `PublishResults`'s own signature is just `(status, headers)`, so the context is carried via the closure local, NOT a new correlation store. Make this explicit in the implementation task.
2. **Harden the "capture before any `await`" invariant** — it must be re-honoured at **4 raise sites** (Kafka sync/async, RMQ sync/async). Consider a single shared capture helper + a test that FAILS if an intervening activity is started before capture (AC-2/AC-10 only catch a regression when a tracing listener is active). The RMQ-async `await EnsureBrokerAsync` (`:167`) precedes per-message tracking → capture must go at the top of the method.
3. **Binary-break + test migration** — `Action<bool,string>` → `Action<PublishConfirmationResult>` breaks ~**10 in-repo test subscriber sites** (Kafka tests `delegate(bool,string)`; RMQ tests `(success, messageId)`/`(success, guid)`). Migrate them; confirm the break fits the target release line (minor vs major). `InMemoryMessageProducer` is NOT affected (does not implement `ISupportPublishConfirmation`; its `OnMessagePublished` is a separate `Action<bool, Id>` with no false path).
4. **Test fixture for shared mediator path** — FR-1/2/3 unit tests need a **fake `ISupportPublishConfirmation` producer** that can raise `(false, …)`. InMemory cannot serve (not a confirmation producer, no failure path). Either build a fake or use real Kafka/RMQ integration tests. (Making InMemory a confirmation producer = scope expansion beyond Kafka+RMQ; out of scope unless PO decides otherwise.)

## What the feature does (issue #4179)

When a confirmation-based producer (Kafka/RMQ) reports a failed publish via
`OnMessagePublished(success: false, id)`, the `success == false` branch is currently **silently
dropped**. This spec makes it observable **without** changing the "don't bubble, let the Sweeper
retry" design:
- **FR-1** Warning log (NOT Error) with message id + wire topic.
- **FR-2** Standalone short-lived OTel `Activity` with an `ActivityLink` to the original publish
  span (degrades to no-Link when context unreachable; standalone span/log/breaker are unconditional).
- **FR-3** Trip the circuit breaker for the **wire topic** `message.Header.Topic` (exact parity
  with the non-confirmation `!sent` path, incl. reply/rewritten topics — NOT `Publication.Topic`).
- **FR-4** Preserve un-dispatched / no-bubble semantics.
- **FR-5** Empty/null id handled gracefully.
- **FR-6/FR-7** Kafka: log the swallowed `ProduceException` (reason+code; catch currently binds no
  var); keep synthetic `NotPersisted` callback flow.
- **FR-8** Kafka: propagate failed id through `PublishResults` (read from report-level `Headers`,
  NOT `Message.Headers`).
- **FR-9** RMQ already carries the id on nack — verify-only for the id path.
- **FR-10 (conditional)** Optional `RequestContext?` on `Send`/`SendWithDelay`/`SendAsync`/
  `SendWithDelayAsync` to flow the publish trace context to the callback.
- **NFR-1..6** Warning level; concrete non-blocking bar; thread-safety (concurrency, not "background
  thread"); error isolation (NFR-4 → AC-15); always-on; same TripTopic semantics.

## 🔑 OPEN DECISION for ADR 0063 — how do the wire topic + original ActivityContext reach the `OnMessagePublished` callback?

The callback delegate is `Action<bool, string>` (success + id only). The per-message wire topic and
the original publish span's `ActivityContext` are NOT reachable from the callback's captured state
(see verified facts). Candidates (requirements C-7/C-8 defer the choice to the ADR):
- **(a) Extend the contract** — add params to `OnMessagePublished` carrying topic + ActivityContext,
  fed by FR-10's optional `RequestContext?` on the producer `Send` methods. Cleanest data flow.
  COST: changes `ISupportPublishConfirmation` + `IAmAMessageProducerSync`/`Async`; ripples to **all**
  gateway implementations (SNS/SQS/ASB/Redis/MQTT/…); makes RMQ more than verify-only for FR-2.
  Accepted as effectively non-breaking for END users (implementors are middleware authors).
- **(b) Correlation map** — `messageId → (topic, ActivityContext)` populated at dispatch, read in
  callback. No contract change; COST: shared mutable state (NFR-3 thread-safety) + lifecycle/cleanup.
- **(c) Header propagation** — read trace context back from message headers. No new state; COST: only
  recovers trace context, not cleanly the wire topic; depends on header round-trip.

Product-owner steer so far = lean **(a)**. ADR must also cover: standalone-span construction with
`ActivityLink`; breaker wiring on the `false` branch; error isolation (NFR-4); Kafka FR-6/FR-8;
the tidy-first opportunity to drop the empty construction-time `RequestContext` (only if the flowed
context also covers the success-path `MarkDispatched`).

## 🔒 Verified codebase facts (don't re-derive; confirmed across 3 review rounds)

- Callback wired **once per producer** in `OutboxProducerMediator` ctor via `ConfigureCallbacks`
  (`OutboxProducerMediator.cs:162`); closure captures `producer` + a single **empty**
  construction-time `RequestContext`; delegate is `delegate(bool success, string id)` (`:741, :765`).
  Captured ctx IS used on success → `MarkDispatched(Async)` (`:749, :776`).
- Non-confirmation `!sent` path trips `TripTopic(message.Header.Topic)` (`:998`) and batch path trips
  `batch.RoutingKey` (`:933`). Wire topic ≠ `Publication.Topic` for reply msgs
  (`GetProducerLookupTopic`, `:786-807`).
- `TripTopic`: interface overload `TripTopic(RoutingKey)` (`IAmAnOutboxCircuitBreaker.cs:44`);
  mediator private overload `TripTopic(RoutingKey?)` (`OutboxProducerMediator.cs:1168`).
- `Publication.Topic` is `RoutingKey?` (`Publication.cs:86`); `ProducerRegistry` keys by composite
  `ProducerKey(Topic, CloudEvents type)` (`ProducerRegistry.cs:29, 52`).
- Kafka swallow: `KafkaMessagePublisher.PublishMessageAsync` catch is unbound
  `catch (ProduceException<string, byte[]>)` (`KafkaMessagePublisher.cs:52`); MESSAGE_ID written to
  `deliveryResult.Headers` while `Message.Headers` left empty (`:58-60`).
- `KafkaMessageProducer.PublishResults` (`:364-384`) reads MESSAGE_ID only on `Persisted`; `false`
  branch hardcodes `OnMessagePublished(false, string.Empty)` (`:381-383`); callback marshalled via
  `Task.Run` (`:373, :381`).
- OOS-2 dead wrong-typed `catch (ProduceException<string,string>)` at `KafkaMessageProducer.cs:262, 336`.
- RMQ async nack already `OnMessagePublished(false, messageId)` (`RmqMessageProducer.cs:480`) + Debug
  log `FailedToPublishMessageAsync` (`:481, :512-513`); sync analogous (`:235`).

## Key files
- `src/Paramore.Brighter/OutboxProducerMediator.cs` — shared callback (`Configure*CallbackMaybe`
  ~737-784), `TripTopic` (~998, ~1168)
- `src/Paramore.Brighter/ISupportPublishConfirmation.cs` — `OnMessagePublished` contract
- `src/Paramore.Brighter/IAmAMessageProducerSync.cs` / `IAmAMessageProducerAsync.cs` — `Send`/`SendAsync` (FR-10)
- `src/Paramore.Brighter/CircuitBreaker/IAmAnOutboxCircuitBreaker.cs` — `TripTopic`
- `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessagePublisher.cs` (~52-62) / `KafkaMessageProducer.cs` (~364-384)
- `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageProducer.cs` + `.RMQ.Sync/` (~480) — verify-only
- `src/Paramore.Brighter/Observability/BrighterTracer.cs:73` — `AddExceptionToSpan` (existing helper)

## Review history (requirements)
3 adversarial rounds: **7 → 4 → 2** findings ≥ threshold, all resolved. Latest findings file:
`specs/0034-failed-delivery-context/review-requirements.md` (round 3). Notable catches: callback is
once-per-producer at construction; breaker must trip wire topic (not `Publication.Topic`).

## ⚠️ Process reminders (CLAUDE.md)
- **TDD MANDATORY**: TEST tasks use `/test-first <behavior>`; STOP for approval after each test.
- Spec workflow: Requirements → ADR → adversarial review (multiple rounds) → Tasks → Implement.
  Wait for explicit approval between phases.
- Do NOT change defaults / scope beyond what's asked.
- Adversarial reviews: clear violation = FAIL (be strict).
