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
   ADR `0040-mssql-dlq-brighter-managed`. Exactly three configurations declare
   `HasSupportToDelayedMessages: true` — `AWS/SqsStandard`, `AWS.V4/SqsStandard` and `RocketMQ` — so
   the delayed-delivery test runs for three of the twenty wired gateway configurations.

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
  Canonical templates are ungated *by construction*, not by removing the flags (FR-10(1)).
- **Conformance ledger** — the checked-in record of which targeted gateway configurations honour
  which canonical behaviours, and the gate on the gate-retirement change (FR-21).
- **Canonical behaviours** — FR-2, FR-4 … FR-9, FR-15, FR-16, FR-17, FR-22.
- **Canonical template** — a template this spec creates to assert a canonical behaviour. Ungated by
  construction (FR-10(1)) and generated for every targeted gateway configuration.
- **Legacy gated template** — one of the four pre-existing templates `SkipTest` suppresses on a
  capability gate, enumerated in FR-10(3). Never ungated — it keeps exactly its current gating, so it
  continues to generate wherever that gate is `true`, gains no new generation site, and is deleted
  rather than freed.
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

  **Interim obligation — the still-live legacy caller.** The exhaustion template
  (`When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue`) is the only current
  caller, and FR-10(3) keeps it generating for **sixteen** configurations until it is deleted at
  FR-10(3)'s retirement step — long after FR-1 lands. Its `.liquid` source is not a "generated
  caller", so it MUST be edited in the FR-1 change to stop passing the flag, or the AWS, AWS.V4, GCP,
  Redis, RMQ.Async and RocketMQ test projects fail to compile on the next regeneration.

  ⚠️ The template passes the flag **positionally**, as a bare `true` fourth argument — not as a
  named `setupDeadLetterQueue:` argument. None of the 32 generated copies contains the string
  `setupDeadLetterQueue`, so a name-based search for callers finds the 40 generated interface copies
  and the 20 provider implementations and **misses every one of the 32 broken call sites**. The bare
  `true` will not bind to the nullable `RoutingKey?` parameters of FR-1(1), so this is a hard compile
  break that the obvious search strategy cannot find. Search for the call, not for the parameter name.

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
Generate a test proving that when a channel requeues a received message with a non-zero delay `D`,
the message is **not** redelivered before `D` elapses and **is** redelivered after it. Both arms are
required: a gateway that ignores the delay and redelivers immediately still satisfies a
"redelivered eventually" assertion, so the test MUST also prove the lower bound — a receive attempted
before `D` elapses yields no message. The test asserts the observable outcome only; it makes no
assertion about which mechanism the gateway used to achieve the delay.

*Example:* send `M`; receive `M`; call `channel.Requeue(M, TimeSpan.FromSeconds(5))`; assert
`Requeue` returns `true`; a receive attempted immediately (a single bounded receive, before the 5s
elapses) yields `MT_NONE`; and a subsequent receive, within a bounded retry loop, after the delay
yields a message whose body equals `M`'s. A gateway whose `Requeue` ignores the delay by
**redelivering immediately** fails the immediate-`MT_NONE` arm — this is what makes GCP ×4 (FR-21)
fail as generated rather than pass green. (A gateway that ignores the delay without redelivering at
once — e.g. one whose no-op requeue leaves the message held by a native invisibility timeout — is
instead caught, if at all, on the after-delay arm; see FR-21 on RocketMQ.)

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
Generate a **canonical** test proving that a message sent with `producer.SendWithDelay(M, delay)` is
not receivable before the delay elapses and is receivable after it. The legacy
`When_reading_a_delayed_message_via_the_messaging_gateway_should_delay_delivery.cs.liquid` template
asserts this behaviour today but is gated and is never ungated (FR-10) — it continues to generate for
the three configurations declaring `HasSupportToDelayedMessages: true` until it is deleted. It MAY be
**migrated** into the canonical template rather than rewritten from scratch, and is retired with the
other legacy templates. The canonical template, not the legacy one, is what satisfies FR-9.

**FR-10 — Gating lifecycle: canonical templates are never gated; the gates retire with the legacy templates they gate.**
The three capability gates are **not** removed up front. They are narrowed, then retired last, in
this order:

- **FR-10(1) — Canonical templates are ungated by construction.** No canonical template may ever be
  suppressed by `HasSupportToDelayedMessages`, `HasSupportToDeadLetterQueue`,
  `HasSupportToRequeue`, or any successor flag, whatever the template is named. This MUST NOT rely
  on a naming convention: `SkipTest` matches filename *substrings*, and NFR-1's naming means a
  delayed-requeue template naturally contains `requeuing` and `with_delay`.
- **FR-10(2) — The gates are scoped to an explicit legacy list.** `SkipTest` MUST consult the three
  gates only for the four **legacy gated templates** named in FR-10(3). A template not on that list
  generates regardless of any flag value. The list is exhaustive and closed — nothing is ever added
  to it.
