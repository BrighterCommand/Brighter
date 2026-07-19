---
id: 0066-conformance-test-provider-and-ungating
title: "Conformance-Test Provider Interface Extension and Capability-Gate Retirement"
status: Proposed
author:
  - "Ian Cooper"
created: 2026-07-18
summary: "Extends the generated messaging-gateway provider interfaces (IAmAMessageGatewayReactorProvider / IAmAMessageGatewayProactorProvider) with explicit DLQ + invalid-message routing keys, an invalid-channel read, and a strongly-typed RejectionMetadataKeys accessor; makes canonical conformance templates ungated by construction by narrowing the HasSupportToDelayedMessages / HasSupportToDeadLetterQueue / HasSupportToRequeue gates to a closed list of four legacy templates, which are never ungated (they keep their current gating and reach no new configuration) until deleted along with their eighty generated copies, after which the gates and config keys retire as a terminal cleanup; and withdraws FR-3's scheduler-delegation test as a mechanism assertion (folded into a mechanism-agnostic FR-2), so no scheduler-carrying provider member is required."
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
AWS **and AWS.V4** each declare `HasSupportToDelayedMessages: false` in three of their four gateway
configurations (both `SqsStandard` declare `true`) despite native `DelaySeconds` — six of the eight
AWS-family configurations; and Kafka — the transport whose
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
neither; cannot read from the invalid channel; and cannot surface the transport's
rejection-metadata key names.
Those key names genuinely diverge per transport — Kafka stamps PascalCase
(`src/Paramore.Brighter.MessagingGateway.Kafka/HeaderNames.cs`: `OriginalTopic`, `OriginalType`,
`RejectionReason`, `RejectionMessage`, `RejectionTimestamp`) while Redis
(`RedisMessageConsumer.RefreshMetadata`) and SQS (`SqsMessageConsumer.RefreshMetadata`) stamp
camelCase (`originalTopic`, `originalMessageType`, `rejectionReason`, `rejectionMessage`,
`rejectionTimestamp`) — and the divergence is *more than casing*: the "original message type"
field is `OriginalType` on Kafka but `originalMessageType` on Redis/SQS. Only the *semantic set*
is universal, which is exactly why the provider must own the key names (per constraint C-2:
these are **not** a shared core type).

**Why now:** making the canonical behaviours universal and ungated (the parent requirement) is only
possible once the provider interface can drive them. Extending that interface and narrowing the gates
are the same change — both are what the first canonical template needs in order to generate. Retiring
the gates outright is *not* part of that change; it waits until the legacy templates they suppress
have been deleted.

**Parent Requirement**: [specs/0036-universal-transport-conformance-tests/requirements.md](../../specs/0036-universal-transport-conformance-tests/requirements.md)

**Scope**: This ADR decides (1) the provider-interface extension (FR-1), (2) the **gating lifecycle**
— scoping the three capability gates to the legacy templates they suppress, and retiring gates and
config keys only once those templates are deleted (FR-10 / FR-11), and (3) deleting the broken
`with_delay` template (FR-12) and the requeue-count-exhaustion template (FR-19), both of which are
legacy gated templates. The per-transport conformance-fix *sequencing* (FR-13), the FR-20
onboardings, and the detailed *content* of each individual canonical template (FR-2, FR-4 … FR-9,
FR-15, FR-16, FR-17, FR-22) beyond what the provider must expose to make them writable are
**addressed separately** — FR-13 and FR-20 in the sibling ADR
[0067](0067-conformance-rollout-and-deferral-governance.md), the template content as generator work
under this spec's tasks. Any reintroduction of a `HasNative*` flag is explicitly rejected (OOS-1).

**The target set this ADR's interface must serve** is defined by requirements.md FR-13: every
transport with a messaging gateway — all twelve `src/Paramore.Brighter.MessagingGateway.*` projects —
counted per *gateway configuration*, not per project. Nine of the twelve are wired today and declare
**twenty** configurations between them; the interface changes below land against those twenty now.
The other three — AzureServiceBus, MQTT and RMQ.Sync — are wired by FR-20 and implement the
post-FR-1 signature directly, never the `bool setupDeadLetterQueue` this ADR removes. Where the text
below cites "the twenty configurations wired today", it is describing today's implementation
surface, not the boundary of the target set.

## Decision

We extend both generated provider interfaces to expose the full surface the canonical conformance
tests drive; make the canonical templates **ungated by construction** rather than by removing the
gates; keep the three gates in force — narrowed to the four legacy templates they suppress — until
those templates are deleted; and then retire the gates and their config keys as a terminal cleanup.

