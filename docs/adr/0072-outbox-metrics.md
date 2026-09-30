---
id: 0072-outbox-metrics
title: "Outbox Metrics"
status: Proposed
author:
  - "taha-rafique"
created: 2026-09-28
summary: "Outbox metrics are recorded by direct OpenTelemetry instruments (paramore.brighter.outbox.added.messages and .cleared.messages as Counter<long>, .publish.duration as a Histogram<double> in seconds with explicit long-tail buckets) behind a new IAmABrighterOutboxMeter/OutboxMeter role owned by OutboxProducerMediator rather than derived from traces, with the clear kind carried as an OutboxClearSource parameter on the private dispatch methods and confirmation-path creation time resolved from a bounded, TTL-evicted in-mediator pending-clear ConcurrentDictionary."
tags:
  - "observability"
  - "metrics"
  - "otel"
  - "outbox"
---

# 0072. Outbox Metrics

Date: 2026-09-28

## Status

Proposed

## Context

**Parent Requirement**: [specs/0037-outbox-metrics/requirements.md](../../specs/0037-outbox-metrics/requirements.md)

**Scope**: This ADR focuses specifically on the complete design of Outbox metrics: the instrument set (names, types, units, attributes and their cardinality bound), the recording mechanism, the meter role and its implementation, how the clear kind reaches the point of recording, and how creation time reaches the asynchronous confirmation callback. It is the only ADR for spec 0037.

### The problem

Today the Outbox's only observability signal is trace spans and span events. `BrighterTracer.WriteOutboxEvent` (`src/Paramore.Brighter/Observability/BrighterTracer.cs:951`) opens with:

```csharp
if (span == null) return;
```

and the add path passes `requestContext.Span`, which is frequently null. Combined with production tail/ratio sampling, this means the add path leaves no aggregate record at all. Spans also cannot answer the rate/ratio questions an operator needs ("what fraction of my volume is the sweeper rescuing?", "what is p99 publish lag?"). Incident PI-36093 (NOFR events emitted after significant Outbox lag, causing `IgnoredOrders`) was invisible for exactly this reason, and RLOI-4186 is blocked on having chartable, alertable signals.

### Forces

1. **FR-9 forbids any dependency on tracing or sampling.** Recording must happen when no `IAmABrighterTracer` is registered, when `requestContext.Span` and `Activity.Current` are null, when the sampler drops the trace, and when `InstrumentationOptions` is `None`.
2. **The two clear kinds converge (C-5).** `ClearOutbox`/`ClearOutboxAsync` (explicit) and `ClearOutstandingFromOutboxAsync` → `BackgroundDispatchUsingAsync` (sweeper) both reach the same private `Dispatch` (`OutboxProducerMediator.cs:1035`), `BulkDispatchAsync` (`:1092`) and `DispatchAsync` (`:1175`). FR-4's sweeper attribution is impossible at the point of recording unless the initiating context is carried down.
3. **Confirmation-based clears are asynchronous and data-poor (C-4).** For a producer implementing `ISupportPublishConfirmation`, `MarkDispatched` happens later, on a broker/threadpool thread, inside `HandleAsyncPublishConfirmation` (`:860`) or the `ConfigurePublisherCallbackMaybe` delegate (`:952`). The callback receives a `PublishConfirmationResult(bool Success, Id MessageId, RoutingKey? Topic, ActivityContext? PublishSpanContext)` — no `Message`, so no `Header.TimeStamp`.
4. **Bulk dispatch marks dispatched by id (C-8).** `BulkDispatchAsync` iterates `batch.Ids()` (`IEnumerable<Id>`, `MessageBatch.Ids()`), while the `Message` objects are one level up in `var messages = topicBatch.ToArray()` (`:1126`).
5. **Zero cost when nobody is listening (NFR-1), no behavioural change (NFR-2), faults isolated (NFR-3), thread safe (NFR-4).** The mediator is documented as a process singleton (`OutboxProducerMediator.cs:45`) whose sweeper dispatch (`BackgroundDispatchUsingAsync`, holding `_backgroundClearSemaphore`) runs concurrently with request-thread adds and with confirmation callbacks on producer threads.
6. **All TFMs (NFR-5).** `Paramore.Brighter` targets `netstandard2.0;net8.0;net9.0;net10.0` (`src/Directory.Build.props:43`).
7. **No public API break, no Outbox-implementation change, no schema change (C-11, C-12).**

### Constraint that shapes the mechanism choice

`BrighterMetricsFromTracesProcessor` is a `BaseProcessor<Activity>` whose `OnEnd` reads `activity.GetTagItem(BrighterSemanticConventions.InstrumentationDomain)` on **ended, sampled** activities. Nothing it can observe exists on the Outbox add path (an `ActivityEvent` on a possibly-null span is not an `Activity`), and every span it could observe on the clear path is subject to the sampler. It is therefore structurally incapable of satisfying FR-9 for the Outbox.

## Decision

Record Outbox metrics **directly on instruments** created on a `Meter` named `BrighterSemanticConventions.MeterName` (`"Paramore.Brighter"`, `BrighterSemanticConventions.cs:97`), behind a new role `IAmABrighterOutboxMeter`, called from `OutboxProducerMediator` at the exact points where the Outbox store confirms the write or the dispatch.

### Architecture Overview

```
 Application                    OutboxProducerMediator                        OutboxMeter
 ───────────                    ──────────────────────                        ───────────
 AddToOutbox(Async) ──────────► write succeeds ─────────► RecordAdded ──────► added.messages   (Counter<long>)
 EndBatchAddToOutbox(Async) ──► write succeeds, per msg ► RecordAdded ──────►        │
                                                                                     │
 ClearOutbox(Async) ──────────► Dispatch / DispatchAsync (clearSource: Explicit)     │
 OutboxSweeper.SweepAsync ────► ClearOutstandingFromOutboxAsync                      │
                                 └► BackgroundDispatchUsingAsync                     │
                                      ├► DispatchAsync     (clearSource: Sweeper)    │
                                      └► BulkDispatchAsync (clearSource: Sweeper)    │
                                              │                                      │
                     non-confirmation producer │ MarkDispatched succeeded             ▼
                                              └────────────► RecordCleared ──► cleared.messages (Counter<long>)
                                                                  │            publish.duration (Histogram<double>)
                     confirmation producer                        │
                        send ──► _pendingClears[id] = {TimeStamp, Topic, clearSource}
                        broker ──► OnMessagePublished(result)
                                  MarkDispatched succeeded
                                  _pendingClears.TryRemove(id) ────┘
```

The meter is a **collaborator of the mediator**, not of the trace pipeline. `BrighterMetricsFromTracesProcessor` is untouched and continues to own the messaging and db signals it already derives (ADR 0022 remains in force).

### 1. Instrument names, types and units (proposal, C-7)

No OpenTelemetry semantic convention covers an outbox. The nearest existing convention, `messaging.client.sent.messages`, is defined as "number of messages producer **attempted to send to the broker**" and is already emitted by `MessagingMeter` (`MessagingMeter.cs:54`); reusing it for Outbox adds would conflate store writes with broker sends and double-count. `db.client.operation.duration` describes the duration of one store call, not a wait between two events. We therefore mint `paramore.brighter.*` names, as FR-10 and `BrighterSemanticConventions` prescribe for the no-convention case.

