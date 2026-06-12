# Resume State — Spec 0034 failed-delivery-context

**Last updated:** 2026-06-11
**Branch:** `issue-4179-failed-delivery-context`  ·  **Spec dir:** `specs/0034-failed-delivery-context/`
**Issue:** #4179  ·  **PR:** #4180 (open)

## ✅ STATUS: ADR 0063 design **APPROVED** (R7 PASS). **`tasks.md` DRAFTED** (9 phases, Phase 1 = `/tidy-first` contract break, Phases 4–6 tested in-process via InMemory pump, Kafka/RMQ unit-level). **NEXT: `/spec:review tasks` → `/spec:approve tasks`.** Design approval committed `9cf9f11ee`; `tasks.md` + PROMPT.md UNCOMMITTED.

Spec artifacts only so far; **no production code yet**. Workflow position:
Issue → **Requirements ✅** → **ADR ✅ (APPROVED)** → **Tasks 🔄 (drafted; review+approve owed)** ◀ HERE → Tests → Code.

**Two locked decomposition decisions (PO, 2026-06-11):** (1) contract break = standalone `/tidy-first` structural task FIRST (behavior-preserving, mediator still success-only); (2) Kafka/RMQ FRs tested UNIT-level (no broker), shared mediator FR-1..5/AC-10/AC-13 tested in-process via InMemory pump (so pump Phase 3 precedes mediator Phases 4–6).

**R6 findings (folded into ADR, committed `edc198391`):** ① InMemory two-stage drain quiescent-deadlock + count-before-spawn ordering (64) — drain bullet rewritten with 2 mandatory rules + preferred `Task.WhenAll` alt; ② Kafka closure did NOT actually capture `message` ("already closes over" was false) (63) — reworded to "rewrite the closure body to close over `message`"; ③ lazy worker single-start guard (57) — `Interlocked.CompareExchange`; ④ `Task.Run` AC-10 concurrency probabilistic (46) — softened + test must force overlap with a gate; ⑤ capture-site `Send`→`SendWithDelay` prose (28).
**R7 nits (folded in, uncommitted):** ① Negative-consequence bullet still said TCS-only (48) — reworded to "preferably `Task.WhenAll`…or `Interlocked`+TCS with quiescent re-check"; ② InMemory line drift (30) — `Span :76→:69`, `Scheduler :79→:72` (×2), batch `SendAsync :114→:112`.

| Phase | State |
|---|---|
| Requirements | ✅ Approved (`.requirements-approved`), `d7c997512`, PR #4180. Edited since (FR-10 removed, FR-2 made symmetric, OOS-4 narrowed, AC-13 rewritten) — committed `d8b930803`. |
| Design | ✅ ADR 0063 (`.adr-list`). History: R1 Crit → R2 High → R3/R4 PASS (`5b96a2f4c`) → InMemory expansion (`b8fceb3bc`) → clarity tidy + symmetric success/failure decision (`d8b930803`) → **R5 PASS** (`review-design.md`, 0 findings ≥ 60) with all 5 sub-threshold findings folded in → **Option B InMemory pump** (single worker + `Task.Run`-per-raise, AC-10 now exercised in-memory). **R6 re-review owed**: scrutinize the detailed pump — drain correctness, the `Task.Run` concurrency claim, dispose ordering, no lost/double confirmations. |
| Tasks | Not started |
| Code | Not written |

## ▶️ FIRST THING NEXT SESSION

