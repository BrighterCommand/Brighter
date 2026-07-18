# Review: design — 0036-universal-transport-conformance-tests (re-review)

**Date**: 2026-07-19
**Threshold**: 60
**Verdict**: NEEDS WORK

1 finding at or above threshold 60. Address these before approving.

## Prior Findings — Status

| # | Prior finding | Prior score | Status |
|---|---------------|-------------|--------|
| 1 | FR-3 scheduler seam (0066) | 82 | Partially resolved |
| 2 | RMQ mischaracterization (0067) | 62 | Resolved |
| 3 | CI audit tracker coupling (0067) | 50 | Resolved |
| 4 | Spy naming consistency (0066) | 30 | Resolved |

Verification notes on the claimed fixes:

- **#1** — The seam correction is *substantively right*. Confirmed `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Reactor/When_kafka_consumer_requeues_with_delay_should_use_scheduler.cs` constructs `new KafkaMessageConsumer(..., scheduler: _scheduler)` and asserts on `_consumer.Requeue(received, TimeSpan.FromSeconds(5))`; `KafkaMessageConsumer.Requeue` (line 716) routes `delay > TimeSpan.Zero` to `_requeueProducer.SendWithDelay`, with `producer.Scheduler = _scheduler` set on the lazily-created producer (line 983). `docs/adr/0039-transport-scheduler-wiring.md` does thread `IAmAMessageScheduler` through the channel-factory → consumer-factory → consumer chain, as 0066 states. The FR-3 test is now writable at the channel surface from `CreateChannelWithSpyScheduler` alone, and the Proactor mirrors (`CreateChannelWithSpySchedulerAsync` → `SpyScheduledChannelAsync { IAmAChannelAsync, SpySchedulerAsync }`) are coherent. Two residues remain (findings 2 and 4 below), hence "partially".
- **#2** — Verified accurate. There is no RabbitMQ per-transport DLQ ADR (`0043` in that band is `0043-rabbitmq-mutual-tls`), and `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageConsumer.cs` confirms native DLX (`x-dead-letter-routing-key`, line 623), `RejectAsync` reduced to `BasicRejectAsync` (line 361), **zero** occurrences of any invalid-message producer, and no `RefreshMetadata`. 0067's "its fix may therefore be larger" is well-grounded. 0066's DLQ-ADR list omits RMQ; the two ADRs now agree.
- **#3** — The rescoping is coherent. The audit is now a closed in-tree triangle (Skip pattern ↔ ledger row ↔ issue link + sign-off entry), which is exactly what makes a deferral *auditable* per FR-13; live issue state was never what AC-13's "no silent skip and no unaudited deferral" required. Correctly split from the maintainer review gate.
- **#4** — `SpySchedulerSync`/`SpySchedulerAsync` and `SpyScheduledChannel`/`SpyScheduledChannelAsync` are used consistently at every occurrence including the frontmatter summary; no dangling `SchedulerSpyProducer` or `CreateProducerWithSpyScheduler` reference survives.

Grounding spot-checks that all passed: `SkipTest` branches at lines 122/127/132/145 and the three retained gates; `MessagingGatewayConfiguration` lines 91/96/106; both provider `.liquid` templates' current member sets; the broken template's `_channel.Requeue(received);` at line 62 (both Reactor and Proactor variants exist); `IAmAChannelSync` lines 64/83 plus `Nack` at 75; `IAmAMessageProducer.Scheduler` at line 46; `MessageRejectionReason.cs` enum + record; `InMemoryScheduler` primary constructor (`IAmACommandProcessor`, `TimeProvider`, two id funcs, `OnSchedulerConflict`); Kafka `HeaderNames` PascalCase vs Redis `RedisMessageConsumer.RefreshMetadata:596-604` / SQS `SqsMessageConsumer.RefreshMetadata:499-511` camelCase, including the `OriginalType` vs `originalMessageType` name (not casing) divergence; the nine gateway-declaring `test-configuration.json` files; Azure/ASB genuinely having none; PostgreSQL deriving `$"{routingKey}.DLQ"` internally under `setupDeadLetterQueue`; GCP's only generated DLQ test being `When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue.cs`; and every per-transport DLQ ADR id.