| Instrument | Type | Unit (UCUM) | Description |
|---|---|---|---|
| `paramore.brighter.outbox.added.messages` | `Counter<long>` | `{message}` | Number of messages written to the Outbox. |
| `paramore.brighter.outbox.cleared.messages` | `Counter<long>` | `{message}` | Number of messages recorded as dispatched from the Outbox. |
| `paramore.brighter.outbox.publish.duration` | `Histogram<double>` | `s` | Time a message waited between being created for the Outbox and being recorded as dispatched. |

Notes and deliberate deviations, to be confirmed in `#interest-oss-brighter`:

- **`.duration`, not `.latency`.** The requirements' illustrative name was `paramore.brighter.outbox.publish.latency`. OpenTelemetry metric naming guidance uses `duration` for timings (`messaging.client.operation.duration`, `db.client.operation.duration`, `messaging.process.duration` — all three already in `MessagingMeter`/`DbMeter`). `...publish.latency` is an acceptable fallback if maintainers prefer the spec's wording; nothing else in this design depends on the choice.
- **`Counter<long>`, not `Counter<int>`.** `MessagingMeter` uses `Counter<int>`. The difference is cosmetic — the OTel SDK aggregates both into a 64-bit sum — but `long` is the OTel .NET recommendation for monotonic counts and removes any question about a long-lived publisher's cumulative total. Reverting to `Counter<int>` for house consistency is an acceptable alternative.
- **Instrument names live as `public const` on `BrighterSemanticConventions`** (`OutboxAddedMessages`, `OutboxClearedMessages`, `OutboxPublishDuration`), unlike `MessagingMeter`'s inline literals, because RLOI-4186 needs a stable, referenceable identifier and AC-19 tests assert against constants.

### 2. Attribute keys and value domains

| Key | Constant | Value domain | Applies to |
|---|---|---|---|
| `messaging.destination.name` | `BrighterSemanticConventions.MessagingDestination` (existing) | `message.Header.Topic.Value`, or `RoutingKey.Empty.Value` (`""`) when the confirmation callback cannot supply a topic | all three |
| `paramore.brighter.outbox.clear_source` | **new** `BrighterSemanticConventions.OutboxClearSource` | `"explicit"`, `"sweeper"`, `"unknown"` | cleared counter, duration histogram |

`messaging.destination.name` is the OTel messaging convention key and is already in `MessagingMeter`'s allowed-tag sets, so it satisfies FR-10's "OpenTelemetry semantic-convention keys where one exists". `snake_case` in `paramore.brighter.outbox.clear_source` matches the existing `paramore.brighter.outbox.shared_transaction`.

**No `error.type` / outcome dimension.** FR-8 means only successes are counted, so an outcome dimension would be constant. Failures remain observable via `messaging.client.sent.messages` + `error.type`, logs and circuit-breaker trips (OOS-3).

**Service-resource attributes are not duplicated as data-point attributes.** `MessagingMeter`/`DbMeter` append `meterProvider.GetServiceAttributes()` (`MeterProviderExtensions.cs:37`) because they are constructed *inside* a `MeterProvider`'s service collection. The mediator is not — `BrighterTracerBuilderExtensions` has to probe `services.Any(sd => sd.ServiceType == typeof(IAmABrighterMessagingMeter))` precisely because those registrations are scoped to the builder's own container, which the application container (and hence the mediator) does not see when the host uses `Sdk.CreateMeterProviderBuilder()`. For a directly instrumented metric the SDK attaches the `Resource` to every exported data point anyway, so `service.*` is already present downstream. `OutboxMeter` therefore takes `MeterProvider?` and appends `GetServiceAttributes()` **only** when a host explicitly supplies one, preserving parity for anyone who wants it.

**Cardinality arithmetic (AC-20, NFR-6).** With `D` configured destinations:

- added counter: `D` series.
- cleared counter: `≤ (D + 1) × 3` series (the `+1` is the empty-destination fallback; the `× 3` is the enumerated clear source). In normal operation `"unknown"` and `""` never appear, so `D × 2 = 6` for `D = 3`, within AC-20's `3 × (dimension values)` bound.
- duration histogram: the same `≤ (D + 1) × 3` series, each with 19 buckets + `+Inf`.

Total upper bound `≤ 7 × (D + 1)` series, independent of message count. No dimension derives from per-message data.

### 3. Histogram bucket strategy

Publish lag spans milliseconds (inline dispatch) to minutes (sweeper rescue after a restart — AC-14 expects a 300 s sample). The OTel SDK default explicit boundaries are `[0, 5, 10, 25, 50, 75, 100, 250, 500, 750, 1000, 2500, 5000, 7500, 10000]`, unit-agnostic numbers: with a **seconds** instrument, every healthy sample collapses into the single `(0, 5]` bucket, destroying p50/p95 resolution exactly where operators need it.

We therefore supply explicit boundaries via instrument advice:

```csharp
private static readonly double[] s_publishDurationBoundaries =
[
    0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1,
    2.5, 5, 7.5, 10, 30, 60, 300, 600
];
```

The first fourteen are the OTel-recommended duration boundaries (as used for `db.client.operation.duration` and `messaging.client.operation.duration`); `30, 60, 300, 600` extend the tail for sweeper rescue. AC-14's 300 s sample lands on the `300` boundary; anything beyond 600 s lands in `+Inf`.

**Per-TFM availability — verified.** `System.Diagnostics.Metrics.InstrumentAdvice<T>` and `Meter.CreateHistogram<T>(name, unit, description, tags, advice)` were introduced in .NET 9, but `Paramore.Brighter` resolves `System.Diagnostics.DiagnosticSource` **10.0.11** on every target (pinned in `Directory.Packages.props:154`, resolved transitively via `OpenTelemetry` 1.18.0), and the package's `lib/netstandard2.0` build contains `InstrumentAdvice` and `HistogramBucketBoundaries`. So **no `#if` is required** and no TFM loses the buckets. Honouring advice requires OTel SDK ≥ 1.9.0; the repo pins 1.18.0.

Two consequences to accept:

- `Paramore.Brighter.csproj` gains an explicit `<PackageReference Include="System.Diagnostics.DiagnosticSource" />` (version already centrally pinned) so this becomes an intentional dependency rather than a transitive accident that a later `OpenTelemetry` bump could downgrade.
- If maintainers reject raising that floor, the fallback is `#if NET9_0_OR_GREATER` around the advice with no advice on `netstandard2.0`/`net8.0`, and operator documentation of the equivalent view: `.AddView("paramore.brighter.outbox.publish.duration", new ExplicitBucketHistogramConfiguration { Boundaries = [...] })`. This is a documented degradation, not a silent one.

### 4. Unit of the latency sample: seconds

The instrument records **seconds** (`unit: "s"`), consistent with every existing Brighter duration instrument and with OTel's preference for base units. The requirements state examples in milliseconds; the acceptance criteria assert on the *sample value*, so they are satisfied by the seconds-equivalent:

