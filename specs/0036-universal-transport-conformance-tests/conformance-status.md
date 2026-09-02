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
| `Deferred -> #NNNN (sign-off: @maintainer)` | A named, linked, maintainer-signed-off follow-up issue. |

## Rules

- **Placeholder rows** (transports with no gateway configuration declared yet) occupy a single row
  per transport. Their cells may resolve only to `Deferred -> #NNNN (sign-off: @maintainer)` — never
  to `Pass` or `Fixed` — because no generated suite exists to pass.
- A behaviour is `Pass` or `Fixed` only when **both** the Reactor and Proactor variants pass against
  a running broker (FR-14). If only one variant passes, the cell must be `Deferred`.
- Five cells in the FR-2 column carry a pre-identified non-conformance annotation. GCP's four
  consumers redeliver immediately (the delay argument is ignored); RocketMQ's requeue is a no-op
  holding the message under the native invisibility timeout with no delay applied. These are seeded
  ahead of any generation run rather than discovered late.
- The cleanup gate is evaluated over all twelve targeted transports, not over whichever rows happen
  to exist.
- `AWS / SqsFifo` **and `AWS.V4 / SqsFifo`** FR-9 (delayed send) are `Deferred -> #4240`: SQS **FIFO
  queues do not support per-message delay** — `SendMessage` with `DelaySeconds` returns
  `AmazonSQSException: … not valid for this queue type`. Delayed send is proven natively for
  `AWS / SqsStandard` (and `AWS.V4 / SqsStandard`); on FIFO it would require an external scheduler
  (re-publish after the delay, as wired for Kafka), which is beyond this configuration's localized fix
  boundary. The V4 gateway shares the same AWS SQS platform limit, so the deferral applies identically.
  Requeue-with-delay (FR-2) conforms on FIFO because it uses `ChangeMessageVisibility`, which FIFO does
  support.
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
- `AWS / SnsFifo` **and `AWS.V4 / SnsFifo`** FR-9 (delayed send) are `Fixed (#4240)` — and, unlike
  `AWS / SqsFifo` (+ `AWS.V4 / SqsFifo`), they are **not**
  deferred. SqsFifo's deferral was because SQS FIFO **rejects native per-message `DelaySeconds`**; SNS
  FIFO never uses that path — the SNS producer delegates the delay to the `Scheduler` seam, so the FIFO
  platform limit does not apply. The `SnsHarnessMessageScheduler` re-publishes to the FIFO topic after
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
- `RMQ.Async / Classic` is `Pass` on **ten** behaviours and `Deferred -> #4240` on **FR-5 only**
  (a separate invalid channel). **⚠️ Reference-environment fix first:** `docker-compose-rmq.yaml` pointed
  at `rabbitmq:management` (now RabbitMQ 4.3), which **hard-rejects the transient non-exclusive queues the
  gateway declares** (`INTERNAL_ERROR - Feature 'transient_nonexcl_queues' is deprecated`) — every test
  failed at queue declaration. Pinned it to the CI/reference image
  `brightercommand/rabbitmq:4.2-management-delay` (RabbitMQ 4.2, matching CI and the root
  `docker-compose.yaml`); a fresh data volume is required (downgrading 4.3→4.2 over a stale volume crashes
  the broker on boot). **Requeue/redeliver + no-channel ack (FR-7/15/16/22) `Pass` natively** — notably
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
  `RejectionMetadataKeys.StampsRejectionMetadata` (true iff the provider declares non-empty keys). So
  FR-4/6/8/17 `Pass` for RMQ (routing) while metadata-stamping transports (SQS/Redis/Postgres/MSSQL, ADRs
  `0038`/`0039`/`0040`/`0041`) still assert the full metadata. **⛔ FR-5 (a *separate* invalid channel)
  stays `Deferred -> #4240`:** neither `RmqMessageConsumer` nor `RmqSubscription` models an invalid
  destination — an unacceptable rejection dead-letters to the *DLQ*, not a distinct invalid channel (the
  real invalid read hook observes `MT_NONE`). This is not relaxed by FR-8 (it is a routing gap, not a
  metadata gap); conforming requires Brighter-managed invalid routing in `src/…RMQ.Async` (three deferral
  preconditions met: evidence recorded, the invalid read hook was implemented, the residual is a
  substantial src change).
