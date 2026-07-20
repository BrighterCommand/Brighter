# Decision log — spec 0036, universal transport conformance tests

Why identifiers were retired and why decisions were reversed. **This file is the record of
deliberation.** `requirements.md` states what must be true; the ADRs state how and why the design
works; neither carries withdrawal narration. Readers wanting only the specification can ignore this
file entirely.

Retired identifiers are never reused, so a gap in the numbering is deliberate. Each is explained
below.

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