- **FR-10(3) — The legacy gated templates are never ungated, and are then retired.** Exactly four
  templates are gated today, in both Reactor and Proactor variants:
  `When_reading_a_delayed_message_via_the_messaging_gateway_should_delay_delivery` (gated by
  `delayed_message`), `When_requeuing_a_failed_message_should_receive_message_again` (`requeuing`),
  `When_requeuing_a_failed_message_with_delay_should_receive_message_again` (`with_delay` **and**
  `requeuing`), and `When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue`
  (`dead_letter_queue` **and** `requeuing`). **None is ever ungated**: each keeps exactly the gating
  it has today until it is **deleted** — template and every generated copy — once the canonical set
  covers the required behaviours. Two are superseded by canonical replacements (FR-9's delayed send,
  FR-22's plain requeue) which MAY be migrations of them; two are deleted outright as defective
  (FR-12, FR-19).

  ⚠️ *Never ungated* is not *never generated*. A gate suppresses a template only where that gate is
  declared `false`, and most configurations declare these gates `true`. **All four** legacy templates
  therefore generate **today**, and keep generating on every regeneration run, until they are
  deleted:

  | Legacy template | Gates | Configurations generating it | Copies |
  |---|---|---|---|
  | `..._too_many_times_should_move_to_dead_letter_queue` | DLQ ∧ Requeue | AWS 4, AWS.V4 4, GCP 4, Redis 1, RMQ.Async 2, RocketMQ 1 = **16** | 32 |
  | `..._failed_message_should_receive_message_again` | Requeue | all but Kafka ×2 = **18** | 36 |
  | `..._with_delay_...` | Delayed ∧ Requeue | `AWS/SqsStandard`, `AWS.V4/SqsStandard`, `RocketMQ` = **3** | 6 |
  | `..._delayed_message_...` | Delayed | same **3** | 6 |

  What FR-10(2) guarantees is that none of them ever generates **anywhere new**. What it does not
  claim is that they stop generating where they already do — see FR-1(6) for the compile-time
  consequence of that during the interim.
- **FR-10(4) — Only then do the gates themselves go.** Once the four legacy templates are deleted,
  the gate branches in `SkipTest` gate nothing and MUST be removed, along with the three properties
  on `MessagingGatewayConfiguration` and the keys in every configuration (FR-11). Note there are
  **four branches keyed on the three gates** — `HasSupportToDelayedMessages` is tested twice, once
  for `delayed_message` and once for `with_delay` — so removing "the three gates" leaves one branch
  behind.

No replacement `HasNative*` flag is introduced at any point (see OOS-1).

*Rationale:* the old tests are never wanted — not before the canonical set exists and not after. A
gate that suppresses a legacy template is doing useful work until that template is deleted, so
removing the gates first would generate exactly the tests this spec exists to replace, and would do
it against transports that have not been fixed yet.

**FR-11 — Remove the gate keys from every per-transport configuration.**
The three keys MUST be removed from every `test-configuration.json`, rather than left carrying
misleading values. This is the final step of FR-10(4) and happens **after** the legacy templates are
deleted — removing the keys earlier would ungate the legacy templates, which FR-10(3) forbids.

**FR-22 — Canonical plain requeue.**
Generate a **canonical** test proving that a message requeued with no delay is redelivered and
receivable again within a bounded retry loop. The legacy
`When_requeuing_a_failed_message_should_receive_message_again.cs.liquid` template asserts this today
but is gated (FR-10) and is never ungated; it MAY be **migrated** into the canonical template, and is
retired with the other legacy templates. This is the no-delay counterpart to FR-2's positive-delay
path.

FR-22 owns the **no-delay call in both its spellings**. Because the runtime signature is
`bool Requeue(Message message, TimeSpan? timeOut = null)`, `Requeue(m)` and `Requeue(m, null)`
compile to the identical call; they are not two behaviours and MUST NOT be specified as two. FR-15 is
therefore scoped to the explicit `TimeSpan.Zero` argument only — see FR-15.

**FR-12 — Remove the defective delayed-requeue template.**
The `When_requeuing_a_failed_message_with_delay_should_receive_message_again` template calls
`Requeue` with no `timeout` argument, so it never exercises delayed requeue, and it lacks the
bounded retry loop the plain-requeue template has. It MUST be deleted, superseded by FR-2. After
deletion, no messaging-gateway template that purports to exercise **delayed** requeue may call
`Requeue` or `RequeueAsync` without a non-null `TimeSpan`.

It is one of the four legacy gated templates of FR-10(3), and is deleted on that schedule — with its
generated copies — rather than as a standalone early change.

This prohibition is deliberately scoped to delayed-requeue templates. Two canonical templates
legitimately call `Requeue` without a positive delay and are unaffected: the canonical plain-requeue
template (FR-22, `Requeue(M)` — equivalently `Requeue(M, null)`) and the zero-boundary template
(FR-15, `Requeue(M, TimeSpan.Zero)`).

**FR-13 — Generate for every targeted gateway configuration; non-conformance is a defect to fix.**
A **targeted transport** is **every transport with a messaging gateway** — that is, every
`src/Paramore.Brighter.MessagingGateway.*` project. There are twelve: AWSSQS, AWSSQS.V4,
AzureServiceBus, GcpPubSub, Kafka, MQTT, MsSql, Postgres, Redis, RMQ.Async, RMQ.Sync, RocketMQ.
Membership is having a messaging gateway; generator wiring is not a criterion.

A **targeted gateway configuration** is a single gateway configuration declared by a targeted
transport's test project, under either the `MessagingGateway` (singular — one configuration) or the
`MessagingGateways` (plural — several) section of its
`tests/Paramore.Brighter.*.Tests/test-configuration.json`. **Generation and conformance are per
gateway configuration, not per project**: a transport declaring four configurations owes the
canonical behaviours four times over, once per configuration, and may conform in one configuration
while failing in another. This is the unit the target set is counted in, the unit a generated test is
emitted for, and the unit a conformance-ledger row (FR-21) records.

Nine of the twelve are wired today, declaring **twenty** targeted gateway configurations between
them: AWS, AWS.V4, GCP, Kafka, MSSQL, PostgresSQL, Redis, RMQ.Async, RocketMQ — AWS and AWS.V4 four
each, GCP four, Kafka two, RMQ.Async two, and one apiece for the rest, matching the twenty
`*MessageGatewayProvider.cs` implementations. (Fourteen `test-configuration.json` files exist; five —
DynamoDB, DynamoDB.V4, MongoDb, MySQL, Sqlite — declare only `Outbox`/`Outboxes` and have no gateway
at all, so they are not transports for this purpose.) The remaining three gateways —
**AzureServiceBus, MQTT, RMQ.Sync** — are targeted transports declaring no configuration yet; FR-20
brings them into the generator, and they contribute their own configurations to the target set.

Gateway project names and test project names do not correspond by simple string match — five of the
twelve differ. This is the mapping, and it is definitive:

| Gateway project (`src/Paramore.Brighter.MessagingGateway.*`) | Test project (`tests/Paramore.Brighter.*.Tests`) | Wired today |
|---|---|---|
| `AWSSQS`           | `AWS`           | yes |
| `AWSSQS.V4`        | `AWS.V4`        | yes |
| `AzureServiceBus`  | `AzureServiceBus` | no — FR-20 |
| `GcpPubSub`        | `Gcp`           | yes |
| `Kafka`            | `Kafka`         | yes |
| `MQTT`             | `MQTT`          | no — FR-20 |
| `MsSql`            | `MSSQL`         | yes |
| `Postgres`         | `PostgresSQL`   | yes |
| `Redis`            | `Redis`         | yes |
| `RMQ.Async`        | `RMQ.Async`     | yes |
| `RMQ.Sync`         | `RMQ.Sync`      | no — FR-20 |
| `RocketMQ`         | `RocketMQ`      | yes |

⚠️ `tests/Paramore.Brighter.Azure.Tests` exists and is **not** the AzureServiceBus gateway's test
project; a prefix match selects it wrongly. Use the table, not a name-derivation rule.

The canonical templates MUST be generated for **every** configuration of **every** targeted
transport — generation is not optional and no transport is excluded. Where the generated suite
fails to compile or fails at runtime because a transport does not honour a universal obligation,
that is a **defect in that transport's gateway and is in scope to fix**. No canonical test may be
silently skipped or gated away to make the suite green. Where a specific fix is deferred, the
deferral MUST be recorded as a named, linked follow-up issue with explicit maintainer sign-off,
carrying a conformance-ledger row (FR-21) and an auditable in-tree marker at the deferred test —
never an open-ended escape hatch.

**FR-14 — Sync (Reactor) and async (Proactor) parity.**
Every canonical template MUST be produced in both a Reactor variant driving `IAmAChannelSync` and
`IAmAMessageProducerSync`, and a Proactor variant driving `IAmAChannelAsync` and
`IAmAMessageProducerAsync`. Neither variant drives a message pump.

**FR-15 — An explicit zero delay does not delay.**
Generate a canonical test asserting that `channel.Requeue(M, TimeSpan.Zero)` behaves as an immediate
plain requeue: the message is received on the **first** iteration of the plain-requeue bounded retry
loop (NFR-2), and the elapsed time from the `Requeue` call to receipt is less than FR-2's positive
delay (5s). This proves `TimeSpan.Zero` is not special-cased into an error or an unbounded wait, and
is not treated as a positive delay. (FR-2's own before-`D` arm, not FR-15, is what prevents a
delay-ignoring gateway from being mistaken for a conforming one.)

FR-15 is scoped to the **explicit `TimeSpan.Zero` argument only**. The omitted and explicitly-null
spellings both belong to FR-22: the signature is
`bool Requeue(Message message, TimeSpan? timeOut = null)`, so `Requeue(M)` and `Requeue(M, null)` are
the same call and cannot be two requirements. FR-15 and FR-22 are separate canonical behaviours with
separate ledger columns — *"zero is not special-cased"* and *"plain requeue redelivers"* — asserted by
separate templates.

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
NFR-3 and OOS-1). It is one of the four legacy gated templates of FR-10(3): never ungated, still
generating for its sixteen configurations, and deleted with its thirty-two generated copies. FR-10(2)'s
closed-list guarantee ensures it never reaches a configuration with no delivery counter, so it cannot
produce a false failure that FR-13 would misclassify as a gateway defect.

