# Bugfix: Canonical FR-7 and FR-16 two-message arms assume broker delivery order, so they fail on unordered SNS/SQS Standard queues

**Linked Issue**: _(none — surfaced by CI on `2e650c36e`)_
**Status**: Verified (generator + build; broker execution is owed to CI — see Fix)

## Symptom

CI run 35220513183, head SHA `2e650c36e2c5225172debca14b69542b5f93f240`, branch `feature/4240-universal-transport-conformance-tests`: the `aws-ci` job is RED. Every failure is one of exactly two generated canonical arms:

- `When_nacking_first_of_two_messages_should_redeliver_nacked_then_receive_second` (FR-16)
- `When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log` (FR-7)

in `Paramore.Brighter.AWS.Tests.MessagingGateway.SnsStandard.{Reactor,Proactor}` and `...SqsStandard.{Reactor,Proactor}`.

Expected: the arm receives its two messages in send order. Observed: it receives the *other* of its own two messages.

```
Assert.Equal() Failure: Values differ
Expected: 01a0af59-8f26-7797-be9c-6475addab1e7
Actual:   01a0af59-8f26-72f6-8f24-56b5c8a1636a
   at AwsMessageAssertion.Assert(...) AwsMessageAssertion.cs:line 49
   at ...SnsStandard.Proactor....When_nacking_first_of_two_messages_...() line 139
```

`AwsMessageAssertion.cs:49` is `Xunit.Assert.Equal(expected.Header.MessageId, actual.Header.MessageId)`. Both ids are UUIDv7 stamped in the same millisecond — they are the arm's own two messages. Nothing was lost; the wrong one arrived first.

Two discriminators, both measured:

1. **Only the `Standard` variants fail.** Every `SnsFifo` / `SqsFifo` copy of the same two arms passes in the same job.
2. **Non-deterministic.** The `aws-ci` job runs two projects back to back — `Paramore.Brighter.AWS.Tests` then `Paramore.Brighter.AWS.V4.Tests` (`.github/workflows/ci.yml:560-562`), the same generated arms against the two AWS SDK majors. The failing sets differ between them (`Failed: 4`, then `Failed: 3`), and `SqsStandard.Proactor` nack is `Passed` in one and `Failed` in the other.

A third discriminator, from the workflow itself: **`aws-mock-ci` is green.** It runs the *same* generated Standard arms against an in-process LocalStack-compatible emulator (`floci/floci:1.5.19`, `.github/workflows/ci.yml:478-494`), while `aws-ci` (`.github/workflows/ci.yml:524`) runs them against a real AWS account in `eu-west-1`. Same code, same configuration; passes on the emulator, flaky on the real broker.

Reproduction route: run `dotnet test ./tests/Paramore.Brighter.AWS.Tests/Paramore.Brighter.AWS.Tests.csproj --filter "Fragile!=CI"` against a **real** AWS account (not the emulator), repeatedly. The two arms fail intermittently on the four `*Standard` namespaces only.

## Suspected Location

Templates are the source of truth; the generated `.cs` files are outputs.

**FR-16 — nack, two-message arm (Proactor template)**
`tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Proactor/When_nacking_a_message_it_should_be_redelivered.cs.liquid`
- `:117-118` — two sequential sends, `nackedMessage` then `followingMessage`
- `:121` — first receive, **assumed to be `nackedMessage`** (never asserted to be)
- `:139` — `_messageAssertion.Assert(nackedMessage, redelivered);` ← the assertion in the stack trace
- `:159` — `_messageAssertion.Assert(followingMessage, receivedFollowing);`

**FR-16 — nack, two-message arm (Reactor template)**
`.../Templates/MessagingGateway/Reactor/When_nacking_a_message_it_should_be_redelivered.cs.liquid`
- `:111-112` sends, `:115` first receive, `:133` first identity assertion, `:153` second.

**FR-7 — no-channels rejection (Reactor template)**
`.../Templates/MessagingGateway/Reactor/When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log.cs.liquid`
- `:68-69` sends `rejectedMessage` then `followingMessage`
- `:72` first receive, **assumed to be `rejectedMessage`**
- `:75` rejects whatever arrived
- `:97` `_messageAssertion.Assert(followingMessage, receivedFollowing);`

**FR-7 — no-channels rejection (Proactor template)**
`.../Templates/MessagingGateway/Proactor/When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log.cs.liquid` — `:74-75`, `:78`, `:81`, `:103`.