| AC | Requirement wording | Assertion in this design |
|---|---|---|
| AC-13 | one sample of 250 ms | one sample of `0.25` |
| AC-14 | one sample of 300,000 ms | one sample of `300` |
| AC-15 | one sample of 0 | one sample of `0` |

Tests assert via `TimeSpan` on the spy double (`TimeSpan.FromMilliseconds(250)`), so the millisecond wording of the spec is preserved at the test level; only the exported instrument is in seconds.

### 5. Key Components

#### 5.1 `IAmABrighterOutboxMeter` — a new role

```csharp
namespace Paramore.Brighter.Observability;

public interface IAmABrighterOutboxMeter
{
    /// <summary>Record one message written to the Outbox.</summary>
    void AddMessageAdded(RoutingKey destination);

    /// <summary>Record one message recorded as dispatched from the Outbox.</summary>
    void AddMessageCleared(RoutingKey destination, OutboxClearSource clearSource);

    /// <summary>Record how long a cleared message waited in the Outbox.</summary>
    void RecordPublishDuration(TimeSpan wait, RoutingKey destination, OutboxClearSource clearSource);

    /// <summary>False when no MeterListener is attached to any instrument.</summary>
    bool Enabled { get; }
}
```

**Responsibilities.** *Knowing*: the instrument identities (name, type, unit, description, bucket boundaries) and any service-resource attributes it was given. *Doing*: turning a `(destination, clearSource, value)` triple into one instrument recording. *Deciding*: nothing beyond `Enabled` — whether an event is countable is the mediator's decision, because only the mediator knows whether the store accepted the write. Stereotype: service provider.

**Contract.** `destination` may be `RoutingKey.Empty`; `clearSource` is always one of the three enumerated values; `wait` is expected non-negative (callers clamp per FR-7a) and an implementation receiving a negative value must record `0` rather than throw. **No member may throw** — see §7 for why the mediator nonetheless guards the call site.

**Why a new role rather than extending `IAmABrighterMessagingMeter`.** Every member of that interface takes an `Activity` (`IAmABrighterMessagingMeter.cs:37-55`); its single responsibility is *translating an ended Brighter activity into OTel messaging metrics*, and its only collaborator is `BrighterMetricsFromTracesProcessor`. FR-9 makes Outbox recording activity-free, so adding activity-free members would give the type two incompatible collaboration contracts, and would force the existing `SpyMessagingMeter` to grow members it cannot meaningfully implement — editing a double that AC-23 wants left alone. Different collaborator, different data, different trigger ⇒ different role.

#### 5.2 `OutboxMeter`

```csharp
public sealed class OutboxMeter(MeterProvider? meterProvider = null) : IAmABrighterOutboxMeter, IDisposable
```

- Creates `new Meter(BrighterSemanticConventions.MeterName)`, held in a readonly field and disposed in `Dispose()`.
- `_serviceAttributes = meterProvider?.GetServiceAttributes() ?? []`.
- `Enabled => _addedMessages.Enabled || _clearedMessages.Enabled || _publishDuration.Enabled` — the same disjunction as `MessagingMeter.Enabled` (`MessagingMeter.cs:186`).
- Builds tags with `System.Diagnostics.TagList` (available on all TFMs, verified in the `netstandard2.0` lib), whose eight inline slots hold destination + clear source + up to four service attributes with **zero heap allocation**, and calls the `Add(T, in TagList)` / `Record(T, in TagList)` overloads. This is why **no `#if NET8_0_OR_GREATER` is needed**: unlike `MessagingMeter`/`DbMeter`, which filter an `Activity`'s tags through a `FrozenSet` allow-list, `OutboxMeter` constructs its tags directly and needs no allow-list at all. (NFR-5 is satisfied by not using any TFM-gated API, rather than by conditional compilation.)
- Each method body is wrapped in `try`/`catch` logging through a source-generated `Log` partial (ADR 0026), honouring the "must not throw" contract.

#### 5.3 `OutboxClearSource`

```csharp
namespace Paramore.Brighter.Observability;

public enum OutboxClearSource { Explicit, Sweeper, Unknown }

public static class OutboxClearSourceExtensions
{
    public static string ToClearSourceName(this OutboxClearSource clearSource) => clearSource switch
    {
        OutboxClearSource.Explicit => "explicit",
        OutboxClearSource.Sweeper  => "sweeper",
        _                          => "unknown"
    };
}
```

This mirrors `MessagingSystemExtensions.ToMessagingSystemName` / `DbSystemExtensions` exactly, except that the default arm returns `"unknown"` instead of throwing `ArgumentOutOfRangeException`: on a metrics path an unmapped value must degrade, not throw (NFR-3).

Regarding ADR 0019: a closed two-and-a-half-state enumeration whose only job is to select one of three interned strings is what an `enum` is for; a record struct would add a type without necessity. The enum is a domain type, not a primitive, so it is not the primitive obsession ADR 0019 targets.

Placed in `Paramore.Brighter.Observability` for cohesion with `MessagingSystem`/`DbSystem` (enums-as-attribute-domains). It could arguably live in the `Paramore.Brighter` root as a dispatch concept; observability is the only consumer, so cohesion wins.

#### 5.4 Meter ownership and lifetime

`OutboxProducerMediator`'s constructor gains a trailing optional parameter:

```csharp
IAmABrighterOutboxMeter? outboxMeter = null
```

with `_outboxMeter = outboxMeter ?? new OutboxMeter();` and `_ownsOutboxMeter = outboxMeter is null;`. This reuses the `_ownsRegistry` / `_ownsTransformerFactories` pattern already in this class and satisfies ADR 0069 directly: `Dispose(bool)` calls `DisposeQuietly(_outboxMeter)` only when `_ownsOutboxMeter`, and additionally calls `_pendingClears.Clear()`.

Mediator-owned-by-default is what makes FR-11/AC-21 work with **no DI at all**: instruments are published globally by `System.Diagnostics.Metrics`, and a `MeterProvider` that has called `AddMeter("Paramore.Brighter")` — which is exactly what `AddBrighterInstrumentation()` does (`BrighterMetricsBuilderExtensions.cs:41`) — attaches its listener to instruments on any `Meter` of that name, including ones created after the provider was built. No new registration call, no new package reference, no flag.

Two call-site consequences, both verified:

- `ServiceCollectionExtensions.BuildOutBoxProducerMediator` (`:760`) uses `Activator.CreateInstance` with explicit positional arguments and its own comment notes that "Activator.CreateInstance does not apply constructor default values". It **must** be updated to pass a new trailing argument `OutboxMeter(serviceProvider)`, a private helper returning `serviceProvider.GetService<IAmABrighterOutboxMeter>()` in the style of the existing `Tracer(serviceProvider)` / `OutboxCircuitBreaker(serviceProvider)` (`:926`, `:931`). This lets a host that wires OTel through DI share one meter instance; when nothing is registered it returns null and the mediator owns its own.
- `ControlBusSenderFactory.cs:61` and `ControlBusReceiverBuilder.cs:177` use `new OutboxProducerMediator<Message, CommittableTransaction>(...)` with C# optional-argument binding and need no change.

This is not a public-interface change: `IAmAnOutboxProducerMediator` and the `IAmAnOutbox*` hierarchy are untouched, and adding a trailing optional constructor parameter is source-compatible (C-11).

