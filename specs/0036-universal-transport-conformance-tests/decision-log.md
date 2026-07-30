# Decision log — spec 0036, universal transport conformance tests

Why identifiers were retired and why decisions were reversed. **This file is the record of
deliberation.** `requirements.md` states what must be true; the ADRs state how and why the design
works; neither carries withdrawal narration. Readers wanting only the specification can ignore this
file entirely.

Retired identifiers are never reused, so a gap in the numbering is deliberate. Each is explained
below.

---

## Corrected: RocketMQ does not fail FR-2's before-`D` arm — design round 7

**Corrected 2026-07-20, after design review round 7 (single finding, score 64).** No identifier
changed; the two-sided-FR-2 edits made while remediating requirements rounds 9–10 attributed the
before-`D` (immediate-`MT_NONE`) lower-bound arm to the wrong witness for RocketMQ.

The edits credited the before-`D` arm as "what makes GCP ×4 **and RocketMQ** fail as generated."
Verified against source, that holds for GCP but not RocketMQ:

- **GCP ×4 redelivers immediately** — `GcpPullMessageConsumer.Requeue` calls
  `ModifyAckDeadline(..., 0)`; `GcpPubSubStreamMessageConsumer.Requeue` calls
  `gcpStreamMessage.Reject()`. An immediate receive gets the message straight back, so GCP fails the
  before-`D` arm. Correct as written.
- **RocketMQ's `Requeue` is a no-op** (`RocketMessageConsumer.cs:179-189`) that neither acks nor
  changes visibility; `ChangeInvisibleDuration` is commented out pending an upstream client fix. The
  message was received under `invisibilityTimeout`, whose **default is 30 s**
  (`RocketMqSubscription.cs:105`), and `Nack`'s own comment (`:103`) confirms redelivery is
  timeout-governed. So after the no-op requeue the message stays *invisible*; an immediate receive
  yields `MT_NONE` and RocketMQ **passes** the before-`D` arm. Its non-conformance (it ignores the
  delay) shows, if the generated test catches it at all, on the **after-`D`** arm — the 30 s timeout
  lands at FR-2's 30 s retry ceiling — and a timeout inside the retry window could even let RocketMQ
  pass FR-2 by accident, which is a further reason its mechanism belongs in OOS-2.

**Why it mattered.** The before-`D` arm was the hard-won FR-2 lower bound from requirements rounds
9–10; getting its witness wrong undercut the very rationale for the arm. The arm itself stands — it
is fully justified by GCP ×4 alone. Only RocketMQ's attribution was wrong. Corrected in both ADRs
(0066, 0067) and, because the same imprecision was inherited into the approved requirements (AC-2
even self-contradicted, scoping the arm to "ignores the delay **and redelivers at once**" while
naming RocketMQ), in requirements FR-2/AC-2/FR-21 as well — a factual correction that changes no
design decision, scope, or acceptance outcome (RocketMQ lands as a signed-off `Deferred` row either
way). The `.requirements-approved` marker stands.

---

## Corrected: "never ungated" is not "never generated" — requirements round 8

**Corrected 2026-07-19, after requirements review round 8 (findings 1 and 3).** No identifier
changed; the reversal below was described in language that overstated it.

The gating reversal was written as though suppressing the four legacy templates meant they stopped
generating. It does not. A gate suppresses a template only where that gate is declared `false`, and
most configurations declare these gates `true`. Verified against all fourteen `test-configuration.json`
files and the checked-in generated tree: **all four** legacy templates generate **today** — the
exhaustion template for 16 configurations (32 copies), plain requeue for 18 (36 copies), `with_delay`
and delayed-message for 3 each (6 copies each) — and keep generating on every regeneration run until
they are deleted.

(Requirements round 8 stated this as "three of the four" while tabulating four non-zero counts; design
round 6 caught the arithmetic. All four are non-zero, and 6 + 36 + 6 + 32 = 80 is the sweep the
cleanup depends on.)