**Rendered copies (outputs, do not hand-edit)**
- `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/SnsStandard/Generated/Proactor/When_nacking_a_message_it_should_be_redelivered.cs:139` — verified to be `_messageAssertion.Assert(nackedMessage, redelivered);`, matching the trace.
- `.../SnsStandard/Generated/Reactor/When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log.cs:72` (first receive) and `:97` (terminal identity assertion).

**Assertion helper**
`tests/Paramore.Brighter.AWS.Tests/AwsMessageAssertion.cs:49` (MessageId) and `:60` (Body) — the two fields that make the arm's messages distinguishable, and the two the distinct-id commits deliberately made distinct.

**Why FIFO behaves differently (harness, verified)**
- `tests/Paramore.Brighter.AWS.Tests/test-configuration.json:20,43` — `SnsFifo` and `SqsFifo` alone set `"MessageBuilder": "FifoMessageBuilder"`; `SnsStandard`/`SqsStandard` use the default builder.
- `tests/Paramore.Brighter.AWS.Tests/FifoMessageBuilder.cs:45` — the constructor stamps **one** partition key per *builder instance*. Each generated test class holds a single `_messageBuilder`, so both of an arm's messages carry the **same** partition key → the same `MessageGroupId`.
- `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/FifoMetadataProducer.cs:73-76` — the wrapper only supplies a group id when none is present, so it does not break that shared group; `:81` supplies a per-send dedup id.

**Broker-side mechanism (verified)**
- `src/Paramore.Brighter.MessagingGateway.AWSSQS/SqsMessageConsumer.cs:348` — `Nack` is `ChangeMessageVisibility(..., 0)`: it makes the message visible again and lets SQS choose what to hand out next. It does not push it to the head of a queue.
- `SqsMessageConsumer.cs:264-272` — with neither DLQ nor invalid channel, `Reject` calls `Acknowledge` (delete) and returns `true`. The rejected message is genuinely gone, so FR-7's failure cannot be the rejected message "coming back".
- `SqsMessageConsumer.cs:188-190` — receive uses `MaxNumberOfMessages = _batchSize`, which is `Subscription`'s `bufferSize`, default `1` (`src/Paramore.Brighter/Subscription.cs:200`). One message per call, chosen by SQS.

## Root-Cause Hypothesis

**Both arms assert a delivery *order* that the specification never requires and that an SQS/SNS Standard queue does not provide.**

The ordering assumption sits in two places per arm, not one:

- FR-16: the **unasserted** assumption at template `:121`/`:115` that the first receive yields `nackedMessage`, plus the **asserted** claim at `:139`/`:133` that the message returned after `Nack` is the nacked one rather than the one queued behind it.
- FR-7: the **unasserted** assumption at template `:72`/`:78` that the first receive yields `rejectedMessage`. Because `Reject` deletes (`SqsMessageConsumer.cs:271`), if the first receive returned `followingMessage` then `followingMessage` is deleted and the only message left is `rejectedMessage` — so the terminal assertion at `:97`/`:103` fails with the *other* id. Same failure signature, different mechanism: FR-7's ordering assumption is upstream of its terminal assertion.

Against a real Standard queue: SQS Standard is documented as best-effort ordering and at-least-once delivery, and SNS fan-out to a Standard queue preserves no order either. With `MaxNumberOfMessages = 1`, each `ReceiveMessage` samples a subset of SQS servers, so which of two visible messages comes back is genuinely arbitrary per call.

**This explains the FIFO/Standard split.** FIFO passes because of *genuine* broker ordering, but only because the harness puts both of an arm's messages in one message group: `FifoMessageBuilder.cs:45` stamps a single partition key per builder instance, and `FifoMetadataProducer.cs:73-76` leaves it alone. A FIFO message group is strictly ordered *and* blocked while a message from it is in flight, so the first receive must be the first send, and the nacked message must be re-offered before the one behind it. FIFO therefore satisfies an assumption the template never states — it is not evidence that the assumption is a conformance obligation. (Equally: the same harness detail is what makes FIFO's pass fragile. Give the two messages different groups and FIFO would be free to reorder them too.)

