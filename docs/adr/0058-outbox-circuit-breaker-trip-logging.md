# 58. Outbox Circuit Breaker Trip Observability (Logging, Tracing, Metrics)

Date: 2026-09-22

## Status

Proposed

## Context

**Scope**: This ADR covers everything emitted when `IAmAnOutboxCircuitBreaker`'s state actually
changes (a topic trips, re-trips, or recovers): structured logs, an OpenTelemetry trace span per
transition, and a metric derived from that span. It does not cover the existing failure logs that
lead up to a trip, and it does not cover the sync `Dispatch()` gap described under Constraints.

> Originally scoped to logging only. Broadened after review feedback (see PR #4402 — Ian Cooper:
> "I wonder if we want to add OTel for this too?") once investigation showed Brighter's tracing
> infrastructure already normalizes constructor-injecting `IAmABrighterTracer` and derives metrics
> from spans automatically, so tracing/metrics are a natural extension rather than a second,
> separable decision.

### The Problem

Brighter added `IAmAnOutboxCircuitBreaker` (ADR 0028) so a topic that keeps failing to publish gets
circuit-broken: `TripTopic()` marks it, and the Sweeper excludes tripped topics from
`OutstandingMessagesAsync` until `CoolDown()` decrements it back to zero and evicts it.

Today, none of this is observable. Investigating "what logs do I get when a publish to topic X
fails" surfaces only indirect, inconsistent signals in `OutboxProducerMediator`:

- Confirmation-based producers (`ISupportPublishConfirmation`): `Log.ConfirmationFailed`
  (Warning, includes id + topic) fires before `TripTopic` is called.
- Everything else (`DispatchAsync` / `BulkDispatchAsync`): if the resilience pipeline catches an
  exception from `SendAsync`, `Log.ExceptionWhilstTryingToPublishMessage` (Error) fires — but this
  message carries **no topic or message id**, only the exception. If `SendAsync` simply returns
  `false` without throwing, nothing is logged at all before `TripTopic` runs.
- `InMemoryOutboxCircuitBreaker.TripTopic()` and `CoolDown()` themselves have no `ILogger`, no
  span, and no metric — they log and trace nothing when they change state.

The result: there is no single, deterministic signal — log, trace, or metric — that says "topic X
has been circuit-broken" or "topic X has recovered", independent of which producer/transport
caused the failure. An operator has to correlate scattered, differently-shaped log lines (or poll
`TrippedTopics` directly) to know the breaker state, and there is nothing to alert on.

### Requirements Context

Standalone ADR — no parent spec. Raised directly from investigating current circuit-breaker
observability at the user's request, then broadened per PR review feedback.

### Constraints

- Logging follows Brighter's existing static-logger + source-generated `LoggerMessage` convention
  (see the nested `static partial class Log` in `OutboxProducerMediator.cs` and
  `CommandProcessor.cs`, backed by `ApplicationLogging.CreateLogger<T>()`), rather than
  constructor-injecting `ILogger`.
- Tracing follows Brighter's existing `IAmABrighterTracer` convention: a single `ActivitySource`
  (wrapped by `BrighterTracer`), one `Create*Span` method per domain (`CreateProducerSpan`,
  `CreateDbSpan`, `CreateClaimCheckSpan`, `CreateArchiveSpan`, ...), each driven by a
  domain-specific `*SpanInfo` record and an `*Operation` enum with a `ToSpanName()` extension
  (see `BoxSpanInfo`/`BoxDbOperation`, `ClaimCheckSpanInfo`/`ClaimCheckOperation`). Unlike the
  logger, `IAmABrighterTracer` **is** constructor-injected everywhere it's used today (e.g.
  `OutboxProducerMediator`), so injecting it into `InMemoryOutboxCircuitBreaker` matches
  convention rather than breaking it.
- Metrics in this codebase are **derived from spans, not emitted directly**:
  `BrighterMetricsFromTracesProcessor` (an OTel SDK `BaseProcessor<Activity>`) listens for every
  `Activity` ending on Brighter's `ActivitySource`, reads its `instrumentation.domain` tag, and
  routes it to `IAmABrighterMessagingMeter` / `IAmABrighterDbMeter`. Adding a metric therefore
  means tagging a span correctly, not incrementing a `Counter<T>` inline from business logic.
- `IAmAnOutboxCircuitBreaker` is a public interface with (at least potentially) third-party
  implementations; this ADR must not force a specific logging/tracing mechanism onto every
  implementation.