What the lifecycle actually guarantees is that a legacy template gains **no new** generation site.
"Stays suppressed" and "never generated" were false as written, and AC-22 contradicted itself by
asserting "never generated" one clause after counting the thirty-two copies that disprove it.

**Why it mattered beyond wording.** FR-1(6) removes `setupDeadLetterQueue` from `CreateSubscription`
while the exhaustion template is still live for sixteen configurations. The template passes the flag
**positionally**, as a bare `true` fourth argument, so none of its 32 generated copies contains the
string `setupDeadLetterQueue`. A maintainer migrating "every generated caller" by searching for the
parameter name finds the 40 interface copies and 20 provider implementations and misses all 32 broken
call sites — a hard compile break invisible to the obvious search. FR-1(6) now carries an explicit
interim obligation to edit the template in the same change, and AC-1 records that positional call
sites are not name-searchable.

---

## Decided: FR-15 narrowed to `TimeSpan.Zero`; FR-22 owns the no-delay call

**Decided 2026-07-19, by spec-owner ruling, after requirements review round 8 (finding 2).**

FR-22 claimed to be "distinct from FR-15, which pins the explicit `TimeSpan.Zero` / `null` arguments
rather than the no-argument call". That distinction does not exist: the signature is
`bool Requeue(Message message, TimeSpan? timeOut = null)`, so `Requeue(m)` and `Requeue(m, null)`
compile to the identical call. FR-22 and FR-15's null arm were one behaviour specified twice, with
two ledger columns.

**Ruling**: FR-22 owns the no-delay call in both spellings (omitted and explicitly null). FR-15
narrows to the explicit `TimeSpan.Zero` argument only, asserting that zero is not special-cased into
an error or an unbounded wait. Both remain canonical behaviours with their own ledger columns and
their own templates — *"plain requeue redelivers"* and *"zero is not special-cased"* are genuinely
different assertions.

**Rejected alternative**: merging both into one template with three assertions and a single ledger
column. Fewer artifacts, but it loses the separation between the two assertions and would have
reopened the FR-21/AC-24 column counts.

---

## Reversed: gate retirement moved from first step to last — FR-10 rewritten

**Reversed 2026-07-19, by spec-owner instruction, after design review round 5.** Not a retirement;
FR-10 keeps its identifier and changes its content. FR-22/AC-25 added for the canonical plain-requeue
template the old FR-10 supplied by ungating.

FR-10 originally read "`SkipTest` MUST no longer skip any template on the three gates … the existing
plain-requeue template becomes ungated and generates for every transport alongside the canonical
templates", and FR-9 said the existing delayed-message template "satisfies FR-9 once FR-10 ungates
it; no new canonical template is required".

**How it arose.** The spec's founding insight is that the gates mis-model universal obligations, so
removing them read as the obvious first move. Nobody asked what the gates were *doing* in the
meantime.

**Why it was wrong.** Two independent failures, one found by review, one by the spec owner.

Design review round 5 found a **circular dependency**: 0067's flip gate required every ledger cell
resolved *before* the gates were retired, but while the gates are live `SkipTest` suppresses any
template whose filename contains `requeuing`, `with_delay`, `delayed_message` or `dead_letter_queue`
— and Kafka, the reference transport, declares all three gates `false`. The rollout could not
execute step 1: Kafka's canonical FR-2 and FR-9 tests would not generate until the very change the
ledger was supposed to authorise.

The spec owner's ruling went further and dissolved the problem at its root: **the old tests are never
wanted.** Not before the canonical set exists, and not after. A gate suppressing a legacy template is
doing useful work until that template is deleted, so retiring the gates first generates precisely the
tests this spec exists to replace — the broken `with_delay` template and the pump-owned exhaustion
template among them — against transports not yet fixed. Ungating an old test is never a step toward
the goal.

