# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and
> implementation details are documented in
> [ADR 0070](../../docs/adr/0070-generator-owned-rejection-and-delay-conformance.md).

**Linked Issue**: #4240
**Linked ADR**: [ADR 0070: Generator-Owned Rejection and Delay Conformance](../../docs/adr/0070-generator-owned-rejection-and-delay-conformance.md)

## Problem Statement

The messaging gateway test generator has two optional feature flags,
`HasSupportToDelayedMessages` and `HasSupportToDeadLetterQueue`, which are read as
"does this broker have this feature natively?" and are set per configuration to whatever keeps the
generated suite green. Both are wrong as written:

1. **They gate behaviour Brighter supplies universally.** `Reject(Message, MessageRejectionReason?)`
   is on `IAmAMessageConsumerSync` and `IAmAMessageConsumerAsync` for every transport. Where a
   transport has no native dead letter queue, Brighter provisions a dead-letter (and
   invalid-message) producer and drives it from `Reject`
   ([ADR 0045](../../docs/adr/0045-provide-dlq-where-missing.md),
   [ADR 0047](../../docs/adr/0047-message-rejection-routing-strategy.md)). Where a transport has no
   native delay, `SendWithDelay` falls back to `IAmAMessageScheduler`
   ([ADR 0037 Universal Scheduler Delay](../../docs/adr/0037-universal-scheduler-delay.md),
   [ADR 0039](../../docs/adr/0039-transport-scheduler-wiring.md)). These are conformance
   obligations, not opt-in capabilities.

2. **They are already mis-declared against the code.** `PostgresMessageProducer.SendWithDelay`
   writes a delay parameter into the insert, and `PostgresSubscription` implements both
   `IUseBrighterDeadLetterSupport` and `IUseBrighterInvalidMessageSupport`, yet the Postgres
   configuration declares `HasSupportToDelayedMessages: false` and
   `HasSupportToDeadLetterQueue: false`. `MsSqlSubscription` implements both interfaces too, and the
   MSSQL configuration declares `HasSupportToDeadLetterQueue: false`. AWS SQS has native
   `DelaySeconds` (up to 15 minutes) but three of its four configurations declare delay `false`.

The root cause is timing: the Reject to DLQ, invalid-channel, requeue-via-producer and
requeue-via-scheduler features post-date the generator. Because they could not be expressed as
templates, contributors hand-wrote them per transport. The result is broad but inconsistent
duplication, and no enforcement.

### Evidence: the same tests, hand-written per transport

Verified inventory of hand-written (non-generated) coverage. `G` marks a project that is onboarded
to the generator (has a `test-configuration.json` with a messaging gateway section).

| Transport | Gen? | Reject to DLQ | Reject to invalid | Fallback ladder | No channels | Metadata | Requeue w/ delay | Plain requeue | Delayed send |
|---|---|---|---|---|---|---|---|---|---|
| Kafka | G | yes | yes | yes | yes | yes | yes (producer + scheduler) | — | — |
| AWS SQS | G | yes | yes | yes | yes | — | — | — | — |
| AWS SQS V4 | G | yes | yes | yes | yes | — | — | — | — |
| Redis | G | yes | yes | yes | yes | — | yes (producer) | yes (zero-delay) | — |
| PostgreSQL | G | yes | yes | yes | yes | — | yes (native SQL) | yes | — |
| MSSQL | G | yes | yes | yes | yes | — | yes (producer) | yes (+ zero-delay) | — |
| RocketMQ | G | yes | yes | yes | yes | — | — | — | — |
| RMQ.Async | G | yes (native DLX) | — | — | — | — | yes (producer) | — | — |
| GCP Pub/Sub | G | — | — | — | — | — | — | — | — |
| MQTT | — | yes | yes | yes | yes | — | yes (producer) | — | — |
| RMQ.Sync | — | yes (native DLX) | — | — | — | — | yes (producer) | yes | yes |
| Azure Service Bus | — | — | — | — | yes (deferred) | — | — | — | yes (scheduler) |

Representative sources:

