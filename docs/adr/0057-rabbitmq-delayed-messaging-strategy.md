# 57. RabbitMQ Delayed Messaging Strategy (Post Plugin Archival)

Date: 2026-04-27

## Status

Proposed

## Context

**Scope**: This ADR addresses how Brighter's RabbitMQ transports should provide delayed messaging now that the upstream [`rabbitmq_delayed_message_exchange`](https://github.com/rabbitmq/rabbitmq-delayed-message-exchange) plugin has been archived and is incompatible with RabbitMQ 4.3+.

### The Problem

Brighter's RabbitMQ transports — both [`Paramore.Brighter.MessagingGateway.RMQ.Sync`](../../src/Paramore.Brighter.MessagingGateway.RMQ.Sync) and [`Paramore.Brighter.MessagingGateway.RMQ.Async`](../../src/Paramore.Brighter.MessagingGateway.RMQ.Async) — expose `Exchange.SupportDelay = true` as the opt-in for broker-side message delay. Setting that flag changes the exchange declaration to type `x-delayed-message`, which requires the community plugin to be installed on the broker. Brighter's CI test image (`brightercommand/rabbitmq`) bundles the plugin to exercise this path.

Three upstream events converge to make the current design untenable:

1. **The plugin's repository was archived** by Team RabbitMQ on 2026-01-29. Its final release is `v4.2.0-rc.1` — a release candidate. No stable 4.2 release will ever ship.
2. **RabbitMQ 4.3** (released 2026-04-21) [removed Mnesia entirely](https://www.rabbitmq.com/blog/2026/04/23/rabbitmq-4.3-release) and is Khepri-only. The plugin's persistence layer is built on Mnesia, so the plugin cannot run on 4.3+.
3. **Team RabbitMQ now recommends** TTL + DLX patterns or external schedulers for OSS users, and steers paying users to VMware Tanzu RabbitMQ's [`rabbitmq_delayed_queue`](https://techdocs.broadcom.com/us/en/vmware-tanzu/data-solutions/tanzu-rabbitmq-ova/4-2/tanzu-rabbitmq-ova-virtual-machine/site-delayed-queues.html) (Raft-replicated, commercial).

The terminal working combination — RMQ 4.2 + plugin v4.2.0-rc.1 — is being adopted in PR #4104 as a holding pattern. We need a long-term answer.

### Constraints

- **Existing role available.** `IAmAMessageScheduler` is already a role in Brighter, with six production implementations: `Paramore.Brighter.MessageScheduler.{Aws, AWS.V4, Azure, Hangfire, Quartz, TickerQ}`, plus an `InMemoryScheduler`. Other transports without native broker-side delay (notably AWS classic where applicable) already use this role.
- **Partial fallback already wired.** [`RmqMessageConsumer.RequeueAsync`](../../src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageConsumer.cs#L419) routes through the scheduler when `DelaySupported = false`:
  ```csharp
  if (DelaySupported || timeout <= TimeSpan.Zero)
      await rmqMessagePublisher.RequeueMessageAsync(message, _queueName, timeout.Value, cancellationToken);
  else
      await _requeueProducer!.SendWithDelayAsync(message, timeout, cancellationToken);
  ```
  The `SendWithDelay` producer surface, used directly by callers (not just by requeue), does **not** yet have an equivalent fallback.
- **OSS users must keep working.** Brighter cannot mandate Tanzu RabbitMQ — its OSS user base is significant and would be excluded by a commercial-only path.
- **Migration window required.** Existing users with `Exchange.SupportDelay = true` should not break at the next minor release. A deprecation warning before removal is required.
- **Wire format compatibility.** RabbitMQ's various delay implementations (community plugin, Tanzu plugin) all accept the `x-delay` header Brighter already emits. Decisions about *where* the delay is enforced are independent of *how* the message is shaped on the wire.

## Decision

Combine **Option B** (route delay through `IAmAMessageScheduler`) with **Option E** (deprecate `Exchange.SupportDelay` and remove next major), with **Option D** (Tanzu) documented as a supported configuration for paying users.

### Phase 1 — Current major

1. Mark `Exchange.SupportDelay` `[Obsolete("Configure an IAmAMessageScheduler instead — the rabbitmq_delayed_message_exchange plugin is archived upstream and incompatible with RabbitMQ 4.3+. See ADR 0057.")]`.
2. Extend the producer's `SendWithDelay` path to use `IAmAMessageScheduler` when one is registered, falling through to the plugin path only when no scheduler is registered and `SupportDelay = true` (preserves current behaviour for users who haven't migrated).
3. Keep the plugin path operational against RMQ 4.2 + plugin v4.2.0-rc.1 (PR #4104 already adopts this pinning).
4. Add a migration guide section to [`docs/contents/RabbitMQConfiguration.md`](../contents/RabbitMQConfiguration.md) showing how to register a scheduler and remove `SupportDelay`.

### Phase 2 — Next major

1. Remove the plugin-aware code paths in `ExchangeConfigurationHelper`, the `x-delay` exchange declaration, and the `DelaySupported` branch in `RmqMessageConsumer.RequeueAsync`.
2. Make `IAmAMessageScheduler` registration mandatory for any caller of `SendWithDelay` or `RequeueAsync(timeout: nonzero)` on RMQ. Throw a `ConfigurationException` at startup if delay is used without a registered scheduler.
3. Stop bundling the delay plugin in the CI test image; pin to plain `rabbitmq:management`.

### Tanzu RabbitMQ support

For users on Tanzu, document — in [`docs/contents/RabbitMQConfiguration.md`](../contents/RabbitMQConfiguration.md) — how to declare `x-queue-type: delayed` queues and rely on Tanzu's `rabbitmq_delayed_queue` plugin. No code change is required: Tanzu accepts the same `x-delay` header Brighter already emits, so Brighter's wire format is forward-compatible.

### Responsibilities

Following Responsibility-Driven Design (per [.agent_instructions/design_principles.md](../../.agent_instructions/design_principles.md)):

- **Knowing the delay value** — the `Message` and its routing slip carry the requested delay. Unchanged.
- **Deciding where to enforce delay** — currently split between `Exchange.SupportDelay` (broker enforces) and the absence of that flag (scheduler enforces, in the requeue path only). After Phase 2, this responsibility consolidates into a single *coordinator* role: the producer asks "is a scheduler registered?" and there is exactly one path.
- **Doing the delay** — currently a *service-provider* role split between RabbitMQ-via-plugin (broker side) and `IAmAMessageScheduler` (Brighter side). After Phase 2, only the latter remains.

This consolidation matches the design-principles guidance: "There should be one — and preferably only one — obvious way to do it."

## Consequences

### Positive

- One delay implementation across all transports without native broker-side delay (RMQ, Redis), reducing surface area.
- Decouples Brighter's RMQ support from broker-version drift; we can move freely to RMQ 4.3, 4.4, 5.0+.
- Operators get a single set of metrics/dashboards (their scheduler) for all delayed messages regardless of transport.
- No new transport code to maintain; we lean on the `IAmAMessageScheduler` role already in use elsewhere in Brighter.
- Removes a known-broken-on-4.3 dependency that we'd otherwise have to keep patching around.
- Aligns Brighter's signal with RabbitMQ upstream's archival signal.

### Negative

- Users currently relying on `SupportDelay = true` must register an `IAmAMessageScheduler` before the next major. This is a real migration, not just a renaming.
- Delayed messages are no longer visible in the RabbitMQ Management UI as queued-but-not-yet-delivered; they sit in the scheduler's store. Operators using the RMQ UI for delay visibility will need to look elsewhere.
- Sub-second delay precision now depends on the scheduler implementation. Hangfire and Quartz have polling intervals (default ~15s for Hangfire); TickerQ is finer-grained. Users with strict sub-second SLAs need to pick the scheduler appropriately.
- Adds a deployment dependency for delay users (a scheduler backend), which the plugin path did not require beyond the broker.

### Risks and Mitigations

**Risk**: The deprecation window catches users by surprise, breaking production at the next major.
- **Mitigation**: Ship the `[Obsolete]` warning one minor before removal; flag prominently in release notes; leave the plugin path working in the current major so warning-suppression is reversible.

**Risk**: A user has built around the broker-side queue visibility for delayed messages.
- **Mitigation**: Document the visibility shift in the migration guide; for users on Tanzu, point them at the documented Tanzu configuration.

**Risk**: Scheduler choice paralysis — users don't know which of six to pick.
- **Mitigation**: Add a comparison table to the RMQ docs (in-process vs distributed, polling interval, persistence backend). Recommend TickerQ for in-process, Hangfire or Quartz for distributed deployments.

**Risk**: The Phase 1 fallback ordering (scheduler-if-registered, else plugin) introduces ambiguity for users who have *both* a scheduler and `SupportDelay = true`.
- **Mitigation**: Document explicitly that registering a scheduler takes precedence; emit an `[Obsolete]` warning that says exactly this; surface it in `[Obsolete]` message text.

## Alternatives Considered

The five candidate options were examined in full. Summary:

| | Option | Migration cost | Time horizon | Outcome |
|---|---|---|---|---|
| **A** | Stay on RMQ 4.2 + plugin (status quo) | nil | 1–2 yrs | rejected as default |
| **B** | Drop plugin path; route via `IAmAMessageScheduler` | low (users) / moderate (Brighter) | durable | **chosen** |
| **C** | Implement TTL + DLX in the RMQ transport | moderate-high (Brighter) | durable | rejected |
| **D** | Adopt Tanzu RabbitMQ `rabbitmq_delayed_queue` | small (Tanzu) / impossible (OSS) | durable for paying users | rejected as default; documented as supported config |
| **E** | Mark `[Obsolete]`, remove next major | nil — signalling option | — | **adopted as part of B** |

### Alternative A: Stay on RabbitMQ 4.2 + plugin

Pin the test broker and any user opting into `SupportDelay` at RMQ 4.2 forever. Plugin remains at `v4.2.0-rc.1`.

**Rejected because**:
- Locks Brighter's test broker (and any user opting into `SupportDelay`) at RMQ 4.2 indefinitely.
- The plugin is unmaintained — a future OTP/Erlang/RabbitMQ patch could silently break it with no upstream fix path.
- Brighter would own a deprecation surface that upstream has already disowned.
- Defers the problem rather than solves it; lifetime ~1–2 years until 4.2.x maintenance ends.

### Alternative C: Implement TTL + DLX inside the RMQ transport

Synthesise broker-native delay using `x-message-ttl` plus `x-dead-letter-exchange`. Two sub-variants: (C1) per-queue TTL with a fixed set of bucket queues (1s/10s/1m/5m/1h/1d) routing on expiry to the destination; (C2) per-message TTL into a single buffer queue.

**Rejected because**:
- C1 quantises delay precision to bucket size — a 7-second delay rounds up to 10s or down to 1s; not a drop-in replacement for the plugin's millisecond-precision `x-delay`.
- C2 suffers [head-of-line blocking](https://www.rabbitmq.com/docs/ttl): RabbitMQ only dead-letters from the *head* of a queue, so a 1-hour-TTL message queued before a 1-second-TTL message blocks the latter for an hour.
- Higher engineering cost than B (queue topology management, test coverage across two semantics) for inferior semantics.
- Brighter already ships and tests `IAmAMessageScheduler`; reimplementing scheduling inside the RMQ transport duplicates knowledge.

### Alternative D as default: VMware Tanzu RabbitMQ delayed queues

Tanzu RabbitMQ ships a closed-source replacement: a queue type `x-queue-type: delayed` using Ra (Raft) replication, scaling to "tens or hundreds of millions of delayed messages", supporting `x-delay`, `x-opt-delivery-delay`, and `x-opt-delivery-time` headers.

**Rejected as default because**:
- Tanzu RabbitMQ is a commercial Broadcom product. OSS RabbitMQ users would be excluded.
- Brighter cannot make this its default path without alienating its OSS user base.

**Documented as a supported configuration** — Brighter's wire format is already compatible (it emits `x-delay`), so Tanzu users only need to declare delayed queues at their topology layer. See §Decision/Tanzu RabbitMQ support.

## References

- Tracking issue: [#4105](https://github.com/BrighterCommand/Brighter/issues/4105)
- Holding-pattern PR: [#4104](https://github.com/BrighterCommand/Brighter/pull/4104) (RMQ 4.2 + plugin v4.2.0-rc.1)
- Plugin archival: <https://github.com/rabbitmq/rabbitmq-delayed-message-exchange>
- RabbitMQ 4.3 (Mnesia removed): <https://www.rabbitmq.com/blog/2026/04/23/rabbitmq-4.3-release>
- TTL + DLX official docs: <https://www.rabbitmq.com/docs/ttl>, <https://www.rabbitmq.com/docs/dlx>
- Tanzu delayed queues: <https://techdocs.broadcom.com/us/en/vmware-tanzu/data-solutions/tanzu-rabbitmq-ova/4-2/tanzu-rabbitmq-ova-virtual-machine/site-delayed-queues.html>
- Brighter scheduler interface: [`src/Paramore.Brighter/IAmAMessageScheduler.cs`](../../src/Paramore.Brighter/IAmAMessageScheduler.cs)
- Existing fallback site: [`src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageConsumer.cs`](../../src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageConsumer.cs) (the `else` branch in `RequeueAsync`)
- Design principles applied: [.agent_instructions/design_principles.md](../../.agent_instructions/design_principles.md) (Responsibility-Driven Design — knowing/doing/deciding)