**What replaced it.** Canonical templates are ungated *by construction*: `SkipTest` consults the
three gates only for a closed, explicit list of the four legacy template filenames, so a canonical
template cannot be suppressed however it is named. This deliberately does not rely on naming —
`SkipTest` matches substrings, and NFR-1's convention means a canonical delayed-requeue template
naturally contains both `requeuing` and `with_delay`, the same trap that left the exhaustion template
doubly gated. The four legacy templates are never ungated for their whole remaining life, are deleted
with their eighty generated copies once the canonical set is complete, and only then do the gates and
config keys go.

**Consequences accepted.** FR-9 and FR-22 now require canonical templates that MAY be migrations of
the two salvageable legacy ones, rather than reusing them in place. The deletion sweep doubles from
38 generated copies to 80. And the deferral marker becomes the *normal* transitional state of a
canonical test rather than an exceptional escape hatch — canonical tests generate everywhere from the
day they land, so the five configurations known to fail FR-2 (GCP ×4, RocketMQ) carry linked,
signed-off, ledger-backed markers until fixed. ADR 0067 records why that is not the "normalized red"
it otherwise resembles: a suppressed test is one being retired, a deferred test is one being adopted.

---

## Retired: OOS-6 — the exclusion of unwired gateway transports

**Retired 2026-07-19, by spec-owner instruction.** In scope: all twelve gateway transports. FR-20
onboards the three that lack generator wiring.

OOS-6 excluded AzureServiceBus, MQTT and RMQ.Sync from the spec because they declare no
`test-configuration.json` and are therefore not generated for today.

**How it arose.** Review round 4 found that FR-13's phrase "every gateway configuration the
generator targets" was undefined. The definition written to close that finding was *descriptive* —
it named the nine projects the generator wires today — and that description then hardened into a
normative scope boundary without anyone deciding it on the merits. Round 5 added RMQ.Sync and MQTT
to the exclusion; round 6 observed that Azure Service Bus belonged in the same class and it was
added too. Three review rounds refined an exclusion none of them questioned, because each round was
told the target set was settled.

**Why it was wrong.** It repeats the error the spec exists to correct. The capability flags
described what was *being tested* and were treated as what a transport *owed*; a missing
`test-configuration.json` describes what the generator *covers* and was likewise treated as what
needs covering. Absence of wiring is a gap to close, not a scope boundary.

The three are not bare gateways: counted as test files under
`tests/Paramore.Brighter.*.Tests/MessagingGateway/`, RMQ.Sync carries 31, MQTT 19,
AzureServiceBus 15. MQTT has its own dead-letter ADR (`0043-mqtt-dlq-brighter-managed`), and both
MQTT and RMQ.Sync implement `IAmAChannelFactoryWithScheduler`.

**Consequence accepted.** Onboarding needs a config, provider implementations and CI infrastructure
per transport; ASB is a cloud service with no container story in this repo. FR-13's deferral rule
governs any that cannot complete in-spec — a named, linked, signed-off ledger row, never silent
absence. ADR 0067 records the expectation that one or more lands as `Deferred` at flip time.

---

## Retired: FR-18, AC-19 — requeue-count exhaustion as a canonical behaviour

**Retired 2026-07-19, after review round 5.** Replaced by FR-19: delete the template.

FR-18 asserted that requeuing a message `Subscription.RequeueCount` times routes it to the DLQ, as a
universal channel obligation.

**How it arose.** Review round 4 found that
`When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue.cs.liquid` would be
silently ungated by FR-10, because `SkipTest` matches filename substrings and that name matches both
`dead_letter_queue` and `requeuing`. Adopting it as canonical was chosen over leaving it
unaccounted.

**Why it was wrong.** The supporting claim — "`RequeueCount` is a property of `Subscription` for
every transport, so this is a universal obligation" — is a non-sequitur, and was asserted without
being checked. `RequeueCount` existing says nothing about whether the *channel* enforces it, and it
does not:

- `Message.HandledCountReached` has exactly two callers, `Reactor.cs:498` and `Proactor.cs:504` —
  both in the message pump, which OOS-5 excludes.
- `Channel.Requeue` / `ChannelAsync.RequeueAsync` forward straight to `_messageConsumer.Requeue` and
  count nothing.
- Where the template passes today it proves the *transport's* native redrive: the AWS provider pairs
  `requeueCount: 3` with `redrivePolicy: new RedrivePolicy(dlqName, 3)`, so SQS does the counting.
  NFR-3 and OOS-1 exclude native-mechanism assertions.

It is the same defect that withdrew FR-3. Deleting the template loses no channel-owned coverage: the
plain-requeue half is the FR-10-ungated plain-requeue template, and the DLQ-arrival half is FR-4.

---

## Retired: FR-3, FR-1(4), NFR-4, AC-3 — scheduler-delegation testing

**Retired 2026-07-18, by ADR 0066.** Folded into the mechanism-agnostic FR-2.

FR-3 asserted that a delayed requeue is delegated to the producer's scheduler when the transport has
no native delay; FR-1(4) required a provider member exposing a scheduler-carrying producer; NFR-4
constrained that test to an in-memory scheduler or spy; AC-3 was FR-3's criterion.

**Why withdrawn.** Two reasons. It is a *mechanism* assertion, which NFR-3 and OOS-1 forbid —
asserting a requeue went via the scheduler rather than via native delay tests *how* a transport
achieves the behaviour. And the seam does not exist for most transports:
`IAmAChannelFactoryWithScheduler` is implemented by six gateways (Kafka, MQTT, MsSql, Redis,
RMQ.Async, RMQ.Sync); the other consumers take no scheduler at all, and giving them one would be a
public runtime API change that C-1 forbids.

The observable behaviour FR-3 cared about — a delayed requeue redelivers after the delay — is
covered by FR-2 uniformly. Scheduler-delegation testing for the six gateways that have the seam is
supplementary work under OOS-2.

---

## Narrowed: FR-12 / AC-12 — the no-delay-requeue prohibition

**Narrowed 2026-07-19, after review round 4.** Not a retirement; the identifiers remain live.

FR-12 originally read "after deletion, no messaging-gateway template may call `Requeue` or
`RequeueAsync` without a non-null `TimeSpan`". That was self-contradictory: FR-10 preserves the
plain-requeue template, which calls `Requeue` with no argument, and FR-15 *requires* a test calling
`Requeue(M, null)`. AC-12 was unsatisfiable as written. The prohibition is now scoped to templates
that exercise *delayed* requeue, with those two exempt.

---

## Deferred: Phase 2 Kafka reference — both rows Deferred -> #4240 (infra block)

**Recorded 2026-07-22, Phase 2 (ralph task 28).** The Kafka reference task ran the generated
canonical suite for `Kafka / Standard` and `Kafka / PartitionKey` (both variants) against a local
single-broker Kafka via `docker compose`. It did **not** produce a reliable full-green gate, so
both rows are `Deferred -> #4240 (sign-off: @maintainer)` across all 11 columns — flag-and-move-on
per ADR 0067 "CI-infrastructure inability is a first-class deferral ground" and spec ruling 0a
(infra block → Deferred, **not** `[!]` FAILED).

**What was established (keep for the follow-up).** The generate→ledger-driven-Skip mechanism works
end-to-end: flipping cells and regenerating un-skips/re-skips the canonical tests by construction,
and the suite executes and yields correct per-behaviour signal. Two distinct classes of
non-conformance were observed:

- **Deterministic failures — but test-harness gaps, not transport limitations.** On a *fresh*
  broker, FR-2, FR-4, FR-5, FR-6, FR-8, FR-9, FR-17 failed every time. Reading the *actual* errors
  (not inferring from test names) shows the cause is the harness, not Kafka:
  - **FR-4/5/6/8/17 (reject → DLQ / invalid-channel)** fail at the DLQ-read assertion because
    `KafkaMessageGatewayProvider.GetMessageFromDeadLetterQueue()` /
    `GetMessageFromInvalidChannel()` (and the async variants, in *both* the Standard and PartitionKey
    providers) are **stubs that `return Message.Empty`** — i.e. `MT_NONE`. The harness never consumes
    the `<topic>.DLQ` topic, so `Assert.NotEqual(MT_NONE, dlqMessage…)` (line ~74) can never pass.
    This says **nothing** about whether Kafka's reject path routes to a DLQ (native or universal
    fallback) — the behaviour is simply never observed. Fixable in the harness.
  - **FR-2/FR-9 (delayed requeue / delayed send)** fail with
    `ConfigurationException: KafkaMessageProducer: delay … requested but no scheduler is configured`.
    The harness `CreateProducer` wires **no `MessageSchedulerFactory`**, though Kafka implements the
    scheduler seam (`IAmAChannelFactoryWithScheduler`; cf. hand-written
    `When_kafka_channel_factory_forwards_scheduler_to_consumers`). Fixable in the harness/config.

  An earlier draft of this note wrongly attributed these to "no native DLQ / delayed-redelivery
  primitive." That was inference from the test names before the errors were read; it is retracted.
- **Broker flakiness under load** — FR-7, FR-15, FR-16, FR-22 *passed* on the fresh broker, but on
  a re-run under full-suite load they failed alongside the most basic pre-existing hand-written
  gateway tests (`When_posting_a_message_via_the_messaging_gateway_should_be_received`,
  `…reads_multiple_messages…`, `KafkaMessageConsumerUpdateOffset` — the last passed in isolation yet
  failed in-suite). Non-deterministic results against a single dev broker → cannot certify `Pass`
  (FR-14 requires *reliable* green in both variants), hence Deferred rather than Pass.

**A speculative gateway change was reverted.** An in-progress sub-agent had changed
`KafkaMessageCreator.cs:252` from `DateTimeStyles.AssumeUniversal` to
`AssumeUniversal | AdjustToUniversal`. It is a real latent bug (the parsed UTC timestamp is
converted to local time), but it was **not** proven to bring any canonical behaviour to conformance
and it *regressed* the hand-written `KafkaMessageConsumerUpdateOffset` test (green in isolation only
after the revert). Out of scope for this task; reverted. If pursued, it belongs in its own bugfix,
not the conformance ledger.

