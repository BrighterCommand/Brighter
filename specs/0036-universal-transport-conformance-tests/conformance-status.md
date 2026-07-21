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

## Conformance Matrix

| Configuration | FR-2 | FR-4 | FR-5 | FR-6 | FR-7 | FR-8 | FR-9 | FR-15 | FR-16 | FR-17 | FR-22 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| AWS / SnsStandard | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| AWS / SnsFifo | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| AWS / SqsStandard | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| AWS / SqsFifo | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| AWS.V4 / SnsStandard | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| AWS.V4 / SnsFifo | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| AWS.V4 / SqsStandard | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| AWS.V4 / SqsFifo | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| GCP / Pull | Unknown (known FR-2 gap) | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| GCP / PullOrdering | Unknown (known FR-2 gap) | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| GCP / Stream | Unknown (known FR-2 gap) | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| GCP / StreamOrdering | Unknown (known FR-2 gap) | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| Kafka / Standard | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| Kafka / PartitionKey | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| MSSQL / MSSQLMessagingGateway | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| PostgresSQL / PostgresMessagingGateway | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| Redis / RedisMessagingGateway | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| RMQ.Async / Classic | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| RMQ.Async / Quorum | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| RocketMQ / RocketMQMessagingGateway | Unknown (known FR-2 gap) | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| AzureServiceBus / (not yet declared) | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| MQTT / (not yet declared) | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| RMQ.Sync / (not yet declared) | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
