---
id: 0070-generator-owned-rejection-and-delay-conformance
title: "Generator-Owned Rejection and Delay Conformance Tests"
status: Proposed
author:
  - "Brighter Team"
created: 2026-08-29
summary: "Makes the Reject routing ladder, Brighter-provisioned dead letter and invalid message channels, rejection metadata and scheduler-backed delay generator-owned templates that run for every gateway configuration by default; re-scopes HasSupportToDeadLetterQueue and HasSupportToDelayedMessages to native-only flags, and replaces the escape hatch with named, justified conformance waivers."
tags:
  - "testing"
  - "test-generation"
  - "message-rejection"
  - "dead-letter-queue"
---

# 70. Generator-Owned Rejection and Delay Conformance Tests

Date: 2026-08-29

## Status

Proposed

Implements [spec 0036](../../specs/0036-generator-universal-rejection-tests/).
Extends [ADR 0037: Add Messaging Gateway Generated Tests](0037-add-messaging-gateway-generated-test.md).
Tests the behaviour decided in [ADR 0045](0045-provide-dlq-where-missing.md),
[ADR 0047](0047-message-rejection-routing-strategy.md),
[ADR 0037: Universal Scheduler Delay Support](0037-universal-scheduler-delay.md) and
[ADR 0039](0039-transport-scheduler-wiring.md).

## Context

[ADR 0037 (Add Messaging Gateway Generated Tests)](0037-add-messaging-gateway-generated-test.md)
introduced a Liquid-template generator that emits a common messaging gateway suite per transport,
with per-configuration feature flags to skip templates a transport cannot satisfy. At the time,
every flag described a genuine broker capability: publish confirmation, broker-existence validation,
infrastructure validation.

Since then we made three decisions that changed what "the transport cannot do this" means:

- [ADR 0045](0045-provide-dlq-where-missing.md) — where a broker has no native dead letter queue,
  Brighter provisions a dead letter channel and drives it from `Reject`.
- [ADR 0047](0047-message-rejection-routing-strategy.md) — `Reject` routes `DeliveryError` to the
  dead letter channel and `Unacceptable` to the invalid message channel, falling back to the dead
  letter channel; the rejected message is stamped with origin metadata first.
- [ADR 0037 (Universal Scheduler Delay)](0037-universal-scheduler-delay.md) and
  [ADR 0039](0039-transport-scheduler-wiring.md) — where a broker has no native delay,
  `SendWithDelay` delegates to `IAmAMessageScheduler`, wired through the channel factory.

Each of those is a promise Brighter makes *on behalf of* a transport. None of them could be
expressed in the generator, because the generator had no way to build a consumer with dead letter
and invalid message routing, and no way to wire a scheduler. So the behaviour was tested by hand,
once per transport, by whoever implemented that transport's DLQ spec (0001, 0010–0015).

Two problems follow.

**The flags now mean the wrong thing.** `HasSupportToDeadLetterQueue` and
`HasSupportToDelayedMessages` read as native-capability switches but sit in front of templates that
exercise behaviour Brighter supplies universally. In practice they became "set this to whatever
keeps the suite green". The declarations no longer match the code:

| Configuration | Declares | Code says |
|---|---|---|
| PostgreSQL | `DelayedMessages: false` | `PostgresMessageProducer.SendWithDelay` writes a delay parameter into the insert |
| PostgreSQL | `DeadLetterQueue: false` | `PostgresSubscription : IUseBrighterDeadLetterSupport, IUseBrighterInvalidMessageSupport` |
| MSSQL | `DeadLetterQueue: false` | `MsSqlSubscription : IUseBrighterDeadLetterSupport, IUseBrighterInvalidMessageSupport` |
| AWS `SnsStandard`, `SnsFifo`, `SqsFifo` | `DelayedMessages: false` | SQS has native `DelaySeconds` |

Delayed send is consequently generated for three of twenty-one gateway configurations: AWS
`SqsStandard`, AWS.V4 `SqsStandard`, and RocketMQ.

**The coverage is broad but uneven.** Reject-to-DLQ and the fallback ladder are hand-written on
essentially every modern transport; reject-to-invalid on most; requeue-with-delay on six; rejection
metadata on one (Kafka). GCP Pub/Sub has none of it. The full inventory is in
[the requirements](../../specs/0036-generator-universal-rejection-tests/requirements.md).

There are also two defects in the existing templates. `When_requeuing_a_failed_message_with_delay_should_receive_message_again`
calls `Requeue(received)` without the `TimeSpan?` argument, so it never exercises a delayed requeue —
it is the plain requeue test plus a sleep, and weaker, because it lacks the plain test's
receive-retry loop. And because `SkipTest` matches filename substrings, that filename matches both
`requeuing` and `with_delay`, so it is gated on two unrelated flags at once.