### 6. Clear-kind plumbing

`OutboxClearSource` travels as an explicit parameter on the private dispatch methods:

```csharp
private void Dispatch(
    IEnumerable<Message> posts,
    RequestContext requestContext,
    OutboxClearSource clearSource,
    Dictionary<string, object>? args = null)

private async Task DispatchAsync(
    IEnumerable<Message> posts,
    RequestContext requestContext,
    OutboxClearSource clearSource,
    bool continueOnCapturedContext,
    CancellationToken cancellationToken)

private async Task BulkDispatchAsync(
    IEnumerable<Message> posts,
    RequestContext requestContext,
    OutboxClearSource clearSource,
    bool continueOnCapturedContext,
    CancellationToken cancellationToken)
```

There are exactly four internal call sites, all inside `OutboxProducerMediator.cs`, and no callers outside it:

| Line | Current call | Becomes |
|---|---|---|
| 398 (`ClearOutbox`) | `Dispatch([message], requestContext, args)` | `Dispatch([message], requestContext, OutboxClearSource.Explicit, args)` |
| 467 (`ClearOutboxAsync`) | `DispatchAsync([message], requestContext, continueOnCapturedContext, cancellationToken)` | `DispatchAsync([message], requestContext, OutboxClearSource.Explicit, continueOnCapturedContext, cancellationToken)` |
| 749 (`BackgroundDispatchUsingAsync`) | `BulkDispatchAsync(messages, requestContext, false, cancellationToken)` | `BulkDispatchAsync(messages, requestContext, OutboxClearSource.Sweeper, false, cancellationToken)` |
| 753 (`BackgroundDispatchUsingAsync`) | `DispatchAsync(messages, requestContext, false, cancellationToken)` | `DispatchAsync(messages, requestContext, OutboxClearSource.Sweeper, false, cancellationToken)` |

`BackgroundDispatchUsingAsync`, `ClearOutbox`, `ClearOutboxAsync`, `ClearOutstandingFromOutboxAsync` and `OutboxSweeper` keep their signatures unchanged: the sweeper kind is already implied by entering through `ClearOutstandingFromOutboxAsync`, so `OutboxSweeper.cs` needs no edit at all.

**Why a parameter.** It is compile-checked (a new dispatch path cannot forget it), introduces no shared mutable state (so no thread-safety question — NFR-4), changes no public API (C-11), and changes no dispatch behaviour (NFR-2).

**Rejected: stamping `RequestContext.Bag`.** Implicit, unvalidated, and unsafe on a shared context: `RequestContext` is copied and mutated across the clear paths (`requestContext.CreateCopy()` in both confirmation handlers), and a stale bag entry would silently mis-attribute a later clear. **Rejected: a separate public entry point per clear kind.** Duplicates the dispatch logic and grows the public surface.

### 7. Exactly where each recording sits, with guard and fault isolation

Two private helpers in the mediator carry the guard, the FR-7a clamp and the fault isolation:

```csharp
private void RecordAdded(Message message)
{
    if (!_outboxMeter.Enabled) return;                 // NFR-1: before any tag work
    try { _outboxMeter.AddMessageAdded(message.Header.Topic); }
    catch (Exception ex) { Log.OutboxMetricsFault(s_logger, ex); }
}

private void RecordCleared(
    RoutingKey destination,
    OutboxClearSource clearSource,
    DateTimeOffset? createdAt,
    DateTimeOffset dispatchedAt)
{
    if (!_outboxMeter.Enabled) return;
    try
    {
        _outboxMeter.AddMessageCleared(destination, clearSource);

        if (createdAt is { } created)                   // FR-7b: null => no sample
        {
            var wait = dispatchedAt - created;
            _outboxMeter.RecordPublishDuration(
                wait < TimeSpan.Zero ? TimeSpan.Zero : wait, destination, clearSource); // FR-7a
        }
    }
    catch (Exception ex) { Log.OutboxMetricsFault(s_logger, ex); }
}
```

**Where the try/catch lives: both, deliberately.** The **call-site** catch is load-bearing for AC-22, whose double's record methods throw — a catch inside `OutboxMeter` cannot protect the mediator from a third-party or test implementation of `IAmABrighterOutboxMeter`. The **implementation-side** catch in `OutboxMeter` makes the "must not throw" contract true for the shipped type regardless of caller, including on the confirmation thread. Neither alone is sufficient; the cost is one extra `try` region on a path that already has one.

**The FR-7a clamp lives at the call site, not in the meter**, so that a spy double observes the post-policy value and AC-15 can assert "exactly one sample of 0" against the double rather than only through an exporter. `OutboxMeter` also clamps defensively.

Recording call sites, method by method:

| Path | File:line today | Placement |
|---|---|---|
| **FR-1 async single add** | `AddToOutboxAsync` `:278-291` | after the `if (!written) throw new ChannelFailureException(...)` guard: `RecordAdded(message)`. Nothing recorded when `batchId != null` (early return at `:267`) or when the write failed (AC-3). |
| **FR-1 sync single add** | `AddToOutbox` `:321-329` | same, after the `!written` throw. |
| **FR-2 async batch** | `EndBatchAddToOutboxAsync` `:659-672` | after the `!written` throw, `foreach (var message in batch) RecordAdded(message);`. `batch` is the live `List<TMessage>` returned by `BeginBatchAddToOutbox` → `GetBatchOrThrow` and is the exact list handed to `_asyncOutbox.AddAsync`, so the mediator already knows the batch membership at flush time — no new state is needed for AC-2. |
| **FR-2 sync batch** | `EndBatchAddToOutbox` `:631-641` | same. |
| **FR-3/FR-5 sync dispatch** | `Dispatch` `:1066-1077` | non-confirmation branch: capture the MarkDispatched result and instant, then record. |
| **FR-3/FR-5 async dispatch** | `DispatchAsync` `:1211-1221` | as above. |
| **FR-3/FR-5 bulk** | `BulkDispatchAsync` `:1141-1154` | per id in `batch.Ids()`, after that id's MarkDispatched succeeded. |
| **FR-3/FR-5 async confirmation** | `HandleAsyncPublishConfirmation` `:879-884` | after MarkDispatchedAsync succeeded, using the pending-clear entry. |
| **FR-3/FR-5 sync confirmation** | `ConfigurePublisherCallbackMaybe` delegate `:976-978` | as above. |

**Capturing the dispatch instant without changing behaviour.** FR-5 requires the *same* instant passed to the clearing `MarkDispatched`. Today `_timeProvider.GetUtcNow()` is evaluated **inside** the lambda handed to `ExecuteWithResiliencePipeline(Async)`, so under a retrying pipeline each attempt gets a fresh instant. Hoisting it outside would change the stored `MarkDispatched` timestamp under retry — a real, if small, behavioural change (NFR-2). We therefore capture it from inside the lambda:

```csharp
var dispatchedAt = default(DateTimeOffset);
var marked = await ExecuteWithResiliencePipelineAsync(
    async _ =>
    {
        dispatchedAt = _timeProvider.GetUtcNow();
        await _asyncOutbox.MarkDispatchedAsync(
            message.Id, requestContext, dispatchedAt, cancellationToken: cancellationToken);
    },
    requestContext, cancellationToken: cancellationToken);

if (marked)
    RecordCleared(message.Header.Topic, clearSource, message.Header.TimeStamp, dispatchedAt);
```

