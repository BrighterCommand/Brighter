# Transport Conformance — Getting Started

Brighter supports a dozen message transports. Each one implements the same small set of consumer
behaviours — requeue, reject, dead-letter, nack, delayed send — and each broker does so with
wildly different primitives underneath. The **transport conformance suite** is how we prove that
those behaviours look the same to an application regardless of which transport is underneath.

This guide answers three questions a newcomer has: what the suite proves, what your transport
currently supports, and where to pick up work. Its companions are
[transport-conformance-new-transport.md](transport-conformance-new-transport.md) (adding a
transport) and [transport-conformance-new-behaviour.md](transport-conformance-new-behaviour.md)
(adding a behaviour).

> **Quick reference for AI agents**:
> [`.agent_instructions/generated_tests.md`](../../.agent_instructions/generated_tests.md). This
> guide is the long-form companion.

## The shape of it

One behaviour is written **once**, as a Liquid template, and generated into **every** transport's
test project. There is no per-transport copy of a conformance test to drift, and no transport that
quietly lacks one.

```
tools/Paramore.Brighter.Test.Generator/
  Templates/MessagingGateway/Reactor/   ← one template per behaviour, sync
  Templates/MessagingGateway/Proactor/  ← the same behaviours, async
  CanonicalBehaviours.cs                ← behaviour → conformance-ledger column
  ConformanceLedger.cs                  ← reads the ledger, decides what is skipped

tests/Paramore.Brighter.{Transport}.Tests/MessagingGateway/
  {Transport}MessageGatewayProvider.cs  ← hand-written; the transport-specific half
  Generated/Reactor/                    ← generated, checked in, never hand-edited
  Generated/Proactor/
```

Twelve transports are wired, as **24 configurations** — a configuration is a transport plus a mode,
so `AWS.Tests` alone contributes `SnsStandard`, `SnsFifo`, `SqsStandard` and `SqsFifo`, each of
which gets its own generated copy of every behaviour and its own row in the conformance ledger.

## The twelve canonical behaviours

`CanonicalBehaviours.cs` is the authority. Each behaviour has a Reactor template, a Proactor
template, and a column in the conformance ledger. The `FR-n` token is the **ledger column name**
— a cross-reference into the requirements, not the behaviour's identity:

| Behaviour | Ledger column |
|---|---|
| Plain requeue redelivers the message | FR-22 |
| Requeue with a delay redelivers only after the delay | FR-2 |
| An explicit `TimeSpan.Zero` delay is not special-cased | FR-15 |
| Requeue past the budget moves the message to the dead-letter queue | FR-23 |
| A delayed send arrives only after the delay | FR-9 |
| `Nack` redelivers the message | FR-16 |
| Reject with a delivery error routes to the dead-letter queue | FR-4 |
| Reject as unacceptable routes to the invalid-message channel | FR-5 |
| Reject as unacceptable with no invalid channel falls back to the DLQ | FR-6 |
| Reject with no channels configured acknowledges and logs | FR-7 |
| Reject with no reason given routes to the dead-letter queue | FR-17 |
| A rejected message carries rejection metadata | FR-8 |

### Reactor and Proactor are both mandatory

Brighter offers a synchronous (Reactor) and an asynchronous (Proactor) channel surface, and a
transport can conform on one and not the other — an async path that deadlocks, a sync path that
swallows a delay. So every behaviour is generated **twice**, driving `IAmAChannelSync` /
`IAmAMessageProducerSync` and `IAmAChannelAsync` / `IAmAMessageProducerAsync` respectively.

A conformance cell is `Pass` only when **both** variants are green against a live broker. If one
passes and the other does not, the cell is `Deferred`, not `Pass`.

## What the suite deliberately does NOT prove

This is the part most often misread, and each exclusion is deliberate.

- **It never asserts *how* a behaviour is achieved.** A transport may dead-letter through a native
  broker mechanism (RabbitMQ's DLX, Azure Service Bus's own dead-letter queue) or through
  Brighter's own fallback; a delay may be native or delegated to the scheduler seam. The suite
  asserts only that the observable behaviour holds, and only against the channel and producer
  surfaces. Adding a "and it used the native path" assertion is a defect, not an improvement.
- **It never asserts delivery *order*, and never assumes it.** Ordering is a transport property,
  not a Brighter behaviour, and the targeted transports do not share one — SNS/SQS Standard and GCP
  Pub/Sub without an ordering key guarantee nothing at all. A multi-message arm identifies each
  received message **by id** against what it sent, and states its assertions over the **set** of ids
  seen. Transports are at-least-once unless they say otherwise, so arms tolerate repeat receipts and
  an exact-count assertion over received messages is forbidden. *(A guarantee the transport does
  offer is still void across a delay: the scheduler fallback re-publishes, and a re-publish is a new
  enqueue at the tail.)*
- **It never drives a message pump.** The conformance claim is compositional. This suite proves the
  transport honours a requeue or a reject; deciding *when* to requeue or reject is the pump's job,
  and is proven separately against in-memory channels in
  `tests/Paramore.Brighter.Core.Tests/MessageDispatch`. `PumpCoverageAudit` is what stops those two
  halves drifting apart — delete a pump test and it fails the build rather than letting every
  conformance cell stay green over a claim that has quietly become false.
