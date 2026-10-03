# Conformance Status

Location: `specs/0036-universal-transport-conformance-tests/conformance-status.md`

This matrix is the single source of truth for per-configuration conformance state (FR-21 / AC-24).
It is the gate on the FR-10(4)/FR-11 gate-retirement change: the cleanup MUST NOT merge while any
cell remains `Unknown`.

## Cell Vocabulary

| Value | Meaning |
|-------|---------|
| `Unknown` | Transient; permitted only during the fix phase. The cleanup is blocked while any cell holds this value. |
| `Pass` | Conforms as generated; both Reactor and Proactor variants are green against a live broker. |
| `Fixed (#PR/commit)` | Conformed via an in-spec gateway fix linked to the PR or commit. |
| `Deferred -> #NNNN (sign-off: @iancooper)` | A named, linked, maintainer-signed-off follow-up issue. The handle must name a real maintainer: `LedgerSkipCrossCheckAudit` rejects a placeholder such as `@maintainer` or `@m`, which has the shape of a sign-off while naming nobody. |

## Rules

- **Placeholder rows** (transports with no gateway configuration declared yet) occupy a single row
  per transport. Their cells may resolve only to `Deferred -> #NNNN (sign-off: @iancooper)` — never
  to `Pass` or `Fixed` — because no generated suite exists to pass.
- A behaviour is `Pass` or `Fixed` only when **both** the Reactor and Proactor variants pass against
  a running broker (FR-14). If only one variant passes, the cell must be `Deferred`.
- Five cells in the FR-2 column carry a pre-identified non-conformance annotation. GCP's four
  consumers redeliver immediately (the delay argument is ignored); RocketMQ's requeue is a no-op
  holding the message under the native invisibility timeout with no delay applied. These are seeded
  ahead of any generation run rather than discovered late.
- The cleanup gate is evaluated over all twelve targeted transports, not over whichever rows happen
  to exist.
- `AWS / SqsFifo` **and `AWS.V4 / SqsFifo`** FR-9 (delayed send) are `Fixed (7a0ec644f)` for #4390:
  SQS FIFO does not support per-message `DelaySeconds`, so both producers delegate positive FIFO
  delays to the configured `Scheduler`. Zero and null delays remain immediate sends; Standard queues
  continue to use native delays up to 15 minutes and the scheduler for longer delays. Both FIFO
  providers wire the generated `ConformanceHarnessMessageScheduler`, which waits before publishing
  the already-stamped message directly through `SqsMessageProducer`, preserving its group and
  deduplication identifiers. Reactor and Proactor delayed-send tests pass against Floci 1.5.19;
  this verification does not include real AWS. Before the fix, Floci delivered immediately rather
  than reproducing AWS's parameter rejection, and both variants failed the before-delay assertion.
  Requeue-with-delay (FR-2) remains native via `ChangeMessageVisibility` and is unchanged.
- `AWS / SnsStandard` **and `AWS.V4 / SnsStandard`** FR-9 (delayed send) are `Fixed (#4240)`: SNS has
  **no native delayed publish** — `SnsMessageProducer.SendWithDelay` delegates a non-zero delay to the
  `IAmAMessageProducer.Scheduler` seam (as Kafka does). Two changes were needed: (1) a localized `src`
  fix — the **sync** `SnsMessageProducer.SendWithDelay` dropped its `delay` argument (passed
  `TimeSpan.Zero` to the inner overload), so the Reactor path published immediately regardless of the
  requested delay; it now forwards `delay`, matching the async path and `SqsMessageProducer`. **The V4
  gateway (`src/Paramore.Brighter.MessagingGateway.AWSSQS.V4/SnsMessageProducer.cs`) is a separate file
  and carried the identical bug — the same one-line fix was applied there.** (2) A wired harness
  scheduler (`SnsHarnessMessageScheduler`, in-scope per the deferral preconditions) that honours the
  delay by wall-clock and re-publishes to the SNS topic once it elapses (the V4 test project got its own
  copy). Requeue-with-delay (FR-2) is `Pass` natively — it is consumer-side `ChangeMessageVisibility` on
  the subscribed SQS queue, not an SNS publish, so it needs no scheduler.
- `AWS / SnsFifo` **and `AWS.V4 / SnsFifo`** FR-9 (delayed send) are `Fixed (#4240)`. Like SQS FIFO,
  SNS FIFO delegates delayed sends to the `Scheduler` seam rather than using native per-message
  `DelaySeconds`. The harness scheduler re-publishes to the FIFO topic after
  the delay, and the delayed message keeps the FIFO `MessageGroupId`/`MessageDeduplicationId` that
  `FifoMetadataProducer` stamped, so the re-publish is a valid FIFO publish. Reuses the same
  `SnsMessageProducer` sync `SendWithDelay` src fix as `AWS / SnsStandard` (hence `Fixed`). FR-2 is
  `Pass` natively (consumer-side `ChangeMessageVisibility`, which FIFO supports). The SqsFifo dedup
  trap (a reused `DefaultMessageBuilder` yielding byte-identical messages) applies to SnsFifo too and is
  handled by the shared `FifoMetadataProducer` (constant group id + a unique dedup id per send, with
  content-based deduplication disabled on both the FIFO topic and queue so the explicit ids govern).
- `Redis / RedisMessagingGateway` FR-16 (Nack → redelivery) is `Deferred -> #4240`: Redis reads
  destructively via BLPOP/LPOP, so by the time a handler nacks a message it has already been popped and
  cannot be returned. `RedisMessageConsumer.Nack`/`NackAsync` are therefore documented **no-ops** (per
  ADR `0039`), and — because they neither redeliver nor clear the in-flight set — the next `Receive`
  throws `ChannelFailureException: Unacked message still in flight`. Both variants (Reactor + Proactor)
  fail identically. This is a genuine platform limitation, not a harness gap: faking redelivery in the
  test harness would mask a capability the gateway genuinely lacks. Contrast Kafka, where Nack leaves the
  offset uncommitted and the broker redelivers natively (FR-16 `Pass`). The other ten Redis behaviours
  conform: delay (FR-2/FR-9) via a wired `RedisHarnessMessageScheduler` (Redis delegates a non-zero delay
  to the scheduler seam, as Kafka does — `Pass`, harness-only); reject→DLQ/invalid + metadata
  (FR-4/5/6/8/17) via the Brighter-managed DLQ and invalid-message channels (ADR `0039`), with the
  invalid-channel read hook implemented in the provider (was a `Message.Empty` stub); and requeue/
  redeliver + no-channel ack (FR-7/15/22).
