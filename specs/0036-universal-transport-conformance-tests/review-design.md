# Review: design — 0036-universal-transport-conformance-tests (re-review 2)

**Date**: 2026-07-19
**Threshold**: 60
**Verdict**: NEEDS WORK

2 findings at or above threshold 60. Address these before approving.

## Prior Findings — Status

| # | Prior finding | Prior score | Status |
|---|---------------|-------------|--------|
| 1 | InMemoryScheduler cost understated (0066) | 65 | Resolved |
| 2 | Stale producer phrasing (0066) | 55 | Resolved |
| 3 | Ledger granularity (0067) | 55 | Resolved |
| 4 | AC-1 drift (0066 + requirements) | 50 | Resolved |
| 5 | 0066 missing sibling ref | 40 | Resolved |
| 6 | Gate inventory incomplete (both) | 35 | Partially resolved |

Verification notes:

1. **Resolved, and the documented chain is factually correct.** `InMemoryScheduler.Schedule(Message, TimeSpan)` builds `(processor, new FireSchedulerMessage { Async = false, Message = message })` (`src/Paramore.Brighter/InMemoryScheduler.cs:80-82`) and `Execute` calls `BrighterAsyncContext.Run(() => processor.SendAsync(message))` at **line 285** — cited line correct. `FireSchedulerMessageHandler.HandleAsync` calls `processor.Post(command)` for the sync arm (`src/Paramore.Brighter/Scheduler/Handlers/FireSchedulerMessageHandler.cs:21`) — correct. `OutboxProducerMediator` unwraps at **lines 458 and 483** (`if (request is FireSchedulerMessage scheduler) return scheduler.Message;`) — both cited lines correct. The four-item obligation list matches the primary-constructor parameters exactly (`processor`, `timeProvider`, two id funcs, `OnSchedulerConflict`). The claim "no existing test constructs `InMemoryScheduler` directly" is true — the only three `new InMemoryScheduler(` sites are all inside `src/Paramore.Brighter/InMemorySchedulerFactory.cs`. The claim that the spy arm has none of these prerequisites is true: `SpySchedulerSync` is a ~25-line recording class implementing `IAmAMessageSchedulerSync` (`tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Reactor/When_kafka_consumer_requeues_with_delay_should_use_scheduler.cs:153`), assigned straight onto the consumer. (The residual issue is *whether* the in-memory arm is justified at all — Finding 2 below, a different point.)
2. **Resolved.** Role bullets and the Context sentence are now in channel terms. The only surviving producer-level mentions are the deliberate contrastive ones ("A standalone producer-with-scheduler factory would be **unobservable** through `channel.Requeue`"), the quotation of the original requirement wording in the Decision, and the References gloss on `0037-universal-scheduler-delay` — which correctly describes the *runtime* mechanism (the lazily-created requeue producer's `Scheduler`), not the provider seam. No contradiction survives.
3. **Resolved, counts verified exactly.** Nine projects declare `HasSupportTo*` (AWS, AWS.V4, Gcp, Kafka, MSSQL, PostgresSQL, Redis, RMQ.Async, RocketMQ); DynamoDB/DynamoDB.V4/MongoDb/MySQL/Sqlite carry zero `HasSupportTo` keys, so the target-set claim holds. Configuration counts verified from the JSON: AWS 4 (SnsStandard/SnsFifo/SqsStandard/SqsFifo), AWS.V4 4, Gcp 4 (Pull/PullOrdering/Stream/StreamOrdering), Kafka 2 (Standard/PartitionKey), RMQ.Async 2 (Classic/Quorum), MSSQL/PostgresSQL/Redis/RocketMQ 1 each = **20**, matching exactly 20 `*MessageGatewayProvider.cs` files.
4. **Resolved.** requirements.md FR-1(4), AC-1, AC-3 and the FR-3 example now all read "channel whose backing consumer carries a scheduler", with an explicit note recording the amendment and that intent is unchanged. No place now asserts a standalone producer-with-scheduler *provider member*; the surviving "producer-with-scheduler factory" phrase is in the closing deferred-decisions list, which 0066's Decision explicitly quotes as its latitude grant — coherent, not contradictory. NFR-4 and FR-3's normative sentence still say "the producer's `Scheduler`", but that is accurate at the runtime level (the consumer sets its lazily-created requeue producer's `Scheduler`) and 0066 explains it. Intent preserved, not weakened.
5. **Resolved.** 0067 is named in 0066's Scope (line 106) and is the first Related-ADRs entry (line 421), marked "sibling". All ADR filenames cited by both documents exist in `docs/adr/`.
6. **Partially resolved** — see Finding 3. Kafka verified: both `Standard` and `PartitionKey` declare `HasSupportToDeadLetterQueue: false`, `HasSupportToDelayedMessages: false`, `HasSupportToRequeue: false`. AWS verified: `SqsStandard` declares `true`, the other three (`SnsStandard`, `SnsFifo`, `SqsFifo`) declare `false` — "three of its four" is exactly right, in both AWS and AWS.V4. But 0066's Implementation Approach and 0067's Context still carry the old imprecise summary, and requirements.md retains an outright false claim.

Other grounding spot-checks, all correct: `SkipTest` branches at lines 122–125 / 127–130 / 132–135 / 145–148; `MessagingGatewayConfiguration` properties at lines 91 / 96 / 106; `IAmAChannelSync.Reject` line 64 and `Requeue` line 83; `IAmAMessageProducer.Scheduler` line 46; Kafka `HeaderNames` PascalCase constants; the `with_delay` template's `_channel.Requeue(received);` at line 62 of the Reactor variant; the current provider interface surface; the Kafka scheduler test wiring `new KafkaMessageConsumer(..., scheduler: _scheduler)` asserting on `_consumer.Requeue(received, 5s)`; `tests/Paramore.Brighter.Azure.Tests` and `tests/Paramore.Brighter.AzureServiceBus.Tests` exist without a `test-configuration.json`.

## Findings

### 1. FR-2/FR-3 are mechanism-conditional requirements, but both ADRs treat them as unconditionally universal — and the consumer-scheduler seam exists in only 6 of the ~20 target configurations (Score: 75)

Documents: **0066** (Architecture Overview / Technology Choices / "What `CreateChannelWithInMemoryScheduler` actually requires") and **0067** (ledger columns, Implementation Approach).

0066 mandates `CreateChannelWithInMemoryScheduler` / `CreateChannelWithSpyScheduler` on every provider, on the stated grounds that "the seam that carries the scheduler is the consumer/channel factory (ADR 0039)". That seam is **opt-in and partial**. `IAmAChannelFactoryWithScheduler` is implemented by only six gateways — Kafka, MQTT, MsSql, Redis, RMQ.Async, RMQ.Sync — and only those consumers accept an `IAmAMessageScheduler`. Of the twenty target configurations 0067 enumerates, exactly **six** (Kafka ×2, MSSQL, Redis, RMQ.Async ×2) have a consumer that can carry a scheduler at all. The other **fourteen** (AWS ×4, AWS.V4 ×4, GCP ×4, PostgreSQL, RocketMQ) have consumers with no scheduler parameter, and they implement delay *natively*: `SqsMessageConsumer.RequeueAsync` issues a `ChangeMessageVisibilityRequest` with the delay seconds (line 402); `PostgresMessageConsumer.Requeue` passes the delay as an Npgsql parameter (lines 460, 347).

This is not the "non-conformance is a defect" case 0066's Risks section covers. A transport that achieves delayed requeue natively is *conformant* — requirements.md says so explicitly ("regardless of whether the transport achieves it natively or via Brighter's scheduler/producer fallback"). Yet:

- FR-3 as written is conditional ("when a channel requeues with a non-zero delay **and the transport has no native delay**"), and FR-2 is likewise conditional ("**and the transport routes the requeue through a producer**"). FR-13/AC-13 forbid gating or skipping any canonical test, and NFR-3/OOS-1 forbid a native/non-native switch. So there is no sanctioned way to *not* run FR-3 on SQS.
- Run unconditionally on a native-delay transport, the FR-3 spy assertion (`spy.ScheduleCalled == true`) fails **by design**, and the in-memory arm's black-box redelivery would come back via native visibility timeout rather than the scheduler, proving nothing.
- 0067's ledger vocabulary is exactly `Pass | Fixed | Deferred -> #issue | Unknown` (transient). There is no `N/A (native)` cell, so a maintainer facing SQS × FR-3 must either mislabel a conformant transport as `Deferred` behind a fabricated issue, or mislabel it `Pass` on a test that cannot pass. Neither is right, and two maintainers will pick differently.

Neither ADR addresses what a universal, ungated FR-3 (or FR-2) asserts for a transport whose delay is native. Additionally, making those fourteen configurations able to satisfy `CreateChannelWith*Scheduler` would require adding a scheduler constructor parameter to `SqsMessageConsumer`, `PostgresMessageConsumer`, the GCP consumers and RocketMQ's — a public runtime API change that C-1 forbids this spec; 0066 records the gap only as "a compile or implementation gap. This is *intended*", without noting that for most of the target set the remedy is outside the spec's own constraint.

**Evidence**:
- 0066: "the seam that carries the scheduler is the consumer/channel factory (ADR 0039)"; provider obligation list; Negative bullet "A transport that cannot yet supply an invalid channel, a scheduler-backed channel, or a metadata-key set surfaces as a compile or implementation gap. This is *intended*".
- `grep -rl IAmAChannelFactoryWithScheduler src/` → Kafka, MQTT, MsSql, Redis, RMQ.Async, RMQ.Sync only (plus core/DI/InMemory).
- `src/Paramore.Brighter.MessagingGateway.AWSSQS/SqsMessageConsumer.cs:73-82` — constructor has no `IAmAMessageScheduler` parameter; line 402 uses `ChangeMessageVisibilityRequest` for the delay.
- 0067 Key Components: cells hold "exactly one of" `Pass` / `Fixed (#PR/commit)` / `Deferred -> #NNNN`; "A transient `Unknown` is permitted only during the fix phase".
- requirements.md FR-3: "when a channel requeues with a non-zero delay **and the transport has no native delay**"; FR-13: "No canonical test may be silently skipped, `[Skip]`-ped, or gated away".

**Recommendation**: Decide explicitly, in 0066 (test semantics) and 0067 (ledger vocabulary), what FR-2/FR-3 mean for a transport that delays natively. Options that do not smuggle back a `HasNative*` flag: (a) restate FR-3's generated assertion as observable-outcome-only ("the message is redelivered after the delay"), with the spy/in-memory arms used only where the consumer seam exists, and record the mechanism nowhere; or (b) add an explicit `N/A (native delay — <one-line justification>)` ledger cell type, defined as a non-blocking cell that is *not* a deferral, and state which configurations are expected to carry it. Either way, record in 0066 that the consumer-scheduler seam covers only 6 of the 20 target configurations today, and that extending it to the other 14 is a runtime API change bounded by C-1 (i.e. an ADR-0067 `Deferred` class, not in-spec `Fixed`).

---

### 2. Alternative 4's rejection is now under-argued: no canonical requirement needs the in-memory arm, yet it is a mandatory member on all 20 providers (Score: 65)

Document: **0066** (Technology Choices, "What `CreateChannelWithInMemoryScheduler` actually requires", Alternatives Considered #4).

This round's amendment establishes that `CreateChannelWithInMemoryScheduler` is "the single most expensive part of implementing the extended interface" — a real `CommandProcessor`, a `FireSchedulerMessage` handler pipeline, and an external bus with a producer registry bound to the transport's topic, per provider, ×20, with no existing precedent anywhere in the tree. Having established that, the ADR's justification for keeping it has not been strengthened correspondingly. Alternative 4 still reads as a one-line appeal to "NFR-4 wants the right tool per test", and the Technology Choices bullet asserts "**Some** canonical tests want a black-box redelivery assertion" — but **no canonical test in FR-2…FR-9, FR-15, FR-16, FR-17 is identified as requiring it**, and none appears to. AC-3 explicitly permits *either* arm for FR-3; FR-2 drives the real producer path; FR-9 drives `SendWithDelay`; FR-15 asserts the *absence* of delay. 0066 then concedes the point itself: "FR-3 remains provable via the spy arm alone, so a provider may defer the in-memory arm as an ADR-0067 `Deferred` row without losing FR-3 coverage."

The net position is that the most expensive obligation on the interface is mandated across twenty providers to serve a test that does not exist in the requirement set, and is simultaneously declared optional in practice. That is scope beyond requirements and an ambiguity two implementers would resolve differently — one building the full `CommandProcessor` harness per provider, one stubbing the member to `throw new NotImplementedException()` and opening a `Deferred` row on day one. Alternative 4 as written does not engage with the cost the ADR now documents, so it reads as a strawman relative to its own evidence.

**Evidence**: 0066 Technology Choices — "Some canonical tests want a black-box redelivery assertion … others want a delegation assertion … so the provider supplies both"; Negative — "That is materially more work than any other member on the interface, it is repeated across every provider implementation (there are ~20 `*MessageGatewayProvider.cs` files) … We accept this cost"; Alternatives #4 — "Supplying only one forces the other kind of test into an awkward or weaker shape." requirements.md AC-3: "*then* the redelivery is proven to go via the scheduler — **either** the message is redelivered after the delay (in-memory scheduler) **or** the spy records a schedule call carrying `5s`".

**Recommendation**: Either (a) name the specific canonical test(s) that require the in-memory arm and cannot be written with the spy — and if there are none, adopt spy-only and record it as the decision, with the in-memory factory listed as future work under OOS-2; or (b) keep both but demote `CreateChannelWithInMemoryScheduler` from a mandatory interface member to an explicitly optional one (separate interface, or a documented `throw new NotSupportedException()` contract), so "a provider may defer it" is expressible without a compile break and without a fabricated ledger deferral. Rewrite Alternative 4 to weigh spy-only against the now-documented per-provider cost rather than against a hypothetical test.

---

### 3. Residual gate-inventory inaccuracies: 0066's Implementation Approach contradicts its own corrected Context, and requirements.md retains a verifiably false claim (Score: 50)

Documents: **0066** (Implementation Approach), **0067** (Context), **requirements.md** (Problem Statement, Additional Context).

The Context sections were corrected this round, but the summaries elsewhere were not brought along, so the document set now contradicts itself:

- 0066 Context (corrected): "AWS declares `HasSupportToDelayedMessages: false` in **three of its four** gateway configurations (the fourth declares `true`)" — verified correct.
- 0066 Implementation Approach (uncorrected, ~line 328): "including the mis-declared PostgreSQL (`false`/`false`), **AWS SQS (`false`)**, and Kafka (`HasSupportToRequeue: false`) values" — `SqsStandard` declares `true` in both AWS and AWS.V4; only `SqsFifo` is `false`. Same imprecision in 0067's Context ("AWS `HasSupportToDelayedMessages: false`").
- requirements.md Problem Statement: "**RocketMQ is the only configuration declaring `HasSupportToDelayedMessages: true`**, so the delayed-delivery test currently runs for exactly one transport" — **false**. `tests/Paramore.Brighter.AWS.Tests/test-configuration.json` and `tests/Paramore.Brighter.AWS.V4.Tests/test-configuration.json` both declare `SqsStandard.HasSupportToDelayedMessages: true`. Repeated in Additional Context ("RocketMQ the only `HasSupportToDelayedMessages:true`") and implied by "AWS SQS declares `HasSupportToDelayedMessages: false`". The delayed-delivery template therefore currently generates for three configurations, not one. 0066's amended Context now directly contradicts the parent requirement it cites.

This does not change what gets built (all three keys are removed regardless, per FR-10/AC-11), so it is cosmetic — but it is a factual error in the requirement that motivates the work, in a document amended this round.

**Evidence**: Verified JSON — AWS/`SqsStandard`: `{'HasSupportToDeadLetterQueue': True, 'HasSupportToDelayedMessages': True, 'HasSupportToRequeue': True}`; AWS.V4/`SqsStandard`: identical; RocketMQ: `'HasSupportToDelayedMessages': True`.

**Recommendation**: Correct requirements.md's Problem Statement bullet 2 and the Additional Context line to "AWS `SqsStandard` and RocketMQ are the only configurations declaring `HasSupportToDelayedMessages: true`; the other three AWS configurations declare `false` despite native `DelaySeconds`". Align 0066's Implementation Approach and 0067's Context with 0066's corrected Context wording.

---

### 4. `GetMessageFromDeadLetterQueue` / `GetMessageFromInvalidChannel` have no specified empty/absence semantics, but AC-5 and AC-18 require negative assertions (Score: 50)

Document: **0066** (Architecture Overview, Implementation Approach).

0066 adds `Message GetMessageFromInvalidChannel({Subscription} subscription)` mirroring the existing `GetMessageFromDeadLetterQueue`, both returning a non-nullable `Message`. Two canonical tests need to assert **absence**: AC-5 — the unacceptable message "does **not** appear on any DLQ"; AC-18 — with `RejectionReason.None`, "`M` does **not** appear on the invalid channel". The existing interface's XML doc is silent on what happens when the queue is empty ("The message from the dead letter queue"), and 0066 inherits that silence rather than closing it. Implementers will diverge: one returns a `MessageType.MT_NONE` message after a bounded wait, another blocks until timeout and throws, a third returns `default`. A throwing implementation makes AC-5/AC-18 unwritable as a uniform template; a blocking one makes them slow and flaky.

**Evidence**: `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Reactor/IAmAMessageGatewayReactorProvider.cs.liquid:69-75` — `/// <returns>The message from the dead letter queue.</returns>`, no empty-case contract. 0066 Architecture Overview lists both read members with no return-semantics note. requirements.md AC-5, AC-18.

**Recommendation**: State the contract in 0066's Architecture Overview: both read members poll with a bounded retry (NFR-2) and return a message with `Header.MessageType == MessageType.MT_NONE` when nothing arrives within the bound — never throw, never block indefinitely. That makes "did not appear on the DLQ/invalid channel" a uniform, transport-agnostic assertion.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 1 |
| 50-69 (Medium) | 3 |
| 0-49 (Low) | 0 |

**Total findings**: 4
**Findings at or above threshold (60)**: 2