Note this template is the **only** current caller of `CreateSubscription`'s `bool
setupDeadLetterQueue`; deleting it does not remove the obligation to drop that parameter, which
FR-1(6) carries.

**FR-20 — Onboard the three unwired gateway transports.**
`AzureServiceBus`, `MQTT` and `RMQ.Sync` have messaging gateways and test projects but no generator
wiring, so they generate nothing today. Each MUST be brought into the generator so the canonical
templates are produced for it like any other transport. For each:

1. add a `test-configuration.json` declaring its gateway configuration(s) under `MessagingGateway`
   or `MessagingGateways`;
2. implement **both** `IAmAMessageGatewayReactorProvider` and `IAmAMessageGatewayProactorProvider`
   against the FR-1 surface — including the routing-key parameters of FR-1(1), the invalid-channel
   read of FR-1(3) and the metadata key names of FR-1(5). Both variants are required because FR-14
   mandates a Reactor and a Proactor variant of every canonical template, and FR-21 records a
   configuration as conforming only when both variants pass; a single-variant onboarding could never
   resolve to conforming. All three FR-20 transports have both a sync and an async
   consumer/producer surface, so this asks for nothing they cannot supply;
3. supply the CI test infrastructure the transport's generated suite needs in order to **execute
   against a real broker** — a container, an emulator, or a live service instance. The acceptance
   condition is that the canonical tests *run* against that broker, not merely that they compile:
   a configuration whose suite only compiles is not conformant and MUST NOT be recorded as passing
   (FR-21).

Existing hand-written gateway coverage these onboardings build on, counted as test files under
`tests/Paramore.Brighter.*.Tests/MessagingGateway/`: RMQ.Sync 31, MQTT 19, AzureServiceBus 15. MQTT
has its own dead-letter ADR (`0043-mqtt-dlq-brighter-managed`); MQTT and RMQ.Sync both implement
`IAmAChannelFactoryWithScheduler`.

An onboarding that cannot be completed within this spec is governed by FR-13's deferral rule: a
named, linked follow-up issue with explicit maintainer sign-off, recorded in the conformance ledger.
**Inability to stand up CI infrastructure is an explicit, first-class ground for such a deferral** —
AzureServiceBus is a cloud service with no container story in this repository, so its onboarding is
the likeliest to land deferred. A deferral on infrastructure grounds still requires the
configuration to be named in the target set and carry a ledger row; no transport is dropped from the
target set.

**FR-21 — A conformance ledger records the state of every targeted gateway configuration.**
A **conformance ledger** MUST be created and checked in at
`specs/0036-universal-transport-conformance-tests/conformance-status.md`. It is the single record of
which targeted gateway configurations honour which canonical behaviours, and it is the gate on the
FR-10/FR-11 gate-retirement change.

- **One row per targeted gateway configuration** (FR-13), identified as `project / configuration` —
  e.g. `AWS.V4 / SqsStandard`, `GCP / PullOrdering`, `Kafka / PartitionKey`. Twenty rows across the
  nine wired projects today, plus the rows AzureServiceBus, MQTT and RMQ.Sync contribute under
  FR-20. A project-level row cannot express "SQS Standard conforms to FR-5 but SNS FIFO does not",
  which is why the granularity is per configuration.

  **Row identity.** A row's `project` token is the **test-project** name from FR-13's mapping table
  (e.g. `PostgresSQL`, not the `Postgres` gateway-project name, and not the product spelling
  `PostgreSQL`), because ADR 0067's CI audit compares that token as a string against in-code `Skip`
  markers. The three examples above come from `MessagingGateways` (plural) sections, where the
  configuration name is the JSON key. Four wired configurations — **Redis, MSSQL, PostgresSQL,
  RocketMQ** — instead use a singular `MessagingGateway` section, which carries no name key. Such a
  configuration is named by its `CollectionName`, so the row reads e.g. `Redis / RedisMessagingGateway`.
  Without this rule four of the twenty rows have no constructible identifier.

  **Placeholder rows for un-onboarded transports.** A transport whose FR-20 onboarding is deferred
  declares no `test-configuration.json`, therefore declares no configuration, therefore would
  contribute no row — and the completeness gate below would pass **vacuously** for exactly the
  transport the spec expects to defer. Every one of the twelve targeted transports MUST therefore
  occupy the ledger. A transport that has not yet declared a configuration takes a single
  **placeholder row** named `<Project> / (not yet declared)` — e.g.
  `AzureServiceBus / (not yet declared)`. A placeholder cell is seeded like any other cell and may be
  provisionally unresolved while the fix phase runs, but it may only ever *resolve* to a signed-off
  deferral — never to conforming or fixed — because the transport has no generated suite to pass. At
  the point the FR-10(4)/FR-11 gate-retirement change is proposed for merge, every placeholder cell
  must read as a signed-off deferral. The placeholder is replaced by per-configuration rows when its
  configuration lands. The completeness
  gate is evaluated over **all twelve targeted transports**, not over whichever rows happen to exist.
- **One column per canonical behaviour** — FR-2, FR-4 … FR-9, FR-15, FR-16, FR-17, FR-22.
- **Each cell records** that the behaviour conforms as generated, conforms via an in-spec gateway
  fix linked to its PR or commit, or is deferred to a named, linked, maintainer-signed-off follow-up
  issue. A cell may be provisionally unresolved only while the fix phase is in progress.
- **The completeness gate**: the change that retires the three gates (**FR-10(4)**, FR-11) MUST NOT
  merge while any cell remains unresolved. Every cell must read as conforming, fixed, or signed-off
  deferred at that point. The citation is to FR-10(**4**) specifically: FR-10 as a whole is a
  four-part lifecycle whose *first* step must land before any canonical test can generate, and
  therefore before any cell can resolve — gating the whole of FR-10 on a populated ledger would
  reinstate the circular dependency this lifecycle exists to dissolve.
- A configuration conforms for a behaviour only when **both** the Reactor and Proactor variants pass
  (FR-14), and only when the suite has actually run against a broker rather than merely compiled
  (FR-20(3)).

Non-conformances identified before the rollout begins are seeded into the ledger rather than
discovered late. **Five configurations across two transports are already known not to conform to
FR-2**, ahead of any generation run, and are seeded as such:

- **GCP ×4** (`Pull`, `PullOrdering`, `Stream`, `StreamOrdering`) — all four Pub/Sub consumers'
  `Requeue` ignores the delay argument: `GcpPullMessageConsumer.Requeue` calls
  `ModifyAckDeadline(..., 0)` (its XML doc reads "not used by Pub/Sub"), and
  `GcpPubSubStreamMessageConsumer.Requeue` calls `Reject()` on the stream message; neither applies the
  delay, so the message is redelivered immediately and fails AC-2's before-`D` arm. Redelivery timing
  comes from the subscription's RetryPolicy, not the requeue delay. These are in-scope gateway defects
  under FR-13.
- **RocketMQ** — `RocketMessageConsumer.Requeue` is a no-op returning `true`
  (`ChangeInvisibleDuration` is commented out pending an upstream RocketMQ C# client fix). Unlike
  GCP, RocketMQ does **not** redeliver immediately: the no-op neither acks nor changes visibility,
  so the message stays held by the consumer's native invisibility timeout (default 30 s), unrelated
  to the requeue delay. It therefore **passes** AC-2's before-`D` arm (an immediate receive finds the
  message still invisible and yields `MT_NONE`); its non-conformance is that it ignores the delay,
  which the generated test catches — if at all — on the after-`D` arm, and a timeout falling inside
  the retry window could even let it pass FR-2 by accident. Because the fix is blocked on a third
  party regardless, RocketMQ is expected to resolve as a signed-off `Deferred` ledger row rather than
  an in-spec fix.

These are known *non-conformances*, not the only ones the rollout may surface; other cells resolve
as configurations are generated and run. The exact cell vocabulary, the greppable in-code deferral
marker that cross-checks the ledger, and the CI audit that enforces the two against each other are
design decisions recorded in ADR 0067.

### Non-functional Requirements

- **NFR-1 (Consistency).** Generated tests MUST follow the naming convention established by the
  hand-written tests, e.g. `When_rejecting_message_with_delivery_error_should_send_to_dlq`,
  `When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel`,
  `When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log`.
- **NFR-2 (Determinism).** Timing-dependent tests MUST use bounded receive-retry loops rather than
  a single receive after a fixed sleep, so broker propagation delays do not cause false failures. The
  bound is a single wall-clock ceiling with a poll interval, stated once here and cited by FR-2,
  FR-9, FR-15, FR-16 and FR-22: **an arrival retry loop polls at a 500 ms interval up to a 30 s
  ceiling**, returning as soon as a message arrives and failing if the ceiling is reached with none.
  The **positive delay** used by the FR-2 and FR-9 tests is **5 s**, comfortably inside the 30 s
  ceiling; the **lower-bound / negative** assertions (a receive that must find nothing — FR-2's
  before-`D` arm, FR-9's immediate receive, and the "does not appear" checks) are the exemption of
  AC-20: a single bounded receive with the transport's default receive timeout, performed before the
  relevant window, not a retry loop. FR-15's zero-delay check is **not** in this set — it is a
  positive first-iteration arrival that stays inside the retry loop. (These figures are a determinism
  floor for CI, not a behavioural contract; a maintainer may widen the ceiling for a slow broker.)
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

**The scope boundary in one line: this spec's generated suite proves that each transport conforms to
the behaviours *Brighter* guarantees universally. Proving that a transport behaves as *its own
implementer* intends — that a transport-specific mechanism works, or that transport-internal
machinery is correct — remains bespoke, hand-written by that transport's implementer, and out of
scope here.** The generated tests answer "does this gateway honour Brighter's universal contract?";
they deliberately do not answer "does this gateway do everything its implementer meant it to?". That
second question is real and worth testing, but it is per-transport work the implementer owns, not
something this generator can or should produce from a shared template. OOS-2 and OOS-3 are the two
faces of that boundary — mechanism proofs and internal-mechanics proofs respectively.

- **OOS-1.** Re-introducing any `HasNative*` capability flag into the suite. A native/non-native
  distinction in these conformance tests is explicitly rejected: we test *supported*, not *how*.
- **OOS-2.** Implementer-owned proofs that a *specific mechanism* works — native redrive policies,
  native DLX, native delay columns, the *provisioning* substitution of Brighter's generic DLQ for an
  absent native one (distinct from FR-6's in-scope reject-reason routing to whatever DLQ exists),
  scheduler delegation for the six gateways that implement `IAmAChannelFactoryWithScheduler`, and the
  like.
  These assert *how* a transport achieves a behaviour, so by NFR-3 they can never be part of the
  mechanism-agnostic generated suite; they are the transport implementer's bespoke tests, and where
  this spec surfaces a candidate one it is recorded as a follow-up task in tasks.md rather than as an
  obligation here. For the scheduler-delegation subset, injecting an `InMemoryScheduler` backed by a
  `FakeTimeProvider` (which the scheduler already supports — it schedules via
  `timeProvider.CreateTimer`) gives a deterministic, non-flaky way to assert the delay by advancing
  the fake clock past it. That technique is deliberately confined to OOS-2: it controls only the six
  scheduler-delegating configurations, not the native-delay ones (AWS SQS visibility, Postgres), and
  it asserts the *mechanism*, so it cannot serve the mechanism-agnostic universal FR-2, which uses
  the real-broker before-`D`/after-`D` arms instead.
- **OOS-3.** Implementer-owned proofs of transport-*internal* mechanics: offset commit and sweep,
  header byte round-tripping, partition-key handling, fatal/non-fatal error escalation,
  producer-not-persisted synthesis, requeue-producer disposal, and factory/subscription wiring unit
  tests. These prove the transport behaves as its implementer intends, not as Brighter universally
  requires, so they stay bespoke. Routing-key plumbing is the one item proven transitively by the
  end-to-end canonical tests; the remainder is transport-specific and out of scope.
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
  setupDeadLetterQueue` parameter, and no provider implementation or generated caller references it —
  **including the still-live exhaustion template**, which passes the flag positionally as a bare
  `true` and so is not found by searching for the parameter name (FR-1(6)). *Then* every affected
  project compiles after regeneration.