Everything is committed + pushed (working tree clean). Design is at **R5 PASS** but the InMemory pump gained substantial new detail (Option B concurrency + two-stage drain) that should be re-reviewed before approval. Options:
1. Run `/spec:review design` (**round 6**) — focus on the InMemory pump: drain correctness (two-stage: await worker, then await in-flight `Task.Run` raises), the AC-10 concurrency claim, dispose ordering, no lost/double confirmations on shutdown. Also re-verify the symmetric orphan-fix (R5 confirmed it works via `StartActivity(parentId:null)` → ambient `Activity.Current` fallback).
2. If R6 is clean: `/spec:approve design` → then `/spec:tasks`.

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
1. **Kafka context+topic channel** — BOTH the captured `ActivityContext` AND the wire topic ride the **delivery-report closure
   created inside `SendWithDelay`/`SendWithDelayAsync`** (`KafkaMessageProducer.cs:260` sync, `:333` async → `PublishResults`;
   `Send`/`SendAsync` `:200/:215` are thin delegators). The CURRENT lambda body (`report => PublishResults(...)`) references only
   `report`, so it does NOT yet capture `message` — the mechanism is to **rewrite the closure body to close over `message`** (and the
   captured `ActivityContext` local), making `message.Header.Topic` (topic) + `message.Id` (id) reachable; capture
   `Activity.Current?.Context` into a send-method local for the ctx. NO correlation store (closure is the carrier — the Kafka analogue
   of RMQ's `PendingConfirmation`). **`PublishResults` signature must widen** from `(status, headers)` to also take
   `(RoutingKey topic, ActivityContext? publishContext)` — OR build the `PublishConfirmationResult` inline in the closure; this is the
   listable Kafka producer-side change (parallel to RMQ's map-value change). **⚠️ TRAP: do NOT source topic from `report.Topic`** —
   the async synthetic `NotPersisted` `DeliveryResult` never sets `.Topic` (`KafkaMessagePublisher.cs:56-61`), so it's empty on
   exactly the failure path we observe; `message.Header.Topic` is the only reliable source. **Add a Kafka-specific link/topic AC** —
   AC-2/AC-2b/AC-3b are exercised in-memory only and do NOT cover the Kafka closure, so the link could silently degrade to no-link on
   Kafka without a dedicated assertion. (ADR 0063 "Kafka FR-8 + FR-2 (data path)" + Risks now pin all of this.)
2. **Harden the "capture before any `await`" invariant** — re-honoured at **4 raise sites** (Kafka sync/async, RMQ sync/async).
   Consider a shared capture helper + a test that FAILS if an intervening activity starts before capture. RMQ-async
   `await EnsureBrokerAsync` (`:167`) precedes per-message tracking → capture at top of method.
3. **Binary-break + test migration** — `Action<bool,string>` → `Action<PublishConfirmationResult>` breaks ~**10 in-repo test
   subscriber sites** (Kafka `delegate(bool,string)`; RMQ `(success,messageId)`/`(success,guid)`). Migrate them; confirm break
   fits target release line.
4. **InMemory confirmation capability** — `InMemoryMessageProducer` = 3rd `ISupportPublishConfirmation` implementer. Pinned
   design (round-5 review folded in): settable props (property-injection style) **`bool UseAsyncPublishConfirmation`** (default
   `false` = today's sync write + `(true,id)`) and **`Func<Message,bool>? PublishFailurePredicate`** (default null = never fail;
   `true` ⇒ no bus write + raise `(false,…)`). When switch ON, **fire-and-forget**: `Send`/`SendAsync` capture `Activity.Current`,
   enqueue a work-item (carrying the `Message`) onto an **unbounded** `Channel` (SingleReader, SingleWriter=false) and return
   WITHOUT writing the bus; a **single** lazily-started worker drains **FIFO**, **writes `InternalBus`** (produce-order
   deterministic) **then dispatches the raise via `Task.Run`** (Option B — concurrent callbacks, mirrors Kafka `:373,381`). This
   makes AC-10/NFR-3 same-topic concurrency REACHABLE in-memory but `Task.Run` only *queues* (best-effort, like Kafka) — **AC-10
   test MUST force overlap with a `Barrier`/gate** in the callback, else short raise bodies run serially & test passes vacuously
   (R6 #4). Cost: confirmation *ordering* non-deterministic, but no AC asserts it; single-msg ACs await 1 confirm, unaffected.
   **Single-start guard (R6 #3):** worker started exactly once via `Interlocked.CompareExchange` on a `Task?` field — `SingleReader=true`
   is unenforced, 2 concurrent first-enqueuers must not spin up 2 readers. **Two-stage drain (R6 #1) — TWO ordering rules, both
   mandatory:** (i) **count BEFORE spawn** — worker does `Interlocked.Increment` immediately before each `Task.Run`, raise decrements
   in `finally`; increment inside the `Task.Run` body ⇒ `await worker` returns with uncounted raise ⇒ LOST confirmation. (ii) **re-check
   already-zero** — quiescent shutdown already decremented to 0 before writer completed, so a TCS keyed on "transition to 0" blocks
   forever; disposer must re-eval `count==0` after `await worker` and short-circuit. **PREFERRED: drop the TCS — collect raise `Task`s
   in a thread-safe bag + `await Task.WhenAll` after the worker** (immune to both hazards). `SendWithDelay` → Scheduler re-enters `Send`
   later (pump engages then). Batch `SendAsync` = one work-item per message. Failure hook: `true` ⇒ no bus write + raise `(false,…)`.
   Orphaned `Action<bool,Id>` event (no external subscribers) → `Action<PublishConfirmationResult>`.
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
- **Design:** R1 Critical → R2 High (concurrency) → R3/R4 PASS → **R5 PASS** (`review-design.md`, 0 findings ≥ 60; covered the
  InMemory expansion + symmetric span; empirically confirmed the orphan-fix re-parents via `StartActivity(parentId:null)` →
  ambient fallback). All 5 R5 sub-threshold findings folded in (pump switch/hook names, single-worker FIFO, `SendWithDelay`,
  deferred-visibility consequence, the 2-async-only `CancellationToken` correction, `:1006` line fix) + Option B pump adopted.
  **R6 owed** — re-review the detailed pump (drain correctness, `Task.Run` concurrency, dispose ordering).

## ⚠️ Process reminders (CLAUDE.md)
- **TDD MANDATORY**: TEST tasks use `/test-first <behavior>`; STOP for approval after each test.
- Spec workflow: Requirements → ADR → adversarial review (multiple rounds) → Tasks → Implement. Wait for explicit approval.
- Do NOT change defaults / scope beyond what's asked. Adversarial reviews: clear violation = FAIL (be strict).