- `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Reactor/When_kafka_consumer_requeues_with_delay_should_use_producer.cs` and `..._should_use_scheduler.cs`
- `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Reactor/When_rejecting_message_with_delivery_error_should_send_to_dlq.cs`, `..._unacceptable_reason_should_send_to_invalid_channel.cs`, `..._unacceptable_and_no_invalid_channel_should_fallback_to_dlq.cs`, `..._no_channels_configured_should_acknowledge_and_log.cs`, `..._should_include_metadata.cs`
- `tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/Reactor/When_rejecting_a_message_to_a_dead_letter_queue.cs`
- Equivalents in AWS, AWS.V4, Redis, PostgreSQL, MSSQL, RocketMQ and MQTT, sharing a naming convention

Reject to DLQ and the fallback ladder are covered on essentially every modern transport; reject to
invalid on most (absent on both RMQ variants and GCP); requeue-with-delay on six. GCP has none of
it. Delayed send is generated for only three of twenty-one gateway configurations (AWS
`SqsStandard`, AWS.V4 `SqsStandard`, RocketMQ). The commitment is real and near-universal — it just
is not enforced by the generator, and the gaps are exactly where a template would close them.

## Proposed Solution

Distil the hand-written tests into a canonical set of behaviours, implement them as generator
templates that run by default for every gateway configuration, and re-scope the two flags so they
describe native broker capability rather than acting as an escape hatch. Where a transport genuinely
cannot satisfy a Brighter-provided behaviour today, record an explicit, justified **waiver** in its
configuration rather than a silent `false`.

## Requirements

### Definitions

- **Brighter-provided behaviour** — behaviour Brighter supplies regardless of broker capability:
  the `Reject` routing ladder, the Brighter-provisioned dead letter and invalid message channels,
  rejection metadata stamping, and scheduler-backed delay.
- **Native behaviour** — behaviour the broker performs itself: SQS redrive policy, RabbitMQ DLX,
  GCP dead letter policy, SQS `DelaySeconds`, the PostgreSQL delay column.
- **Gateway configuration** — one entry under `MessagingGateway`/`MessagingGateways` in a
  `test-configuration.json`. There are currently 21, across 9 test projects.
- **Waiver** — a named, justified, per-configuration declaration that a Brighter-provided behaviour
  is not yet satisfied by that transport, carrying a tracking reference.

### Functional Requirements

**FR-1: The generator owns the Brighter-provided rejection behaviours.**
The generator emits Reactor and Proactor templates for each of:

1. Reject with `DeliveryError` routes the message to the Brighter dead letter channel.
2. Reject with `Unacceptable` routes the message to the invalid message channel.
3. Reject with `Unacceptable` and no invalid message channel configured falls back to the dead
   letter channel.
4. Reject with neither channel configured completes the transport's no-channel semantics
   (acknowledge or delete) and does not throw.
5. A rejected message carries rejection metadata: original topic, original message type, rejection
   reason, rejection timestamp, and the rejection description when one was supplied.

**FR-2: The generator owns the requeue-with-delay behaviours.**

6. Requeue with a non-zero delay redelivers the message after the delay has elapsed.
7. Requeue with a non-zero delay on a transport without native delay routes through the configured
   `IAmAMessageScheduler`.

**FR-3: The generator owns delayed send.**

8. `SendWithDelay` with a non-zero delay does not deliver before the delay elapses and does deliver
   after it, with a scheduler wired for transports that have no native delay.

**FR-4: These templates are not gated by `HasSupportToDelayedMessages` or
`HasSupportToDeadLetterQueue`.** They run for every gateway configuration unless that configuration
carries an explicit waiver (FR-7).

**FR-5: Native behaviour is asserted separately and remains gated.**
Tests that assert broker-native behaviour specifically — redrive or DLX moving a message after the
requeue limit, and delay honoured with no scheduler wired — are gated on new flags that mean
*native capability* and nothing else.

**FR-6: Configuration flags are re-scoped.**
`HasSupportToDelayedMessages` and `HasSupportToDeadLetterQueue` are retired as opt-in gates for
Brighter-provided behaviour. Any replacement flag must be named so that it cannot be read as
"turn this test off".

**FR-7: Non-conformance is explicit and justified.**
A gateway configuration that cannot satisfy a Brighter-provided behaviour declares a named waiver
with a reason and a tracking reference. A waiver is visible in the configuration diff and in the
generator's output. Setting a waiver is not a way to make a red suite green quietly.

**FR-8: The provider interface exposes what these tests need.**
`IAmAMessageGatewayReactorProvider` and `IAmAMessageGatewayProactorProvider` must be able to supply:

- a subscription with dead letter routing, invalid message routing, both, or neither;
- a channel wired to a supplied `IAmAMessageScheduler`;
- a read-back of a message from an arbitrary rejection channel, not only "the DLQ";
- whether the transport's producer delays natively.

**FR-9: The mis-declared configurations are corrected.**
PostgreSQL (native delay, Brighter DLQ), MSSQL (Brighter DLQ), and the AWS SQS configurations
(native `DelaySeconds`) are corrected as part of re-scoping the flags.

**FR-10: Hand-written tests superseded by a template are removed.**
Where a template covers a hand-written test's behaviour on that transport, the hand-written test is
deleted in the same change, so the behaviour has exactly one owner. Hand-written tests asserting
transport-specific detail beyond the canonical set are kept.

### Defects to fix in the same pass

**D-1: The `requeuing_with_delay` template never exercises a delayed requeue.**
`Templates/MessagingGateway/{Reactor,Proactor}/When_requeuing_a_failed_message_with_delay_should_receive_message_again`
calls `Requeue(received)` with no timeout argument (`IAmAChannelSync.Requeue(Message, TimeSpan?)`).
It is the plain requeue test plus a `Thread.Sleep`, and weaker than the plain test because it lacks
its receive-retry loop.

**D-2: The same template is gated on two flags at once.**
`MessagingGatewayGenerator.SkipTest` matches on filename substrings. That filename contains both
`requeuing` and `with_delay`, so the template is skipped unless `HasSupportToRequeue` **and**
`HasSupportToDelayedMessages` are true.

### Non-functional Requirements

- **Determinism**: regenerating with no configuration change produces no diff.
- **Runtime**: delay-based templates must not add unbounded sleeps; delays are configurable per
  configuration, as `ReceiveMessageTimeoutInMilliseconds` already is.
- **Isolation**: each generated test provisions its own routing key, channel name and rejection
  channels, and cleans them up, matching the existing templates.
- **Reviewability**: the set of waivers across all configurations is readable as a single list —
  the conformance gap is a fact about the codebase, not folklore.

### Out of Scope

- Adding `IUseBrighterDeadLetterSupport` and `IUseBrighterInvalidMessageSupport` to the transports
  that lack them (RabbitMQ, GCP Pub/Sub, Azure Service Bus). This spec *exposes* those gaps as
  waivers; closing them is per-transport work with its own specs.
- Adding `IAmAChannelFactoryWithScheduler` to the channel factories that lack it (AWS SQS,
  PostgreSQL, GCP Pub/Sub, RocketMQ, Azure Service Bus).
- Onboarding MQTT, RMQ.Sync and Azure Service Bus to the generator.
- Aligning the generated test class names with the `[Behavior]Tests` convention from
  [spec 0031](../0031-test_naming_conventions/) — the templates currently emit
  `WhenRequeuingAFailedMessageWithDelayShouldReceiveMessageAgain`. Real, but a separate rename.
- Any change to `src/` production behaviour.

## Acceptance Criteria

1. All eight canonical Brighter-provided behaviours exist as Reactor and Proactor templates.
2. Regenerating produces those tests for all 21 gateway configurations except where a waiver is
   declared, and the waived set is exactly the transports shown to lack the underlying support.
3. `HasSupportToDelayedMessages` and `HasSupportToDeadLetterQueue` no longer gate any
   Brighter-provided behaviour.
4. The requeue-with-delay template passes a delay to `Requeue` and asserts redelivery after it.
5. No template is gated by more than one flag by filename accident.
6. PostgreSQL, MSSQL and AWS SQS configurations declare their native capabilities correctly.
7. Every hand-written test that a template now covers is deleted, and the generated suite is green
   on every transport whose containers CI runs.
8. `generate-test.sh` and `generate-test.ps1` run clean, and re-running produces no diff.

## Testing Approach

- The deliverable *is* tests; correctness is judged by running the regenerated suite against the
  transport containers in `docker-compose-*.yaml`.
- Each template is validated first against a transport that already has the equivalent hand-written
  test (Kafka has the most complete set), so the generated test can be compared against a known-good
  assertion before it is rolled out.
- Waivers are verified by asserting that a waived configuration emits no file for that behaviour and
  that the generator logs the waiver.