- **AC-2 (FR-2).** *Given* a received message on **any** target configuration, *when*
  `channel.Requeue(message, 5s)` is called, *then* `Requeue` returns `true`; *and when* a receive is
  attempted immediately — a single bounded receive, before the 5s elapses — *then* it yields
  `MT_NONE`; *and when* a receive is attempted after the delay, within the bounded retry loop, *then*
  it yields a message with the same body — with no assertion about how the delay was achieved. The
  immediate-`MT_NONE` arm is the lower bound: without it a gateway that ignores the delay and
  redelivers at once would pass. It is the assertion **GCP ×4** are known to fail (FR-21), because
  those consumers redeliver immediately. RocketMQ also ignores the delay but does *not* redeliver at
  once — its no-op requeue leaves the message held by a native invisibility timeout — so it passes
  this arm and, if caught at all, fails on the after-delay arm; see FR-21.
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
- **AC-9 (FR-9).** *Given* `producer.SendWithDelay(message, 5s)` driven by the **canonical**
  delayed-send template, *when* a receive is attempted immediately, *then* it yields `MT_NONE`;
  *when* a receive is attempted after the delay, *then* it yields the message. The legacy
  `..._delayed_message_...` template does not satisfy this criterion; it is never ungated and is then
  deleted (FR-10(3)).