**This explains the non-determinism.** The ordering is a coin flip per receive, so any given run fails a random subset of the eight Standard arms (4 namespaces × 2 SDK majors in one job), which is exactly the `Failed: 4` / `Failed: 3` pattern with `SqsStandard.Proactor` passing in one project and failing in the other. It also explains `aws-mock-ci` being green: the emulator is an in-memory, effectively FIFO implementation, so the coin always lands the same way there.

**What the other transports' passes do and do not tell us.** The conformance matrix (`specs/0036-universal-transport-conformance-tests/conformance-status.md`, Conformance Matrix) records FR-7 and FR-16 as `Pass` for Kafka ×3, RMQ ×3, Redis, Postgres, MSSQL, Azure Service Bus and GCP. Those brokers are ordered per partition/queue/subscription, so the passes confirm the *behaviour* under ordered delivery — they say nothing about whether the arm is a valid conformance test, because ordering is not a conformance requirement: `requirements.md` contains no ordering obligation anywhere (a grep for "order"/"ordering"/"at-least-once" finds only the fallback-*ladder* routing order at `:76` and unrelated prose).

Other wired configurations that are unordered and therefore latently exposed to the same failure:
- `AWS.V4 / SnsStandard` and `AWS.V4 / SqsStandard` — **already failing**; they are inside the same `aws-ci` job, not a separate green one.
- `GCP / Pull` and `GCP / Stream` — the non-ordering variants; only `GcpPullOrderingMessageGatewayProvider.cs:139` and `GcpStreamOrderingMessageGatewayProvider.cs:139` set `EnableMessageOrdering = true`. These run against the Pub/Sub emulator, which likely masks it the way `floci` does for AWS.
- RocketMQ normal topics and MQTT are further candidates, not verified here.

**Is the ordering intrinsic to the behaviour?** No, and the spec text says so twice over, though its *examples* embed the order:

> **FR-7** — "`channel.Reject(M, ...)` returns `true` — the message is removed rather than redelivered — and the channel goes on to receive the next message without blocking." (`requirements.md:176-180`)
> **FR-16** — "proves the nacked message is redelivered and the following message is not blocked behind it." (`requirements.md:365-369`)

Neither obligation mentions order. The *illustrations* do — FR-7's "*Example:* send `M1` then `M2`; receive `M1`; ... the next receive yields `M2`" (`requirements.md:182-183`), AC-7 (`requirements.md:634-636`) and AC-17's "after nacking `M` the redelivered `M` is received and then `M2` is received" (`requirements.md:698-701`). The arms encode the illustration rather than the obligation. The behaviour actually specified is *set*-shaped: both messages are received, the nacked one reappears, the rejected one does not, and neither blocks the other.

**Other canonical arms of the same shape.** Only these two templates send more than one message (verified: a count of send calls across all 42 messaging-gateway templates yields exactly these four files). The one other multi-message arm, `When_a_message_consumer_reads_multiple_messages_should_receive_all_messages`, sends four messages but is **already order-insensitive** — it looks the expected message up by id before asserting (`.../Templates/MessagingGateway/Reactor/When_a_message_consumer_reads_multiple_messages_should_receive_all_messages.cs.liquid:74`, Proactor `:81`). So there is no third latent arm, and the repo already contains the idiom for the fix.

**Suggested remedy — UNVERIFIED, to be proven or refuted in `/bugfix:confirm`:** rewrite both templates to assert the set rather than the sequence — identify each received message by id against the two sent messages (the `FirstOrDefault(x => x.Header.MessageId == received.Header.MessageId)` idiom at `..._multiple_messages...cs.liquid:74`), then assert the FR-specific claims that do not depend on order: for FR-16, that the nacked message is among those received after the `Nack` and that the other message is also received; for FR-7, that `Reject` returns `true` on whichever message arrived first and that the *other* message is subsequently received. Two consequences to check before proposing that: (a) four generator golden tests pin the current text — `tests/Paramore.Brighter.Test.Generator.Tests/CanonicalTemplates/When_generating_nack_test_should_emit_redelivery_and_two_message_variant_both_variants.cs:262` and `:285`, and `.../When_generating_no_channels_reject_should_emit_ack_and_continue_both_variants.cs:214` and `:237`, all `Assert.Contains("_messageAssertion.Assert(followingMessage, receivedFollowing)", content)` — so they must move with the templates; (b) SQS Standard is also *at-least-once*, so a set-based rewrite that asserts "exactly once" would trade an ordering flake for a duplicate flake.