`dispatchedAt` then holds exactly the instant used by the successful attempt. `ExecuteWithResiliencePipeline(Async)` already returns `bool` (`:1380`, `:1405`) — `true` only when the delegate completed without throwing — so "MarkDispatched succeeded" is observable with no new plumbing, satisfying FR-8's "metric follows the store, not the send" and AC-11. The additional captured local adds one field to an already-allocated closure display class; no new allocation, nothing computed when disabled beyond the assignment itself (NFR-1, AC-18).

**Recordings sit outside every tracing guard.** None of them reads `_tracer`, `requestContext.Span` or `_instrumentationOptions`, and none is nested inside a `_tracer?.` or `instrumentationOptions.HasFlag(...)` block. That is the whole of FR-9/AC-17.

**AC-12 (empty sweep).** `messages.Length == 0` means the `foreach` bodies in `DispatchAsync`/`BulkDispatchAsync` never execute, so there are no recordings at all — not even zero-valued ones.

### 8. The bulk dispatch path (C-8, AC-7)

Within one `topicBatch` the grouping key is `(WireTopic, LookupTopic)` (`:1108`), so **the destination is constant for the batch**: `topicBatch.Key.WireTopic`. Only `Header.TimeStamp` needs a per-message lookup, resolved from the `messages` array already materialised at `:1126`:

```csharp
var messages = topicBatch.ToArray();

Dictionary<string, DateTimeOffset>? createdAtById = null;
if (_outboxMeter.Enabled)                              // NFR-1: not built when disabled
{
    createdAtById = new Dictionary<string, DateTimeOffset>(messages.Length, StringComparer.Ordinal);
    foreach (var m in messages)
        createdAtById[m.Id.Value] = m.Header.TimeStamp; // indexer, not Add: a duplicate id must not throw
}
```

and inside `foreach (var successfulMessage in batch.Ids())`, after that id's MarkDispatched returned `true`:

```csharp
RecordCleared(
    topicBatch.Key.WireTopic,
    clearSource,
    createdAtById is not null && createdAtById.TryGetValue(successfulMessage.Value, out var created)
        ? created
        : null,
    dispatchedAt);
```

One cleared unit and one latency sample **per message**, not per batch (AC-7). Keying on `message.Id.Value` (string, `Ordinal`) matches the existing `producerSpans.TryAdd(message.Id.Value, span)` convention in these same methods.

### 9. Confirmation-path creation time: the pending-clear map

For a producer implementing `ISupportPublishConfirmation`, the callback gets a `PublishConfirmationResult` with no `Message`. The mediator therefore remembers the small amount it needs at send time.

**State.**

```csharp
private readonly ConcurrentDictionary<string, PendingClear> _pendingClears = new(StringComparer.Ordinal);

private readonly record struct PendingClear(
    DateTimeOffset CreatedAt,
    RoutingKey Destination,
    OutboxClearSource ClearSource);

private const int MAX_PENDING_CLEARS = 10_000;
private static readonly TimeSpan s_pendingClearTtl = TimeSpan.FromMinutes(5);
private static readonly TimeSpan s_pendingClearPurgeInterval = TimeSpan.FromMinutes(1);
private DateTimeOffset _nextPendingClearPurgeAt;
```

`ConcurrentDictionary` is the right collection because the confirmation callback arrives on a broker/threadpool thread while the sweeper's `BackgroundDispatchUsingAsync` and request-thread explicit clears may be writing: it gives lock-free reads, striped-lock writes, an atomic read-and-remove (`TryRemove(key, out value)`), and safe enumeration for the purge. No caller has to serialise access (NFR-4), and nothing about it reintroduces the serialisation ADR 0032 removed from the explicit clear path. `PendingClear` is a `readonly record struct` per ADR 0019, stored by value — no per-message heap allocation.

**Population — only when the meter is enabled (NFR-1, AC-18).** In `Dispatch`, `DispatchAsync` and `BulkDispatchAsync`, in the `producer is ISupportPublishConfirmation` branch, **before** the send (the broker confirmation can arrive before `SendAsync` returns):

```csharp
if (_outboxMeter.Enabled)
    TrackPendingClear(message, clearSource);
```

```csharp
private void TrackPendingClear(Message message, OutboxClearSource clearSource)
{
    try
    {
        var now = _timeProvider.GetUtcNow();

        if (now >= _nextPendingClearPurgeAt)
        {
            _nextPendingClearPurgeAt = now + s_pendingClearPurgeInterval;
            foreach (var entry in _pendingClears)          // snapshot enumeration; never throws
            {
                if (now - entry.Value.CreatedAt > s_pendingClearTtl)
                    _pendingClears.TryRemove(entry.Key, out _);
            }
        }

        if (_pendingClears.Count >= MAX_PENDING_CLEARS) return;   // hard bound: drop-newest

        // Indexer, not TryAdd: a re-dispatch of the same id must refresh the clear source.
        _pendingClears[message.Id.Value] = new PendingClear(
            message.Header.TimeStamp, message.Header.Topic, clearSource);
    }
    catch (Exception ex) { Log.OutboxMetricsFault(s_logger, ex); }
}
```

In `DispatchAsync`'s confirmation branch, when `sent == false` (already detected at `:1223` for the `TripTopic` call) the entry is removed immediately, so a failed send does not occupy the map for the full TTL.

**The eviction rule and its bound.** Two mechanisms, deliberately layered:

- *Primary — age-based, amortised, driven by the existing `_timeProvider`.* At most once per minute (checked on insert) entries older than 5 minutes are removed. The scan is O(n) with n bounded below, and runs on the thread that was already about to send. The purge clock is `_timeProvider`, so `FakeTimeProvider` makes it fully deterministic in tests (NFR-7). We deliberately do **not** hang eviction off `OutstandingMessagesCheck`: that method exists to count outstanding messages, runs on a `Task.Run` background thread, is rate-limited by `_maxOutStandingCheckInterval`, and is skipped entirely when `_outBox` is null — piggy-backing on it would both muddy its responsibility and leave async-only configurations unpurged.
- *Backstop — hard cap.* Never more than 10,000 entries. At roughly 80 bytes per entry plus `ConcurrentDictionary` overhead this bounds the map at well under 2 MB regardless of broker behaviour.

**The honest trade-off.** Both mechanisms lose latency samples rather than memory. A confirmation that arrives after the 5-minute TTL, or a send made while the map is at its cap, hits FR-7b: the **cleared unit is still recorded** (with `clear_source = "unknown"` and the destination from `result.Topic`), the **latency sample is skipped**. Latency-sample count can therefore be lower than cleared count — the divergence FR-7b requires the ADR to document. A 5-minute TTL is generous relative to RabbitMQ publisher confirms and Kafka acks (sub-second to seconds); a confirmation that takes longer than five minutes has almost certainly been lost.

**Consumption in the callback.** In both `HandleAsyncPublishConfirmation` and the `ConfigurePublisherCallbackMaybe` delegate, after MarkDispatched returned `true`:

```csharp
var resolved = _outboxMeter.Enabled
               && !Id.IsNullOrEmpty(result.MessageId)
               && _pendingClears.TryRemove(result.MessageId.Value, out var pending);

RecordCleared(
    resolved ? pending.Destination : result.Topic ?? RoutingKey.Empty,
    resolved ? pending.ClearSource : OutboxClearSource.Unknown,
    resolved ? pending.CreatedAt : null,
    dispatchedAt);
```

- `result.MessageId == Id.Empty` (documented on `PublishConfirmationResult.MessageId` as "the producer could not determine the id from the broker response") makes lookup impossible ⇒ FR-7b/AC-16: no latency sample, cleared unit still recorded because MarkDispatched succeeded, no exception escapes the callback.
- The entry is **not** removed when MarkDispatched fails, so a later sweeper re-dispatch can still resolve a creation time.
- `result.Topic` is documented nullable; the fallback is `RoutingKey.Empty` (attribute value `""`), which is what the `+1` in §2's cardinality arithmetic accounts for.

**Known gap, stated plainly.** Read-and-remove holds at most one pending entry per message id. If a confirmation-producer message is dispatched twice concurrently (the FR-3/AC-24 scenario: an explicit clear whose confirmation is late, then a sweeper re-dispatch), the **first** confirmation consumes the entry and the **second** records a cleared unit with `clear_source = "unknown"` and no latency sample. So AC-24's literal expectation — cleared units 2 (1 explicit, 1 sweeper) and latency samples 2 — is met on the non-confirmation dispatch paths, where each dispatch computes latency from its own in-scope `Message` and the map is not involved, and **not** on confirmation transports. The AC-24 test must therefore use a non-confirmation producer, and the confirmation variant is a documented FR-7b divergence. The escape hatch, if maintainers require parity, is to add an outstanding-send count to `PendingClear` and decrement-then-remove; that is deferred as unjustified complexity for a race that only occurs when the sweeper's `minimumAge` is shorter than a broker's confirmation latency.

### 10. Registration (FR-11)

No change to `BrighterMetricsBuilderExtensions.AddBrighterInstrumentation()` is required or made: it already calls `builder.AddMeter(BrighterSemanticConventions.MeterName)`, which is all the instruments need. ADR 0039 (`0039-opentelemetry-builder-extension`) delegates to this same per-signal extension, so its single-call `AddBrighterInstrumentation()` on `OpenTelemetryBuilder` picks Outbox metrics up for free. No new package, no flag, no second registration call.

### 11. Testing strategy

**Landing directory**: `tests/Paramore.Brighter.Core.Tests/Observability/Metrics/Outbox/`, alongside the existing `Metrics/` tests (`When_ending_a_send_span_should_still_record_a_client_operation.cs`), following the `When_...` file-naming convention.

**Doubles** in `tests/Paramore.Brighter.Core.Tests/Observability/TestDoubles/`, in the style of `SpyMessagingMeter`/`DisabledDbMeter`:

- `SpyOutboxMeter : IAmABrighterOutboxMeter` — `Enabled => true`; appends to `List<(RoutingKey Destination, OutboxClearSource Source)> Added/Cleared` and `List<(TimeSpan Wait, RoutingKey, OutboxClearSource)> Durations`. All three lists are the assertion surface for AC-1/2/4/5/6/7/8/13/14/15/16/24. Backed by a `lock` for AC-25's concurrent adds.
- `DisabledOutboxMeter` — `Enabled => false` plus a `RecordCallCount` that must remain `0` (AC-18), paired with an outbox double whose operation counters prove no extra store read.
- `ThrowingOutboxMeter` — `Enabled => true`, every record method throws (AC-22).

**Determinism (NFR-7).** `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`, already referenced by `Paramore.Brighter.Core.Tests.csproj:25` and used e.g. in `tests/.../Archiving/When_Archiving_Old_Messages_From_The_Outbox.cs`) is injected as the mediator's `timeProvider`, giving the dispatch instant; `MessageHeader.TimeStamp` is `{ get; init; }` (`MessageHeader.cs:321`), so creation time is set explicitly. No `Thread.Sleep`, no wall clock. AC-14 constructs two independent mediators over one shared `InMemoryOutbox`, disposes the first, then advances the second's `FakeTimeProvider` by five minutes before sweeping.

**AC-21 (in-memory exporter through `AddBrighterInstrumentation()`)** requires two changes to the test project only — no `src` change:

- add `<ProjectReference Include="..\..\src\Paramore.Brighter.Extensions.Diagnostics\Paramore.Brighter.Extensions.Diagnostics.csproj" />` to `Paramore.Brighter.Core.Tests.csproj` (no test project currently references it; `OpenTelemetry.Exporter.InMemory` is already referenced at line 28).
- build `Sdk.CreateMeterProviderBuilder().AddBrighterInstrumentation().AddInMemoryExporter(metrics).Build()`, add and clear one message, `ForceFlush()`, and assert the three instrument names appear with `Meter.Name == "Paramore.Brighter"`.

Two complementary tests, because instrument identity is process-wide:

- a **presence** test using the mediator's own default `OutboxMeter`, asserting the three instruments appear and are attributed to the right destination and clear source;
- a **value** test injecting a freshly constructed `OutboxMeter` into the mediator (still collected by name, so still exercising the registration path) and disposed with the test, so exact totals are isolated.

`tests/Paramore.Brighter.Core.Tests/xunit.runner.json` already sets `"parallelizeTestCollections": false`, which removes cross-test interference on the shared meter name; the value test's explicit `Dispose` removes it across runs.

**AC-23** is satisfied by running the existing Outbox, sweeper, circuit-breaker and observability suites unmodified; no existing assertion is touched, and `SpyMessagingMeter`/`DisabledDbMeter` are not edited.

### 12. Documentation obligation (C-1, C-6)

Three places, all part of this change:

1. **`docs/guides/outbox-metrics.md`** (new, following the `docs/guides/box-provisioning-*.md` convention) — the instrument table, the attribute domains, and two explicit operator warnings:
   - *What the duration actually measures.* The basis is `message.Header.TimeStamp`, stamped during message construction, so the value **includes message mapping/transform time** immediately preceding the Outbox write and is **subject to clock skew** between the host that created the message and the host that dispatched it (negative results are recorded as 0 per FR-7a). It is therefore *not* a pure store-to-broker duration, and must not be alerted on as one.
   - *The circuit breaker can hide backlog (C-6).* `IAmAnOutboxCircuitBreaker.TrippedTopics` is passed to `OutstandingMessagesAsync` (`OutboxProducerMediator.cs:738`), so a tripped topic's messages are never selected by the sweeper. With no outstanding-count gauge (OOS-1), the alertable signature of a stalled topic is **`added.messages` for a destination continuing to rise while `cleared.messages` for that destination goes flat**. This is the alert RLOI-4186 should build; see ADR 0028.
   - *Latency samples may be fewer than cleared units* on confirmation transports (FR-7b), with the three causes: empty `MessageId`, TTL eviction, cap drop.
2. **XML doc comments** on `IAmABrighterOutboxMeter.RecordPublishDuration`, `BrighterSemanticConventions.OutboxPublishDuration` and `OutboxClearSource.Unknown`, repeating the measurement basis and the `"unknown"` cause so it is visible at the call site (NFR-8).
3. **The Brighter documentation site** lives in a separate repository; updating it is a follow-up on the upstream PR, not a deliverable in this repo.

