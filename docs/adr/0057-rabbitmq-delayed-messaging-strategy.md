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

- **Existing role available.** `IAmAMessageScheduler` is already a role in Brighter, with six production implementations: `Paramore.Brighter.MessageScheduler.{Aws, AWS.V4, Azure, Hangfire, Quartz, TickerQ}`, plus an `InMemoryScheduler`. Other transports without native broker-side delay already use this role.
- **Fallback already wired across both transports and both surfaces.** When a scheduler is registered and `DelaySupported = false`, the producers route through `IAmAMessageScheduler` directly. The producer guard is in `RmqMessageProducer.SendWithDelayAsync` (Async transport) and `RmqMessageProducer.SendWithDelay` (Sync transport):
  ```csharp
  if (delay == TimeSpan.Zero || DelaySupported || Scheduler == null)
  {
      // plugin path: publish to the x-delayed-message exchange
  }
  else if (useSchedulerAsync)
  {
      var schedulerAsync = (IAmAMessageSchedulerAsync)Scheduler!;
      await schedulerAsync.ScheduleAsync(message, delay.Value, cancellationToken);
  }
  else
  {
      var schedulerSync = (IAmAMessageSchedulerSync)Scheduler!;
      schedulerSync.Schedule(message, delay.Value);
  }
  ```
  The consumer requeue path — `RmqMessageConsumer.RequeueAsync` (Async) and `RmqMessageConsumer.Requeue` (Sync) — has a *simpler* condition (`if (DelaySupported || timeout <= TimeSpan.Zero) plugin else producer.SendWithDelay(Async)`); it has no `Scheduler == null` guard of its own. The consumer **delegates** to a `_requeueProducer` constructed in `EnsureProducer` with `Scheduler = _scheduler` set, inheriting the scheduler-fallback by composition rather than duplicating the guard. This is correct but asymmetric, and matters in §Phase 1: any change to the producer condition automatically flows through to requeue.

  Runtime work for Option B is therefore essentially complete; what remains is signalling (deprecation), documentation, and eventual removal.
- **DI auto-wires `Scheduler` onto producers.** `Paramore.Brighter.Extensions.DependencyInjection.ServiceCollectionExtensions.BuildCommandProcessor` does:
  ```csharp
  producerRegistry?.Producers
      .Each(x => x.Scheduler ??= messageSchedulerFactory.Create(command));
  ```
  Users who register an `IAmAMessageSchedulerFactory` (via the standard `AddBrighter(...).UseScheduler(...)` pattern) get the scheduler propagated to every producer in the registry automatically — no per-producer wiring required. Migration is therefore: remove `SupportDelay = true`, register a scheduler factory, done.
- **OSS users must keep working.** Brighter cannot mandate Tanzu RabbitMQ — its OSS user base is significant and would be excluded by a commercial-only path.
- **Migration window required.** Existing users with `Exchange.SupportDelay = true` should not break at the next minor release. A deprecation warning before removal is required.
- **Wire format compatibility.** RabbitMQ's various delay implementations (community plugin, Tanzu plugin) all accept the `x-delay` header Brighter already emits. Decisions about *where* the delay is enforced are independent of *how* the message is shaped on the wire.

## Decision

Combine **Option B** (route delay through `IAmAMessageScheduler`) with **Option E** (deprecate `Exchange.SupportDelay` and remove next major), with **Option D** (Tanzu) documented as a supported configuration for paying users.

### Phase 1 — Current major (target version: TBD on acceptance)

1. **Mark every public surface that exposes the plugin opt-in `[Obsolete]`.** Specifically:
   - `Exchange.SupportDelay` (the property setter and the `supportDelay` constructor parameter on the `Exchange` type)
   - `RmqMessageGateway.DelaySupported` (the public read-only property derived from `SupportDelay`, in both the Sync and Async transports)

   `[Obsolete]` text along the lines of: `"Configure an IAmAMessageScheduler instead — the rabbitmq_delayed_message_exchange plugin is archived upstream and incompatible with RabbitMQ 4.3+. See ADR 0057."` Implementation should choose the most-narrow target (the `supportDelay` parameter via documentation rather than the whole `Exchange` constructor, if other parameters remain valid) — this is a PR-level decision, not an ADR one.
2. **Pick and document the runtime precedence rule.** The current code in `RmqMessageProducer` reads:
   ```csharp
   if (delay == TimeSpan.Zero || DelaySupported || Scheduler == null) { /* plugin path */ }
   else if (...) { /* scheduler path */ }
   ```
   So today, **the plugin wins whenever `SupportDelay = true`**, regardless of whether a scheduler is also registered. The scheduler path activates only when `SupportDelay = false` AND a scheduler is registered AND `delay != TimeSpan.Zero`. Phase 1 must make a decision and document it:
    - **(a) Keep current behaviour** — plugin wins when `SupportDelay = true`. `[Obsolete]` is a signal only; users must remove `SupportDelay = true` to activate the scheduler. Phase 1 is documentation-only.
    - **(b) Flip the precedence** — when `Scheduler` is registered, scheduler wins even if `SupportDelay = true`. This is a silent behaviour change for users who set both, but smoothes the migration. Phase 1 includes a one-line condition change.

   **Recommended: (a).** Silent behaviour changes inside a major are exactly what `[Obsolete]` is meant to avoid; the warning gives users an explicit prompt to remove the flag, after which they get the new path on their schedule.
