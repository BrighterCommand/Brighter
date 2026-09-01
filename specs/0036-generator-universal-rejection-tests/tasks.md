# Tasks: Generator-Owned Rejection and Delay Conformance Tests (Spec 0036)

**Requirements**: [requirements.md](requirements.md)
**Design**: [ADR 0070](../../docs/adr/0070-generator-owned-rejection-and-delay-conformance.md)
**Status**: Draft — do not start until the ADR is approved.

## Note on TDD

The deliverable here *is* tests, so `/test-first` does not apply in its usual form. The equivalent
gate is: **each new template is validated against a transport that already has the hand-written
equivalent before it is rolled out to the rest.** Kafka has the most complete hand-written set and is
the reference transport for every behaviour except delayed send, where RocketMQ and AWS `SqsStandard`
already have generated coverage.

## Phase 0 — Generator plumbing (no generated output changes yet)

- [ ] **T0.1 Repair the requeue-with-delay templates (D-1).**
  - `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Reactor/When_requeuing_a_failed_message_with_delay_should_receive_message_again.cs.liquid`
  - and the Proactor equivalent.
  - Pass a delay to `Requeue` / `RequeueAsync`; adopt the receive-retry loop from
    `When_requeuing_a_failed_message_should_receive_message_again`; assert the message is *not*
    available before the delay elapses and *is* after.
  - Regenerate and confirm the three currently-affected configurations (AWS `SqsStandard`,
    AWS.V4 `SqsStandard`, RocketMQ) still pass.

- [ ] **T0.2 Replace substring gating with explicit template metadata (D-2).**
  - `Generators/MessagingGatewayGenerator.SkipTest` currently matches substrings of the **full
    template path**, so one filename can match two unrelated gates.
  - Replace with an explicit map from template file name to its gate (or to "always generate").
  - Add a generation-time error for a template that no rule covers, so a new template cannot be
    added without a deliberate decision about gating.

- [ ] **T0.3 Add the new configuration surface.**
  - `Configuration/MessagingGatewayConfiguration.cs`: add `HasNativeDeadLetterQueue`,
    `HasNativeDelay`, `ConformanceWaivers` (`Dictionary<string, string>?`).
  - **Remove** `HasSupportToDeadLetterQueue` and `HasSupportToDelayedMessages`.
  - Bind with `JsonSerializerOptions` that reject unknown members for the gateway section, so a
    stale flag left in a configuration file fails generation rather than being ignored.
  - Validate waiver names against the closed set (`BrighterRejectionChannels`,
    `BrighterInvalidMessageChannel`, `SchedulerBackedDelay`); an unknown name is a generation error.
  - Log every honoured waiver at `Information`.

## Phase 1 — Provider interface (breaking; 21 provider classes)

- [ ] **T1.1 Emit `RejectionRouting`.**
  - New templates `Templates/MessagingGateway/{Reactor,Proactor}/RejectionRouting.cs.liquid`,
    emitted into the same namespace as the provider interface.

- [ ] **T1.2 Extend the provider interface templates.**
  - `IAmAMessageGatewayReactorProvider.cs.liquid` and `IAmAMessageGatewayProactorProvider.cs.liquid`.
  - Replace `bool setupDeadLetterQueue` with `RejectionRouting? rejectionRouting` and add
    `int? requeueCount`.
  - Add the `IAmAMessageScheduler?` parameter to `CreateChannel` / `CreateChannelAsync`.
  - Replace `GetMessageFromDeadLetterQueue(subscription)` with `GetMessageFrom(routingKey)`
    (and the async equivalent).
  - Add `bool HasNativeDelay { get; }`.

- [ ] **T1.3 Emit `RecordingMessageScheduler`.**
  - Shared test double implementing `IAmAMessageSchedulerSync` and `IAmAMessageSchedulerAsync`.
  - Records `Schedule` calls (message + delay); optionally re-sends through an injected producer once
    the delay elapses. See ADR 0070 §5.

- [ ] **T1.4 Update the 21 provider implementations.**
  - AWS x4, AWS.V4 x4 — `Sns{Standard,Fifo}`, `Sqs{Standard,Fifo}MessageGatewayProvider`
  - GCP x4 — `GcpPull`, `GcpPullOrdering`, `GcpStream`, `GcpStreamOrderingMessageGatewayProvider`
  - Kafka x3 — `KafkaClassic`, `KafkaConsumer`, `KafkaPartitionKeyMessageGatewayProvider`
  - `MsSqlMessageGatewayProvider`, `PostgresMessageGatewayProvider`, `RedisMessageGatewayProvider`
  - RMQ.Async x2 — `RmqClassic`, `RmqQuorumMessageGatewayProvider`
  - `RocketMqMessageGatewayProvider`
  - The test projects do not compile until all 21 are done. Land as one change.

