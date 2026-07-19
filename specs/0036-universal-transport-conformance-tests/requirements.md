# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and
> implementation details are documented in the Architecture Decision Records in `docs/adr/` —
> here, ADR 0066 (provider interface and ungating) and ADR 0067 (rollout and deferral governance).

**Linked Issue**: #4240

## Problem Statement

As a **Brighter maintainer / transport contributor**, I want the messaging-gateway test generator
to own and generate — for *every* transport — the conformance tests for the
Reject / dead-letter / invalid-message / requeue-with-delay / delayed-send / Nack behaviours that
Brighter guarantees universally, so that I can add or change a transport and have the generated
suite prove the transport honours those obligations.

Three things prevent that today:

1. **Universal obligations are gated as if they were opt-in capabilities.** The generator skips
   templates on `HasSupportToDelayedMessages`, `HasSupportToDeadLetterQueue`, and
   `HasSupportToRequeue`. But `Reject`, `Requeue`, and `Nack` are on the consumer and channel
   interfaces for every transport, and where a transport lacks a native delay or a native DLQ,
   Brighter supplies the behaviour itself. These are conformance obligations, not capabilities.

2. **The gate values are mis-declared against the code.** Kafka declares all three gates `false`
   despite its hand-written suite being the canonical grounding for these behaviours; PostgreSQL
   declares no DLQ and no delayed messages despite having both; MSSQL declares no DLQ despite
   ADR 0040. Only AWS `SqsStandard` and RocketMQ declare `HasSupportToDelayedMessages: true`, so
   the delayed-delivery test runs for three of roughly twenty gateway configurations.

3. **Coverage is inconsistent and one template is defective.** The reject / DLQ / invalid-channel /
   delayed-requeue features post-date the generator, so contributors hand-wrote them per transport
   (Kafka and RMQ.Sync have rich suites; GCP has none). The generated `with_delay` template calls
   `Requeue` with no delay argument, so it does not exercise delayed requeue at all.

## Proposed Solution

Make the canonical Reject / DLQ / invalid-channel / requeue-with-delay / delayed-send / Nack
behaviours a **universal, ungated** part of the generated messaging-gateway suite, produced
identically for every transport in both sync (Reactor) and async (Proactor) variants. Retire the
three opt-in gates and remove them from every per-transport configuration.

The suite proves *that* a behaviour holds — delayed send works, requeue-with-delay redelivers,
`Reject` routes per the fallback ladder, rejection metadata is stamped — **regardless of whether
the transport achieves it natively or through Brighter's own fallback**. The tests never assert
*how*.

Where generating an ungated template surfaces that a transport does not conform, that
non-conformance is a **defect in that transport's gateway**, and fixing it is part of this work
(see FR-13).

## Objective and Test Boundary

The suite proves that **each transport's gateway** implements reject / requeue / delay correctly.
It does **not** re-prove Reactor/Proactor message-pump orchestration — that is owned by Brighter's
core in-memory tests and must not be duplicated here.

- **Unit under test.** The transport's **channel** and **producer** surfaces directly:
  `IAmAChannelSync` / `IAmAChannelAsync` (`Receive`, `Reject`, `Requeue`, `Nack`, `Acknowledge`)
  and `IAmAMessageProducerSync` / `IAmAMessageProducerAsync` (`Send`, `SendWithDelay`).
- **Both sync and async are exercised**, because they are genuinely distinct code paths. That —
  not pump mechanics — is what the Reactor and Proactor variants mean in this suite (FR-14).
- **Right-sized assertions.** Each test proves the observable outcome (message redelivered,
  message on DLQ, message on invalid channel, metadata present), not the internal mechanism.

## Requirements

Terminology:

- **Channel / ChannelAsync** — `IAmAChannelSync` / `IAmAChannelAsync`, the surface a test drives.
- **DLQ** — the dead-letter channel a rejected message is routed to; identified by a *dead-letter
  routing key* on the subscription.
- **Invalid channel** — the channel an `Unacceptable` rejection is routed to; identified by an
  *invalid-message routing key* on the subscription.
- **Fallback ladder** — the routing order Brighter applies to a rejected message: `Unacceptable` →
  invalid channel if configured, else DLQ; `DeliveryError` → DLQ; `None` / unspecified → DLQ.