- Must not change `OutboxCircuitBreakerOptions` defaults or any other existing public behaviour
  (per CLAUDE.md's Change Scope guardrail).
- Must remain correct under concurrent `TripTopic`/`CoolDown` calls — `InMemoryOutboxCircuitBreaker`
  already has an atomicity guarantee here (see `When_cooldown_runs_concurrently_should_be_atomic`),
  and this change must not weaken it.
- Out of scope: `OutboxProducerMediator.Dispatch()` (the sync producer path) never calls `TripTopic`
  at all today, so sync-producer failures never trip the breaker. That is a separate correctness bug,
  not an observability gap, and is tracked independently rather than folded into this ADR.

## Decision

We will add logging, tracing, and metrics **inside `InMemoryOutboxCircuitBreaker`**, not in
`OutboxProducerMediator`, because the circuit breaker is the only component that actually owns the
trip state and can tell a *fresh* trip from a *re-trip*, and is the only component that sees
`CoolDown()` evictions (recoveries) at all. `OutboxProducerMediator.TripTopic()` is a thin,
fire-and-forget wrapper called from two different dispatch methods; instrumenting there would
still miss `CoolDown()` entirely and would need an extra state lookup to detect re-trips, giving up
the "one location, one source of truth" property this ADR is for.

### Logging

Add a static logger and a nested `Log` class matching the existing pattern:

```csharp
private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<InMemoryOutboxCircuitBreaker>();

private static partial class Log
{
    [LoggerMessage(LogLevel.Warning, "Circuit breaker tripped for topic {Topic}; suppressing publish for {CooldownCount} cooldown cycle(s)")]
    public static partial void Tripped(ILogger logger, string topic, int cooldownCount);

    [LoggerMessage(LogLevel.Debug, "Circuit breaker re-tripped for topic {Topic}; cooldown extended to {CooldownCount} cycle(s)")]
    public static partial void ReTripped(ILogger logger, string topic, int cooldownCount);

    [LoggerMessage(LogLevel.Information, "Circuit breaker reset for topic {Topic}; publish suppression lifted")]
    public static partial void Reset(ILogger logger, string topic);
}
```

Three distinct, single-purpose transitions:

1. **Fresh trip (Warning)** — the topic was healthy and just became tripped. This is the signal an
   operator/alert should act on.
2. **Re-trip (Debug)** — the topic was already tripped and a further failure reset its cooldown
   counter back to `CooldownCount`. While a topic is failing continuously, every failed publish
   attempt calls `TripTopic` again, so logging this at Warning would flood log/alerting sinks
   during an incident with a fact the operator already knows ("topic X is still broken"). Debug
   keeps it available for deep debugging without competing with the fresh-trip signal.
3. **Reset (Information)** — `CoolDown()` evicted the topic back to healthy. This closes the loop
   so "time tripped" can be measured from log timestamps alone, without polling `TrippedTopics`.

### Tracing

Add a new span domain, following the exact shape every other domain already uses
(`BoxSpanInfo`/`BoxDbOperation` for db, `ClaimCheckSpanInfo`/`ClaimCheckOperation` for claim check):

```csharp
// Observability/CircuitBreakerSpanOperation.cs
public enum CircuitBreakerSpanOperation
{
    Trip = 0,    // Topic transitions from healthy to tripped
    ReTrip = 1,  // An already-tripped topic fails again; cooldown counter reset
    Reset = 2    // CoolDown() evicted the topic back to healthy
}

// BrighterSpanExtensions.cs — new ToSpanName() overload
public static string ToSpanName(this CircuitBreakerSpanOperation operation) => operation switch
{
    CircuitBreakerSpanOperation.Trip => "trip",
    CircuitBreakerSpanOperation.ReTrip => "re_trip",
    CircuitBreakerSpanOperation.Reset => "reset",
    _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
};

// Observability/CircuitBreakerSpanInfo.cs
public record CircuitBreakerSpanInfo(
    CircuitBreakerSpanOperation Operation,
    RoutingKey Topic,
    int CooldownCount);
```

New semantic convention constants in `BrighterSemanticConventions` (its own instrumentation
domain, matching `MessagingInstrumentationDomain`/`DbInstrumentationDomain`; the existing generic
`Operation` const is reused for the operation tag, matching every other `Create*Span` method):

```csharp
public const string CircuitBreakerInstrumentationDomain = "circuit_breaker";
public const string CircuitBreakerTopic = "paramore.brighter.circuitbreaker.topic";
public const string CircuitBreakerCooldownCount = "paramore.brighter.circuitbreaker.cooldown_count";
```

New method on `IAmABrighterTracer`, implemented in `BrighterTracer` following the `CreateDbSpan`/
`CreateClaimCheckSpan` pattern (an `ActivityKind.Internal` span, since a trip/reset is a Brighter-
internal decision, not a call to an external system):

```csharp
Activity? CreateCircuitBreakerSpan(
    CircuitBreakerSpanInfo info,
    InstrumentationOptions options = InstrumentationOptions.All);
```

```csharp
public Activity? CreateCircuitBreakerSpan(CircuitBreakerSpanInfo info, InstrumentationOptions options = InstrumentationOptions.All)
{
    var spanName = $"{info.Topic} {info.Operation.ToSpanName()}";
    const ActivityKind kind = ActivityKind.Internal;
    var now = _timeProvider.GetUtcNow();

    var tags = GetNewTagsCollection(options, BrighterSemanticConventions.CircuitBreakerInstrumentationDomain);

    if (options.HasFlag(InstrumentationOptions.Messaging))
    {
        tags.Add(BrighterSemanticConventions.Operation, info.Operation.ToSpanName());
        tags.Add(BrighterSemanticConventions.CircuitBreakerTopic, info.Topic.Value);
        tags.Add(BrighterSemanticConventions.CircuitBreakerCooldownCount, info.CooldownCount);
    }

    var activity = ActivitySource.StartActivity(name: spanName, kind: kind, tags: tags, startTime: now);
    Activity.Current = activity;
    return activity;
}
```

`InMemoryOutboxCircuitBreaker` takes the tracer and an `InstrumentationOptions` field the same way
`OutboxProducerMediator` does — constructor-injected, defaulted so existing callers (manual
construction or DI) are unaffected:

```csharp
public class InMemoryOutboxCircuitBreaker(
    OutboxCircuitBreakerOptions? options = null,
    IAmABrighterTracer? tracer = null,
    InstrumentationOptions instrumentationOptions = InstrumentationOptions.All)
    : IAmAnOutboxCircuitBreaker
```

Trip/re-trip and reset are momentary decisions with no downstream work inside them, so the span is
created and ended back-to-back (a near-zero-duration span still records a precisely timestamped
trace event — the same shape `CreateClaimCheckSpan` produces for a single storage call):

```csharp
public void TripTopic(RoutingKey topic)
{
    var isReTrip = false;
    var cooldownCount = _outboxCircuitBreakerOptions.CooldownCount;

    _trippedTopics.AddOrUpdate(topic,
        addValueFactory: _ => cooldownCount,
        updateValueFactory: (_, _) => { isReTrip = true; return cooldownCount; });

    var operation = isReTrip ? CircuitBreakerSpanOperation.ReTrip : CircuitBreakerSpanOperation.Trip;
    var span = tracer?.CreateCircuitBreakerSpan(new CircuitBreakerSpanInfo(operation, topic, cooldownCount), instrumentationOptions);
    tracer?.EndSpan(span);

    if (isReTrip)
        Log.ReTripped(s_logger, topic.Value, cooldownCount);
    else
        Log.Tripped(s_logger, topic.Value, cooldownCount);
}

private void TryEvictAndLogReset(RoutingKey topic, int cooledValue)
{
    if (cooledValue >= 0)
        return;

    var removed = ((ICollection<KeyValuePair<RoutingKey, int>>)_trippedTopics)
        .Remove(new KeyValuePair<RoutingKey, int>(topic, cooledValue));

    if (!removed)
        return;

    var span = tracer?.CreateCircuitBreakerSpan(new CircuitBreakerSpanInfo(CircuitBreakerSpanOperation.Reset, topic, 0), instrumentationOptions);
    tracer?.EndSpan(span);

    Log.Reset(s_logger, topic.Value);
}
```

### Metrics

Metrics fall out of the spans above for free via the existing trace-to-metric pipeline. Extend
`BrighterMetricsFromTracesProcessor.OnEnd` with a new case:

```csharp
case BrighterSemanticConventions.CircuitBreakerInstrumentationDomain:
    messagingMeter.AddCircuitBreakerEvent(activity);
    break;
```

Extend `IAmABrighterMessagingMeter` / `MessagingMeter` (rather than adding a third, dedicated
meter interface for one instrument) with a single `Counter<int>`, tagged by operation and topic —
the same shape as the existing `_sentMessagesCounter`, which already tags by
`MessagingDestination` (topic), so per-topic cardinality here is consistent with existing practice,
not a new category of risk:

```csharp
private readonly Counter<int> _circuitBreakerTripsCounter = meterFactory
    .Create(BrighterSemanticConventions.MeterName)
    .CreateCounter<int>(
        name: "messaging.circuitbreaker.trips",
        description: "Number of times a topic's outbox circuit breaker tripped, re-tripped, or reset.",
        unit: "{event}");

private static readonly FrozenSet<string> s_circuitBreakerTripsCounterAllowedTags = new[]
{
    BrighterSemanticConventions.Operation,
    BrighterSemanticConventions.CircuitBreakerTopic
}.ToFrozenSet();

public void AddCircuitBreakerEvent(Activity activity)
{
    _circuitBreakerTripsCounter.Add(1, [..activity.TagObjects.Filter(s_circuitBreakerTripsCounterAllowedTags), .._serviceAttributes]);
}
```

This is deliberately a `Counter<int>` only (no `UpDownCounter`/gauge for "currently tripped topic
count"): a counter answers "how often is this happening" and is enough to alert on rate/increase
(e.g. "any `Trip` in the last 5 minutes", "`ReTrip` count > N in 10 minutes" for a sustained
outage). It does **not** answer "is topic X tripped right now" as a queryable point-in-time state —
that would need the rejected `UpDownCounter` (see Alternatives) or inference from a `Trip` log with
no later `Reset`.

No change to `IAmAnOutboxCircuitBreaker` or `OutboxCircuitBreakerOptions` is required for any of the
above.

## Consequences

### Positive

- One deterministic, greppable phrase (`"Circuit breaker tripped for topic"` /
  `"Circuit breaker reset for topic"`) regardless of which producer/transport or dispatch path
  caused the failure — closing the gap identified during the original investigation.
- A trace span per transition, and a metric derived from it for free via the existing
  `BrighterMetricsFromTracesProcessor` pipeline — no new exporters or backend integration needed;
  it rides whatever `MeterProvider`/`TracerProvider` the host app already configured for Brighter.
- Recovery is now observable, not just the trip — enables measuring time-to-recovery, not only
  trip counts.
- No breaking change: `IAmAnOutboxCircuitBreaker` and `OutboxCircuitBreakerOptions` are untouched;
  the new tracer/`InstrumentationOptions` constructor parameters on `InMemoryOutboxCircuitBreaker`
  are optional and default-compatible with existing manual construction and DI registration.

### Negative

- Only covers `InMemoryOutboxCircuitBreaker`. A custom `IAmAnOutboxCircuitBreaker` implementation
  gets none of this for free and must add its own logging/tracing/metrics.
- Still doesn't cover the sync `Dispatch()` gap — sync-producer failures remain invisible to the
  breaker (and therefore to all of this) until that separate bug is fixed.
- Re-trip is deliberately logged at Debug, so a deployment running at the default Information/Warning
  level won't see re-trip volume in logs — only the initial trip and the eventual reset (re-trip is
  still visible in the metric, tagged `operation=re_trip`, regardless of log level).
- Counter-only metrics mean "is this topic tripped right now" is not a directly queryable metric
  state — only trip/re-trip/reset *events* are. A consumer wanting a live gauge must either add
  their own `UpDownCounter` downstream or accept the log/trace-based inference described above.

### Risks and Mitigations

**Risk**: Log line order across threads could look contradictory (e.g. `Reset` appearing to precede
a concurrent `Tripped` for the same topic in a multi-threaded log stream).
- **Mitigation**: The underlying state (`_trippedTopics`) is already proven atomic under concurrency
  by `When_cooldown_runs_concurrently_should_be_atomic`; this ADR doesn't add synchronization purely
  for log-line/span ordering. Document this as a known limitation rather than over-engineering the fix.

**Risk**: Re-trip at Debug means it's easy to forget it exists and be surprised it's silent by default in logs.
- **Mitigation**: Documented explicitly above and in the XML doc comments on `TripTopic`; the
  metric still captures re-trip volume regardless of log level.

**Risk**: A consumer expects to alert on "topic X is currently tripped" and finds the counter
insufficient.
- **Mitigation**: Documented explicitly in Consequences/Negative above; a follow-up ADR can add an
  `UpDownCounter` if this becomes a real need — it was scoped out here as a separate, smaller
  decision (see Alternatives), not because it's undesirable.

## Alternatives Considered

### Alternative 1: Log/trace in `OutboxProducerMediator.TripTopic()` instead

**Rejected because**: it's a thin wrapper reached from two dispatch methods, but `CoolDown()`
(recovery) is called from a completely different path (`ClearOutstandingFromOutbox`) that this
wrapper never touches — instrumentation would need a second site anyway. It also can't cheaply tell
a fresh trip from a re-trip without an extra `TrippedTopics` lookup, duplicating state the breaker
already has.

### Alternative 2: Constructor-inject `ILogger<InMemoryOutboxCircuitBreaker>`

**Rejected because**: it doesn't match the convention used everywhere else in this codebase
(`OutboxProducerMediator`, `CommandProcessor`) of a static logger via `ApplicationLogging.CreateLogger<T>()`.
Introducing a second logging style for one class adds inconsistency for no behavioural benefit.
(Note: this is unlike the *tracer*, which genuinely is constructor-injected everywhere already —
see Constraints — so the two decisions are not symmetric.)

### Alternative 3: Log every trip at the same (Warning) severity, with no fresh/re-trip distinction

**Rejected because**: during a sustained topic outage every failed publish attempt calls `TripTopic`
again, so this would flood Warning-level sinks/alerts with a fact already known ("topic X is still
broken"), which is precisely the noisy-but-uninformative logging this ADR is trying to move away from.

### Alternative 4: Add an `ActivityEvent` to an existing span instead of a new span type

Brighter already has a lighter-weight pattern for this (`BrighterTracer.WriteOutboxEvent`, used by
the Sweeper): add a timestamped `ActivityEvent` onto an *existing* parent span rather than starting
a new `Activity`.

**Rejected because**: there is no single existing parent span common to every call site. `TripTopic`
is called from both `DispatchAsync` and `BulkDispatchAsync` (each with their own producer span), and
`CoolDown` is called from `ClearOutstandingFromOutbox` (a different span entirely, or none at all if
the Sweeper runs standalone). A dedicated `CreateCircuitBreakerSpan` gives one consistent span kind
regardless of caller, matching how every other domain (db, messaging, claim check) gets its own
`Create*Span` method rather than piggybacking on whichever span happens to be open. This was the
explicit direction chosen when scoping this ADR ("cleanest, matches every other operation").

### Alternative 5: Dedicated `IAmABrighterCircuitBreakerMeter` instead of extending `MessagingMeter`

Full parity with the one-domain-per-meter pattern `MessagingMeter`/`DbMeter` establish.

**Rejected because**: it's a new interface, a new class, and a new DI registration for what is
currently a single instrument. Circuit breaking is a publish-health concern conceptually under
"messaging" already (the same domain `_sentMessagesCounter` lives in), so extending
`IAmABrighterMessagingMeter` was chosen as proportionate to the current scope. Nothing here
precludes graduating to a dedicated meter later if circuit-breaker metrics grow.

### Alternative 6: Add an `UpDownCounter` for live tripped-topic count

Track currently-tripped-topic count as a gauge (+1 on trip, -1 on reset), enabling a direct
"topics tripped right now" dashboard/alert without inference.

**Rejected for now**: it's a second instrument to name, tag, and test, and the counter-only design
already supports the primary use case (alerting on trip/re-trip *frequency*). Explicitly left as a
follow-up rather than bundled in, consistent with keeping this ADR's metric surface minimal; see
Consequences/Risks for the gap this leaves.

## References

- [ADR 0028: Support Circuit Breaking of Topics](0028-support-circuit-breaking-of-topics.md) — establishes the breaker this ADR instruments.
- `src/Paramore.Brighter/CircuitBreaker/InMemoryOutboxCircuitBreaker.cs`
- `src/Paramore.Brighter/CircuitBreaker/IAmAnOutboxCircuitBreaker.cs`
- `src/Paramore.Brighter/OutboxProducerMediator.cs` — existing `Log` partial class convention and current `TripTopic` call sites.
- `src/Paramore.Brighter/Logging/ApplicationLogging.cs` — static logger factory convention followed here.
- `src/Paramore.Brighter/Observability/BrighterTracer.cs`, `IAmABrighterTracer.cs`, `BrighterSemanticConventions.cs`, `BrighterSpanExtensions.cs` — tracing conventions followed here.
- `src/Paramore.Brighter/Observability/MessagingMeter.cs`, `IAmABrighterMessagingMeter.cs`, `BrighterMetricsFromTracesProcessor.cs` — trace-to-metric pipeline extended here.
- [PR #4402](https://github.com/BrighterCommand/Brighter/pull/4402) — original logging-only PR; review comment from Ian Cooper prompted broadening this ADR to include tracing and metrics.
