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
| GCP / Pull | Unknown (known FR-2 gap) | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| GCP / PullOrdering | Unknown (known FR-2 gap) | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| GCP / Stream | Unknown (known FR-2 gap) | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| GCP / StreamOrdering | Unknown (known FR-2 gap) | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| Kafka / Standard | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass |
| Kafka / PartitionKey | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass |
| MSSQL / MSSQLMessagingGateway | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Deferred -> #4240 (sign-off: @maintainer) | Pass | Pass |
| PostgresSQL / PostgresMessagingGateway | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass |
| Redis / RedisMessagingGateway | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Deferred -> #4240 (sign-off: @maintainer) | Pass | Pass |
| RMQ.Async / Classic | Pass | Pass | Deferred -> #4240 (sign-off: @maintainer) | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass |
| RMQ.Async / Quorum | Pass | Pass | Deferred -> #4240 (sign-off: @maintainer) | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass |
| RocketMQ / RocketMQMessagingGateway | Unknown (known FR-2 gap) | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| AzureServiceBus / (not yet declared) | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| MQTT / (not yet declared) | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| RMQ.Sync / (not yet declared) | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