**What would REFUTE this hypothesis.** Instrument every receive in the two arms to log the received `MessageId`, and run them repeatedly against real AWS Standard queues. The hypothesis predicts that every failure coincides with a receive returning the other of the two ids the test sent, in send-order-violating position, and never a third id, a rewritten id, or a missing message. It is refuted if failures occur while the receives *do* return the ids in send order (which would mean the id mismatch comes from somewhere other than delivery order — e.g. the transport rewriting `Header.MessageId` on redelivery), or if a variant that removes the ordering freedom entirely — send `followingMessage` only after `nackedMessage` has been received and nacked — still fails on Standard at the same rate. It is also refuted if the FIFO arms fail at a comparable rate once enough runs accumulate, since that would mean strict ordering is not what is protecting them.

## Confirmed Root Cause

Commits `febd0d7c6` and `2e650c36e` gave each of the two messages in the FR-7 and FR-16 two-message arms its own `MessageId` and body. Before that, both messages were built from one `DefaultMessageBuilder` whose id/body are field initialisers, so *both* messages were byte-identical and every identity assertion passed no matter which message arrived. Making them distinguishable exposed a latent assumption the templates had always carried: **that a queue delivers in send order and at most once.** SNS/SQS **Standard** guarantees neither; SNS/SQS **FIFO** guarantees both, which is why only the `*Standard` arms are red and only intermittently.

⛔ **MATERIAL CORRECTION TO THE TRIAGE.** The triage claimed "the specification never requires" ordering. **That is wrong. AC-7 and AC-17 are normative and both mandate the order:**

> **AC-7 (FR-7)** (`requirements.md:634-636`) — "*Given* a channel with neither DLQ nor invalid channel and two queued messages `M1` and `M2`, *when* `channel.Reject(M1, DeliveryError)` is called, *then* it returns `true` and **the next receive yields `M2`**."
> **AC-17 (FR-16)** (`requirements.md:698-701`) — "...given a second queued message `M2`, after nacking `M` **the redelivered `M` is received and then `M2` is received**."

The FR prose is looser — FR-7 (`:176-181`) requires only "returns `true` … and the channel goes on to receive the next message without blocking"; FR-16 (`:368-369`) only "the nacked message is redelivered and the following message is not blocked behind it" — but FR-7's *Example* (`:183-184`) re-imports the order, and **the ACs are normative**. So this is **not a template-only fix**: AC-7, AC-17 and FR-7's Example must be amended first, by whoever owns spec 0036.

⭐ **The deeper defect: the spec nowhere states which wired configurations offer an ordering guarantee.** That omission is what let an order-dependent AC be written for a suite that targets unordered transports.

## Evidence

**[x] Code-trace — every `file:line` re-verified in this worktree. [ ] Red repro — none possible for the bug itself (needs real multi-node SQS; the `floci` emulator serialises delivery, which is why `aws-mock-ci` is green).**

**The stack-trace line is the FIRST identity assertion, not the terminal one.** `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/SnsStandard/Generated/Proactor/When_nacking_a_message_it_should_be_redelivered.cs`: `:117-118` sends → `:121` `receivedForNack` → `:124` `NackAsync` → `:129-136` bounded 30 s/500 ms loop, `break` on first non-`MT_NONE` → **`:139` `_messageAssertion.Assert(nackedMessage, redelivered)` ← the reported line** → `:159` terminal assert. `AwsMessageAssertion.cs:49` is the **fifth** comparison, reached only after `MessageType`/`ContentType`/`CorrelationId`/`DataSchema` all matched — identical between the two messages, so the id is what first discriminates.

**Interleaving analysis (FR-16) — which interleavings produce the observed failure:**

| # | receive1 | after Nack | result |
|---|---|---|---|
| A | `nacked` | `following` | **`:139` fails, expected=nacked actual=following** ✅ matches CI |
| B | `nacked` | `nacked` | passes (the intended path) |
| C | `following` | `nacked` | `:139` passes (expected==actual==nacked); Ack deletes nacked; loop gets `following` → `:159` passes. **Self-corrects.** |
| D | `following` | `following` | **`:139` fails, expected=nacked actual=following** ✅ also matches CI |

The CI failure is A or D — both require SQS to have handed the arm the *other* message. B and C pass, which is exactly why it is intermittent.