- **AC-10 (FR-10).** Three checkpoints, in order.
  *(a) While the canonical set is being built* — *given* the generator source, *when* `SkipTest` is
  inspected, *then* the four gate branches (keyed on three gates) are reachable only for the four
  legacy template names of FR-10(3); and *when* generation runs for a configuration declaring all
  three gates `false` (Kafka Standard and PartitionKey do today), *then* every canonical template is
  still emitted for it, and none of the four legacy templates is. This checkpoint asserts the legacy
  templates gain **no new** generation sites; it does not assert they stop generating where their
  gates are already `true`.
  *(b) After the canonical set is complete* — *when* the template directories are inspected, *then*
  none of the four legacy templates remains, in either variant, and no generated copy of any of them
  remains under any `tests/Paramore.Brighter.*.Tests/**/Generated/` directory (**eighty** such copies
  exist today: 6 + 36 + 6 + 32).
  *(c) Finally* — *when* `SkipTest` and `MessagingGatewayConfiguration` are inspected, *then* **no
  branch** is keyed on any of the three gates — all **four** of them, since
  `HasSupportToDelayedMessages` is tested twice — and the three properties are absent.
- **AC-11 (FR-11).** *Given* the per-transport configurations, *when* they are inspected **after
  AC-10(b) holds**, *then* none contains `HasSupportToDelayedMessages`,
  `HasSupportToDeadLetterQueue`, or `HasSupportToRequeue`. Inspected *before* that point, the keys
  are expected to still be present — their removal is what would wrongly ungate the legacy
  templates.