### 13. FR/NFR → design element

| Requirement | Design element |
|---|---|
| FR-1 single add | `RecordAdded` after the `!written` throw in `AddToOutboxAsync` / `AddToOutbox` (§7) |
| FR-2 batched add | per-message `RecordAdded` over the `batch` list in `EndBatchAddToOutbox(Async)` after the `!written` throw (§7) |
| FR-3 cleared count | `RecordCleared` in all four paths: `Dispatch`, `DispatchAsync`, `BulkDispatchAsync`, both confirmation handlers (§7, §8, §9) |
| FR-4 sweeper vs explicit | `OutboxClearSource` parameter on the three private dispatch methods; `paramore.brighter.outbox.clear_source` attribute (§2, §6) |
| FR-5 latency per cleared message | `RecordPublishDuration` in `RecordCleared`, using the closure-captured `dispatchedAt` and `Header.TimeStamp` (§4, §7) |
| FR-6 survives restart / cross-process | basis is the persisted `Header.TimeStamp`; no in-process state on the non-confirmation paths (§7) |
| FR-7a negative → 0 | clamp in `RecordCleared` at the call site (§7) |
| FR-7b no creation time → skip sample | `createdAt` is `DateTimeOffset?`; null on `Id.Empty`, TTL eviction, cap drop (§9) |
| FR-8 failures not counted | recording gated on `ExecuteWithResiliencePipeline(Async)` returning `true` for the **MarkDispatched** call; no recording on `!written`, `!sent`, failed confirmation (§7) |
| FR-9 independent of tracing | direct instruments; no recording reads `_tracer`, `requestContext.Span` or `_instrumentationOptions` (§7) |
| FR-10 safe, bounded attributes | exactly two attribute keys, both from `BrighterSemanticConventions`; no id, body, partition key, correlation id or bag value (§2) |
| FR-11 existing registration path | instruments on `new Meter(BrighterSemanticConventions.MeterName)`; `AddBrighterInstrumentation()` unchanged (§5.4, §10) |
| NFR-1 zero cost when disabled | `if (!_outboxMeter.Enabled) return;` first statement of both helpers; `TrackPendingClear` and the bulk `createdAtById` dictionary gated on `Enabled` (§7, §8, §9) |
| NFR-2 no behavioural change | signature changes confined to private methods; dispatch instant captured *inside* the resilience lambda rather than hoisted; no change to producer lookup, `MarkDispatched` args, ordering, exceptions, logs or spans (§6, §7) |
| NFR-3 faults isolated | try/catch at every call site **and** inside `OutboxMeter`; `ToClearSourceName` degrades instead of throwing (§5.2, §5.3, §7) |
| NFR-4 thread safety | instruments are thread-safe by contract; `ConcurrentDictionary` for the pending map; no new lock, no reintroduced serialisation (ADR 0032) (§9) |
| NFR-5 TFM compatibility | no TFM-gated API; `TagList` and `InstrumentAdvice<double>` verified present in the `netstandard2.0` build of DiagnosticSource 10.0.11, with a documented `#if` + View fallback (§3, §5.2) |
| NFR-6 bounded cardinality | `≤ 7 × (D + 1)` series; arithmetic in §2 |
| NFR-7 test determinism | `FakeTimeProvider` for dispatch and purge; `MessageHeader.TimeStamp` `init` for creation (§11) |
| NFR-8 upstream acceptability | MIT headers, XML docs, `Add*`/`Record*`/`Enabled` naming and `MessagingSystemExtensions`-shaped mapper, source-generated logging (ADR 0026), no interface break (§5, §12) |

## Consequences

### Positive

- Added volume, cleared volume, sweeper-versus-explicit ratio and publish-lag percentiles become available to every application that already calls `AddBrighterInstrumentation()`, with no new opt-in — unblocking RLOI-4186.
- Metrics survive the PI-36093 failure mode exactly: the latency basis is the persisted header timestamp, so a sweeper in a different process after a restart reports the full wait.
- Completely independent of sampling and of whether a tracer exists, which is the whole point of the spec.
- Zero heap allocation on the recording path (`TagList` inline slots, `readonly record struct` pending entries) and a single `bool` read when no listener is attached.
- The clear-kind parameter is compile-checked, so a future dispatch path cannot silently lose attribution.
- Ownership follows the `_ownsRegistry`/`_ownsTransformerFactories` precedent already in the class, so ADR 0069's disposal cascade extends naturally.
- No Outbox implementation, schema, public interface or existing test assertion changes.

### Negative

- **Latency samples can be fewer than cleared units.** On confirmation transports, an empty `MessageId`, a TTL eviction or a cap drop yields a cleared unit with `clear_source = "unknown"` and no sample. Dashboards must not assume `count(duration) == sum(cleared)`.
- **A third `clear_source` value exists.** `"unknown"` is bounded but is an admission that the confirmation path cannot always be attributed; AC-6's ratio should be computed as `sweeper / (sweeper + explicit + unknown)`.
- **AC-24 is not literally satisfiable on confirmation transports** (§9). This is a design limitation of read-and-remove, recorded openly rather than papered over.
- **New in-mediator state.** The pending-clear map is the only new mutable state; it is bounded and disposed, but it is state that did not exist and that a reviewer must reason about.
- **One extra field of closure capture and one extra `bool` branch** on the dispatch path even when metrics are off.
- **An explicit `System.Diagnostics.DiagnosticSource` reference** raises `Paramore.Brighter`'s direct dependency surface, in exchange for the bucket boundaries not depending on a transitive resolution.
- **Two mediators in one process produce duplicate instrument identity.** The mediator is documented as a singleton, but `ControlBusSenderFactory` and `ControlBusReceiverBuilder` each construct a second one; with both owning their own `OutboxMeter`, the OTel SDK sees two identical instruments on the meter name and will report a duplicate. Mitigation: inject a shared `IAmABrighterOutboxMeter` (the DI path does exactly this when one is registered). This is a pre-existing structural quirk of the control bus that the design exposes rather than creates.
- **The header-timestamp basis is imprecise** (mapping/transform time included, clock skew clamped to 0) and will be misread by someone despite §12.

### Risks and Mitigations

| Risk | Mitigation |
|---|---|
| Names/types rejected by maintainers (C-7) | Everything in §1–§2 is presented as a proposal; the design depends on none of the specific strings. Instrument names and the attribute key are single `const`s. |
| `InstrumentAdvice<double>` unavailable if the DiagnosticSource floor is not raised | Documented `#if NET9_0_OR_GREATER` fallback plus the equivalent operator-side `AddView` (§3). |
| Bucket boundaries wrong for a given workload | Boundaries are only *advice*; a host can override with a View without a Brighter change. |
| A broker that never confirms grows the map | Hard cap + age eviction, both driven by `_timeProvider` (§9). |
| The purge scan runs on a request thread | Amortised to once per minute over at most 10,000 struct entries, and only when the meter is enabled. |
| Static/shared instrument identity makes tests flaky | `parallelizeTestCollections: false`; the value-asserting AC-21 test injects and disposes its own `OutboxMeter`. |
| `Activator.CreateInstance` in `BuildOutBoxProducerMediator` breaks on the new parameter | Identified explicitly (§5.4); the call site is updated in the same change, and the existing comment there already warns about exactly this. |
| An operator alerts on `publish.duration` as a broker latency | §12's documentation, repeated in XML docs at the point of use. |

