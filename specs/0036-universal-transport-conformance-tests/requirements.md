# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Linked Issue**: #4240

## Problem Statement

As a **Brighter maintainer / transport contributor**, I want the messaging-gateway test
generator to own and generate — for *every* transport — the conformance tests for the
Reject/dead-letter/invalid-message/requeue-with-delay/delayed-send behaviours that Brighter
guarantees universally, so that I can add or change a transport and have the generated suite
prove the transport honours those universal obligations, instead of relying on broad,
inconsistent, hand-written duplicates and on capability flags that are set to whatever makes
the suite green.

Today these behaviours are gated behind generator flags — `HasSupportToDelayedMessages`,
`HasSupportToDeadLetterQueue`, and (for plain requeue) `HasSupportToRequeue` — that are treated
as *native-capability* switches. This is wrong on two counts:

1. **They gate behaviour Brighter provides universally.** When a transport has no native
   delay, Brighter falls back to its own scheduler (held on the producer's `Scheduler`
   property) for delayed send/requeue; when a transport has no native DLQ, Brighter provisions
   a dead-letter (and invalid-message) producer driven by the `Reject` flow.
   `Reject(Message, MessageRejectionReason?)` is on `IAmAMessageConsumerSync` /
   `IAmAMessageConsumerAsync` (and surfaced on `IAmAChannelSync` / `IAmAChannelAsync`) for
   every transport. The rejection reasons are the `RejectionReason` enum (`None`,
   `Unacceptable`, `DeliveryError`); `MessageRejectionReason`
   (`src/Paramore.Brighter/MessageRejectionReason.cs`) pairs a `RejectionReason` with an
   optional description. These are universal conformance obligations, not opt-in capabilities.
2. **The flags are mis-declared against the code.** Verified in the per-transport
   `test-configuration.json` files: PostgreSQL declares `HasSupportToDeadLetterQueue: false`
   and `HasSupportToDelayedMessages: false` despite having a native delay column and
   native/provided DLQ; three of AWS's four gateway configurations (`SnsStandard`, `SnsFifo`,
   `SqsFifo`, in both the V3 and V4 test projects) declare `HasSupportToDelayedMessages: false`
   despite native `DelaySeconds` (≤15 min); and Kafka declares **all three** gates `false` even
   though its hand-written suite is the canonical grounding for these behaviours. Only AWS
   `SqsStandard` and RocketMQ declare `HasSupportToDelayedMessages: true`, so the delayed-delivery
   test currently runs for just three of the ~20 gateway configurations.

The root cause is timing: the Reject→DLQ / invalid-channel / requeue-via-producer /
requeue-via-scheduler features post-date the generator, so contributors hand-wrote them per
transport (see the Kafka and RMQ.Sync `MessagingGateway/Reactor` test folders). The result is
broad but inconsistent coverage — e.g. GCP has none of these tests and Azure/ASB has only
partial coverage — and the one generated `with_delay` template is broken (see FR-12).

## Proposed Solution

Make the canonical Reject/DLQ/requeue-with-delay/delayed-send/Nack behaviours a
**universal, ungated** part of the generated messaging-gateway suite, produced identically for
every transport in both sync (Reactor) and async (Proactor) variants. Retire the
`HasSupportToDelayedMessages`, `HasSupportToDeadLetterQueue`, and `HasSupportToRequeue` opt-in
gates and correct the configs that were mis-declared against them. The generic conformance suite proves *that* a
behaviour is supported (delayed send works, requeue-with-delay works, `Reject` routes to
DLQ/invalid channel, the fallback ladder holds, rejection metadata is stamped), **regardless of
whether the transport achieves it natively or via Brighter's scheduler/producer fallback** — the
tests do not assert *how* it is achieved.

Where generating an ungated template for a transport surfaces that the transport does not
actually conform, that non-conformance is a **defect in that transport's gateway** and fixing it
is part of this work (fix-to-conform; see FR-13).

## Objective and Test Boundary

The purpose of this suite is to prove that **each transport's gateway** implements the
reject / requeue / delay support correctly. It is **not** to re-prove Reactor/Proactor message-
pump orchestration — that is already owned by Brighter's core in-memory tests and must not be
duplicated here.

Concretely:

- **Unit under test.** Canonical tests exercise the transport's **channel** and **producer**
  surface directly: `IAmAChannelSync` / `IAmAChannelAsync` (`Receive`, `Reject`, `Requeue`,
  `Acknowledge`) and `IAmAMessageProducerSync` / `IAmAMessageProducerAsync`
  (`Send`, `SendWithDelay`). *(The producer's `Scheduler` was in scope while FR-3 asserted
  scheduler delegation; with FR-3 withdrawn the suite no longer reaches for that mechanism.)*
- **Sync and async are both exercised** because the sync and async channel/producer methods are
  genuinely distinct code paths. This — not pump mechanics — is what the Reactor and Proactor
  test variants (FR-14) mean in this suite.
- **Right-sized assertions.** For each pathway the test proves the observable outcome (message
  redelivered / message on DLQ / message on invalid channel / metadata present), not the
  internal mechanism. Do not over-test: assert that the path works, not how the gateway wires
  it internally.