- **AC-12 (FR-12).** *Given* the template source, *when* it is inspected **after the legacy
  retirement of AC-10(b)**, *then* the defective `with_delay` template is absent, **and no generated
  copy of it remains under any `tests/Paramore.Brighter.*.Tests/**/Generated/` directory** (six such
  copies exist today, and deleting a template does not delete them), and every remaining
  messaging-gateway template that exercises delayed requeue passes a non-null `TimeSpan` to
  `Requeue`/`RequeueAsync`. The canonical plain-requeue template (FR-22, calling `Requeue(M)` or
  equivalently `Requeue(M, null)`) and the zero-boundary template (FR-15, calling
  `Requeue(M, TimeSpan.Zero)`) do not exercise delayed requeue and do not violate this.
- **AC-13 (FR-13).** *Given* a full generation run, *when* the suite is generated, *then* every
  configuration of all **twelve** gateway transports — including AzureServiceBus, MQTT and RMQ.Sync
  once FR-20 wires them — has the canonical tests present, and none is **silently** skipped or gated
  away; and for each configuration the generated suite either runs and passes, or the
  non-conformance is fixed in the gateway, or it is deferred — where a deferred test carries an
  auditable in-tree marker naming its linked, maintainer-signed-off issue, and a matching
  conformance-ledger row (FR-21). A skip without that marker and row, or **any canonical template
  suppressed by a capability gate**, fails this criterion; a skip carrying both satisfies it. The
  suppression of the four legacy templates (FR-10(3)) is not a violation — they are not canonical
  tests and never become any configuration's coverage. No silent skip, no unaudited deferral.