- **Ungated** — the template is generated for every transport, regardless of any capability flag.
- **Canonical behaviours** — FR-2, FR-4 … FR-9, FR-15, FR-16, FR-17.
- **Rejection-metadata key names** — the per-transport `Header.Bag` key strings under which
  rejection metadata is stamped. The *semantic set* is universal (FR-8); only the names and casing
  vary per transport.

### Functional Requirements

**FR-1 — Provider-interface extension.**
The generated provider interfaces `IAmAMessageGatewayReactorProvider` and
`IAmAMessageGatewayProactorProvider` MUST be extended so that a generated test can:

- **FR-1(1)** — create a subscription/channel configured with **both** a dead-letter routing key
  and an invalid-message routing key;
- **FR-1(2)** — create a channel configured with a DLQ only, with an invalid channel only, and with
  neither (to drive FR-6 and FR-7);
- **FR-1(3)** — read a message from the invalid channel, as an analogue of the existing
  `GetMessageFromDeadLetterQueue`;
- **FR-1(5)** — obtain the transport's **rejection-metadata key names**, so a generated test
  asserts the universal semantic set (FR-8) without hard-coding any one transport's key strings.
- **FR-1(6)** — the existing `bool setupDeadLetterQueue` parameter MUST be **removed** from
  `CreateSubscription`, superseded by the routing-key parameters of FR-1(1); a boolean cannot
  express the DLQ-only / invalid-only / neither combinations FR-1(2) requires. This is a breaking
  change: all twenty existing `*MessageGatewayProvider.cs` implementations, both
  `IAmAMessageGateway*Provider.cs.liquid` interface templates, and every generated caller migrate in
  the same change. Providers newly written under FR-20 implement the post-FR-1 signature directly
  and never carry the bool.

Both dead-letter and invalid-channel reads MUST be bounded and non-throwing: they return
`MessageType.MT_NONE` when the channel is empty *or* when the subscription does not configure that
channel, so the negative assertions in AC-5 and AC-18 are expressible in a single template.

*Example:* `CreateSubscription(routingKey, channelName, OnMissingChannel.Create,
deadLetterRoutingKey: "orders.dlq", invalidMessageRoutingKey: "orders.invalid")` returns a
subscription from which the provider builds a channel whose `Reject` routes per the fallback ladder;
passing only one of the two keys yields the DLQ-only and invalid-only channels of FR-1(2), and
passing neither yields the FR-7 channel. For FR-1(5),
`provider.RejectionMetadataKeys.OriginalTopic` returns `"OriginalTopic"` for Kafka and
`"originalTopic"` for Redis and SQS — the divergence is PascalCase versus camelCase per transport.

**FR-2 — Requeue with delay redelivers after the delay.**
Generate a test proving that when a channel requeues a received message with a non-zero delay, the
message is redelivered after that delay. The test asserts the observable outcome only; it makes no
assertion about which mechanism the gateway used to achieve the delay.

*Example:* send `M`; receive `M`; call `channel.Requeue(M, TimeSpan.FromSeconds(5))`; assert
`Requeue` returns `true` and a subsequent receive, within a bounded retry loop, yields a message
whose body equals `M`'s.

**FR-4 — Reject with delivery error routes to the DLQ.**
Generate a test proving that
`channel.Reject(M, new MessageRejectionReason(RejectionReason.DeliveryError, "..."))` on a channel
configured with a dead-letter routing key causes `M` to appear on the DLQ, carrying the transport's
original-topic key (equal to the data topic) and a rejection-reason entry.

**FR-5 — Reject with unacceptable reason routes to the invalid channel.**
Generate a test proving that
`channel.Reject(M, new MessageRejectionReason(RejectionReason.Unacceptable, "..."))` on a channel
configured with an invalid-message routing key causes `M` to appear on the invalid channel — with
rejection reason `"Unacceptable"` and original-topic equal to the data topic — and **not** on the
DLQ.

**FR-6 — Fallback ladder: unacceptable with no invalid channel falls back to the DLQ.**
Generate a test proving that when a channel is configured with a DLQ only and a message is rejected
with `RejectionReason.Unacceptable`, the message is routed to the DLQ, with its rejection-reason
key still equal to `"Unacceptable"`.