**FR-7 mechanism.** `RejectAsync` with neither channel → `SqsMessageConsumer.cs:263-272` → `AcknowledgeAsync` → `:125-132` → `DeleteSourceMessageAsync(receiptHandle)` → `return true`. A wrong-order first receive **deletes `followingMessage`**, leaving only `rejectedMessage` → terminal assert fails with the sibling's id. The ordering assumption is upstream of the failing assertion.

**Why FIFO passes.** `FifoMessageBuilder.cs:43-46` stamps one `PartitionKey(Uuid.NewAsString())` per **instance**; each generated class holds one `_messageBuilder`, so both messages share a `MessageGroupId` → strictly ordered *and* head-of-line blocked. `test-configuration.json:20,43` — only `SnsFifo`/`SqsFifo` set `"MessageBuilder": "FifoMessageBuilder"`. ⚠️ FIFO's pass is therefore **an artefact of the harness**, not proof the assumption is legitimate.

**Batch size.** `SqsMessageConsumer.cs:190` `MaxNumberOfMessages = _batchSize`; providers never pass `bufferSize`; `Subscription.cs:200,286` defaults it to `1`. One message per receive — no client-side buffering can reorder or hide anything.

**The nack template's third send is irrelevant.** The file holds two `[Fact]`s — a one-message arm and the two-message arm (hence `DISTINCTLY_BUILT_MESSAGES = 3` in the golden test). xUnit builds a fresh instance per fact and the provider mints a new topic+queue per call, so they cannot see each other's messages.

### Rival explanations — eliminated

**(a) At-least-once duplication instead of reordering — RULED OUT for FR-16; NOT ruled out for FR-7.** For FR-16 the failure is at `:139` with `actual = followingMessage.Id`; a duplicate of `nackedMessage` carries the *nacked* id and would make `:139` **pass**. Reordering is necessarily present. For FR-7 the two are indistinguishable from the log — a surviving duplicate of `rejectedMessage` produces the same terminal failure. ⚠️ **This does not rescue the templates** (both mechanisms are "Standard gives neither order nor at-most-once") but it **constrains the remedy**.

**(b) The transport rewriting `Header.MessageId` — RULED OUT three ways.** `SqsMessageCreator.cs:367-376` reads the id from the `id` attribute verbatim, `Id.Random()` only as a fallback when absent. (1) Both reported ids decode to the same millisecond — `0x01a0af598f26` = 2026-09-17T12:31:17.286Z — i.e. *send* time, seconds before any receive, so "actual" is a sibling built in Arrange, not a fabrication at receive. (2) `ReadHandledCount` (`:323-332`) defaults to 0 and derives nothing from `ApproximateReceiveCount`. (3) **The single-message nack arm asserts the redelivered message's full identity and is GREEN in the same job** — a live proof that a nack round trip preserves the id exactly.

**(c) Cross-test pollution — RULED OUT.** `SnsStandardMessageGatewayProvider.cs:58-66`: `GetOrCreateRoutingKey`/`GetOrCreateChannelName` **accept `[CallerMemberName]` and ignore it**, returning a fresh `Uuid.New():N` every call. No two tests — and no two facts in one class — can share a topic or queue. (Belt and braces: every gateway has a `CollectionName`, so the arms are serialised.) The "same millisecond" argument stands on its own and is not load-bearing.

**(d) A different assertion position than claimed — RULED OUT.** Line 139 is the *first* identity assertion, as stated. `NackAsync` is `ChangeMessageVisibility(..., 0)` (`SqsMessageConsumer.cs:334-352`), so the nacked message is immediately visible and competes with `followingMessage` on equal terms. ⚠️ Note the triage's "first receive yields `nackedMessage`" assumption is **not on its own sufficient to fail the nack arm** — per row C it self-corrects; it only bites combined with the post-Nack receive, or in FR-7 where the reject destroys the evidence.

**Cheaply reproducible gate on any fix (generator tests, no infrastructure):** the four golden facts listed in Scope Notes go red the moment the assertion expressions change.

## Scope Notes

⛔ **The fix is wider than the triage implied. In order:**

1. **Spec first — AC-7 (`requirements.md:634-636`), AC-17 (`:698-701`) and FR-7's Example (`:183-184`) mandate the order and must be amended before any template moves.** Not mine to decide. NFR-2 (`:514-522`) says the bounded loop returns "as soon as a message arrives"; a drain-until-target loop returns as soon as the *target* arrives, so it needs a one-clause amendment too, or the rewritten arms read as violating it.