## Alternatives Considered

**1. Extend `BrighterMetricsFromTracesProcessor` (ADR 0022) to derive Outbox metrics from spans.** *Rejected — cannot satisfy FR-9.* The processor's `OnEnd` only ever sees ended, sampled activities, and the Outbox add path writes an `ActivityEvent` onto a possibly-null `requestContext.Span` via `BrighterTracer.WriteOutboxEvent`, which begins `if (span == null) return;`. In the sampled environments the spec cares about there would be nothing to derive from. ADR 0022 is **not** superseded: it remains correct and in force for the messaging and db signals it already derives; this ADR adds a second, complementary mechanism for a signal that cannot be derived from traces.

**2. Add a creation-time field to `PublishConfirmationResult` (ADR 0063).** *Rejected.* It is a positional `sealed record`, so a new parameter is a source-breaking change for every external `ISupportPublishConfirmation` implementer, and it would require **every** producer (Kafka, RMQ sync and async, and any third-party transport) to be edited to populate it correctly — including in the failure paths where the producer may not even know the message. That is a large, cross-transport, breaking change for a value the mediator already has in hand at send time.

**3. Skip latency entirely for confirmation producers (lean on FR-7b).** *Rejected.* The cheapest option, and it would satisfy the letter of FR-7b, but it omits the metric on precisely Kafka and RabbitMQ — the transports PI-36093 ran on. The pending-clear map costs one bounded dictionary; the value is the motivating use case.

**4. Carry the clear kind in `RequestContext.Bag`.** *Rejected.* Implicit and unvalidated, with a genuine correctness hazard: `RequestContext` is copied and re-parented across the clear and confirmation paths (`requestContext.CreateCopy()` at `:877` and `:974`), and a stale bag entry on a shared context would mis-attribute a later clear with no compiler help and no test that would necessarily catch it.

**5. A separate public entry point per clear kind** (e.g. `ClearOutboxAsSweeperAsync`). *Rejected.* Duplicates the dispatch logic that ADR 0032 and the circuit-breaker work have already made delicate, and grows the public surface against C-11.

**6. Add Outbox members to `IAmABrighterMessagingMeter`.** *Rejected.* Every existing member takes an `Activity`; Outbox recording is activity-free by FR-9. Merging them gives one type two unrelated collaboration contracts and forces `SpyMessagingMeter` to grow members it cannot implement, touching a double that AC-23 wants left alone.

**7. Register `IAmABrighterOutboxMeter` in DI only, mirroring `MessagingMeter`/`DbMeter`.** *Rejected as the sole mechanism.* `AddBrighterInstrumentation()` on a `MeterProviderBuilder` writes its registrations into the builder's own service collection; with `Sdk.CreateMeterProviderBuilder()` (AC-21's exact scenario) the application container never sees them, so the mediator would be left with no instruments at all. DI injection is kept as an *override* so hosts that wire OTel through `AddOpenTelemetry()` can share one instance and get `GetServiceAttributes()` parity.

**8. A process-wide static `OutboxMeter.Default`.** *Rejected.* It would remove the duplicate-instrument risk from the control-bus case, but at the cost of process-global mutable state that is never disposed, cumulative counters that leak across tests, and no way to give different hosts different service attributes. Mediator ownership with the existing `_owns*` pattern is testable, disposable and consistent with ADR 0069.

**9. An outstanding/backlog gauge, archiver metrics, or publish-failure counters.** *Out of scope* (OOS-1, OOS-2, OOS-3). The gauge needs either a timer poll or an observable callback that queries the store, raising cost, multi-instance double-counting and API questions that deserve their own spec. §12 documents the added-minus-cleared approximation and the tripped-topic signature instead.

**10. Adding an outstanding-send counter to `PendingClear`** so N concurrent dispatches of one id yield N latency samples. *Rejected for now* as unjustified complexity for a race that only arises when the sweeper's `minimumAge` is shorter than a broker's confirmation latency; recorded in §9 as the escape hatch if maintainers require AC-24 parity on confirmation transports.

## References

- Requirements: [specs/0037-outbox-metrics/requirements.md](../../specs/0037-outbox-metrics/requirements.md)
- Related ADRs:
  - [ADR 0010 — Brighter OpenTelemetry Semantic Conventions](0010-brighter-semantic-conventions.md) *(Accepted)* — the attribute-key and naming conventions this ADR extends; `messaging.destination.name` is reused unchanged and the new key takes the `paramore.brighter.*` prefix it prescribes.
  - [ADR 0022 — Generate Metrics from Traces](0022-generate-metrics-from-traces.md) *(Proposed)* — the trace-derived mechanism this ADR departs from for the Outbox, and only for the Outbox. Not superseded.
  - [ADR 0063 — Failed Delivery Context for Confirmation-Based Producers](0063-failed-delivery-context.md) *(Accepted)* — supplies `PublishConfirmationResult`, which this ADR consumes (`MessageId`, `Topic`) but deliberately does not extend.
  - [ADR 0028 — Support Circuit Breaking for Topics in Outbox Sweeper](0028-support-circuit-breaking-of-topics.md) *(Proposed)* — the tripped-topic behaviour that hides backlog; §12 states the resulting alert signature for RLOI-4186.
  - [ADR 0032 — Remove Semaphore from Explicit Clear](0032-remove-explicit-clear-lock.md) *(Accepted)* — the explicit clear path must not be re-serialised; the pending-clear map uses `ConcurrentDictionary` and adds no lock.
  - [ADR 0039 — OpenTelemetry Builder Extension](0039-opentelemetry-builder-extension.md) *(Proposed)* — delegates to the same `Extensions.Diagnostics` per-signal extension, so Outbox metrics arrive through it with no extra work.
  - [ADR 0019 — Avoid Primitive Obsession](0019-avoid-primitive-obsession.md) *(Accepted)* — `RoutingKey` on the meter interface; `PendingClear` as a `readonly record struct`.
  - [ADR 0026 — Use Source Generated Logging](0026-use-source-generated-logging.md) *(Accepted)* — the new `Log.OutboxMetricsFault` entries are source-generated.
  - [ADR 0069 — Ownership and disposal cascade for mapper/transform factories](0069-factory-registry-ownership-and-disposal-cascade.md) *(Accepted)* — `_ownsOutboxMeter` follows its "a component that did not create a disposable does not dispose it" rule.
- External references:
  - [OpenTelemetry Semantic Conventions — Messaging metrics](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/messaging/messaging-metrics.md)
  - [OpenTelemetry Metric naming guidelines](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/general/naming.md)
  - [.NET `InstrumentAdvice<T>` / histogram bucket boundaries](https://learn.microsoft.com/dotnet/api/system.diagnostics.metrics.instrumentadvice-1)
  - Jira: RLOI-4185 (this work), RLOI-4186 (blocked consumer), PI-36093 (motivating incident)
