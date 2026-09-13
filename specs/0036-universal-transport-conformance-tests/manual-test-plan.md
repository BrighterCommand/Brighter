# Manual test plan — Phase 2 Kafka conformance (spec 0036)

**Goal:** let a human run the generated Kafka conformance suite by hand, reproduce the three
observed failure classes, and confirm (or refute) the diagnosis that **two of the three are
test-harness gaps, not Kafka transport limitations**. When the harness gaps are patched, most of
the currently-`Deferred` Kafka cells are expected to flip to `Pass`/`Fixed`.

Related records: `specs/0036-universal-transport-conformance-tests/decision-log.md`
("Deferred: Phase 2 Kafka reference"), ledger `…/conformance-status.md`, ADRs `0066`/`0067`,
Kafka DLQ ADR `0046`.

---

## 0. TL;DR — what we already found

Running the generated canonical suite for `Kafka / Classic` + `Kafka / PartitionKey` (both
Reactor and Proactor) against a local single-broker Kafka produced three distinct failure classes.
Root causes were read from the actual errors/stack-traces, not inferred from test names:

| Class | Behaviours (FR) | Real error | Root cause | Fixable? |
|---|---|---|---|---|
| **A. Reject → DLQ / invalid-channel** | FR-4, FR-5, FR-6, FR-8, FR-17 | `Assert.NotEqual(MT_NONE…)` fails at the **DLQ-read** line (~74) | Harness hook `GetMessageFromDeadLetterQueue()` / `GetMessageFromInvalidChannel()` is a **stub returning `Message.Empty`** (= `MT_NONE`); the DLQ topic is never consumed | **Yes — harness** |
| **B. Delay** | FR-2, FR-9 | `ConfigurationException: KafkaMessageProducer: delay … requested but no scheduler is configured` | Harness `CreateProducer` wires **no `MessageSchedulerFactory`**, though Kafka has the scheduler seam | **Yes — harness/config** |
| **C. Broker flakiness** | FR-7, FR-15, FR-16, FR-22 (+ basic hand-written post/receive) | intermittent `MT_NONE` | Non-deterministic against a single dev broker under full-suite load (passed on a fresh broker, failed on a loaded re-run) | Needs a **stable broker** |

Class A and B never actually exercise the behaviour under test — the harness can't observe the
result. So the current `Deferred -> #4240` ledger state is correct ("doesn't pass today"), but the
*reason* is harness completeness, not "Kafka can't do DLQ/delay".

**Key files**
- Generated tests: `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/{Standard,PartitionKey}/Generated/{Reactor,Proactor}/*.cs`
- Harness providers (the stubs live here):
  `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/KafkaMessageGatewayProvider.cs`
  and `…/KafkaPartitionKeyMessageGatewayProvider.cs`
  - `GetMessageFromDeadLetterQueue` / `…Async` → `return Message.Empty;` (Standard ≈ line 224/216)
  - `GetMessageFromInvalidChannel` / `…Async` → `return Message.Empty;` (≈ line 237/229)
  - `CreateProducer` (≈ line 139) builds a `KafkaProducerRegistryFactory` with no scheduler
- Broker: `docker-compose-kafka.yaml` (repo root), advertises `localhost:9092`; harness hardcodes
  `BootStrapServers = ["localhost:9092"]`.

---

## 1. Prerequisites

- **.NET SDKs**: 8/9/10 installed (`dotnet --list-sdks`). The test project multi-targets
  `net9.0` and `net10.0`; **pin one TFM** for manual runs to halve wall-clock (see §3).
- **Container runtime**: Podman or Docker. ⚠️ In this environment `docker compose` (two words) was
  **not** available — only a `docker` CLI with containers already running. Use whichever of these
  works for you:
  - `docker compose -f docker-compose-kafka.yaml up -d`
  - `podman compose -f docker-compose-kafka.yaml up -d`
  - `docker-compose -f docker-compose-kafka.yaml up -d`