**The canonical templates are never gated, and the legacy templates are never ungated.** This is the
ordering decision, and it is the opposite of "retire the gates first, then fix the fallout". The old
tests are not wanted at any point: not before the canonical set exists, and not after. A gate that
suppresses a legacy template is doing useful work right up until that template is deleted, so
removing the gates first would generate precisely the tests this spec exists to replace — against
transports that have not been fixed yet. Sequencing the gate removal last means the generated suite
only ever grows the tests we want.

Concretely, `SkipTest` consults the three gates **only** for an explicit, closed list of the four
legacy template filenames. Anything not on that list generates regardless of any flag value, so a
canonical template cannot be suppressed however it is named. This deliberately does not rely on
naming: `SkipTest` matches filename *substrings*, and NFR-1's convention means a canonical
delayed-requeue template naturally contains both `requeuing` and `with_delay` and would otherwise be
silently suppressed — the same defect that left the exhaustion template doubly gated.

⚠️ **"Never ungated" is not "never generated", and the difference has a compile-time consequence.** A
gate suppresses a template only where that gate is declared `false`; most configurations declare
these gates `true`. **All four** legacy templates therefore generate today and keep generating until
deletion — the exhaustion template for 16 configurations (32 copies), plain requeue for 18 (36
copies), `with_delay` and delayed-message for 3 each (6 copies each). What the narrowing guarantees
is that they gain no **new** generation site.

This bites on FR-1(6). Removing `bool setupDeadLetterQueue` from `CreateSubscription` breaks the
exhaustion template's call sites while that template is still live for sixteen configurations, and it
passes the flag **positionally** — a bare `true` fourth argument — so none of its 32 generated copies
contains the string `setupDeadLetterQueue`. Migrating "every generated caller" by searching for the
parameter name finds the 40 interface copies and 20 provider implementations and misses all 32. The
template's `.liquid` source MUST therefore be edited in the FR-1 change, even though the template is
scheduled for deletion later.

**The provider exposes no scheduler-carrying member.** A generated test never obtains a producer
whose `Scheduler` property is set, and the suite asserts nothing about the delay *mechanism*:
such an assertion violates NFR-3/OOS-1, and 14 of the twenty configurations wired today have no
scheduler seam and cannot acquire one within C-1 (see "Why there is no scheduler member"). The
delayed-requeue behaviour is covered mechanism-agnostically by requirements.md FR-2; FR-1(4), FR-3,
NFR-4 and AC-3 are retired identifiers there; supplementary scheduler-delegation testing is OOS-2.
The deliberation behind that withdrawal is recorded in the spec's
[decision-log.md](../../specs/0036-universal-transport-conformance-tests/decision-log.md).

### Architecture Overview

The provider is a **service-provider / interfacer** role (per Responsibility-Driven Design): it
is the single seam between a transport-agnostic generated test and a transport-specific gateway.

- What the provider **knows**: this transport's actual `Header.Bag` rejection-metadata key
  strings (Kafka PascalCase vs Redis/SQS camelCase), and how to build that transport's
  subscriptions, channels, and producers.
- What the provider **does**: create publications/subscriptions/channels/producers; and read a
  message back from the DLQ and from the invalid channel.

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

  // NOTE: no scheduler-carrying member. FR-2's delayed-requeue test is mechanism-agnostic
  // (it asserts redelivery after the delay, not which mechanism delivered it), so the suite
  // never inspects a scheduler. See "Why there is no scheduler member" below.

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
`GetMessageFromDeadLetterQueueAsync`, `GetMessageFromInvalidChannelAsync` are async;
`RejectionMetadataKeys` is a plain property shared by both interfaces).

**Read-member contract (needed for the negative assertions in AC-5 and AC-18).** Both
`GetMessageFromDeadLetterQueue` and `GetMessageFromInvalidChannel` — and their async siblings —
MUST poll with a bounded retry (NFR-2) and, when nothing arrives within the bound, return a message
whose `Header.MessageType` is `MessageType.MT_NONE`. They MUST NOT throw and MUST NOT block
indefinitely on an empty queue. Reading a DLQ or invalid channel that the subscription does **not**
configure likewise returns `MT_NONE` rather than throwing. This makes "the message did **not**
appear on the DLQ / invalid channel" a uniform, transport-agnostic assertion; without it, AC-5
("does not appear on any DLQ") and AC-18 ("does not appear on the invalid channel") are unwritable
as a single template. The FR-5 and FR-17 templates configure **both** routing keys and assert the
message landed on one channel and not the other.

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