3. **Keep the plugin path operational** against RMQ 4.2 + plugin v4.2.0-rc.1 for users who haven't yet migrated. PR #4104 already adopts this pinning.
4. **Update the user-facing RabbitMQ documentation** in the [`BrighterCommand/Docs`](https://github.com/BrighterCommand/Docs) sibling repository — primarily [`contents/RabbitMQConfiguration.md`](https://github.com/BrighterCommand/Docs/blob/master/contents/RabbitMQConfiguration.md), with cross-links to [`contents/BrighterSchedulerSupport.md`](https://github.com/BrighterCommand/Docs/blob/master/contents/BrighterSchedulerSupport.md) and the per-scheduler pages already there ([`TickerQScheduler.md`](https://github.com/BrighterCommand/Docs/blob/master/contents/TickerQScheduler.md), [`HangfireScheduler.md`](https://github.com/BrighterCommand/Docs/blob/master/contents/HangfireScheduler.md), [`QuartzScheduler.md`](https://github.com/BrighterCommand/Docs/blob/master/contents/QuartzScheduler.md), [`InMemoryScheduler.md`](https://github.com/BrighterCommand/Docs/blob/master/contents/InMemoryScheduler.md), [`AwsScheduler.md`](https://github.com/BrighterCommand/Docs/blob/master/contents/AwsScheduler.md), [`AzureScheduler.md`](https://github.com/BrighterCommand/Docs/blob/master/contents/AzureScheduler.md), [`CustomScheduler.md`](https://github.com/BrighterCommand/Docs/blob/master/contents/CustomScheduler.md)). The migration section should add: how to register a scheduler for RMQ delay; the precedence rule from step 2; an explicit caveat that `InMemoryScheduler` is **not durable and not safe for production delayed messaging** — pending delays are held in process memory only and **silently disappear on restart with no error raised**; use TickerQ for in-process production, Hangfire/Quartz for distributed deployments; a comparison table of scheduler backends (polling interval, persistence, distributed vs in-process). Lands as a separate PR against `BrighterCommand/Docs`.

### Phase 2 — Next major (target version: TBD on acceptance)

1. **Remove the plugin-aware code paths**:
   - `ExchangeConfigurationHelper` — drop the `if (connection.Exchange.SupportDelay) { x-delayed-type / x-delayed-message }` block in **both** Sync and Async transports.
   - `RmqMessageProducer` — drop the `DelaySupported` branch in the `SendWithDelay` condition in **both** transports.
   - `RmqMessageConsumer.RequeueAsync` (Async) and `RmqMessageConsumer.Requeue` (Sync) — drop the `DelaySupported` branch.
   - `RmqMessageGateway.DelaySupported` and `Exchange.SupportDelay` — delete (after the `[Obsolete]` window).
   - `Paramore.Brighter.IAmAMessageGatewaySupportingDelay` — orphan public interface in `src/Paramore.Brighter/IAmAMessageGatewaySupportingDelay.cs` with **zero current implementations or callers** (a Serena/LSP search across `src`, `tests`, and `samples` finds none). It's the abstract role for "broker supports delay" that the new strategy retires; safe to delete in Phase 2 alongside the rest.
2. **Throw `ConfigurationException` at call-time** (not at startup — delay is decided per-call by the caller, so the check has to live inside `SendWithDelay`/`RequeueAsync` when `delay > TimeSpan.Zero && Scheduler == null`). Brighter already exposes `Paramore.Brighter.ConfigurationException`.
3. **Stop bundling the delay plugin** in the CI test image; pin to plain `rabbitmq:management`.

### Tanzu RabbitMQ support

For users on Tanzu, document how to declare `x-queue-type: delayed` queues and rely on Tanzu's `rabbitmq_delayed_queue` plugin. No code change is required: Tanzu's [delayed-queues documentation](https://techdocs.broadcom.com/us/en/vmware-tanzu/data-solutions/tanzu-rabbitmq-ova/4-2/tanzu-rabbitmq-ova-virtual-machine/site-delayed-queues.html) lists `x-delay` as a recognised delay header (alongside the higher-priority `x-opt-delivery-time` and `x-opt-delivery-delay`), so Brighter's existing wire format is forward-compatible. Documentation lands in [`BrighterCommand/Docs/contents/RabbitMQConfiguration.md`](https://github.com/BrighterCommand/Docs/blob/master/contents/RabbitMQConfiguration.md) alongside the migration guide added in Phase 1, step 4.

### Responsibilities

Following Responsibility-Driven Design:

- **Knowing the delay value** — the `Message` and its routing slip carry the requested delay. Unchanged.
- **Deciding where to enforce delay** — currently split between `Exchange.SupportDelay` (broker enforces) and the absence of that flag (scheduler enforces). After Phase 2, this responsibility consolidates into a single *coordinator* role: the producer asks "is a scheduler registered?" and there is exactly one path.
- **Doing the delay** — currently a *service-provider* role split between RabbitMQ-via-plugin (broker side) and `IAmAMessageScheduler` (Brighter side). After Phase 2, only the latter remains.

This consolidation matches the long-standing principle in Brighter's design guidelines: "There should be one — and preferably only one — obvious way to do it."

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
- **Pre-existing silent no-delay failure mode is exposed by Phase 1 documentation, not introduced.** Today, when a caller passes `delay > TimeSpan.Zero` to `SendWithDelay`/`RequeueAsync` with `SupportDelay = false` and no `Scheduler` registered, the producer condition `delay == TimeSpan.Zero || DelaySupported || Scheduler == null` evaluates `false || false || true` and takes the plugin path, publishing with the `x-delay` header to a normal exchange. The broker silently ignores the header and the message is delivered immediately, with no error. Documenting the migration surfaces this gap; Phase 2's call-time `ConfigurationException` (step 2) closes it permanently.

### Risks and Mitigations

**Risk**: The deprecation window catches users by surprise, breaking production at the next major.
- **Mitigation**: Ship the `[Obsolete]` warning one minor before removal; flag prominently in release notes; leave the plugin path working in the current major so warning-suppression is reversible.

**Risk**: A user has built around the broker-side queue visibility for delayed messages.
- **Mitigation**: Document the visibility shift in the migration guide; for users on Tanzu, point them at the documented Tanzu configuration.

**Risk**: Scheduler choice paralysis — users don't know which of six to pick, and `InMemoryScheduler` looks attractive because it needs no extra infrastructure.
- **Mitigation**: Add a comparison table to the RMQ docs (in-process vs distributed, polling interval, persistence backend). Call out explicitly that `InMemoryScheduler` is non-durable and not appropriate for production delayed messaging — its delays are lost on process restart. Recommend TickerQ for in-process production, Hangfire or Quartz for distributed deployments.

**Risk**: Users who configured both `SupportDelay = true` and a scheduler may misunderstand which path runs today. Current code takes the **plugin path** in that case (see §Decision/Phase 1 step 2); the scheduler activates only after the user removes `SupportDelay = true`.
- **Mitigation**: Under recommended sub-option (a) the runtime behaviour is unchanged — only the documentation is new. The `[Obsolete]` warning surfaces in the IDE the moment users open the file, prompting them to remove the flag at their pace.

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
- Brighter `ConfigurationException` (used by Phase 2 step 2): [`src/Paramore.Brighter/ConfigurationException.cs`](../../src/Paramore.Brighter/ConfigurationException.cs)
- Plugin opt-in surfaces (targets for `[Obsolete]` in Phase 1 step 1):
  - `Exchange.SupportDelay` / `supportDelay` ctor param — referenced in `src/Paramore.Brighter.MessagingGateway.RMQ.{Sync,Async}/RmqMessagingGatewayConnection.cs`
  - `RmqMessageGateway.DelaySupported` — derived public property, both transports
- Orphan public interface to delete in Phase 2 step 1: [`src/Paramore.Brighter/IAmAMessageGatewaySupportingDelay.cs`](../../src/Paramore.Brighter/IAmAMessageGatewaySupportingDelay.cs) — no implementations, no callers
- DI scheduler propagation: [`src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs`](../../src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs) (`BuildCommandProcessor` — `producerRegistry.Producers.Each(x => x.Scheduler ??= ...)`)
- Plugin-aware code paths (targets for removal in Phase 2 step 1):
  - Producer guard: `RmqMessageProducer.SendWithDelayAsync` (Async) and `RmqMessageProducer.SendWithDelay` (Sync)
  - Consumer requeue guard: `RmqMessageConsumer.RequeueAsync` (Async) and `RmqMessageConsumer.Requeue` (Sync) — note these delegate to the producer rather than duplicating its `Scheduler == null` guard
  - Exchange declaration: `ExchangeConfigurationHelper.CreateExchange` in both transports (`x-delayed-type` argument and `x-delayed-message` exchange-type override)
- User-facing documentation (sibling repo [`BrighterCommand/Docs`](https://github.com/BrighterCommand/Docs)):
  - [`contents/RabbitMQConfiguration.md`](https://github.com/BrighterCommand/Docs/blob/master/contents/RabbitMQConfiguration.md) — primary target for the migration guide
  - [`contents/BrighterSchedulerSupport.md`](https://github.com/BrighterCommand/Docs/blob/master/contents/BrighterSchedulerSupport.md) — scheduler overview to cross-link from the migration guide
- User-facing documentation (sibling repo [`BrighterCommand/Docs`](https://github.com/BrighterCommand/Docs)):
  - [`contents/RabbitMQConfiguration.md`](https://github.com/BrighterCommand/Docs/blob/master/contents/RabbitMQConfiguration.md) — primary target for the migration guide
  - [`contents/BrighterSchedulerSupport.md`](https://github.com/BrighterCommand/Docs/blob/master/contents/BrighterSchedulerSupport.md) — scheduler overview to cross-link from the migration guide