- **A healthy, freshly-started broker** matters for Class C — see §6.

---

## 2. How generation + the skip gate work (so you can un-skip)

Every canonical test's `[Fact(Skip = "…")]` is **ledger-driven**. The generator reads
`specs/0036-universal-transport-conformance-tests/conformance-status.md` and, per
(configuration × behaviour), emits:
- **no Skip** when the cell is `Pass`/`Fixed` → the test runs;
- `Skip = "Deferred: #<n> …"` for a `Deferred -> #<n>` cell;
- `Skip = "Deferred: #NNNN …"` for `Unknown`.

So to **run** a behaviour you flip its ledger cell to `Pass` and **regenerate**. Do not hand-edit
the generated files for a real run — regeneration overwrites them. (For a throwaway single-test
poke you *can* delete the `Skip = "…"` argument in one generated `.cs`, but the next regen restores
it.)

Both Kafka rows are currently `Deferred -> #4240` across all 11 columns, so **everything is skipped
right now**. Step §4 flips them to all-`Pass` for diagnosis.

**Regenerate command** (required `--framework net10.0`, because the generator multi-targets):
```bash
dotnet build tools/Paramore.Brighter.Test.Generator
( cd tests/Paramore.Brighter.Kafka.Tests \
  && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator --framework net10.0 )
```

---

## 3. Fast, context-safe iteration recipe

The full `~MessagingGateway` suite is ~142 tests and took **16 min per TFM** (32 min for both) when
the slow/failing delay tests ran — it will time out generic runners and floods the console. Always:

1. **Pin one TFM**: append `--framework net10.0` to `dotnet test`.
2. **Scope tightly** with a filter — one behaviour, one config, one variant. Namespaces carry the
   config + variant token, e.g. `…MessagingGateway.Standard.Reactor.…`:
   ```bash
   dotnet test tests/Paramore.Brighter.Kafka.Tests --framework net10.0 \
     --filter "FullyQualifiedName~MessagingGateway.Standard.Reactor.WhenRejectingMessageWithDeliveryErrorShouldSendToDlq"
   ```
   Use the trailing `.Standard.` / `.PartitionKey.` dot to avoid cross-matching; add `.Reactor.` or
   `.Proactor.` to pick a variant.
3. **Redirect output to a log**, inspect only the summary:
   ```bash
   dotnet test … > /tmp/k.log 2>&1; echo EXIT=$?
   grep -E "Passed!|Failed!|error|\.cs:line [0-9]+|Expected:|Actual:|Exception" /tmp/k.log | tail -40
   ```

**Behaviour → filename map** (append variant suffix `Async` for Proactor classes):

| FR | Behaviour | Generated file stem | Uses harness hook |
|---|---|---|---|
| FR-22 | plain requeue | `When_requeuing_a_failed_message_should_be_redelivered` | — |
| FR-2 | requeue with delay | `When_requeuing_a_failed_message_with_delay_should_redeliver_after_delay` | **scheduler** |
| FR-15 | zero-delay requeue | `When_requeuing_a_failed_message_with_zero_delay_should_redeliver_immediately` | — |
| FR-16 | nack → redelivered | `When_nacking_a_message_it_should_be_redelivered` | — |
| FR-7 | no channels → ack + log | `When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log` | — |
| FR-8 | reject stamps metadata | `When_rejecting_message_should_include_metadata` | **DLQ read** |
| FR-4 | delivery error → DLQ | `When_rejecting_message_with_delivery_error_should_send_to_dlq` | **DLQ read** |
| FR-17 | unknown reason → DLQ | `When_rejecting_message_with_unknown_reason_should_send_to_dlq` | **DLQ read** |
| FR-6 | unacceptable, no invalid → DLQ | `When_rejecting_message_with_unacceptable_and_no_invalid_channel_should_fallback_to_dlq` | **DLQ read** |
| FR-5 | unacceptable → invalid channel | `When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel` | **invalid-channel read** |
| FR-9 | delayed send | `When_sending_a_delayed_message_should_deliver_after_delay` | **scheduler** |

