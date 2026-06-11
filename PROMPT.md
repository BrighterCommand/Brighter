# Resume State — Spec 0034 failed-delivery-context

**Last updated:** 2026-06-11
**Branch:** `issue-4179-failed-delivery-context`  ·  **Spec dir:** `specs/0034-failed-delivery-context/`
**Issue:** #4179  ·  **PR:** #4180 (open)

## ✅ STATUS: ADR 0063 committed + expanded; requirements & ADR tidied; **uncommitted working-tree edits** (symmetric decision + clarity pass)

Spec artifacts only so far; **no production code yet**. Workflow position:
Issue → **Requirements ✅** → **ADR ✅ (committed, expanded, edited — uncommitted edits live)** ◀ HERE → re-review (R5) → approve → Tasks → Tests → Code.

| Phase | State |
|---|---|
| Requirements | ✅ Approved (`.requirements-approved`), committed `d7c997512`, pushed, PR #4180. **Edited since (uncommitted)** — see below. |
| Design | ✅ ADR 0063 committed `d4dd4d793`, in `.adr-list`. Reviewed through **R4 PASS** (`5b96a2f4c`). THEN expanded (InMemory confirmation capability, `b8fceb3bc`). THEN **two uncommitted edit passes today** (clarity tidy + symmetric success/failure decision). **R5 re-review still owed** (must cover InMemory expansion **and** the symmetric change). |
| Tasks | Not started |
| Code | Not written |

## ▶️ FIRST THING NEXT SESSION

Working tree has **uncommitted edits** to `specs/0034-failed-delivery-context/requirements.md` and `docs/adr/0063-failed-delivery-context.md` (clarity pass + symmetric decision). Options:
1. Eyeball the diffs, then commit them (the prior ADR/requirements work is already committed; these are follow-up edits).
2. Run `/spec:review design` (round 5) — must cover the InMemory expansion **and** the new symmetric-span decision.
3. When design is settled: `/spec:approve design` → then `/spec:tasks`.

## ✅ RESOLVED DECISIONS (were open; now locked)

**1. How does the wire topic + original `ActivityContext` reach the `OnMessagePublished` callback?**
**Mechanism (a): extend ONLY the confirmation contract.** New public `PublishConfirmationResult` record
(`Success`, `MessageId` typed `Id`, `Topic` `RoutingKey?`, `PublishSpanContext` `ActivityContext?`) carried by
`Action<PublishConfirmationResult>` on `ISupportPublishConfirmation`. **NO `Send`-signature change** — the producer
self-sources the publish span from **`Activity.Current`** (set to S1 by `CreateProducerSpan` at `BrighterTracer.cs:700`,
`AsyncLocal` so race-free), captured as the FIRST action inside `Send`/`SendAsync` (before any `await`) and carried
per-message (Kafka delivery-handler closure / RMQ delivery-tag `PendingConfirmation` record). Only Kafka + RMQ (+ opt-in
InMemory) change; ~10 non-confirmation gateways untouched. The old FR-10 `RequestContext?`-on-`Send` mechanism is now a
**rejected alternative** in the ADR's "Alternatives Considered" (with the map + header options). **FR-10 deleted from
requirements**; mechanism prose stripped from C-7/C-8; the spec now states only the *need* (context reachable at callback),
not the *how*.

**2. Success-path tracing — symmetric? (PO decided 2026-06-11: YES, be symmetric.)**
The standalone linked confirmation span (FR-2) is now emitted on **BOTH** the success and failure branches, not failure-only.
Rationale: today the **success** branch's `MarkDispatched` Outbox span is emitted **orphaned** — the callback passes the
empty construction-time `RequestContext` (C-6) whose `.Span` is null, so `CreateDbSpan(..., requestContext?.Span, ...)`
(`RelationDatabaseOutbox.cs:770`) parents it to nothing and it surfaces as a disconnected root span. The confirmation span
(S2) is created FIRST on every callback and set as `Activity.Current` (per `CreateDbSpan`/`CreateProducerSpan` convention),
so on success `MarkDispatched`'s DB span nests **under S2** (which links to S1). Framed as **"cleanup of how the confirmation
callback reports"**, NOT a success-path feature add. Warning log (FR-1) + breaker trip (FR-3) stay **failure-only**; success
keeps "sent" log + mark-dispatched (delivery semantics frozen — OOS-4 narrowed to *delivery* only). FR-2 / OOS-4 / AC-13 /
NFR-2 / AC-12 all updated; ground-truth item #5 documents the orphan defect.