**FR-7 — No channels configured: acknowledge and log.**
Generate a test proving that when a channel has neither a DLQ nor an invalid channel configured,
`channel.Reject(M, ...)` returns `true` — the message is removed rather than redelivered — and the
channel goes on to receive the next message without blocking. The asserted outcome is
acknowledge-and-continue; the `_and_log` suffix in the mandated test name (NFR-1) is retained for
continuity with the hand-written tests, and logging itself is not asserted.

*Example:* send `M1` then `M2`; receive `M1`; `Reject(M1, DeliveryError)` returns `true`; the next
receive yields `M2`.

**FR-8 — Rejection metadata stamping.**
Generate a test proving that a rejected message routed to the DLQ or invalid channel carries the
universal rejection-metadata **semantic set** in `Header.Bag`, read via the per-transport key names
the provider exposes (FR-1(5)). The semantic set is: **original topic** (equal to the data topic),
**original message type**, **rejection reason**, **rejection message** (equal to the description
passed to `Reject`), and **rejection timestamp** (a parseable ISO-8601 `DateTimeOffset`). A
transport that emits the set under its own key names conforms; one that omits a semantic field does
not, and is handled under FR-13.

**FR-9 — Delayed send.**
Generate a test proving that a message sent with `producer.SendWithDelay(M, delay)` is not
receivable before the delay elapses and is receivable after it. The existing
`When_reading_a_delayed_message_via_the_messaging_gateway_should_delay_delivery.cs.liquid` template
already asserts this and satisfies FR-9 once FR-10 ungates it; no new canonical template is
required, and writing one would duplicate coverage.

**FR-10 — Retire the three opt-in gates.**
`SkipTest` in `MessagingGatewayGenerator` MUST no longer skip any template on
`HasSupportToDelayedMessages`, `HasSupportToDeadLetterQueue`, or `HasSupportToRequeue`. All three
properties are removed from `MessagingGatewayConfiguration`. The existing plain-requeue template
becomes ungated and generates for every transport alongside the canonical templates. No replacement
`HasNative*` flag is introduced (see OOS-1).

**FR-11 — Remove the gate keys from every per-transport configuration.**
The three keys MUST be removed from every `test-configuration.json`, rather than left carrying
misleading values.

**FR-12 — Remove the defective delayed-requeue template.**
The `When_requeuing_a_failed_message_with_delay_should_receive_message_again` template calls
`Requeue` with no `timeout` argument, so it never exercises delayed requeue, and it lacks the
bounded retry loop the plain-requeue template has. It MUST be deleted, superseded by FR-2. After
deletion, no messaging-gateway template that purports to exercise **delayed** requeue may call
`Requeue` or `RequeueAsync` without a non-null `TimeSpan`.

This prohibition is deliberately scoped to delayed-requeue templates. Two templates legitimately
call `Requeue` with no delay, or with an explicitly null one, and are unaffected: the plain-requeue
template ungated by FR-10, and the zero/null-boundary template required by FR-15.

