---
id: 0066-conformance-test-provider-and-ungating
title: "Conformance-Test Provider Interface Extension and Capability-Gate Retirement"
status: Proposed
author:
  - "Ian Cooper"
created: 2026-07-18
summary: "Extends the generated messaging-gateway provider interfaces (IAmAMessageGatewayReactorProvider / IAmAMessageGatewayProactorProvider) with explicit DLQ + invalid-message routing keys, invalid-channel read, in-memory and spy scheduler-backed channels (scheduler wired into the backing consumer per ADR 0039), and a strongly-typed RejectionMetadataKeys accessor; retires the HasSupportToDelayedMessages / HasSupportToDeadLetterQueue / HasSupportToRequeue opt-in gates; and deletes the broken with_delay requeue template (superseded by FR-2/FR-3)."
tags:
  - "test-generation"
  - "testing"
  - "message-rejection"
  - "dead-letter-queue"
---

# 0066. Conformance-Test Provider Interface Extension and Capability-Gate Retirement

Date: 2026-07-18

## Status

Proposed

## Context

Brighter guarantees a family of universal channel/producer obligations for *every* transport:
`Reject` routing a message to a dead-letter (DLQ) or invalid-message channel per a fallback
ladder, requeue-with-delay (natively or via the producer's `Scheduler`), `SendWithDelay`,
`Nack` for redelivery, and rejection-metadata stamping. The messaging-gateway **test
generator** (`tools/Paramore.Brighter.Test.Generator`), introduced by
`0037-add-messaging-gateway-generated-test`, does not currently generate conformance tests for
these obligations universally. Instead it gates them behind three opt-in
*native-capability* switches on `MessagingGatewayConfiguration`:

- `HasSupportToDelayedMessages` — gates any template whose filename contains `delayed_message`
  or `with_delay` (`MessagingGatewayGenerator.SkipTest`, lines 122 and 127);
- `HasSupportToDeadLetterQueue` — gates `dead_letter_queue` (line 132);
- `HasSupportToRequeue` — gates `requeuing` (line 145).

This is wrong on two counts.

**(a) The gates conceptually mis-model the behaviour.** These are universal Brighter
obligations, provided *natively* by some transports and *via Brighter's scheduler/producer
fallback* by others — not opt-in features. `Reject(Message, MessageRejectionReason?)` and
`Requeue(Message, TimeSpan?)` are on `IAmAChannelSync`/`IAmAChannelAsync`
(`src/Paramore.Brighter/IAmAChannelSync.cs` lines 64, 83) for every transport, and the
`RejectionReason` enum (`None`/`Unacceptable`/`DeliveryError`) plus the
`MessageRejectionReason` record (`src/Paramore.Brighter/MessageRejectionReason.cs`) are core
types. The canonical reject/DLQ/scheduler behaviours are already decided by
`0047-message-rejection-routing-strategy` (the fallback ladder + origin-metadata enrichment),
`0037-universal-scheduler-delay` (delayed requeue via `IAmAMessageScheduler`/`InMemoryScheduler`),
`0045-provide-dlq-where-missing` (Brighter-managed DLQ + invalid channel), and
`0039-transport-scheduler-wiring`. The generated suite should *assert those contracts hold*,
not gate them.

**(b) The gate values are mis-declared against the code** — set to whatever keeps the suite
green. Verified in `tests/Paramore.Brighter.*.Tests/test-configuration.json`: PostgreSQL
declares `HasSupportToDeadLetterQueue: false` **and** `HasSupportToDelayedMessages: false`;
AWS declares `HasSupportToDelayedMessages: false` in three of its four gateway configurations
(the fourth declares `true`) despite native `DelaySeconds`; and Kafka — the transport whose
hand-written suite is the canonical grounding for these templates — declares **all three** gates
`false` (`HasSupportToRequeue`, `HasSupportToDeadLetterQueue`, `HasSupportToDelayedMessages`) in
both its Standard and PartitionKey gateway configs, which is the sharpest illustration that these
values track "what keeps the suite green" rather than what the gateway does. Because the
`with_delay` filename matches both `requeuing` and `with_delay`, that template is today *doubly*
gated.

The root cause is timing: the reject→DLQ / invalid-channel / requeue-via-producer /
requeue-via-scheduler features post-date the generator, so contributors hand-wrote them per
transport (the richest set is `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/`). The
result is broad but inconsistent coverage, and the one generated `with_delay` template is
broken (FR-12).

Crucially, the generator's **provider interface cannot express what the canonical tests
need.** The current interfaces
(`tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/{Reactor,Proactor}/IAmAMessageGateway*Provider.cs.liquid`)
expose only `CreateSubscription(..., bool setupDeadLetterQueue = false)`, `CreatePublication`,
`CreateProducer`/`CreateProducerAsync`, `CreateChannel`/`CreateChannelAsync`, `CleanUp`, and
`GetMessageFromDeadLetterQueue`/`...Async`. They cannot construct a channel with *both* a
dead-letter **and** an invalid-message routing key; cannot vary DLQ-only / invalid-only /
neither; cannot read from the invalid channel; cannot hand back a channel whose backing consumer
carries a scheduler (in-memory or spy) — `CreateChannel` takes only a subscription, so a test has
no way to reach the scheduler the requeue path actually consults; and cannot surface the
transport's rejection-metadata key names.
Those key names genuinely diverge per transport — Kafka stamps PascalCase
(`src/Paramore.Brighter.MessagingGateway.Kafka/HeaderNames.cs`: `OriginalTopic`, `OriginalType`,
`RejectionReason`, `RejectionMessage`, `RejectionTimestamp`) while Redis
(`RedisMessageConsumer.RefreshMetadata`) and SQS (`SqsMessageConsumer.RefreshMetadata`) stamp
camelCase (`originalTopic`, `originalMessageType`, `rejectionReason`, `rejectionMessage`,
`rejectionTimestamp`) — and the divergence is *more than casing*: the "original message type"
field is `OriginalType` on Kafka but `originalMessageType` on Redis/SQS. Only the *semantic set*
is universal, which is exactly why the provider must own the key names (per constraint C-2:
these are **not** a shared core type).

**Why now:** making the ten canonical behaviours universal and ungated (the parent
requirement) is only possible once the provider interface can drive them; extending that
interface and retiring the gates are the same change.

**Parent Requirement**: [specs/0036-universal-transport-conformance-tests/requirements.md](../../specs/0036-universal-transport-conformance-tests/requirements.md)

**Scope**: This ADR decides (1) the provider-interface extension (FR-1), (2) retiring the three
capability gates and correcting the mis-declared configs (FR-10 / FR-11), and (3) replacing the
broken `with_delay` template (FR-12). The per-transport conformance-fix *sequencing* (FR-13),
the detailed *content* of each individual canonical template (FR-2…FR-9, FR-16, FR-17) beyond
what the provider must expose to make them writable, and any reintroduction of a `HasNative*`
flag (explicitly rejected, OOS-1) are **addressed separately** — FR-13 in the sibling ADR
[0067](0067-conformance-rollout-and-deferral-governance.md), the template content as generator work
under this spec's tasks.

## Decision

We extend both generated provider interfaces to expose the full surface the canonical
conformance tests drive, retire the three capability gates so the canonical templates generate
universally in both Reactor and Proactor variants, and delete the broken `with_delay` template
(superseded by FR-2/FR-3).

**One deliberate departure from the literal requirement wording.** FR-1(4) and AC-1 ask for a member
that returns "a producer whose `Scheduler` property is set". We do **not** provide that, because the
canonical FR-3 test drives `channel.Requeue(M, delay)` and a standalone producer's scheduler is not
on that path (see Architecture Overview). We realise FR-1(4)/AC-1 instead as a **channel whose
backing consumer carries the scheduler** — which is where the runtime actually reads it. This is
within the latitude requirements.md grants ("the exact shape of the provider-interface extension …
producer-with-scheduler factory" is explicitly deferred to this ADR), and AC-1's wording is amended
to match so it does not read as unmet at verification.

### Architecture Overview

The provider is a **service-provider / interfacer** role (per Responsibility-Driven Design): it
is the single seam between a transport-agnostic generated test and a transport-specific gateway.

- What the provider **knows**: this transport's actual `Header.Bag` rejection-metadata key
  strings (Kafka PascalCase vs Redis/SQS camelCase), and how to build that transport's
  subscriptions, channels, and producers — including how to wire a scheduler into the
  **consumer that backs a channel**.
- What the provider **does**: create publications/subscriptions/channels/producers; read a
  message back from the DLQ and from the invalid channel; hand back a **channel whose backing
  consumer's `Scheduler`** is either an in-memory scheduler or a recording spy (and, for the spy,
  the spy itself so the test can assert on it).

A generated canonical test knows *nothing* transport-specific; it consumes only the role:

```
IAmAMessageGatewayReactorProvider (extended)
  RoutingKey    GetOrCreateRoutingKey([CallerMemberName] ...)
  ChannelName   GetOrCreateChannelName([CallerMemberName] ...)
  {Publication} CreatePublication(routingKey, makeChannels = Create)

  // FR-1(1)(2): DLQ + invalid routing keys, both nullable -> DLQ-only / invalid-only / neither
  {Subscription} CreateSubscription(
        RoutingKey routingKey, ChannelName channelName, OnMissingChannel makeChannel,
        RoutingKey? deadLetterRoutingKey = null,
        RoutingKey? invalidMessageRoutingKey = null)

  IAmAMessageProducerSync CreateProducer({Publication} publication)

  IAmAChannelSync CreateChannel({Subscription} subscription)

  // FR-1(4): a channel whose BACKING CONSUMER has its Scheduler set — two distinct members (NFR-4).
  // The scheduler is wired into the consumer that backs the channel (the seam ADR 0039 established
  // via the channel factory), NOT into a standalone producer — because the FR-3 test drives
  // channel.Requeue(M, delay), and the consumer lazily creates the requeue-producer internally.
  IAmAChannelSync     CreateChannelWithInMemoryScheduler({Subscription} subscription)
  SpyScheduledChannel CreateChannelWithSpyScheduler({Subscription} subscription)
        // SpyScheduledChannel holds { IAmAChannelSync Channel; SpySchedulerSync Spy }
        // Spy records ScheduleCalled / ScheduledDelay; the test calls Channel.Requeue(M, 5s)

  // FR-1(3): read back from DLQ (exists) and from the invalid channel (new)
  Message GetMessageFromDeadLetterQueue({Subscription} subscription)
  Message GetMessageFromInvalidChannel({Subscription} subscription)

  // FR-1(5): transport's own key names, strongly typed
  RejectionMetadataKeys RejectionMetadataKeys { get; }

  void CleanUp(IAmAMessageProducerSync?, IAmAChannelSync?, IEnumerable<Message>)
```

`IAmAMessageGatewayProactorProvider` mirrors this exactly with `...Async` members returning
`Task`/`Task<...>` and taking `CancellationToken` (`CreateSubscription` is synchronous config
construction and stays sync; `CreateChannelAsync`, `CreateProducerAsync`,
`CreateChannelWithInMemorySchedulerAsync`, `CreateChannelWithSpySchedulerAsync` (returning a
`SpyScheduledChannelAsync` holding an `IAmAChannelAsync` and a `SpySchedulerAsync`),
`GetMessageFromDeadLetterQueueAsync`, `GetMessageFromInvalidChannelAsync` are async;
`RejectionMetadataKeys` is a plain property shared by both interfaces).

`RejectionMetadataKeys` is a small immutable value type (record) exposing the universal
semantic set as named members returning this transport's actual key strings:

```csharp
public sealed record RejectionMetadataKeys(
    string OriginalTopic,      // Kafka "OriginalTopic"      | Redis/SQS "originalTopic"
    string OriginalType,       // Kafka "OriginalType"       | Redis/SQS "originalMessageType"
    string RejectionReason,    // Kafka "RejectionReason"    | Redis/SQS "rejectionReason"
    string RejectionMessage,   // Kafka "RejectionMessage"   | Redis/SQS "rejectionMessage"
    string RejectionTimestamp);// Kafka "RejectionTimestamp" | Redis/SQS "rejectionTimestamp"
```

A metadata test then reads `provider.RejectionMetadataKeys.OriginalTopic` rather than hard-coding
any one transport's strings — the semantic set is asserted uniformly (FR-8), the key names come
from the provider.

For the scheduler-fallback path (FR-3), the spy is the shape already proven in
`tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Reactor/When_kafka_consumer_requeues_with_delay_should_use_scheduler.cs`:
`SpySchedulerSync : IAmAMessageSchedulerSync` exposing `ScheduleCalled` and `ScheduledDelay`.
**Critically, that test wires the scheduler into the consumer** — `new KafkaMessageConsumer(...,
scheduler: _scheduler)` — and asserts against `_consumer.Requeue(received, 5s)`, *not* against a
producer it holds. The consumer lazily creates its requeue-producer and sets *that* producer's
`Scheduler` (`IAmAMessageProducer.Scheduler`, `src/Paramore.Brighter/IAmAMessageProducer.cs` line
46). So the provider sets the scheduler on the **consumer that backs the channel** — the seam
`0039-transport-scheduler-wiring` established via the channel factory — and hands the test the
channel plus the spy: `CreateChannelWithSpyScheduler` returns a `SpyScheduledChannel { Channel, Spy }`,
and the generated FR-3 test calls `channel.Requeue(M, 5s)` then asserts `spy.ScheduleCalled` and
`spy.ScheduledDelay == 5s`. A standalone producer-with-scheduler factory would be **unobservable
through `channel.Requeue`**, which is the surface the canonical test drives (Objective and Test
Boundary) — hence the member is channel-level, not producer-level.

### Key Components

- The two provider interface templates:
  `.../Templates/MessagingGateway/{Reactor,Proactor}/IAmAMessageGateway*Provider.cs.liquid`.
- `MessagingGatewayGenerator.SkipTest`
  (`.../Generators/MessagingGatewayGenerator.cs`) — loses three branches.
- `MessagingGatewayConfiguration`
  (`.../Configuration/MessagingGatewayConfiguration.cs`) — loses three properties.
- A new `RejectionMetadataKeys` record (in the generated test-support namespace, per transport,
  **not** in `src/Paramore.Brighter` — C-2).
- A recording spy scheduler (`SpySchedulerSync` / `SpySchedulerAsync`) wired into the consumer that
  backs the channel returned by `CreateChannelWithSpyScheduler`, plus the `SpyScheduledChannel`
  holder (`{ Channel, Spy }`), for the FR-3 delegation assertion.
- Every hand-written per-transport provider implementation that satisfies these interfaces —
  e.g. `tests/Paramore.Brighter.PostgresSQL.Tests/MessagingGateway/PostgresMessageGatewayProvider.cs`,
  the Kafka/Redis/MSSQL/RMQ/AWS/GCP/RocketMQ providers — must be extended to implement the new
  members (see Consequences).
- Every `tests/Paramore.Brighter.*.Tests/test-configuration.json` — the three keys removed.

### Technology Choices

- **Strongly-typed `RejectionMetadataKeys` (record) over a `string`-keyed dictionary.** The
  semantic set is fixed and known at author time; named members give refactor-safe, discoverable
  call sites (`provider.RejectionMetadataKeys.OriginalTopic`) and make an omitted field a
  compile error rather than a silent `null` lookup. This is exactly the FR-1(5) example. A
  dictionary would reintroduce stringly-typed access — the primitive-obsession the design
  principles reject.
- **Both an in-memory scheduler *and* a recording spy, exposed as channel-level members (NFR-4).**
  FR-3's requirement is that requeue-with-delay is proven to route through the scheduler on the
  consumer that backs the channel, not native delay. Some canonical tests want a black-box
  redelivery assertion (in-memory `InMemoryScheduler` actually re-publishes after the delay); others
  want a delegation assertion (spy records `ScheduleCalled` / `ScheduledDelay == 5s`). Each test
  picks the right tool, so the provider supplies both — as `CreateChannelWithInMemoryScheduler` and
  `CreateChannelWithSpyScheduler`. They are **channel-level, not producer-level**, because the seam
  that carries the scheduler is the consumer/channel factory (ADR 0039); a standalone
  producer-with-scheduler would be unobservable through the `channel.Requeue` the test drives.
  The in-memory arm's wiring cost is **not** incidental and is specified below rather than waved at.
- **Delete (not fix) the `with_delay` template.** FR-2 (requeue-with-delay via producer) and FR-3
  (via scheduler fallback) fully supersede it and are stronger (they pass a non-null delay and use
  bounded retry loops); keeping a fixed-in-place third template would duplicate knowledge.

#### What `CreateChannelWithInMemoryScheduler` actually requires

The in-memory arm only delivers its advertised black-box assertion if the provider stands up enough
of Brighter for a scheduled message to come back out on the transport. `InMemoryScheduler` is not a
self-contained timer — it is a *dispatcher into a command processor*. The real chain
(`src/Paramore.Brighter/InMemoryScheduler.cs`) is:

```
scheduler.Schedule(message, delay)
  -> timeProvider.CreateTimer(Execute, (processor, FireSchedulerMessage{Message, Async=false}), delay)
  -> Execute: BrighterAsyncContext.Run(() => processor.SendAsync(fireSchedulerMessage))   // line 285
  -> FireSchedulerMessageHandler.HandleAsync -> processor.Post(command)                   // Scheduler/Handlers
  -> OutboxProducerMediator unwraps FireSchedulerMessage and produces the inner Message    // lines 458, 483
  -> message reappears on the transport topic
```

So a provider implementing `CreateChannelWithInMemoryScheduler` MUST supply, per transport:

1. an `IAmACommandProcessor` (a real `CommandProcessor`) — not a stub, because the timer callback
   calls `SendAsync` on it;
2. a handler pipeline that resolves `FireSchedulerMessage` to `FireSchedulerMessageHandler`
   (`src/Paramore.Brighter/Scheduler/Handlers/FireSchedulerMessageHandler.cs`);
3. an external bus / `OutboxProducerMediator` with a producer registry bound to **this transport's**
   topic, so the unwrapped inner `Message` is actually produced;
4. a `TimeProvider` (real, or a fake to compress the delay), the two scheduler-id factory funcs
   (`Func<IRequest,string>`, `Func<Message,string>`), and an `OnSchedulerConflict` policy — the
   `InMemoryScheduler` primary-constructor parameters.

The provider owns all four so the generated test does not. Note no existing test constructs
`InMemoryScheduler` directly — every current usage goes through `InMemorySchedulerFactory` inside a
full dispatcher setup — so this is new per-provider wiring, and it is the single most expensive part
of implementing the extended interface. Where a transport cannot yet supply it, that is a
conformance gap handled under ADR 0067's ledger (a `Deferred` row), not a reason to skip FR-3: the
**spy arm has none of these prerequisites** (the spy is assigned straight onto the consumer's
scheduler and records the call), so FR-3's delegation assertion remains available even where the
in-memory arm is deferred.

### Implementation Approach

**Provider interface — before → after (Reactor; Proactor mirrors).**

Current (verbatim):
```csharp
{{ Subscription }} CreateSubscription(RoutingKey routingKey, ChannelName channelName,
    OnMissingChannel makeChannel, bool setupDeadLetterQueue = false);
...
Message GetMessageFromDeadLetterQueue({{ Subscription }} subscription);
```
After: replace the `bool setupDeadLetterQueue` with explicit
`RoutingKey? deadLetterRoutingKey = null, RoutingKey? invalidMessageRoutingKey = null` (both
null ⇒ "neither"; one set ⇒ DLQ-only or invalid-only ⇒ FR-6/FR-7); add
`GetMessageFromInvalidChannel`, `CreateChannelWithInMemoryScheduler`,
`CreateChannelWithSpyScheduler` (returning `SpyScheduledChannel`), and the `RejectionMetadataKeys`
property. The `bool` overload
is the one thing removed rather than added; a transport that previously derived its DLQ routing
key internally (as PostgreSQL does when `setupDeadLetterQueue` is true) now receives the routing
key explicitly from the test, which also removes hidden per-transport DLQ-naming knowledge from
the provider.

**`SkipTest` loses three branches.** Delete the `HasSupportToDelayedMessages`/`delayed_message`
branch (lines 122–125), the `HasSupportToDelayedMessages`/`with_delay` branch (lines 127–130),
the `HasSupportToDeadLetterQueue`/`dead_letter_queue` branch (lines 132–135), and the
`HasSupportToRequeue`/`requeuing` branch (lines 145–148). The retained gates
(`HasSupportToPublishConfirmation`/`confirming_posting`,
`HasSupportToValidateBrokerExistence`/`no_broker_created`,
`HasSupportToValidateInfrastructure`/`assume_channel`/`validate_channel`) are untouched — this
ADR retires only the three named gates.

**Config surface loses three properties.** Remove `HasSupportToDelayedMessages`,
`HasSupportToDeadLetterQueue`, and `HasSupportToRequeue` from `MessagingGatewayConfiguration`
(lines 91, 96, 106) and remove the keys from every `test-configuration.json` — including the
mis-declared PostgreSQL (`false`/`false`), AWS SQS (`false`), and Kafka
(`HasSupportToRequeue: false`) values (FR-11). Removing the keys (rather than correcting them to
`true`) is the AC-11 outcome: after FR-10 the flags do not exist, so a stale value cannot mislead.

**Delete the broken template.** Remove
`.../Templates/MessagingGateway/{Reactor,Proactor}/When_requeuing_a_failed_message_with_delay_should_receive_message_again.cs.liquid`
(the Reactor variant calls `_channel.Requeue(received);` with no delay after a `SendWithDelay` +
`Thread.Sleep(6s)` — line 62). This is the AC-12 **replace** arm: after deletion, a
template-source inspection MUST confirm no messaging-gateway template calls `Requeue` /
`RequeueAsync` without a non-null `TimeSpan` delay argument. The still-valid plain-requeue
template (`When_requeuing_a_failed_message_should_receive_message_again.cs.liquid`, ungated by
FR-10) covers the no-delay path; FR-2/FR-3 cover the delayed path.

**Reactor/Proactor parity (FR-14).** Every change above lands in *both* template trees and both
provider interfaces — the async members return `Task`/`Task<...>` and take a
`CancellationToken`, matching the existing dual layout. Parity here exercises the distinct sync
vs async gateway code paths; it is not pump re-testing.

## Consequences

### Positive

- The ten canonical reject/DLQ/invalid-channel/requeue-with-delay/delayed-send/Nack behaviours
  become universal and ungated — every transport, both variants — so adding or changing a
  transport proves conformance instead of relying on hand-written duplicates.
- The provider interface finally expresses the full test surface; canonical templates become
  writable without hard-coding any transport's key strings or DLQ naming.
- Deleting the mis-declared gates removes a class of "green because the flag says so" false
  confidence; the configs no longer carry misleading values.
- Strongly-typed `RejectionMetadataKeys` gives refactor-safe metadata assertions across the
  genuinely divergent Kafka/Redis/SQS key names.
- One obvious way to test delayed requeue (FR-2/FR-3) instead of two overlapping templates.

### Negative

- **Every per-transport provider implementation must be extended** to satisfy the wider
  interface (Kafka, Redis, MSSQL, PostgreSQL, RMQ, AWS SNS/SQS, GCP, RocketMQ, …). A transport
  that cannot yet supply an invalid channel, a scheduler-backed channel, or a metadata-key set
  surfaces as a compile or implementation gap. This is *intended* (it is how non-conformance
  becomes visible per FR-13), but it is real work and is the bulk of the follow-on effort.
- **The in-memory scheduler arm is expensive per provider.** `CreateChannelWithInMemoryScheduler`
  obliges every provider to stand up a real `CommandProcessor`, a `FireSchedulerMessage` handler
  pipeline, and an external bus with a producer registry bound to that transport's topic (see
  "What `CreateChannelWithInMemoryScheduler` actually requires"). That is materially more work than
  any other member on the interface, it is repeated across every provider implementation (there are
  ~20 `*MessageGatewayProvider.cs` files, more than the nine test projects), and no existing test
  builds `InMemoryScheduler` directly today. We accept this cost to keep the black-box redelivery
  assertion available (NFR-4); the mitigation is that FR-3 remains provable via the spy arm alone,
  so a provider may defer the in-memory arm as an ADR-0067 `Deferred` row without losing FR-3
  coverage.