- **AC-14 (FR-14).** *Given* the generated output, *when* a configuration is generated, *then* each
  canonical behaviour appears in both a Reactor variant driving `IAmAChannelSync` and a Proactor
  variant driving `IAmAChannelAsync`, and neither drives a pump. Every targeted configuration has
  both channel surfaces (FR-20(2)), so the parity is unconditional — there is no configuration for
  which only one variant is generated.
- **AC-15 (NFR-1).** *Given* the generated files, *when* their names are inspected, *then* they
  match the established `When_...` conventions.
- **AC-16 (FR-15).** *Given* a received message, *when* `channel.Requeue(message, TimeSpan.Zero)` is
  called, *then* `Requeue` returns `true` and the message is received on the **first** iteration of
  the plain-requeue bounded retry loop, with elapsed time from the `Requeue` call to receipt less
  than FR-2's positive delay (5s). The `Requeue(message, null)` and `Requeue(message)` spellings are
  the same call and are asserted by AC-25, not here.
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

  The exemption is per *assertion*, not per AC — several ACs contain one of each. Exempt are only
  the assertions that a message is **not** present: AC-2's "a receive attempted before the delay
  yields `MT_NONE`", AC-5's "does not appear on any DLQ", AC-9's "a receive attempted immediately
  yields `MT_NONE`", and AC-18's "does not appear on the invalid channel". Each is a single bounded
  receive checking absence — before a delay elapses (AC-2, AC-9) or on a channel that must stay empty
  (AC-5, AC-18) — because retrying until arrival would invert the assertion. The **positive** half of
  the same criteria — AC-2's and AC-9's receive after the delay, AC-5's and AC-18's
  DLQ/invalid-channel arrivals — stays inside a bounded retry loop like every other arrival, as does
  AC-16's first-iteration receipt.
