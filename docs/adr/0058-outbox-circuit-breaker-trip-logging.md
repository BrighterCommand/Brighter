# 58. Outbox Circuit Breaker Trip Logging

Date: 2026-09-22

## Status

Proposed

## Context

**Scope**: This ADR covers only the logging emitted when `IAmAnOutboxCircuitBreaker`'s state actually
changes (a topic trips, re-trips, or recovers). It does not cover the existing failure logs that lead
up to a trip, and it does not cover the sync `Dispatch()` gap described under Constraints.

### The Problem

Brighter added `IAmAnOutboxCircuitBreaker` (ADR 0028) so a topic that keeps failing to publish gets
circuit-broken: `TripTopic()` marks it, and the Sweeper excludes tripped topics from
`OutstandingMessagesAsync` until `CoolDown()` decrements it back to zero and evicts it.

Today, none of this is logged. Investigating "what logs do I get when a publish to topic X fails"
surfaces only indirect, inconsistent signals in `OutboxProducerMediator`:

- Confirmation-based producers (`ISupportPublishConfirmation`): `Log.ConfirmationFailed`
  (Warning, includes id + topic) fires before `TripTopic` is called.
- Everything else (`DispatchAsync` / `BulkDispatchAsync`): if the resilience pipeline catches an
  exception from `SendAsync`, `Log.ExceptionWhilstTryingToPublishMessage` (Error) fires — but this
  message carries **no topic or message id**, only the exception. If `SendAsync` simply returns
  `false` without throwing, nothing is logged at all before `TripTopic` runs.
- `InMemoryOutboxCircuitBreaker.TripTopic()` and `CoolDown()` themselves have no `ILogger` and log
  nothing when they change state.

The result: there is no single, deterministic log line that says "topic X has been circuit-broken"
or "topic X has recovered", independent of which producer/transport caused the failure. An operator
has to correlate scattered, differently-shaped log lines (or poll `TrippedTopics` directly) to know
the breaker state.

### Requirements Context

Standalone ADR — no parent spec. Raised directly from investigating current circuit-breaker log
output at the user's request.

### Constraints

- Follow Brighter's existing static-logger + source-generated `LoggerMessage` convention (see the
  nested `static partial class Log` in `OutboxProducerMediator.cs` and `CommandProcessor.cs`, backed
  by `ApplicationLogging.CreateLogger<T>()`), rather than constructor-injecting `ILogger`.
- `IAmAnOutboxCircuitBreaker` is a public interface with (at least potentially) third-party
  implementations; this ADR must not force a specific logging mechanism onto every implementation.