**Where the record lives.** The two provider interfaces are emitted into *different* namespaces and
*different* directories — `{{ Namespace }}.MessagingGateway{{ Prefix }}.Reactor` under
`Generated/Reactor/`, and `….Proactor` under `Generated/Proactor/` — so a type "shared by both"
cannot live in either. `RejectionMetadataKeys` is therefore emitted **once per gateway configuration**
by a new template, `Templates/MessagingGateway/Shared/RejectionMetadataKeys.cs.liquid`, into the
parent namespace `{{ Namespace }}.MessagingGateway{{ Prefix }}` at `Generated/RejectionMetadataKeys.cs`
— a sibling of the `Reactor/` and `Proactor/` directories, which both already have the parent
namespace in scope. This requires a third template directory (`Shared/`) alongside `Reactor/` and
`Proactor/`, and generator wiring to emit its contents once per configuration rather than once per
variant; both are new and are listed in Key Components below.

The rejected alternatives are worth naming, because each is a plausible misreading: emitting the
record into *both* variant namespaces produces two distinct types with the same name, so nothing is
shared and any common helper breaks; emitting it into `src/Paramore.Brighter` is forbidden by C-2;
hand-writing it per transport contradicts "generated" and re-admits the drift the record exists to
remove.

**What a provider returns for a field its gateway does not stamp.** The member returns
`string.Empty` — never `null`, never a plausible-looking guess at a key name the gateway does not
actually write. An empty key cannot match a header, so the FR-8 assertion for that semantic field
fails, and it fails as a **genuine non-conformance** rather than as a test defect: FR-8 says a
transport that does not stamp the universal semantic set does not conform. That failure is then
recorded in the ledger like any other — `Fixed` if the gateway is taught to stamp the field, or
`Deferred -> #NNNN` with sign-off. Returning `null` would instead surface as a `NullReferenceException`
inside the test body, which reads as a broken test rather than a non-conforming transport.

#### Why there is no scheduler member

A scheduler-carrying provider member would let a generated test prove a delayed requeue was
*delegated to the scheduler* rather than served by native delay. The provider exposes no such
member, and requirements.md FR-2 covers the observable behaviour mechanism-agnostically instead, for
two reasons:

1. **It is a mechanism assertion, which NFR-3 and OOS-1 forbid.** "Did the requeue go via the
   scheduler rather than native delay?" is a question about *how* the transport achieves the
   behaviour. The generic suite exists to prove *that* delayed requeue works. Asserting the
   mechanism reintroduces the native/non-native distinction this spec set out to remove — just
   relocated from a config flag into a test assertion.