- `RMQ.Async / Quorum` mirrors `RMQ.Async / Classic` exactly: **10 `Pass` + FR-5 `Deferred -> #4240`**.
  Quorum queues use the same RabbitMQ AMQP gateway (`src/Paramore.Brighter.MessagingGateway.RMQ.Async`),
  so every conformance argument that applies to Classic applies to Quorum. **Delay (FR-2/FR-9) `Pass` via
  the same wired `RmqHarnessMessageScheduler`** — the Quorum provider presents a plain (non-delay) durable
  exchange so `DelaySupported == false` and the scheduler seam is exercised (delivered `Header.Delayed ==
  TimeSpan.Zero`, honoured delay; same as Classic). **Reject→DLQ (FR-4/6/17) and metadata (FR-8) `Pass`
  via the native DLX under the FR-8 relaxation** — Quorum queues support `x-dead-letter-exchange` /
  `x-dead-letter-routing-key` identically to Classic queues; `RejectionMetadataKeys` is empty → routing
  only asserted (same as Classic). **FR-16 `Pass`** — `RmqMessageConsumer.NackAsync` → `BasicNackAsync(requeue:
  true)` → broker redelivers, same mechanism as Classic. **FR-7/15/22 `Pass`** natively. **⛔ FR-5 (a
  *separate* invalid channel) stays `Deferred -> #4240`** — same architectural src gap as Classic: an
  unacceptable rejection dead-letters to the DLX, not a distinct invalid channel; the real invalid read
  hook observes `MT_NONE` (evidence from the Quorum test run on a live 4.2 broker, both variants); the
  residual is a substantial src change to `src/…RMQ.Async` (three deferral preconditions met).
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
- `RMQ.Sync / RmqSyncMessagingGateway` — **10 `Fixed (#4240)` + FR-5 `Deferred -> #4240`**, both
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
  correctly by `RmqMessageAssertion`. **FR-16/22 `Fixed`** natively. **⛔ FR-5 (a *separate* invalid
  channel) `Deferred -> #4240`** — same architectural src gap as RMQ.Async: an unacceptable rejection
  calls `BasicReject` which dead-letters to the DLX, not a distinct invalid channel; the real invalid
  read hook observes `MT_NONE` (evidence from live 4.2 broker, both variants); the residual is a
  substantial src change to `src/…RMQ.Sync` (three deferral preconditions met).
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
- `AzureServiceBus / AzureServiceBusMessagingGateway` — **ALL ELEVEN cells `Deferred -> #4240
  (sign-off: @maintainer)` on INFRA grounds.** AC-23 makes *"inability to provide CI infrastructure"* a
  valid ground for deferral, and ADR `0067` ("Negative") anticipated ASB landing here. **This is a
  deferral of VERIFICATION, not a declaration of non-conformance** — no ASB behaviour has been observed
  to fail; none has been observed at all. The configuration stays in the target set and is never dropped
  (FR-21).
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
    **Precedent**: this is the same resolution as `GCP / Stream` + `/ StreamOrdering`, where the scoped
    suite could not be run locally and **the no-`Unknown` ledger is the gate**, with `dotnet test`
    verification deferred to real infrastructure.
  - **To close these deferrals**: supply `BrighterTestsASBConnectionString` (or
    `BrighterTestsASBNameSpace`) against a real namespace, or stand up the Service Bus emulator, flip the
    cells, and regenerate. No code change is expected to be needed to *run* — only to fix whatever the
    run then reveals.

## Conformance Matrix

| Configuration | FR-2 | FR-4 | FR-5 | FR-6 | FR-7 | FR-8 | FR-9 | FR-15 | FR-16 | FR-17 | FR-22 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| AWS / SnsStandard | Pass | Pass | Pass | Pass | Pass | Pass | Fixed (#4240) | Pass | Pass | Pass | Pass |
| AWS / SnsFifo | Pass | Pass | Pass | Pass | Pass | Pass | Fixed (#4240) | Pass | Pass | Pass | Pass |
| AWS / SqsStandard | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass |
| AWS / SqsFifo | Pass | Pass | Pass | Pass | Pass | Pass | Deferred -> #4240 (sign-off: @maintainer) | Pass | Pass | Pass | Pass |
| AWS.V4 / SnsStandard | Pass | Pass | Pass | Pass | Pass | Pass | Fixed (#4240) | Pass | Pass | Pass | Pass |
| AWS.V4 / SnsFifo | Pass | Pass | Pass | Pass | Pass | Pass | Fixed (#4240) | Pass | Pass | Pass | Pass |
| AWS.V4 / SqsStandard | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass |
| AWS.V4 / SqsFifo | Pass | Pass | Pass | Pass | Pass | Pass | Deferred -> #4240 (sign-off: @maintainer) | Pass | Pass | Pass | Pass |
| GCP / Pull | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) | Pass | Deferred -> #4240 (sign-off: @maintainer) | Fixed (#4240) | Pass | Pass | Deferred -> #4240 (sign-off: @maintainer) | Pass |
| GCP / PullOrdering | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) | Pass | Deferred -> #4240 (sign-off: @maintainer) | Fixed (#4240) | Pass | Pass | Deferred -> #4240 (sign-off: @maintainer) | Pass |
| GCP / Stream | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) |
| GCP / StreamOrdering | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) |
| Kafka / Standard | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass |
| Kafka / PartitionKey | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass |
| MSSQL / MSSQLMessagingGateway | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Deferred -> #4240 (sign-off: @maintainer) | Pass | Pass |
| PostgresSQL / PostgresMessagingGateway | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass |
| Redis / RedisMessagingGateway | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Deferred -> #4240 (sign-off: @maintainer) | Pass | Pass |
| RMQ.Async / Classic | Pass | Pass | Deferred -> #4240 (sign-off: @maintainer) | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass |
| RMQ.Async / Quorum | Pass | Pass | Deferred -> #4240 (sign-off: @maintainer) | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass |
| RocketMQ / RocketMQMessagingGateway | Deferred -> #4240 (sign-off: @maintainer) | Fixed (#4240) | Fixed (#4240) | Fixed (#4240) | Fixed (#4240) | Fixed (#4240) | Fixed (#4240) | Deferred -> #4240 (sign-off: @maintainer) | Fixed (#4240) | Fixed (#4240) | Fixed (#4240) |
| AzureServiceBus / AzureServiceBusMessagingGateway | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) | Deferred -> #4240 (sign-off: @maintainer) |
| MQTT / MqttMessagingGateway | Fixed (#4240) | Fixed (#4240) | Fixed (#4240) | Fixed (#4240) | Fixed (#4240) | Fixed (#4240) | Fixed (#4240) | Fixed (#4240) | Deferred -> #4240 (sign-off: @maintainer) | Fixed (#4240) | Fixed (#4240) |
| RMQ.Sync / RmqSyncMessagingGateway | Fixed (#4240) | Fixed (#4240) | Deferred -> #4240 (sign-off: @maintainer) | Fixed (#4240) | Fixed (#4240) | Fixed (#4240) | Fixed (#4240) | Fixed (#4240) | Fixed (#4240) | Fixed (#4240) | Fixed (#4240) |
