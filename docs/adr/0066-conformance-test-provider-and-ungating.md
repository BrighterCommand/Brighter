---
id: 0066-conformance-test-provider-and-ungating
title: "Conformance-Test Provider Interface Extension and Capability-Gate Retirement"
status: Proposed
author:
  - "Ian Cooper"
created: 2026-07-18
summary: "Extends the generated messaging-gateway provider interfaces (IAmAMessageGatewayReactorProvider / IAmAMessageGatewayProactorProvider) with explicit DLQ + invalid-message routing keys, an invalid-channel read, and a strongly-typed RejectionMetadataKeys accessor; retires the HasSupportToDelayedMessages / HasSupportToDeadLetterQueue / HasSupportToRequeue opt-in gates; deletes the broken with_delay requeue template; and withdraws FR-3's scheduler-delegation test as a mechanism assertion (folded into a mechanism-agnostic FR-2), so no scheduler-carrying provider member is required."
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

**Why now:** making the canonical behaviours universal and ungated (the parent
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
(superseded by the amended FR-2).

**One deliberate departure from the original requirement wording.** FR-1(4) and AC-1 asked for a
member returning "a producer whose `Scheduler` property is set", to support FR-3's
scheduler-delegation test. We provide **no scheduler-carrying member at all**, and FR-3 is withdrawn
as a distinct canonical behaviour — folded into a mechanism-agnostic FR-2 — because asserting the
delay *mechanism* violates NFR-3/OOS-1 and because 14 of the ~20 target configurations have no
scheduler seam and cannot acquire one within C-1 (see "Why there is no scheduler member").

Requirements.md has since been rewritten to state obligations without carrying design rationale.
FR-1(4), FR-3, NFR-4 and AC-3 are **retired identifiers** there — left as permanent gaps, never
reused — and AC-1 no longer asks for a scheduler-carrying member. **This ADR is therefore the sole
record of why that surface is absent**, and nothing in requirements.md reads as unmet at
verification. The supplementary scheduler-delegation work the withdrawal displaced is OOS-2.

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

#### Why there is no scheduler member

Earlier drafts of this ADR exposed a scheduler-carrying provider member so a generated test could
prove a delayed requeue was *delegated to the scheduler* rather than served by native delay. We
withdraw that, and FR-3 with it — its observable behaviour is now covered by the mechanism-agnostic
requirements.md FR-2 — for two reasons:

1. **It is a mechanism assertion, which NFR-3 and OOS-1 forbid.** "Did the requeue go via the
   scheduler rather than native delay?" is a question about *how* the transport achieves the
   behaviour. The generic suite exists to prove *that* delayed requeue works. Asserting the
   mechanism reintroduces the native/non-native distinction this spec set out to remove — just
   relocated from a config flag into a test assertion.
2. **The scheduler seam does not exist for most of the target set.**
   `IAmAChannelFactoryWithScheduler` is implemented by six gateways only — Kafka, MQTT, MsSql,
   Redis, RMQ.Async, RMQ.Sync — and only those consumers accept an `IAmAMessageScheduler`. Of the
   ~20 gateway configurations the generator targets, **6** can carry a scheduler (Kafka ×2, MSSQL,
   Redis, RMQ.Async ×2); the other **14** (AWS ×4, AWS.V4 ×4, GCP ×4, PostgreSQL, RocketMQ) take no
   scheduler at all. Nine of those fourteen honour the delay **natively** —
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
   **fail on GCP ×4 and RocketMQ** at the flip, because neither actually delays a requeue. That is
   a genuine conformance gap — exactly what an ungated suite is meant to expose — and ADR 0067
   sequences and governs it (RocketMQ's is blocked on an upstream dependency, so it is a likely
   signed-off `Deferred` row rather than an in-spec `Fixed`).

So FR-2's generated test asserts the observable outcome only: *requeue with delay D, and the message
is redelivered after D*. That is uniform across all ~20 configurations regardless of mechanism, and
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
  (`.../Generators/MessagingGatewayGenerator.cs`) — loses three branches.
- `MessagingGatewayConfiguration`
  (`.../Configuration/MessagingGatewayConfiguration.cs`) — loses three properties.
- A new `RejectionMetadataKeys` record (in the generated test-support namespace, per transport,
  **not** in `src/Paramore.Brighter` — C-2).
- *(No scheduler spy or scheduler-carrying member — withdrawn with FR-3; see "Why there is no
  scheduler member".)*
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
- **No scheduler member; FR-2's delayed-requeue test is mechanism-agnostic.** The suite asserts
  that a delayed requeue redelivers after the delay, not which mechanism delivered it (NFR-3,
  OOS-1). This keeps the test uniform across all ~20 gateway configurations — including the 14 whose
  consumers cannot carry a scheduler — and avoids obliging every provider to stand up a
  `CommandProcessor`, a `FireSchedulerMessage` handler pipeline and an external bus purely to
  satisfy an assertion the suite no longer makes. See "Why there is no scheduler member".
- **Delete (not fix) the `with_delay` template.** The amended FR-2 (delayed requeue redelivers after
  the delay) supersedes it and is stronger — it passes a non-null delay and uses a bounded retry
  loop; keeping a fixed-in-place duplicate would duplicate knowledge.

#### Recorded for the OOS-2 follow-up: what an in-memory-scheduler harness would cost

A draft of this ADR proposed a provider member handing back a channel backed by an
`InMemoryScheduler`, for a black-box "the scheduler really re-published it" assertion. We dropped it
with FR-3, but the cost analysis is recorded here because whoever builds the OOS-2 supplementary
scheduler tests will hit it.

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
mis-declared PostgreSQL (`false`/`false`), AWS (`HasSupportToDelayedMessages: false` in three of its
four gateway configurations; `SqsStandard` declares `true`), and Kafka (all three gates `false`, in
both Standard and PartitionKey) values (FR-11). Removing the keys (rather than correcting them to
`true`) is the AC-11 outcome: after FR-10 the flags do not exist, so a stale value cannot mislead.

**Delete the broken template.** Remove
`.../Templates/MessagingGateway/{Reactor,Proactor}/When_requeuing_a_failed_message_with_delay_should_receive_message_again.cs.liquid`
(the Reactor variant calls `_channel.Requeue(received);` with no delay after a `SendWithDelay` +
`Thread.Sleep(6s)` — line 62). This is the AC-12 **replace** arm: after deletion, a
template-source inspection MUST confirm that every template *purporting to exercise delayed
requeue* passes a non-null `TimeSpan` to `Requeue` / `RequeueAsync`.

The prohibition is scoped to delayed-requeue templates, not to every call site, because two
templates legitimately requeue with no delay: the plain-requeue template
(`When_requeuing_a_failed_message_should_receive_message_again.cs.liquid`, ungated by FR-10) and the
zero/null-boundary template required by FR-15. (The requeue-count-exhaustion template also requeues
without a delay, but FR-19 deletes it.)
Between them they cover the no-delay path; the amended FR-2 covers the delayed path.

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
  interface (Kafka, Redis, MSSQL, PostgreSQL, RMQ, AWS SNS/SQS, GCP, RocketMQ, …). A transport
  that cannot yet supply an invalid channel or a metadata-key set surfaces as a compile or
  implementation gap. This is *intended* (it is how non-conformance
  becomes visible per FR-13), but it is real work and is the bulk of the follow-on effort.
- **We lose the ability to prove, in the generic suite, that a transport without native delay really
  falls back to Brighter's scheduler.** That was FR-3's purpose, and dropping it means a gateway
  could regress from scheduler-fallback to silently-no-delay and the universal suite would still be
  green *provided the message is redelivered after the delay by some means*. This is an accepted
  trade: the mechanism assertion is unavailable on 14 of 20 configurations anyway, and NFR-3
  forbids it. The mitigation is the OOS-2 supplementary per-transport scheduler tests for the six
  gateways that expose the seam — which should be raised as a follow-up issue rather than left
  implicit.
- Removing the three properties is a **breaking change** to `MessagingGatewayConfiguration` and
  to the `test-configuration.json` schema; any external tooling that reads those keys breaks.
- Replacing `bool setupDeadLetterQueue` with explicit routing-key parameters is a breaking
  change to `CreateSubscription`; every provider and every existing generated caller must move to
  the new signature in the same change (requirements.md FR-1(6), verified by AC-1). The only
  existing template that passes the bool is
  `When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue.cs.liquid`, which FR-19
  deletes — so after this change the parameter has no callers at all.
- Retiring the gates ungates **more than the canonical set**. Because `SkipTest` matches on
  filename substrings, `When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue`
  matches *both* `dead_letter_queue` and `requeuing` and is doubly gated today; it currently
  generates for 16 of the 20 configurations (all but Kafka ×2, MSSQL and PostgreSQL), and after
  FR-10 it would generate for all 20.

  **It must not be allowed to.** Requeue-count exhaustion is enforced by the message pump —
  `Reactor`/`Proactor.RequeueMessage` call `Header.UpdateHandledCount()` then
  `Message.HandledCountReached(RequeueCount)`, and that method has no other caller in the codebase.
  `Channel.Requeue` forwards straight to `_messageConsumer.Requeue` and counts nothing. Where the
  template passes today it is proving the *transport's* native redrive: the AWS provider pairs
  `requeueCount: 3` with `redrivePolicy: new RedrivePolicy(dlqName, 3)`, so SQS does the counting.
  That is a pump test (OOS-5) or a native-mechanism test (NFR-3, OOS-1) — the same defect that got
  FR-3 withdrawn. Requirements.md **FR-19 deletes the template**, as FR-12 deletes the broken
  `with_delay` one.

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
3. **Fix the `with_delay` template in place (FR-12 option a).** Rejected: redundant with and
   weaker than the amended, mechanism-agnostic FR-2; keeping it duplicates knowledge and
   leaves a third delayed-requeue template to maintain.
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
5. **A shared core `RejectionMetadataKeys` type in `src/Paramore.Brighter`.** Rejected per C-2:
   the key names/casing are genuinely per-transport (Kafka `OriginalType` vs Redis/SQS
   `originalMessageType` is a name difference, not just casing). A shared core type would falsely
   imply a single canonical key set and would not match what each gateway actually stamps. Only
   the *semantic set* is universal; the provider owns the names.

## References

- Requirements: [specs/0036-universal-transport-conformance-tests/requirements.md](../../specs/0036-universal-transport-conformance-tests/requirements.md)
- Related ADRs (this ADR builds on all of these; it supersedes none):
  - [`0067-conformance-rollout-and-deferral-governance`](0067-conformance-rollout-and-deferral-governance.md) [Proposed] — **sibling.** Sequences the per-transport rollout around this ADR's gate-retirement flip (fix-then-flip) and governs auditable deferrals via the conformance ledger; a provider that cannot yet supply a member added here (e.g. the invalid-channel read, or the rejection-metadata key set) is tracked as a `Deferred` row there.
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
    seam is opt-in (`IAmAChannelFactoryWithScheduler`) and reaches only 6 of the ~20 target gateway
    configurations, so a scheduler assertion cannot be universal. It remains the seam any OOS-2
    supplementary scheduler test would use.
  - Per-transport DLQ ADRs (`0038-aws-sqs-dlq-direct-send`, `0039-redis-dlq-brighter-managed`,
    `0040-mssql-dlq-brighter-managed`, `0041-postgres-dlq-brighter-managed`,
    `0042-rocketmq-dlq-brighter-managed`, `0043-mqtt-dlq-brighter-managed`,
    `0046-kafka-dlq-producer-for-requeue`) — collectively, the transport DLQ behaviours now
    brought under universal conformance test.
- External references: none.