## Phase 2 — Brighter-provided templates (Kafka first, then roll out)

For each behaviour: write the Reactor and Proactor template, generate for Kafka only, compare
against the hand-written Kafka test, then regenerate for all configurations.

- [ ] **T2.1** `When_rejecting_message_with_delivery_error_should_send_to_dlq`
      — reference: `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Reactor/When_rejecting_message_with_delivery_error_should_send_to_dlq.cs`
- [ ] **T2.2** `When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel`
- [ ] **T2.3** `When_rejecting_message_with_unacceptable_and_no_invalid_channel_should_fallback_to_dlq`
- [ ] **T2.4** `When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log`
      — asserts only the shared invariant (no throw, no redelivery); see ADR 0070 §1
- [ ] **T2.5** `When_rejecting_message_should_include_rejection_metadata`
      — asserts `ORIGINAL_TOPIC`, `ORIGINAL_TYPE`, `REJECTION_REASON`, `REJECTION_TIMESTAMP`, and
        `REJECTION_MESSAGE` when a description was supplied
- [ ] **T2.6** `When_requeuing_a_failed_message_with_delay_should_receive_message_again` — ungate
      (built on T0.1)
- [ ] **T2.7** `When_requeuing_a_failed_message_with_delay_should_use_scheduler`
      — reference: `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Reactor/When_kafka_consumer_requeues_with_delay_should_use_scheduler.cs`
- [ ] **T2.8** `When_reading_a_delayed_message_via_the_messaging_gateway_should_delay_delivery`
      — ungate; wire `RecordingMessageScheduler` where `HasNativeDelay` is false

## Phase 3 — Native-only templates

- [ ] **T3.1** Re-gate `When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue` on
      `HasNativeDeadLetterQueue`.
- [ ] **T3.2** Add `When_sending_a_delayed_message_with_no_scheduler_should_delay_natively`, gated on
      `HasNativeDelay`.

## Phase 4 — Configuration corrections and waivers

- [ ] **T4.1 Correct the native declarations (FR-9).**

  | Project | Configuration | `HasNativeDelay` | `HasNativeDeadLetterQueue` |
  |---|---|---|---|
  | AWS, AWS.V4 | `SnsStandard`, `SnsFifo`, `SqsStandard`, `SqsFifo` | true | true |
  | PostgreSQL | (single) | true | false |
  | MSSQL | (single) | false | false |
  | Kafka | all three | false | false |
  | Redis | (single) | false | false |
  | RocketMQ | (single) | true | true |
  | RMQ.Async | `Classic`, `Quorum` | false | true |
  | GCP | all four | false | true |

  Confirm each cell against the transport source before writing it — this table is derived from the
  survey in the requirements and must not be trusted blind. In particular, check FIFO queue
  `DelaySeconds` semantics for `SnsFifo`/`SqsFifo`, and RocketMQ delay-level granularity.

- [ ] **T4.2 Declare the waivers (FR-7).** Per the table in ADR 0070 §3. Each value carries a reason
      and a tracking issue reference.

- [ ] **T4.3 Open the tracking issues** for the gaps the waivers name:
  - RabbitMQ subscriptions to implement `IUseBrighterDeadLetterSupport` / `IUseBrighterInvalidMessageSupport`
  - GCP Pub/Sub subscriptions likewise
  - `IAmAChannelFactoryWithScheduler` on the AWS SQS, PostgreSQL, GCP Pub/Sub and RocketMQ channel factories

## Phase 5 — Retire the superseded hand-written tests

- [ ] **T5.1** Delete hand-written tests now covered by a template, per ADR 0070 §6, in the same
      change that generates the replacement. Keep the transport-specific tests listed there.
- [ ] **T5.2** Leave MQTT, RMQ.Sync and Azure Service Bus untouched — not generator-onboarded.

## Phase 6 — Verification

- [ ] **T6.1** Run `generate-test.ps1` (or `.sh`); confirm a clean run and that a second run produces
      no diff.
- [ ] **T6.2** Run each transport suite against its `docker-compose-*.yaml` container.
- [ ] **T6.3** Confirm no `HasSupportToDeadLetterQueue` or `HasSupportToDelayedMessages` remains
      anywhere in `tests/` or `tools/`.
- [ ] **T6.4** Update `docs/adr/index.md` (regenerate from frontmatter) and `specs/README.md`.
