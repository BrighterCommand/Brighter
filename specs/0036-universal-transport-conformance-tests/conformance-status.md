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
- `AWS / SqsFifo` FR-9 (delayed send) is `Deferred -> #4240`: SQS **FIFO queues do not support
  per-message delay** — `SendMessage` with `DelaySeconds` returns `AmazonSQSException: … not valid
  for this queue type`. Delayed send is proven natively for `AWS / SqsStandard`; on FIFO it would
  require an external scheduler (re-publish after the delay, as wired for Kafka), which is beyond this
  configuration's localized fix boundary. Requeue-with-delay (FR-2) conforms on FIFO because it uses
  `ChangeMessageVisibility`, which FIFO does support.
- `AWS / SnsStandard` FR-9 (delayed send) is `Fixed (#4240)`: SNS has **no native delayed publish** —
  `SnsMessageProducer.SendWithDelay` delegates a non-zero delay to the `IAmAMessageProducer.Scheduler`
  seam (as Kafka does). Two changes were needed: (1) a localized `src` fix — the **sync**
  `SnsMessageProducer.SendWithDelay` dropped its `delay` argument (passed `TimeSpan.Zero` to the inner
  overload), so the Reactor path published immediately regardless of the requested delay; it now
  forwards `delay`, matching the async path and `SqsMessageProducer`. (2) A wired harness scheduler
  (`SnsHarnessMessageScheduler`, in-scope per the deferral preconditions) that honours the delay by
  wall-clock and re-publishes to the SNS topic once it elapses. Requeue-with-delay (FR-2) is `Pass`
  natively — it is consumer-side `ChangeMessageVisibility` on the subscribed SQS queue, not an SNS
  publish, so it needs no scheduler.

## Conformance Matrix

| Configuration | FR-2 | FR-4 | FR-5 | FR-6 | FR-7 | FR-8 | FR-9 | FR-15 | FR-16 | FR-17 | FR-22 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| AWS / SnsStandard | Pass | Pass | Pass | Pass | Pass | Pass | Fixed (#4240) | Pass | Pass | Pass | Pass |
| AWS / SnsFifo | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| AWS / SqsStandard | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass |
| AWS / SqsFifo | Pass | Pass | Pass | Pass | Pass | Pass | Deferred -> #4240 (sign-off: @maintainer) | Pass | Pass | Pass | Pass |
| AWS.V4 / SnsStandard | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| AWS.V4 / SnsFifo | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| AWS.V4 / SqsStandard | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| AWS.V4 / SqsFifo | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| GCP / Pull | Unknown (known FR-2 gap) | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| GCP / PullOrdering | Unknown (known FR-2 gap) | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| GCP / Stream | Unknown (known FR-2 gap) | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| GCP / StreamOrdering | Unknown (known FR-2 gap) | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| Kafka / Standard | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass |
| Kafka / PartitionKey | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass |
| MSSQL / MSSQLMessagingGateway | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| PostgresSQL / PostgresMessagingGateway | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| Redis / RedisMessagingGateway | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| RMQ.Async / Classic | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| RMQ.Async / Quorum | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| RocketMQ / RocketMQMessagingGateway | Unknown (known FR-2 gap) | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| AzureServiceBus / (not yet declared) | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| MQTT / (not yet declared) | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| RMQ.Sync / (not yet declared) | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
