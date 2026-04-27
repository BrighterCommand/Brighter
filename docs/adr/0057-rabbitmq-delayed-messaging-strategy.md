# 57. RabbitMQ Delayed Messaging Strategy (Post Plugin Archival)

Date: 2026-04-27

## Status

Proposed

## Context

Brighter's RabbitMQ transports — both [`Paramore.Brighter.MessagingGateway.RMQ.Sync`](../../src/Paramore.Brighter.MessagingGateway.RMQ.Sync) and [`Paramore.Brighter.MessagingGateway.RMQ.Async`](../../src/Paramore.Brighter.MessagingGateway.RMQ.Async) — expose `Exchange.SupportDelay = true` as the opt-in for broker-side message delay. Setting that flag changes the exchange declaration to type `x-delayed-message`, which requires the [`rabbitmq_delayed_message_exchange`](https://github.com/rabbitmq/rabbitmq-delayed-message-exchange) community plugin to be installed on the broker. Brighter's CI test image (`brightercommand/rabbitmq`) bundles the plugin to exercise this path.

Three upstream events converge to make the current design untenable:

1. **The plugin's repository was archived** by Team RabbitMQ on 2026-01-29. Its final release is `v4.2.0-rc.1` — a release candidate. No stable 4.2 release will ever ship.
2. **RabbitMQ 4.3** (released 2026-04-21) [removed Mnesia entirely](https://www.rabbitmq.com/blog/2026/04/23/rabbitmq-4.3-release) and is Khepri-only. The plugin's persistence layer is built on Mnesia, so the plugin cannot run on 4.3+.
3. **Team RabbitMQ now recommends** TTL + DLX patterns or external schedulers for OSS users, and steers paying users to VMware Tanzu RabbitMQ's [`rabbitmq_delayed_queue`](https://techdocs.broadcom.com/us/en/vmware-tanzu/data-solutions/tanzu-rabbitmq-ova/4-2/tanzu-rabbitmq-ova-virtual-machine/site-delayed-queues.html) (Raft-replicated, commercial).

The terminal working combination — RMQ 4.2 + plugin v4.2.0-rc.1 — is being adopted in PR #4104, but that is a holding pattern. We need a long-term answer.

### What Brighter already has

A scheduler-based fallback already exists. In [`RmqMessageConsumer.RequeueAsync`](../../src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageConsumer.cs):

```csharp
if (DelaySupported || timeout <= TimeSpan.Zero)
{
    var rmqMessagePublisher = new RmqMessagePublisher(Channel, Connection);
    await rmqMessagePublisher.RequeueMessageAsync(message, _queueName, timeout.Value, cancellationToken);
}
else
{
    EnsureProducer();
    await _requeueProducer!.SendWithDelayAsync(message, timeout, cancellationToken);
}
```

When `DelaySupported` is `false`, requeue-with-delay routes through `IAmAMessageScheduler`. Brighter ships six production schedulers — `Paramore.Brighter.MessageScheduler.{Aws, AWS.V4, Azure, Hangfire, Quartz, TickerQ}` — plus an `InMemoryScheduler`. The pieces for a no-broker-plugin path are already in the box.

The `SendWithDelay` API surface (used directly by callers, not just by requeue) is the other consumer of the plugin path; that one currently has no equivalent fallback when `SupportDelay = false`.

## Options under consideration

### Option A — Stay on RabbitMQ 4.2 + plugin (status quo after PR #4104)

Pin the test broker and any user opting into `SupportDelay` at RMQ 4.2 forever. Plugin remains at `v4.2.0-rc.1`.

- **Pros**: zero code change; existing users keep working; familiar semantics; one-line `x-delay` header per message.
- **Cons**: locks Brighter's test broker at RMQ 4.2 indefinitely; users on `SupportDelay` cannot upgrade to 4.3+; plugin is unmaintained, so a future OTP/Erlang/RabbitMQ patch could silently break it; we own a deprecation surface that upstream has already disowned.
- **Migration cost**: nil.
- **Time horizon**: viable until Erlang/OTP or RabbitMQ 4.2.x maintenance ends — likely 1–2 years.

### Option B — Drop the plugin path; rely on `IAmAMessageScheduler`

Treat `IAmAMessageScheduler` as the only supported delay mechanism. Mark `Exchange.SupportDelay` `[Obsolete]`; route both `SendWithDelay` and `RequeueAsync(timeout)` through the scheduler when no plugin is present. Remove the plugin-aware code paths in the next major.

- **Pros**: leverages infrastructure Brighter already ships; decouples from broker-version drift; works on RMQ 3.x, 4.x, 5.x, and any future major; same code path as AWS/Azure/Kafka transports that lack native delay; consistent operator story.
- **Cons**: requires consumers to register a scheduler (Hangfire/Quartz/TickerQ/etc.); introduces a second piece of infrastructure; semantics shift slightly — delayed messages no longer sit in a broker queue, they sit in the scheduler's store; visibility moves from RabbitMQ Management UI to whatever the scheduler exposes.
- **Migration cost**: low for users — register an `IAmAMessageScheduler` in DI; ~moderate for Brighter — extend `SendWithDelay` to use scheduler, deprecate `SupportDelay`.
- **Time horizon**: durable across RMQ majors.

### Option C — Implement TTL + DLX in the RMQ transport

Make Brighter's RMQ transport synthesise broker-native delay via a queue with `x-message-ttl` (or per-message TTL) plus `x-dead-letter-exchange` pointing at the destination. Two sub-variants:

- **C1 — Per-queue TTL with delay buckets.** Declare a fixed set of holding queues with TTLs (e.g. 1s / 10s / 1m / 5m / 1h / 1d), each dead-lettering to the target exchange. Round each `SendWithDelay` request up to the nearest bucket.
- **C2 — Per-message TTL into a single buffer queue.** One buffer queue per destination, dead-lettering on expiry. Each message carries `expiration` (per-message TTL).

- **Pros**: pure RabbitMQ — no plugin, no external infrastructure; works on any RMQ version.
- **Cons (C1)**: precision is quantised to bucket size; many bucket queues to manage; a 7-second delay either rounds up to 10s (correct-ish) or down to 1s (wrong); not a drop-in replacement for the plugin's millisecond-precision per-message delay.
- **Cons (C2)**: **head-of-line blocking** — RabbitMQ only dead-letters from the *head* of a queue. If a 1-hour-TTL message is queued before a 1-second-TTL message, the 1-second message waits an hour. The [official docs](https://www.rabbitmq.com/docs/ttl) call this out explicitly. This makes C2 a poor fit for arbitrary-precision delay.
- **Migration cost**: moderate-high for Brighter — non-trivial transport change, queue topology management, test coverage; nil for users on the API surface.
- **Time horizon**: durable.

### Option D — Adopt VMware Tanzu RabbitMQ's `rabbitmq_delayed_queue`

Tanzu RabbitMQ ships a closed-source replacement: a new queue type `x-queue-type: delayed` using Ra (Raft) for replication, scaling to "tens or hundreds of millions of delayed messages", supporting `x-delay`, `x-opt-delivery-delay`, and `x-opt-delivery-time` headers.

- **Pros**: official, supported, replicated, scalable; `x-delay` header is preserved, so very small Brighter-side change (declare the queue with the new type instead of declaring an `x-delayed-message` exchange).
- **Cons**: **commercial only** — Tanzu RabbitMQ is a Broadcom product. OSS RabbitMQ users are excluded. Brighter cannot make this its default path without alienating its OSS user base.
- **Migration cost**: small for Tanzu users; impossible for OSS users.
- **Time horizon**: durable for paying users.

### Option E — Mark `Exchange.SupportDelay` `[Obsolete]` and remove next major

A signalling option, not standalone. Layered on top of A, B, or C.

- **Pros**: cheapest possible response; aligns Brighter with upstream's archival signal; gives users a release window to migrate.
- **Cons**: alone, it doesn't pick a replacement — must combine with B and/or C.

## Decision

**Recommendation: B + E combined, with documentation for D.**

1. **In the current major**, keep the plugin path working (RMQ 4.2 + `v4.2.0-rc.1`, as PR #4104 lands). Mark `Exchange.SupportDelay` `[Obsolete("Configure an IAmAMessageScheduler instead — the rabbitmq_delayed_message_exchange plugin is archived upstream and incompatible with RabbitMQ 4.3+. See ADR 0057.")]`. Extend `SendWithDelay` to use `IAmAMessageScheduler` when registered, falling through to the plugin path only when the scheduler is absent and `SupportDelay = true`.
2. **In the next major**, remove the plugin-aware code paths. `SendWithDelay` and requeue-with-delay route exclusively through `IAmAMessageScheduler`. The CI test image stops bundling the plugin and pins to plain `rabbitmq:management`.
3. **For Tanzu users**, add a documentation note in [`docs/contents/RabbitMQConfiguration.md`](../contents/RabbitMQConfiguration.md) describing how to declare `x-queue-type: delayed` queues if they want to keep broker-side delay. No code change needed — Tanzu accepts the same `x-delay` header Brighter already emits, so Brighter's wire format is forward-compatible.

We **reject Option A** because indefinite RMQ 4.2 lock-in is a slow-motion failure mode.

We **reject Option C** because the engineering cost (especially for fair per-queue-bucket implementations with their own ADR-worthy edge cases) is high relative to the value, given Brighter already has six scheduler implementations and an established "register a scheduler when the transport lacks native delay" pattern (used by AWS classic and Azure where applicable).

We **reject Option D as Brighter's default** because it excludes OSS users, but document it as a supported configuration for Tanzu users.

## Consequences

### Positive

- One delay implementation across all transports without native broker-side delay (RMQ, Redis), reducing surface area.
- Decouples Brighter's RMQ support from broker-version drift; we can move freely to RMQ 4.3, 4.4, 5.0+.
- Operators get a single set of metrics/dashboards (their scheduler) for all delayed messages regardless of transport.
- No new transport code to maintain; we lean on infrastructure already in `Paramore.Brighter.MessageScheduler.*`.
- Removes a known-broken-on-4.3 dependency that we'd otherwise have to keep patching around.

### Negative

- Users currently relying on `SupportDelay = true` must register an `IAmAMessageScheduler` before the next major. This is a real migration, not just a renaming.
- Delayed messages are no longer visible in RabbitMQ Management UI as queued-but-not-yet-delivered; they sit in the scheduler's store. Operators using the RMQ UI for visibility will need to look elsewhere.
- Sub-second delay precision now depends on the scheduler implementation. Hangfire and Quartz have polling intervals (default ~15s for Hangfire); TickerQ is finer-grained. Users with strict sub-second SLAs need to pick the scheduler appropriately.
- Adds a deployment dependency for delay users (a scheduler backend), which the plugin path did not require beyond the broker.

### Risks and Mitigations

- **Risk**: The deprecation window catches users by surprise, breaking production at the next major. **Mitigation**: ship the `[Obsolete]` warning one minor before removal; flag prominently in release notes; leave the plugin path working in the current major so warning-suppression is reversible.
- **Risk**: Some user has built around the broker-side queue visibility for delayed messages. **Mitigation**: document the visibility shift in the migration guide; for users on Tanzu, point them at Option D.
- **Risk**: Scheduler choice paralysis — users don't know which of six to pick. **Mitigation**: add a table to the RMQ docs comparing the six scheduler backends (in-process vs distributed, polling interval, persistence). Recommend TickerQ for in-process, Hangfire or Quartz for distributed deployments.

## Alternatives Considered

The five options above are the alternatives. A summary of why each non-recommended option was rejected:

- **A** (status quo): defers the problem rather than solves it; locks the broker version.
- **C** (TTL+DLX): higher engineering cost than B for inferior semantics (precision quantised or head-of-line blocked).
- **D** (Tanzu plugin): not viable as a default for an OSS framework; documented as a supported configuration.

## References

- Tracking issue: [#4105](https://github.com/BrighterCommand/Brighter/issues/4105)
- Holding-pattern PR: [#4104](https://github.com/BrighterCommand/Brighter/pull/4104) (RMQ 4.2 + plugin v4.2.0-rc.1)
- Plugin archival: <https://github.com/rabbitmq/rabbitmq-delayed-message-exchange>
- RabbitMQ 4.3 (Mnesia removed): <https://www.rabbitmq.com/blog/2026/04/23/rabbitmq-4.3-release>
- TTL + DLX official docs: <https://www.rabbitmq.com/docs/ttl>, <https://www.rabbitmq.com/docs/dlx>
- Tanzu delayed queues: <https://techdocs.broadcom.com/us/en/vmware-tanzu/data-solutions/tanzu-rabbitmq-ova/4-2/tanzu-rabbitmq-ova-virtual-machine/site-delayed-queues.html>
- Brighter scheduler interface: [`src/Paramore.Brighter/IAmAMessageScheduler.cs`](../../src/Paramore.Brighter/IAmAMessageScheduler.cs)
- Existing fallback site: [`src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageConsumer.cs`](../../src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageConsumer.cs) (the `else` branch in `RequeueAsync`)