**CRITICAL coverage walk** — every canonical behaviour is writable from the extended interface as drafted: FR-2 (`CreateChannel` + `CreateProducer`), FR-3 (`CreateChannelWithSpyScheduler` / `...InMemoryScheduler`), FR-4 (`CreateSubscription(deadLetterRoutingKey:)` + `GetMessageFromDeadLetterQueue`), FR-5 (both keys + `GetMessageFromInvalidChannel` + DLQ read for the negative arm), FR-6 (DLQ-only via null invalid key), FR-7 (both null), FR-8 (`RejectionMetadataKeys` + DLQ read), FR-9 (`CreateProducer`), FR-15/FR-16 (`CreateChannel`), FR-17 (both keys, DLQ read + invalid read for the negative arm). No canonical behaviour is left unwritable.

## Findings

### 1. `CreateChannelWithInMemoryScheduler` mandates substantial, unspecified per-transport wiring that no canonical test is shown to need (Score: 65)

ADR 0066 requires **both** scheduler-backed channel members on every provider interface, and disposes of the cost in one clause: "`InMemoryScheduler` requires an `IAmACommandProcessor`, a `TimeProvider`, scheduler-id factory funcs, and a conflict policy … the provider owns that wiring so tests do not."

That clause understates the obligation. For the in-memory arm's advertised "black-box redelivery assertion (in-memory `InMemoryScheduler` actually re-publishes after the delay)" to hold, `InMemoryScheduler.Schedule(message, delay)` fires a `FireSchedulerMessage` **through the injected `IAmACommandProcessor`** (`src/Paramore.Brighter/InMemoryScheduler.cs:73-83`). Redelivery therefore only happens if the provider stands up a real `CommandProcessor` with an external bus and producer registry bound to that transport's topic, plus the `FireSchedulerMessage` handler — per transport, times ~20 provider implementations (there are 20 `*MessageGatewayProvider.cs` files, not nine). No existing test constructs `new InMemoryScheduler(...)` directly anywhere in `tests/` or `src/`; every usage goes through `InMemorySchedulerFactory` inside a full dispatcher/CommandProcessor setup. Two implementers would build this very differently, or discover it is disproportionate.

Compounding this: AC-3 is satisfied by the spy arm alone, and 0066 identifies no canonical template that requires the in-memory arm. The requirements list "whether FR-3 uses an in-memory scheduler or a spy" as a decision **deferred to the ADR** (requirements.md line 523); 0066 answers "both, each test picks the right tool" — but FR-3 is a *single* canonical behaviour, so the choice for the one FR-3 template is left open. Alternative 4 ("Spy-only or in-memory-only scheduler member") is rejected on the general NFR-4 principle rather than on any identified test that needs the in-memory arm, which reads as a strawman rejection given the cost.

**Evidence**: 0066 Technology Choices — "`InMemoryScheduler` requires an `IAmACommandProcessor` … the provider owns that wiring so tests do not"; Alternatives 4 — "Rejected: NFR-4 wants the right tool per test". Against `InMemoryScheduler.cs:81`: `var state = (processor, new FireSchedulerMessage { Id = id, Async = false, Message = message });`.

**Recommendation**: Either (a) decide FR-3 uses the **spy** channel and make `CreateChannelWithInMemoryScheduler` optional/deferred until a canonical test needs it, or (b) keep both and add an explicit paragraph specifying how the provider constructs the `IAmACommandProcessor` (external bus + producer registry + `FireSchedulerMessage` handler) so redelivery actually occurs, and record that cost in Consequences → Negative alongside the existing "every per-transport provider must be extended" bullet.

---

### 2. Stale producer-level phrasing survives in 0066's Architecture Overview role description (Score: 55)

The role description that opens the Architecture Overview still describes the removed producer-level seam, contradicting the corrected channel-level design 25 lines below and the explicit "channel-level, not producer-level" statements later in the same ADR.

**Evidence**: 0066 lines 117 and 120 — "how to build that transport's subscriptions, channels, and producers (**including wiring a producer's `Scheduler`**)" and "What the provider **does**: … **hand back a producer whose `Scheduler` is set** to either an in-memory scheduler or a recording spy." Versus line 194: "A standalone producer-with-scheduler factory would be **unobservable through `channel.Requeue`**."

The normative member listing carries no producer-with-scheduler factory, so this does not change what gets built — but it is precisely the residue of the prior finding and will mislead a reader who reads only the RDD role summary.

**Recommendation**: Rewrite both bullets in channel terms — "…and channels whose backing consumer has its `Scheduler` set" / "hand back a channel whose backing consumer's scheduler is an in-memory scheduler or a recording spy, plus the spy".

---

### 3. 0067's ledger granularity ("nine target transports") does not match the actual gateway-configuration/provider count (Score: 55)