2. ⭐ **Four ledger cells are now falsified — highest-value finding.** `conformance-status.md:819,821,823,825` record FR-7 = `Pass` **and** FR-16 = `Pass` for `AWS / SnsStandard`, `AWS / SqsStandard`, `AWS.V4 / SnsStandard`, `AWS.V4 / SqsStandard`. **Those Passes were earned by the pre-`2e650c36e` blindness** and are contradicted by run 35220513183. The ledger drives `Skip` emission, so it must be reconciled in the same change or `LedgerSkipCrossCheckAudit` will disagree with reality.

3. **Latent, not yet firing — `GCP / Pull` FR-7 and FR-16 are `Pass` by the same luck** (`conformance-status.md:827`). Verified unordered: `GcpPullMessageGatewayProvider.cs:125-131` sets no `EnableMessageOrdering`, and `tests/Paramore.Brighter.Gcp.Tests/test-configuration.json:4-13` gives `Pull` no `MessageBuilder`, whereas `PullOrdering` (`:19`) and `StreamOrdering` (`:42`) set `FifoMessageBuilder` and `GcpPullOrderingMessageGatewayProvider.cs:139` sets `EnableMessageOrdering = true`. `GCP / Stream` is `Deferred` throughout so is not exposed. `RocketMQ` (`:839`) records FR-7/FR-16 `Fixed (#4240)` on a non-ordered topic — same class. MQTT (`:841`) is lowest risk (QoS-1, one topic, one client is ordered in practice).

4. **Regeneration: 96 generated `.cs` files** — 48 copies of each arm under `tests/**/Generated/` (24 wired configurations × Reactor + Proactor) — plus the 4 `.liquid` templates.

5. **Four golden tests pin the current assertion text and must move in the same commit:** `CanonicalTemplates/When_generating_nack_test_should_emit_redelivery_and_two_message_variant_both_variants.cs:262,285` and `.../When_generating_no_channels_reject_should_emit_ack_and_continue_both_variants.cs:214,237` — all `Assert.Contains("_messageAssertion.Assert(followingMessage, receivedFollowing)", content)`. The occurrence counts at `:257-258,280-281` (`DISTINCTLY_BUILT_MESSAGES = 3`) and `:209-210,232-233` (`= 2`) stay green **provided each build site keeps its own `SetMessageId(Id.Random())` / `.SetBody(`**.

6. **No `ConformanceAudit` is affected by the assertion text** — nothing under `tests/Paramore.Brighter.Test.Generator.Tests/ConformanceAudit/` references `followingMessage`, `receivedFollowing` or `Stopwatch`. Only `LedgerSkipCrossCheckAudit` becomes relevant, and only if ledger cells change (they must — item 2).

7. ⭐ **Prior art for the FR-7 remedy, hand-written and NOT regenerated:** `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/Sqs/Standard/Reactor/When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log.cs:85-93` sends **one** message and asserts the next receive is `MT_NONE` — order-free, and a stronger proof of "removed, not redelivered". (Two sibling legacy files exist: the `AWS.V4` twin and a Kafka one.)

8. ⚠️ **Do not copy the `multiple_messages` idiom wholesale.** `.../Reactor/When_a_message_consumer_reads_multiple_messages_should_receive_all_messages.cs.liquid:74` is order-free but **not duplicate-safe** (a duplicate matches and passes while a third message never arrives). Safe for FR-7's identification step, unsafe for FR-16 where redelivery is the whole point.

## ✋ Gate decision — approved 2026-09-17

**Diagnosis approved by the user.** Scope accepted as widened: spec amendment + 4 falsified ledger
cells + `GCP / Pull` latent.

## ✅ THE AMENDMENTS ARE APPROVED AND APPLIED — 2026-09-17. The block is lifted.

All five edits are in `specs/0036-universal-transport-conformance-tests/requirements.md`
(+52/-7): AC-7, AC-17, FR-7's Example, NFR-2's target clause, and the new **NFR-4 (Ordering
neutrality)**. `/bugfix:test` writes against **this** text, not the pre-amendment text.