### What "universal" actually is today

Issue #4240 proposes making these templates ungated on the ground that the behaviour is universal.
Verifying that against the source shows it is *near*-universal, not universal, and the exceptions
are load-bearing for this design:

| Transport | `IUseBrighterDeadLetterSupport` + `IUseBrighterInvalidMessageSupport` | `IAmAChannelFactoryWithScheduler` |
|---|---|---|
| AWS SQS / SQS V4 | yes | no |
| Kafka | yes | yes |
| MSSQL | yes | yes |
| PostgreSQL | yes | no |
| Redis | yes | yes |
| RocketMQ | yes | no |
| MQTT | yes | yes |
| RabbitMQ (Sync, Async) | **no** — native DLX, `DeadLetterRoutingKey` is get-only DLX config | yes |
| GCP Pub/Sub | **no** — native `DeadLetterPolicy` | no |
| Azure Service Bus | **no** — native DLQ | no |

Of the 21 generator-onboarded gateway configurations, 15 support the Brighter rejection channels
(AWS x4, AWS.V4 x4, Kafka x3, MSSQL, PostgreSQL, Redis, RocketMQ) and 6 do not (GCP x4,
RMQ.Async x2). A genuinely ungated template set would fail on those six.

So the design question is not "gate or ungate". It is: **when a transport does not meet a universal
commitment, how does that fact get recorded?** Today it is recorded as a capability flag, which
reads as "this transport does not need to do this". That is the actual defect.

## Decision

We will make the Brighter-provided rejection and delay behaviours generator-owned and on by
default, split the existing flags along the native/provided seam, and replace the escape hatch with
named conformance waivers.

### 1. The canonical behaviour set

The generator owns eight behaviours, each emitted as a Reactor and a Proactor template. Names follow
the convention the hand-written tests already share, so that a template replaces its hand-written
predecessor under the same name.

**Brighter-provided — on by default:**

| # | Template |
|---|---|
| 1 | `When_rejecting_message_with_delivery_error_should_send_to_dlq` |
| 2 | `When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel` |
| 3 | `When_rejecting_message_with_unacceptable_and_no_invalid_channel_should_fallback_to_dlq` |
| 4 | `When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log` |
| 5 | `When_rejecting_message_should_include_rejection_metadata` |
| 6 | `When_requeuing_a_failed_message_with_delay_should_receive_message_again` (repaired) |
| 7 | `When_requeuing_a_failed_message_with_delay_should_use_scheduler` |
| 8 | `When_reading_a_delayed_message_via_the_messaging_gateway_should_delay_delivery` (ungated) |

**Native — gated:**

| # | Template | Gate |
|---|---|---|
| 9 | `When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue` | `HasNativeDeadLetterQueue` |
| 10 | `When_sending_a_delayed_message_with_no_scheduler_should_delay_natively` | `HasNativeDelay` |

Template 8 keeps its existing generated name rather than moving to the hand-written convention: it
is already a generated template on three configurations, and renaming it churns those for no gain.
Templates 1–5 and 7 take the hand-written names, because those are the files they retire.

Behaviour 4 has genuinely different per-transport semantics — the hand-written variants are named
`..._should_acknowledge_and_log`, `..._should_log_warning`, `..._should_delete_and_log_warning`,
`..._should_remove_from_inflight`, `..._should_return_true`. The template asserts the invariant they
share: `Reject` returns without throwing, and the message is not redelivered. Transport-specific
assertions beyond that stay hand-written.

### 2. Two flag families, split along the native/provided seam

`HasSupportToDeadLetterQueue` and `HasSupportToDelayedMessages` are **removed** from
`MessagingGatewayConfiguration`. In their place:

```jsonc
{
  // Native broker capability. Gates native-only tests (9, 10). Nothing else.
  "HasNativeDeadLetterQueue": true,   // SQS redrive, RMQ DLX, GCP dead letter policy
  "HasNativeDelay": true,             // SQS DelaySeconds, Postgres delay column, RocketMQ

  // Known non-conformance. Every entry is a defect with an owner.
  "ConformanceWaivers": {
    "BrighterRejectionChannels": "RmqSubscription does not implement IUseBrighterDeadLetterSupport; uses native DLX. Tracked by #NNNN"
  }
}
```

Removing rather than renaming the old flags is deliberate: a stale `HasSupportToDeadLetterQueue: false`
left in a configuration file must fail to bind, not silently keep skipping tests.