0067 states the target set is "nine configurations" and specifies "Rows = the nine target transports". The nine figure counts *test projects*, not gateway configurations. The nine `test-configuration.json` files declare roughly twenty gateway configurations between them — AWS.Tests and AWS.V4.Tests carry four each (SQS Standard/FIFO, SNS Standard/FIFO), GCP four (Pull, PullOrdering, Stream, StreamOrdering), Kafka two (Standard, PartitionKey), RMQ.Async two (Classic, Quorum) — and there are 20 `*MessageGatewayProvider.cs` implementations to match. Generation, and therefore conformance, is per configuration, not per project. A nine-row ledger cannot express "SQS Standard passes FR-5 but SNS FIFO does not", which is exactly the state the ledger exists to hold, and it under-counts the flip gate.

**Evidence**: 0067 Context — "**AWS (SQS/SNS V3 and V4), GCP, Kafka, MSSQL, PostgreSQL, Redis, RMQ.Async, and RocketMQ** — nine configurations"; Key Components — "Rows = the nine target transports (plus Azure/ASB once added)". Against `tests/Paramore.Brighter.AWS.Tests/test-configuration.json` (four gateway blocks) and the four `Gcp*MessageGatewayProvider.cs` files.

**Recommendation**: Define a ledger row as a **gateway configuration** (project + configuration name, e.g. `AWS.V4 / SqsStandard`), and restate the count accordingly, or state explicitly that a project row is `Pass` only when all its configurations pass.

---

### 4. AC-1's literal "obtain a producer with its `Scheduler` set" is no longer satisfiable, and 0066 does not flag the requirements drift (Score: 50)

FR-1(4) asks for "a **producer whose `Scheduler` property is set**", and AC-1 requires that "separate members exist to … obtain a producer with its `Scheduler` set (in-memory or spy)". 0066's corrected design provides no such member — deliberately and correctly, per finding #1 of the prior review — but never acknowledges that it is overriding the literal FR-1(4)/AC-1 wording. AC-1 will be checked verbatim at the tasks/verification phase and will read as unmet.

**Evidence**: requirements.md AC-1 vs 0066's member listing, which exposes only `CreateChannelWithInMemoryScheduler` / `CreateChannelWithSpyScheduler`. Mitigating: requirements.md line 522 defers "the exact shape of the provider-interface extension (… producer-with-scheduler factory …)" to the ADR.

**Recommendation**: Add one sentence to 0066's Decision noting that FR-1(4)/AC-1's "producer with `Scheduler` set" is realised as a *channel whose backing consumer* carries the scheduler, and amend AC-1's wording in requirements.md to match.

---

### 5. 0066 does not reference its sibling 0067 (Score: 40)

0066's Scope says FR-13 sequencing is "addressed separately — FR-13 in a **follow-up ADR**", without naming it; 0067 is a same-day sibling, not a follow-up, and 0066's References section omits it entirely. 0067 by contrast names 0066 as "sibling; the most important relation". The asymmetry means a reader arriving at 0066 has no pointer to where the deferral governance lives.

**Evidence**: 0066 lines 100-101 and the References list (which ends at the per-transport DLQ ADRs).

**Recommendation**: Name `0067-conformance-rollout-and-deferral-governance` in 0066's Scope and add it as the first entry under Related ADRs, mirroring 0067's sibling framing.

---

### 6. Mis-declared-gate inventory is incomplete in both ADRs (Score: 35)

0066 and 0067 both single out Kafka's `HasSupportToRequeue: false`, but `tests/Paramore.Brighter.Kafka.Tests/test-configuration.json` also declares `HasSupportToDeadLetterQueue: false` and `HasSupportToDelayedMessages: false` in *both* the Standard and PartitionKey blocks (lines 12-15, 26-29) — while 0067 simultaneously calls Kafka the transport that "already conforms" and the canonical grounding for the templates. Separately, 0066's "AWS SQS declares `HasSupportToDelayedMessages: false`" is true of three of the four AWS gateway blocks; the third block declares `true` (line 40 in both AWS.Tests and AWS.V4.Tests).

**Evidence**: 0067 Context — "yet it mis-declares `HasSupportToRequeue: false` in both its Standard and PartitionKey configs"; Implementation step 2 — "Correct Kafka's mis-declared `HasSupportToRequeue: false`".

**Recommendation**: Broaden to "Kafka mis-declares all three gates `false`" and qualify the AWS claim ("three of AWS's four gateway configurations"). Harmless to the outcome — the flip removes all three keys everywhere — but the ADRs are cited as the evidence base.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 4 |
| 0-49 (Low) | 2 |

**Total findings**: 6
**Findings at or above threshold (60)**: 1
