# 0063. Failed Delivery Context for Confirmation-Based Producers

Date: 2026-06-10

## Status

Proposed

## Context

Confirmation-based producers (those implementing `ISupportPublishConfirmation`, currently `KafkaMessageProducer` and `RmqMessageProducer` in both the `RMQ.Async` and `RMQ.Sync` projects) report publish success or failure *asynchronously*, after the synchronous `Send`/`SendAsync` call has already returned. They do this by raising the `OnMessagePublished` event (`src/Paramore.Brighter/ISupportPublishConfirmation.cs:42`), whose signature today is `event Action<bool, string>` (success, messageId).

`OutboxProducerMediator` wires a single handler onto that event, once per producer, in its constructor via `ConfigureCallbacks` (`src/Paramore.Brighter/OutboxProducerMediator.cs:162`, iterating `_producerRegistry.Producers`). The handlers (`ConfigureAsyncPublisherCallbackMaybe` at `:737` and `ConfigurePublisherCallbackMaybe` at `:764`) handle **only** the `success == true` branch: they log "sent" and call `MarkDispatched(Async)` (`:749`, `:776`). The `success == false` branch is silently dropped. The result is the four gaps in the parent requirement: no log, no telemetry, no circuit-breaker trip, and (for Kafka) a discarded message id and a swallowed `ProduceException`.

Contrast the non-confirmation path: in `DispatchAsync`, a synchronous send failure is observed inline via the `sent` boolean and trips the breaker with `if(!sent) TripTopic(message.Header.Topic);` (`:998`); the batch path trips `TripTopic(batch.RoutingKey)` (`:933`). Confirmation-based producers never reach that code because their send returns "successfully" and the real result arrives later on the callback.

Three facts shape the design and create dependencies:

- **C-6 (verified).** The callback closure captures a single construction-time `RequestContext` produced by `requestContextFactory.Create()` (`:162`). It is empty of per-publish data: it carries neither the per-message wire topic nor the per-publish producer span. The closure does not capture the per-message `message`. So neither the topic nor the original trace context for FR-1/2/3 is reachable from captured closure state. (The captured context is not wholly unused: the success branch passes it to `MarkDispatched`/`MarkDispatchedAsync`.)
- **C-7 (trace-context reachability).** The original publish span is created in `Dispatch`/`DispatchAsync` via `_tracer.CreateProducerSpan(...)` (`:825`, `:970`) and ended in the `finally` via `_tracer.EndSpans(producerSpans)` (`:863`, `:945` / `BrighterTracer.EndSpan` at `src/Paramore.Brighter/Observability/BrighterTracer.cs:709`). By the time the asynchronous confirmation fires, that span has already ended (C-2). Its `ActivityContext` must be made reachable at callback time to build FR-2's `ActivityLink`.
- **C-8 (wire-topic source).** FR-1/FR-3 must use the per-message wire topic `message.Header.Topic`, the exact value the non-confirmation path trips (`:998`), **not** `producer.Publication.Topic`. The two diverge for reply/rewritten-topic messages: `GetProducerLookupTopic` (`:786-808`) reads a `ProducerTopicHeaderName` bag entry that points lookups at the static topic while `Header.Topic` holds a dynamic reply address. The wire topic is not carried by the current `Action<bool, string>` delegate, so it too must be plumbed to the callback.

A further wrinkle constrains *how* the context is captured: spans are thread-affine. `RequestContext.Span` is **thread-keyed by `ManagedThreadId`** (`src/Paramore.Brighter/RequestContext.cs:113-125` — backed by a `ConcurrentDictionary<int, Activity>` `_spans` keyed on `Thread.CurrentThread.ManagedThreadId`), and `producer.Span` is a single mutable property on a shared producer, nulled right after the send (`:858`) and reused per message. The confirmation callback runs on a different thread from `Dispatch`, later in time (Kafka marshals via `Task.Run` at `KafkaMessageProducer.cs:373, 381`; RMQ invokes from its broker nack handler at `RmqMessageProducer.cs:480`). Therefore none of these can be re-read at callback time to recover S1 — the *value* of the `ActivityContext` (a struct, valid after the span ends) must be captured synchronously at dispatch time and carried explicitly to the callback. The Decision sources that value from `Activity.Current` (set to S1 by `CreateProducerSpan` and `AsyncLocal`, so race-free across concurrent dispatch), **not** the shared `producer.Span` field (which would race) and **not** `RequestContext.Span` (which holds the parent span).

**Parent Requirement**: [specs/0034-failed-delivery-context/requirements.md](../../specs/0034-failed-delivery-context/requirements.md)