`HasNativeDelay` also selects between templates 6 and 7 for the requeue-with-delay assertion — with
native delay we assert redelivery directly; without, we assert the scheduler was asked.

### 3. Waivers, not capability flags

A waiver is a named string keyed by behaviour group, whose value is the reason and tracking
reference. Waiver names are a closed set defined by the generator
(`BrighterRejectionChannels`, `BrighterInvalidMessageChannel`, `SchedulerBackedDelay`), so a typo is
a generation error rather than a silent skip.

The generator logs every waiver it honours at `Information` and emits the reason as a comment header
in the affected configuration's generated folder, so the gap is visible in the test tree and not
only in JSON.

Waivers we expect to declare on adoption:

| Configuration | Waiver | Reason |
|---|---|---|
| GCP x4 | `BrighterRejectionChannels` | `PubSubSubscription` uses native `DeadLetterPolicy`; implements neither Brighter interface |
| GCP x4 | `SchedulerBackedDelay` | `GcpPubSubChannelFactory` does not implement `IAmAChannelFactoryWithScheduler` |
| RMQ.Async x2 | `BrighterRejectionChannels` | `RmqSubscription.DeadLetterRoutingKey` is get-only DLX configuration |
| AWS x4, AWS.V4 x4, PostgreSQL, RocketMQ | `SchedulerBackedDelay` | channel factory does not implement `IAmAChannelFactoryWithScheduler` (native delay covers behaviours 6 and 8) |

This is the point of the change. The waiver table *is* the conformance gap, stated once, in a form a
reviewer can read and a maintainer can burn down. Under the current flags the same information is
spread across nine JSON files as `false`, indistinguishable from "not applicable".

### 4. Provider interface extension

The generated `IAmAMessageGatewayReactorProvider` and `IAmAMessageGatewayProactorProvider` gain the
ability to describe rejection routing and to wire a scheduler. The `bool setupDeadLetterQueue`
parameter is replaced, because a boolean cannot express "invalid channel only", which behaviours 2
and 3 require.

```csharp
/// Which Brighter rejection channels a subscription should be built with.
public sealed record RejectionRouting(RoutingKey? DeadLetter = null, RoutingKey? InvalidMessage = null)
{
    public static readonly RejectionRouting None = new();
}
```

Reactor provider (Proactor mirrors it with `Async` suffixes and a `CancellationToken`):

```csharp
{{ Subscription }} CreateSubscription(
    RoutingKey routingKey,
    ChannelName channelName,
    OnMissingChannel makeChannel,
    RejectionRouting? rejectionRouting = null,
    int? requeueCount = null);

IAmAChannelSync CreateChannel(
    {{ Subscription }} subscription,
    IAmAMessageScheduler? scheduler = null);

/// Read a message back from a rejection channel (dead letter or invalid message).
Message GetMessageFrom(RoutingKey routingKey);

/// True when the producer honours SendWithDelay itself, with no scheduler.
bool HasNativeDelay { get; }
```

`GetMessageFromDeadLetterQueue(subscription)` is replaced by `GetMessageFrom(routingKey)`. The
existing implementations already do exactly this internally — they read
`subscription.DeadLetterRoutingKey` and build a consumer on it — so the change is mechanical, and it
is what behaviours 2 and 3 need in order to read the invalid channel.

`RejectionRouting` is emitted per gateway namespace, alongside the provider interface it belongs to,
matching how the two provider interfaces are already duplicated across the Reactor and Proactor
namespaces.

This is a breaking change to 21 hand-written provider classes. That cost is accepted: it is paid
once, and it is the reason the behaviour could not be templated before.

### 5. The scheduler used by generated tests

Behaviours 6, 7 and 8 need a scheduler for transports without native delay. The generator emits a
shared test double, `RecordingMessageScheduler`, implementing `IAmAMessageSchedulerSync` and
`IAmAMessageSchedulerAsync`, which:

- records the message and delay passed to `Schedule`, so behaviour 7 can assert the wiring; and
- when constructed with a producer, re-sends the message through it once the delay elapses, so
  behaviours 6 and 8 assert real redelivery end to end.

Issue #4240 proposes using `InMemoryScheduler` for this. We do not, because `InMemoryScheduler`
takes an `IAmACommandProcessor` and fires by posting through it, which would require every generated
gateway test to stand up a command processor, producer registry and outbox. That turns a gateway
conformance test into an integration test across the gateway, the scheduler and the outbox — the
opposite of the stated intent of "avoid needing to test across both the Gateway and the Scheduler".
`RecordingMessageScheduler` keeps the seam at the gateway and satisfies both assertions with one
double.

