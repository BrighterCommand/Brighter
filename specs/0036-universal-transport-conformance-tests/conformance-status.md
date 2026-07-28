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
| MSSQL / MSSQLMessagingGateway | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| PostgresSQL / PostgresMessagingGateway | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| Redis / RedisMessagingGateway | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Deferred -> #4240 (sign-off: @maintainer) | Pass | Pass |
| RMQ.Async / Classic | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| RMQ.Async / Quorum | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| RocketMQ / RocketMQMessagingGateway | Unknown (known FR-2 gap) | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| AzureServiceBus / (not yet declared) | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| MQTT / (not yet declared) | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| RMQ.Sync / (not yet declared) | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