**FR-13 — Generate for every targeted configuration; non-conformance is a defect to fix.**
A **targeted gateway configuration** is one declared under the `MessagingGateway` (singular, one
A **targeted transport** is **every transport with a messaging gateway** — that is, every
`src/Paramore.Brighter.MessagingGateway.*` project. There are twelve: AWSSQS, AWSSQS.V4,
AzureServiceBus, GcpPubSub, Kafka, MQTT, MsSql, Postgres, Redis, RMQ.Async, RMQ.Sync, RocketMQ.
Membership is having a messaging gateway; generator wiring is not a criterion.

Nine of the twelve are wired today, declaring twenty gateway configurations between them via the
`MessagingGateway` (singular) or `MessagingGateways` (plural) section of a
`tests/Paramore.Brighter.*.Tests/test-configuration.json`: AWS, AWS.V4, GCP, Kafka, MSSQL,
PostgreSQL, Redis, RMQ.Async, RocketMQ. (Fourteen such files exist; five — DynamoDB, DynamoDB.V4,
MongoDb, MySQL, Sqlite — declare only `Outbox`/`Outboxes` and have no gateway at all, so they are
not transports for this purpose.) The remaining three gateways — **AzureServiceBus, MQTT,
RMQ.Sync** — are targeted transports that FR-20 brings into the generator.

The canonical templates MUST be generated for **every** configuration of **every** targeted
transport — generation is not optional and no transport is excluded. Where the generated suite
fails to compile or fails at runtime because a transport does not honour a universal obligation,
that is a **defect in that transport's gateway and is in scope to fix**. No canonical test may be
silently skipped or gated away to make the suite green. Where a specific fix is deferred, the
deferral MUST be recorded as a named, linked follow-up issue with explicit maintainer sign-off,
auditable from this spec — never an open-ended escape hatch.

**FR-14 — Sync (Reactor) and async (Proactor) parity.**
Every canonical template MUST be produced in both a Reactor variant driving `IAmAChannelSync` and
`IAmAMessageProducerSync`, and a Proactor variant driving `IAmAChannelAsync` and
`IAmAMessageProducerAsync`. Neither variant drives a message pump.

**FR-15 — Zero or null delay requeue does not delay.**
Generate (or extend a canonical test to assert) that `channel.Requeue(M, TimeSpan.Zero)` and
`channel.Requeue(M, null)` behave as an immediate plain requeue: the message is receivable again
without a delay window elapsing. This pins the lower boundary of the delay parameter so the
positive-delay path (FR-2) is not conflated with the no-op path.

**FR-16 — Nack releases the message for redelivery.**
Generate a test proving that `channel.Nack(M)` / `NackAsync(M)` releases a received message back to
the transport so it is redelivered on a subsequent receive — as distinct from `Acknowledge`, which
removes it, and `Reject`, which routes it. A second variant, with two queued messages, proves the
nacked message is redelivered and the following message is not blocked behind it.

**FR-17 — Reject with `None` or unspecified reason routes to the DLQ.**
Generate a test proving that `channel.Reject(M, new MessageRejectionReason(RejectionReason.None,
"..."))` on a channel configured with a dead-letter routing key routes `M` to the DLQ — the default
arm of the fallback ladder — and not to the invalid channel. This is a distinct routing rule from
FR-4 and FR-5.

**FR-19 — Remove the requeue-count-exhaustion template.**
`When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue.cs.liquid`
(Reactor and Proactor) MUST be deleted. Like FR-12's `with_delay` template it is unsalvageable,
though for a different reason: it does not assert a channel-surface obligation at all.

Requeue-count exhaustion is enforced by the **message pump** — `Reactor.RequeueMessage` /
`Proactor.RequeueMessage` call `Header.UpdateHandledCount()` and then
`Message.HandledCountReached(RequeueCount)`, and `HandledCountReached` has no other caller in the
codebase. `Channel.Requeue` / `ChannelAsync.RequeueAsync` forward straight to
`_messageConsumer.Requeue` and count nothing. So at the channel surface the template can only pass
where the *transport* natively counts deliveries and redrives — as SQS does, which is why the AWS
provider pairs `requeueCount: 3` with `redrivePolicy: new RedrivePolicy(dlqName, 3)`.

That makes the template a pump test (excluded by OOS-5) or a native-mechanism test (excluded by
NFR-3 and OOS-1). Retiring the gates would otherwise ungate it onto Kafka ×2, MSSQL and PostgreSQL,
none of which have a delivery counter, producing four failures that FR-13 would misclassify as
gateway defects.

Note this template is the **only** current caller of `CreateSubscription`'s `bool
setupDeadLetterQueue`; deleting it does not remove the obligation to drop that parameter, which
FR-1(6) carries.

**FR-20 — Onboard the three unwired gateway transports.**
`AzureServiceBus`, `MQTT` and `RMQ.Sync` have messaging gateways and test projects but no generator
wiring, so they generate nothing today. Each MUST be brought into the generator so the canonical
templates are produced for it like any other transport. For each:

1. add a `test-configuration.json` declaring its gateway configuration(s) under `MessagingGateway`
   or `MessagingGateways`;
2. implement `IAmAMessageGatewayReactorProvider` and/or `IAmAMessageGatewayProactorProvider` against
   the FR-1 surface — including the routing-key parameters of FR-1(1), the invalid-channel read of
   FR-1(3) and the metadata key names of FR-1(5);
3. supply whatever test infrastructure the transport needs to run in CI.

Existing hand-written gateway coverage these onboardings build on: RMQ.Sync 31 tests, MQTT 18,
AzureServiceBus 8. MQTT has its own dead-letter ADR (`0043-mqtt-dlq-brighter-managed`); MQTT and
RMQ.Sync both implement `IAmAChannelFactoryWithScheduler`.

An onboarding that cannot be completed within this spec is governed by FR-13's deferral rule: a
named, linked follow-up issue with explicit maintainer sign-off, recorded in the conformance ledger.
No transport is dropped from the target set.

### Non-functional Requirements

- **NFR-1 (Consistency).** Generated tests MUST follow the naming convention established by the
  hand-written tests, e.g. `When_rejecting_message_with_delivery_error_should_send_to_dlq`,
  `When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel`,
  `When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log`.
- **NFR-2 (Determinism).** Timing-dependent tests MUST use bounded receive-retry loops rather than
  a single receive after a fixed sleep, so broker propagation delays do not cause false failures.
- **NFR-3 (No mechanism assertions).** The suite MUST NOT assert *how* a behaviour is achieved —
  native versus Brighter fallback. It asserts only that the observable behaviour holds, and only
  against the channel and producer surfaces. The related prohibition on reintroducing capability
  gates lives in FR-10 and OOS-1.

### Constraints and Assumptions

- **C-1.** Work is confined to the generator (`tools/Paramore.Brighter.Test.Generator`), its
  templates and per-transport `test-configuration.json` files; the transport-gateway fixes FR-13
  requires; and the test-side onboarding FR-20 requires (new configs, new provider implementations,
  and the CI infrastructure to run them). No public Brighter runtime API is redesigned beyond what
  FR-1 requires of the *generated* provider interfaces — in particular, FR-20 wires existing
  gateways into the generator; it does not modify them except where FR-13 applies.
- **C-2.** The runtime contract the templates target already exists in `src/Paramore.Brighter`:
  `Reject(Message, MessageRejectionReason?)`, `SendWithDelay`, `Requeue(Message, TimeSpan?)`,
  `Nack`, the `RejectionReason` enum and `MessageRejectionReason` record, and the sync/async channel
  and producer interfaces. **The rejection-metadata key names are not a shared core type** — they
  are defined per transport, which is why the provider must expose them (FR-1(5), FR-8).
- **C-3.** Assumption: the fallback-ladder and metadata semantics observed in the hand-written
  transport tests are canonical Brighter behaviour and apply to every transport. Any gateway that
  diverges is treated under FR-13.
- **C-4.** Assumption: transports that provision DLQ and invalid channels via
  `OnMissingChannel.Create` can do so within the test's bounded wait; where a transport needs
  explicit provisioning, the provider template supplies it.

### Out of Scope

- **OOS-1.** Re-introducing any `HasNative*` capability flag into the suite. A native/non-native
  distinction in these conformance tests is explicitly rejected: we test *supported*, not *how*.
- **OOS-2.** Supplementary per-transport tests that prove a *specific mechanism* works — native
  redrive policies, native DLX, native delay columns, and scheduler delegation for the six gateways
  that implement `IAmAChannelFactoryWithScheduler`. These are candidate follow-up work, recorded as
  a task in tasks.md rather than as an obligation of this spec.
- **OOS-3.** Transport-*internal* mechanics: offset commit and sweep, header byte round-tripping,
  partition-key handling, fatal/non-fatal error escalation, producer-not-persisted synthesis,
  requeue-producer disposal, and factory/subscription wiring unit tests. Routing-key plumbing is
  proven transitively by the end-to-end canonical tests; the remainder is transport-specific.
- **OOS-4.** Sibling defects #4238 (single `Outbox` async-only) and #4239 (`CollectionName` ignored
  by sync outbox templates).
- **OOS-5.** Driving any canonical behaviour through the Reactor or Proactor message pump.

## Acceptance Criteria

- **AC-1 (FR-1).** *Given* a transport's generated provider, *when* a test creates a subscription
  with a dead-letter routing key and an invalid-message routing key, *then* it obtains a channel
  that routes rejections per the fallback ladder; and the provider can create channels configured
  with a DLQ only, an invalid channel only, and neither, can read from both the DLQ and the invalid
  channel, and exposes the transport's rejection-metadata key names; and *when* the provider
  interfaces are inspected, *then* `CreateSubscription` no longer declares a `bool
  setupDeadLetterQueue` parameter, and no provider implementation or generated caller references it.
- **AC-2 (FR-2).** *Given* a received message on **any** target configuration, *when*
  `channel.Requeue(message, 5s)` is called, *then* `Requeue` returns `true` and a later receive
  within the bounded retry loop yields a message with the same body — with no assertion about how
  the delay was achieved.
- **AC-4 (FR-4).** *Given* a channel with a dead-letter routing key, *when*
  `channel.Reject(message, DeliveryError)` is called, *then* the DLQ consumer receives the message
  with original-topic equal to the data topic and a rejection-reason entry present.
- **AC-5 (FR-5).** *Given* a channel with an invalid-message routing key, *when*
  `channel.Reject(message, Unacceptable)` is called, *then* the invalid-channel consumer receives
  the message with rejection reason `"Unacceptable"`, and it does **not** appear on any DLQ.
- **AC-6 (FR-6).** *Given* a channel with a DLQ but no invalid channel, *when*
  `channel.Reject(message, Unacceptable)` is called, *then* the DLQ consumer receives the message
  with rejection reason `"Unacceptable"`.
- **AC-7 (FR-7).** *Given* a channel with neither DLQ nor invalid channel and two queued messages
  `M1` and `M2`, *when* `channel.Reject(M1, DeliveryError)` is called, *then* it returns `true` and
  the next receive yields `M2`.
- **AC-8 (FR-8).** *Given* a rejected message on the DLQ, *when* its header bag is inspected using
  the provider-supplied key names, *then* the universal semantic set is present and correct:
  original topic equal to the data topic, original message type equal to `"MT_COMMAND"` (the test
  message being command-typed), rejection reason equal to `"DeliveryError"` (for the FR-4 arm),
  rejection message equal to the description passed to `Reject`, and a parseable ISO-8601 rejection
  timestamp within the last minute.
- **AC-9 (FR-9).** *Given* `producer.SendWithDelay(message, 5s)`, *when* a receive is attempted
  immediately, *then* it yields `MT_NONE`; *when* a receive is attempted after the delay, *then* it
  yields the message.
- **AC-10 (FR-10).** *Given* the generator source, *when* `SkipTest` is inspected, *then* it
  contains no branch keyed on any of the three gates, and the three properties are absent from
  `MessagingGatewayConfiguration`.
- **AC-11 (FR-11).** *Given* the per-transport configurations, *when* they are inspected, *then*
  none contains `HasSupportToDelayedMessages`, `HasSupportToDeadLetterQueue`, or
  `HasSupportToRequeue`.
- **AC-12 (FR-12).** *Given* the template source, *when* it is inspected, *then* the defective
  `with_delay` template is absent, **and no generated copy of it remains under any
  `tests/Paramore.Brighter.*.Tests/**/Generated/` directory** (six such copies exist today, and
  deleting a template does not delete them), and every remaining messaging-gateway template that
  exercises delayed requeue passes a non-null `TimeSpan` to `Requeue`/`RequeueAsync`. The plain-requeue
  template (FR-10) and the zero/null-boundary template (FR-15) are expected to call `Requeue` with
  no delay or an explicitly null one, and do not violate this.
- **AC-13 (FR-13).** *Given* a full generation run, *when* the suite is generated, *then* every
  configuration of all **twelve** gateway transports — including AzureServiceBus, MQTT and RMQ.Sync
  once FR-20 wires them — has the canonical tests present, and none is skipped or gated away;
  and for each configuration the generated suite either compiles and passes, or the non-conformance
  is fixed in the gateway, or it is captured as a named, linked follow-up issue with explicit
  maintainer sign-off — no silent skip, no unaudited deferral.
- **AC-14 (FR-14).** *Given* the generated output, *when* a configuration supporting both sync and
  async channels is generated, *then* each canonical behaviour appears in both a Reactor variant
  driving `IAmAChannelSync` and a Proactor variant driving `IAmAChannelAsync`, and neither drives a
  pump.
- **AC-15 (NFR-1).** *Given* the generated files, *when* their names are inspected, *then* they
  match the established `When_...` conventions.
- **AC-16 (FR-15).** *Given* a received message, *when* `channel.Requeue(message, TimeSpan.Zero)`
  or `Requeue(message, null)` is called, *then* `Requeue` returns `true` and the message is
  receivable again within the plain-requeue bounded retry loop, with no delay window elapsing.
- **AC-17 (FR-16).** *Given* a received message `M`, *when* `channel.Nack(M)` or `NackAsync(M)` is
  called, *then* a subsequent receive within the bounded retry loop yields a message with `M`'s id
  and body; and, given a second queued message `M2`, after nacking `M` the redelivered `M` is
  received and then `M2` is received.
- **AC-18 (FR-17).** *Given* a channel with a dead-letter routing key, *when*
  `channel.Reject(M, new MessageRejectionReason(RejectionReason.None, "..."))` is called, *then* the
  DLQ consumer receives the message with rejection reason `"None"` and original-topic equal to the
  data topic, and `M` does **not** appear on the invalid channel.
- **AC-20 (NFR-2).** *Given* the generated messaging-gateway templates, *when* their sources are
  inspected, *then* every assertion that a message **arrives** sits inside a bounded receive-retry
  loop, and none takes the form of a fixed sleep followed by a single unretried receive.

  The exemption is per *assertion*, not per AC — several ACs contain one of each. Exempt are only:
  AC-5's "does not appear on any DLQ", AC-9's "a receive attempted immediately yields `MT_NONE`",
  AC-16's "no delay window elapsing", and AC-18's "does not appear on the invalid channel". Each of
  those uses a single bounded receive after the stated window, because retrying until arrival would
  invert them. The **positive** half of the same criteria — AC-5's and AC-18's DLQ/invalid-channel
  arrivals, AC-9's receive after the delay, AC-16's "receivable again within the plain-requeue
  bounded retry loop" — stays inside a bounded retry loop like every other arrival.
- **AC-21 (NFR-3).** *Given* the generated messaging-gateway templates, *when* their sources are
  inspected, *then* no assertion references a scheduler, a native-delay API, a redrive policy, a
  DLX, or any other transport-specific mechanism — every assertion is on an observable outcome
  reached through the channel or producer surface.
- **AC-22 (FR-19).** *Given* the template source, *when* it is inspected, *then*
  `When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue.cs.liquid` is absent from
  both the Reactor and Proactor template directories, **and no generated copy of it remains under any
  `tests/Paramore.Brighter.*.Tests/**/Generated/` directory** (thirty-two such copies exist today).
- **AC-23 (FR-20).** *Given* the repository, *when* `src/Paramore.Brighter.MessagingGateway.*` is
  enumerated, *then* each of the twelve gateway projects has a corresponding test project declaring
  a `MessagingGateway`/`MessagingGateways` section and at least one provider implementation; and
  *when* generation runs, *then* AzureServiceBus, MQTT and RMQ.Sync each emit the canonical
  templates. Any transport not meeting this has a named, linked, signed-off deferral issue and a
  conformance-ledger row — never silent absence from the target set.

## Coverage Reconciliation (Kafka reference surface)

The canonical set was reconciled against the richest existing hand-written suite,
`tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/` (Reactor and Proactor), so the generated
suite matches its *behavioural* surface for the reject / requeue / Nack / delay pathways:

| Kafka hand-written test (Reactor + `_async`)                     | Canonical requirement |
|------------------------------------------------------------------|-----------------------|
| `..._requeues_with_delay_should_use_producer`                     | FR-2 |
| `..._requeues_with_delay_should_use_scheduler`                    | — (mechanism assertion; OOS-2) |
| `..._delivery_error_should_send_to_dlq`                           | FR-4 |
| `..._unacceptable_reason_should_send_to_invalid_channel`          | FR-5 |
| `..._unacceptable_and_no_invalid_channel_should_fallback_to_dlq`  | FR-6 |
| `..._no_channels_configured_should_acknowledge_and_log`           | FR-7 |
| `..._rejecting_message_should_include_metadata`                   | FR-8 |
| `..._unknown_reason_should_send_to_dlq`                           | FR-17 |
| `When_nacking_a_message_it_should_be_redelivered` (+ two-message) | FR-16 |
| plain requeue / delayed send                                      | ungated plain-requeue template (FR-10) / FR-9 |

Kafka's metadata test exists only in the Reactor variant; FR-14 parity closes that gap in the
generated suite. The remaining Kafka tests are excluded under OOS-3 as transport-internal.

Note: the hand-written grounding tests drive the raw consumer surface; the canonical templates
re-express the same behaviour at the channel surface per the Objective and Test Boundary, so they
adapt rather than copy.

---

*The numbering contains deliberate gaps. **FR-1(4)**, **FR-3**, **FR-18**, **NFR-4**, **AC-3**,
**AC-19** and **OOS-6** are retired and are never reused. See
[decision-log.md](decision-log.md) for why each was withdrawn.*