Ledger column order (in `conformance-status.md`): `FR-2 | FR-4 | FR-5 | FR-6 | FR-7 | FR-8 | FR-9 |
FR-15 | FR-16 | FR-17 | FR-22`.

---

## 4. Un-skip the Kafka suite for diagnosis

Temporarily flip **both** Kafka rows to all-`Pass`, then regenerate so every canonical test runs.

1. Edit `specs/0036-universal-transport-conformance-tests/conformance-status.md`; set the two rows:
   ```
   | Kafka / Classic | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass |
   | Kafka / PartitionKey | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass |
   ```
2. Regenerate (§2 command). Confirm the Skip is gone:
   ```bash
   grep -c "Skip = " tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Standard/Generated/Reactor/When_rejecting_message_with_delivery_error_should_send_to_dlq.cs   # expect 0
   ```
> Remember to **revert this ledger edit** (back to `Deferred -> #4240`) and regenerate when you're
> done, unless you've genuinely brought cells to `Pass` (§7). Don't commit an all-`Pass` ledger
> whose tests don't reliably pass.

---

## 5. Diagnose each class

### Class A — reject → DLQ / invalid-channel (FR-4/5/6/8/17)

**Confirm the stub is the cause (no broker needed):**
```bash
grep -n "GetMessageFromDeadLetterQueue\|GetMessageFromInvalidChannel" -A3 \
  tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/KafkaMessageGatewayProvider.cs
# You'll see each returns Message.Empty  →  Header.MessageType == MT_NONE
```
Run one Class-A test (§3) and check the stack trace points at the **DLQ-read assert (line ~74)**,
i.e. it got past receiving the original message (line ~58). That proves the reject path ran but the
harness can't read the DLQ back.