## Requirements

### Functional Requirements

Terminology (used consistently below):
- **Channel / ChannelAsync** — `IAmAChannelSync` / `IAmAChannelAsync`, the sync/async surface a
  test drives (`Receive`, `Reject`, `Requeue`, `Acknowledge`).
- **DLQ** — the dead-letter channel to which a rejected message is routed when it cannot be
  delivered; identified by a *dead-letter routing key* on the subscription.
- **Invalid channel** — the invalid/unacceptable-message channel to which an `Unacceptable`
  rejection is routed; identified by an *invalid-message routing key* on the subscription.
- **Fallback ladder** — the ordering Brighter applies when routing a rejected message:
  `Unacceptable` → invalid channel if configured, else DLQ; `DeliveryError` → DLQ; `None` /
  unspecified reason → DLQ.
- **Ungated** — the template is generated for every transport regardless of any capability
  flag.
- **Canonical behaviours** — the behaviours in FR-2, FR-4 … FR-9, FR-15, FR-16 (Nack) and FR-17
  (reject with `None`/unspecified reason → DLQ). FR-3 is not among them: ADR 0066 withdrew it as a
  mechanism assertion and folded it into FR-2.
- **Rejection-metadata key names** — the per-transport `Header.Bag` key strings under which the
  rejection metadata is stamped. The *semantic set* is universal (see FR-8); only the key
  *names/casing* vary per transport (e.g. Kafka `OriginalTopic`, Redis/SQS `originalTopic`).

**FR-1 — Provider-interface extension to enable the suite.**
The generated provider interfaces `IAmAMessageGatewayReactorProvider` and
`IAmAMessageGatewayProactorProvider`
(`tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/{Reactor,Proactor}/IAmAMessageGateway*Provider.cs.liquid`)
MUST be extended so a test can:
  1. create a subscription/channel configured with **both** a dead-letter routing key and an
     invalid-message routing key (today `CreateSubscription` exposes only a
     `bool setupDeadLetterQueue` and no invalid-message routing key);
  2. create a channel configured with a DLQ only, with an invalid channel only, and with
     neither (to drive FR-6 and FR-7);
  3. read a message from the invalid channel (an analogue of the existing
     `GetMessageFromDeadLetterQueue`);
  4. **[WITHDRAWN by ADR 0066 — no scheduler-carrying member is required.]** *Originally: "obtain a
     producer whose `Scheduler` property is set" to an in-memory scheduler or a recording spy, so
     the scheduler-fallback test (FR-3) could interrogate the delegation.* With FR-3 folded into
     FR-2 as a mechanism-agnostic observable-outcome test, the generic suite never inspects the
     scheduler, so the provider needs no scheduler-carrying member — and mandating one would have
     obliged all ~20 provider implementations to stand up a `CommandProcessor`, a
     `FireSchedulerMessage` handler pipeline and an external bus purely to satisfy an assertion the
     suite no longer makes. A scheduler-aware provider member may be introduced later for the
     supplementary per-transport work described in FR-3 / OOS-2;
  5. expose the transport's **rejection-metadata key names** so a generated test asserts the
     universal semantic set (FR-8) without hard-coding any one transport's key strings.

  *Example:* `CreateSubscription(routingKey, channelName, OnMissingChannel.Create,
  deadLetterRoutingKey: "orders.dlq", invalidMessageRoutingKey: "orders.invalid")` returns a
  subscription from which the provider builds a channel whose `Reject` routes per the fallback
  ladder; `provider.RejectionMetadataKeys.OriginalTopic` returns `"OriginalTopic"` for Kafka
  and `"originalTopic"` for Redis/SQS.

**FR-2 — Requeue with delay redelivers after the delay (mechanism-agnostic).**
Generate a test proving that when a channel requeues a received message with a non-zero delay,
the message is redelivered after that delay — **regardless of whether the transport achieves the
delay natively, by routing the requeue through a producer, or via Brighter's scheduler fallback**.
Per NFR-3 the test asserts only the observable outcome; it does **not** assert which mechanism the
gateway used. *(Amended per ADR 0066: originally "Requeue with delay via producer". FR-3 —
scheduler-fallback — is folded into this requirement; see FR-3.)*
  *Example (grounded in
  `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Reactor/When_kafka_consumer_requeues_with_delay_should_use_producer.cs`):*
  send message `M`; receive `M` off the channel; call `channel.Requeue(M, TimeSpan.FromSeconds(5))`;
  assert `Requeue` returns `true` and a subsequent receive (within a bounded retry loop) yields a
  message whose body equals `M`'s body.

**FR-3 — [FOLDED INTO FR-2 by ADR 0066 — no separate canonical test.]**
*Originally: "Requeue with delay via scheduler fallback" — a test proving the requeue is delegated
to the producer's scheduler when the transport has no native delay.*

This is **withdrawn as a distinct canonical behaviour** in the generic conformance suite, for two
reasons established during design review:

1. **It is a mechanism assertion, which NFR-3 forbids.** Asserting the requeue went *via the
   scheduler* rather than *via native delay* is precisely testing *how* a transport achieves the
   behaviour. NFR-3 and OOS-1 require the generic suite to assert only *that* delayed requeue works.
