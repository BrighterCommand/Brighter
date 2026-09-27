# Adding a New Transport to the Conformance Suite

This guide walks a contributor through wiring a brand-new transport — NATS is the running example —
into the generated conformance suite, so that it gets all twelve canonical behaviours in both the
Reactor and Proactor variants, a row in the conformance matrix, and a CI job that runs them.

It assumes you have already written (or are writing) the gateway itself:
`src/Paramore.Brighter.MessagingGateway.Nats/` with its publication, subscription, producer, consumer
and channel factory. This guide covers only the conformance half.

Read [transport-conformance-getting-started.md](transport-conformance-getting-started.md) first if
you have not — in particular *"What the suite deliberately does NOT prove"*, because half the
mistakes made here are assertions that should never have been written.

> **Quick reference for AI agents**:
> [`.agent_instructions/generated_tests.md`](../../.agent_instructions/generated_tests.md).

## The shape of the work

You write **two** things by hand — a configuration file and a provider class — and everything else
is generated from them. You then drive your row of the conformance matrix from `Unknown` to
`Pass`/`Fixed`/`Deferred`, one cell at a time.

```
tests/Paramore.Brighter.Nats.Tests/
  test-configuration.json                          ← you write this
  MessagingGateway/NatsMessageGatewayProvider.cs   ← you write this
  MessagingGateway/Generated/                      ← generated, checked in, never hand-edited
```

## 1. Declare the configuration

Create `tests/Paramore.Brighter.Nats.Tests/test-configuration.json`:

```json
{
  "Namespace": "Paramore.Brighter.Nats.Tests",
  "MessagingGateway": {
    "Publication": "Paramore.Brighter.MessagingGateway.Nats.NatsPublication",
    "Subscription": "Paramore.Brighter.MessagingGateway.Nats.NatsSubscription",
    "MessageGatewayProvider": "Paramore.Brighter.Nats.Tests.MessagingGateway.NatsMessageGatewayProvider",
    "Category": "NATS",
    "CollectionName": "NatsMessagingGateway",
    "LedgerKey": "NATS / NatsMessagingGateway",
    "HasSupportToPublishConfirmation": false,
    "HasSupportToValidateBrokerExistence": false,
    "HasSupportToValidateInfrastructure": false
  }
}
```

- **`Category`** becomes an xUnit trait, and is what the CI job filters on.
- **`CollectionName`** becomes an xUnit collection, serialising the suite against one broker.
- **`LedgerKey`** must match your row heading in the conformance matrix **exactly**. A typo here
  does not fail loudly on its own — the ledger lookup fails *open*, meaning "run, no skip" — which
  is why `LedgerResolutionAuditTests` exists to catch an unresolvable key.
- **The feature flags are not conformance gates.** They decide whether some *legacy*, non-canonical
  templates are generated at all (publisher confirms, broker-existence validation, infrastructure
  validation). The twelve canonical behaviours are ungated by construction — you cannot opt a
  transport out of one by setting a flag. That is the point of [ADR 0066](../adr/0066-conformance-test-provider-and-ungating.md);
  a behaviour your transport cannot do is a `Deferred` ledger cell with a signed-off issue, never a
  silently absent test.

If your transport has several modes worth proving separately — as AWS does with
Standard/FIFO × SNS/SQS — declare a `MessagingGateways` dictionary of named configurations instead
of the single `MessagingGateway`, and each becomes its own generated tree and its own ledger row.
Declare one or the other, never both.

## 2. Implement the provider

The generator emits the interfaces; you implement them. One class can implement both variants:

```csharp
public class NatsMessageGatewayProvider
    : Reactor.IAmAMessageGatewayReactorProvider,
      Proactor.IAmAMessageGatewayProactorProvider
```

The Reactor surface (the Proactor surface is the same members, async):

| Member | Responsibility |
|---|---|
| `GetOrCreateRoutingKey([CallerMemberName] string testName)` | A routing key unique per test, so tests do not collide on a shared broker |
| `GetOrCreateChannelName([CallerMemberName] string testName)` | Likewise for the channel/queue name |
| `CreatePublication(routingKey, makeChannels)` | Your publication type, with transport-specific options |
| `CreateSubscription(routingKey, channelName, makeChannel, deadLetterRoutingKey, invalidMessageRoutingKey)` | Your subscription type. **The last two parameters are what make the rejection behaviours testable** — honour them |
| `CreateProducer(publication)` / `CreateProducerAsync` | A producer for that publication |
| `CreateChannel(subscription)` / `CreateChannelAsync` | A channel for that subscription |
| `GetMessageFromDeadLetterQueue(subscription)` / `…Async` | Read one message from the dead-letter destination |
| `GetMessageFromInvalidChannel(subscription)` / `…Async` | Read one message from the invalid-message destination |
| `CleanUp(producer, channel, messages)` / `CleanUpAsync` | Dispose, and remove what the test sent |
| `RejectionMetadataKeys { get; }` | The header keys your transport stamps on a rejected message |