⭐ **The user added a rule on approval that is now part of NFR-4 and is broader than the bug:**
**a scheduler-mediated delay de-orders even on a transport that guarantees ordering.** The harness
scheduler *re-publishes* the message when the delay elapses (`ConformanceHarnessMessageScheduler.cs`
header comment, 24 copies tree-wide), and a re-publish is a **new enqueue** — it lands at the tail of
the queue, partition or FIFO group, behind anything sent in the interim. So FR-2 and FR-9 arms may
never assume a delayed message keeps its position, on **any** configuration. This is a property of
the delay mechanism, not of a transport.

⚠️ **Still owed (recorded in NFR-4, not silently dropped):** the per-configuration ordering table.
Only 5 rows are verified (`AWS{,.V4} / Sns|SqsStandard` none; `GCP / Pull`+`Stream` none;
`GCP / PullOrdering`+`StreamOrdering` per key; `AWS{,.V4} / Sns|SqsFifo` per group but
**harness-induced**). Kafka, RMQ ×3, Redis, Postgres, MSSQL, ASB, RocketMQ and MQTT are **not
verified** and must not be recorded as ordered until they are.

⛔ **Superseded — kept for the record. The original blocking note said:** The user chose to **amend the ACs themselves, first**, as spec owner — so that the
obligation under test is not authored by the same agent that then tests it. The regression test is
written against the **amended** AC text, not the current text.

**Waiting on edits to `specs/0036-universal-transport-conformance-tests/requirements.md`:**

| What | Where | Why |
|---|---|---|
| **AC-7** | `:634-636` | "the next receive yields `M2`" mandates order |
| **AC-17** | `:698-701` | "the redelivered `M` is received **and then** `M2`" mandates order |
| **FR-7 Example** | `:183-184` | re-imports the order the FR prose leaves out |
| **NFR-2** | `:514-522` | "returns as soon as a message arrives" — a drain-until-target loop returns as soon as the *target* arrives |
| ⭐ **New** | — | a statement of **which wired configurations guarantee ordering**. This omission is the root defect: it is what let an order-dependent AC be written for a suite that targets unordered transports. |

⚠️ **When drafting the amended ACs, the minimal order-free assertion must still forbid the failure
mode `2e650c36e` exists to catch.** "Both messages were eventually received" is satisfied by an
implementation that **never redelivers at all** — the nacked message's first arrival plus the
following message's arrival already makes two ids. The assertion has to key on seeing an id **again
after the `Nack`**, which is a redelivery by definition. And no "exactly once" counting assertion:
Standard is at-least-once and it would trade an ordering flake for a duplicate flake.

⭐ **Draft amendments written for review: `ac-amendments-draft.md` in this folder** (5 edits — AC-7, AC-17, FR-7 Example, NFR-2, and a new NFR-4 *Ordering neutrality*).

## Regression Test

`tests/Paramore.Brighter.Test.Generator.Tests/CanonicalTemplates/When_generating_two_message_arms_should_identify_received_messages_by_id.cs`
— class `TwoMessageArmsIdentifyReceivedMessagesByIdTests`, 4 facts (nack + no-channels, each ×
Reactor/Proactor).

⭐ **Pinned at the GENERATOR level, not the transport level.** The confirmed cause is in the
templates; the 96 generated copies are its output. So the regression is caught broker-free in the
`build` job in ~20 ms instead of on a live AWS run — which is the only reason it was catchable at
all, given the defect needs real multi-node SQS to manifest.

Each fact asserts three things:

| # | Assertion | Role |
|---|---|---|
| 1 | `Header.MessageId` appears — the arm identifies receipts by id | **The red.** 0 occurrences in either template today |
| 2 | `_messageAssertion.Assert(nackedMessage, redelivered)` and `…(followingMessage, receivedFollowing)` are **absent** | The defect itself |
| 3 | `_messageAssertion.Assert(message, redelivered)` **survives** | ⚠️ **Scope guard, not a red assertion** — the single-message arm has one message in flight, so its positional assertion is correct. Catches an over-broad fix that strips identity assertions wholesale and destroys what FR-16 exists to prove. |

⭐ **RED measured: 4 failed / 271 passed / 275 total** — the only failures are the four new facts.

⛔ **Both halves were canary-proved, not argued.** All four fail on assertion 1, which means the
`DoesNotContain` halves are never reached and would otherwise be unproven. Probed by temporarily
pointing `IDENTIFIES_BY_ID` at a string that *is* present: both fired, naming `nackedMessage, r…`
and `followingMessage…` at real offsets. Canary reverted.