- Removing the three properties is a **breaking change** to `MessagingGatewayConfiguration` and
  to the `test-configuration.json` schema; any external tooling that reads those keys breaks.
- Replacing `bool setupDeadLetterQueue` with explicit routing-key parameters is a breaking
  change to `CreateSubscription`; every provider and every existing generated caller must move to
  the new signature in the same change.

### Risks and Mitigations

- *Risk*: newly-ungated tests fail for some transports (e.g. GCP has no coverage today). *This is
  the point*, not a regression — handled under FR-13 (fix-to-conform, or a named, linked,
  signed-off deferral; never a silent `[Skip]`). Sequencing is a separate ADR.
- *Risk*: timing flakiness in delayed/redelivery assertions. *Mitigation*: NFR-2 bounded
  receive-retry loops (as in the plain-requeue template), and the spy-scheduler variant removes
  timing from the delegation assertion entirely.
- *Risk*: provider implementations drift from the interface. *Mitigation*: the interface is
  generated and compiled against each hand-written provider, so drift is a build break.

## Alternatives Considered

1. **Keep the three flags but correct their values.** Rejected: it re-entrenches the
   native/non-native distinction the user explicitly rejects (OOS-1) — "we don't want to test
   *how*, we want to test *supported*." The behaviours are universal obligations, not opt-in
   capabilities, so a per-transport switch is the wrong model regardless of value.