### ⛔ The two rules that get broken most often

**1. The rejection-destination reads must NOT retry internally.**
`GetMessageFromDeadLetterQueue[Async]` and `GetMessageFromInvalidChannel[Async]` must attempt a
**single bounded receive** and return what they found. No loop, no sleep, no backoff.

This is not a style preference. The *caller* is a bounded retry loop with a wall-clock ceiling; a
helper that retries internally overruns that ceiling on its first call, so the caller's loop
re-tests an already-expired stopwatch, never runs a second iteration, and the real bound silently
becomes the helper's. It also makes every "this message must never arrive" assertion run its
internal loop to full term, turning the cheapest tests in the suite into the most expensive.
`DeadLetterPollContractAudit` fails the build on an internal loop or an internal sleep.

**2. `RejectionMetadataKeys` is all-or-nothing.**

```csharp
public RejectionMetadataKeys RejectionMetadataKeys => new(
    OriginalTopic:      "x-original-topic",
    OriginalType:       "x-original-type",
    RejectionReason:    "x-rejection-reason",
    RejectionMessage:   "x-rejection-message",
    RejectionTimestamp: "x-rejection-timestamp");
```

If your transport dead-letters through a **native broker mechanism** — as RabbitMQ does with a DLX,
or Azure Service Bus with its own dead-letter queue — the original message is routed untouched and
nothing is stamped. Declare that by returning `string.Empty` for **every** key. The metadata test
then skips its metadata block and asserts only that the message reached the dead-letter
destination.

Filling some keys and leaving others empty is a violation, and so is stamping nothing while your
ledger's FR-8 cell claims conformance without a declared relaxation.
`RejectionMetadataContractAudit` catches all three shapes, including the stale one — a declared
relaxation whose provider has since started stamping.

## 3. Add the ledger row as `Unknown`

Add one row to the matrix in
[`conformance-status.md`](../../specs/0036-universal-transport-conformance-tests/conformance-status.md),
keyed exactly as your `LedgerKey`, with every cell `Unknown`:

```
| NATS / NatsMessagingGateway | Unknown | Unknown | Unknown | … |
```

`Unknown` generates a suite that is **skipped everywhere and named everywhere** — which is the
honest starting state, and is legal during the fix phase. It is also the one value that blocks the
final cleanup merge, so you are expected to drive it out, not leave it.

## 4. Generate

```bash
dotnet build tools/Paramore.Brighter.Test.Generator
cd tests/Paramore.Brighter.Nats.Tests
dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator
```

The build step is what copies the templates into `bin/`; skip it and you generate from stale
templates. The generator uses the **current working directory** as its output root, which is why
you `cd` first. Commit the generated files — the tree is checked in, and `GeneratedTreeAudit`
compares what the generator *would* write against what is on disk, in both directions.

## 5. Add the broker and the CI job

Add `docker-compose-nats.yaml` alongside the existing ones, then a job in
`.github/workflows/ci.yml` modelled on `redis-ci`:

```yaml
  nats-ci:
    runs-on: ubuntu-latest
    timeout-minutes: 15
    needs: [build]
    services:
      nats:
        image: nats:latest
        ports:
          - 4222:4222
    steps:
      - uses: actions/checkout@v7
      - uses: actions/setup-dotnet@v6
        with:
          dotnet-version: |
            9.0.x
            10.0.x
      - run: dotnet restore
      - name: NATS Transport Tests
        run: dotnet test ./tests/Paramore.Brighter.Nats.Tests/Paramore.Brighter.Nats.Tests.csproj --filter "Fragile!=CI" --configuration Release --logger "console;verbosity=normal" --logger GitHubActions --blame -v n
```

⚠️ **Give the job a generous `timeout-minutes`.** A full conformance suite is substantially slower
than a hand-written transport suite — bounded retry loops and real broker propagation — and
several existing jobs had to be raised when the suite landed.