**Scope.** This single ADR covers, together: the callback-plumbing/contract mechanism (how the wire topic and original trace context reach the callback); the standalone-span + `ActivityLink` observability design (FR-2); the breaker-trip wiring on the `false` branch (FR-3); the Kafka publisher logging of the swallowed `ProduceException` (FR-6) and id propagation through `PublishResults` (FR-8); RMQ verification (FR-9) and RMQ's raise-site context enrichment (FR-2, satisfying FR-10's intent without a `Send`-signature change); and error isolation (NFR-4 / AC-15). Out of scope: Sweeper/retry semantics (C-1, OOS-1), the dead wrong-typed `catch (ProduceException<string, string>)` clauses at `KafkaMessageProducer.cs:262` and `:336` (OOS-2), any config toggle (OOS-3, NFR-5), and the `success == true` path (OOS-4).

## Decision

Adopt **mechanism (a): extend the contract** — but only the *one* contract that must change, the confirmation contract (`ISupportPublishConfirmation.OnMessagePublished`). The original publish span's `ActivityContext` is sourced by the producer from **`Activity.Current`** (which `CreateProducerSpan` sets to the producer span S1 immediately before `Send` is called, and which is `AsyncLocal` so it is race-free across concurrent dispatch), so **no change to the `IAmAMessageProducerSync`/`Async` `Send` signatures is required**.

1. **Producer self-sources the publish span (no `Send` signature change).** When `OutboxProducerMediator.Dispatch`/`DispatchAsync` create the producer span S1 via `CreateProducerSpan`, that helper also sets `Activity.Current = S1` (`BrighterTracer.cs:700`). The producer therefore captures the **value** `Activity.Current?.Context` (an `ActivityContext` — a struct that stays valid after the span ends) **synchronously, as the first action inside `Send`/`SendAsync`, before any `await`**, and carries it alongside the per-message state through its existing confirmation machinery (see Implementation Approach) to the point where it raises `OnMessagePublished`. `Activity.Current` is `AsyncLocal`, so concurrent dispatches on different threads see independent values — sourcing the span this way is **race-free** under concurrent dispatch to the same producer, unlike reading the shared `producer.Span` property (see "Capturing the context at dispatch" for why the shared field is rejected). This **meets FR-10's intent — flowing the publish trace context out to the callback — by a lighter path** that does not touch the producer `Send` contract (see "Relationship to FR-10").

2. **Enrich the `OnMessagePublished` contract** so the callback receives, in addition to `success` + message id, the per-message **wire topic** (`message.Header.Topic`) and the **original publish `ActivityContext`** (the value captured in step 1). The producer holds both pieces — it already receives the `message` in `Send`/`SendAsync` and self-sources the span from `Activity.Current` — so the same enrichment carries both. This is the **only** interface contract that changes, and it changes only for the two producers that implement `ISupportPublishConfirmation` (Kafka and RMQ).

`OutboxProducerMediator`'s callback then, on `success == false`:
- logs at **Warning** including message id (empty-marked when blank) and wire topic (FR-1, NFR-1);
- asks `BrighterTracer` to emit a **standalone short-lived span** carrying an `ActivityLink` to the captured `ActivityContext` when present, or no link when absent (FR-2, degraded per AC-2b), started and stopped synchronously within the callback (NFR-2);
- trips the breaker via the existing private `TripTopic(RoutingKey?)` (`:1168`) with the wire topic (FR-3, NFR-6), giving exact parity with `:998`;
- does **not** mark dispatched, does **not** await any Outbox/broker operation, and does **not** bubble (FR-4, AC-12b);
- wraps the observability work so a throw there cannot escape the callback (NFR-4 / AC-15).

### Choosing the enriched `OnMessagePublished` shape

The existing event is `event Action<bool, string> OnMessagePublished`. Changing its delegate type is **both source- and binary-breaking for implementors** — every producer raising it, plus the mediator's two handlers, must be recompiled and updated, and a precompiled third-party assembly referencing the old delegate type will fail to bind — but it is invisible to end users (gateway authors raise it; application code never does). Because only `ISupportPublishConfirmation` implementors (Kafka and RMQ in-tree) are affected, the blast radius is the two confirmation producers, not all gateways. Two candidate shapes:

- A wider tuple delegate, e.g. `Action<bool, string, RoutingKey?, ActivityContext?>`. Rejected: positional `bool`/`string`/nullable args invite mis-wiring, are not self-describing, and adding a fifth datum later breaks the signature again. This also conflicts with the design principle against primitive obsession.
- **A single immutable args record carried by `Action<PublishConfirmationResult>`** (chosen). Under Responsibility-Driven Design the result type is an *information holder* with named, intention-revealing members:

```csharp
public sealed record PublishConfirmationResult(
    bool Success,
    string MessageId,
    RoutingKey? Topic,            // message.Header.Topic — the wire topic (C-8)
    ActivityContext? PublishSpanContext);  // original publish span (C-7); null => Link degrades (FR-2)
```

