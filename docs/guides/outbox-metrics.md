# Outbox Metrics Operator Guide

Paramore.Brighter emits three OpenTelemetry metrics instruments from the Outbox pipeline. They are
independent of distributed tracing and sampling: you receive counts and durations even when no trace
is being captured.

---

## Instruments

| Name | Type | Unit | Description |
|---|---|---|---|
| `paramore.brighter.outbox.added.messages` | `Counter<long>` | `{message}` | Number of messages written to the Outbox. |
| `paramore.brighter.outbox.cleared.messages` | `Counter<long>` | `{message}` | Number of messages recorded as dispatched from the Outbox. |
| `paramore.brighter.outbox.publish.duration` | `Histogram<double>` | `s` | Time a message waited between being created for the Outbox and being recorded as dispatched. |

All three instruments are on the `Paramore.Brighter` meter.

### Attributes

Every recording carries at most two attributes.

| Key | Values | Instruments |
|---|---|---|
| `messaging.destination.name` | The message's routing key (topic). Empty string when the routing key is absent. | All three |
| `paramore.brighter.outbox.clear_source` | `"explicit"`, `"sweeper"`, `"unknown"` | `cleared.messages`, `publish.duration` |

**Attribute cardinality** is bounded by the number of distinct destinations × the number of clear sources (3). A deployment with 10 topics therefore produces at most 10 attribute sets on `added.messages` and at most 30 on `cleared.messages` and `publish.duration`.

### Clear-source values

| Value | Meaning |
|---|---|
| `"explicit"` | The clear was triggered explicitly by the application (`ClearOutbox` / `ClearOutboxAsync`). |
| `"sweeper"` | The clear was triggered by the background Outbox sweeper (`ClearOutstandingFromOutboxAsync`). |
| `"unknown"` | The clear source could not be determined. See [Unknown source](#unknown-source) below. |

---

## Registration

Outbox metrics are exported automatically through the existing `AddBrighterInstrumentation()` call.
No extra step is required:

```csharp
services.AddOpenTelemetry()
    .WithMetrics(b => b
        .AddBrighterInstrumentation()   // already includes the Outbox instruments
        .AddOtlpExporter());
```

### Providing your own OutboxMeter

To attach service-resource attributes to every recording (e.g. `service.name`), pass a
`MeterProvider` to `OutboxMeter`:

```csharp
// Register the meter as a singleton so the mediator uses it
services.AddSingleton<IAmABrighterOutboxMeter>(sp =>
{
    var meterProvider = sp.GetRequiredService<MeterProvider>();
    return new OutboxMeter(meterProvider);
});
```

The mediator receives `IAmABrighterOutboxMeter` from the container when built via `AddBrighter()`.
If no instance is registered, the mediator creates its own `OutboxMeter()` without service attributes.

---

## Duration measurement

> **Warning:** The `publish.duration` histogram does not measure store-to-broker latency.

The measured value is `MarkDispatched` instant − `Header.TimeStamp`. `Header.TimeStamp` is set when
the message is created (before mapping and transformation). The measured duration therefore:

- **Includes** message mapping and transform time.
- **Is subject to clock skew** when the writer and the sweeper run on different hosts.
- **Clamps negative values to zero** rather than recording them, so a small skew does not produce
  impossible negative histogram values.
- **Does not measure broker round-trip time.** Use broker-provided latency metrics for that.

On **confirmation transports** (producers implementing `ISupportPublishConfirmation`) the duration
is also measured from `Header.TimeStamp` to the `MarkDispatched` instant inside the confirmation
callback, not to the send instant.

---

## Circuit breaker and the rising-added / flat-cleared pattern

> **Warning:** A tripped circuit-breaker topic shows `added.messages` rising while
> `cleared.messages` stays flat for that destination.

When `IAmAnOutboxCircuitBreaker` trips a destination, the sweeper skips its messages. `added.messages`
continues to increment as new messages are deposited, but `cleared.messages` stops incrementing.
The ratio `cleared / (cleared + added)` for that destination will trend toward zero.

Alert on: `rate(paramore_brighter_outbox_added_messages[5m]) > 0` for a destination where
`rate(paramore_brighter_outbox_cleared_messages[5m]) == 0` for more than a few minutes.

See ADR 0028 and the circuit-breaker documentation for recovery options.

---

## Unknown source and fewer samples than cleared units

On **confirmation transports**, each dispatched message is held in an in-process pending-clear map
until the broker confirmation arrives. Three conditions remove it before a confirmation can claim it:

1. **Empty `MessageId`** in the confirmation result — the broker reported success but did not
   identify which message.
2. **TTL eviction** — the entry has been in the map for more than 5 minutes without a confirmation.
   This typically indicates a lost confirmation or a very slow broker.
3. **Map cap** — the map holds at most 10,000 entries. When the cap is reached a new dispatch is
   not tracked; its eventual confirmation records `"unknown"`.

When any of these conditions applies, `cleared.messages` still increments (the message _was_
dispatched), but `publish.duration` receives **no sample** (there is no creation time to compute
the wait from). The `clear_source` is `"unknown"`.

To compute the sweeper ratio correctly, include unknown in the denominator:

```promql
# Sweeper fraction of all clears (Prometheus)
sum by (messaging_destination_name) (
    rate(paramore_brighter_outbox_cleared_messages_total{
        paramore_brighter_outbox_clear_source="sweeper"}[5m])
)
/
sum by (messaging_destination_name) (
    rate(paramore_brighter_outbox_cleared_messages_total[5m])
)
```

---

## Counting semantics

- `added.messages` is incremented **after** a successful Outbox write. A write that throws never
  increments the counter.
- `cleared.messages` is incremented **after** a successful `MarkDispatched` call. A send that
  fails (or a `MarkDispatched` that throws) never increments the counter.
- Both counters use **at-least-once semantics**. If a message is cleared twice (e.g. an explicit
  clear followed by a sweeper re-dispatch with `ForceOutstanding`) both clears are counted.
- On confirmation transports, a second successful confirmation for the same message ID also
  increments `cleared.messages`, but with `clear_source="unknown"` (the pending-clear entry was
  already removed by the first confirmation).

---

## Histogram bucket boundaries

The `publish.duration` histogram uses the following explicit bucket boundaries (in seconds):

```
0.005  0.01  0.025  0.05  0.075  0.1  0.25  0.5  0.75  1
2.5    5     7.5    10    30     60   300   600
```

To override the boundaries for your own deployment, add an OpenTelemetry view:

```csharp
builder.AddView(
    instrumentName: "paramore.brighter.outbox.publish.duration",
    metricStreamConfiguration: new ExplicitBucketHistogramConfiguration
    {
        Boundaries = [0.1, 1.0, 10.0, 60.0, 300.0]
    });
```

---

## Known gaps

- **Sync pipeline over-count on add:** When a `RequestContext.ResilienceContext` is set, the sync
  resilience pipeline may return `true` without invoking the add action. The `added.messages`
  counter is incremented even though nothing was stored. This is a known limitation of the sync
  resilience pipeline path and does not affect the async path.
- **Sync confirmation send cleanup:** When a sync confirming-producer send fails under
  `ResilienceContext`, the pending-clear entry is not removed immediately (unlike the async path).
  The entry persists until the 5-minute TTL purge. Operators may see a slightly elevated
  `"unknown"` rate on sync confirmation transports.