2. **Dictionary / `string`-keyed metadata lookup instead of `RejectionMetadataKeys`.** Rejected:
   stringly-typed call sites, no compile-time guarantee the semantic set is complete, and it
   reintroduces primitive obsession. The strongly-typed record makes an omitted field a compile
   error and reads as intent.
3. **Fix the `with_delay` template in place (FR-12 option a).** Rejected: redundant with and
   weaker than FR-2 (producer) + FR-3 (scheduler fallback); keeping it duplicates knowledge and
   leaves a third delayed-requeue template to maintain.
4. **Spy-only or in-memory-only scheduler member.** Rejected: NFR-4 wants the right tool per
   test — black-box redelivery (in-memory) for outcome assertions, spy for delegation
   assertions. Supplying only one forces the other kind of test into an awkward or weaker shape.
5. **A shared core `RejectionMetadataKeys` type in `src/Paramore.Brighter`.** Rejected per C-2:
   the key names/casing are genuinely per-transport (Kafka `OriginalType` vs Redis/SQS
   `originalMessageType` is a name difference, not just casing). A shared core type would falsely
   imply a single canonical key set and would not match what each gateway actually stamps. Only
   the *semantic set* is universal; the provider owns the names.

## References

- Requirements: [specs/0036-universal-transport-conformance-tests/requirements.md](../../specs/0036-universal-transport-conformance-tests/requirements.md)
- Related ADRs (this ADR builds on all of these; it supersedes none):
  - [`0067-conformance-rollout-and-deferral-governance`](0067-conformance-rollout-and-deferral-governance.md) [Proposed] — **sibling.** Sequences the per-transport rollout around this ADR's gate-retirement flip (fix-then-flip) and governs auditable deferrals via the conformance ledger; a provider that cannot yet supply a member added here (e.g. the in-memory scheduler arm) is tracked as a `Deferred` row there.
  - `0037-add-messaging-gateway-generated-test` [Accepted] — created this generator and
    `MessagingGatewayConfiguration`; this ADR directly extends its provider interfaces and
    retires three of its capability flags (the most important relation).
  - `0047-message-rejection-routing-strategy` — defines the `Reject()` fallback ladder
    (DeliveryError→DLQ; Unacceptable→invalid else DLQ; None→DLQ) and origin-metadata enrichment;
    the contract the generated tests now assert universally (not redefined here).
  - `0037-universal-scheduler-delay` — routes delayed requeue through `IAmAMessageScheduler`
    with `InMemoryScheduler` default; the mechanism FR-3's scheduler-fallback test exercises via
    the producer's `Scheduler`.
  - `0045-provide-dlq-where-missing` — Brighter-managed dead-letter and invalid-message
    channels; the provisioning the new DLQ/invalid provider members rely on.
  - `0039-transport-scheduler-wiring` — threads `IAmAMessageScheduler` through the channel factory
    into the consumer; this is the seam the `CreateChannelWith*Scheduler` members wire, and the
    reason FR-3 is observable at `channel.Requeue` rather than via a standalone producer.
  - Per-transport DLQ ADRs (`0038-aws-sqs-dlq-direct-send`, `0039-redis-dlq-brighter-managed`,
    `0040-mssql-dlq-brighter-managed`, `0041-postgres-dlq-brighter-managed`,
    `0042-rocketmq-dlq-brighter-managed`, `0043-mqtt-dlq-brighter-managed`,
    `0046-kafka-dlq-producer-for-requeue`) — collectively, the transport DLQ behaviours now
    brought under universal conformance test.
- External references: none.