**Patch to actually observe the DLQ (both providers, sync + async).** Implement
`GetMessageFromDeadLetterQueue(subscription)` to consume the dead-letter topic. The test builds the
DLQ routing key as `"<topic>.DLQ"` and the invalid-channel routing key similarly (see the generated
test's `deadLetterRoutingKey:` / `invalidMessageRoutingKey:` args). A minimal implementation:
- create a `KafkaMessageConsumer` subscribed to `subscription.DeadLetterRoutingKey` (resp.
  `InvalidMessageRoutingKey`), **`offsetDefault: AutoOffsetReset.Earliest`**, a fresh `groupId`;
- poll `Receive(timeout)` until a non-`MT_NONE` message arrives or a ceiling elapses; return it.
- ⚠️ **Offset reset matters**: with `Latest`, a consumer created *after* the reject-produce will
  miss the message and you'll still see `MT_NONE` — a false negative. Use `Earliest`.

Re-run the five Class-A tests per config+variant. Interpretation:
- **Now pass** → the failures were purely the harness stub; flip those cells to `Fixed (#…)` (they
  needed a harness fix) or `Pass` per your convention — the *transport* was fine.
- **Still `MT_NONE` after a correct read** → now it's a real routing finding: Kafka's reject path
  isn't delivering to the DLQ. That's the genuine conformance question (cf. ADR `0046` and the
  universal-fallback design) worth escalating.

### Class B — delay (FR-2, FR-9)

**Confirm:** run `When_requeuing_a_failed_message_with_delay_should_redeliver_after_delay` (§3);
expect `ConfigurationException: KafkaMessageProducer: delay … no scheduler is configured`.

**Patch:** wire a `MessageSchedulerFactory` into the producer path the delayed tests use. Kafka
implements the scheduler seam (`IAmAChannelFactoryWithScheduler`; see hand-written
`When_kafka_channel_factory_forwards_scheduler_to_consumers` and
`When_kafka_consumer_factory_scheduler_set_after_construction` for the wiring pattern and a
`StubMessageScheduler`). Decide with the spec owner whether the universal FR-2/FR-9 harness should
provision a real scheduler (proving end-to-end delayed redelivery) or whether Kafka delayed-send is
a signed-off `Deferred`. Re-run after wiring.

### Class C — broker flakiness (FR-7/15/16/22 + basic post/receive)

These **passed on a fresh broker** and failed on a loaded re-run. To separate flake from defect:
```bash
# fully reset the broker so no accumulated topic/offset/consumer-group state remains
docker compose -f docker-compose-kafka.yaml down -v      # (or podman / docker-compose form)
docker compose -f docker-compose-kafka.yaml up -d
# wait ~20-30s for the broker to be ready, then run ONE behaviour+config+variant at a time
```
Run FR-22/15/16/7 individually (§3), one at a time, on the fresh broker. If they pass in isolation
but fail when the whole suite runs, it's contention/timing against a single dev broker → certify
`Pass` only on a stable/CI broker (FR-14 requires reliable green in **both** variants). A first
sanity check is the most basic hand-written test:
```bash
dotnet test tests/Paramore.Brighter.Kafka.Tests --framework net10.0 \
  --filter "FullyQualifiedName~MessagingGateway.Standard.Reactor.WhenPostingAMessageViaTheMessagingGatewayShouldBeReceived"
```
If even that flakes, fix the broker/environment before trusting any Kafka result.

---

## 6. Suggested order of attack

1. **Class B first** (cheap, no broker round-trips): wire a scheduler, prove FR-2/FR-9 stop throwing
   `ConfigurationException`.
2. **Class A next**: implement the DLQ / invalid-channel reads, re-run FR-4/5/6/8/17. This is where
   you learn whether Kafka reject→DLQ actually works — the interesting conformance question.
3. **Class C last**, on a freshly-reset broker, one test at a time, to certify FR-7/15/16/22.

---

## 7. Recording results & cleanup

- For each cell, set the ledger value truthfully: `Pass` (conforms as generated, both variants
  reliably green), `Fixed (#PR)` (needed an in-scope gateway fix — *harness* fixes are test-side,
  discuss whether they count as `Pass` or `Fixed`), or keep `Deferred -> #NNNN` with a real issue.
  A cell is `Pass` only if **both** Reactor and Proactor variants pass reliably (FR-14).
- **Regenerate** after any ledger change so markers match (`§2`).
- The RALPH-VERIFY gate for the whole task:
  ```bash
  dotnet build tools/Paramore.Brighter.Test.Generator \
   && ( cd tests/Paramore.Brighter.Kafka.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator --framework net10.0 ) \
   && dotnet test tests/Paramore.Brighter.Kafka.Tests --filter "FullyQualifiedName~MessagingGateway" \
   && grep -q -- 'Kafka /' specs/0036-universal-transport-conformance-tests/conformance-status.md \
   && ! ( grep -- 'Kafka /' specs/0036-universal-transport-conformance-tests/conformance-status.md | grep -q Unknown )
  ```
- If you were only diagnosing, **revert** the all-`Pass` ledger edit from §4 (back to
  `Deferred -> #4240`) and regenerate, so the committed state stays honest.
- Optional broker teardown: `docker compose -f docker-compose-kafka.yaml down -v`.

---

## 8. Notes / gotchas carried from the automated run

- A speculative one-line change to `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessageCreator.cs`
  (`AssumeUniversal` → `AssumeUniversal | AdjustToUniversal`) was tried and **reverted**: it did not
  fix any canonical behaviour and it *regressed* the hand-written `KafkaMessageConsumerUpdateOffset`
  test. It is a real latent UTC-vs-local bug worth its own bugfix, but it is **out of scope** here —
  don't reintroduce it while chasing conformance.
- The generator only ever *adds/rewrites* files; it never deletes. A ledger flip + regenerate is the
  supported way to change skip state — never rely on hand-edits surviving.
- xUnit `Skip` uses the **named-argument** form `[Fact(Skip = "…")]`; a bogus `[Fact, Skip = "…"]`
  won't compile. (Relevant only if you author templates.)