### 6. Retiring the hand-written tests

A hand-written test is deleted in the same change that generates its replacement, so each behaviour
has one owner. Tests asserting transport-specific detail beyond the canonical set are kept: Kafka's
`When_kafka_consumer_disposes_should_dispose_requeue_producer` and
`When_creating_dlq_producer_with_make_channels_create_should_create_topic`, PostgreSQL's
`When_postgres_consumer_requeues_with_delay_should_use_native_sql`, Redis's and MSSQL's
zero-delay-uses-direct-path tests, RMQ's `When_queue_length_causes_a_message_to_be_rejected`, and
the `When_creating_*_subscription_with_dlq_routing_keys_should_expose_properties` unit tests.

MQTT, RMQ.Sync and Azure Service Bus are not onboarded to the generator, so their hand-written tests
are untouched by this change.

## Consequences

### Positive

- The Brighter-provided rejection ladder, metadata stamping and scheduler-backed delay become
  conformance obligations that a new transport inherits by adding a `test-configuration.json`,
  rather than a checklist a contributor may or may not find.
- GCP Pub/Sub goes from zero rejection coverage to either full coverage or an explicit, tracked
  waiver — in both cases, a stated position rather than an omission.
- Rejection metadata (behaviour 5) goes from one transport to all conformant ones.
- Delayed send goes from 3 of 21 configurations to all non-waived ones.
- The stale declarations on PostgreSQL, MSSQL and AWS SQS are corrected, and the correction is
  forced: the removed flags cannot be left behind.
- Two real defects (D-1, D-2) are fixed, and the filename-substring gating that caused D-2 is
  constrained to the two remaining native flags.

### Negative

- All 21 provider classes must be updated in one change; the test projects do not compile until they
  are. This is the single largest cost of the design.
- Waivers can rot. A waiver with a closed tracking issue and no follow-up is indistinguishable from
  a live one. Mitigation: waiver values carry the issue reference, and the generator logs them, so
  they surface on every regeneration rather than only on review.
- Behaviour 4's assertion is weaker than the hand-written tests it replaces, because the transports
  genuinely differ on what "no channels configured" does. The transport-specific assertions must be
  retained by hand where they matter, which reintroduces a little of the duplication this change
  removes.
- Adding delay-based tests to every configuration increases suite runtime. Delays are configurable
  per configuration for this reason, but the aggregate cost across 21 configurations is real.

### Neutral

- The generated class names still use the `WhenXShouldY` form rather than the `[Behavior]Tests`
  convention of [spec 0031](../../specs/0031-test_naming_conventions/). New templates follow the
  existing generated style for consistency; realigning the generator with spec 0031 is separate work.

## Alternatives Considered

**Ungate the templates entirely, with no waiver mechanism.** This is what #4240 proposes. It is the
purest statement of the commitment, and the six failing configurations would be the point. Rejected
because it lands a red CI on GCP and RMQ.Async with no route to green that is in scope here — the
fix is production changes to three transports. A waiver states the same fact without holding the
generator change hostage to those.

**Rename the flags to `ProvidesBrighterDeadLetterQueue` and keep them as booleans.** Simpler, no new
concept. Rejected because a boolean `false` still reads as a property of the transport rather than a
defect, which is exactly the failure mode being fixed. A waiver requires a sentence of justification
to be written down.

**Keep `bool setupDeadLetterQueue` and add a second `bool setupInvalidMessageChannel`.** Less
disruptive to the 21 providers. Rejected because the parameter list becomes positional booleans at
the call site, and it still cannot express the routing keys the read-back needs.

**Use `InMemoryScheduler` in the generated tests.** Discussed in §5. Rejected: it requires a command
processor per test and widens the unit under test.

**Assert native and provided behaviour in one template, branching on a flag.** Rejected: the two
have different arrange steps and different assertions, and a template with two modes is harder to
read than two templates, which is the whole reason the generator exists.

## Related ADRs

- [0037 Add Messaging Gateway Generated Tests](0037-add-messaging-gateway-generated-test.md) — the generator extended here
- [0035 Test Generation Tool](0035-generated-test.md)
- [0045 Provide a Dead Letter Channel Where Native Support is Missing](0045-provide-dlq-where-missing.md)
- [0047 Message Rejection Routing Strategy](0047-message-rejection-routing-strategy.md)
- [0037 Universal Scheduler Delay Support](0037-universal-scheduler-delay.md)
- [0039 Transport Channel Factory Scheduler Wiring](0039-transport-scheduler-wiring.md)
- [0046 Kafka DLQ Producer for Requeue](0046-kafka-dlq-producer-for-requeue.md)