2. **The scheduler seam does not exist for most transports.** `IAmAChannelFactoryWithScheduler` is
   implemented by only six gateways (Kafka, MQTT, MsSql, Redis, RMQ.Async, RMQ.Sync) — within the
   generator's target set, 6 of the ~20 gateway configurations. The other 14 (AWS ×4, AWS.V4 ×4,
   GCP ×4, PostgreSQL, RocketMQ) have consumers that take no scheduler at all — nine of them delay
   natively (e.g. `SqsMessageConsumer.RequeueAsync` uses `ChangeMessageVisibilityAsync`;
   `PostgresMessageConsumer.Requeue` binds the delay as a query parameter), while GCP ×4 ignores the
   delay outright and RocketMQ's `Requeue` is currently a no-op pending an upstream client fix.
   Running a scheduler-delegation assertion against any of the 14 would fail **by design**,
   including against transports that are nonetheless conformant, and giving them the seam would mean
   a public runtime API change that C-1 forbids this spec. *(The GCP and RocketMQ gaps are real
   FR-2 non-conformances and are governed by ADR 0067's rollout ledger.)*

The observable behaviour FR-3 cared about — *a delayed requeue actually redelivers after the
delay* — is fully covered by the amended FR-2, uniformly across every transport. A
scheduler-delegation test remains valuable for the six gateways that have the seam, but as
**supplementary per-transport work under OOS-2**, not as part of the universal suite.

**FR-4 — Reject with delivery error routes to DLQ.**
Generate a test proving that `channel.Reject(M, new MessageRejectionReason(RejectionReason.DeliveryError,
"..."))` on a channel configured with a dead-letter routing key causes `M` to appear on the DLQ.
  *Example (grounded in
  `...When_rejecting_message_with_delivery_error_should_send_to_dlq.cs`):* after reject, a
  consumer reading the DLQ receives a message whose body equals `M`'s body and whose
  `Header.Bag` contains the transport's original-topic key (== data topic) and a rejection-reason
  entry.

**FR-5 — Reject with unacceptable reason routes to the invalid channel.**
Generate a test proving that `channel.Reject(M, new MessageRejectionReason(RejectionReason.Unacceptable,
"..."))` on a channel configured with an invalid-message routing key causes `M` to appear on the
invalid channel (and *not* the DLQ).
  *Example (grounded in
  `...When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel.cs`):* the
  invalid-channel consumer receives a message whose rejection-reason key equals `"Unacceptable"`
  and whose original-topic key equals the data topic.

**FR-6 — Fallback ladder: unacceptable with no invalid channel falls back to DLQ.**
Generate a test proving that when a channel is configured with a DLQ only (no invalid channel)
and a message is rejected with `RejectionReason.Unacceptable`, the message falls back to the DLQ.
  *Example (grounded in
  `...When_rejecting_message_with_unacceptable_and_no_invalid_channel_should_fallback_to_dlq.cs`):*
  the DLQ consumer receives the message whose rejection-reason key equals `"Unacceptable"`.

**FR-7 — No channels configured: acknowledge and log.**
Generate a test proving that when a channel has neither a DLQ nor an invalid channel
configured, `channel.Reject(M, ...)` returns `true` (the message is acknowledged/removed so it
is not redelivered) and the channel can go on to receive the next message; nothing blocks.
  *Example (grounded in
  `...When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log.cs`):* send
  `M1` then `M2`; receive `M1`; `Reject(M1, DeliveryError)` returns `true`; the next receive
  yields `M2`. (The observable assertion — reject returns `true` and the next message is
  received — is uniform across transports even though the underlying ack mechanism differs, so
  the test does not assert the mechanism.)

**FR-8 — Rejection metadata stamping (universal semantic set).**
Generate a test proving that a rejected message routed to the DLQ/invalid channel carries the
universal rejection-metadata **semantic set** in `Header.Bag`, asserted via the per-transport key
names the provider exposes (FR-1(5)). The universal semantic set is: **original topic** (equals
the data topic), **original message type** (e.g. `"MT_COMMAND"`), **rejection reason** (e.g.
`"DeliveryError"`), **rejection message** (equals the description passed to `Reject`), and
**rejection timestamp** (a parseable ISO-8601 `DateTimeOffset` within the last minute). The set is
universal; the key strings and casing are supplied by the provider (Kafka PascalCase; Redis/SQS
camelCase). A transport that emits the semantic set under its own names conforms; a transport
that omits a semantic field is non-conformant and handled under FR-13.

**FR-9 — Delayed send (`SendWithDelay`).**
Generate a test proving that a message sent with `producer.SendWithDelay(M, delay)` is not
receivable before the delay elapses and is receivable after it.
  *Example (grounded in the existing
  `Templates/MessagingGateway/Reactor/When_reading_a_delayed_message_via_the_messaging_gateway_should_delay_delivery.cs.liquid`):*
  `SendWithDelay(M, 5s)`; an immediate receive yields `MessageType.MT_NONE`; after ~5s a receive
  yields `M`.

**FR-10 — Retire `HasSupportToDelayedMessages`, `HasSupportToDeadLetterQueue`, and
`HasSupportToRequeue` as opt-in gates.**
The `SkipTest` gating in
`tools/Paramore.Brighter.Test.Generator/Generators/MessagingGatewayGenerator.cs` MUST no longer
skip templates on any of the three gates: `HasSupportToDelayedMessages` (currently gates
filenames containing `delayed_message` and `with_delay`), `HasSupportToDeadLetterQueue`
(currently gates `dead_letter_queue`), or `HasSupportToRequeue` (currently gates `requeuing`).
Requeue is a universal obligation — `Requeue`/`RequeueAsync` is on the consumer/channel interface
for every transport — so plain requeue (the existing
`When_requeuing_a_failed_message_should_receive_message_again` template) becomes ungated and
generates for all transports alongside the canonical templates (FR-2 … FR-9). All three
properties on
`tools/Paramore.Brighter.Test.Generator/Configuration/MessagingGatewayConfiguration.cs` are
removed (or repurposed/deprecated per the ADR), and no new `HasNative*` flag is introduced into
the generic suite (see Out of Scope).

**FR-11 — Correct mis-declared per-transport configurations.**
The `test-configuration.json` files that were set to satisfy the retiring gates MUST be corrected
as part of retiring the flags — including PostgreSQL (both `HasSupportToDeadLetterQueue: false` and
`HasSupportToDelayedMessages: false`), AWS (`HasSupportToDelayedMessages: false` in three of its
four gateway configurations), Kafka (all three gates `false`), and MSSQL
(`HasSupportToDeadLetterQueue: false` despite ADR 0040's Brighter-managed DLQ). After FR-10 all
three keys are removed from every config rather than left with misleading values.

**FR-12 — Resolve the broken `requeuing_with_delay` template.**
The template
`Templates/MessagingGateway/{Reactor,Proactor}/When_requeuing_a_failed_message_with_delay_should_receive_message_again.cs.liquid`
is defective: as found, it calls `_channel.Requeue(received)` with **no** `timeout` argument (the
signature is `IAmAChannelSync.Requeue(Message message, TimeSpan? timeOut = null)` per
`src/Paramore.Brighter/IAmAChannelSync.cs`), so it never exercises delayed requeue — it is a
`SendWithDelay` + `Thread.Sleep` plus a plain requeue, and it is *weaker* than the plain-requeue
template (`When_requeuing_a_failed_message_should_receive_message_again.cs.liquid`) because it
lacks that template's bounded receive-retry loop. This defect MUST be resolved one of two ways,
**the choice being an explicit design (ADR) decision**:
  (a) **fix in place** — rewrite it to pass a non-null delay to `Requeue` and include a bounded
      receive-retry loop; or
  (b) **replace** — delete it because the amended, mechanism-agnostic FR-2 supersedes it.
In no case may a template remain that passes no delay to `Requeue`. AC-12 applies conditionally
on outcome (a).

**FR-13 — Generate ungated for ALL transports; non-conformance is a defect to fix.**
The canonical templates MUST be generated for **every** transport the generator targets —
generation is not optional and no transport is excluded — explicitly including GCP (currently no
coverage) and Azure / Azure Service Bus (currently partial). Where the newly generated suite
fails to compile or fails at runtime for a transport because that transport does not honour a
universal obligation, that is a **defect in the transport's gateway and is in scope to fix**
(default posture: fix-to-conform). No canonical test may be silently skipped, `[Skip]`-ped, or
gated away to make the suite green. If a specific gateway fix is deferred, it MUST be recorded as
a **named, linked follow-up issue with explicit maintainer sign-off** referenced from this spec —
a deferral is auditable, never an open-ended escape hatch.

**FR-14 — Sync (Reactor) and async (Proactor) parity.**
Every canonical template MUST be produced in both a Reactor variant driving `IAmAChannelSync`
(and `IAmAMessageProducerSync`) and a Proactor variant driving `IAmAChannelAsync` (and
`IAmAMessageProducerAsync`), mirroring the existing dual `Templates/MessagingGateway/{Reactor,Proactor}`
layout and the dual provider interfaces. The two variants exist to exercise the distinct sync and
async gateway code paths — not to re-test message-pump orchestration (see Objective and Test
Boundary).

**FR-15 — Zero / null delay requeue is a no-op with respect to delay.**
Generate (or extend a canonical test to assert) that `channel.Requeue(M, TimeSpan.Zero)` and
`channel.Requeue(M, null)` do **not** delay the message — they behave as an immediate plain
requeue: the message is available to be received again without waiting for a delay window. Per
NFR-3 the assertion is the observable outcome (no delay window elapses); the test does not assert
scheduler non-engagement as an internal mechanism. This pins the lower boundary of the delay
parameter so a positive-delay path (FR-2) is not conflated with the zero/null no-op path.
  *Example:* receive `M`; call `channel.Requeue(M, TimeSpan.Zero)`; assert `Requeue` returns
  `true` and `M` is receivable again within the plain-requeue bounded retry loop (no delay
  window elapses).

**FR-16 — Nack releases the message for redelivery.**
Generate a test proving that `channel.Nack(M)` / `channel.NackAsync(M)` releases a received
message back to the transport so it is redelivered on a subsequent receive (as distinct from
`Acknowledge`, which removes it, and `Reject`, which routes it to DLQ/invalid). `Nack` is on
`IAmAChannelSync`/`IAmAChannelAsync` (and the consumer interfaces) for every transport, so this
is a universal obligation.
  *Example (grounded in
  `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Reactor/When_nacking_a_message_it_should_be_redelivered.cs`
  and its `_async` sibling):* send `M`; receive `M`; call `Nack(M)`; a subsequent receive
  (within the bounded retry loop) yields a message with `M`'s id and body. A second variant, with
  two queued messages, proves the nacked `M` is redelivered and the pipeline is not blocked for
  the following message.

**FR-17 — Reject with `None` / unspecified reason routes to DLQ.**
Generate a test proving that `channel.Reject(M, new MessageRejectionReason(RejectionReason.None,
"..."))` (an unknown/unspecified reason) on a channel configured with a dead-letter routing key
routes `M` to the DLQ — the default arm of the fallback ladder — rather than to the invalid
channel. This is a distinct routing rule from FR-4 (`DeliveryError`) and FR-5 (`Unacceptable`).
  *Example (grounded in
  `...When_rejecting_message_with_unknown_reason_should_send_to_dlq.cs`):* after reject with
  `RejectionReason.None`, the DLQ consumer receives the message with the transport's rejection-
  reason key == `"None"` and original-topic key == the data topic.

### Non-functional Requirements

- **NFR-1 (Consistency).** Generated tests MUST follow the established naming convention already
  used by the hand-written tests, e.g.
  `When_rejecting_message_with_delivery_error_should_send_to_dlq`,
  `When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel`,
  `When_rejecting_message_with_unacceptable_and_no_invalid_channel_should_fallback_to_dlq`,
  `When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log`,
  `When_rejecting_message_should_include_metadata`.
- **NFR-2 (Determinism / no flakiness).** Timing-dependent tests MUST use bounded receive-retry
  loops (as in the plain-requeue template's retry loop) rather than a single receive after a
  fixed sleep, so intermittent broker propagation delays do not cause false failures.
- **NFR-3 (No new suite-wide gates / no how-testing).** The generic conformance suite MUST NOT
  assert *how* a behaviour is achieved (native vs Brighter fallback); it asserts only *that* the
  observable behaviour holds, and only against the channel/producer surface (not the pump).
- **NFR-4 (Isolation of scheduler test).** **[MOOT — withdrawn with FR-3 by ADR 0066.]**
  *Originally: the scheduler-fallback test MUST use an in-memory scheduler or a recording spy rather
  than an external scheduler transport.* With FR-3 folded into FR-2 as a mechanism-agnostic test,
  the generic suite drives no scheduler at all, so there is nothing to isolate. The constraint
  carries forward to the supplementary per-transport scheduler work (FR-3 / OOS-2) if and when that
  is undertaken.

### Constraints and Assumptions

- **C-1.** Work is confined to the generator (`tools/Paramore.Brighter.Test.Generator`), its
  templates and per-transport `test-configuration.json` files, plus any transport-gateway source
  fixes required by FR-13. No public Brighter runtime API is redesigned by this spec beyond what
  FR-1 requires of the *generated* provider interfaces.
- **C-2.** The runtime contract the templates target already exists in `src/Paramore.Brighter`:
  `Reject(Message, MessageRejectionReason?)`, `SendWithDelay`, `Requeue(Message, TimeSpan?)`, the
  `RejectionReason` enum (`None`/`Unacceptable`/`DeliveryError`) and `MessageRejectionReason`
  record, `IAmAChannelSync`/`IAmAChannelAsync`, `IAmAMessageProducer.Scheduler`, and
  `InMemoryScheduler`. **The rejection-metadata header key names are NOT a shared core type** —
  they are defined per transport (e.g. `HeaderNames` in
  `src/Paramore.Brighter.MessagingGateway.Kafka` uses PascalCase; Redis and SQS stamp camelCase
  keys inline). Only the *semantic set* of metadata is universal; the key strings vary, which is
  why the provider exposes them (FR-1(5), FR-8).
- **C-3.** Assumption: the fallback-ladder and metadata semantics observed in the hand-written
  transport tests are the canonical Brighter behaviour and apply to every transport. Any transport
  whose gateway diverges is treated under FR-13.
- **C-4.** Assumption: transports that provision DLQ/invalid channels via `OnMissingChannel.Create`
  can create those channels within the test's bounded wait; where a transport needs explicit
  provisioning, the provider template supplies it.

### Out of Scope

- **OOS-1.** Re-introducing any `HasNative*` capability flag into the generic suite. The user
  explicitly rejects a native/non-native distinction in these conformance tests: "We don't want
  to test how, we want to test supported."
- **OOS-2.** Supplementary tests that prove the *native* variant of a behaviour specifically
  works (e.g. AWS SQS redrive policy, RabbitMQ DLX, PostgreSQL native-delay column, native
  `DelaySeconds`). These are candidate follow-up work and MUST be captured as a separate issue.
- **OOS-3.** Transport-*internal* mechanics the Kafka suite tests that are not universal channel
  behaviour — offset commit/sweep, header byte round-tripping, partition-key handling,
  fatal/non-fatal error escalation, producer-not-persisted synthesis, requeue-producer disposal,
  and factory/subscription wiring unit tests (`When_creating_channel_with_dlq_subscription_should_pass_routing_keys`,
  `When_kafka_channel_factory_forwards_scheduler_to_consumers`, etc.). The wiring is proven
  transitively by the end-to-end canonical tests (FR-4, FR-5); the rest is Kafka-specific.
  See "Coverage reconciliation against the Kafka surface area".
- **OOS-4.** Sibling defects #4238 (single `Outbox` async-only) and #4239 (`CollectionName`
  ignored by sync outbox templates). Context only.
- **OOS-5.** Driving any canonical behaviour through the `Reactor`/`Proactor` message pump.
  Pump orchestration is owned by the core in-memory tests (see Objective and Test Boundary).

## Acceptance Criteria

- **AC-1 (FR-1).** *Given* a transport's generated provider, *when* a test calls
  `CreateSubscription` with a dead-letter routing key and an invalid-message routing key, *then*
  it obtains a channel that routes rejections per the fallback ladder; and the provider exposes a
  means to create channels configured with a DLQ only, an invalid channel only, and neither —
  ADR 0066 does this via nullable, defaulted `deadLetterRoutingKey` / `invalidMessageRoutingKey`
  parameters on a single `CreateSubscription` rather than separate members (FR-1(2),
  validated in use by AC-5/AC-6/AC-7), to read from the DLQ, read from the invalid channel, and
  read the transport's rejection-metadata key names. *(The scheduler-carrying member originally
  required by FR-1(4) is withdrawn — see FR-1(4) and FR-3.)*
- **AC-2 (FR-2).** *Given* a received message on **any** target transport, *when*
  `channel.Requeue(message, 5s)` is called, *then* `Requeue` returns `true` and a later receive
  (within the bounded retry loop) yields a message with the same body — and the test makes no
  assertion about *how* the delay was achieved (native, producer, or scheduler).
- **AC-3 (FR-3).** **[WITHDRAWN with FR-3 by ADR 0066.]** The observable outcome it asserted is
  covered by AC-2, which now applies uniformly to every transport. No separate scheduler-delegation
  criterion applies to the generic suite.
- **AC-4 (FR-4).** *Given* a channel with a dead-letter routing key, *when*
  `channel.Reject(message, DeliveryError)` is called, *then* the DLQ consumer receives the message
  with the transport's original-topic key == the data topic and a rejection-reason entry present.
- **AC-5 (FR-5).** *Given* a channel with an invalid-message routing key, *when*
  `channel.Reject(message, Unacceptable)` is called, *then* the invalid-channel consumer receives
  the message with the transport's rejection-reason key == `"Unacceptable"` and it does **not**
  appear on any DLQ.
- **AC-6 (FR-6).** *Given* a channel with a DLQ but no invalid channel, *when*
  `channel.Reject(message, Unacceptable)` is called, *then* the DLQ consumer receives the message
  with the transport's rejection-reason key == `"Unacceptable"`.
- **AC-7 (FR-7).** *Given* a channel with neither DLQ nor invalid channel and two queued messages
  `M1`, `M2`, *when* `channel.Reject(M1, DeliveryError)` is called, *then* it returns `true` and
  the next receive yields `M2`.
- **AC-8 (FR-8).** *Given* a rejected message on the DLQ, *when* its header bag is inspected using
  the provider-supplied key names, *then* the universal semantic set is present and correct:
  original topic (== data topic), original message type (`"MT_COMMAND"`), rejection reason
  (`"DeliveryError"`), rejection message (== the passed description), and rejection timestamp
  (parseable ISO-8601, within the last minute).
- **AC-9 (FR-9).** *Given* `producer.SendWithDelay(message, 5s)`, *when* a receive is attempted
  immediately, *then* it yields `MT_NONE`; *when* a receive is attempted after the delay, *then*
  it yields the message.
- **AC-10 (FR-10).** *Given* the generator source, *when* `SkipTest` is inspected, *then* it
  contains no branch keyed on `HasSupportToDelayedMessages`, `HasSupportToDeadLetterQueue`, or
  `HasSupportToRequeue`, and those properties no longer gate `delayed_message`, `with_delay`,
  `dead_letter_queue`, or `requeuing` templates; and the three properties are absent from
  `MessagingGatewayConfiguration` and from every `test-configuration.json`.
- **AC-11 (FR-11).** *Given* the corrected `test-configuration.json` files, *when* PostgreSQL and
  AWS SQS configs are inspected, *then* they no longer carry mis-declared
  `HasSupportToDelayedMessages`/`HasSupportToDeadLetterQueue` values (the keys are removed).
- **AC-12 (FR-12, conditional on fix-in-place).** *If* the design retains and fixes the
  `with_delay` template, *then* the corrected template passes a non-null `TimeSpan` delay to
  `Requeue` and includes a bounded receive-retry loop, and the generated test fails if delayed
  requeue does not redeliver the message. *If* the design replaces it with FR-2, *then* the
  template is deleted and a template-source inspection confirms no remaining messaging-gateway
  template calls `Requeue` / `RequeueAsync` without a `TimeSpan` delay argument.
- **AC-13 (FR-13).** *Given* a full generation run, *when* the suite is generated for every
  transport including GCP and Azure/ASB, *then* the canonical tests are present for each and none
  is skipped or gated away; and for every transport the generated suite either compiles-and-passes
  or the non-conformance is fixed in the transport gateway or captured as a named, linked
  follow-up issue with explicit maintainer sign-off — no silent skip and no unaudited deferral.
- **AC-14 (FR-14).** *Given* the generated output, *when* a transport that supports both a sync
  and an async channel is generated, *then* each canonical behaviour appears in both a Reactor
  variant driving `IAmAChannelSync` and a Proactor variant driving `IAmAChannelAsync`, and neither
  variant drives a `Reactor`/`Proactor` pump.
- **AC-15 (NFR-1).** *Given* the generated files, *when* their names are inspected, *then* they
  match the established `When_rejecting_message_...` / `When_..._requeues_with_delay_...`
  conventions.
- **AC-16 (FR-15).** *Given* a received message, *when* `channel.Requeue(message, TimeSpan.Zero)`
  (or `Requeue(message, null)`) is called, *then* `Requeue` returns `true`, the message is
  receivable again within the plain-requeue bounded retry loop with no delay window elapsing
  (observable outcome only; per NFR-3, scheduler non-engagement is not asserted as a mechanism).
- **AC-17 (FR-16).** *Given* a received message `M`, *when* `channel.Nack(M)` (or `NackAsync`) is
  called, *then* a subsequent receive within the bounded retry loop yields a message with `M`'s id
  and body; and, given a second queued message `M2`, after nacking `M` the redelivered `M` is
  received and then `M2` is received (pipeline not blocked).
- **AC-18 (FR-17).** *Given* a channel with a dead-letter routing key, *when*
  `channel.Reject(M, new MessageRejectionReason(RejectionReason.None, "..."))` is called, *then* the
  DLQ consumer receives the message with the transport's rejection-reason key == `"None"` and
  original-topic key == the data topic, and `M` does **not** appear on the invalid channel.

## Coverage Reconciliation (Kafka surface area)

The canonical set was reconciled against the richest existing hand-written suite —
`tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/` (Reactor + Proactor) — so the generic
suite matches its *behavioural* surface for the reject/requeue/Nack/delay pathways rather than
covering each path with a single thin test. Mapping:

| Kafka hand-written test (Reactor + `_async`)                                   | Canonical FR |
|--------------------------------------------------------------------------------|--------------|
| `..._requeues_with_delay_should_use_producer`                                  | FR-2 (as mechanism-agnostic delayed redelivery) |
| `..._requeues_with_delay_should_use_scheduler`                                 | — (FR-3 withdrawn; mechanism assertion, OOS-2 supplementary) |
| `..._delivery_error_should_send_to_dlq`                                        | FR-4 |
| `..._unacceptable_reason_should_send_to_invalid_channel`                       | FR-5 |
| `..._unacceptable_and_no_invalid_channel_should_fallback_to_dlq`               | FR-6 |
| `..._no_channels_configured_should_acknowledge_and_log`                        | FR-7 |
| `..._rejecting_message_should_include_metadata`                                | FR-8 |
| `..._unknown_reason_should_send_to_dlq` (rejects with `RejectionReason.None`)  | FR-17 (added) |
| `When_nacking_a_message_it_should_be_redelivered` (+ two-message variant)      | FR-16 (added) |
| plain requeue / delayed send                                                   | existing plain-requeue template (ungated by FR-10) / FR-9 |

**Gaps in Kafka's own coverage that the generated suite closes via FR-14 parity:** Kafka's metadata
test exists only in the Reactor variant. Because FR-14 mandates both a sync and an async variant of
every canonical behaviour, the generated suite closes that gap. *(Kafka also lacks an async
requeue-via-scheduler test; the generated suite does **not** close that one, since FR-3 is withdrawn
and scheduler delegation is now OOS-2 supplementary work.)*

**Deliberately excluded (OOS-3)** as transport-internal rather than universal channel behaviour:
offset commit/sweep/revoke, topic declaration, Brighter↔Kafka header conversion and byte/UTF-8
round-tripping, partition-key handling, consumer-config hooks, fatal/non-fatal error escalation,
producer not-persisted/confirmation handling, requeue-producer disposal, and the
`Nack`-without-offset edge. Also excluded are the **wiring unit tests** — `..._pass_routing_keys`,
`..._subscription_with_dead_letter/invalid_message_routing_key_should_expose_property`,
`..._channel_factory_forwards_scheduler_to_consumers`, `..._consumer_factory_..._pass_scheduler` —
because the routing-key plumbing is exercised transitively by the end-to-end canonical tests (a
subscription that drops the DLQ routing key fails FR-4). **The scheduler-forwarding wiring tests are
a different case**: with FR-3 withdrawn, nothing in the generic suite proves scheduler forwarding
transitively, so those two remain excluded not because they are redundant but because scheduler
delegation is now OOS-2 supplementary work for the six gateways that expose the seam. If a transport
should assert this wiring in isolation, that is a candidate follow-up, not part of the generic
suite.

## Additional Context

Grounding read during drafting (all paths relative to the worktree root
`/Users/ian.cooper/CSharpProjects/github/BrighterCommand/generator-transport-tests`):

- Generator gating: `tools/Paramore.Brighter.Test.Generator/Generators/MessagingGatewayGenerator.cs`
  (`SkipTest` gates `delayed_message`/`with_delay` on `HasSupportToDelayedMessages`,
  `dead_letter_queue` on `HasSupportToDeadLetterQueue`, and `requeuing` on
  `HasSupportToRequeue` — note the `with_delay` filename matches both `requeuing` and
  `with_delay`, so today it is doubly gated).
- Flags: `tools/Paramore.Brighter.Test.Generator/Configuration/MessagingGatewayConfiguration.cs`.
- Provider interface templates:
  `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/{Reactor,Proactor}/IAmAMessageGateway*Provider.cs.liquid`
  (today expose `CreateSubscription(..., bool setupDeadLetterQueue = false)`,
  `CreateChannel`, `CreateProducer`, and `GetMessageFromDeadLetterQueue` — no invalid-message
  routing key, no invalid-channel read, no producer-scheduler wiring, no metadata key names).
- Broken template confirmed: `.../Reactor/When_requeuing_a_failed_message_with_delay_should_receive_message_again.cs.liquid`
  calls `_channel.Requeue(received);` (no delay) after a `SendWithDelay` + `Thread.Sleep(6s)`.
- Config discrepancies confirmed in `tests/Paramore.Brighter.*.Tests/test-configuration.json`:
  PostgreSQL `HasSupportToDeadLetterQueue:false`/`HasSupportToDelayedMessages:false`; AWS
  `HasSupportToDelayedMessages:false` in three of its four configurations (`SqsStandard` declares
  `true`, in both V3 and V4); Kafka all three gates `false` in both Standard and PartitionKey;
  MSSQL `HasSupportToDeadLetterQueue:false` despite ADR 0040's Brighter-managed DLQ;
  AWS `SqsStandard` and RocketMQ the only `HasSupportToDelayedMessages:true`.
- Canonical hand-written behaviours (Kafka Reactor folder):
  `When_kafka_consumer_requeues_with_delay_should_use_producer.cs`,
  `..._should_use_scheduler.cs` (uses a `SpySchedulerSync` exposing `ScheduleCalled`/`ScheduledDelay`),
  `When_rejecting_message_with_delivery_error_should_send_to_dlq.cs`,
  `..._unacceptable_reason_should_send_to_invalid_channel.cs`,
  `..._unacceptable_and_no_invalid_channel_should_fallback_to_dlq.cs`,
  `..._no_channels_configured_should_acknowledge_and_log.cs`,
  `When_rejecting_message_should_include_metadata.cs`; plus
  `tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/Reactor/When_rejecting_a_message_to_a_dead_letter_queue.cs`.
  Note: these grounding tests drive the raw consumer surface (`_consumer.Reject` / `_consumer.Requeue`);
  the canonical templates re-express the same behaviour at the channel surface
  (`IAmAChannelSync` / `IAmAChannelAsync`) per the Objective and Test Boundary, so the generated
  tests adapt rather than copy them verbatim.
- Metadata key divergence confirmed (semantic set identical, names differ):
  Kafka `src/Paramore.Brighter.MessagingGateway.Kafka/HeaderNames.cs` — `OriginalTopic`,
  `OriginalType`, `RejectionReason`, `RejectionMessage`, `RejectionTimestamp` (PascalCase);
  Redis `RedisMessageConsumer.cs:596-604` and SQS `SqsMessageConsumer.cs:499-511` — `originalTopic`,
  `originalMessageType`, `rejectionReason`, `rejectionMessage`, `rejectionTimestamp` (camelCase).
- Runtime contract: `src/Paramore.Brighter/MessageRejectionReason.cs`
  (enum `RejectionReason { None, Unacceptable, DeliveryError }` + record `MessageRejectionReason`),
  `src/Paramore.Brighter/IAmAChannelSync.cs` (`Reject`, `Requeue(Message, TimeSpan?)`, `Nack`),
  `src/Paramore.Brighter/IAmAChannelAsync.cs` (async equivalents),
  `src/Paramore.Brighter/IAmAMessageProducer.cs` (`IAmAMessageScheduler? Scheduler { get; set; }`),
  `src/Paramore.Brighter/InMemoryScheduler.cs` (ctor takes `IAmACommandProcessor`, `TimeProvider`).

Design decisions deferred to the ADR (`docs/adr/`) — **all now resolved**: the exact shape of the
provider-interface extension (metadata-key member, producer-with-scheduler factory, invalid-channel
read) → ADR 0066; whether FR-12's template is fixed-in-place or replaced → **replaced/deleted**, ADR
0066; whether FR-3 uses an in-memory scheduler or a spy → **neither; FR-3 is withdrawn as a
mechanism assertion and folded into FR-2**, ADR 0066; and how per-transport conformance fixes
(FR-13) are sequenced → **fix-then-flip with a conformance ledger**, ADR 0067.