- **Timing is bounded, not exact.** Timing-dependent tests use a bounded receive-retry loop — a
  500 ms poll interval against a 30 s ceiling for a channel arrival, 60 s for a rejection
  destination — rather than a fixed sleep and a single receive, so broker propagation delay does not
  cause a false failure. The ceiling lives in the **test**; a provider helper must do a single
  bounded receive and must never loop or sleep internally, which `DeadLetterPollContractAudit`
  enforces.

## Reading the conformance matrix

[`specs/0036-universal-transport-conformance-tests/conformance-status.md`](../../specs/0036-universal-transport-conformance-tests/conformance-status.md)
is the single source of truth for what each configuration is known to do — 24 rows × 12 behaviour
columns, and, above the matrix, the per-cell rationale, which is the best prose in the repository on
what a given transport genuinely cannot do and why.

| Cell value | What the generated test does | What it means |
|---|---|---|
| `Pass` | runs | Conforms as generated, both variants, against a live broker |
| `Fixed (#PR)` | runs | Conforms after an in-spec gateway fix, linked |
| `Deferred -> #NNNN (sign-off: @handle)` | **skipped, naming the issue** | A known, accepted, maintainer-signed-off gap — **this is the work queue** |
| `Unknown` | skipped with a placeholder | Transient; only legal while a behaviour is being driven to conformance |

The generator reads this file. Editing a cell changes what runs — which is why a cell and its
generated `Skip` cannot disagree: `LedgerSkipCrossCheckAudit` fails the build in **both**
directions, and it rejects a placeholder sign-off handle such as `@maintainer`, which has the shape
of accountability while naming nobody.

## How to pick up work

**A `Deferred` cell is a work item, and the skipped test is its specification.** To find one:

```bash
grep -n 'Deferred ->' specs/0036-universal-transport-conformance-tests/conformance-status.md
```

Each hit gives you the configuration, the behaviour, and a linked issue. The generated test for
that cell is already written, already checked in, and carries a `Skip` naming the same issue:

```csharp
[Fact(Skip = "Deferred: #4341 — requeue budget exhausted to DLQ not yet conformant for AWS / SqsStandard (maintainer sign-off)")]
```

So the loop is: read the issue and the ledger's prose for that cell, fix the gateway, flip the cell
to `Fixed (#PR)`, regenerate, and the `Skip` disappears on its own. You do not edit the generated
test — you change the ledger and regenerate.

## Running one transport locally

Each transport needs its broker. Start it from the matching compose file, then run that project's
tests with the same filter CI uses:

```bash
docker compose -f docker-compose-redis.yaml up -d
dotnet test ./tests/Paramore.Brighter.Redis.Tests/Paramore.Brighter.Redis.Tests.csproj \
  --filter "Fragile!=CI" --logger "console;verbosity=normal"
```

Substitute the transport: `docker-compose-kafka.yaml`, `-rmq.yaml`, `-mqtt.yaml`,
`-postgres.yaml`, `-mssql.yaml`, `-rocketmq.yaml`, `-localstack.yaml`, `-gcp.yaml`. The
`Fragile!=CI` filter is not optional — without it you also pick up tests deliberately excluded from
CI. RabbitMQ needs one more exclusion:

```bash
dotnet test ./tests/Paramore.Brighter.RMQ.Async.Tests/Paramore.Brighter.RMQ.Async.Tests.csproj \
  --filter "Fragile!=CI&Requires!=Docker-mTLS"
```

Without `Requires!=Docker-mTLS` you get mutual-TLS failures for a certificate you have not
generated; those need a separate broker and are covered by
[RABBITMQ_MTLS_TESTING_GUIDE.md](../../RABBITMQ_MTLS_TESTING_GUIDE.md).

⚠️ **Some configurations cannot be run locally at all.** GCP's four are emulator-only and the
emulator cannot create a dead-letter subscription; AWS needs real AWS for several behaviours, since
the LocalStack mock serialises delivery and so cannot see an ordering defect that real SQS shows.
The ledger's prose says which, per cell. Trust CI over your laptop for those.

## Reference

- [`.agent_instructions/generated_tests.md`](../../.agent_instructions/generated_tests.md) — the
  full reference: every template, every configuration key, every feature flag, the regeneration
  recipe, the tree audit, the CI-flakiness rules.
- [`tools/Paramore.Brighter.Test.Generator/README.md`](../../tools/Paramore.Brighter.Test.Generator/README.md)
  — the generator itself.
- [Conformance matrix](../../specs/0036-universal-transport-conformance-tests/conformance-status.md)
  — per-configuration state and per-cell rationale.
- [ADR 0035](../adr/0035-generated-test.md) — why tests are generated.
- [ADR 0066](../adr/0066-conformance-test-provider-and-ungating.md) — the provider interface and
  capability-gate retirement.
- [ADR 0067](../adr/0067-conformance-rollout-and-deferral-governance.md) — rollout sequencing and
  deferral governance.