**Follow-up (#4240).** (1) Implement the Kafka test-harness hooks that are currently stubbed —
`GetMessageFromDeadLetterQueue`/`GetMessageFromInvalidChannel` (+ async, both providers) must
actually consume the `<topic>.DLQ` / invalid-channel topic; and wire a `MessageSchedulerFactory`
into the delayed-producer path. (2) Re-run Phase 2 against a stable CI Kafka to certify
FR-7/15/16/22 as `Pass` and to see how many of FR-2/4/5/6/8/9/17 flip to `Pass`/`Fixed` once the
harness observes the behaviour. Only what still fails *after* the harness is complete is a genuine
transport gap to triage `Fixed` (localized gateway work) vs signed-off `Deferred`. Phase 6
reconciles `#4240` → the real issue. NB: the `manual-test-plan.md` runbook drives exactly this.

---

## Strengthened: deferral governance + split-task execution (after the Phase 2 Kafka run)

**Recorded 2026-07-22.** The Phase 2 Kafka run exposed a governance hole: the flag-and-move-on
clause had **no preconditions**, so three unrelated outcomes all drained into `Deferred` — a genuine
external block (legitimate), missing in-scope implementation (the stubbed harness hooks), and a
sub-agent that timed out / blew its context (not a result at all). The task was closed `[x]` on the
strength of "tests fail + a belief it was infrastructure", which laundered "we didn't finish" into
"infrastructure blocked us". The mitigation we *had* added (context-discipline instructions to the
sub-agent) did not make the flow more reliable — a second sub-agent died the same way — because the
broker-heavy suite is a structural mismatch for one-sub-agent-per-task (16 min/TFM, output
overflows context).

**Two changes (owner-approved):**
1. **Deferral preconditions** — `Deferred` is now earned, not defaulted-to: (a) evidence, not
   inference (root cause read from the actual error, recorded); (b) implementation attempted, where
   in-scope explicitly includes completing the test-harness hooks a behaviour needs to be
   *observable*; (c) residual blocker genuinely external or beyond the size/risk boundary. A
   timed-out/context-blown run is **re-run**, never deferred; missing in-scope implementation is work
   to do, not a dependency. Codified in ADR 0067 "Deferral preconditions" (post-acceptance amendment)
   and `ralph-tasks.md` Execution Notes.
2. **Split broker-heavy conformance tasks into behaviour-class sub-runs** — orchestrator-scoped,
   single-TFM, output redirected to a log with only summaries read. Standard classes:
   requeue/redeliver (FR-22/15/16), reject→DLQ/invalid+metadata (FR-4/5/6/8/17, needs the read
   hooks), delay (FR-2/9, needs a scheduler), no-channel ack (FR-7). Each unit does its harness
   prerequisite first, keeping it under the tool/context ceiling and making precondition (b)
   impossible to skip.

**Consequence:** the Phase 2 Kafka task was **reopened** (its `[x]` was invalid under the new rule)
and re-expressed as four behaviour-class sub-tasks. The commit `97c9be7b9` and the all-`Deferred`
ledger remain the honest starting point; the sub-tasks flip columns to `Pass`/`Fixed` as each is
genuinely proven. ADR 0067's rollout *decision* is unchanged — only its deferral *governance* is
tightened.

## Relaxed: FR-8 metadata not required for native-dead-letter transports (RMQ.Async)

**Recorded 2026-07-29 (maintainer-approved).** The RMQ.Async / Classic conformance run surfaced a
contract question the earlier transports never did: the canonical reject behaviours (FR-4 delivery-error
→ DLQ, FR-6 unacceptable-no-invalid → DLQ, FR-17 None → DLQ, and the dedicated FR-8 metadata test) each
assert Brighter **rejection metadata** (`OriginalTopic`, `OriginalType`, `RejectionReason`,
`RejectionMessage`, `RejectionTimestamp`) on the dead-lettered message. RabbitMQ dead-letters **natively**
via a DLX: `RmqMessageConsumer.RejectAsync` calls `Channel.BasicRejectAsync(deliveryTag, requeue: false)`
and, because the queue is declared with `x-dead-letter-exchange`/`x-dead-letter-routing-key`, the broker
routes the message to the DLQ. This **routing works** (verified against a live 4.2 broker, both variants:
the DLQ-arrival assertion passes). But `basic.reject` carries only a delivery tag + requeue flag — it
moves the **untouched original** message, so the gateway cannot add Brighter metadata without abandoning
native DLX for a Brighter-managed re-publish (the `SqsMessageConsumer` pattern: stamp the bag, publish the
enriched copy to a DLQ/invalid routing key, then ack).

**Decision (owner choice among: fix RMQ to Brighter-managed routing / relax FR-8 / keep deferred):**
**relax FR-8** — a transport that dead-letters via a native broker mechanism is conformant on **routing
alone**; requiring Brighter metadata would force every native-DLQ transport into a re-publish path it does
not otherwise need. Mechanism (universal, provider-driven, no per-transport template): the generated
`RejectionMetadataKeys` record gains a computed `StampsRejectionMetadata` (true iff the provider declares
non-empty keys); the canonical templates for FR-4/6/8/17 assert **DLQ arrival unconditionally** and guard
the **metadata sub-assertions** behind `if (keys.StampsRejectionMetadata)`. Transports whose gateway
stamps metadata (SQS/Redis/Postgres/MSSQL — non-empty keys) are **unchanged** (the guard is always true
for them); a native-DLQ transport (RMQ — empty keys) proves routing and skips the metadata assertions.
This is a change to the universal conformance **contract** (FR-8 / AC-8), applied by regenerating every
config and rebuilding `Brighter.slnx`.

**Not relaxed: FR-5** (reject-as-unacceptable must land in a **separate invalid channel**, distinct from
the DLQ). That is a *routing* requirement, not a metadata one — RMQ models no invalid destination
(`RmqMessageConsumer`/`RmqSubscription` carry only a single dead-letter destination), so an unacceptable
rejection dead-letters to the DLQ. FR-5 therefore stays `Deferred -> #4240` for RMQ; conforming needs
Brighter-managed invalid routing in `src`. **Consequence:** RMQ.Async / Classic goes from 6 `Pass` + 5
`Deferred` to **10 `Pass` + 1 `Deferred` (FR-5)**; the same relaxation will apply to RMQ.Async / Quorum
and any future native-DLQ transport.

**⚠️ Guardrail — empty keys must be a *deliberate* native-dead-letter declaration, not an unfilled stub.**
`StampsRejectionMetadata == false` silently skips the metadata sub-assertions, so a provider that returns
empty keys merely because nobody populated them yet would get a free `Pass` on FR-4/6/8/17 (routing only),
masking a genuine metadata gap. This is safe for every *proven* transport today — AWS ×8, PostgreSQL,
MSSQL, Redis, RocketMQ, Kafka all declare real keys, so their metadata is still fully asserted. But
**GCP ×4 currently return empty keys and are still `Unknown`.** When the GCP conformance task runs (Phase 4)
it must consciously determine whether GCP genuinely dead-letters natively (Pub/Sub dead-letter topics — in
which case empty keys + routing-only is correct and conformant, exactly like RMQ) **or** whether the empty
keys are a stub for a gateway that should stamp Brighter metadata (in which case the provider must populate
the keys so the assertions run). The relaxation is a capability *declaration*, not a licence to skip
verification — the per-transport task owns that call, per ADR 0067's evidence-not-inference rule.

---

## Resolved: GCP / Pull — Phase 4, emulator run (2026-07-30)

`GCP / Pull` resolved against a local **Pub/Sub emulator** (`gcr.io/google.com/cloudsdktool/cloud-sdk:emulators`,
`docker-compose-gcp.yaml`; `PUBSUB_EMULATOR_HOST=localhost:8085`, `GOOGLE_CLOUD_PROJECT=brighter-test`).
Both variants: **28 pass / 12 skip / 0 fail** (net10.0). No real-GCP credentials were available, so the
emulator is the only local infra; CI runs GCP against real Pub/Sub (`.github/workflows/ci.yml` `gcp-ci`,
which also excludes `GcpPubSubStream`/`GcpPubSubStreamOrdering`). **Row: FR-7/15/16/22 `Pass`; FR-9
`Fixed (#4240)`; FR-2/4/5/6/8/17 `Deferred -> #4240 (sign-off: @maintainer)`; 0 `Unknown`.**

**Harness gap fixed first (emulator detection).** `GcpMessagingGatewayConnection` exposes *five* separate
client-builder config actions (`TopicManagerConfiguration`, `PublisherConfiguration`,
`SubscriptionManagerConfiguration`, `StreamConfiguration`, `ProjectsClientConfiguration`); the provider only
wired the publish/subscription-manager pair, so the **admin topic client** and **streaming subscriber** hit
real GCP (`Unauthenticated`) even with `PUBSUB_EMULATOR_HOST` set. Wired `EmulatorDetection.EmulatorOrProduction`
on all four Pub/Sub builders (harmless for CI — no env var there → production). *Takeaway for the other three
GCP configs: wire every Pub/Sub builder, not just two.*

**FR-9 `Fixed (#4240)` — wired scheduler + a localized `src` timeout fix.** GCP has no native delayed
publish: `GcpMessageProducer.SendWithDelayAsync` delegates a non-zero delay to the `Scheduler` seam (throws
without one). Added `GcpHarnessMessageScheduler` (wall-clock re-publish, mirrors Redis/MSSQL/SNS). That alone
was **not** sufficient: `GcpPullMessageConsumer.Receive`/`ReceiveAsync` **ignored the caller's `timeOut`** and
long-polled the `Pull` until a message arrived, so FR-9's short before-delay negative window blocked ~5 s and
caught the scheduled re-publish (`MT_EVENT` where `MT_NONE` is required). A `Receive` that ignores its timeout
also blocks the Reactor pump indefinitely — a latent gateway bug. Localized `src` fix (in-boundary per ADR
0067): bound the `Pull` to `timeOut` via `CallSettings.FromExpiration(Expiration.FromTimeout(...))` and treat a
`DeadlineExceeded` with no messages as an empty receive (a null/non-positive timeout stays unbounded,
preserving prior behaviour). With both, FR-9 passes both variants.

**FR-2 `Deferred` — the seeded gap, confirmed.** `GcpPullMessageConsumer.Requeue` calls
`ModifyAckDeadline(..., 0)`, redelivering immediately regardless of the requested delay; the before-delay arm
observed `MT_EVENT`. Not scheduler-routed (contrast FR-9's producer path), so the harness scheduler does not
help. FR-15 (explicit zero-delay requeue) and FR-16 (Nack → redelivery, `ModifyAckDeadline(..., 0)`) and FR-22
(plain requeue) all `Pass` natively.

**FR-4/5/6/8/17 `Deferred -> #4240 (sign-off: @maintainer)` — the reject/DLQ family.** Two independent reasons,
maintainer decision recorded (owner chose *defer all*, this session):
1. **Infra:** the Pub/Sub **emulator cannot exercise any DLQ-configured subscription.**
   `GcpPubSubMessageGateway.EnsureSubscriptionExistsAsync` unconditionally calls
   `UpdateIAmRoleForDeadLetterAsync`, which uses ResourceManager `GetProject` **and**
   `IAMPolicyClient.GetIamPolicy`; the emulator returns `Unimplemented` for IAM (and does not implement
   ResourceManager). This is *why* CI runs GCP against real Pub/Sub. Not verifiable on available local infra.
2. **Architectural (per the FR-8 guardrail above):** GCP's `Reject` calls `client.Acknowledge(...)` — it
   **discards** the message; it does not route to a DLQ. GCP's native DLQ triggers only on `maxDeliveryAttempts`
   exhaustion (requeue/nack), never on `Reject`. So the canonical "reject once → appears in DLQ" tests would
   likely not pass even on real GCP without a gateway change — this is **not** purely an infra gap, and GCP's
   empty `RejectionMetadataKeys` here read as an *unfilled stub*, not a deliberate native-dead-letter
   declaration. FR-5 additionally has no separate invalid channel (like RMQ). Conforming needs Brighter-managed
   reject→DLQ routing (and invalid routing) in `src/…GcpPubSub` — larger than a localized fix.

**Legacy `too_many_times → dead_letter_queue` test gated off.** This capability-gated legacy template (not one
of the 11 conformance columns; on the ADR 0067 deletion list) also needs the IAM-backed DLQ subscription and
so fails on the emulator. Set `HasSupportToDeadLetterQueue: false` for `GCP / Pull` (the flag's only remaining
effect is to gate this one legacy test — the canonical DLQ family is ledger-driven and independent), matching
Kafka/MSSQL/Postgres, and deleted the two stale generated files. Revisit (flag → true) if GCP DLQ is later
verified on real Pub/Sub and the reject-routing gap is closed. *The same treatment applies to the other three
GCP configs; Stream/StreamOrdering additionally use a different consumer (`GcpPubSubStreamMessageConsumer`,
`gcpStreamMessage.Reject()`) and need their own diagnosis.*