- `MSSQL / MSSQLMessagingGateway` FR-16 (Nack → redelivery) is `Deferred -> #4240` — **same root cause
  as Redis**. The MSSQL queue consumer reads destructively from the queue table (the row is removed on
  read), so `MsSqlMessageConsumer.Nack`/`NackAsync` are **no-ops** (`public void Nack(Message) {}`):
  the message is already gone and cannot be redelivered. The FR-16 arms therefore observe `MT_NONE`
  where the redelivered message is expected. Both variants fail identically. Genuine platform limitation,
  not a harness gap (contrast Kafka's uncommitted-offset native redelivery, FR-16 `Pass`). The other ten
  MSSQL behaviours conform: delay (FR-2/FR-9) via a wired `MsSqlHarnessMessageScheduler` (MSSQL delegates
  a non-zero delay to the scheduler seam, as Kafka/Redis do — `Pass`, harness-only); reject→DLQ/invalid +
  metadata (FR-4/5/6/8/17) via the Brighter-managed DLQ and invalid-message channels (ADR `0040`), whose
  provider read hooks were already real (no stub, unlike Redis); and requeue/redeliver + no-channel ack
  (FR-7/15/22).
- `PostgresSQL / PostgresMessagingGateway` is `Pass` on **all eleven** behaviours — notably FR-16
  (Nack → redelivery), which **contrasts with Redis and MSSQL** (both `Deferred` above). Postgres reads
  **non-destructively**: `PostgresMessageConsumer.Receive` leases a message by setting
  `visible_timeout = CURRENT_TIMESTAMP + VisibleTimeout` (it does not delete the row); `Acknowledge`
  performs the `DELETE`. So `Nack`/`NackAsync` — documented no-ops — genuinely redeliver: the visibility
  lease expires and the message becomes available again, exactly the mechanism the consumer's own comment
  describes. This is a real capability, not a faked one, so FR-16 is `Pass` (contrast Redis/MSSQL, whose
  destructive reads pop the message before the nack, and Kafka's uncommitted-offset native redelivery).
  Delay (FR-2/FR-9) is `Pass` **natively** with no scheduler — the producer and consumer bind the delay
  straight into `visible_timeout` arithmetic (`SendWithDelay` / `Requeue`), like AWS SqsStandard. The
  reject→DLQ/invalid + metadata behaviours (FR-4/5/6/8/17) needed **one harness-only fix**: the Postgres
  consumer keys its read on `ChannelName` (`WHERE "queue" = ChannelName.Value`), but the provider's
  DLQ/invalid read hooks created their read consumer with a random `DLQ-{uuid}`/`Invalid-{uuid}` channel
  name instead of the actual `DeadLetterRoutingKey`/`InvalidMessageRoutingKey` the rejection producer
  wrote to — so the hooks polled an empty queue and observed `MT_NONE`. Pointing the read-hook
  `channelName` at the rejection routing key (matching how the main `CreateSubscription` aligns
  `ChannelName` with the topic) makes all five behaviours `Pass`; the reject-to-DLQ routing itself was
  already conformant (Brighter-managed DLQ, ADR `0041`).
- #4388 adds separate `RMQ.Async / NativeClassic`, `RMQ.Async / NativeQuorum`, and
  `RMQ.Sync / NativeClassic` configurations. They use `SupportDelay = true` with no scheduler,
  while the original configurations retain scheduler coverage. Native FR-2 and FR-9 are fixed
  by queue-specific delayed requeue and clearing the consumed delay instruction. Like the original
  RMQ configurations, all three use native DLX routing for FR-8, with empty Brighter rejection keys.
  The generated native suites pass on the pinned RabbitMQ 4.2.6 / plugin 4.2.0-rc.1 broker with Mnesia
  (84 async and 42 sync generated cases on .NET 10). Focused tests also cover subscriber isolation,
  exchange-type preservation, nonpositive delays, and Validate/Assume retry provisioning.
  See [native test setup](../../docker/RabbitMQ/README.md). The historical plugin-retirement notes
  below describe the scheduler rollout; these supplementary configurations restore coverage of the
  existing native API without changing the stock-broker suites or claiming RabbitMQ 4.3+ support.
- `RMQ.Async / Classic` is `Pass` on **ten** behaviours and `Fixed (#4387)` on **FR-5**
  (a separate invalid channel). **⚠️ Reference-environment fix first:** `docker-compose-rmq.yaml` pointed
  at `rabbitmq:management` (now RabbitMQ 4.3), which **hard-rejects the transient non-exclusive queues the
  gateway declares** (`INTERNAL_ERROR - Feature 'transient_nonexcl_queues' is deprecated`) — every test
  failed at queue declaration. Pinned it to the CI/reference image
  (RabbitMQ 4.2, matching CI and the root `docker-compose.yaml`); a fresh data volume is required
  (downgrading 4.3→4.2 over a stale volume crashes the broker on boot). That image is now the **stock**
  `rabbitmq:4.2-management` rather than `brightercommand/rabbitmq:4.2-management-delay`: the
  delayed-message plugin is being retired upstream and the suite never asked for it, so the pins moved to
  stock images and the one plugin-dependent test was retired (see "The delay plugin is retired" below).
  The 4.2 pin itself is unchanged — the `transient_nonexcl_queues` rejection on 4.3 was a product
  forward-compatibility defect, tracked as
  [#4355](https://github.com/BrighterCommand/Brighter/issues/4355) and **since fixed for `RMQ.Async`**: its
  `RmqSubscription` now defaults `isDurable` to `true`, so a default subscription declares a queue 4.3 will
  accept. **Measured against a real 4.3.5 broker: all 80 generated `RMQ.Async` conformance tests pass**, and
  the run is unchanged on 4.2 (145 passed, 6 skipped, 0 failed). `RMQ.Sync` keeps `isDurable: false` by
  decision — it targets the RabbitMQ 3.x line through `RabbitMQ.Client` 6.x, and 3.x permits transient
  non-exclusive queues. **The consequence of that decision, measured so it is not mistaken for a
  regression: the `RMQ.Sync` suite does not run on a 4.3+ broker at all.** Against a real 4.3.5 server it
  is **46 failed / 35 passed / 3 skipped**, every failure on `transient_nonexcl_queues` at queue
  declaration. That is expected, and it is not confined to the tests — `RmqSubscription.isDurable` still
  defaults to `false` in the `RMQ.Sync` package, so a *default* subscription from that package is rejected
  by 4.3+ as well. `RMQ.Sync`'s baseline of **81 passed / 3 skipped / 0 failed** is on the pinned 4.2
  broker in `docker-compose-rmq.yaml`, which is what `rabbitmq-sync-ci` runs and where it is green; do not
  compare a 4.3 run against it. Should `RMQ.Sync` ever need to support 4.3+, the fix is the one #4355
  applied to `RMQ.Async` — move the `isDurable` default to `true` and follow it through the consumer
  construction sites, creator and reader together, since a queue cannot be redeclared under a different
  durability. The **hand-written** `RMQ.Async` suite now runs clean on 4.3 as well. **22 of its
  tests failed there**, every one on `transient_nonexcl_queues`, because they opted into a transient queue
  explicitly rather than inheriting the product default: `QueueFactory`'s own `isDurable: false` default in
  `TestHelpers.cs` fed 16 of them, and the rest passed `false` straight to `RmqMessageConsumer`. That default
  and **23 consumer construction sites** now say durable. **Measured on a real 4.3.5 broker: 145 passed, 6
  skipped, 0 failed** — the same numbers the suite has always had on 4.2, where it is unchanged (`RMQ.Async`
  145/6/0, `RMQ.Sync` 81/3/0). Three of the 23 are mTLS acceptance tests the CI filter excludes: they carry
  the identical defect and the identical fix, but **were not executed** (they need a separate mTLS broker and
  `RMQ_MTLS_ACCEPTANCE_TESTS=true`). Two `isDurable: false` sites stay by design — the unit test asserting a
  subscription can still opt out, and the quorum-queue validation test where a non-durable subscription *is*
  the exception being asserted — and four `_receiver` sites keep theirs because their test doubles throw in
  `EnsureChannelAsync` before any declare reaches the broker. ⚠️ **The 4.2 pin in `docker-compose-rmq.yaml` is
  unchanged**: it matches what CI runs, and moving it is a separate decision. **Requeue/redeliver + no-channel ack (FR-7/15/16/22) `Pass` natively** — notably
  **FR-16** (`RmqMessageConsumer.NackAsync` → `BasicNackAsync(requeue: true)` → the broker redelivers,
  contrast Redis/MSSQL `Deferred`). **Delay (FR-2/FR-9) `Pass` via a wired `RmqHarnessMessageScheduler`** —
  the gateway delegates a non-zero delay to `IAmAMessageProducer.Scheduler` when `DelaySupported == false`
  (the same scheduler seam proven for Kafka/Redis/MSSQL); the harness presents a plain (non-delay) exchange
  so this seam is exercised, yielding conformant semantics (delivered `Header.Delayed == TimeSpan.Zero`,
  honoured delay). The **native** `x-delayed-message` plugin path is deliberately not used because it is not
  yet conformant — `RmqMessagePublisher.RequeueMessageAsync` hardcodes `TimeSpan.Zero` and publishes to the
  default exchange, dropping a requeue delay (FR-2 would redeliver immediately), and a plugin-delivered send
  arrives carrying `Header.Delayed == delay`, tripping the universal message-equivalence assertion; both are
  larger src fixes tracked under #4240. **Reject→DLQ (FR-4/6/17) and metadata (FR-8) `Pass` via the native
  DLX under the FR-8 relaxation.** RMQ's rejection path is a native `BasicReject` that dead-letters through
  the single configured DLX (`x-dead-letter-routing-key`); the message reaches the DLQ (evidence: live 4.2
  broker, both variants, `Assert.NotEqual(MT_NONE, dlqMessage)` passes), but `BasicReject` moves the
  *untouched original* message (the AMQP frame carries only delivery-tag + requeue), so the gateway stamps
  **no** Brighter rejection metadata and the provider's `RejectionMetadataKeys` are empty. Per the
  maintainer-approved **FR-8 relaxation** (see `decision-log.md`), a transport that dead-letters via a
  native broker mechanism is conformant on **routing alone**: the canonical templates assert DLQ arrival
  unconditionally and guard the rejection-metadata sub-assertions on
  `RejectionMetadataKeys.StampsRejectionMetadata` (true iff the provider declares non-empty keys).
  ⭐ **This paragraph has an executable counterpart.** `RejectionMetadataContractAudit` names the four
  configurations allowed to claim FR-8 on routing alone — `RMQ.Async / Classic`, `RMQ.Async / Quorum`,
  `RMQ.Sync` and `AzureServiceBus` — and fails the build if a fifth acquires empty keys while claiming
  FR-8, or if one of the four starts stamping metadata and this note goes stale. **Measured: 24 providers,
  8 with all-empty keys.** So FR-4/6/8/17 `Pass` for RMQ (routing) while metadata-stamping transports
  (SQS/Redis/Postgres/MSSQL/**GCP ×4**, ADRs `0038`/`0039`/`0040`/`0041`/**0078**) still assert the full
  metadata — **GCP's four consumers joined this group 2026-09-28** (spec 0037 task 5.11): `GcpRejectionRouter`
  stamps the real `RejectionMetadataKeyNames` keys via ADR 0078's `RejectionMetadataKeys` implementation
  in each provider (`:588-595`), so GCP was never a routing-only candidate for the audit's declared-relaxation
  set above — see the dated evidence note under the GCP FR-23 paragraph below for the full cell move.
  **FR-5 is `Fixed (#4387)`**: the subscription exposes an
  invalid routing key, and unacceptable messages are forwarded with publisher confirmations before
  the original is acknowledged. Other rejection reasons and the fallback when no invalid destination
  is configured retain the native DLX behavior. The generated FR-5 test always asserts invalid-channel
  arrival and DLQ absence; dedicated gateway tests additionally assert all five metadata fields on the
  Brighter-managed invalid route. The empty provider keys still describe the native DLQ route.
- `RMQ.Async / Quorum` mirrors `RMQ.Async / Classic` exactly: **10 `Pass` + FR-5 `Fixed (#4387)`**.
  Quorum queues use the same RabbitMQ AMQP gateway (`src/Paramore.Brighter.MessagingGateway.RMQ.Async`),
  so every conformance argument that applies to Classic applies to Quorum. **Delay (FR-2/FR-9) `Pass` via
  the same wired `RmqHarnessMessageScheduler`** — the Quorum provider presents a plain (non-delay) durable
  exchange so `DelaySupported == false` and the scheduler seam is exercised (delivered `Header.Delayed ==
  TimeSpan.Zero`, honoured delay; same as Classic). **Reject→DLQ (FR-4/6/17) and metadata (FR-8) `Pass`
  via the native DLX under the FR-8 relaxation** — Quorum queues support `x-dead-letter-exchange` /
  `x-dead-letter-routing-key` identically to Classic queues; `RejectionMetadataKeys` is empty → routing
  only asserted (same as Classic). **FR-16 `Pass`** — `RmqMessageConsumer.NackAsync` → `BasicNackAsync(requeue:
  true)` → broker redelivers, same mechanism as Classic. **FR-7/15/22 `Pass`** natively. **FR-5 is `Fixed (#4387)`** via the same confirmed
  invalid-message forwarding as Classic.
- `RocketMQ / RocketMQMessagingGateway` — **9 `Fixed (#4240)` + FR-2 / FR-15 `Deferred -> #4240`**, both
  variants, on a live RocketMQ 5.5.0 broker (Reactor + Proactor each **21 pass / 2 skip / 0 fail**).
  Every passing cell is `Fixed` (not `Pass`) because of a required `src` fix: `RocketMqMessageProducer`
  guards the CloudEvents `Baggage` property — an empty baggage's `ToString()` is `""` and RocketMQ
  `AddProperty` throws on empty, which crashed **every** send (the shared `DefaultMessageBuilder` yields
  an empty baggage); it is now added only when non-empty.
  - **⭐ FR-9 (delayed send) `Fixed`, NOT deferred** — RocketMQ honours delay **natively** via the
    broker timer wheel: for a `Delay`-type topic the producer sets `SetDeliveryTimestamp(now + delay)`
    (RocketMQ 5.x timer/scheduled messages). No scheduler seam is needed (contrast Kafka/Redis/SNS). The
    prior belief that FR-9 was a gateway gap was an **infrastructure** artifact — the reference compose's
    `create-topic` service hard-codes a `rocketmq-5.4.0` mqadmin path against a `5.5.0` image, so the
    `DELAY`-typed topics were silently never created; with them created (and `timerWheelEnable=true`,
    the broker default), delayed send delivers after the delay. `FR-9 delayed-requeue sibling`
    (`with_delay_should_receive_message_again`) also uses this native path (its topic is `Delay`-typed).
  - **FR-4/5/6/8/17 (reject → DLQ / invalid + metadata) `Fixed`** — RocketMQ has real DLQ **and** a
    distinct invalid channel (both Brighter-managed via reject-time re-publish to the routing key), and
    real `RejectionMetadataKeys` (`originalTopic`/`originalMessageType`/`rejectionReason`/…), so the
    FR-8 metadata sub-assertions run for real (contrast RMQ's empty-keys relaxation). The reject path
    rewrites `Header.Topic` to the DLQ/invalid topic (so the message routes there) while preserving the
    source in the `originalTopic` bag entry; the requeue-exhaustion test's full-message assertion
    compares against that preserved entry.
  - **FR-7/16/22 (no-channels ack / nack redelivery / plain requeue) `Fixed`** — plain requeue and nack
    are no-ops that rely on the invisibility lease (RocketMQ enforces a **10 s minimum**); the message
    redelivers when the lease expires, which the canonical retry loops (30 s ceilings) observe.
  - **⛔ FR-2 (requeue *with delay*) `Deferred -> #4240`** — `RocketMessageConsumer.Requeue` is a no-op
    (`ChangeInvisibleDuration(view, TimeSpan.Zero)` commented out pending an upstream RocketMQ C# client
    release), so a requeued message redelivers at the fixed ~10 s invisibility **regardless of the
    requested delay** — the delay is never honoured. The canonical FR-2 test can pass *by accident*
    (10 s falls inside its 2 s–30 s window) but the capability is genuinely absent, so it is a
    maintainer-signed `Deferred` (do-not-chase-a-green rule). Three deferral preconditions met.
  - **⛔ FR-15 (explicit zero-delay requeue, redeliver within 5 s) `Deferred -> #4240`** — same upstream
    cause: the no-op requeue can only redeliver via the 10 s invisibility lease, so redelivery within the
    asserted 5 s is impossible (`ChangeInvisibleDuration(view, TimeSpan.Zero)` is exactly the commented-out
    call that would fix it). Genuine failure, three preconditions met.
  - **Harness (test-project) adaptations, no non-Baggage `src` change**: `rq_delay`/`exhaust` topics use
    a longer consumer poll so the genuine ~10 s invisibility redelivery is observed by their single-poll
    receive arms (the redelivery is real; only the observation window is widened — delay/FR-9 topics keep
    the short 2 s poll so their before-`D` arm still yields `MT_NONE`); the delayed-requeue sibling's topic
    is `Delay`-typed to use native delivery-timestamp instead of the unwired Scheduler seam; and the
    assertion compares the source topic via the preserved `originalTopic` bag entry for dead-lettered
    messages. **Reference-env fix**: `docker-compose-rocketmq.yaml` broker/proxy heap raised
    (`-Xmx128m`/`-Xmx64m` → `2g`/`1g`) so the broker sustains the suite instead of degrading under load.
  - ⭐ **Run record (2026-10-02, spec 0037 task 7.30, AC-38, R-22, R-23): the nine `Fixed` cells still pass on
    the AC-24 branch.** The store was reset (`docker-compose -f docker-compose-rocketmq.yaml down -v; up -d`, all
    100 compose topics created), then the whole RocketMQ test project ran at `52523c32f`'s code (net10.0): **67
    passed / 0 failed / 6 skipped**. Every generated test for the nine cells passed in both variants: FR-4
    (`delivery_error_should_send_to_dlq`), FR-5 (`unacceptable_reason_should_send_to_invalid_channel`), FR-6
    (`unacceptable_and_no_invalid_channel_should_fallback_to_dlq`), FR-7 (`no_channels_configured`), FR-8
    (`should_include_metadata`), FR-9 (`sending_a_delayed_message`), FR-16 (`nacking_a_message_it_should_be_redelivered`
    and `nacking_first_of_two_messages`), FR-17 (`unknown_reason_should_send_to_dlq`) and FR-22
    (`requeuing_a_failed_message_should_be_redelivered`). FR-16 and FR-22 still pass with 7.10's broker-count
    `Resolve` in the consumer: the redelivery comes from the invisibility lease. The 6 skips are FR-2 and FR-15
    (`Deferred -> #4240`), both variants, plus the two Skip-marked facts in 7.1's measurement fixture. FR-23 (`Fixed (#4353)`, 7.13)
    passed in the same run. **A watch item:** the FR-9 Proactor test
    (`When_sending_a_delayed_message_should_deliver_after_delay_async`) passed here but has failed twice on a
    clean store this month (in 7.11's run on 2026-10-01, and earlier on `6b19023fa`): the message was received
    inside the test's 2 s "before delay" window. Not investigated.
- `MQTT / MqttMessagingGateway` — **10 `Fixed (#4240)` + FR-16 `Deferred -> #4240`**, both variants,
  on a live Mosquitto broker. **Evidence run with every cell un-skipped** (the state that earned the FR-16
  deferral): Reactor **14 pass / 2 fail (FR-16)** + Proactor **16 pass / 2 fail (FR-16)** for the generated
  canonical suite. **Final certified state, FR-16 now carrying its Deferred Skip**: the scoped
  `~MessagingGateway` suite is **55 pass / 4 skip / 0 fail** (the 4 skips = FR-16's two arms × both
  variants); all pre-existing non-generated tests pass. Every passing cell is
  `Fixed` (not `Pass`) because of required `src` fixes: (1) `MQTTMessageConsumer.Receive` was an
  immediate no-wait return when the internal queue was empty — it now polls in 10 ms increments until a
  message arrives or the timeout elapses, matching the contract other transports implement; (2)
  `MQTTMessageProducer.SendWithDelay` did not call `BrighterTracer.WriteProducerEvent`, so the producer
  span was never propagated to `message.Header.TraceParent` — the call was added, matching the Redis /
  Kafka producer pattern. Harness additions (test-project only): `MqttHarnessMessageScheduler` for
  delayed-requeue (FR-2) and delayed-send (FR-9), same wall-clock timer + republish pattern as
  Redis/RMQ/MSSQL; subscription `BufferSize = 5` so the Channel wrapper can buffer the 4 messages from
  the multi-message test without overflowing its internal queue (Channel's hard cap is 10); scheduler
  wired into `CreateProducer` (FR-9) and into the consumer factory (FR-2).
  - **⛔ FR-16 (Nack → redelivery) `Deferred -> #4240`** — MQTT is pub/sub with no acknowledgment
    concept. `MqttMessageConsumer.Nack` is a documented no-op. Messages are received from an in-memory
    `ConcurrentQueue<Message>` populated by the `ApplicationMessageReceivedAsync` event handler; once
    dequeued on `Receive`, the message is gone. MQTT QoS 1 delivers to connected subscribers exactly once
    with no broker-side requeue path. Both arms of FR-16 (`When_nacking_a_message_it_should_be_redelivered`
    and `When_nacking_first_of_two_messages_should_redeliver_nacked_then_receive_second`) observe `MT_NONE`
    after nack on a 30 s ceiling — same root cause as Redis (destructive BLPOP) and MSSQL (row deleted on
    read). Three deferral preconditions met: evidence recorded (live Mosquitto broker, both variants),
    fix is not localized (requires a redelivery buffer or QoS-level redesign in `src`), maintainer sign-off.
- `RMQ.Sync / RmqSyncMessagingGateway` — **10 `Fixed (#4240)` + FR-5 `Fixed (#4387)`**, both
  variants, on a live RabbitMQ 4.2 broker with management + delay plugin image (Reactor + Proactor each
  **19 pass / 1 skip / 0 fail** in the canonical generated suite). Every passing cell is `Fixed` (not
  `Pass`) because of a required `src` fix: `RmqMessageProducer.DisposeAsync()` created a
  `TaskCompletionSource` that was never completed and returned `new ValueTask(tcs.Task)` — so `await
  producer.DisposeAsync()` hung indefinitely; it now returns `ValueTask.CompletedTask`. **Delay
  (FR-2/FR-9) `Fixed` via a wired `RmqSyncHarnessMessageScheduler`** — the harness presents a plain
  (non-delay) exchange so `DelaySupported == false` and the scheduler seam is exercised (wall-clock
  timer re-publishes the same message object, preserving the original ID via the
  `OriginalMessageIdHeaderName` bag entry, asserted by `RmqMessageAssertion`). **Reject → DLQ
  (FR-4/6/17) and metadata (FR-8) `Fixed` via native DLX under the FR-8 relaxation** — `BasicReject`
  dead-letters to the configured DLX; `RejectionMetadataKeys` is empty → routing only asserted (same
  mechanism as RMQ.Async). **FR-15 (explicit zero-delay requeue) `Fixed`** — `RequeueMessage`
  republishes with a new AMQP message ID (original stored in `OriginalMessageIdHeaderName`); asserted
  correctly by `RmqMessageAssertion`. **FR-16/22 `Fixed`** natively. **FR-5 is `Fixed (#4387)`** via
  confirmed invalid-message forwarding. A mandatory return or failed confirmation leaves the original
  unacknowledged. The native DLX path remains in use for other rejection reasons and fallback.
  - **Test-isolation fix (harness, required):** the generated suite originally declared its own xUnit
    collection (`RmqSyncMessagingGateway`) while the hand-written broker tests use `RMQ`. **xUnit runs
    distinct collections in PARALLEL**, so the two suites hit the same broker concurrently and two tests
    failed only in the combined run (the generated FR-8 Proactor arm, and the pre-existing hand-written
    `RmqMessageProducerRequeuingMessageTests.When_posting_a_message_via_the_messaging_gateway`) while
    both passed in isolation. `CollectionName` is now `RMQ`, serialising generated + hand-written against
    the one broker. ⚠️ **RMQ.Async has the same latent split** (`Classic`/`Quorum` vs `RMQ`) but was
    certified on a configuration-scoped filter (`~MessagingGateway.Quorum.`) that never ran the
    hand-written tests alongside, so it never surfaced.
  - **⚠️ Scoped-suite caveat — 9 pre-existing mTLS failures are NOT from this work.** The full
    `~MessagingGateway` filter is **78 pass / 3 skip / 9 fail**; all 9 failures are
    `RmqMutualTls{Acceptance,Observability,QuorumObservability}Tests`, which need a TLS-configured broker
    (certs, port 5671) that `docker-compose-rmq.yaml` does not provide — they fail in <1 ms.
    **Verified pre-existing**: a baseline run at commit `b1a5027d0` (before any RMQ.Sync onboarding) fails
    with the identical 9 mTLS tests. After the collection fix there are **zero non-mTLS failures**. The
    conformance conclusion rests on the generated canonical suite, which is fully green apart from the
    FR-5 Deferred skips.
- `AzureServiceBus / AzureServiceBusMessagingGateway` — **9 of 11 cells `Pass`, verified in CI against a
  real namespace (2026-09-07). FR-5 and FR-9 remain `Deferred -> #4240 (sign-off: @iancooper)`.**
  - **How this was established.** Probe PR #4317 (closed unmerged, as #4308 was) flipped all 11 cells so
    the generator emitted no `Skip`, applied the #4309 fix, and merged the #4310 fix so the run measured
    the right thing. `azure-ci` job `101853410391` against baseline run `34127954869`: the failing set
    went from **30 distinct tests to 6**. The 26-strong `MT_NONE` wave collapsed to 2, and the 2
    `UriFormatException` failures went to 0.
  - ⚠️ **The earlier "ALL ELEVEN Deferred on INFRA grounds" note was superseded, and its stated reason
    was wrong twice over.** It said no ASB behaviour "has been observed at all" because credentials are
    unset. That is true locally but **not in CI**: `.github/workflows/ci.yml` supplies
    `BrighterTestsASBConnectionString` from `secrets.BRIGHTERTESTS_ASB_CONNECTION_STRING`. The follow-up
    correction then read the resulting 30 failures as "cannot complete a plain round trip", which was
    the right call on the evidence then available but the wrong cause: the round trip was broken by
    **#4309**, a test-harness ordering defect, not by anything about ASB's behaviour.
  - **Why the round trip was failing — #4309.** `AzureServiceBusChannelFactory` provisions nothing, and
    the ASB subscription is created lazily by the consumer's first receive. Every generated test sends
    *before* it first receives, and an ASB topic with no subscription attached silently discards the
    message. `AzureServiceBusMessageGatewayProvider` now provisions the topic and subscription before
    handing out a channel — which is what the hand-written ASB tests have always done. ⚠️ Whether the
    **gateway** should do this eagerly (it receives `MakeChannels` and ignores it, unlike AWS and GCP) is
    deferred to **ADR 0066 / PR #4259**, the Infrastructure Provisioner. The fix here is harness-side by
    decision, not by oversight.
  - **FR-5 (reject → invalid channel) stays `Deferred`, and is expected to.** ASB dead-letters natively:
    the DLQ is the built-in sub-queue and **there is no separate invalid-message channel** — the
    provider's own doc comment says so, and `CreateSubscription` records that `invalidMessageRoutingKey`
    has "no structural effect on the ASB subscription itself". This is a genuine platform difference, not
    an unfixed defect. Tracked under #4240.
  - **FR-9 (delayed send) stays `Deferred` — a REAL, newly-visible defect. See #4318.** It was masked
    twice: first by #4309, then by the `UriFormatException` of #4310. With both cleared, the failure is
    `Assert.Equal() Failure: Expected MT_NONE, Actual MT_EVENT` on the **before-delay** arm — i.e. a
    message sent with a 5 s delay is **available within 2 s**. The delay is not honoured.
    ⚠️ **No existing test could ever have caught this**: `FakeServiceBusSenderWrapper.ScheduleMessageAsync`
    discards `scheduleEnqueueTime` and calls `Send(message)`, so every hand-written delayed-send test
    passes identically whether or not the gateway schedules anything.
  - **Broker attempt and why it failed.** ASB is a cloud service with no container story in this repo:
    there is **no `docker-compose-*asb*.yaml`**, the credentials `ASBCreds.cs` requires
    (`BrighterTestsASBConnectionString` / `BrighterTestsASBNameSpace`) are **both unset**, the `az` CLI
    is **absent**, and no Service Bus emulator container is present. `ASBCreds.ASBClientProvider` throws
    at runtime when neither env var is set, so every canonical test would fail on client construction
    rather than on a behaviour.
  - **The wiring is real and ready to flip.** Task 54 (`b70db98e9`) landed a genuine
    `AzureServiceBusMessageGatewayProvider` implementing both provider interfaces, and the 22 canonical
    tests generate. The DLQ read hook is a **real bounded read** — a `ServiceBusReceiver` with
    `SubQueue.DeadLetter` polling the built-in `$DeadLetterQueue` entity — not a `Message.Empty` stub.
  - **`RejectionMetadataKeys` are all `string.Empty` — a native-DLQ declaration, NOT an unfilled stub.**
    `AzureServiceBusConsumer.Reject` dead-letters natively via
    `ServiceBusReceiver.DeadLetterAsync(lockToken, reason, description)` and stamps no Brighter bag keys,
    exactly as RMQ does. Under the maintainer-approved **FR-8 relaxation** a native-dead-letter transport
    is conformant on **routing alone**. ⚠️ This is the same empty-keys situation the GCP guardrail flagged,
    but the opposite reading applies here because the native dead-letter path is real and verified in
    source — contrast GCP, where `Reject` == `Acknowledge` (discards).
  - **⚠️ The `dotnet test` leg of this row's RALPH-VERIFY cannot pass on a machine without ASB
    credentials, and did not pass BEFORE this work either.** Measured: a baseline run at commit
    `32d1a6f9f` (before ASB onboarding) is **112 pass / 10 fail / 0 skip** — the project's hand-written
    broker tests (`ASBConsumerTests`, `ASBProducerTests`, `LargeAsbMessageProducerTests`) already threw
    `ASB ConnectionString or Namespace not set`. After onboarding it is **118 pass / 18 fail / 24 skip**:
    the **22 canonical tests are correctly skipped** carrying their `Deferred: #4240` markers, and the 8
    added failures are the generator's *non-canonical* companions (basic post/receive, multi-message,
    multi-thread post, activity-context) which are not FR-mapped and so take no ledger-driven Skip —
    they fail on the identical missing-credentials exception, not on any behaviour.
    ⚠️ **That last clause holds only where the credentials are absent.** In CI, where they are supplied,
    the same companions fail on `MT_NONE` and on `409 (Conflict)` — see the correction above.
    **Precedent**: this is the same resolution as `GCP / Stream` + `/ StreamOrdering`, where the scoped
    suite could not be run locally and **the no-`Unknown` ledger is the gate**, with `dotnet test`
    verification deferred to real infrastructure.
  - **To close these deferrals**: supply `BrighterTestsASBConnectionString` (or
    `BrighterTestsASBNameSpace`) against a real namespace, or stand up the Service Bus emulator, flip the
    cells, and regenerate. No code change is expected to be needed to *run* — only to fix whatever the
    run then reveals.
- `Kafka / Classic` — **row renamed from `Kafka / Standard`.** Upstream `master` renamed the Kafka
  gateway configuration key `Standard` -> `Classic` (KIP-848 work, PR #4233) and renamed
  `KafkaMessageGatewayProvider` -> `KafkaClassicMessageGatewayProvider`. The row is the same
  configuration and the same certified result (11/11 `Pass`); only the name follows the config.
- `Kafka / Consumer` — **ALL ELEVEN cells `Pass`**, both variants, on the reference Kafka broker
  (`apache/kafka:4.0.2`, the image `docker-compose-kafka.yaml` and `ci.yml` both use). Scoped suite
  `~MessagingGateway.Consumer.` = **34 pass / 0 skip / 0 fail**. This configuration arrived from upstream
  `master` (KIP-848 consumer group protocol, PR #4233) after the conformance templates had already landed,
  so it needed onboarding rather than fixing. **Harness-only — no `src` change**, hence `Pass` not `Fixed`:
  `KafkaConsumerMessageGatewayProvider` was a near-clone of the pre-conformance Classic provider, so the
  resolved Classic provider was ported across verbatim (DLQ + invalid routing keys, the real invalid-channel
  read hook, `RejectionMetadataKeys`, the wired `KafkaHarnessMessageScheduler`), keeping only the one
  behavioural difference: `GroupProtocol = new ConsumerGroupProtocol()` on the subscription. The row
  therefore mirrors `Kafka / Classic` exactly, which is the expected outcome — the two configurations share
  `KafkaMessageConsumer`/`KafkaMessageProducer` and differ only in group coordination.
  - **The new group protocol is genuinely exercised, not silently downgraded.** The broker advertises
    `ConsumerGroupHeartbeat(68)` / `ConsumerGroupDescribe(69)` as usable, and polling
    `kafka-consumer-groups.sh --list --type` during a Consumer-configuration run observes groups of type
    **`Consumer`** (KIP-848), not `Classic`. This was checked explicitly because a librdkafka fallback to
    the classic protocol would have certified Classic behaviour twice under a second row.
  - `CollectionName` is `Kafka` for all three Kafka configurations, so the generated suites and the
    hand-written broker tests serialise against the one broker (the xUnit cross-collection parallelism
    trap recorded under `RMQ.Sync`).
  - Full `Paramore.Brighter.Kafka.Tests` project: **188 pass / 0 fail** with both `kafka` and
    `schema-registry` up. ⚠️ Running `kafka` alone fails the two hand-written
    `KafkaMessageProducerHeaderBytesSendTests` arms on `Connection refused (localhost:8081)` — they need
    the schema registry; that is infra, not conformance.
- `GCP / Stream` **and `GCP / StreamOrdering`** FR-16 (Nack redelivers) are `Fixed (#4449)`
  (2026-09-30, bugfix 0024). The Stream consumer's `Nack`/`NackAsync` were no-ops. On a stream that
  left the `SubscriberClient` callback parked: the message was never redelivered, and at the default
  flow control of one the whole subscription stalled. `Nack` now replies `Nack` to the client
  (`GcpStreamMessage.Reject()`, the same path as `Requeue`), which releases the message for immediate
  redelivery. `GCP / Pull` and `/ PullOrdering` FR-16 stay `Deferred`: Pull `Nack` is still a no-op that
  waits out the ack deadline, recorded as a follow-up.
  ⚠️ **`GCP / StreamOrdering`'s two-message fact is intermittent on the Pub/Sub emulator.** Two messages
  share one ordering key, and the first is nacked. In about 5 of 12 probe runs, the second message was
  acked and the nacked one was never redelivered within 60 s; the acked second message came back at
  about the client's 60 s lease instead. Brighter's call path is the same in every run, so this looks
  like the emulator's ordering-key redelivery, not the fix. That is **UNVERIFIED** until it is run
  against real GCP, which is pending the non-emulator run from #4321. `GCP / Stream` (no ordering)
  passed 20/20. CI is unaffected: `ci.yml` excludes `Category=GcpPubSubStream` and
  `GcpPubSubStreamOrdering`, so it runs no Stream tests. Evidence is in
  `bugfixes/0024-gcp-stream-nack-no-redelivery/bugfix.md`.

## FR-23 — requeue budget exhausted to DLQ

Added after review of PR #4297 noticed a coverage regression: retiring the legacy
`When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue` template removed the only
coverage of a message reaching the dead-letter queue by **exhausting its delivery budget**, and none
of the other canonical behaviours replaced it.

That is distinct from FR-4. FR-4 is an explicit `Reject`; FR-23 never rejects anything — the budget
runs out on its own, which is the behaviour [ADR 0040](../../docs/adr/0040-mssql-dlq-brighter-managed.md)
and [ADR 0046](../../docs/adr/0046-kafka-dlq-producer-for-requeue.md) actually specify. A gateway that
redelivered for ever would pass every other DLQ behaviour in this suite.

**The column opened with all 24 cells `Deferred`**, and cells move to `Pass` only on evidence — a
`Pass` recorded without a broker run is exactly what this ledger exists to prevent.

- **`Redis / RedisMessagingGateway` is `Pass`**, both variants green against a live Redis
  (`docker-compose-redis.yaml`), satisfying the both-variants rule (FR-14).
- **All three `Kafka` configurations are `Pass`**, both variants each, against a live broker
  (`docker-compose-kafka.yaml`).
- **`MSSQL / MSSQLMessagingGateway` and `PostgresSQL / PostgresMessagingGateway` are `Pass`**, both
  variants each, against `docker-compose-mssql.yaml` and `docker-compose-postgres.yaml`.
- **`RMQ.Async / Classic` and `RMQ.Async / Quorum` are `Pass`**, both variants each, against
  `docker-compose-rmq.yaml`. These two are the first configurations to reach the DLQ by the
  **broker's** own dead-lettering rather than a Brighter-side republish, and they are what exposed
  the two transport-specific assumptions described below.
- **`RMQ.Sync / RmqSyncMessagingGateway` is `Pass`**, both variants green against the same broker
  `docker-compose-rmq.yaml` stands up. It shares the DLX mechanism with the two `RMQ.Async`
  configurations, so it lands on the loosened identity and delivery-count bounds described below. It
  needed no broker the async pair did not already need: the conformance suite never asks for the
  delayed-message plugin, so a stock `rabbitmq:*-management` image serves all three RMQ cells.
- **The other 15 remain `Deferred`, and two different things hold them.** **Ten** are blocked on a
  product defect rather than on a broker run: the eight `AWS` and `AWS.V4` cells on
  [#4341](https://github.com/BrighterCommand/Brighter/issues/4341), `MQTT` on
  [#4351](https://github.com/BrighterCommand/Brighter/issues/4351), and `RocketMQ` on
  [#4353](https://github.com/BrighterCommand/Brighter/issues/4353). Re-running those changes nothing
  until the defect is fixed. Of the remaining six, **`GCP` ×4 and `AzureServiceBus` cannot be run
  against local infrastructure at all** — GCP's emulator implements neither of the two APIs the DLQ
  path needs (see below), and ASB has no emulator — so `gcp-ci` and `azure-ci`, which run against real
  cloud projects, are the only evidence that can move those five. So **no** FR-23 cell is still
  reachable by a local broker run: all 15 are either blocked on a product defect (10) or reachable only
  through CI against real cloud infrastructure (5).
  *Superseded for the eight `AWS` / `AWS.V4` cells by the 2026-09-27 evidence note, "The eight AWS
  cells are `Fixed (#4341)`", below: #4341 is fixed and those cells now pass.*

### Run record — 2026-09-26 (tasks 3.1–3.4 regenerated tree, AC-26, R-22, R-23)

Regenerated tree audit: `dotnet test tests/Paramore.Brighter.Test.Generator.Tests -f net10.0` — **307 pass / 0 fail**. `git status --short` after `./generate-test.sh` reported no diff (tree was committed with each task as required).

**FR-23 — all nine already-conformant configurations re-verified against the regenerated templates:**

| Configuration | compose file | Generated suite (Reactor+Proactor) | FR-23 Reactor | FR-23 Proactor |
|---|---|---|---|---|
| Redis / RedisMessagingGateway | `docker-compose-redis.yaml` | 71 pass / 0 fail / 4 skip | Pass | Pass |
| Kafka / Classic | `docker-compose-kafka.yaml` | *(see Kafka row below)* | Pass | Pass |
| Kafka / Consumer | `docker-compose-kafka.yaml` | *(see Kafka row below)* | Pass | Pass |
| Kafka / PartitionKey | `docker-compose-kafka.yaml` | *(see Kafka row below)* | Pass | Pass |
| Kafka (all three, combined) | `docker-compose-kafka.yaml` | 118 pass / 0 fail / 0 skip | — | — |
| MSSQL / MSSQLMessagingGateway | `docker-compose-mssql.yaml` | 141 pass / 0 fail / 4 skip | Pass | Pass |
| PostgresSQL / PostgresMessagingGateway | `docker-compose-postgres.yaml` | 55 pass / 0 fail / 0 skip | Pass | Pass |
| RMQ.Async / Classic | `docker-compose-rmq.yaml` | *(see RMQ.Async row below)* | Pass | Pass |
| RMQ.Async / Quorum | `docker-compose-rmq.yaml` | *(see RMQ.Async row below)* | Pass | Pass |
| RMQ.Async (Classic+Quorum, combined) | `docker-compose-rmq.yaml` | 80 pass / 0 fail / 4 skip | — | — |
| RMQ.Sync / RmqSyncMessagingGateway | `docker-compose-rmq.yaml` | 62 pass / 0 fail / 3 skip | Pass | Pass |

FR-23 passes on all nine; no Pass/Fixed cell regressed. The RMQ republish path's `x-original-message-id` fallback (from 3.2) is exercised in the RMQ.Async and RMQ.Sync runs and stays green.

Kafka notes: `schema-registry` could not start (port 8081 held by the pre-existing `rmqproxy` container). The two hand-written `KafkaMessageProducerHeaderBytesSendTests` arms fail on `Connection refused (localhost:8081)` — documented infra, not conformance. One further hand-written `KafkaMessageConsumerUpdateOffsetAsync` arm also failed; this is not in the generated suite and not a ledger regression. All 118 generated-suite tests pass.

**AWS (Floci) and RocketMQ — FR-2/15/16/22 re-run to verify the 3.1 relaxation keeps Pass cells green:**

| Project | Transport | FR-2 (with_delay) | FR-15 (zero_delay) | FR-16 (nacking) | FR-22 (requeuing_redelivered) | Notes |
|---|---|---|---|---|---|---|
| AWS.Tests | all four configs × Reactor+Proactor | 44 pass (total) | — | — | — | Filter matched FR-2/15/16/22 together: 44 pass / 0 fail / 0 skip |
| AWS.V4.Tests | all four configs × Reactor+Proactor | 44 pass (total) | — | — | — | Same result |
| RocketMQ.Tests | RocketMQMessagingGateway | Skip (Deferred) | Skip (Deferred) | 6 fail | 6 fail | See noise note below |

AWS (total filter: `with_delay OR zero_delay OR nacking OR requeuing_a_failed_message_should_be_redelivered`): **44 pass / 0 fail / 0 skip** for `AWS.Tests`; **44 pass / 0 fail / 0 skip** for `AWS.V4.Tests`. All Pass/Fixed cells remain green. Tested against `generator-transport-tests-floci-1` (port 4566) with `AWS_SERVICE_URL=http://localhost:4566`.

**RocketMQ — FR-16 and FR-22 pass after a clean broker reset.** The first run failed all six FR-16/FR-22 tests at the first receive, and failed them identically on `82f28ff01`, the commit before 3.1. The broker was at fault: even the basic posting test failed, and the rejection tests reported `No topic route info in name server`. The fix, in order:
1. `docker-compose -f docker-compose-rocketmq.yaml down -v && up -d`.
2. `docker start rmqproxy`. The proxy exits on first start because the broker is not ready yet.
3. Create the missing topics by hand with `mqadmin updateTopic`. `create-topic` in the compose file makes only a subset of the topics `RocketMqMessageGatewayProvider` maps (for example, `gen_{r,p}_nack`, `gen_{r,p}_nack2` and `gen_{r,p}_rq_redeliver` are absent), and RocketMQ 5 does not auto-create topics through the gRPC proxy. All 40 mapped `gen_*` topics were created, plus their `_DLQ`/`_Invalid` siblings (120 in all), typed as `GetTopicType` does: `partition_key` FIFO; `delayed_msg`/`requeue_delay`/`rq_delay` DELAY; the rest NORMAL.

After that, **FR-16 passes on all four tests** (`nacking_a_message` and `nacking_first_of_two`, Reactor and Proactor) and **FR-22 on both** (`requeuing_a_failed_message_should_be_redelivered`, Reactor and Proactor), about 12 s each on the 10 s invisibility lease. The 3.1 relaxation keeps both `Fixed` cells green. The compose file's incomplete topic list is a pre-existing local-infra gap. It is out of scope here and not changed.

FR-2 and FR-15 are `Deferred` for RocketMQ and emitted as Skip — as expected.

### `MQTT` was attempted and stays `Deferred` — the Proactor pump deadlocks on the first requeue

Measured 2026-09-12 against `docker-compose-mqtt.yaml`, and tracked as
[#4351](https://github.com/BrighterCommand/Brighter/issues/4351). The two variants disagree, and
FR-14's both-variants rule is what holds the cell:

| variant | result |
|---|---|
| `Reactor` | **passes in 5 s** — the message reaches the DLQ |
| `Proactor` | **hangs indefinitely** — killed at 18 minutes, and again by `--blame-hang` at 180 s |

Instrumenting the Proactor run locates it exactly. The handler is invoked **once**, and nothing
happens after that: no redelivery, no second invocation, no dead-lettering, and the pump never sees
the quit message the test enqueues.

The cause is a sync-over-async deadlock in the transport, not in the harness.
`MqttMessagePublisher`'s **constructor** blocks on its own connect
(`MQTTMessagePublisher.cs:54`, `ConnectAsync().GetAwaiter().GetResult()`), and
`MqttMessageConsumer` creates its requeue producer **lazily inside `RequeueAsync`**
(`EnsureRequeueProducer()`). A `Proactor` runs its event loop inside `BrighterAsyncContext.Run(...)`
(`Proactor.cs:99`), which is single-threaded. So the first deferral constructs the publisher on the
pump's only thread, that thread blocks, and MQTTnet's continuation is posted back to the context it
is blocking. The `Reactor` survives the identical path because `Requeue` runs on an ordinary
thread-pool thread, where the continuation has somewhere to go.

Isolated, so the mechanism is not inferred from the symptom: constructing the publisher on a
thread-pool thread completes; constructing the same publisher inside `BrighterAsyncContext.Run`
times out at 15 s. One passes, one fails, same broker, same configuration.

⭐ **This is a product defect on a production path.** Any `Proactor` consumer on MQTT deadlocks the
first time a handler defers. It is also the first defect FR-23 has found that no other behaviour in
this suite could: FR-22 requeues from the *test's* thread, so it never constructs the producer under
the pump's context and passes — which is exactly the composition FR-23 exists to test.

[#4082](https://github.com/BrighterCommand/Brighter/issues/4082) named `MQTTMessagePublisher.cs:54`
as candidate 4 and carried a checklist item for it, but was closed as **COMPLETED** on 2026-04-27
with the file untouched since `b42887af4`, which predates it. The item was missed.

Either half of the fix unblocks the cell: stop connecting in the constructor (an async factory, or
lazy connect on first publish), or create the requeue producer eagerly with the consumer so it is
never built on the pump thread.

### `GCP` ×4 was `Deferred`; the four cells are now `Fixed (#4386)` — the budget reads the broker's delivery counter

⭐ **Evidence (2026-10-01, spec 0037 task 6.16, AC-19, AC-3 on GCP, AC-30 row 3, NFR-7): the four `GCP / *`
FR-23 cells moved to `Fixed (#4386)`.** The two blockers recorded below no longer stand. The first was the
emulator refusing the IAM calls a native `DeadLetterPolicy` needs; 5.3 now tolerates those failures, so a
DLQ-backed subscription is creatable on the emulator. The second was the header-carried `HandledCount`
never advancing; 6.10/6.11 now resolve it from the broker's own delivery counter (`DeliveryAttempt` on Pull,
`GetDeliveryAttempt()` on Stream), which 6.7 measured as 1, 2, 3. Regenerating `Paramore.Brighter.Gcp.Tests`
un-skipped exactly the 8 FR-23 tests (4 configurations × Reactor/Proactor). On a clean emulator
(`docker-compose -f docker-compose-gcp.yaml down -v; up -d`) all 8 passed, in 4–9 s each. Each one
asserts that the handler was invoked at most `RequeueCount` (3) times, and that the Brighter DLQ copy
arrived within 60 s carrying `rejectionReason == "DeliveryError"`. The full scoped GCP suite
(`Category!=Spanner&Category!=GcpPubSubStream&Category!=GcpPubSubStreamOrdering&Fragile!=CI`) showed
151 passed / 50 failed / 30 skipped. The 50 failures match the pre-existing Firestore + GCS baseline
failures (real-GCP-only) by name. The Stream suite
(`(Category=GcpPubSubStream|Category=GcpPubSubStreamOrdering)&Fragile!=CI`) showed 118 passed / 0 failed /
25 skipped. The conformance audits (42) re-ran green against the new ledger. The dated notes below record
how the cells got here. Their "stays `Deferred`" statements are superseded by this note.

⭐ **Repeatability (2026-10-01, spec 0037 task 6.31, AC-22, R-21): the GCP rejection-routing behaviours and
FR-23 gave identical results on two clean-emulator runs, with no gcp-ci.** The behaviours run were FR-4,
FR-5, FR-6, FR-8, FR-17 and FR-23, across all four configurations and both variants: 48 generated tests,
selected by template name. The emulator was reset (`docker-compose -f docker-compose-gcp.yaml down -v; up -d`)
before each run. Run 1 was 48 passed, 0 failed, 0 skipped (34.7 s). Run 2 was 48 passed, 0 failed, 0 skipped
(34.4 s). A per-test diff of the two runs' outcomes was empty.

⭐ **Evidence (2026-09-28, spec 0037 task 5.11): the twenty rejection-routing cells (FR-4, FR-5, FR-6,
FR-8, FR-17 × four configurations) moved to `Fixed (#4386)`, and this paragraph's blocker does not
apply to them.** This section's two-API blocker (below) is specific to **FR-23**: FR-23 dead-letters
by a message exhausting its delivery budget under the subscription's own native `DeadLetterPolicy`,
which `EnsureSubscriptionExistsAsync` provisions via `UpdateIAmRoleForDeadLetterAsync`, and that call
is what needs the two APIs the emulator refuses. FR-4/5/6/8/17 are a different mechanism entirely
(ADR 0078): an explicit `Reject` routes a stamped copy to a Brighter-managed dead-letter or
invalid-message topic addressed by `deadLetterRoutingKey`/`invalidMessageRoutingKey`, published by an
ordinary lazy producer — no native `DeadLetterPolicy`, no `UpdateIAmRoleForDeadLetterAsync` call, and
so no dependency on Resource Manager or the emulator's absent IAM surface. Ledger regeneration
un-skipped 40 generated tests (20 cells × Reactor/Proactor); all 40 passed on a clean emulator
(`docker-compose -f docker-compose-gcp.yaml down -v; up -d`), and the full scoped GCP suite
(`Category!=Spanner&Category!=GcpPubSubStream&Category!=GcpPubSubStreamOrdering&Fragile!=CI`) showed
120 passed / 50 failed / 32 skipped — the 50 failures are exactly the pre-existing 45 Firestore + 5 GCS
baseline failures (real-GCP-only), zero in `MessagingGateway`, so no cell was reverted.
`RejectionMetadataContractAudit`/`LedgerSkipCrossCheckAudit` (44 tests) re-ran green against the new
ledger. **FR-23 itself stays `Deferred` here** — this paragraph's blocker is unaffected and still
applies; it is Phase 6/7 territory (ADR 0077, delivery count), not this task's scope.

⭐ **Measurement (2026-09-29, spec 0037 task 6.7, AC-39): the emulator's broker delivery counter is
populated and advances on a DLQ-backed subscription.** This was run on a clean emulator. The
subscription carried a native `DeadLetterPolicy` (`MaxDeliveryAttempts = 5`), which Brighter's channel
factory can now create on the emulator because 5.3 tolerates the IAM failures described below. One
message was deferred on its first two deliveries and acknowledged on the third, and the raw counter
was read on each delivery:
- **Pull:** `ReceivedMessage.DeliveryAttempt` = **1, 2, 3**, with the deferral as `ModifyAckDeadline(…, 0)`,
  the same call `GcpPullMessageConsumer.Requeue` makes.
- **Stream:** `GetDeliveryAttempt()` = **1, 2, 3**, with the deferral as `SubscriberClient.Reply.Nack`.

Both sequences repeated on a second run. Fixture:
`tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Pull/GcpDeliveryAttemptMeasurementTests.cs`
(committed Skip-marked). ADR 0077's *Measurement outcome* under "R-13 (GCP): the branch rule" records
that A-2 held and claims **AC-19**. The broker counter advances where the header-carried `HandledCount`
cannot (see the second blocker below), and that is the mechanism 6.10/6.11 wire in. **The four FR-23
cells stay `Deferred` until 6.16 moves them.**

⭐ **Lease-lapse evidence (2026-09-30, spec 0037 tasks 6.13/6.14, AC-42): `GCP / StreamOrdering` uses the
keyless alternative.** 6.13 measured that a message published with an ordering key is not redelivered while
its first delivery is held (none in 90 s), because ordered delivery withholds a same-key redelivery behind an
outstanding predecessor. So on `GCP / StreamOrdering`, 6.14 publishes **without** an ordering key on the same
ordering-enabled subscription, as ADR 0077's stream lease-lapse procedure allows. `GCP / Stream` uses the
primary procedure. Both need the client's own `AckDeadline` set to 10 s as well as the subscription's (lapse at
~15 s rather than ~60 s). See ADR 0077's 6.13 amendment. Tests:
`tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Stream/When_a_gcp_stream_lease_lapses_should_present_greater_delivery_count{,_async}.cs`.

Measured 2026-09-12 against `docker-compose-gcp.yaml` (the `cloud-sdk:emulators` Pub/Sub emulator on
`localhost:8085`, with `PUBSUB_EMULATOR_HOST` and `GOOGLE_CLOUD_PROJECT` exported). All eight tests —
four configurations × both variants — fail in ~4 s during **arrange**, before any pump runs. The
behaviour was never reached, so this run is evidence about the *infrastructure*, not about FR-23.

**The DLQ path needs two APIs the emulator does not implement.** Creating a subscription that
carries a `DeadLetterPolicy` routes through `GcpPubSubMessageGateway.EnsureSubscriptionExistsAsync`
(`:235`), which calls `UpdateIAmRoleForDeadLetterAsync` (`:481`). That method does two things, and
the emulator refuses both:

| call | what happens on the emulator |
|---|---|
| `ProjectsClient.GetProjectAsync` — Cloud Resource Manager, used to derive the default Pub/Sub service account when `DeadLetterPolicy.PublisherMember` is unset | `Unauthenticated` — the request leaves for **real GCP**. `ProjectsClientConfiguration` is the one builder hook the four GCP providers do not wire for emulator detection, and Resource Manager is a different service from Pub/Sub, so `PUBSUB_EMULATOR_HOST` could not redirect it anyway |
| `PublisherServiceApiClient.IAMPolicyClient.GetIamPolicyAsync` on the dead-letter topic | `Unimplemented` — the Pub/Sub emulator has no IAM surface at all |

The second was isolated rather than inferred: setting `PublisherMember` explicitly on the provider's
`DeadLetterPolicy` skips the Resource Manager call, and the run then fails one line later on
`GetIamPolicy` with `Unimplemented`. That probe was reverted; it is recorded here, not committed.

**So no amount of harness work makes this cell runnable locally.** This is the same resolution as
`AzureServiceBus`: the scoped suite cannot be run against local infrastructure, and verification is
deferred to real infrastructure — here `gcp-ci`, which runs against a real GCP project.

⚠️ **A second, independent blocker is visible in source and would survive the emulator being fixed.**
`GcpPullMessageConsumer.Requeue{,Async}` requeues with `ModifyAckDeadline(…, 0)` (`:335`, `:369`),
which returns the message to the subscription **without rewriting the stored copy**, and
`HandledCount` is written only on *send* (`Parser.AddHeaders`, `:307`). Every redelivery therefore
arrives reading 0, the pump bumps it to 1, `HandledCountReached(RequeueCount)` is never true, and the
budget cannot run down. What would dead-letter the message is the subscription's own
`MaxDeliveryAttempts`, and the DLQ copy would carry `HandledCount = 0` — failing this behaviour's
`>= RequeueCount - 1` bound. **This is the same defect as
[#4341](https://github.com/BrighterCommand/Brighter/issues/4341) on SQS**, whose `ChangeMessageVisibility`
requeue has exactly this shape. It is recorded here as a source reading, not a measurement: the
emulator blocker above stops the run that would confirm it.

⭐ **A product observation worth separating from the cell, filed as
[#4354](https://github.com/BrighterCommand/Brighter/issues/4354).** `UpdateIAmRoleForDeadLetterAsync` makes
channel creation *hard-fail* when the project's IAM cannot be read or written. That means an
application creating a DLQ-backed channel must hold `resourcemanager.projects.get` and
`pubsub.topics.{get,set}IamPolicy`, which an application service account frequently will not — the
DLQ is usually provisioned by infrastructure-as-code. Tolerating `Unimplemented` and
`PermissionDenied` there (log and continue, as the binding may already exist) would both fix that and
make the emulator path usable.

### `RocketMQ` was `Deferred`; the cell is now `Fixed (#4353)` — the budget reads the broker's delivery counter

⭐ **Evidence (2026-10-02, spec 0037 task 7.13, AC-24, AC-3 on RocketMQ, AC-30 row 4): the
`RocketMQ / RocketMQMessagingGateway` FR-23 cell moved to `Fixed (#4353)`.** `Requeue` is still a broker no-op,
as recorded below; R-14 allows that on the AC-24 branch. The budget runs down anyway: 7.10 resolves
`HandledCount` from the broker's own `MessageView.DeliveryAttempt`, which 7.1 measured as 1, 2, 3 across
lease-lapse redeliveries. Each 10 s lease lapse therefore presents a higher count, and the pump rejects once
`RequeueCount` is reached. 7.3 makes the header-owned `HandledCount` win over a stale bag copy on the routed
dead-letter copy, and `DeliveryCount.Resolve` keeps that stamped count because the copy carries
`rejectionReason` (7.12). Regenerating `Paramore.Brighter.RocketMQ.Tests` un-skipped exactly the 2 FR-23 tests
(Reactor/Proactor). On a clean store (`docker-compose -f docker-compose-rocketmq.yaml down -v; up -d`, all
compose topics created) both passed, in 39 s and 41 s. Each one asserts that the handler was invoked at most
`RequeueCount` (3) times, and that the Brighter DLQ copy arrived within 60 s with `HandledCount >= RequeueCount - 1`,
carrying `rejectionReason == "DeliveryError"`. The full RocketMQ suite on a clean store (net10.0): 67 passed / 0 failed / 6 skipped. The
conformance audits (42) re-ran green against the new ledger. The paragraphs below record how the cell got
here. Their "stays `Deferred`" and "never dead-lettered" statements are superseded by this note.

Measured 2026-09-12 against `docker-compose-rocketmq.yaml`, and tracked as
[#4353](https://github.com/BrighterCommand/Brighter/issues/4353). Both variants fail the same way: the
dead-letter poll returns `MT_NONE` for the whole window. The message is never dead-lettered.

**It is not a timing problem, and that was measured rather than argued.** RocketMQ redelivers only
when its 10 s invisibility lease expires, so a budget of 3 could plausibly need more than the 30 s
ceiling. Re-running the Reactor variant with the ceiling widened to **150 s** — roughly fifteen
redeliveries against a budget of three — still ends in `MT_NONE`. The widened ceiling was a probe on
the generated file, restored with `./generate-test.sh`; it is recorded here, not committed.

**The cause is in the transport.** `RocketMessageConsumer.Requeue` (`:179`) does nothing at all: it
resolves the `MessageView` from the bag and returns `true`, with the one call that would act on the
broker commented out —

```csharp
// Waiting for next RocketMQ C# version, due an issue on ChangeInvisibleDuration
// consumer.ChangeInvisibleDuration(view, TimeSpan.Zero);
```

so the message simply stays invisible until its lease lapses and the broker re-serves the **stored**
copy. `HandledCount` is read from the message's published properties (`ReadHandledCount`, `:422`) and
written only on send (`RocketMqMessageProducer`, `:157`), so every redelivery arrives reading the
value that was originally published. The pump bumps it to 1, `HandledCountReached(RequeueCount)` is
never true, no `Reject` is ever issued, and nothing reaches the DLQ — for ever, not merely for 150 s.

⭐ **This is the third instance of one defect family, and the most complete.** `AWS`
([#4341](https://github.com/BrighterCommand/Brighter/issues/4341)) requeues with
`ChangeMessageVisibility` and `GCP` with `ModifyAckDeadline(0)`; neither rewrites the stored message,
so neither can spend a Brighter-side budget either. But both of those have a broker-side redrive
(`maxReceiveCount`, `MaxDeliveryAttempts`) that dead-letters the message anyway, which is why their
FR-23 tests reach the DLQ and fail on the *count*. RocketMQ has no such policy wired, so its message
is never dead-lettered by anyone. **The common requirement this column keeps finding: a transport
whose requeue does not persist the delivery count cannot exhaust the pump's budget.**

**2026-10-01: AC-23 measured (spec 0037, task 7.1). R-14's condition holds, so AC-24 is claimed.** Two runs
with the Skip-marked fixture `tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/Reactor/RocketMqDeliveryAttemptMeasurementTests.cs`,
broker `apache/rocketmq:5.5.0`, RocketMQ.Client 5.2.1. Run 1 used a freshly created topic. Run 2 used a store reset
with `down -v; up -d`. Setup: `requeueCount: 3`, Reactor, 10 s invisibility lease, and no `Requeue`, `Nack` or
`ChangeInvisibleDuration` call. The raw `MessageView.DeliveryAttempt` was read from `Header.Bag["ReceiptHandle"]`.
**The consumer was disposed and a new one created in the same consumer group before delivery 3.**

| Run | Delivery 1 | Delivery 2 | Delivery 3 (new client) | `HandledCount` as received |
|---|---|---|---|---|
| 1 (fresh topic) | **1** (t+0.1 s) | **2** (t+13.3 s) | **3** (t+25.4 s) | 0, 0, 0 |
| 2 (clean store) | **1** (t+0.2 s) | **2** (t+12.3 s) | **3** (t+24.3 s) | 0, 0, 0 |

The sequence is strictly increasing, and it survives a new client, so the increment is broker-supplied, not the
client-local `IncrementAndGetDeliveryAttempt`. AC-25 is not applicable on the strength of this measurement. The
counter stays classified **approximate** (ADR 0077): the Apache RocketMQ retry documentation describes the retry
mechanism but makes no statement that the attempt count is exact. The `HandledCount` column shows the defect
above as measured: the received header stays at its published value.

**`ChangeInvisibleDuration` works on client 5.2.1.** This is a probe in the same fixture, prompted by PR #4263,
which calls it from `Nack`. `SimpleConsumer.ChangeInvisibleDuration(view, TimeSpan.Zero)` returned without error,
and the message was redelivered after **2.9 s / 2.3 s** (runs 1 / 2) rather than after the 10 s lease (the
lease-lapse control above took 12–13 s). `DeliveryAttempt` still advanced, 1 → 2. So the "upstream blocker" in the
`Requeue` comment ("Waiting for next RocketMQ C# version") no longer exists. R-14 lets `Requeue` stay a broker
no-op on AC-24, so enabling it is a follow-up, not part of this spec.

#4353 was updated with this measurement:
[#4353 (comment)](https://github.com/BrighterCommand/Brighter/issues/4353#issuecomment-5930270994).

### ⚠️ Running `RocketMQ` locally — what the compose file now handles, and what it cannot

The FR-23 attempt cost **four failed runs** before a single one measured the behaviour, and
`rocketmq-ci` has been commented out since #3696, so local is the only place this ever runs. Two of
the causes are now fixed in `docker-compose-rocketmq.yaml`; three remain facts about the broker.

**Fixed in the compose file** (and verified end to end — the stack was torn down with `down -v`,
rebuilt, and the FR-23 failure reproduced on infrastructure the compose file alone created):

1. **`create-topic` used to silently create nothing.** It invoked
   `/home/rocketmq/rocketmq-5.4.0/bin/mqadmin`, but `apache/rocketmq:latest` now ships **5.5.0**, so
   every line failed with `not found` — and nothing checks the service's exit. The path is now
   resolved at run time (`ls -d /home/rocketmq/rocketmq-*/bin/mqadmin`), so it survives the next
   version bump.
2. **The conformance topics were absent from its list.** `gen_r_exhaust`, `gen_p_exhaust` and their
   `_DLQ` / `_Invalid` companions are now created with the rest.

**Still true of the broker, and not fixable in compose:**

3. **RocketMQ 5.x does not auto-create topics through the gRPC proxy** — a producer on an unknown
   topic fails with `No topic route info in name server`. Any new generated topic must be added to
   `create-topic`; nothing will conjure it at first publish.
4. **`rmqproxy` caches topic routes at startup**, so a topic created after it started stays invisible
   until `docker restart rmqproxy`. Because `create-topic` and the proxy come up together, **the proxy
   generally needs one restart after a first `up -d`.** The proxy also fails outright if it beats the
   broker's nameserver registration (`create system broadcast topic DefaultHeartBeatSyncerTopic
   failed`); starting it again is enough.
5. **`rmqproxy` needs port 8081, which the Kafka suite's `schema-registry` container also binds**, so
   the two stacks cannot be up at once. Stop `schema-registry` first, and **restart it afterwards**.

⛔ **The dead-letter topics are shared and persistent, and residue breaks the identity assertion.** The
first two runs failed against a **~6-week-old** message. The DLQ read *acks* what it returns, but it
uses a fresh GUID consumer group each time, so the ack moves only that group's offset and drains
nothing; `mqadmin deleteTopic` and recreate does not help either, because the consumequeue files
survive. **Only `docker-compose … down -v` followed by `up -d` gives a clean store** — RocketMQ's
store lives inside the container filesystem, not in a named volume.

### ✅ RESOLVED — a harness note the MQTT run exposed, which turned out to be tree-wide

The finding, as first recorded here: `MqttMessageGatewayProvider.GetMessageFromDeadLetterQueue{,Async}`
looped 10 times over a 5 s `Receive` plus a 1 s `Thread.Sleep`, so a single call took ~60 s. The
FR-23 retry loop wrapped it in a 30 s stopwatch, which therefore could not bound anything: one call
already overran it. The async overload was `Thread.Sleep`-based inside an `async` method. Neither
affected the verdict above — the Proactor deadlock is upstream of the DLQ poll — but the ceiling
documented in NFR-2 was not the ceiling MQTT observed.

It was never an MQTT quirk. The same `10 × (5 s + 1 s)` loop had been copied into **50 helper bodies
across 13 provider files** — every `GetMessageFromDeadLetterQueue[Async]` and
`GetMessageFromInvalidChannel[Async]` in the tree bar the already-compliant ones (Kafka's six, RMQ's
invalid-channel pair, GCP's and ASB's invalid-channel helpers, RocketMQ's sync one). So no rejection-
destination arrival anywhere observed NFR-2's documented bound, and the cost was not spread evenly:
AC-5's "DLQ must be empty" check asserts a message *never* arrives, so it ran the full 60 s on every
run — 8 minutes per CI job on `aws-ci` alone, across its 8 live invalid-channel tests.

All 50 now attempt a single bounded receive and return, the retry having moved to the caller's loop
where NFR-2 says it lives; the rejection-destination ceiling is stated as 60 s so today's tolerance
is preserved exactly rather than tightened. `DeadLetterPollContractAudit` fails the build if a helper
starts retrying internally again.

Every cell above was also checked for vacuity the same way: force the pump's budget to `int.MaxValue`
so it can never be exhausted, and confirm the test goes red. Both variants were probed separately —
`CreateChannel` and `CreateChannelAsync` are different paths, and a probe that touches one proves
nothing about the other.

### The budget is enforced by the pump, and FR-23 drives the pump

`Reactor` and `Proactor` own this rule: they call `UpdateHandledCount`, test
`HandledCountReached(RequeueCount)`, and reject with `RejectionReason.DeliveryError` when the budget
is spent. Nothing else does. A test that calls `Requeue` on a channel never reaches that code, so it
cannot answer the question FR-23 is named for however green it goes.

So FR-23 starts a real pump over the channel and routes it to a handler that always defers, using
the `ConformanceDeferredPump` scaffolding generated into each configuration's `Generated` folder.

⚠️ **This replaced a harness that stood in for the product.** `RequeueTrackingChannel{Sync,Async}`
re-implemented the budget rule at channel level in eight providers, converting the Nth `Requeue` into
a `Reject`. Every `Pass` it produced certified the harness's copy of the rule, not Brighter's, and
would have kept certifying it after the two diverged. The substituting branch is gone; the wrapper
now only stamps `x-original-message-id`, which is bookkeeping rather than behaviour.

The distinction is not academic. Measured on this branch, the channel-level version reported Kafka as
non-conformant — the message kept being delivered after the budget was spent — and the pump-driven
version passes on all three Kafka configurations. **The apparent defect was the test bypassing the
code under test.** Kafka's DLQ is Brighter-managed ([ADR 0046](../../docs/adr/0046-kafka-dlq-producer-for-requeue.md)
gives it a DLQ producer, which fires on the `Reject` the pump issues), and it works.

Kafka's one real gap was in the harness: its three providers were the only ones of the 21 that never
declared `requeueCount`, so they took `Subscription`'s default of `-1`. The pump reads that as
`DiscardRequeuedMessagesEnabled() == false` and never rejects, which left the behaviour untestable
rather than failing. They now declare a budget like every other provider.

### What the dead-lettered message may be expected to carry

Running RMQ made two of FR-23's assertions visible as assumptions about *how* a message reaches the
DLQ rather than *whether* it does. Both were written against Brighter-side republish, which was the
only mechanism the column had seen. Neither is a defect in RMQ.

**Identity.** The original assertion was `dlqMessage.Header.MessageId == message.Header.MessageId`.
Most transports carry the id through a requeue untouched. RabbitMQ does not: `RmqMessagePublisher`
mints a fresh id on republish (`Uuid.NewAsString()`) and records the first one in the
`x-original-message-id` header. That is deliberate, and the product already depends on it —
`Reactor.cs:500` and `Proactor.cs:506` read exactly that header when they log a dropped message. The
assertion now accepts **either** the message id or `x-original-message-id`, and nothing else; an
unrelated message matches neither.

**Delivery count.** The original assertion was `HandledCount >= RequeueCount`. The pump increments
the count and *then* tests it, so the delivery that exhausts the budget is the one never republished.
Where the harness puts the message on the DLQ itself, it sends that final in-memory header and the
count reads `RequeueCount`. Where the broker dead-letters natively — `RmqMessageConsumer.RejectAsync`
calls `BasicRejectAsync` and the broker's DLX moves the copy it already holds — the stored copy was
written by the last republish, so it reads one less. Both spent the same budget; they differ only in
which copy gets recorded. The assertion is now `>= RequeueCount - 1`, the strongest bound true of
both mechanisms, and it still fails anything dead-lettered before the budget ran down.

Both changes live in the shared templates, so every configuration is held to the same rule. The four
configurations that were already `Pass` were re-run against the revised assertions and stay green.

### The division of labour: what this suite proves, and what the pump suite proves

FR-23 drives a real pump. **The other canonical behaviours deliberately do not**, and that is a
decision rather than an oversight, so it is recorded here.

FR-2, FR-15 and FR-22 call `Requeue` on the channel; FR-4, FR-5, FR-6, FR-7, FR-8 and FR-17 call
`Reject`. None starts a pump. Read as "does the pump do the right thing", those tests would be
bypassing the code under test — the objection that made FR-23 change. Read as what they are, a
**gateway** suite, they are correct: they ask whether *this transport* honours a requeue or a reject,
which is the only question a transport can answer.

The pump's half of the behaviour is proven separately, against in-memory channels, in
`tests/Paramore.Brighter.Core.Tests/MessageDispatch` — **86 tests, 41 `Reactor` and 45 `Proactor`**:

| the pump decision | covering tests |
|---|---|
| a deferring handler is requeued until rejected | `When_a_command_handler_throws_a_defer_message_Then_message_is_requeued_until_rejected` (×4) |
| the requeue-count threshold is reached | `When_a_requeue_count_threshold_for_{commands,events}_has_been_reached` (×4) |
| `ChannelFailureException` retries until reconnected | `When_a_channel_failure_exception_is_thrown_…_should_retry_until_connection_re_established` (×4) |
| the unacceptable-message limit | `When_an_unacceptable_message_limit_is_{reached,reset}` (×8) |
| reject falls back to the DLQ with no invalid channel | `When_no_imq_configured_reject_falls_back_to_dlq` (×2) |

**Validity is compositional**: the gateway suite proves the transport performs the operation, the pump
suite proves the pump decides to perform it, and together they cover the behaviour. Neither needs to
re-prove the other's half, and a gateway suite that re-drove the pump for all nine behaviours would be
duplicating 86 tests across 24 configurations to learn nothing new.

FR-23 is the exception that shows where the line falls. Its question — *does exhausting the budget put
the message on the DLQ?* — spans both halves at once: the budget exists only in the pump, and the
landing is observable only on the transport. No test on either side alone can answer it, so FR-23
alone drives a real pump.

⚠️ **The risk this carries, stated so it is not discovered the hard way.** The composition is only as
good as both halves. If the `MessageDispatch` tests above are deleted, weakened, or quietly narrowed,
every conformance cell in this ledger stays green while the composite claim silently stops being true
— the gateway suite cannot notice, because it never asserted the pump's half in the first place.
Treat those 86 tests as load-bearing for this ledger, not as an independent suite that happens to
exist.

**`PumpCoverageAudit` is the link, and it fails the build.** For each behaviour in the table above it
requires, in *both* the `Reactor` and the `Proactor` tree (FR-14 parity — one surviving half proves
half of what the ledger claims), a file whose name carries the behaviour's fragments **and** whose
contents still make at least one assertion. It reports the three losses separately, because the fix
differs: `PumpBehaviourNotCovered` (gone from both trees — a deletion), `PumpBehaviourVariantNotCovered`
(one pump kept it, the other dropped it), and `PumpBehaviourCoverageHollowedOut` (the name survives,
the assertions do not). Each rule was probe-verified red against the real tree, not argued.

⚠️ **What it still does not catch, so nobody reads it as more than it is.** It cannot tell a strong
assertion from a vacuous one, so a test narrowed to a trivial case still passes it; and it can only
guard behaviours listed in `PumpCoverageAudit.RequiredBehaviours`, so **adding a row to the table
above without adding it there leaves that row unguarded.** Editing that list down to match a narrowed
tree silences the audit by construction — the list and this table are meant to be changed together,
and in review.

### The eight AWS cells are `Fixed (#4341)`: the budget now reads SQS's own receive count

**Evidence, run 2026-09-27** (spec 0037 task 4.11). Both variants (Reactor and Proactor) of every
`AWS` and `AWS.V4` configuration, against the **Floci** emulator (`floci/floci:1.5.19`,
`docker-compose-aws.yaml`, port 4566; LocalStack is no longer used locally), net10.0,
`--filter "LiveAWS!=true"`, the two projects run one after the other:

| Project | Passed | Failed | Skipped | FR-23 (Reactor + Proactor × 4 configurations) |
|---|---|---|---|---|
| `AWS.Tests` | 286 | 0 | 2 | 8 / 8 pass |
| `AWS.V4.Tests` | 286 | 0 | 2 | 8 / 8 pass |

The only skips are `SqsFifo` FR-9 (Reactor and Proactor) in each project, which is still `Deferred -> #4240`.
Every previously `Pass`/`Fixed` cell stayed green. The eight FR-23 tests per project are the
difference from the previous baseline (278 passed / 10 skipped).

**What was wrong, for the record.** Before #4341, `SqsMessageConsumer.RequeueAsync` requeued by
`ChangeMessageVisibilityAsync`, which never rewrites the stored message, and `handled-count` was
written only on send. Every redelivery therefore read `0`, `HandledCountReached(3)` never fired, and
the message reached the DLQ only via the queue's `maxReceiveCount` redrive. It arrived with
`handled-count=0` and **no** rejection metadata, so `requeueCount` was silently inert on SQS.

**What fixed it** ([ADR 0077](../../docs/adr/0077-delivery-count-contract.md)): both SQS message
creators read the broker's `ApproximateReceiveCount` on receive, minus 1, through the core
`DeliveryCount.Resolve`. The harness keeps the budget below the native limit (`requeueCount: 3`,
`RedrivePolicy` `maxReceiveCount: 5`, assumption A-5), so the pump rejects first. The message then
reaches the Brighter DLQ through `Reject` ([ADR 0038](../../docs/adr/0038-aws-sqs-dlq-direct-send.md)'s
direct send), carrying its stamped count and ADR 0036's rejection metadata. The count is
**approximate** (SQS's counter is), so FR-23 asserts it only as bounded, never as exact.

#### v3 and v4 are indistinguishable on the FR-23 run (AC-13, NFR-6; spec 0037 task 4.12)

**Evidence, run 2026-09-27**, Floci, net10.0. The generated FR-23 test asserts only a bound on
`HandledCount` and `rejectionReason`, so a throwaway, uncommitted probe re-ran the generated FR-23
scenario unchanged for each configuration and variant. After the pump stopped, it recorded the
dead-lettered message's header and bag, plus every Warning-or-above log entry from a capturing
`ApplicationLogging.LoggerFactory`. It ran with collection parallelism off, so the logs are per test.
All 16 runs passed their FR-23 assertions.

| Configuration | Variant | Reason | Destination | Metadata key set | `rejectionReason` / `rejectionMessage` / `originalMessageType` | Warning+ logs | Count (v3 / v4), R = 3 | Verdict |
|---|---|---|---|---|---|---|---|---|
| SnsStandard | Reactor | DeliveryError | Brighter DLQ | 13 keys, equal | equal | equal | 3 / 3 | same |
| SnsStandard | Proactor | DeliveryError | Brighter DLQ | 13 keys, equal | equal | equal | 3 / 3 | same |
| SnsFifo | Reactor | DeliveryError | Brighter DLQ | 14 keys, equal | equal | equal | 3 / 3 | same |
| SnsFifo | Proactor | DeliveryError | Brighter DLQ | 14 keys, equal | equal | equal | 3 / 3 | same |
| SqsStandard | Reactor | DeliveryError | Brighter DLQ | 13 keys, equal | equal | equal | 3 / 3 | same |
| SqsStandard | Proactor | DeliveryError | Brighter DLQ | 13 keys, equal | equal | equal | 3 / 3 | same |
| SqsFifo | Reactor | DeliveryError | Brighter DLQ | 14 keys, equal | equal | equal | 3 / 3 | same |
| SqsFifo | Proactor | DeliveryError | Brighter DLQ | 14 keys, equal | equal | equal | 3 / 3 | same |

- **Values compared for equality**, identical in every pair: `rejectionReason` = `DeliveryError`;
  `rejectionMessage` = `Handle count of messages reached; rejecting at limit`; `originalMessageType` =
  `MT_COMMAND`; the legacy `RejectionReason` summary string; `handled-count`.
- **The key set**: `handled-count`, `header1`–`header5` (the test's own), `originalMessageType`,
  `originalTopic`, `ReceiptHandle`, `rejectionMessage`, `rejectionReason`, `RejectionReason`,
  `rejectionTimestamp`, plus `messageDeduplicationId` on the two FIFO configurations.
- **Compared for presence and shape only**, all matching in every pair: `originalTopic` equals the
  source topic; the destination is `{topic}.DLQ` (`{topic}-DLQ` on the wire, `.fifo` kept on FIFO);
  `rejectionTimestamp` has the same format. `ReceiptHandle` and `messageDeduplicationId` are per-run
  values (a fresh GUID minted on the DLQ send, in both variants).
- **Warning+ logs**, identical once per-run names and thread ids are removed: one pump Error
  (`Have tried 3 times to handle this message … dropping message`) and one pump Warning
  (`Rejecting message …`). Neither consumer logged a Warning.
- **Delivery count**: `HandledCount` = 3 in all 16 runs, which meets `<= R`. The pump dispatched 3 times
  in all 16 runs.

**No divergence, so nothing to fix.** Three things hold in **both** variants alike. They are not
v3/v4 divergences, but they are recorded so they are not rediscovered:

- `rejectionTimestamp` is written in a culture-dependent format (`27/09/2026 19:03:31`), not ISO 8601.
- The bag carries both the ADR 0036 `rejectionReason` key and the older `RejectionReason` summary string.
- On the SQS configurations the pump's log line reads `from <queue> with  on thread`, with an empty topic.


## The delay plugin is retired — all three RMQ configurations run on stock images

The RabbitMQ delayed-message-exchange plugin is being retired upstream, so the CI and compose pins moved
off `brightercommand/rabbitmq:*-management-delay` onto stock `rabbitmq:4.2-management` and
`rabbitmq:3.13-management`. **The RabbitMQ versions are unchanged** — only the plugin is gone.

**The suite never used the plugin.** The RMQ providers present a plain (non-delay) exchange, so
`RmqMessageProducer` reports `DelaySupported == false` and routes a non-zero delay to
`IAmAMessageProducer.Scheduler` — the same scheduler seam proven for Kafka, Redis and MSSQL. The native
`x-delayed-message` path is deliberately not exercised because it is not yet conformant (see the
`RMQ.Async / Classic` note above).

**One test required the plugin and was retired**:
`tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/Reactor/When_reading_a_delayed_message_via_the_messaging_gateway.cs`
— the only `supportDelay: true` in the repository. Re-pointing it at the scheduler seam would have made it
a hand-written duplicate of generated coverage that already exists six times over
(`When_sending_a_delayed_message_should_deliver_after_delay` and
`When_requeuing_a_failed_message_with_delay_should_redeliver_after_delay`, across `RMQ.Sync`,
`RMQ.Async / Classic` and `RMQ.Async / Quorum`, Reactor and Proactor), so it was deleted instead. What is
no longer covered is `ExchangeConfigurationHelper`'s `SupportDelay` branch, which is the path being retired.

**Measured, not assumed.** Against a stock, plugin-free broker (`rabbitmq:4.2-management`, RabbitMQ 4.2.9,
zero delayed-message plugins installed):

| suite | result |
|---|---|
| `Paramore.Brighter.RMQ.Sync.Tests` | 81 passed, 3 skipped, **0 failed** (84 total) |
| `Paramore.Brighter.RMQ.Async.Tests` | 142 passed, 6 skipped, **0 failed** (148 total) |

Both run under the CI filter `Fragile!=CI&Requires!=Docker-mTLS`. Every delay behaviour passes in both
variants. Before the retirement, the two facts in the deleted file failed on that broker in **1 ms** with
`PRECONDITION_FAILED - unknown exchange type 'x-delayed-message'`, which is what made them the whole
blocker to stock images.

⚠️ **Switching an existing local volume needs one manual step.** The old `brightercommand` image runs as
root and leaves a root-owned, mode-400 `.erlang.cookie` in `rabbitmq_data`; the stock image runs as uid 999
and cannot read it, so the container crash-loops on
`Error when reading /var/lib/rabbitmq/.erlang.cookie: eacces`. Delete the volumes before the first run:

```bash
docker-compose -f docker-compose-rmq.yaml down
docker volume rm generator-transport-tests_rabbitmq_data generator-transport-tests_rabbitmq_logs
```

CI is unaffected — GitHub Actions `services:` mount no volume.

## The routing reject tests now check the routed body and `Reject`'s return value

Spec 0037 Phase 7A (tasks 7A.1, 7A.2) added two assertions to the five routing reject templates, Reactor and
Proactor: FR-4 (`delivery_error_should_send_to_dlq`), FR-5 (`unacceptable_reason_should_send_to_invalid_channel`),
FR-6 (`unacceptable_and_no_invalid_channel_should_fallback_to_dlq`), FR-8 (`should_include_metadata`) and FR-17
(`unknown_reason_should_send_to_dlq`). They now assert that:

- the copy read from the DLQ or invalid channel has the sent message's `Body.Value` (7A.1, `170ec0012`); and
- `Reject` / `RejectAsync` returned `true` (7A.2, `487a9e554`). FR-7's `no_channels_configured` template
  already asserted this.

The deletion of the hand-written RocketMQ reject tests (`ec2071bb0`) had exposed both gaps. **No ledger cell
changed.** The new assertions passed wherever the old ones did.

⭐ **Run record (2026-10-02, spec 0037 task 7A.3).** All 17 projects with a `test-configuration.json` were
regenerated. The diff was 190 generated files in ten projects (AWS 40, AzureServiceBus 10, Gcp 40, Kafka 30, MQTT
10, MSSQL 10, PostgresSQL 10, Redis 10, RMQ.Async 20, RMQ.Sync 10), each `+5/-1`, all routing reject tests.
RocketMQ and AWS.V4 had already been regenerated in 7A.1/7A.2 and did not change again. The outbox-only projects
(DynamoDB, DynamoDB.V4, MongoDb, MySQL, Sqlite) generate no reject tests and did not change. The generated reject
tests (filter `FullyQualifiedName~.WhenRejectingMessage`, net10.0) were then run against local infrastructure:

| Project | Passed / Failed / Skipped | Infrastructure |
|---|---|---|
| AWS | 48 / 0 / 0 | Floci (`LiveAWS!=true`) |
| AWS.V4 | 48 / 0 / 0 | Floci (`LiveAWS!=true`); see the note below |
| Gcp | 40 / 0 / 8 | Pub/Sub emulator (`Fragile!=CI`); the 8 are FR-7 `Deferred` × 4 configurations × 2 variants |
| Kafka | 36 / 0 / 0 | `docker-compose-kafka.yaml` (RocketMQ stack stopped first: port 8081) |
| MQTT | 12 / 0 / 0 | `docker-compose-mqtt.yaml` |
| MSSQL | 12 / 0 / 0 | `docker-compose-mssql.yaml` |
| PostgresSQL | 12 / 0 / 0 | `docker-compose-postgres.yaml` |
| Redis | 12 / 0 / 0 | `docker-compose-redis.yaml` |
| RMQ.Async | 20 / 0 / 4 | `docker-compose-rmq.yaml`; the 4 are FR-5 `Deferred` × 2 configurations × 2 variants |
| RMQ.Sync | 10 / 0 / 2 | `docker-compose-rmq.yaml`; the 2 are FR-5 `Deferred` × 2 variants |
| RocketMQ | 12 / 0 / 0 | `docker-compose-rocketmq.yaml`, from `down -v` (100/100 topics) |

**Not run locally:** AzureServiceBus. It has no local emulator or compose file. Two of its 12 reject tests are
skipped by the ledger (FR-5 `Deferred`). The other ten compiled but did not run.

**AWS.V4 note:** the first AWS.V4 run went alongside the AWS run against the same Floci container and took
4 m 36 s. One test failed: SnsFifo Proactor `unacceptable_reason_should_send_to_invalid_channel_async`, after
2 m 48 s, with `AmazonSQSException: … ReceiptHandle is invalid. Reason: The receipt handle has expired`. That is
the visibility timeout lapsing under load, not a new assertion. Run alone, the same 48 tests passed in 1 m 22 s.
They had also passed in four earlier runs that day (7A.1 and 7A.2). Run the two AWS projects one after the other
against one Floci container.

**RED under mutation (7A.1, 7A.2):** RocketMQ (`RocketMessageConsumer.RejectAsync`) and SQS V4
(`SqsMessageConsumer.RejectAsync`) were mutated:

- **Routed copy sent with body `"mutated-body"`:** RocketMQ 10/10 and SQS V4 40/40 generated tests failed on the
  body assertion.
- **`return false` after a successful route:** the same counts failed on the return-value assertion.

An **empty** body is not a usable mutation. RocketMQ and Floci both refuse to send it, so nothing is routed and
the tests fail on arrival (`MT_NONE`) instead. On a RocketMQ store that has had a run on it, the DLQ read returns
a stale message, which can pass for a body-assertion failure.

## Spec 0037's cell moves, and the cells it did not move (AC-30, AC-31)

**Audited 2026-10-02 (spec 0037 task 8.6).** Every cell below was checked against the matrix at
`a4b30a329`, compared with the merge base `f906efc0b`.

**Moved by spec 0037: 33 cells, each citing its run.**

| Cells | Moved to | Evidence (dated note in this ledger) |
|---|---|---|
| FR-23 × the 8 `AWS` / `AWS.V4` configurations | `Fixed (#4341)` | task 4.11, run 2026-09-27: 286 / 0 / 2 per project, FR-23 8 / 8 per project ("The eight AWS cells are `Fixed (#4341)`") |
| FR-4, FR-5, FR-6, FR-8, FR-17 × the 4 `GCP` configurations (20) | `Fixed (#4386)` | task 5.11, 2026-09-28: 40 / 40 on a clean emulator. Repeated by task 6.31, 2026-10-01: 48 / 48 twice, with an empty per-test diff (GCP FR-23 section) |
| FR-23 × the 4 `GCP` configurations | `Fixed (#4386)` | task 6.16, 2026-10-01: 8 / 8 on a clean emulator (GCP FR-23 section) |
| FR-23 × `RocketMQ` | `Fixed (#4353)` | task 7.13, 2026-10-02: 2 / 2 on a clean store, 67 / 0 / 6 suite (RocketMQ FR-23 section) |

**Also moved on this branch, but not by spec 0037:** `GCP / Stream` and `GCP / StreamOrdering` FR-16 →
`Fixed (#4449)`, by bugfix 0024 (2026-09-30). The evidence is in the Rules note for those two cells and in
`bugfixes/0024-gcp-stream-nack-no-redelivery/bugfix.md`. So the branch moves 35 cells in all.

**Not moved by spec 0037: every remaining `Deferred` cell, 36 in all.** Each moves only on its own evidence.

- `MQTT` FR-23 (`Deferred -> #4351`): the Proactor pump deadlocks on the first requeue (see the MQTT note
  in the FR-23 section).
- `AzureServiceBus` FR-23, and its other `Deferred` cells, FR-5 and FR-9: there is no local emulator, and
  Azure Service Bus dead-letters natively.
- `GCP` FR-2, FR-7, FR-9, FR-15 and FR-22 on all four configurations, plus FR-16 on `GCP / Pull` and
  `GCP / PullOrdering` (22 cells). Spec 0037 changed neither the requeue delay, the no-channels path, delayed
  send, nor Pull `Nack`.
- `AWS / SqsFifo` and `AWS.V4 / SqsFifo` FR-9: SQS FIFO queues refuse per-message delay.
- `MSSQL`, `Redis` and `MQTT` FR-16.
- `RMQ.Async / Classic`, `RMQ.Async / Quorum` and `RMQ.Sync` FR-5: there is no separate invalid-message
  channel.
- `RocketMQ` FR-2 and FR-15. `Requeue` is still a broker no-op on the AC-24 branch. Task 7.1 found that
  `ChangeInvisibleDuration(view, 0)` works on client 5.2.1, so these two cells could now be made to pass,
  but that is outside spec 0037.

**A-6 refutations from task 6.30: none.** On all four `GCP` configurations and both variants, a message
whose rejection routing kept failing reached the native `DeadLetterPolicy` subscription in 12–17 s
(`38c91413f`). R-19's native-cap bound therefore held on the emulator, and no cell carries a refutation.

## Final run record — 2026-10-02 (spec 0037 task 8.7)

**Revision:** `5e7d78ef8`. The last code change is `30828b1c7` (7A.3's reject regeneration); everything after it
is docs-only. Every test project with an in-scope or AC-26 configuration was run whole, net10.0, against local
infrastructure, one project at a time. Two counts are reported per project:

- **Project**: every test in the project.
- **Generated**: tests whose class is declared under a `Generated/` folder. These are the conformance suites.

That scope differs from the 3.1–3.4 run record above, whose "generated suite" counts were taken with narrower
filters. So the comparison below is made by test name, not by count.

| Group | Project | Infrastructure | Project P / F / S | Generated P / F / S, per configuration | FR-23 |
|---|---|---|---|---|---|
| AC-26 | Redis | `docker-compose-redis.yaml` | 71 / 0 / 4 | Redis 30 / 0 / 4 | 2 / 2 |
| AC-26 | Kafka | `docker-compose-kafka.yaml` (RocketMQ stopped: :8081) | 198 / 4 / 0, then the 4 re-run: 4 / 0 / 0 | Classic 39 / 1 / 0 → 40 / 0 / 0 on re-run; Consumer 38 / 0 / 0; PartitionKey 40 / 0 / 0 | 6 / 6 |
| AC-26 | MSSQL | `docker-compose-mssql.yaml` | 343 / 0 / 4 | MSSQL 34 / 0 / 4 (plus the outbox suites: Binary 31 / 0 / 0, Text 31 / 0 / 0) | 2 / 2 |
| AC-26 | PostgresSQL | `docker-compose-postgres.yaml` | 261 / 0 / 0 | Postgres 38 / 0 / 0 (plus the outbox suites: Binary 31 / 0 / 0, Text 31 / 0 / 0) | 2 / 2 |
| AC-26 | RMQ.Async | `docker-compose-rmq.yaml` | 145 / 9 / 6 | Classic 40 / 0 / 2; Quorum 40 / 0 / 2 | 4 / 4 |
| AC-26 | RMQ.Sync | `docker-compose-rmq.yaml` | 81 / 9 / 3 | RMQ.Sync 40 / 0 / 2 | 2 / 2 |
| in scope | AWS | Floci, `LiveAWS!=true` | 286 / 0 / 2 | SnsFifo 40 / 0 / 0; SnsStandard 40 / 0 / 0; SqsFifo 38 / 0 / 2; SqsStandard 40 / 0 / 0 | 8 / 8 |
| in scope | AWS.V4 | Floci, `LiveAWS!=true`, after the AWS run | 286 / 0 / 2 | SnsFifo 40 / 0 / 0; SnsStandard 40 / 0 / 0; SqsFifo 38 / 0 / 2; SqsStandard 40 / 0 / 0 | 8 / 8 |
| in scope | Gcp, CI filter | Pub/Sub emulator, from `down -v` | 155 / 50 / 30 | Pull 26 / 0 / 14; PullOrdering 26 / 0 / 14 | 4 / 4 |
| in scope | Gcp, stream filter | Pub/Sub emulator, from `down -v` | 122 / 0 / 25 | Stream 30 / 0 / 10; StreamOrdering 30 / 0 / 10 | 4 / 4 |
| in scope | RocketMQ | `docker-compose-rocketmq.yaml`, from `down -v` (100 / 100 topics) | 67 / 0 / 6 | RocketMQ 34 / 0 / 4 | 2 / 2 |

FR-23 is counted as Reactor + Proactor over the project's configurations. The GCP filters are
`Category!=Spanner&Category!=GcpPubSubStream&Category!=GcpPubSubStreamOrdering&Fragile!=CI` and
`(Category=GcpPubSubStream|Category=GcpPubSubStreamOrdering)&Fragile!=CI`. Every skipped generated test is a
`Deferred` cell emitted as Skip.

**No conformance test failed, and no Pass or Fixed cell regressed.** Each failure, by name:

- **Kafka, 4: a cold broker.** The suite started six seconds after `up -d`. Three tests failed on
  `ChannelFailureException: Error finding topic …`: generated Classic Proactor
  `When_sending_a_message_should_propagate_activity_context_async`, and the hand-written
  `KafkaMessageConsumerFactoryDLQTests.When_creating_channel_with_dlq_subscription_should_pass_routing_keys` and
  `ConsumerConfigHookTests.When_using_a_consumer_config_hook`. The fourth was hand-written
  `KafkaMessageConsumerUpdateOffsetAsync.When_a_message_is_acknowledged_update_offset`, the arm that also failed in
  3.x. Re-run on the warm broker, all four passed (4 / 0 / 0, 17 s). This time `schema-registry` was up, so the two
  `KafkaMessageProducerHeaderBytesSendTests` arms passed.
- **RMQ.Async 9, RMQ.Sync 9: no mTLS broker.** These are the hand-written `Acceptance/*mtls*` tests
  (`RmqMutualTlsAcceptanceTests`, `RmqMutualTls[Quorum]Observability[Async]Tests`). All are tagged
  `Requires=Docker-mTLS` and need an `amqps://localhost:5671` broker that `docker-compose-rmq.yaml` does not provide.
  They fail on `BrokerUnreachableException` or `ChannelFailureException`, and CI excludes them
  (`Requires!=Docker-mTLS`). None is generated.
- **Gcp CI filter, 50: no real GCP.** These are the Firestore outbox (30 generated) and inbox tests, plus the GCS
  luggage-store tests. That is the known baseline of 50, and no messaging test is among them.

**Against the session baselines:**

- AWS and AWS.V4: 286 / 0 / 2 each, as at 4.11 and `181abd588`.
- GCP CI filter: 155 / 30 / 50, as before.
- GCP stream filter: 122 / 25 / 0 against 121 / 25 / 0–1. The StreamOrdering two-message Nack flake did not fire.
- RocketMQ: 67 / 0 / 6, as at `181abd588`. The FR-9 Proactor delay watch item
  (`When_sending_a_delayed_message_should_deliver_after_delay_async`) passed.
- Reject tests: all 264 results from 7A.3's runs (the ten projects above, compared by test name) have the same
  outcome here. None is missing, and none changed from pass to fail or from skip to run. MQTT and AzureServiceBus
  were not run (they are outside 8.7's scope).

**C-7 (ADR 0077, "First delivery on an approximate counter"): none observed.** No first-delivery identity
assertion failed in any run, on the exposed AWS, AWS.V4 and RocketMQ cells or anywhere else.

## Conformance Matrix

| Configuration | FR-2 | FR-4 | FR-5 | FR-6 | FR-7 | FR-8 | FR-9 | FR-15 | FR-16 | FR-17 | FR-22 | FR-23 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| AWS / SnsStandard | Pass | Pass | Pass | Pass | Pass | Pass | Fixed (#4240) | Pass | Pass | Pass | Pass | Fixed (#4341) |
| AWS / SnsFifo | Pass | Pass | Pass | Pass | Pass | Pass | Fixed (#4240) | Pass | Pass | Pass | Pass | Fixed (#4341) |
| AWS / SqsStandard | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Fixed (#4341) |
| AWS / SqsFifo | Pass | Pass | Pass | Pass | Pass | Pass | Fixed (7a0ec644f) | Pass | Pass | Pass | Pass | Fixed (#4341) |
| AWS.V4 / SnsStandard | Pass | Pass | Pass | Pass | Pass | Pass | Fixed (#4240) | Pass | Pass | Pass | Pass | Fixed (#4341) |
| AWS.V4 / SnsFifo | Pass | Pass | Pass | Pass | Pass | Pass | Fixed (#4240) | Pass | Pass | Pass | Pass | Fixed (#4341) |
| AWS.V4 / SqsStandard | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Fixed (#4341) |
| AWS.V4 / SqsFifo | Pass | Pass | Pass | Pass | Pass | Pass | Fixed (7a0ec644f) | Pass | Pass | Pass | Pass | Fixed (#4341) |
| GCP / Pull | Deferred -> #4240 (sign-off: @iancooper) | Fixed (#4386) | Fixed (#4386) | Fixed (#4386) | Deferred -> #4240 (sign-off: @iancooper) | Fixed (#4386) | Deferred -> #4240 (sign-off: @iancooper) | Deferred -> #4240 (sign-off: @iancooper) | Deferred -> #4240 (sign-off: @iancooper) | Fixed (#4386) | Deferred -> #4240 (sign-off: @iancooper) | Fixed (#4386) |
| GCP / PullOrdering | Deferred -> #4240 (sign-off: @iancooper) | Fixed (#4386) | Fixed (#4386) | Fixed (#4386) | Deferred -> #4240 (sign-off: @iancooper) | Fixed (#4386) | Deferred -> #4240 (sign-off: @iancooper) | Deferred -> #4240 (sign-off: @iancooper) | Deferred -> #4240 (sign-off: @iancooper) | Fixed (#4386) | Deferred -> #4240 (sign-off: @iancooper) | Fixed (#4386) |
| GCP / Stream | Deferred -> #4240 (sign-off: @iancooper) | Fixed (#4386) | Fixed (#4386) | Fixed (#4386) | Deferred -> #4240 (sign-off: @iancooper) | Fixed (#4386) | Deferred -> #4240 (sign-off: @iancooper) | Deferred -> #4240 (sign-off: @iancooper) | Fixed (#4449) | Fixed (#4386) | Deferred -> #4240 (sign-off: @iancooper) | Fixed (#4386) |
| GCP / StreamOrdering | Deferred -> #4240 (sign-off: @iancooper) | Fixed (#4386) | Fixed (#4386) | Fixed (#4386) | Deferred -> #4240 (sign-off: @iancooper) | Fixed (#4386) | Deferred -> #4240 (sign-off: @iancooper) | Deferred -> #4240 (sign-off: @iancooper) | Fixed (#4449) | Fixed (#4386) | Deferred -> #4240 (sign-off: @iancooper) | Fixed (#4386) |
| Kafka / Classic | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass |
| Kafka / Consumer | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass |
| Kafka / PartitionKey | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass |
| MSSQL / MSSQLMessagingGateway | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Deferred -> #4240 (sign-off: @iancooper) | Pass | Pass | Pass |
| PostgresSQL / PostgresMessagingGateway | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass |
| Redis / RedisMessagingGateway | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Deferred -> #4240 (sign-off: @iancooper) | Pass | Pass | Pass |
| RMQ.Async / Classic | Pass | Pass | Fixed (#4387) | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass |
| RMQ.Async / Quorum | Pass | Pass | Fixed (#4387) | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass |
| RocketMQ / RocketMQMessagingGateway | Deferred -> #4240 (sign-off: @iancooper) | Fixed (#4240) | Fixed (#4240) | Fixed (#4240) | Fixed (#4240) | Fixed (#4240) | Fixed (#4240) | Deferred -> #4240 (sign-off: @iancooper) | Fixed (#4240) | Fixed (#4240) | Fixed (#4240) | Fixed (#4353) |
| AzureServiceBus / AzureServiceBusMessagingGateway | Pass | Pass | Deferred -> #4240 (sign-off: @iancooper) | Pass | Pass | Pass | Deferred -> #4240 (sign-off: @iancooper) | Pass | Pass | Pass | Pass | Deferred -> #4240 (sign-off: @iancooper) |
| MQTT / MqttMessagingGateway | Fixed (#4240) | Fixed (#4240) | Fixed (#4240) | Fixed (#4240) | Fixed (#4240) | Fixed (#4240) | Fixed (#4240) | Fixed (#4240) | Deferred -> #4240 (sign-off: @iancooper) | Fixed (#4240) | Fixed (#4240) | Deferred -> #4351 (sign-off: @iancooper) |
| RMQ.Sync / RmqSyncMessagingGateway | Fixed (#4240) | Fixed (#4240) | Fixed (#4387) | Fixed (#4240) | Fixed (#4240) | Fixed (#4240) | Fixed (#4240) | Fixed (#4240) | Fixed (#4240) | Fixed (#4240) | Fixed (#4240) | Pass |
| RMQ.Async / NativeClassic | Fixed (#4388) | Pass | Pass | Pass | Pass | Pass | Fixed (#4388) | Pass | Pass | Pass | Pass | Pass |
| RMQ.Async / NativeQuorum | Fixed (#4388) | Pass | Pass | Pass | Pass | Pass | Fixed (#4388) | Pass | Pass | Pass | Pass | Pass |
| RMQ.Sync / NativeClassic | Fixed (#4388) | Pass | Pass | Pass | Pass | Pass | Fixed (#4388) | Pass | Pass | Pass | Pass | Pass |