This keeps the contract stable against future additions (add a property, not a parameter), makes the degradation contract explicit (`PublishSpanContext == null` ⇒ no link), and reads at the call site. `PublishConfirmationResult` is a **new public type** in the `Paramore.Brighter` namespace (alongside `ISupportPublishConfirmation`); it is part of the public API surface, so it carries the project's XML-documentation obligation on the type and every member, and it participates in semantic versioning as an additive public type.

**Both branches use the record.** `OnMessagePublished` is raised on success *and* failure, so its single argument carries both cases. On the `Success == true` raise the producer populates `Success = true`, `MessageId`, and `Topic` (`message.Header.Topic`, which it holds); `PublishSpanContext` MAY be the captured value or `default` — the mediator's success branch reads only `Success` and `MessageId` (to call `MarkDispatched`) and ignores `Topic`/`PublishSpanContext`, so its behaviour is unchanged (OOS-4, AC-13). On the `Success == false` raise all four members are populated as described. Documenting this makes the success-side contract unambiguous so two implementors populate it the same way.

Backward-compat story, stated honestly: this *is* a source- **and binary-**breaking change to a public interface, but only for implementors of `ISupportPublishConfirmation` (Kafka, RMQ, and any third-party confirmation producer); they must update their raise sites and recompile. End users are unaffected, and non-confirmation gateways are untouched. We accept this because the data simply cannot reach the callback without it (C-6/C-7/C-8), and a correlation map or header round-trip (see Alternatives) carry worse trade-offs.

### Relationship to FR-10 (meeting the intent without the fan-out)

FR-10, as written in the requirements, proposed an optional `RequestContext?` parameter on the four `IAmAMessageProducerSync`/`Async` `Send`/`SendWithDelay`(`Async`) methods to flow the publish trace context down to the producer. FR-10 is explicitly *conditional on the ADR's mechanism choice* and may be "replaced by that mechanism's equivalent." This ADR adopts a different, lighter path that satisfies the same intent:

- **The producer already has the publish span ambiently.** `CreateProducerSpan` sets `Activity.Current = S1` (`BrighterTracer.cs:700`) before `Send`/`SendAsync` is called, so the producer can read `Activity.Current?.Context` itself. The `RequestContext` does not need to be plumbed through the `Send` signature to deliver the span.
- **This removes the all-gateways fan-out** that the original FR-10 mechanism incurred. No `Send`-signature change means the ~10 non-confirmation gateways (`InMemoryMessageProducer`, AWS SNS/SQS V3+V4, Azure Service Bus, GCP, MQTT, MsSql, Postgres, Redis, RocketMQ) are **untouched**. Only the two `ISupportPublishConfirmation` producers (Kafka, RMQ) change — and they would change anyway, because they raise the enriched `OnMessagePublished`.
- **It is therefore an improvement over the literal FR-10:** same observable outcome (FR-2's `ActivityLink`), strictly smaller blast radius, and it sidesteps the optional-parameter-ordering hazard entirely (no new parameter ahead of the existing `CancellationToken` on the async methods).

The original FR-10 mechanism is retained as a rejected alternative below only implicitly; the wire-topic plumbing of C-8 is still satisfied (the producer holds `message`, so `message.Header.Topic` is always available).

### Architecture Overview

```
Dispatch / DispatchAsync (mediator)                     OnMessagePublished callback (later, other thread)
  S1 = tracer.CreateProducerSpan(...)  ── publish span ──┐
       └─ sets Activity.Current = S1   (BrighterTracer :700)
  producer.SendAsync(message) ───────────────────────────┤
    └─ inside Send (sync, first line, pre-await):         │
         ctx = Activity.Current?.Context  (value, == S1)  │  ← AsyncLocal, race-free
         stash ctx with per-message confirmation state    │
  finally: tracer.EndSpans(...)   << S1 ENDED >>          │
                                                          │   broker nack arrives (async)
                                                          ▼
                                      producer raises OnMessagePublished(
                                          PublishConfirmationResult(
                                              Success=false,
                                              MessageId=id,
                                              Topic=message.Header.Topic,        (C-8)
                                              PublishSpanContext=ctx ))          (C-7, may be null)
                                                          │
                                                          ▼
                                      mediator false-branch handler:
                                        ├─ Log.Warning(id-or-"unknown", wireTopic)     (FR-1)
                                        ├─ S2 = standalone span,                        (FR-2)
                                        │     links=[ new ActivityLink(ctx) ] if ctx!=null
                                        │     (S1 NOT reopened; Activity.Current NOT used)
                                        │     start + stop within callback              (NFR-2)
                                        └─ TripTopic(wireTopic)                          (FR-3)
                                        (all wrapped: no throw escapes — NFR-4)
                                        (no MarkDispatched, no await broker/Outbox — FR-4/AC-12b)
```

`S1` is the original producer span, already ended. `S2` is a distinct standalone span linked back to `S1`'s context — never an enrichment of `S1` and never attributed to ambient `Activity.Current`.

### Key Components (roles and responsibilities)

- **`ISupportPublishConfirmation`** (interfacer / contract). Now *knows* it must surface the wire topic and the publish span context; its event carries `PublishConfirmationResult`.
- **`IAmAMessageProducerSync` / `IAmAMessageProducerAsync`** (interfacer). **Unchanged.** Their `Send`/`SendAsync` signatures are not modified; the publish span is sourced by the producer from `Activity.Current` (set to S1 by `CreateProducerSpan`, `BrighterTracer.cs:700`), not from a new parameter.
- **Confirmation-based producers** (`KafkaMessageProducer`, `RmqMessageProducer` ×2) — *coordinators* between broker and contract. They *know* the per-message wire topic (they hold `message`) and the publish-span context (captured from `Activity.Current` at send time), and *do* the raising of the enriched event. Kafka additionally *knows* the failed id (FR-8) and the produce error reason/code (FR-6, via the publisher).
- **`OutboxProducerMediator`** (controller). *Decides* what a confirmation failure means: warn-log, request a span, trip the breaker, leave un-dispatched. Owns the `false`-branch logic and the error-isolation boundary.
- **`IAmAnOutboxCircuitBreaker`** (service provider). *Does* the trip via `TripTopic(RoutingKey)` (`src/Paramore.Brighter/CircuitBreaker/IAmAnOutboxCircuitBreaker.cs:44`). Already the trip mechanism for the non-confirmation path; reused unchanged.
- **`BrighterTracer`** (service provider). *Does* span creation. Owns the standalone-failure-span method, following the existing `CreateProducerSpan`/`CreateSpan` conventions (its `ActivitySource` is named `BrighterSemanticConventions.SourceName = "Paramore.Brighter"`; `CreateSpan<TRequest>` already accepts `ActivityLink[]? links` at `BrighterTracer.cs:106`).

### Technology Choices

- **OpenTelemetry `ActivityLink` + standalone `Activity`**, created through `BrighterTracer` using the existing `ActivitySource`. `CreateSpan<TRequest>` and `CreateBatchSpan<TRequest>` already accept and pass an `ActivityLink[]` to `ActivitySource.StartActivity(..., links: links, ...)` (`BrighterTracer.cs:106, 132`), and `LinkSpans` already constructs `new ActivityLink(hs.Value.Context)` — so a failure span carrying `new ActivityLink(ctx)` matches an established pattern. Tag conventions reuse `BrighterSemanticConventions`: `MessageId = "messaging.message.id"`, `MessagingDestination = "messaging.destination.name"`, `MessagingOperationType`, and `ErrorType = "error.type"` to mark the publish-confirmation failure. Span kind: `Producer`, consistent with `CreateProducerSpan`.
- **Why a standalone span, not span enrichment (C-2).** The original producer span has already ended before the confirmation arrives, so there is no live span to enrich; reopening an ended span or attributing the failure to whatever `Activity.Current` happens to be active on the callback thread would be incorrect. A short-lived linked span is the OTel-idiomatic representation of an out-of-band signal correlated to a prior operation.

### Implementation Approach

**No producer `Send` signature change.** The `IAmAMessageProducerSync`/`Async` `Send`/`SendWithDelay`(`Async`) signatures are left exactly as they are. The publish span is sourced by the producer from `Activity.Current` (set to S1 by `CreateProducerSpan`; see "Capturing the context at dispatch" below and "Relationship to FR-10"), so the ~10 non-confirmation gateways and all existing call sites are untouched. This removes the all-gateways ripple and the optional-parameter-ordering hazard that a `RequestContext?` (or `ActivityContext?`) parameter on the async methods would have introduced — those methods already end in `CancellationToken cancellationToken = default`, and many in-tree callers pass the token positionally (e.g. `KafkaMessageConsumer.cs:696,766`; `OutboxProducerMediator.cs:908,978`), so any new parameter inserted ahead of it would have been a compile break.

**`OnMessagePublished` contract — before/after** (`src/Paramore.Brighter/ISupportPublishConfirmation.cs:42`):
```csharp
// before
event Action<bool, string> OnMessagePublished;
// after
event Action<PublishConfirmationResult> OnMessagePublished;   // record defined above
```

**Mediator false-branch logic** (added to both `ConfigureAsyncPublisherCallbackMaybe` `:737` and `ConfigurePublisherCallbackMaybe` `:764`). On `result.Success == false`:
```
try {
    var topic   = result.Topic;                         // wire topic; may be null -> TripTopic guards (FR-5)
    var id      = result.MessageId;                     // may be empty -> log "unknown" marker (FR-5)
    Log.<Warning>(id, topic);                            // FR-1/NFR-1
    var links   = result.PublishSpanContext is { } ctx  // FR-2 / AC-2 vs AC-2b
                    ? new[] { new ActivityLink(ctx) }
                    : null;
    using var s2 = _tracer?.Create<failure span>(id, topic, links);   // started+stopped in-callback (NFR-2)
    TripTopic(topic);                                    // FR-3 via existing :1168 (no-op if null/empty)
}
catch (Exception ex) { Log.<observability failed, swallowed>(ex); }    // NFR-4 / AC-15
```
No `MarkDispatched`, no awaited broker/Outbox call (AC-12b). The breaker trip is synchronous against in-memory state. The `success == true` branch is untouched (OOS-4, AC-13).

**Capturing the context at dispatch (C-7).** When the mediator creates the producer span S1, `CreateProducerSpan` sets `Activity.Current = S1` (`BrighterTracer.cs:700`) before `Send`/`SendAsync` is invoked. The producer reads `Activity.Current?.Context` — the *value* of S1's `ActivityContext` (a struct, valid after the span ends) — **as the first action inside `Send`/`SendAsync`, before any `await`**, and stashes that value alongside the per-message confirmation state. This satisfies AC-2's requirement that the link's `ActivityContext` **equal S1's context**.

Three correctness points this design depends on:

- **The source is `Activity.Current`, which is race-free; NOT the shared `producer.Span` field, and NOT `requestContext.Span`.** `producer.Span` (`IAmAMessageProducer.Span`, `IAmAMessageProducer.cs:41`) is a single mutable property on a **shared, reused** producer instance (one per topic in `ProducerRegistry`), and dispatch is **not serialized per producer**. Two threads dispatching to the same topic concurrently would race on `producer.Span` — thread A could read thread B's span and link the failure to the **wrong** publish span, which AC-2 forbids (FR-2 tolerates a *missing* link, AC-2b, but not a *wrong* one; NFR-3/AC-10 require correct behaviour under concurrent same-topic failures). `Activity.Current` avoids this because it is `AsyncLocal` — each dispatch thread sees its own value, set to S1 by `CreateProducerSpan` on that thread. `requestContext.Span` is also wrong: it holds the *parent/ambient* span (`:812`, restored to `parentSpan` in the `finally` at `:1007`), never the producer span.
- **The value must be captured *during* the call, never at callback time.** S1 is ended by `EndSpans` in the `finally` (and `Activity.Current` is reset — the sync path sets `Activity.Current = parentSpan` at `:857` and nulls `producer.Span` at `:858`; the async path restores `requestContext.Span` at `:1007` and relies on the next iteration's overwrite rather than nulling `producer.Span`). By the time the asynchronous confirmation fires, neither `Activity.Current` nor `producer.Span` still holds S1. Capturing the immutable `ActivityContext` value synchronously at the top of `Send` — the only window where `Activity.Current == S1` — and carrying it forward is therefore mandatory; the captured value is immutable and thread-safe to carry across the confirmation thread (NFR-3).
- **`Activity.Current` is not perturbed before the capture.** The OutboxProducer resilience pipeline that wraps the send (executed by `ExecuteWithResiliencePipeline`/`Async`, `OutboxProducerMediator.cs:1111-1160`) is the Brighter default pipeline defined in `ResiliencePipelineRegistryExtensions.AddBrighterDefault` (`src/Paramore.Brighter/Extensions/ResiliencePipelineRegistryExtensions.cs:57-67`) — a bare `AddRetry` with no telemetry/activity strategy, and Polly starts no `Activity` absent its OTel telemetry middleware — so `Activity.Current` remains S1 from `CreateProducerSpan` through to the first line of `Send`/`SendAsync`. (The pipeline is registry-keyed and user-overridable; this holds for the Brighter default.) The producer MUST read it before doing any work of its own that could start a child activity or `await`.

When `Activity.Current` is `null` (tracing disabled, or no listener so `StartActivity` returned null), the captured context is `default`/absent and FR-2 degrades to no link (AC-2b), while FR-1/FR-3 are unaffected (the wire topic comes from `message`, always present).

**Kafka FR-6** (`src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessagePublisher.cs:52`). The catch currently binds no variable:
```csharp
// before
catch (ProduceException<string, byte[]>)
// after
catch (ProduceException<string, byte[]> pe)
{
    Log.<Warning>(pe.Error.Reason, pe.Error.Code);   // FR-6, Warning level (NFR-1)
    ... // existing synthetic NotPersisted DeliveryResult + deliveryReport(...) unchanged (FR-7)
}
```
The synthetic result still writes `MESSAGE_ID` to `deliveryResult.Headers` and still invokes the callback (FR-7). This is the correctly-typed catch; the wrong-typed `<string, string>` catches at `KafkaMessageProducer.cs:262, 336` are OOS-2 and left untouched.

**Kafka FR-8** (`KafkaMessageProducer.cs:364-384`). `PublishResults` reads `MESSAGE_ID` only on the `Persisted` branch; the `NotPersisted` fall-through hardcodes `OnMessagePublished?.Invoke(false, string.Empty)`. Change the failure branch to read `MESSAGE_ID` from the same report-level `headers` collection the success branch uses — **not** `result.Message.Headers`, which the publisher leaves empty (`KafkaMessagePublisher.cs:58-60`) — falling back to empty when genuinely absent (AC-8). The id (and the wire topic + captured context) then populate the enriched `PublishConfirmationResult` that is raised. The callback continues to be marshalled via `Task.Run` (`:373, :381`), preserving NFR-2's broker-thread isolation.

**RMQ FR-9 + FR-2.** The id is already supplied on nack: `OnMessagePublished?.Invoke(false, messageId)` (`RmqMessageProducer.cs:480`, async; the `RMQ.Sync` project is analogous), with a pre-existing Debug log `FailedToPublishMessageAsync` (`:481, :512-513`) that may remain (it is not a Warning, so AC-1/AC-9's "exactly one Warning" still holds). For the id, RMQ stays verify-only. RMQ is still **not** verify-only for FR-2 — but the change is confined to the **raise site**, not the `Send` signature: inside its `Send`/`SendAsync` the RMQ producer captures `Activity.Current?.Context` and `message.Header.Topic`, carries them through its delivery-tag → message tracking, and surfaces them in the enriched `PublishConfirmationResult` raised at `:480`. This is not entirely free: the existing `_pendingConfirmations` map is `Dictionary<ulong, string>` (deliveryTag → messageId; `RmqMessageProducer.cs:60`, `ConcurrentDictionary` in RMQ.Sync). Rather than smear a loose tuple of fields across the tracking code, the map's value type becomes a **named record** holding exactly the metadata the failure callback needs to build the result:

```csharp
private readonly Dictionary<ulong, PendingConfirmation> _pendingConfirmations = new();

// internal/private to the RMQ producer (NOT a public API type like PublishConfirmationResult)
internal readonly record struct PendingConfirmation(Id MessageId, RoutingKey Topic, ActivityContext? Context);
```

`AddPendingConfirmation` stashes the message id, the wire topic (`message.Header.Topic`) and the `Activity.Current?.Context` captured at the top of `Send`/`SendAsync`; `OnPublishFailed` looks the entry up by delivery tag and builds the `PublishConfirmationResult` from it. This keeps the delivery-tag map focused and intention-revealing instead of polluted with positional fields — and the map is genuinely the right home for this metadata, because it is the **only bridge** between RabbitMQ's delivery-tag-based, asynchronous, batched-confirm protocol and Brighter's message identity + telemetry context. `PendingConfirmation` is internal to the producer, so (unlike the public `PublishConfirmationResult`) it carries no public-API / XML-doc / versioning obligation. The change touches `AddPendingConfirmation` (`:397`), `RemovePendingConfirmations`/`RemoveConfirmationsLocked` (`:419`, lookup at `:447`) and `OnPublishFailed` (`:476`); sync analogues at `:153, :233`. Both RMQ projects change at the raise (and in the map value type), but neither changes its `Send` signature. **Capture-ordering caveat:** in `SendWithDelayAsync` the existing body awaits `EnsureBrokerAsync` (`:167`) *before* it reaches `WriteProducerEvent`/`AddPendingConfirmation`, so the `Activity.Current` capture must be placed at the very top of the method — above `BeginSend()`/the broker-ensure await — not co-located with the per-message tracking, to honour the "before any `await`" invariant.

**Degradation path.** `PublishConfirmationResult.PublishSpanContext == null` ⇒ no `ActivityLink`; the standalone span, Warning log, and breaker trip are unconditional (FR-2 wording; AC-2b). `MessageId` empty ⇒ logged as an explicit "unknown" marker; breaker trips on the wire topic regardless (FR-5; AC-5/AC-8). Wire topic `null`/empty ⇒ `TripTopic`'s guard (`:1170`) makes the trip a safe no-op.

**Thread safety (NFR-3).** Callbacks may run concurrently and off the dispatch thread. The breaker (`IAmAnOutboxCircuitBreaker` implementation) must remain safe under concurrent `TripTopic`; logging via the source-generated `ILogger` is thread-safe; `ActivitySource.StartActivity` is thread-safe. We do not read the thread-keyed `RequestContext.Span` on the callback thread — only the immutable captured `ActivityContext` value — so there is no cross-thread state hazard from the context flow.

**Error isolation (NFR-4 / AC-15).** The observability block (log + span) is wrapped so that an exception there is caught and logged but does not propagate out of the callback, and the breaker trip (FR-3) is ordered/guarded to still run. The producer's invoking/thread-pool thread is never destabilised.

**Tidy-first note (C-7 "Enabled cleanup" — NOT enabled by this revision).** C-7 anticipated that *flowing* a `RequestContext` into the producer might make the empty construction-time `RequestContext` captured in `ConfigureCallbacks` (`:162`) removable. Because the revised mechanism sources the span from `Activity.Current` and does **not** flow a `RequestContext`, that cleanup is **not** enabled here: the construction-time `RequestContext` stays, still consumed by the success-path `MarkDispatched`/`MarkDispatchedAsync` (`:749, :776`, C-6). No tidy-first change is in scope for this work.

## Consequences

### Positive
- Confirmation failures become observable for both Kafka and RMQ: a Warning log (FR-1), a standalone linked OTel span navigable back to the originating publish (FR-2), and a circuit-breaker trip (FR-3) — closing all four issue gaps with one shared mediator change plus targeted gateway changes.
- Exact behavioural parity with the non-confirmation `!sent` path: same `TripTopic`, same wire-topic argument, including reply/rewritten-topic messages (NFR-6, AC-3/AC-3b).
- Kafka's previously-swallowed produce reason/code is logged (FR-6) and the failed id is no longer discarded (FR-8).
- Safe-retry semantics preserved: message stays un-dispatched, nothing bubbles, Sweeper still retries (FR-4, C-1).
- The `PublishConfirmationResult` record is a clean RDD information holder, future-extensible without further signature breaks, and self-documenting at call sites.
- **Minimal blast radius.** Sourcing the span from `Activity.Current` means no `Send`-signature change, so the ~10 non-confirmation gateways and every existing `Send`/`SendAsync` call site are untouched. Only the two confirmation producers (Kafka, RMQ) change. This is strictly smaller than the originally-proposed FR-10 mechanism while producing the identical observable outcome.
- End users see no API change.

### Negative
- **Source- and binary-breaking for confirmation-producer implementors.** Changing `OnMessagePublished` from `Action<bool, string>` to `Action<PublishConfirmationResult>` breaks any in-tree or third-party gateway that implements `ISupportPublishConfirmation` and raises the event; they must update their raise sites, the mediator's two handlers must be rewritten to the new delegate shape (behaviour-preserving on the success branch), and precompiled assemblies referencing the old delegate type will fail to bind until recompiled. End users and non-confirmation gateways are unaffected, but this is a real break for the confirmation-gateway-author audience and must be called out in release notes. (This is the *only* breaking surface; the originally-feared all-gateways ripple is avoided — see Positive.)
- **RMQ is not verify-only for FR-2.** The RMQ producers must capture `Activity.Current`/topic and surface them in the enriched raise, replacing the `_pendingConfirmations` map value with a named internal `PendingConfirmation` record; only the id handling (FR-1/3/4) stays verify-only. The change is confined to the raise site + map, not the `Send` signature.
- **Coupling to `Activity.Current` at dispatch and a narrow capture window.** The design relies on `CreateProducerSpan` having set `Activity.Current = S1` before `Send` and on the producer capturing `Activity.Current?.Context` synchronously as the first action, before any `await` or any work that could start a child activity. If a future change started an intervening activity between span creation and the `Send` body (e.g. an activity-emitting resilience strategy on the OutboxProducer pipeline), or a producer read `Activity.Current` after an `await`, the captured context would be wrong or null. This invariant must be documented at both ends and pinned by tests (AC-2). Note this is a *narrow same-thread ordering* invariant, not a cross-thread race — `Activity.Current` is `AsyncLocal`, so concurrent dispatch to the same producer does not corrupt it (the reason the shared `producer.Span` field was rejected as the source).
- **New public type to document and version.** `PublishConfirmationResult` is an additive public type requiring XML docs on the type and all members, and it becomes part of the versioned public API surface.
- **New obligations on the failure path.** Thread-safety (NFR-3) and error-isolation (NFR-4) become standing requirements on the mediator's callback and the breaker implementation; getting either wrong reintroduces the very instability this change is meant to avoid.

### Risks and Mitigations
- *Reading the wrong Kafka header collection* (FR-8): reading `result.Message.Headers` yields empty because the publisher writes only to `deliveryResult.Headers` (`KafkaMessagePublisher.cs:58-60`). Mitigation: read the report-level `headers` the success branch already uses; pin with AC-7/AC-8 tests.
- *Capturing the span context at the wrong moment, or from the wrong source, returns the wrong span or null.* The shared `producer.Span` field races under concurrent same-topic dispatch (rejected); `requestContext.Span` is the parent (rejected). Mitigation: capture `Activity.Current?.Context` (`AsyncLocal`, race-free) as the **first action** inside `Send`/`SendAsync`, before any `await` or child-activity creation, while `Activity.Current == S1`; carry the immutable `ActivityContext` value, never re-read it at callback time. Pin with AC-2 (link equals S1), AC-2b (degrades to no link), and AC-10 (concurrent same-topic failures link to the correct respective spans).
- *Observability throw destabilises the producer thread* (AC-15). Mitigation: wrap observability work; prioritise breaker trip + Warning log.
- *Mis-tripping `Publication.Topic` instead of the wire topic for reply messages.* Mitigation: always use `result.Topic` sourced from `message.Header.Topic`; pin with AC-3b.
- *Span left open across an await* (NFR-2). Mitigation: create and dispose the standalone span synchronously inside the callback; add no new awaited broker/Outbox call on the failure path (AC-12/AC-12b).

## Alternatives Considered

**(b) `messageId → ActivityContext` correlation map.** Populate a shared map in `Dispatch`/`DispatchAsync` keyed by message id, and read it in the callback. *Genuine appeal*: no contract change, so no all-gateways ripple and no source break. *Why rejected*: it introduces shared mutable state across the dispatch thread and the gateway-dependent callback threads (NFR-3), requiring its own concurrency control and — critically — a lifecycle/eviction policy. Confirmations are asynchronous and may never arrive (a permanently failed broker), so entries leak unless evicted on a timer or TTL, which races against late-arriving confirmations and can either drop a still-valid context or evict too late. It also still does not solve C-8 cleanly: the wire topic would have to be co-stored in the map, duplicating knowledge the producer already holds in `message`. It trades a one-time, compile-time-checked contract break for an ongoing runtime correctness hazard.

**(c) Header propagation / read trace context back from message headers.** Write the publish span's trace context into the message headers (Brighter already round-trips `TraceParent`), and have the callback reconstruct the `ActivityContext` from the message. *Genuine appeal*: leans on an existing W3C-traceparent mechanism; no producer signature change for the context. *Why rejected*: the callback (per C-6) does not capture the `message`, so the headers are not reachable there without *also* plumbing the message to the callback — i.e. it still needs a contract or map change, so it does not actually avoid the cost. It conflates the *consumer-side* parent context (what `traceparent` is for) with an *internal* producer→confirmation link, risking incorrect parenting of downstream consumer spans. And it only addresses the trace context, leaving C-8's wire-topic plumbing unsolved. Mechanism (a) carries both the context and the wire topic through one explicit, type-checked channel.

## References

- Requirements: [specs/0034-failed-delivery-context/requirements.md](../../specs/0034-failed-delivery-context/requirements.md)
- Related ADRs: none — this is the first and only ADR for this spec.
- External references:
  - OpenTelemetry span links: https://opentelemetry.io/docs/concepts/signals/traces/#span-links
  - .NET `System.Diagnostics.ActivityLink` / `ActivityContext`: https://learn.microsoft.com/dotnet/api/system.diagnostics.activitylink

### Critical Files for Implementation
- `src/Paramore.Brighter/OutboxProducerMediator.cs` — mediator callback handlers (rewritten to the new delegate shape; failure branch gains log + span + breaker trip), `TripTopic` at `:1168`
- `src/Paramore.Brighter/Observability/BrighterTracer.cs` — `CreateProducerSpan` sets `Activity.Current = S1` (`:700`, the capture source); standalone failure-span helper using `ActivityLink`
- `src/Paramore.Brighter/ISupportPublishConfirmation.cs` — `OnMessagePublished` delegate change to `Action<PublishConfirmationResult>`
- `src/Paramore.Brighter/PublishConfirmationResult.cs` *(new)* — the public result record (with XML docs)
- `IAmAMessageProducerSync`/`IAmAMessageProducerAsync` — **no signature change** (`Activity.Current`, not a `Send` parameter, carries the span)
- `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessagePublisher.cs` and `KafkaMessageProducer.cs` — FR-6 logging, FR-8 id propagation, capture `Activity.Current?.Context` in `Send`/`SendAsync` (delivery-report closure), enriched raise
- `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageProducer.cs` and `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageProducer.cs` — capture `Activity.Current?.Context`/topic, introduce internal `PendingConfirmation` record as the `_pendingConfirmations` value, enriched raise (no `Send`-signature change)