- **AC-21 (NFR-3).** *Given* the generated messaging-gateway templates, *when* their sources are
  inspected, *then* no assertion references a scheduler, a native-delay API, a redrive policy, a
  DLX, or any other transport-specific mechanism — every assertion is on an observable outcome
  reached through the channel or producer surface.
- **AC-22 (FR-19).** *Given* the template source, *when* it is inspected **after the legacy
  retirement of AC-10(b)**, *then*
  `When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue.cs.liquid` is absent from
  both the Reactor and Proactor template directories, **and no generated copy of it remains under any
  `tests/Paramore.Brighter.*.Tests/**/Generated/` directory** (thirty-two such copies exist today).
  Before that point the template is expected to be present and gated exactly as it is today —
  generated for the **sixteen** configurations declaring both `HasSupportToDeadLetterQueue` and
  `HasSupportToRequeue` true (AWS ×4, AWS.V4 ×4, GCP ×4, Redis, RMQ.Async ×2, RocketMQ), and for no
  others. It is never *ungated*; it does not stop generating where it already does.
- **AC-23 (FR-20).** *Given* the twelve gateway-project → test-project pairs in FR-13's mapping
  table, *when* each pair's test project is inspected, *then* it contains a `test-configuration.json`
  declaring a `MessagingGateway` or `MessagingGateways` section and a
  `*MessageGatewayProvider.cs` implementing **both** the Reactor and Proactor provider interfaces of
  the post-FR-1 surface (FR-20(2)) — as all twenty wired providers already do; and *when* generation
  runs,
  *then* AzureServiceBus, MQTT and RMQ.Sync each emit the canonical templates for every
  configuration they declare; and *when* those templates are executed, *then* they run against a
  broker — container, emulator, or live service — rather than only compiling. Any pair not meeting
  this has a named, linked, signed-off deferral issue and a conformance-ledger row (FR-21) — never
  silent absence from the target set. Inability to provide CI infrastructure is a valid ground for
  that deferral; silently omitting the transport is not.
- **AC-24 (FR-21).** *Given* the spec directory, *when*
  `specs/0036-universal-transport-conformance-tests/conformance-status.md` is inspected, *then* it
  contains one row per targeted gateway configuration — identified as `project / configuration`,
  naming a singular `MessagingGateway` section's configuration by its `CollectionName` — and one
  column per canonical behaviour (FR-2, FR-4 … FR-9, FR-15, FR-16, FR-17, FR-22).

  *Then* **all twelve** targeted transports are represented: a transport that declares no
  configuration yet occupies a single placeholder row `<Project> / (not yet declared)`. A
  placeholder cell may be provisionally unresolved while the fix phase runs, but may only ever
  resolve to a signed-off deferral — never to conforming or fixed. **At the point the
  FR-10(4)/FR-11 gate-retirement change is proposed for merge**, every placeholder cell reads as a
  signed-off deferral. The completeness check counts transports, not rows, so a transport
  contributing no rows fails it rather than passing vacuously.

  And *when* the **FR-10(4)**/FR-11 gate-retirement change is proposed for merge, *then* no cell is
  unresolved: each reads as conforming, fixed with a linked PR or commit, or deferred to a named,
  linked, signed-off issue. A configuration is recorded as conforming for a behaviour only where both
  the Reactor and Proactor variants passed against a running broker.
- **AC-25 (FR-22).** *Given* a received message on any target configuration, *when* the canonical
  plain-requeue template calls `channel.Requeue(message)` with no delay argument — equivalently
  `Requeue(message, null)`, the parameter being optional and null-defaulted — *then* `Requeue`
  returns `true` and a subsequent receive within the bounded retry loop yields a message with the
  same body. This criterion is satisfied by the canonical template only; the legacy
  `When_requeuing_a_failed_message_should_receive_message_again` template is never ungated and is
  then deleted (FR-10(3)).

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
| plain requeue / delayed send                                      | FR-22 / FR-9 (canonical replacements; the legacy templates are retired, not ungated) |

Kafka's metadata test exists only in the Reactor variant; FR-14 parity closes that gap in the
generated suite. The remaining Kafka tests are excluded under OOS-3 as transport-internal.

Note: the hand-written grounding tests drive the raw consumer surface; the canonical templates
re-express the same behaviour at the channel surface per the Objective and Test Boundary, so they
adapt rather than copy.

---

*The numbering contains deliberate gaps. **FR-1(4)**, **FR-3**, **FR-18**, **NFR-4**, **AC-3**,
**AC-19** and **OOS-6** are retired and are never reused. See
[decision-log.md](decision-log.md) for why each was withdrawn.*