2. **The scheduler seam does not exist for most of the target set.**
   `IAmAChannelFactoryWithScheduler` is implemented by six gateways only — Kafka, MQTT, MsSql,
   Redis, RMQ.Async, RMQ.Sync — and only those consumers accept an `IAmAMessageScheduler`. Of the
   **twenty configurations wired today**, **6** can carry a scheduler (Kafka ×2, MSSQL, Redis,
   RMQ.Async ×2); the other **14** (AWS ×4, AWS.V4 ×4, GCP ×4, PostgreSQL, RocketMQ) take no
   scheduler at all. (MQTT and RMQ.Sync hold the seam but generate nothing until FR-20 wires them,
   at which point they add seam-capable configurations to the target set — which does not change the
   conclusion, since the fourteen without the seam remain.) Nine of those fourteen honour the delay **natively** —
   `SqsMessageConsumer.RequeueAsync` issues a `ChangeMessageVisibilityAsync` carrying the delay
   (line 402), `PostgresMessageConsumer.Requeue` binds it as a query parameter (line 459). The
   remaining five honour it **by neither route**: all four GCP configurations ignore the delay
   outright (`GcpPullMessageConsumer.Requeue` calls `ModifyAckDeadline(..., 0)`; its own XML doc
   reads *"An optional delay (not used by Pub/Sub)"* — redelivery timing is governed by the
   subscription's RetryPolicy), and `RocketMessageConsumer.Requeue` is a **no-op that returns
   `true`**, its `ChangeInvisibleDuration` call commented out pending an upstream RocketMQ C#
   client fix. A scheduler-delegation assertion would fail **by design** on all fourteen, and
   giving them the seam would mean adding a constructor parameter to `SqsMessageConsumer`,
   `PostgresMessageConsumer`, the GCP consumers and RocketMQ's — a public runtime API change that
   **C-1 forbids this spec**.

   Those last five matter for the rollout, not for this decision: the mechanism-agnostic FR-2 will
   **fail on GCP ×4 and RocketMQ** as soon as the canonical templates generate, because neither
   actually delays a requeue. That is
   a genuine conformance gap — exactly what an ungated suite is meant to expose — and ADR 0067
   sequences and governs it (RocketMQ's is blocked on an upstream dependency, so it is a likely
   signed-off `Deferred` row rather than an in-spec `Fixed`).

So FR-2's generated test asserts the observable outcome only: *requeue with delay D, and the message
is redelivered after D*. That is uniform across every targeted configuration regardless of mechanism, and
it is exactly what the Objective and Test Boundary asks for ("prove the observable outcome … not the
internal mechanism"). The consequence is that FR-2 and the former FR-3 collapse into one canonical
behaviour — which is correct, because once the mechanism is not asserted, they *were* the same test.

A scheduler-delegation test remains genuinely useful for the six gateways that have the seam — the
existing hand-written
`tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Reactor/When_kafka_consumer_requeues_with_delay_should_use_scheduler.cs`
(wiring `new KafkaMessageConsumer(..., scheduler: _scheduler)` and asserting `ScheduleCalled` /
`ScheduledDelay`) is the model. It belongs with the **supplementary per-transport native/mechanism
tests under OOS-2**, not in the universal suite.

### Key Components

- The two provider interface templates:
  `.../Templates/MessagingGateway/{Reactor,Proactor}/IAmAMessageGateway*Provider.cs.liquid`.
- `MessagingGatewayGenerator.SkipTest`
  (`.../Generators/MessagingGatewayGenerator.cs`) — loses **four** branches (keyed on three gates:
  `HasSupportToDelayedMessages` is tested twice, for `delayed_message` and for `with_delay`).
- `MessagingGatewayConfiguration`
  (`.../Configuration/MessagingGatewayConfiguration.cs`) — loses three properties.
- A new `RejectionMetadataKeys` record (in the generated test-support namespace, per gateway
  configuration, **not** in `src/Paramore.Brighter` — C-2), emitted into the parent namespace
  `{{ Namespace }}.MessagingGateway{{ Prefix }}` so both variant namespaces can see it.
- A new template `Templates/MessagingGateway/Shared/RejectionMetadataKeys.cs.liquid`, and with it a
  third template directory `Shared/` alongside the existing `Reactor/` and `Proactor/`. Its contents
  are emitted **once per gateway configuration** to `Generated/RejectionMetadataKeys.cs`, not once
  per variant — a generation mode the generator does not have today, so
  `MessagingGatewayGenerator` gains it.
- *(No scheduler spy or scheduler-carrying member — withdrawn with FR-3; see "Why there is no
  scheduler member".)*
- Every hand-written per-transport provider implementation that satisfies these interfaces — the
  twenty `*MessageGatewayProvider.cs` files wired today, e.g.
  `tests/Paramore.Brighter.PostgresSQL.Tests/MessagingGateway/PostgresMessageGatewayProvider.cs`,
  and the Kafka/Redis/MSSQL/RMQ.Async/AWS/AWS.V4/GCP/RocketMQ providers — must be extended to
  implement the new members (see Consequences).
- The **provider implementations FR-20 adds** for AzureServiceBus, MQTT and RMQ.Sync. These are
  written fresh against the post-FR-1 surface — routing-key parameters, `GetMessageFromInvalidChannel`,
  `RejectionMetadataKeys` — and never carry `bool setupDeadLetterQueue`, so they need no migration.
  Each transport supplies its own `RejectionMetadataKeys` values from the key strings its gateway
  actually stamps; ASB, MQTT and RMQ.Sync are not assumed to share Kafka's or Redis/SQS's casing.
- Every `tests/Paramore.Brighter.*.Tests/test-configuration.json` — the three keys removed; plus the
  three new configuration files FR-20 creates, which never declare them.

### Technology Choices

- **Strongly-typed `RejectionMetadataKeys` (record) over a `string`-keyed dictionary.** The
  semantic set is fixed and known at author time; named members give refactor-safe, discoverable
  call sites (`provider.RejectionMetadataKeys.OriginalTopic`) and make an omitted field a
  compile error rather than a silent `null` lookup. This is exactly the FR-1(5) example. A
  dictionary would reintroduce stringly-typed access — the primitive-obsession the design
  principles reject.
- **No scheduler member; FR-2's delayed-requeue test is mechanism-agnostic.** The suite asserts
  that a delayed requeue redelivers after the delay, not which mechanism delivered it (NFR-3,
  OOS-1). This keeps the test uniform across every targeted gateway configuration — including the
  14 wired today whose consumers cannot carry a scheduler — and avoids obliging every provider to stand up a
  `CommandProcessor`, a `FireSchedulerMessage` handler pipeline and an external bus purely to
  satisfy an assertion the suite no longer makes. See "Why there is no scheduler member".
- **Delete (not fix) the `with_delay` template.** The amended FR-2 (delayed requeue redelivers after
  the delay) supersedes it and is stronger — it passes a non-null delay and uses a bounded retry
  loop; keeping a fixed-in-place duplicate would duplicate knowledge.

#### Recorded for the OOS-2 follow-up: what an in-memory-scheduler harness would cost

The alternative to a recording spy is a provider member handing back a channel backed by an
`InMemoryScheduler`, giving a black-box "the scheduler really re-published it" assertion. It forms no
part of this design, but its cost is recorded here because whoever builds the OOS-2 supplementary
scheduler tests faces the same choice.

`InMemoryScheduler` is not a self-contained timer — it is a *dispatcher into a command processor*
(`src/Paramore.Brighter/InMemoryScheduler.cs`):

```
scheduler.Schedule(message, delay)
  -> timeProvider.CreateTimer(Execute, (processor, FireSchedulerMessage{Message, Async=false}), delay)
  -> Execute: BrighterAsyncContext.Run(() => processor.SendAsync(fireSchedulerMessage))   // line 285
  -> FireSchedulerMessageHandler.HandleAsync -> processor.Post(command)                   // Scheduler/Handlers
  -> OutboxProducerMediator unwraps FireSchedulerMessage and produces the inner Message    // lines 458, 483
  -> message reappears on the transport topic
```

So an in-memory-scheduler harness needs, per transport: a real `IAmACommandProcessor` (the timer
callback calls `SendAsync` on it — a stub will not do); a handler pipeline resolving
`FireSchedulerMessage` to `FireSchedulerMessageHandler`; an external bus / `OutboxProducerMediator`
with a producer registry bound to that transport's topic; plus a `TimeProvider`, the two
scheduler-id factory funcs and an `OnSchedulerConflict` policy. No existing test constructs
`InMemoryScheduler` directly — every current usage goes through `InMemorySchedulerFactory` inside a
full dispatcher setup.

By contrast a **recording spy** (`SpySchedulerSync : IAmAMessageSchedulerSync`, ~25 lines, as in the
existing Kafka scheduler test) has none of these prerequisites — it is assigned straight onto a
scheduler-capable consumer. Any OOS-2 scheduler work should prefer the spy, and applies only to the
six gateways that expose the seam.

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
`GetMessageFromInvalidChannel` and the `RejectionMetadataKeys` property, and specify the
`MT_NONE`-on-empty contract for both read members. The `bool` overload
is the one thing removed rather than added; a transport that previously derived its DLQ routing
key internally (as PostgreSQL does when `setupDeadLetterQueue` is true) now receives the routing
key explicitly from the test, which also removes hidden per-transport DLQ-naming knowledge from
the provider.

**`SkipTest`'s four gate branches are first narrowed, then deleted.**

*Step A — narrow (lands first, with the first canonical template).* The gate branches at lines
122–125 (`HasSupportToDelayedMessages`/`delayed_message`), 127–130
(`HasSupportToDelayedMessages`/`with_delay`), 132–135
(`HasSupportToDeadLetterQueue`/`dead_letter_queue`) and 145–148
(`HasSupportToRequeue`/`requeuing`) become reachable only for an explicit legacy list:

```csharp
private static readonly string[] LegacyGatedTemplates =
[
    "When_reading_a_delayed_message_via_the_messaging_gateway_should_delay_delivery",
    "When_requeuing_a_failed_message_should_receive_message_again",
    "When_requeuing_a_failed_message_with_delay_should_receive_message_again",
    "When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue",
];
// the four gate branches below are consulted only when fileName is on this list;
// every other template — canonical or otherwise — generates regardless of flag value
```

The list is exhaustive and closed: it enumerates exactly the four templates gated today (verified by
substring match against the template directory), and nothing is ever added to it. It is also the
work list for step C — the set to delete — so the two cannot drift apart.

*Step B — the canonical set is built and the transports are fixed.* Throughout, canonical templates
generate for every configuration while the four legacy templates keep exactly the gating they have
today — never ungated, never reaching a new configuration. This is what makes
the reference transport workable: Kafka declares all three gates `false`, yet its canonical FR-2 and
FR-9 tests generate and run, because the gates no longer reach them.

*Step C — delete (lands last).* Delete the four legacy templates in both variants, **and their
eighty checked-in generated copies** (6 + 36 + 6 + 32 — see "The generated tree" below). Then delete
the four now-unreachable gate branches, remove `HasSupportToDelayedMessages`,
`HasSupportToDeadLetterQueue` and `HasSupportToRequeue` from `MessagingGatewayConfiguration` (lines
91, 96, 106), and remove the keys from every `test-configuration.json` (FR-11) — including the
mis-declared PostgreSQL (`false`/`false`), AWS and AWS.V4 (`HasSupportToDelayedMessages: false` in
three of their four gateway configurations each; both `SqsStandard` declare `true` — six of the eight
AWS-family configurations mis-declare), and Kafka (all three gates `false`, in both Standard and
PartitionKey) values. Removing the keys rather than correcting them to `true` is the AC-11 outcome:
the flags no longer exist, so a stale value cannot mislead.

**The key removal must not precede the template deletion.** Removing a key makes the flag default,
which ungates the legacy template it was suppressing — generating exactly the old test the sequence
exists to avoid. AC-11 is therefore stated as an *ordered* criterion.

The retained gates (`HasSupportToPublishConfirmation`/`confirming_posting`,
`HasSupportToValidateBrokerExistence`/`no_broker_created`,
`HasSupportToValidateInfrastructure`/`assume_channel`/`validate_channel`) are untouched throughout —
this ADR narrows and then retires only the three named gates.

**The generated tree.** Generated output is committed to this repository, and deleting a `.liquid`
template does not delete what it previously produced. Two sweeps are therefore part of this change,
not follow-up hygiene:

- **Legacy orphans (step C)** — eighty `.cs` copies of the four legacy templates under
  `tests/Paramore.Brighter.*.Tests/**/Generated/`: 6 of the delayed-message template, 36 of
  plain-requeue, 6 of `with_delay`, 32 of the exhaustion test. AC-10(b), AC-12 and AC-22 assert their
  absence.
- **Provider-interface copies (with FR-1(6))** — **forty** checked-in
  `Generated/{Reactor,Proactor}/IAmAMessageGateway*Provider.cs` files declare
  `CreateSubscription(..., bool setupDeadLetterQueue = false)`. Removing that parameter makes every
  one of them stale, so all twenty configurations are regenerated and re-committed in the same change
  that lands FR-1(6).

**Delete the broken template (in step C).** Remove
`.../Templates/MessagingGateway/{Reactor,Proactor}/When_requeuing_a_failed_message_with_delay_should_receive_message_again.cs.liquid`
(the Reactor variant calls `_channel.Requeue(received);` with no delay after a `SendWithDelay` +
`Thread.Sleep(6s)` — line 62), together with its six generated copies. It is one of the four legacy
gated templates, so it is never ungated and is deleted at step C rather than early. It is not inert
in the meantime: it generates for the three configurations declaring `HasSupportToDelayedMessages:
true` (`AWS/SqsStandard`, `AWS.V4/SqsStandard`, `RocketMQ`), six copies in all, and continues to do
so until step C deletes it. After deletion, a template-source
inspection MUST confirm that every template *purporting to exercise delayed requeue* passes a
non-null `TimeSpan` to `Requeue` / `RequeueAsync` (AC-12).

The prohibition is scoped to delayed-requeue templates, not to every call site, because two
**canonical** templates legitimately requeue with no delay: the canonical plain-requeue template
(FR-22, which may be migrated from the legacy `When_requeuing_a_failed_message_should_receive_message_again`)
and the zero/null-boundary template required by FR-15. Between them they cover the no-delay path;
FR-2 covers the delayed path.

**Reactor/Proactor parity (FR-14).** Every change above lands in *both* template trees and both
provider interfaces — the async members return `Task`/`Task<...>` and take a
`CancellationToken`, matching the existing dual layout. Parity here exercises the distinct sync
vs async gateway code paths; it is not pump re-testing.

## Consequences

### Positive

- The canonical reject/DLQ/invalid-channel/requeue-with-delay/delayed-send/Nack behaviours
  become universal and ungated — every transport, both variants — so adding or changing a
  transport proves conformance instead of relying on hand-written duplicates.
- The provider interface finally expresses the full test surface; canonical templates become
  writable without hard-coding any transport's key strings or DLQ naming.
- Deleting the mis-declared gates removes a class of "green because the flag says so" false
  confidence; the configs no longer carry misleading values.
- Strongly-typed `RejectionMetadataKeys` gives refactor-safe metadata assertions across the
  genuinely divergent Kafka/Redis/SQS key names.
- One canonical delayed-requeue behaviour (the amended FR-2) instead of two overlapping templates.

### Negative

- **Every per-transport provider implementation must be extended** to satisfy the wider
  interface — the twenty wired today (Kafka, Redis, MSSQL, PostgreSQL, RMQ.Async, AWS SNS/SQS ×2
  versions, GCP, RocketMQ). A transport that cannot yet supply an invalid channel or a metadata-key
  set surfaces as a compile or implementation gap. This is *intended* (it is how non-conformance
  becomes visible per FR-13), but it is real work and is the bulk of the follow-on effort.
- **Three further provider implementations must be written from scratch** under FR-20, for
  AzureServiceBus, MQTT and RMQ.Sync. They are not a migration cost — they implement the post-FR-1
  signature directly — but they are new work this ADR's interface defines the shape of, and they
  land after the twenty existing providers have proven that shape. ADR 0067 sequences them last.
- **We lose the ability to prove, in the generic suite, that a transport without native delay really
  falls back to Brighter's scheduler.** That was FR-3's purpose, and dropping it means a gateway
  could regress from scheduler-fallback to silently-no-delay and the universal suite would still be
  green *provided the message is redelivered after the delay by some means*. This is an accepted
  trade: the mechanism assertion is unavailable on 14 of the 20 configurations wired today anyway, and NFR-3
  forbids it. The mitigation is the OOS-2 supplementary per-transport scheduler tests for the six
  gateways that expose the seam — which should be raised as a follow-up issue rather than left
  implicit.
- Removing the three properties is a **breaking change** to `MessagingGatewayConfiguration` and
  to the `test-configuration.json` schema; any external tooling that reads those keys breaks.
- Replacing `bool setupDeadLetterQueue` with explicit routing-key parameters is a breaking
  change to `CreateSubscription`; every provider and every existing generated caller must move to
  the new signature in the same change (requirements.md FR-1(6), verified by AC-1). The only
  existing template that passes the bool is
  `When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue.cs.liquid`, which is
  **edited in this same change** to stop passing the flag — and deleted outright later, under FR-19.
  It is the edit, not the deletion, that removes the last caller: the template keeps generating for
  sixteen configurations until step C, so waiting for FR-19 would leave 32 generated call sites
  passing an argument the signature no longer has. After this change the parameter has no callers at
  all.
- **The substring-matching hazard is now prevented by construction rather than argued about.**
  Because `SkipTest` matches on filename substrings,
  `When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue` matches *both*
  `dead_letter_queue` and `requeuing` and is doubly gated today; it currently generates for 16 of the
  20 configurations (all but Kafka ×2, MSSQL and PostgreSQL). Under the superseded "remove the gates
  first" sequencing it would have generated for all 20.

  **It must not, and now cannot.** Requeue-count exhaustion is enforced by the message pump —
  `Reactor`/`Proactor.RequeueMessage` call `Header.UpdateHandledCount()` then
  `Message.HandledCountReached(RequeueCount)`, and that method has no other caller in the codebase.
  `Channel.Requeue` forwards straight to `_messageConsumer.Requeue` and counts nothing. Where the
  template passes today it is proving the *transport's* native redrive: the AWS provider pairs
  `requeueCount: 3` with `redrivePolicy: new RedrivePolicy(dlqName, 3)`, so SQS does the counting.
  That is a pump test (OOS-5) or a native-mechanism test (NFR-3, OOS-1) — the same defect that got
  FR-3 withdrawn. The template is on the closed legacy list, so it is never ungated — it keeps
  generating for its sixteen configurations until it is deleted with its 32 generated copies (FR-19),
  exactly as the broken `with_delay` template is (FR-12).

  The residual risk inverts: the legacy list is now load-bearing, and a canonical template is only
  ungated because it is *absent* from that list. A future edit that adds a name to the list, or a
  legacy template renamed without updating it, silently changes what generates. The mitigations are
  that the list is declared closed, and that it is the same list step C deletes — so a stale entry
  surfaces as a deletion that finds no file.

### Risks and Mitigations

- *Risk*: newly-ungated tests fail for some transports (e.g. GCP has no coverage today). *This is
  the point*, not a regression — handled under FR-13 (fix-to-conform, or a named, linked,
  signed-off deferral; never a silent `[Skip]`). Sequencing is a separate ADR.
- *Risk*: timing flakiness in delayed/redelivery assertions. *Mitigation*: NFR-2 bounded
  receive-retry loops (as in the plain-requeue template), which is now the only defence since the
  suite makes no timing-free mechanism assertion.
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
3. **Fix the `with_delay` template in place.** Rejected: redundant with and weaker than the
   mechanism-agnostic FR-2; keeping it duplicates knowledge and leaves a third delayed-requeue
   template to maintain. It is also an *old* test, and the spec's position is that the old tests are
   replaced rather than repaired.
4. **Keep a scheduler-delegation test in the generic suite (spy, in-memory, or both).** Rejected on
   two independent grounds. *Principle*: it asserts the delay **mechanism**, which NFR-3 and OOS-1
   forbid — "we don't want to test how, we want to test supported". *Practice*:
   `IAmAChannelFactoryWithScheduler` is implemented by six gateways (Kafka, MQTT, MsSql, Redis,
   RMQ.Async, RMQ.Sync) — all six targeted transports under FR-13, though MQTT and RMQ.Sync generate
   nothing until FR-20 wires them. Of the twenty configurations wired today, the seam covers **6**
   (Kafka ×2, MSSQL, Redis, RMQ.Async ×2); the other 14 take no scheduler at all, so the assertion
   fails by design on them, including on conformant transports, and giving them the seam is a public
   runtime API change C-1 forbids. Retained as OOS-2 supplementary work for the six gateways that can
   support it.
5. **Retire the three gates up front, then chase the fallout.** Rejected — this was the earlier
   sequencing, and it does not survive contact with the fix phase. Removing the gates first
   immediately generates the four legacy templates for every configuration: the exhaustion template
   onto Kafka ×2, MSSQL and PostgreSQL (none of which counts deliveries), and the broken `with_delay`
   template onto every configuration declaring a delay gate `false`. Those are precisely the tests
   this spec deletes, and they would land red on transports not yet fixed. It also creates a circular
   dependency with ADR 0067's cleanup gate: the ledger cannot record a `Pass` for FR-2 or FR-9 on the
   reference transport, because Kafka declares all three gates `false` and so would not generate
   those canonical tests until the very change the ledger is meant to authorise. Narrowing the gates
   to a closed legacy list dissolves both problems at once.
6. **A shared core `RejectionMetadataKeys` type in `src/Paramore.Brighter`.** Rejected per C-2:
   the key names/casing are genuinely per-transport (Kafka `OriginalType` vs Redis/SQS
   `originalMessageType` is a name difference, not just casing). A shared core type would falsely
   imply a single canonical key set and would not match what each gateway actually stamps. Only
   the *semantic set* is universal; the provider owns the names.

## References

- Requirements: [specs/0036-universal-transport-conformance-tests/requirements.md](../../specs/0036-universal-transport-conformance-tests/requirements.md)
- Related ADRs (this ADR builds on all of these; it supersedes none):
  - [`0067-conformance-rollout-and-deferral-governance`](0067-conformance-rollout-and-deferral-governance.md) [Proposed] — **sibling.** Sequences the per-transport rollout around this ADR's gating lifecycle (generate-then-fix-then-clean-up) and governs auditable deferrals via the conformance ledger; a provider that cannot yet supply a member added here (e.g. the invalid-channel read, or the rejection-metadata key set) is tracked as a `Deferred` row there.
  - `0037-add-messaging-gateway-generated-test` [Accepted] — created this generator and
    `MessagingGatewayConfiguration`; this ADR directly extends its provider interfaces and
    retires three of its capability flags (the most important relation).
  - `0047-message-rejection-routing-strategy` — defines the `Reject()` fallback ladder
    (DeliveryError→DLQ; Unacceptable→invalid else DLQ; None→DLQ) and origin-metadata enrichment;
    the contract the generated tests now assert universally (not redefined here).
  - `0037-universal-scheduler-delay` — routes delayed requeue through `IAmAMessageScheduler`
    with `InMemoryScheduler` default; this was the mechanism the withdrawn FR-3 would have
    asserted, and remains the mechanism any OOS-2 supplementary scheduler test would exercise.
  - `0045-provide-dlq-where-missing` — Brighter-managed dead-letter and invalid-message
    channels; the provisioning the new DLQ/invalid provider members rely on.
  - `0039-transport-scheduler-wiring` — threads `IAmAMessageScheduler` through the channel factory
    into the consumer. Relevant here as the reason FR-3 was withdrawn rather than redesigned: that
    seam is opt-in (`IAmAChannelFactoryWithScheduler`) and reaches only 6 of the twenty gateway
    configurations wired today, so a scheduler assertion cannot be universal. It remains the seam any OOS-2
    supplementary scheduler test would use.
  - Per-transport DLQ ADRs (`0038-aws-sqs-dlq-direct-send`, `0039-redis-dlq-brighter-managed`,
    `0040-mssql-dlq-brighter-managed`, `0041-postgres-dlq-brighter-managed`,
    `0042-rocketmq-dlq-brighter-managed`, `0043-mqtt-dlq-brighter-managed`,
    `0046-kafka-dlq-producer-for-requeue`) — collectively, the transport DLQ behaviours now
    brought under universal conformance test.
- External references: none.