- Must not change `OutboxCircuitBreakerOptions` defaults or any other existing public behaviour
  (per CLAUDE.md's Change Scope guardrail).
- Must remain correct under concurrent `TripTopic`/`CoolDown` calls — `InMemoryOutboxCircuitBreaker`
  already has an atomicity guarantee here (see `When_cooldown_runs_concurrently_should_be_atomic`),
  and the logging change must not weaken it.
- Out of scope: `OutboxProducerMediator.Dispatch()` (the sync producer path) never calls `TripTopic`
  at all today, so sync-producer failures never trip the breaker. That is a separate correctness bug,
  not a logging gap, and is tracked independently rather than folded into this ADR.

## Decision

We will add the logging **inside `InMemoryOutboxCircuitBreaker`**, not in
`OutboxProducerMediator`, because the circuit breaker is the only component that actually owns the
trip state and can tell a *fresh* trip from a *re-trip*, and is the only component that sees
`CoolDown()` evictions (recoveries) at all. `OutboxProducerMediator.TripTopic()` is a thin,
fire-and-forget wrapper called from two different dispatch methods; putting the log there would
still miss `CoolDown()` entirely and would need an extra state lookup to detect re-trips, giving up
the "one location, one source of truth" property this ADR is for.

### Logging convention

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

### Three distinct, single-purpose transitions

1. **Fresh trip (Warning)** — the topic was healthy and just became tripped. This is the signal an
   operator/alert should act on.
2. **Re-trip (Debug)** — the topic was already tripped and a further failure reset its cooldown
   counter back to `CooldownCount`. While a topic is failing continuously, every failed publish
   attempt calls `TripTopic` again, so logging this at Warning would flood log/alerting sinks during
   an incident with a fact the operator already knows ("topic X is still broken"). Debug keeps it
   available for deep debugging without competing with the fresh-trip signal.
3. **Reset (Information)** — `CoolDown()` evicted the topic back to healthy. This closes the loop so
   "time tripped" can be measured from log timestamps alone, without polling `TrippedTopics`.

### Implementation sketch

`TripTopic` needs to know whether the topic was already present, which `ConcurrentDictionary.AddOrUpdate`
exposes via which factory delegate runs:

```csharp
public void TripTopic(RoutingKey topic)
{
    var isReTrip = false;
    var cooldownCount = _outboxCircuitBreakerOptions.CooldownCount;

    _trippedTopics.AddOrUpdate(topic,
        addValueFactory: _ => cooldownCount,
        updateValueFactory: (_, _) => { isReTrip = true; return cooldownCount; });

    if (isReTrip)
        Log.ReTripped(s_logger, topic.Value, cooldownCount);
    else
        Log.Tripped(s_logger, topic.Value, cooldownCount);
}
```

`CoolDown` already computes whether an eviction should happen; we log only when the conditional
`Remove` actually succeeds, so a race between two `CoolDown` passes (or a `CoolDown` racing a
`TripTopic` re-trip) logs `Reset` exactly once, for whichever call actually removed the entry. The
eviction-and-log check is pulled into a guard-clause helper so `CoolDown` itself stays at one level
of indentation:

```csharp
public void CoolDown()
{
    foreach (var trippedTopicsKey in _trippedTopics.Keys)
    {
        var cooled = _trippedTopics.AddOrUpdate(trippedTopicsKey, -1, (_, count) => count - 1);
        TryEvictAndLogReset(trippedTopicsKey, cooled);
    }
}

private void TryEvictAndLogReset(RoutingKey topic, int cooledValue)
{
    if (cooledValue >= 0)
        return;

    var removed = ((ICollection<KeyValuePair<RoutingKey, int>>)_trippedTopics)
        .Remove(new KeyValuePair<RoutingKey, int>(topic, cooledValue));

    if (removed)
        Log.Reset(s_logger, topic.Value);
}
```

No change to `IAmAnOutboxCircuitBreaker`, `OutboxCircuitBreakerOptions`, or any constructor
signature is required.

## Consequences

### Positive

- One deterministic, greppable phrase (`"Circuit breaker tripped for topic"` /
  `"Circuit breaker reset for topic"`) regardless of which producer/transport or dispatch path
  caused the failure — closing the gap identified during the original investigation.
- Recovery is now observable, not just the trip — enables measuring time-to-recovery, not only
  trip counts.
- No breaking change: `IAmAnOutboxCircuitBreaker`, `OutboxCircuitBreakerOptions`, and existing
  constructors are untouched.

### Negative

- Only covers `InMemoryOutboxCircuitBreaker`. A custom `IAmAnOutboxCircuitBreaker` implementation
  gets none of this for free and must add its own instrumentation.
- Still doesn't cover the sync `Dispatch()` gap — sync-producer failures remain invisible to the
  breaker (and therefore to this logging) until that separate bug is fixed.
- Re-trip is deliberately logged at Debug, so a deployment running at the default Information/Warning
  level won't see re-trip volume — only the initial trip and the eventual reset. Teams that want
  re-trip visibility must lower their minimum log level for this category.

### Risks and Mitigations

**Risk**: Log line order across threads could look contradictory (e.g. `Reset` appearing to precede
a concurrent `Tripped` for the same topic in a multi-threaded log stream).
- **Mitigation**: The underlying state (`_trippedTopics`) is already proven atomic under concurrency
  by `When_cooldown_runs_concurrently_should_be_atomic`; this ADR doesn't add synchronization purely
  for log-line ordering. Document this as a known limitation rather than over-engineering the fix.

**Risk**: Re-trip at Debug means it's easy to forget it exists and be surprised it's silent by default.
- **Mitigation**: Documented explicitly above and in the XML doc comments on `TripTopic`.

## Alternatives Considered

### Alternative 1: Log in `OutboxProducerMediator.TripTopic()` instead

**Rejected because**: it's a thin wrapper reached from two dispatch methods, but `CoolDown()`
(recovery) is called from a completely different path (`ClearOutstandingFromOutbox`) that this
wrapper never touches — logging would need a second site anyway. It also can't cheaply tell a fresh
trip from a re-trip without an extra `TrippedTopics` lookup, duplicating state the breaker already has.

### Alternative 2: Constructor-inject `ILogger<InMemoryOutboxCircuitBreaker>`

**Rejected because**: it doesn't match the convention used everywhere else in this codebase
(`OutboxProducerMediator`, `CommandProcessor`) of a static logger via `ApplicationLogging.CreateLogger<T>()`.
Introducing a second logging style for one class adds inconsistency for no behavioural benefit.

### Alternative 3: Log every trip at the same (Warning) severity, with no fresh/re-trip distinction

**Rejected because**: during a sustained topic outage every failed publish attempt calls `TripTopic`
again, so this would flood Warning-level sinks/alerts with a fact already known ("topic X is still
broken"), which is precisely the noisy-but-uninformative logging this ADR is trying to move away from.

### Alternative 4: Expose trip/reset as metrics (counter/gauge) instead of, or in addition to, logs

**Rejected for this ADR**: the ask was specifically for logging. A complementary OpenTelemetry
counter/gauge is a reasonable follow-up but is a separate architectural decision, kept out to honour
CLAUDE.md's "one architectural decision per ADR" guidance.

## References

- [ADR 0028: Support Circuit Breaking of Topics](0028-support-circuit-breaking-of-topics.md) — establishes the breaker this ADR instruments.
- `src/Paramore.Brighter/CircuitBreaker/InMemoryOutboxCircuitBreaker.cs`
- `src/Paramore.Brighter/CircuitBreaker/IAmAnOutboxCircuitBreaker.cs`
- `src/Paramore.Brighter/OutboxProducerMediator.cs` — existing `Log` partial class convention and current `TripTopic` call sites.
- `src/Paramore.Brighter/Logging/ApplicationLogging.cs` — static logger factory convention followed here.