## 6. Drive the cells

Run the suite, and for each behaviour take one of three outcomes:

- **It passes in both variants** → cell becomes `Pass`.
- **It fails because of a gateway defect you fix in this PR** → cell becomes `Fixed (#PR)`.
- **It fails because the broker genuinely cannot do it** → cell becomes
  `Deferred -> #NNNN (sign-off: @handle)`, where `#NNNN` is a real, filed issue and the handle names
  a real maintainer who accepted it. Record *why* in the ledger's prose above the matrix; that prose
  is the most valuable part of the file.

Regenerate after every ledger edit — a cell and its generated `Skip` must agree, and an audit
checks that exact intersection rather than merely that the issue number appears somewhere.

⚠️ **A cell is `Pass` only when the test actually RAN.** A green job over a skipped suite proves
nothing. Confirm by name in the log, or with `--list-tests`, before you write `Pass`.

## The audits that will fail you, and what each means

All of these run in the `build` job, broker-free, in seconds.

| Audit | Fails when |
|---|---|
| `GeneratedTreeAudit` (missing) | A file your configuration asks for is not on disk — you changed a template or a config and did not regenerate |
| `GeneratedTreeAudit` (orphan) | A file on disk is one the generator would no longer write — a renamed or deleted template, or a flipped flag. The generator never deletes; you must |
| `GatewaySkipConventionAudit` | A `Skip` value that is not a greppable `Deferred: #<n>` marker. A bare or reasonless skip is a silent deferral with no issue behind it |
| `LedgerSkipCrossCheckAudit` | A `Skip` issue number with no matching `Deferred` ledger cell, **or** a `Deferred` cell missing its issue link or sign-off, **or** a placeholder handle like `@maintainer` |
| *(cell agreement)* | A generated `Skip` disagrees with **its own** `(LedgerKey × column)` cell — a cell flipped without regenerating, in either direction |
| `DeadLetterPollContractAudit` | A provider's DLQ/invalid-channel helper loops or sleeps internally (rule 1 above) |
| `RejectionMetadataContractAudit` | Mixed rejection keys, an undeclared routing-only provider whose FR-8 cell claims conformance, or a stale relaxation declaration |
| `PumpCoverageAudit` | A `MessageDispatch` pump test the ledger's composite claim leans on has been deleted, moved, hollowed out, or covered in only one of Reactor/Proactor |

## Checklist

- [ ] `tests/Paramore.Brighter.Nats.Tests/test-configuration.json` created; `LedgerKey` matches the
      matrix row heading exactly.
- [ ] `NatsMessageGatewayProvider` implements both the Reactor and Proactor interfaces.
- [ ] `CreateSubscription` honours `deadLetterRoutingKey` and `invalidMessageRoutingKey`.
- [ ] DLQ and invalid-channel reads do a **single bounded receive** — no internal loop or sleep.
- [ ] `RejectionMetadataKeys` returns a full set of keys, **or** `string.Empty` for every key with
      the relaxation recorded in the ledger prose.
- [ ] Ledger row added with all cells `Unknown`.
- [ ] Generated tree produced by `build` + `cd` + `run --no-build`, and committed.
- [ ] `docker-compose-nats.yaml` added; job added to `ci.yml` with a generous timeout.
- [ ] Every cell driven off `Unknown` to `Pass` / `Fixed (#PR)` / `Deferred -> #NNNN (sign-off: @handle)`.
- [ ] Each `Pass` confirmed to have **run**, by name, not inferred from a green tick.
- [ ] Every `Deferred` has a real filed issue, a real sign-off handle, and prose saying why.
- [ ] `dotnet test tests/Paramore.Brighter.Test.Generator.Tests` green — every audit above.

## Reference

- [transport-conformance-getting-started.md](transport-conformance-getting-started.md) — what the
  suite proves, and what it deliberately does not.
- [transport-conformance-new-behaviour.md](transport-conformance-new-behaviour.md) — the companion
  for adding a behaviour rather than a transport.
- [`.agent_instructions/generated_tests.md`](../../.agent_instructions/generated_tests.md) — every
  template, configuration key and feature flag.
- [ADR 0066](../adr/0066-conformance-test-provider-and-ungating.md) — the provider interface, and
  why canonical behaviours are ungated by construction.
- [ADR 0067](../adr/0067-conformance-rollout-and-deferral-governance.md) — the deferral governance
  this guide's `Unknown` → `Deferred` workflow implements.