⚠️ **These four facts deliberately CONTRADICT four existing golden assertions** (`…nack_test…:262,285`
and `…no_channels_reject…:214,237`, all `Assert.Contains("_messageAssertion.Assert(followingMessage,
receivedFollowing)")`). That conflict is the point — it forces both to move in the same commit. **The
fix will turn those four red and they must be rewritten then.**

⚠️ **NOT pinned by this test:** AC-17's "acknowledging each message it receives" clause, which is
load-bearing for head-of-line-blocking transports (without it a FIFO group never yields the second
id). Left out rather than guess at its generated shape — **the fix must honour it anyway**.

## Fix

**The arms now resolve identity by id and assert over the set of ids observed, never over arrival
position.** Four `.liquid` templates under
`tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/{Reactor,Proactor}/`:
`When_nacking_a_message_it_should_be_redelivered` and
`When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log`.

| Arm | Shape now |
|---|---|
| **FR-16** | Receive → `_sentMessages.Single(m => m.Header.MessageId == receivedForNack.Header.MessageId)` names the nacked one, its sibling is `theOtherMessage`. Nack. **One** bounded loop accumulates `Header.MessageId.Value` into a `HashSet<string>`, **acknowledging each receipt**, exiting at `Count < 2` or the 30 s ceiling. Asserts both ids present. |
| **FR-7** | Receive → same id lookup names `rejectedMessage` and `theOtherMessage`. `Assert.True(rejected)` unchanged. Bounded loop takes the first non-`MT_NONE` receipt, asserting `NotEqual(rejectedMessage.Header.MessageId, next.Header.MessageId)` — the rejected message coming back is still forbidden — then `_messageAssertion.Assert(theOtherMessage, receivedOther)`. |

⭐ **Why FR-16 proves redelivery without naming a position:** the nacked message was *already
received once* before the `Nack`, so its id appearing **again inside the post-Nack loop** is a
redelivery by definition. An implementation that never redelivers never produces that id in that
window and times out. ⛔ **Do not "simplify" this to "both messages were received"** — that is
satisfied by a gateway that never redelivers, and is precisely the blindness `2e650c36e` removed.

⭐ **The acknowledge-as-you-go clause is load-bearing, not housekeeping.** Without it a transport
that blocks a message group while one of its messages is in flight (FIFO) can never deliver the
second id, and the arm would fail the transports that are *most* correct. This is AC-17's clause,
which the regression test does **not** pin — it is honoured here deliberately.

**Supporting changes, all required by the same cause:**
- `using System.Linq;` added to all four templates (for `Single`).
- Build-site variables renamed `nackedMessage`/`rejectedMessage`+`followingMessage` →
  **`firstSent`/`secondSent`**. ⚠️ **Not cosmetic:** `followingMessage` asserts an ordering claim in
  its own name. The names that carry meaning are now the *derived* ones, resolved by id.
- The **single-message** nack arm is untouched — one message in flight raises no ordering question,
  so `_messageAssertion.Assert(message, redelivered)` stays correct. Pinned by the regression test.
- Four golden assertions updated **in the same commit** as predicted by the Scope Notes
  (`…nack_test…` ×2, `…no_channels_reject…` ×2): they asserted the now-deleted positional
  expressions and now assert the id-based ones. Their `DISTINCTLY_BUILT_MESSAGES` counts (3 and 2)
  were unaffected — each build site kept its own `SetMessageId(Id.Random())` / `.SetBody(`.

⭐ **Measured, not predicted:**

| | |
|---|---|
| Regression test | **4/4 green** (was 4/4 red) |
| Generator suite | **275 / 275**, 0 failed |
| `./generate-test.sh` | exit 0 — **exactly 96** generated files changed, **and nothing else**: 48 × each arm across 24 wired configurations × Reactor + Proactor, precisely the Scope Notes' count |
| `dotnet build Brighter.slnx` | **0 errors** |

⚠️ **NOT yet proven, and this is the whole point of the fix:** that the two behaviours actually hold
on an **unordered** transport. No test has ever been able to tell before now. `aws-ci` on the next
run is the first real evidence either way — **if it goes red, that is a genuine transport finding,
not a defective test**, and the four `AWS{,.V4} / Sns|SqsStandard` FR-7/FR-16 ledger cells change.
The cells are deliberately left at `Pass` pending that run.