KEY CAPTURE FACTS (unchanged): source from `Activity.Current` (==S1 during send, AsyncLocal=race-free), NOT the shared
`producer.Span` field (races under concurrent same-topic dispatch → R2 finding #1), NOT `requestContext.Span`
(=parent span). Capture MUST be inside the send call before any await/child-activity. Verified Polly OutboxProducer
pipeline (`src/Paramore.Brighter/Extensions/ResiliencePipelineRegistryExtensions.cs:57-67`, bare AddRetry) does not
perturb `Activity.Current`.

## 📌 TASKS-PHASE carry-forward notes (fold into `/spec:tasks`)
1. **Kafka context channel** — captured `ActivityContext` rides the **delivery-report closure created inside `Send`/`SendAsync`**
   (`KafkaMessageProducer.cs:260, 333` → `PublishResults`); `PublishResults`'s own signature is `(status, headers)`, so context
   carried via closure local, NOT a correlation store.
2. **Harden the "capture before any `await`" invariant** — re-honoured at **4 raise sites** (Kafka sync/async, RMQ sync/async).
   Consider a shared capture helper + a test that FAILS if an intervening activity starts before capture. RMQ-async
   `await EnsureBrokerAsync` (`:167`) precedes per-message tracking → capture at top of method.
3. **Binary-break + test migration** — `Action<bool,string>` → `Action<PublishConfirmationResult>` breaks ~**10 in-repo test
   subscriber sites** (Kafka `delegate(bool,string)`; RMQ `(success,messageId)`/`(success,guid)`). Migrate them; confirm break
   fits target release line.
4. **InMemory confirmation capability** — `InMemoryMessageProducer` = 3rd `ISupportPublishConfirmation` implementer: opt-in
   async-confirm switch (default off = today's sync `(true,id)`), `Channel`+worker pump raising enriched callback off-thread,
   failure-injection hook (drives the `false` path), capture `Activity.Current` before enqueue + carry, dispose draining. Its
   orphaned `Action<bool,Id>` event (no external subscribers) → enriched `Action<PublishConfirmationResult>`.
5. **Symmetric span + orphan fix (NEW)** — mediator callback creates the confirmation span (S2) FIRST on every invocation, sets
   `Activity.Current = S2`, THEN branches. New `BrighterTracer` confirmation-span helper (outcome flag, both branches, sets
   `Activity.Current`). Success branch needs its OWN regression test (AC-13): asserts S2 + link to S1 + `MarkDispatched` DB span
   **nested under S2** (not orphaned root) + no Warning + no breaker.

## What the feature does (issue #4179)

When a confirmation-based producer (Kafka/RMQ) reports a failed publish via `OnMessagePublished(success: false, id)`, the
`success == false` branch is currently **silently dropped**. This spec makes it observable **without** changing the
"don't bubble, let the Sweeper retry" design, AND (PO 2026-06-11) cleans up confirmation-callback tracing symmetrically:
- **FR-1** Warning log (NOT Error) with message id + wire topic. *(failure-only)*
- **FR-2** Standalone short-lived OTel `Activity` (S2) with an `ActivityLink` to the original publish span, **on BOTH success
  and failure**; on success S2 parents the `MarkDispatched` DB span (fixes today's orphaned root span). Degrades to no-Link
  when context unreachable; S2 itself unconditional.
- **FR-3** Trip the circuit breaker for the **wire topic** `message.Header.Topic` (exact parity with the non-confirmation
  `!sent` path). *(failure-only)*
- **FR-4** Preserve un-dispatched / no-bubble semantics.
- **FR-5** Empty/null id handled gracefully.
- **FR-6/FR-7** Kafka: log the swallowed `ProduceException` (reason+code; catch currently binds no var); keep synthetic
  `NotPersisted` callback flow.
- **FR-8** Kafka: propagate failed id through `PublishResults` (read from report-level `Headers`, NOT `Message.Headers`).
- **FR-9** RMQ already carries the id on nack — verify-only for the id path (NOT verify-only for FR-2's link).
- **(FR-10 DELETED)** — the optional `RequestContext?`-on-`Send` mechanism is now a rejected alternative in the ADR only.
- **NFR-1..6** Warning level; concrete non-blocking bar (span bar now applies to both branches); thread-safety; error
  isolation (NFR-4 → AC-14); always-on; same TripTopic semantics.

## 🔒 Verified codebase facts (don't re-derive; confirmed across review rounds)

- Callback wired **once per producer** in `OutboxProducerMediator` ctor via `ConfigureCallbacks`
  (`OutboxProducerMediator.cs:162`); closure captures `producer` + a single **empty** construction-time `RequestContext`;
  delegate is `delegate(bool success, string id)` (`:741, :765`). Captured ctx IS used on success → `MarkDispatched(Async)`
  (`:749, :776`).
- **Success-path orphan (drives the symmetric decision):** the success branch calls `MarkDispatched`/`MarkDispatchedAsync`
  with the empty construction-time `RequestContext` (Span=null); the Outbox builds the DB span via
  `CreateDbSpan(BoxSpanInfo(... MarkDispatched ...), requestContext?.Span, ...)` (`RelationDatabaseOutbox.cs:770`), which uses
  `parentId = parentActivity?.Id` (`BrighterTracer.cs:496`) → null parent; the async callback thread's `Activity.Current` is
  also unset → span is a disconnected root. `CreateDbSpan` ALSO sets `Activity.Current = activity` (`BrighterTracer.cs:535`),
  so once S2 is current the MarkDispatched span nests under it.
- Non-confirmation `!sent` path trips `TripTopic(message.Header.Topic)` (`:998`); batch path trips `batch.RoutingKey` (`:933`).
  Wire topic ≠ `Publication.Topic` for reply msgs (`GetProducerLookupTopic`, `:786-807`).
- `TripTopic`: interface overload `TripTopic(RoutingKey)` (`IAmAnOutboxCircuitBreaker.cs:44`); mediator private overload
  `TripTopic(RoutingKey?)` (`OutboxProducerMediator.cs:1168`).
- `Publication.Topic` is `RoutingKey?` (`Publication.cs:86`); `ProducerRegistry` keys by composite `ProducerKey(Topic, CE type)`.
- Kafka swallow: `KafkaMessagePublisher.PublishMessageAsync` catch is unbound `catch (ProduceException<string, byte[]>)`
  (`KafkaMessagePublisher.cs:52`); MESSAGE_ID written to `deliveryResult.Headers` while `Message.Headers` left empty (`:58-60`).
- `KafkaMessageProducer.PublishResults` (`:364-384`) reads MESSAGE_ID only on `Persisted`; `false` branch hardcodes
  `OnMessagePublished(false, string.Empty)` (`:381-383`); callback marshalled via `Task.Run` (`:373, :381`).
- OOS-2 dead wrong-typed `catch (ProduceException<string,string>)` at `KafkaMessageProducer.cs:262, 336`.
- RMQ async nack already `OnMessagePublished(false, messageId)` (`RmqMessageProducer.cs:480`) + Debug log; sync analogous (`:235`).
  `_pendingConfirmations` map value type widens to internal `PendingConfirmation` record (Id, RoutingKey topic, ActivityContext?).
- `BrighterTracer` span helpers: `CreateProducerSpan` (`:641`, sets `Activity.Current=S1` at `:700`), `CreateDbSpan` (`:490`,
  sets `Activity.Current=activity` at `:535`), `CreateSpan<TRequest>` accepts `ActivityLink[]? links` (`:106`).

## Key files
- `src/Paramore.Brighter/OutboxProducerMediator.cs` — shared callback (`Configure*CallbackMaybe` ~737-784), `TripTopic` (~998, ~1168)
- `src/Paramore.Brighter/ISupportPublishConfirmation.cs` — `OnMessagePublished` → `Action<PublishConfirmationResult>`
- `src/Paramore.Brighter/PublishConfirmationResult.cs` *(new)* — public result record (XML docs)
- `src/Paramore.Brighter/Observability/BrighterTracer.cs` — `CreateProducerSpan` (`:700` capture source), `CreateDbSpan`
  (`:490`/`:535`), new confirmation-span helper (both branches, sets `Activity.Current`)
- `src/Paramore.Brighter/RelationDatabaseOutbox.cs` — `MarkDispatched(Async)` DB span (`:770`) — the orphaned-on-success span
- `src/Paramore.Brighter/CircuitBreaker/IAmAnOutboxCircuitBreaker.cs` — `TripTopic`
- `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessagePublisher.cs` (~52-62) / `KafkaMessageProducer.cs` (~364-384)
- `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageProducer.cs` + `.RMQ.Sync/` (~480) — capture + enriched raise
- `src/Paramore.Brighter/InMemoryMessageProducer.cs` — 3rd confirmation producer (opt-in async pump + failure injection)
- `src/Paramore.Brighter/IAmAMessageProducerSync.cs` / `IAmAMessageProducerAsync.cs` — **NO signature change**

## Review history
- **Requirements:** 3 adversarial rounds (7→4→2 findings ≥ threshold, all resolved). `review-requirements.md` (round 3).
- **Design:** R1 Critical → R2 High (concurrency) → R3/R4 PASS, all committed. `review-design.md` (round 4). **R5 owed** —
  must cover (a) the InMemory expansion and (b) the symmetric success/failure span decision.

## ⚠️ Process reminders (CLAUDE.md)
- **TDD MANDATORY**: TEST tasks use `/test-first <behavior>`; STOP for approval after each test.
- Spec workflow: Requirements → ADR → adversarial review (multiple rounds) → Tasks → Implement. Wait for explicit approval.
- Do NOT change defaults / scope beyond what's asked. Adversarial reviews: clear violation = FAIL (be strict).
