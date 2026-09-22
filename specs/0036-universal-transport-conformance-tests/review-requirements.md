# Review: requirements — 0036-universal-transport-conformance-tests

## Round 10 (2026-07-20) — review of round-9 remediation + the OOS reframing

**Threshold**: 60
**Verdict**: PASS — **0 findings at or above threshold 60.** First clean requirements round of the
series (findings by round 4→10: 11 → 8 → 5 → 11 → 8 → 9 → **2**, and both round-10 findings are
below threshold). No Critical for two rounds since the one round-9 raised and closed.

The round-9 remediation was verified against source and holds up: AC-2's before-`D` arm is a single
unretried receive listed in AC-20's exemption list, FR-2 and both ADRs are two-sided, NFR-2's bound
is stated once (500 ms / 30 s / 5 s) and consistent, FR-15 is assertable and no longer claims to
"pin the lower boundary", FR-21 seeds the five known non-conformances, FR-20(2)/AC-23 require both
provider interfaces with no "and/or" surviving, test counts are 31/19/15, the `PostgresSQL` token is
normalised, AC-24 precedes AC-25, 6+36+6+32 = 80, and every live FR maps to ≥1 AC.

Two below-threshold findings, **both remediated immediately after this round**:

### F1. FR-21 mis-cited `GcpPubSubStreamMessageConsumer.Requeue`'s mechanism (Score: 58) — FIXED

The F4 seeding text claimed both GCP consumers call `ModifyAckDeadline(..., 0)` with the doc "not
used by Pub/Sub". Verified in source: only `GcpPullMessageConsumer.Requeue` does that (`:297`, doc
`:281`); `GcpPubSubStreamMessageConsumer.Requeue` calls `gcpStreamMessage.Reject()` (`:224`), doc
"may not be strictly honored by Pub/Sub" (`:215`). The conclusion (all four GCP ignore the delay ⇒
fail AC-2) was unaffected, and the ADRs already cite only Pull — so requirements.md was inconsistent
with its own ADRs in fresh round-9 text. **Corrected**: the GCP bullet now cites Pull's
`ModifyAckDeadline(..., 0)` and Stream's `Reject()` separately, matching source and the ADRs.

### F2. OOS-2's "fallback from a native DLQ to Brighter's generic one" collided lexically with FR-6's in-scope fallback ladder (Score: 45) — FIXED

FR-6 is the "Fallback ladder" (reject-reason routing, in scope); OOS-2's DLQ-provisioning "fallback"
reused the word. The distinction was recoverable from context but the collision was avoidable.
**Corrected**: OOS-2 now reads "the *provisioning* substitution of Brighter's generic DLQ for an
absent native one (distinct from FR-6's in-scope reject-reason routing to whatever DLQ exists)".

**Summary**

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 1 |
| 0-49 (Low) | 1 |

**Total findings**: 2 · **At or above threshold (60)**: 0 · both fixed post-round.

---

## Round 9 (2026-07-19) — review of round-8 remediation

**Threshold**: 60
**Verdict**: NEEDS WORK — 6 findings at or above threshold 60. **One Critical** (F1), ending three
rounds clean at 90+.

> **All nine findings remediated (2026-07-19).** F2/F5/F6/F7/F8/F9 applied first. F1/F3/F4 held for a
> spec-owner ruling on FR-2's shape; the owner chose **FR-2 stays universal-by-wallclock with an
> AC-9-style lower-bound arm**, and `FakeTimeProvider` was routed to OOS-2. The three then landed as
> one change: AC-2 gained an immediate-`MT_NONE` before-`D` arm (added to AC-20's exemption list);
> NFR-2's bound was quantified (500 ms poll / 30 s ceiling, 5 s positive delay); FR-15's unassertable
> "no window elapsing" clause and its false "pins the lower boundary" claim were replaced with a
> concrete first-iteration/elapsed-<5 s assertion; FR-21 now names the five known FR-2
> non-conformances (GCP ×4 + RocketMQ); ADR 0067's "two vs five" was reconciled (two transports /
> five configurations); ADRs 0066/0067 swept for the two-sided FR-2. Independently re-verified.
> Round 10 is owed on this remediation before approval.

Round 8's arithmetic remediation is **clean**, verified against source: all four legacy templates
generate today (6 + 36 + 6 + 32 = 80 checked-in copies, confirmed by `find`), four gate branches in
`SkipTest` keyed on three gates, exactly 20 configurations across 9 wired projects, exactly 3
declaring `HasSupportToDelayedMessages: true`, DLQ∧Requeue = 16, Requeue = 18, 62 files matching
`setupDeadLetterQueue` of which **0** are the 32 exhaustion copies (positional bare `true` confirmed
at the template's line 48), `HandledCountReached` with exactly two callers, both in the pump. Every
live FR has at least one AC; no retired identifier has reappeared; the FR-15/FR-22 boundary has not
been re-merged and `null` has not been re-added to FR-15. Every count in the document reconciles.

**F1 was not found by the review agent.** It was raised from the spec owner's own read of AC-2 and
then confirmed by an independent verification pass against source. The review agent filed F4 —
that five configurations are known to fail FR-2 — without noticing that AC-2 as written would
report all five as conforming. Recording this because it is the same class of miss as round 8's:
a reviewer asserting a count while the evidence in front of it contradicted the assertion.

Findings by round: 11 → 8 → 5 → 11 → 8 → **9**.

### 1. FR-2's "after the delay" obligation is stated and then never rendered into an assertion; AC-2 cannot detect the non-conformance the spec says it will detect (Score: 92)

FR-2's normative prose is correct — the heading reads "Requeue with delay redelivers **after** the
delay" and the body says "the message is redelivered after that delay". But neither the worked
Example nor AC-2, its sole acceptance criterion, renders that "after" into an assertable form. Both
arms of AC-2 are positive: `Requeue` returns `true`, and *a later receive* within the bounded retry
loop yields the body. "A later receive" has no floor — a receive at t=0ms qualifies. AC-2 therefore
cannot distinguish "honoured D" from "ignored D and redelivered immediately".

Three pieces of corroborating evidence that the arm was never intended, rather than merely worded
loosely:

- **AC-9 and AC-16 both carry the arm AC-2 lacks.** AC-9: "*when* a receive is attempted
  immediately, *then* it yields `MT_NONE`". AC-16: "with no delay window elapsing". FR-9's prose
  even states the two-sided obligation explicitly ("not receivable before the delay elapses and is
  receivable after it") — which FR-2's prose never does. The document has the vocabulary and
  applies it to the delay requirements on *either side* of FR-2.
- **AC-20's exemption list is exhaustive and omits AC-2.** "Exempt are only:" names four arms —
  AC-5, AC-9, AC-16, AC-18. A negative arm for AC-2 would necessarily be a single unretried receive
  and would therefore *have* to appear on that list. Its absence is structural evidence.
- **FR-12 constrains the call, not the assertion.** It requires delayed-requeue templates to pass a
  non-null `TimeSpan` — but a template can pass `5s` and assert nothing about it. That is precisely
  the defect FR-12 exists to kill in the legacy `with_delay` template, reappearing one level up.

**The consequence is a direct contradiction with the seeded ledger.** ADR 0066 (`:303-307`) and
ADR 0067 (`:84-95`, `:154`) assert as established fact that GCP ×4 and RocketMQ fail FR-2. Verified
in source this round: `GcpPullMessageConsumer.Requeue` ignores `delay` and calls
`ModifyAckDeadline(..., 0)` — its XML doc reads "An optional delay (not used by Pub/Sub)" — and
`GcpPubSubStreamMessageConsumer.Requeue` likewise; `RocketMessageConsumer.Requeue` is a no-op
returning `true` with `ChangeInvisibleDuration` commented out. GCP **does** redeliver, just
immediately. So the generated FR-2 test would report GCP's four configurations **green** while the
ledger seeds them as failing. FR-21's completeness gate could then be satisfied by a cell reading
"conforms" for a behaviour the source demonstrably does not implement.

**FR-15's prose is also wrong, not merely incomplete.** It claims to "pin the lower boundary of the
delay parameter so the positive-delay path (FR-2) is not conflated with the no-op path". It does
not: a gateway ignoring a positive delay passes FR-15 and FR-2 identically — the exact conflation
the sentence claims to prevent.

**Evidence**: `requirements.md` AC-2 — "*then* `Requeue` returns `true` and a later receive within
the bounded retry loop yields a message with the same body"; FR-2 Example — "assert `Requeue`
returns `true` and a subsequent receive, within a bounded retry loop, yields a message whose body
equals `M`'s", which is behaviourally identical to AC-25's *no-delay* criterion except for the
argument. Against ADR 0066 `:303-307` and ADR 0067 `:84-95`, `:154`.

**Recommendation**: Give AC-2 a lower-bound arm mirroring AC-9 — "*when* a receive is attempted
before the delay elapses, *then* it yields `MT_NONE`" — and add that arm to AC-20's exemption list
as a fifth entry. Mirror it into FR-2's prose and worked Example. Correct FR-15's "pins the lower
boundary" sentence, which is the claim this finding refutes. **Sequencing**: this repair is what
*causes* GCP ×4 and RocketMQ to fail, so it must land in the same change as F4's seeded ledger rows,
not after them.

---

### 2. FR-20's "and/or", AC-23's "at least one" and AC-14's "supporting both" together open a documented route to zero Proactor coverage, which FR-21 then makes permanently non-conformant (Score: 68)

FR-20 step 2 permits an onboarding to implement one provider interface. AC-23 ratifies this ("at
least one `*MessageGatewayProvider.cs`"). AC-14 hedges parity behind an untested precondition ("a
configuration **supporting both** sync and async channels"), and the document never says who decides
whether a configuration supports both, or on what evidence.

But FR-14 is unconditional ("Every canonical template MUST be produced in both a Reactor variant …
and a Proactor variant"), and FR-21 makes parity constitutive of conformance ("A configuration
conforms for a behaviour only when **both** the Reactor and Proactor variants pass"). So a
Reactor-only onboarding is not a smaller version of the work — it is a row whose eleven cells can
*never* read `Pass` or `Fixed`, only signed-off `Deferred`, indefinitely. Nothing in the document
acknowledges that outcome or forbids it, and FR-13's prohibition on gating a test away "to make the
suite green" is exactly what a one-variant onboarding achieves with a maintainer signature.

Verified this is not needed in the safe direction: **all twenty existing providers implement both
interfaces** (confirmed — 20 of 20 non-generated `*MessageGatewayProvider.cs` name both), and all
three FR-20 transports have both sync and async consumer/producer surfaces
(`RmqMessageConsumer`/`RmqMessageConsumerFactory`, `MQTTMessageConsumer`, `AzureServiceBusConsumer`).
There is no transport for which the "and/or" is needed; its only effect is to license a gap.

**Evidence**: FR-20(2) — "implement `IAmAMessageGatewayReactorProvider` **and/or**
`IAmAMessageGatewayProactorProvider`"; AC-23 — "**at least one** `*MessageGatewayProvider.cs`";
AC-14 — "*when* a configuration **supporting both sync and async channels** is generated"; against
FR-14 and FR-21.

**Recommendation**: Change FR-20(2) to "implement **both**" and AC-23 to "**two** provider
implementations, or one type implementing both interfaces". Delete AC-14's "supporting both sync and
async channels" hedge. If a one-variant onboarding is genuinely to be permitted, say so explicitly
and state that its row is `Deferred` by construction.

---

### 3. FR-15/AC-16's "with no delay window elapsing" is not assertable, and AC-20's exemption rationale does not apply to it (Score: 65)

FR-15 is now the *only* thing separating the zero-delay behaviour from FR-22's plain requeue, so its
distinguishing assertion carries the whole weight of the round-8 boundary ruling. That assertion is
"receivable again **without a delay window elapsing**". No window is defined. There is nothing to
elapse — the point of the requirement is that the delay is zero.

AC-20 compounds this. It lists AC-16's clause among the four exemptions and explains all four with
one sentence: "Each of those uses a **single bounded receive after the stated window**". AC-5, AC-9
and AC-18 all have a stated window or a stated "immediately". **AC-16 has neither**, so AC-20's
rationale is inapplicable to the one entry that most needs it.

Aggravated by NFR-2 never quantifying the bound it mandates. "Bounded receive-retry loop" appears in
NFR-2, FR-2, FR-22, AC-2, AC-16, AC-17, AC-25 and AC-20 with no retry count, interval, or wall-clock
ceiling anywhere. Because AC-16's only remaining assertion is "receivable again within the
plain-requeue bounded retry loop", the bound is precisely what determines whether FR-15's test
asserts anything FR-22's does not. **This interacts with F1**: quantifying the bound is also what
makes F1's lower-bound arm expressible.

**Evidence**: FR-15 — "receivable again without a delay window elapsing"; AC-20 — "Exempt are only:
… AC-16's 'no delay window elapsing' … Each of those uses a single bounded receive **after the
stated window**". NFR-2 states no bound.

**Recommendation**: Replace AC-16's clause with a concrete one — e.g. "received on the **first**
iteration of the plain-requeue retry loop, and total elapsed time from `Requeue` to receipt is less
than FR-2's delay value". Quantify NFR-2's bound once and have FR-2/FR-15/FR-22 cite it.

---

### 4. Round-7 F11 re-assessed: the known FR-2 non-conformances are asserted as fact in both ADRs and stated nowhere in requirements.md, and FR-21's seeding rule is a dangling forward reference (Score: 62)

**Strengthened since round 7; now above threshold.** In round 7 this was an ADR-only fact (score
45). Round 8 added FR-21's closing sentence — "Non-conformances identified before the rollout begins
are seeded into the ledger rather than discovered late" — which introduces a category of information
and never populates it. A reader of requirements.md alone cannot tell whether any such
non-conformance exists; the sentence reads as a contingency when it is known to apply to **five of
the twenty rows on day one**.

This is material at the requirements level, not design detail: FR-13 declares non-conformance "a
**defect in that transport's gateway** and is in scope to fix", so five known-failing configurations
are five in-scope defects the requirements never scopes. It also decides RocketMQ's likely
disposition (blocked on an upstream client release ⇒ `Deferred`), which is a scope commitment.

The ADRs are also internally inconsistent on granularity — 0067`:84` says "**Two** known FR-2
non-conformances" (transports) while `:154` says "**five** of them" (configurations) — precisely the
per-project/per-configuration confusion FR-13 exists to prevent.

**Of the two candidate resolutions, (a) is right and (b) does not work.** The ledger cannot be the
sole home: it does not exist yet, it is *created by* this spec under FR-21, and FR-13 makes these
non-conformances in-scope work items. A scope fact cannot live only in the artifact the scope
produces.

**Evidence**: FR-21 final paragraph (no non-conformance named anywhere in the document). Contrast
`docs/adr/0066-…:303-307` and `docs/adr/0067-…:84-95, :154`.

**Recommendation**: Add two or three sentences alongside FR-21's seeding rule naming the four GCP
configurations and RocketMQ, with the mechanism for each and RocketMQ's expected deferral. Reconcile
0067`:84`'s "Two" with `:154`'s "five" by stating both counts explicitly (two transports, five
configurations). **Land with F1** — see F1's sequencing note.

---

### 5. AC-24's placeholder-row clause carries no temporal qualifier and contradicts ADR 0067's seeding step (Score: 62)

AC-24 asserts unconditionally that a placeholder row's "cells **all read as a signed-off deferral**".
FR-21 says the same ("**may only** record a signed-off deferral"). But ADR 0067's step 1 seeds the
entire ledger — placeholder rows included — with `Unknown`, and explicitly parenthesises this. Read
literally, the ADR's mandated first action violates the requirement, and AC-24 fails from the moment
the ledger is created until sign-off is obtained.

The neighbouring FR-21 bullet ("A cell may be provisionally unresolved only while the fix phase is in
progress") makes the intent recoverable, but AC-24 is the *testable* form and is stated as an
unqualified property of the file. The document elsewhere shows it knows how to handle this: AC-11
scopes to "after AC-10(b) holds", AC-12 and AC-22 to "after the legacy retirement of AC-10(b)", and
AC-24's own third paragraph to the merge point. Only the placeholder paragraph is unscoped. This is
the newest and least-reviewed AC in the document.

**Evidence**: AC-24 — "whose cells all read as a signed-off deferral"; FR-21 — "may only record a
signed-off deferral". Contrast `docs/adr/0067-…:230-243` and step 1 at `:308-310`.

**Recommendation**: Scope it as AC-11 and AC-12 are scoped: "…whose cells, **at the point the
FR-10(4)/FR-11 change is proposed for merge**, all read as a signed-off deferral; before that point
they may be provisionally unresolved, but may only ever resolve to a deferral." Mirror into FR-21.

---

### 6. FR-19 carries narration about the superseded draft sequencing, which belongs in decision-log.md (Score: 60)

requirements.md is required to carry assertions only. FR-19 argues its case partly by counterfactual
against a withdrawn earlier decision, naming it as such. The content is not wrong (Kafka ×2 + MSSQL +
PostgreSQL = 4, and 16 + 4 = 20) and it duplicates material already correctly held in
`decision-log.md` and ADR 0066's Alternatives Considered §5 — the right homes. Left in place it
re-establishes the precedent that requirements prose may reason about drafts.

**Evidence**: FR-19 — "**Under the superseded 'retire the gates first' sequencing** it would have
been ungated onto Kafka ×2, MSSQL and PostgreSQL … FR-10(3) removes that hazard by construction."

**Recommendation**: Delete both sentences. The live assertion FR-19 needs is already carried by
FR-10(2)'s closed-list guarantee and FR-19's preceding sentence.

---

### 7. FR-20's hand-written test counts (31 / 18 / 8) are not reproducible under any single counting rule (Score: 58)

FR-20 states "RMQ.Sync 31 tests, MQTT 18, AzureServiceBus 8." The unit "tests" is undefined and no
consistent rule yields all three. Counted against source this round:

| | files under `MessagingGateway/` | `[Fact]`+`[Theory]` there | whole project `[Fact]` | claimed |
|---|---|---|---|---|
| RMQ.Sync | **31** | 51 | 53 | 31 |
| MQTT | 19 | 27 | 30 | **18** |
| AzureServiceBus | 15 | 89 | 64 | **8** |

RMQ.Sync's 31 is exactly the file count; the same rule gives MQTT 19 and AzureServiceBus 15.
AzureServiceBus's "8" equals its top-level `When_*.cs` count, a rule giving MQTT 4 and RMQ.Sync 5.
Three numbers, three rules. FR-20 uses these to size the largest new work in the spec, and ADR
0067`:101` and `decision-log.md:139` repeat them verbatim. AzureServiceBus — the transport the spec
expects to defer — is understated by roughly half.

**Recommendation**: State the unit and recount under one rule — "test files under
`tests/Paramore.Brighter.*.Tests/MessagingGateway/`: RMQ.Sync 31, MQTT 19, AzureServiceBus 15" — and
sweep the ADR and decision log. **Also correct PROMPT.md**, which carries 18 and 8 as verified facts.

---

### 8. The PostgreSQL test project is named inconsistently, and ledger row identity depends on that token (Score: 52)

FR-13's mapping table gives the test project as `PostgresSQL` (correct — verified
`tests/Paramore.Brighter.PostgresSQL.Tests`), but FR-13's prose list and FR-21's naming rule both
write `PostgreSQL`. FR-21 makes row identity `project / configuration` and ADR 0067 specifies a CI
audit cross-checking ledger rows against in-code `Skip` markers, so the token is a machine-compared
string. `PostgreSQL / …` and `PostgresSQL / …` are different rows.

The document already warns about exactly this class of error for AzureServiceBus ("Use the table,
not a name-derivation rule") and then does not follow its own table.

**Recommendation**: State once that row identity uses the **test project** name from FR-13's table,
and normalise every occurrence to `PostgresSQL`. Consider adding the four singular-section rows as
worked examples.

---

### 9. AC-25 is placed before AC-24 (Score: 25)

The section runs AC-20 … AC-23, **AC-25, AC-24**. Every other identifier is in ascending order, and
the two out-of-order entries are the two newest ACs.

**Recommendation**: Swap them.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 1 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 7 |
| 0-49 (Low) | 1 |

**Total findings**: 9
**Findings at or above threshold (60)**: 6

---

## Round 8 (2026-07-19) — first pass over the gating-lifecycle reversal

**Threshold**: 60
**Verdict**: NEEDS WORK — 4 findings at or above threshold 60. **No Critical findings** (third
round running clean at 90+).

First review of the material rewritten under the 2026-07-19 spec-owner ruling (FR-9, FR-10, FR-11,
FR-12, FR-19 rewritten; FR-21/AC-24 and FR-22/AC-25 new). Findings by round: 11 → 8 → 5 → 11 → **8**.

The dominant theme is a single conflation: the reversal was written as if *not ungated* meant *not
generated*. It does not. The gates are `true` for most configurations, so three of the four legacy
templates generate today — for 16, 18 and 3 configurations respectively — and will keep generating on
every regeneration run until they are deleted. That one error produces findings 1 and 3 and has
propagated into both ADRs, the README and the decision log.

> **Corrected by design review round 6 (finding 3):** this paragraph is itself wrong — it is **all
> four**, for 16, 18, 3 and 3 configurations. The finding-1 table below tabulates four non-zero
> counts (6 + 36 + 6 + 32 = 80), which contradicts the "three of the four" in this summary. The
> delayed-message template generates for the same three configurations as `with_delay`. The error was
> propagated into both ADRs, requirements.md, the README and the decision log during remediation, and
> corrected in all five. Left in place here as the record of what round 8 actually said.

**Main-agent verification note.** Every factual claim below was independently re-verified against
source before remediation (the standing rule after two rounds were lost to unverified remediation).
All confirmed. Finding 3 was found to be *understated* — see the addendum on that finding.

### 1. AC-22 asserts the legacy templates are "never generated" and FR-10(3) says they "stay suppressed" — both are false for most configurations, and AC-22 contradicts itself in the same sentence (Score: 85)

The rewrite's central framing is that the four legacy templates are suppressed for their whole
remaining life. That is true only where the gate is `false`. Verified gate values across the twenty
wired configurations:

| Legacy template | Gates | Configurations where it generates | Copies |
|---|---|---|---|
| `..._too_many_times_should_move_to_dead_letter_queue` | DLQ ∧ Requeue | AWS 4, AWS.V4 4, GCP 4, Redis 1, RMQ.Async 2, RocketMQ 1 = **16** | 32 |
| `..._failed_message_should_receive_message_again` | Requeue | all but Kafka ×2 = **18** | 36 |
| `..._with_delay_...` | Delayed ∧ Requeue | AWS/SqsStandard, AWS.V4/SqsStandard, RocketMQ = **3** | 6 |
| `..._delayed_message_...` | Delayed | same **3** | 6 |

AC-22 is self-contradicting: it asserts "never generated" and in the preceding clause counts the 32
generated copies that disprove it. As a test assertion it is unsatisfiable — an implementer checking
"is this template ever generated?" finds the file under AWS, GCP, Redis, RMQ.Async and RocketMQ and
must conclude AC-22 fails before the deletion, when the intent is that it passes trivially.

The distinction the document needs — and repeatedly blurs — is between *never ungated* (true, and
what FR-10(2) actually guarantees) and *never generated* (false).

**Evidence**: `requirements.md:548-553` (AC-22) — "Before that point it is expected to be present and
suppressed by its gates, **never generated**" immediately after "(**thirty-two such copies exist
today**)". `requirements.md:197-199` (FR-10(3)). Same framing in `README.md:73-76`,
`decision-log.md:49-50`, `docs/adr/0066-…:128, :401, :443, :515` and frontmatter `summary`,
`docs/adr/0067-…:36, :180`.

**Recommendation**: Replace "stays suppressed" / "never generated" with "is never *ungated* — it
continues to generate wherever its gate is already `true`, and generates nowhere new". Rewrite
AC-22's final clause to name the sixteen configurations explicitly. Sweep FR-10(3), FR-9, FR-22,
AC-25, both ADRs, the README and the decision log.

---

### 2. FR-22 and FR-15's null arm are the same call: `Requeue`'s delay parameter is optional and defaults to `null` (Score: 72)

FR-22 claims it "is distinct from FR-15, which pins the explicit `TimeSpan.Zero` / `null` arguments
rather than the no-argument call". That distinction does not exist. The runtime signature is
`bool Requeue(Message message, TimeSpan? timeOut = null)` — `Requeue(m)` and `Requeue(m, null)`
compile to the identical call. AC-25 and AC-16 therefore assert the same behaviour of the same call,
with the same expected outcome.

This is not a naming quibble — FR-22 is a *new* canonical behaviour with its own ledger column
(FR-21), so it duplicates a column with FR-15's null arm.

**Evidence**: `src/Paramore.Brighter/IAmAChannelSync.cs:83`; `IAmAChannelAsync.cs:90`.
`requirements.md:222-224` (FR-22), `:299-303` (FR-15), `:522-524` (AC-16), `:564-569` (AC-25).

**Recommendation**: Either state that FR-15 covers only the `TimeSpan.Zero` boundary and that the
null/omitted case is FR-22, or merge them into one template with three assertions and one ledger
column. Resolve FR-15's "generate or extend" into a single decision so AC-12's reference to "the
zero/null-boundary template" resolves.

---

### 3. FR-1(6) removes `setupDeadLetterQueue` while the exhaustion template still generates for sixteen configurations, and nothing requires the interim template edit (Score: 70)

FR-1(6) removes `bool setupDeadLetterQueue` from `CreateSubscription` and requires all twenty
provider implementations, both interface templates, and "every **generated caller**" migrate in the
same change. FR-19 notes the exhaustion template is "the **only** current caller" — but the new
lifecycle keeps that template generating for sixteen configurations until AC-10(b), long after FR-1
lands. The `.liquid` template is the *source*, not a "generated caller", so FR-1(6)'s enumeration
does not reach it. An implementer who satisfies FR-1(6) literally regenerates and the AWS, GCP,
Redis, RMQ.Async and RocketMQ test projects fail to compile.

**⚠️ Addendum from main-agent verification — the finding is understated.** The exhaustion template
passes the flag **positionally**, not by name:

```csharp
_subscription = _messageGatewayProvider.CreateSubscription(_publication.Topic!,
    _messageGatewayProvider.GetOrCreateChannelName(),
    OnMissingChannel.Create,
    true);          // <-- setupDeadLetterQueue, positional
```

So the 32 generated copies contain **no occurrence of the string `setupDeadLetterQueue`**. A
maintainer following FR-1(6) by grepping the parameter name finds the 40 interface copies and the 20
provider implementations (62 files total) and **misses all 32 broken call sites**. The bare `true`
will not bind to the new nullable `RoutingKey?` parameters, so this is a hard compile break that the
obvious search strategy cannot find.

**Evidence**: `requirements.md:104-110` (FR-1(6)), `:337-339`, `:197-200`.
`tools/…/Templates/MessagingGateway/Reactor/When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue.cs.liquid:45-48`
(and the Proactor twin at `:50`). Verified: 62 files match `setupDeadLetterQueue`; 0 of the 32
generated exhaustion copies do.

**Recommendation**: Add an explicit interim obligation to FR-1(6) or FR-19: the exhaustion template
MUST be edited in the FR-1 change to stop passing the flag, so the sixteen configurations for which
it still generates continue to compile until deletion. Extend AC-1 to name the still-live legacy
template **and to state that positional call sites will not be found by name search**.

---

### 4. FR-21's ledger row identity `project / configuration` cannot be constructed for four wired projects, nor for a transport deferred before onboarding (Score: 68)

FR-21 mandates "One row per targeted gateway configuration … identified as `project / configuration`
— e.g. `AWS.V4 / SqsStandard`". All three examples come from `MessagingGateways` (plural) sections,
where the configuration name is the JSON key. Four of the twenty wired configurations — Redis, MSSQL,
PostgreSQL, RocketMQ — use the **singular** `MessagingGateway` section, which has no name key. So
four of twenty rows have no constructible identifier.

The same gap appears at the other end: a transport deferred at FR-20 step 1 declares no
configuration, so AC-24's completeness assertion is unverifiable for exactly the transport the spec
expects to defer (AzureServiceBus).

**Evidence**: `requirements.md:375-378`, `:570-578`, `:363-367`. Verified: MSSQL, PostgresSQL, Redis
and RocketMQ `test-configuration.json` all use `"MessagingGateway"` singular with no name key; AWS,
AWS.V4, Gcp, Kafka, RMQ.Async use `"MessagingGateways"` plural.

**Recommendation**: State the naming rule for singular sections (name by `CollectionName`), give a
worked example, and state that a transport deferred before declaring any configuration carries a
single placeholder row whose cells all read as the signed-off deferral. This is the requirements-side
twin of design finding F3.

---

### 5. FR-21's completeness gate cites "FR-10" whole, but FR-10 spans the entire lifecycle including its first step (Score: 52)

FR-21's gate reads "the change that retires the three gates (**FR-10**, FR-11) MUST NOT merge while
any cell remains unresolved". FR-10 is now a four-part lifecycle whose *first* step must land before
any canonical test can generate, and therefore before any ledger cell can resolve. Read literally,
the FR-10 change is gated on a ledger that cannot be populated until the FR-10 change has landed —
precisely the circular dependency this rewrite exists to dissolve. The "gate-retirement" qualifier
makes the intent recoverable, so this is imprecision rather than contradiction.

**Evidence**: `requirements.md:384-386`, `:573-574`; contrast `:201-203` (FR-10(4)).

**Recommendation**: Cite `FR-10(4)` and `FR-11` rather than `FR-10` in both places.

---

### 6. The problem statement still names two configurations where there are three (Score: 50)

"Only AWS `SqsStandard` and RocketMQ declare `HasSupportToDelayedMessages: true`, so the
delayed-delivery test runs for three of roughly twenty gateway configurations." The enumeration reads
as two items and the count says three. This is the exact phrasing error PROMPT.md flags as recurring.

**Evidence**: `requirements.md:28-30`. Verified: exactly three configurations declare it —
`AWS.Tests`, `AWS.V4.Tests`, `RocketMQ.Tests`.

**Recommendation**: "Exactly three configurations declare `HasSupportToDelayedMessages: true` —
`AWS/SqsStandard`, `AWS.V4/SqsStandard` and `RocketMQ` — so the delayed-delivery test runs for three
of the twenty wired gateway configurations."

---

### 7. README's canonical-behaviour enumeration was not updated for FR-22 (Score: 45)

The rewrite added FR-22 to requirements.md's terminology list, FR-21's columns, AC-24 and the
Coverage Reconciliation table. The spec README's Scope section still enumerates the set without it.

**Evidence**: `README.md:47` and the nine bullets following, none covering plain requeue.

**Recommendation**: Add FR-22 to the enumeration and a plain-requeue bullet.

---

### 8. "The three gate branches" in `SkipTest` are actually four (Score: 40)

FR-10(4) and AC-10(a)/(c) refer to "the three gate branches in `SkipTest`". `SkipTest` has **four**
branches keyed on the three properties: `HasSupportToDelayedMessages` appears twice, once for
`delayed_message` and once for `with_delay`. An implementer removing "the three gate branches" leaves
one behind.

**Evidence**: `MessagingGatewayGenerator.cs` `SkipTest` — verified branches at lines 122, 127, 132,
145. `requirements.md:201-203`, `:482-484`, `:491-492`.

**Recommendation**: "the four branches keyed on the three gates" throughout FR-10(4) and AC-10.

---

### Checks that passed (no finding)

Verified against source: twelve `src/Paramore.Brighter.MessagingGateway.*` projects; fourteen
`test-configuration.json` files, nine with a gateway section, the five outbox-only exclusions named
correctly; twenty configurations distributed 4+4+4+2+2+1+1+1+1; twenty `*MessageGatewayProvider.cs`
implementations; forty generated provider-interface copies; 6+36+6+32 = **80** generated legacy
copies; the FR-13 mapping table including the `Paramore.Brighter.Azure.Tests` trap; RMQ.Sync 31 /
MQTT 18 / ASB 8 hand-written tests; Kafka as the only all-three-gates-`false` configuration; FR-12's
characterisation of the `with_delay` template; the four gated filenames matching exactly the
substrings FR-10(3) attributes to them, with no fifth template matching any of them.

**Ordering integrity is adequately handled** — FR-10 states its parts "in this order", AC-10 is three
ordered checkpoints, and AC-11 explicitly pins the *before* state. The dangerous intermediate state is
stated as a constraint and is checkable at a single point in time. No finding.

Every FR maps to at least one AC and every AC to an FR or NFR; the retired-identifier gaps match the
closing note exactly.

### Round 8 summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 3 |
| 50-69 (Medium) | 3 |
| 0-49 (Low) | 2 |

**Total findings**: 8
**Findings at or above threshold (60)**: 4

---

## Round 7 (2026-07-19) — cross-document consistency after the twelve-transport rewrite

**Threshold**: 60
**Verdict**: NEEDS WORK — 10 findings at or above threshold 60. **No Critical findings** (second
round running clean at 90+).

Round weighted toward cross-document consistency per the standing instruction: round 6 found that
requirements were being edited without the ADRs following. That is exactly what this round found
again — 5 of the 10 at-threshold findings are ADR follow-through (2, 3, 6, 7, 8), and one is the
spec README (9). Only findings 1, 4, 5 and 10 are defects in `requirements.md` itself.

Findings by round: 11 → 8 → 5 → 11. The count rose because FR-20, the twelve-transport target set,
the widened C-1, AC-20's exemption and the AC-12/AC-22 generated-copy clauses had never been
reviewed by anyone.

### 1. FR-13's definition of "targeted gateway configuration" is a truncated, dangling sentence — the term is never defined (Score: 88)

FR-13 opens by defining two terms. The first definition stops mid-clause and the second sentence
begins on the next line, so `requirements.md` never says what a *targeted gateway configuration* is.
The term is load-bearing: it is in FR-13's own heading, it is what FR-13's normative sentence ("MUST
be generated for **every** configuration of **every** targeted transport") quantifies over, and
AC-13 verifies "every configuration of all twelve gateway transports". Confirmed by direct read —
a real editing artifact, not a rendering effect.

The only complete statement of the rule now lives in ADR 0067 (`0067:42-45`: "generation is per
**gateway configuration**, not per project… those nine declare **twenty** configurations"). That
inverts the intended direction of authority: a requirement's defining term is recoverable only from
a design document.

**Evidence**: `requirements.md:192-194`

```
**FR-13 — Generate for every targeted configuration; non-conformance is a defect to fix.**
A **targeted gateway configuration** is one declared under the `MessagingGateway` (singular, one
A **targeted transport** is **every transport with a messaging gateway** — that is, every
```

**Recommendation**: Restore the sentence, e.g. "A **targeted gateway configuration** is one declared
under the `MessagingGateway` (singular, one configuration) or `MessagingGateways` (plural, several)
section of a `tests/Paramore.Brighter.*.Tests/test-configuration.json`; generation and conformance
are per configuration, not per project." Also reconcile the heading ("targeted configuration") with
the defined term.

---

### 2. ADR 0066 was never updated for the twelve-transport target set or for FR-20 (Score: 74)

ADR 0066 still describes the target set in the pre-reversal, nine-transport world. It refers to
"**~20 target configurations**" in five places, all of which now describe only the *wired* set — the
target set under FR-13 is twelve transports, and ADR 0067 and requirements.md both say so. Line 461
was corrected to "the twenty configurations **wired today**"; lines 119, 219, 241, 281 and 490 were
not, so the ADR contradicts itself and the requirement.

Worse, **FR-20 appears exactly once in ADR 0066** (line 461, in an Alternatives footnote). It
appears nowhere in the Decision, Key Components, Implementation Approach, or Consequences. Yet
FR-1(6) — which ADR 0066 is the deciding ADR for — explicitly carves out FR-20: "Providers newly
written under FR-20 implement the post-FR-1 signature directly and never carry the bool." ADR 0066's
corresponding text enumerates only the nine wired transports' providers.

Note also that MQTT and RMQ.Sync *do* implement `IAmAChannelFactoryWithScheduler`, so once FR-20
lands, the "6 of ~20 have the seam" arithmetic in the "Why there is no scheduler member" section no
longer describes the target set at all.

**Evidence**: `docs/adr/0066-…:119` ("14 of the ~20 **target** configurations have no scheduler
seam"); `:219` ("Of the ~20 gateway configurations the generator **targets**"); `:490` ("reaches
only 6 of the ~20 **target** gateway configurations"); vs `:461` ("Of the twenty configurations
**wired today**"). Provider enumeration at `:265-268` and `:396` — "Kafka, Redis, MSSQL, PostgreSQL,
RMQ, AWS SNS/SQS, GCP, RocketMQ, …" with no AzureServiceBus / MQTT / RMQ.Sync. `grep -c "FR-20"
docs/adr/0066-*.md` returns 1.

**Recommendation**: Replace "~20 target configurations" with "the twenty configurations wired today"
everywhere it means the wired set. Add an FR-20 paragraph to Key Components and Consequences stating
that three further provider implementations are written fresh against the post-FR-1 signature, and
correct the scheduler-seam arithmetic to say it covers 6 of the 20 wired configurations, rising once
FR-20 wires two more scheduler-capable gateways.

---

### 3. AC-13 forbids skipping outright, while ADR 0067 mandates a `Skip` attribute on every deferred canonical test (Score: 72)

FR-13 correctly says "No canonical test may be **silently** skipped or gated away." AC-13 drops the
qualifier: "every configuration … has the canonical tests present, **and none is skipped or gated
away**". As a directly-executable assertion — which is what an AC is for — that reads "no
messaging-gateway test carries a `Skip`". ADR 0067 requires the opposite: every deferred canonical
test **must** carry `Skip = "Deferred: #NNNN — …"`, and a CI audit fails any deferred test lacking
one.

The trailing "no silent skip, no unaudited deferral" makes the intent recoverable, but two
developers will build different audit checks from AC-13 — one asserting zero `Skip` attributes, one
asserting only `Deferred:`-form ones.

**Evidence**: `requirements.md:376-381` (AC-13) vs `docs/adr/0067-…:178-183` ("any deferred canonical
test carries an explicit `Skip` string of the form `Skip = "Deferred: #NNNN — …"`. … A bare or
reasonless `Skip` … is a CI failure") and `:247-251`.

**Recommendation**: Reword AC-13's middle clause to match FR-13 and ADR 0067: "…has the canonical
tests present, and none is silently skipped or gated away — any skip carries an auditable deferral
marker…".

---

### 4. The "conformance ledger" is depended on by an AC but never required, defined, or created (Score: 70)

`requirements.md` refers to a "conformance ledger" twice, as though it were an established artifact,
but no FR requires its existence, no AC verifies it, and the document never names the file. AC-23
makes a deferral's validity conditional on "a conformance-ledger row" — an acceptance criterion that
cannot be evaluated because the thing it tests has no defining requirement.

ADR 0067 defines it completely (`specs/0036-universal-transport-conformance-tests/conformance-status.md`,
one row per configuration, cell vocabulary `Pass` / `Fixed (#PR)` / `Deferred -> #NNNN`, `Unknown`
blocking the flip) and makes it the **flip gate**. So the single most consequential process
obligation in the spec exists only in the design document. The file does not exist in the spec
directory.

**Evidence**: `requirements.md:277`, `:423`; no other occurrence of "ledger" in the file.
`docs/adr/0067-…:159-177`, `:221-222`, `:243-246`.

**Recommendation**: Add a numbered FR requiring the ledger — its location, its per-configuration row
granularity, its canonical-behaviour columns, and the rule that no `Unknown` cell may remain when
the ungating change merges — plus a matching AC. ADR 0067 then decides *how* it is enforced (the
Skip convention and CI audit), which is properly ADR territory.

---

### 5. FR-20(3) — "supply whatever test infrastructure the transport needs to run in CI" — is unbounded and has no acceptance criterion (Score: 70)

FR-20 enumerates three obligations per onboarded transport. The first two are concrete and
checkable. The third is not: "supply **whatever** test infrastructure the transport needs to run in
CI" specifies no target, no boundary, and no acceptance condition. AC-23 verifies only that a config
section and a provider implementation exist, and that generation emits the canonical templates — it
says nothing about infrastructure.

Not hypothetical vagueness: ADR 0067 concedes the problem directly — "**ASB in particular is a cloud
service whose CI story is not solved here**." Three reasonable implementations follow from the same
sentence: stand up an emulator, provision a live cloud namespace, or declare it infeasible and open
a deferral. All three satisfy FR-20 as written.

**Evidence**: `requirements.md:270`; AC-23 at `:418-423` contains no infrastructure clause;
`docs/adr/0067-…:286-291`.

**Recommendation**: Either state the acceptance condition ("the generated suite for the transport
executes against a broker in CI — container, emulator, or live service — and `Pass` requires an
actual run, not a compile", mirroring ADR 0067's "Infra reality" paragraph), or state explicitly
that CI infrastructure which cannot be stood up in-spec is a first-class candidate for the FR-13
deferral rule, and add that arm to AC-23.

---

### 6. DLQ ADRs are cited by bare number, and every number cited resolves to two or more ADRs (Score: 68)

`docs/adr/` reuses numbers 0038–0043. Verified by direct listing: `0038-aws-sqs-dlq-direct-send.md`,
`0038-dont-ack-action.md`, `0038-remove-clear-service-bus.md`; `0039-redis-dlq-brighter-managed.md`,
`0039-opentelemetry-builder-extension.md`, `0039-scoping-dependencies-inline-with-lifetime-scope.md`,
`0039-transport-scheduler-wiring.md`; `0040-mssql-dlq-brighter-managed.md`,
`0040-add-the-specification-pattern.md`; `0041-postgres-dlq-brighter-managed.md`,
`0041-add-parallel-split-to-mediator.md`; `0042-rocketmq-dlq-brighter-managed.md`,
`0042-use-reactive-programming-for-mediator.md`; `0043-mqtt-dlq-brighter-managed.md`,
`0043-rabbitmq-mutual-tls.md`.

Every bare-number citation is therefore ambiguous, and the range form is outright wrong: "the
per-transport DLQ ADRs (**0038–0043**, 0046)" sweeps in six ADRs about ack actions, OpenTelemetry,
DI scoping, the specification pattern, parallel split, reactive mediator, and RabbitMQ mutual TLS.
ADR 0066's References section and requirements.md FR-20 both get this right by using slugs; the body
prose of both requirements.md and ADR 0067 does not.

**Evidence**: `requirements.md:28` ("MSSQL declares no DLQ despite ADR **0040**").
`docs/adr/0067-…:55-60`, `:132-135` (diagram), `:198` and `:228` ("the per-transport DLQ ADRs
(0038–0043, 0046)").

**Recommendation**: Replace every bare number with the slug (`0040-mssql-dlq-brighter-managed`), and
replace the range with the explicit seven-slug list already present in both ADRs' References
sections.

---

### 7. ADR 0067 restates C-1 without the FR-20 widening, so its own stated constraint forbids the work it schedules (Score: 65)

C-1 was widened to permit FR-20's test-side work: "…and the test-side onboarding FR-20 requires (new
configs, new provider implementations, and the CI infrastructure to run them)." ADR 0067 restates
C-1 in its Context and omits that clause entirely. ADR 0067's own Implementation Approach step 4
then schedules exactly the omitted work — "each needing a `test-configuration.json`, provider
implementation(s) and CI infrastructure" — which its own statement of C-1 places outside the spec's
boundary.

This is the round-6 pattern repeating: requirements edited, ADR not followed.

**Evidence**: `requirements.md:295-300` (C-1) vs `docs/adr/0067-…:92-95`: "C-1 confines the work to
the generator, its templates and configs, plus the transport-gateway source fixes FR-13 requires —
no public Brighter runtime API redesign beyond FR-1's generated providers." Contrast
`docs/adr/0067-…:236-242` (step 4).

**Recommendation**: Bring ADR 0067's C-1 restatement into line with the widened C-1, naming the
FR-20 test-side onboarding as in-boundary.

---

### 8. ADR 0066 carries draft-history narration the editorial rule bans, and claims to be the "sole record" of rationale decision-log.md now also holds (Score: 64)

The editorial rule in force is that `requirements.md` and the ADRs carry assertions only; withdrawal
narration and self-reference to earlier drafts belong in `decision-log.md`. ADR 0066 violates this
in four places, and one of them makes a claim that is now factually false.

- `:115` — "**One deliberate departure from the original requirement wording.** FR-1(4) and AC-1
  asked for…"
- `:122-124` — "Requirements.md **has since been rewritten**… **This ADR is therefore the sole record
  of why that surface is absent**"
- `:205-206` — "**Earlier drafts of this ADR** exposed a scheduler-carrying provider member…"
- `:289-293` — "**A draft of this ADR proposed** a provider member handing back a channel backed by
  an `InMemoryScheduler`… We dropped it with FR-3"

The "sole record" claim is contradicted by `decision-log.md`'s section "Retired: FR-3, FR-1(4),
NFR-4, AC-3 — scheduler-delegation testing", which gives the same two reasons in the same order. Two
documents now assert exclusive ownership of one rationale, and readers get no rule for which governs
if they drift.

**Evidence**: `docs/adr/0066-…:115`, `:122-126`, `:205-208`, `:289-293`; `decision-log.md:76-93`.

**Recommendation**: Rewrite the four passages as present-tense assertions ("The provider exposes no
scheduler-carrying member, because…"), delete the "sole record" sentence, and let `decision-log.md`
own the withdrawal history.

---

### 9. README.md contradicts OOS-1 and the withdrawn scheduler design, and its status checklist is stale (Score: 62)

The spec's own front page still describes the pre-ADR-0066 world and states as a goal the exact
thing OOS-1 explicitly rejects. It is the first document a reader of this spec directory opens.

**Evidence**: `README.md:21-22` — "…and **(optionally) reintroduce genuinely *native* behaviour as
distinct `HasNative...` flags**" vs OOS-1: "Re-introducing any `HasNative*` capability flag into the
suite… is explicitly rejected." `README.md:47-48` — "Provider interface extension: …
**InMemoryScheduler wired to the producer for these tests**" vs ADR 0066's decision that there is no
scheduler-carrying provider member at all. `README.md:26-30` — Requirements marked "✅ APPROVED
2026-07-18" and Design "(ADR `0066`) — `/spec:design` (**not started**)", though ADRs 0066 and 0067
exist and requirements has since been through review rounds 4–6.

**Recommendation**: Update the README summary to the current scope (twelve transports, no scheduler
member, no `HasNative*` flags, FR-19 and FR-20) and refresh the status checklist.

---

### 10. AC-23's "corresponding test project" has no mapping rule and is not mechanically checkable (Score: 60)

AC-23 is written as an enumeration assertion: "*when* `src/Paramore.Brighter.MessagingGateway.*` is
enumerated, *then* each of the twelve gateway projects has a **corresponding** test project…". Five
of the twelve pairs do not correspond by name, so "corresponding" cannot be evaluated without a
mapping the document does not supply.

Verified pairings: `MessagingGateway.AWSSQS` → `Paramore.Brighter.AWS.Tests`;
`MessagingGateway.AWSSQS.V4` → `Paramore.Brighter.AWS.V4.Tests`; `MessagingGateway.GcpPubSub` →
`Paramore.Brighter.Gcp.Tests`; `MessagingGateway.MsSql` → `Paramore.Brighter.MSSQL.Tests`;
`MessagingGateway.Postgres` → `Paramore.Brighter.PostgresSQL.Tests`. A further trap:
`tests/Paramore.Brighter.Azure.Tests` exists alongside `tests/Paramore.Brighter.AzureServiceBus.Tests`,
so a naive prefix match picks the wrong project.

**Evidence**: `requirements.md:418-420`; directory listings of `src/Paramore.Brighter.MessagingGateway.*`
and `tests/Paramore.Brighter.*.Tests`.

**Recommendation**: Replace the enumeration with the explicit twelve-row gateway→test-project
mapping, or restate AC-23 in terms the generator actually consumes ("twelve `test-configuration.json`
files declare a `MessagingGateway`/`MessagingGateways` section, one per gateway project, per the
mapping in FR-13").

---

### 11. Requirements.md records no known non-conformances, though ADR 0067 seeds two into the ledger before any generation run (Score: 45)

ADR 0067 identifies, ahead of implementation, that all four GCP configurations and RocketMQ will
fail the mechanism-agnostic FR-2, and that RocketMQ's is blocked on an upstream client release and
is "a likely signed-off `Deferred` row". FR-13's deferral machinery covers this, so it is not a
contradiction — but the requirement that establishes the twelve-transport target set records nothing
about the five configurations already known to fail it, which understates the spec's size for a
reader of requirements.md alone.

**Evidence**: `docs/adr/0067-…:66-75`; no corresponding text in `requirements.md` FR-2, FR-13 or
AC-13.

**Recommendation**: Optional — a sentence in FR-13 noting that non-conformances identified before the
rollout begins are seeded into the ledger rather than discovered at the flip.

---

### Round 7 summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 5 |
| 50-69 (Medium) | 5 |
| 0-49 (Low) | 1 |

**Total findings**: 11
**Findings at or above threshold (60)**: 10

---

## Round 6 (2026-07-19) — re-review after round-5 remediation

**Threshold**: 60
**Verdict**: NEEDS WORK — 4 findings at or above threshold 60. **No Critical findings** (first round
with none).

Round 5's remediation was substantively correct: every load-bearing factual claim in the new
material was independently verified against source and holds exactly (see "Verified factual claims"
below). What remained was **follow-through** — two edits that changed requirements.md without
updating ADR 0066 in step, an exemption written at the wrong granularity, and deletion ACs that stop
at the template while ignoring the generated copies already in the tree.

### 1. ADR 0066 still says "three templates" and carries a mangled sentence, contradicting FR-12's "Two templates" (Score: 72)

Round-5 remediation removed the exhaustion template from ADR 0066's FR-12 exemption list but left the
count at "three" and left a dangling fragment where the third item was deleted. The sentence is not
parseable, and the two documents state different exemption-set sizes for the same prohibition.

**Evidence**: `docs/adr/0066-...:366-370` — "…because **three** templates legitimately requeue with no
delay: the plain-requeue template (…, ungated by FR-10), **the** / **and the** zero/null-boundary
template required by FR-15." vs requirements.md FR-12: "**Two** templates legitimately call `Requeue`
with no delay". *(Confirmed by direct read.)*

**Recommendation**: "three" → "two"; delete the orphaned "the".

### 2. AC-20 grants its NFR-2 exemption per-AC, but the exempted ACs each also contain positive arrival assertions — and AC-16 is directly contradicted (Score: 70)

AC-20's rescoping fixed the unsatisfiability but is written at **AC granularity rather than assertion
granularity**, so it exempts the *whole* of four ACs whose principal assertions are exactly the flaky
positive arrivals NFR-2 exists to protect:

- **AC-18**: "the DLQ consumer **receives the message**" — a DLQ arrival, the most propagation-
  sensitive read in the suite. Exempt as written.
- **AC-5**: "the invalid-channel consumer **receives the message**" — same.
- **AC-9**: "*when* a receive is attempted **after the delay**, *then* it **yields the message**".
- **AC-16** is worse than exempt, it is *contradicted*: AC-16 requires the message be "receivable
  again **within the plain-requeue bounded retry loop**", while AC-20 says AC-16 uses "a single
  bounded receive". AC-16's condition ("no delay window elapsing") is an *elapsed-time* assertion,
  not an "a message has not arrived" assertion, so it does not fit AC-20's stated category at all.

Two developers will implement AC-18's DLQ read differently — one retried, one single-shot.

**Recommendation**: Exempt *assertions*, not ACs — name the negative half of each and state the
positive half stays inside a bounded retry loop. The exemption list is otherwise **complete**: every
AC was checked and no other negative or timing assertion needs listing.

### 3. FR-12/AC-12 and FR-19/AC-22 delete templates but nothing requires deleting the 38 already-checked-in generated copies (Score: 68)

Both deletion ACs assert only that the `.liquid` template is absent. The generated `.cs` output is
checked into the repo and deleting a template does not delete it: **32** copies of the exhaustion
test and **6** of the `with_delay` test. *(Counts confirmed by `find`.)*

For the exhaustion test this is transitively saved by AC-1 (all 32 pass `setupDeadLetterQueue: true`,
which FR-1(6) removes). For the **`with_delay` test there is no such rescue** — its 6 generated copies
reference nothing FR-1(6) removes, so an implementation can satisfy AC-12 with all 6 still in the
tree, still compiling, still calling `Requeue` with no delay: the exact defect FR-12 exists to
eliminate.

**Recommendation**: Extend both ACs with "…and no generated copy of it remains under any
`tests/Paramore.Brighter.*.Tests/**/Generated/` directory."

### 4. OOS-6 omits Azure Service Bus, and ADR 0067 schedules Azure/ASB as in-scope rollout work (Score: 68)

OOS-6 excludes "transports that have a gateway but declare no `test-configuration.json` (RMQ.Sync,
MQTT)". `src/Paramore.Brighter.MessagingGateway.AzureServiceBus` and
`tests/Paramore.Brighter.AzureServiceBus.Tests` both exist, and the latter has **no**
`test-configuration.json` *(confirmed)* — it is in exactly that class and is not named, though the
enumeration reads as exhaustive.

Not cosmetic: ADR 0067 puts Azure/ASB **inside** the rollout in three places — stage (iii) "known-gap
transports: GCP …, **Azure/ASB (not yet in generator target set)**" (`0067:131`), "plus Azure/ASB once
added" in the ledger (`~148`), and Implementation Approach step 4 (`226`), which explicitly calls
adding a `test-configuration.json` and provider a prerequisite. That is precisely the onboarding
OOS-6 declares separate work, so the rollout ADR cannot complete without doing something
requirements.md places out of scope.

**Recommendation**: Add AzureServiceBus to OOS-6 and strike Azure/ASB from ADR 0067's stage (iii),
ledger and step 4 — or promote it to a numbered FR if genuinely wanted.

### 5. FR-19's "for the same reason FR-12 deletes the `with_delay` template" mischaracterises FR-12 (Score: 40)

FR-12's stated reason is that the template is *defective* (no `timeout` argument, no retry loop).
FR-19's reason is different and better: the behaviour is not a channel-surface obligation. Asserting
they are the same weakens FR-19's own correct argument, which the following paragraphs then make
properly.

**Recommendation**: "Like FR-12's `with_delay` template it is unsalvageable, though for a different
reason: it does not assert a channel-surface obligation."

### Verified factual claims (no finding)

Every load-bearing claim in the new material checks out:

- **`HandledCountReached`** — exactly three hits repo-wide: the definition at `Message.cs:161` and two
  callers, `Reactor.cs:498` and `Proactor.cs:504`. FR-19's "no other caller" is exact, and FR-19
  correctly names **both** pumps (round 5 cited only Reactor).
- **`Channel.Requeue` counts nothing** — `Channel.cs:174-176` / `ChannelAsync.cs:181-183` are pure
  pass-throughs.
- **AWS pairs `requeueCount: 3` with a `RedrivePolicy`** — `SqsStandardMessageGatewayProvider.cs:52-67`.
- **"Kafka ×2, MSSQL and PostgreSQL — four failures"** — exact; those are precisely the four
  configurations where a gate is `false` today, the other 16 already generate it.
- **No coverage loss from FR-19** — the template asserts only plain requeue N times (covered by the
  FR-10-ungated plain-requeue template) and eventual DLQ arrival (FR-4). Nothing channel-owned lost.
- **FR-1(6)'s twenty providers** — exactly 20 `*MessageGatewayProvider.cs` files, **all 20** declaring
  `bool setupDeadLetterQueue = false`; both interface templates declare it; the exhaustion template is
  the only template passing the argument.
- **FR-13's target set** — 14 config files, exactly 9 with a gateway section, exactly the nine named;
  the five excluded are correct; 4+4+4+2+2+1+1+1+1 = 20.
- **ADR 0067's canonical enumeration is correct, not accidentally so** — matches the requirements.md
  terminology list character-for-character, and FR-19 is correctly *not* a column (a deletion, not a
  behaviour).
- **No identifier collisions**; ADR 0066's surviving FR-18/AC-19 references occur only in the
  paragraph explicitly labelling them retired.
- **Whole-document mapping** — every FR (1,2,4-17,19) maps to an AC and every AC (1,2,4-18,20-22) to
  an FR or NFR; the gaps match the retired-identifiers note exactly.

### Resolved from round 5

- **R5-F1** — FR-18/AC-19 withdrawn and listed as retired; FR-19 + AC-22 replace them, and every
  factual claim in FR-19's rationale verifies.
- **R5-F2/F3/F4** — moot; they concerned FR-18's internals, which are gone.
- **R5-F5** — AC-20's unsatisfiability is gone (remaining granularity defect is finding 2).
- **R5-F6** — ADR 0067's target set now defined by the gateway *section*, with a note that a
  `HasSupportTo*`-keyed test would erase itself; canonical enumeration matches.
- **R5-F7** — FR-1(6) added, AC-1 extended; the twenty-provider claim verified exact.
- **R5-F8** — FR-13 keys on the gateway section and names the five excluded projects; verified.

### Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 2 |
| 50-69 (Medium) | 2 |
| 0-49 (Low) | 1 |

**Total findings**: 5 · **At or above threshold (60)**: 4

---

## Round 5 (2026-07-19) — re-review after round-4 remediation

**Threshold**: 60
**Verdict**: NEEDS WORK — 7 findings at or above threshold 60.

Round 4's eleven findings were all remediated (see "Resolved from round 4" below). Round 5's
findings cluster on the **new** material — FR-18 in particular, which was added in response to
round-4 finding 2 and turns out to rest on a false premise.

### 1. FR-18 adopts a message-pump behaviour as a universal channel-surface obligation; its stated premise is false (Score: 95)

FR-18 asserts: "when a message is requeued `Subscription.RequeueCount` times, the next requeue does
not return it to the channel … `RequeueCount` is a property of `Subscription` for every transport,
so this is a universal obligation." The inference is a non-sequitur and the behaviour is not a
channel obligation at all:

- The **only** code in Brighter that compares delivery count to `RequeueCount` is the message pump.
  `Reactor.RequeueMessage` calls `message.Header.UpdateHandledCount()`, then
  `if (message.HandledCountReached(RequeueCount))` → `RejectMessage(..., DeliveryError, "Handle
  count of messages reached; rejecting at limit")`, further gated on `RequeueCount != -1`.
- `Channel.Requeue` / `ChannelAsync.RequeueAsync` do **no** counting — they forward straight to
  `_messageConsumer.Requeue(message, timeOut)`. No gateway consumer compares `HandledCount` to
  `RequeueCount`; the gateways only serialise `HandledCount` as a wire header.

At the channel surface — the only surface this suite may drive (Objective and Test Boundary, OOS-5)
— exhaustion→DLQ can only happen if the *transport* natively counts deliveries and redrives. So
FR-18 is either (a) a pump test, barred by OOS-5, or (b) a native-mechanism test, barred by NFR-3
and OOS-1 — the exact defect that got FR-3 withdrawn and folded into FR-2.

**Confirmed independently against source.** `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/SqsStandardMessageGatewayProvider.cs:52-67`
builds the DLQ subscription with `redrivePolicy: new RedrivePolicy(deadLetterChannelName, 3)`
alongside `requeueCount: 3`. The template passes on AWS because **SQS natively redrives after
maxReceiveCount** — not because of anything Brighter does at the channel. Kafka, Redis, MSSQL,
PostgreSQL and GCP have no such native counter, and FR-13 would classify every one of those failures
as "a defect in that transport's gateway … in scope to fix".

**Evidence**: requirements.md FR-18; `src/Paramore.Brighter.ServiceActivator/Reactor.cs:494-502`;
`src/Paramore.Brighter/Channel.cs:174-177` (pure pass-through);
`src/Paramore.Brighter/Message.cs:161` — `HandledCountReached` has exactly one caller, in the pump.

**Recommendation**: Withdraw FR-18/AC-19 as a canonical behaviour and dispose of the ungated
template another way — delete it (FR-12-style, the behaviour it asserts being pump-owned), or make
it an OOS entry naming the transports whose native redrive it happens to exercise.

### 2. FR-18/AC-19 never bound `RequeueCount`, and its default of `-1` makes the test vacuous (Score: 75)

`Subscription` defaults `requeueCount` to `-1` (`Subscription.cs:203,288`). The template loops
`for (var i = 0; i < _subscription.RequeueCount; i++)` — at the default the body **never executes**,
the message is never received, and the following `Assert.Equal(MessageType.MT_NONE, ...)` fails
because the sent message is still there. AC-19 is false at the default. Nothing in FR-18, AC-19 or
FR-1 requires the provider to set a positive, bounded `RequeueCount`; two developers will pick
different values or none. The pump also treats `-1` as "discard disabled", a boundary the
requirement never mentions.

**Recommendation**: If FR-18 survives finding 1, require in FR-1 that the provider can create a
subscription with a caller-specified positive `RequeueCount`, and pin a concrete value in AC-19
(e.g. `RequeueCount: 2` → two requeues, third receive `MT_NONE`).

### 3. FR-18's body and its own example disagree by one requeue (Score: 70)

Body: "when a message is requeued `RequeueCount` times, **the next requeue** does not return it to
the channel: a subsequent receive yields `MT_NONE`" — that describes `RequeueCount + 1` calls, and is
internally incoherent, because performing "the next requeue" requires first receiving the message,
which the same sentence says yields `MT_NONE`. The example and AC-19 say `RequeueCount` calls, as
does the template. This off-by-one is exactly the boundary the requirement exists to pin.

**Recommendation**: Rewrite the body to match: "after the `RequeueCount`-th requeue the message is no
longer returned to the channel".

### 4. FR-18's prescribed NFR-2 fix names the wrong receive (Score: 65)

FR-18 says the template needs "its **final receive** wrapped in the bounded retry loop NFR-2
requires". The final receive is the *negative* one (`Assert.Equal(MT_NONE, ...)`); wrapping it in a
retry loop inverts its meaning, since a retry loop retries **until a message arrives**. The genuinely
timing-dependent read is the one after it — `GetMessageFromDeadLetterQueue(_subscription)`, a DLQ
redrive that can lag arbitrarily and is read exactly once. FR-18's claim that the two named changes
suffice is wrong on the one that matters.

Third, unmentioned gap: the Reactor variant has a `DelayBetweenReceiveMessageInMilliseconds`
`Thread.Sleep` inside the loop; the **Proactor variant has no equivalent**, so the two are not
parity-equal today (FR-14).

### 5. AC-20 (NFR-2) contradicts AC-9, AC-16 and AC-19, which all require unretried negative receives (Score: 65)

AC-20 requires that "**every** timing-dependent assertion sits inside a bounded receive-retry loop".
Four ACs mandate assertions that are timing-dependent *and* must not be retried: AC-9 ("a receive
attempted **immediately** yields `MT_NONE`" — a retry loop would poll past the delay window), AC-16
("with no delay window elapsing"), AC-19 ("the next receive yields `MT_NONE`") and AC-5 ("does not
appear on any DLQ"). As written AC-20 is unsatisfiable alongside them.

**Recommendation**: Scope AC-20 to *positive* assertions — "every assertion that a message **arrives**
sits inside a bounded receive-retry loop; assertions that a message has **not** arrived use a single
bounded receive after the stated window, and are exempt."

### 6. ADR 0067's canonical set omits FR-18, and its target-set definition is now incompatible with FR-13 (Score: 65)

1. ADR 0067 enumerates the canonical set as "FR-2, FR-4…FR-9, FR-15, FR-16, FR-17" — requirements.md
   now includes FR-18. ADR 0066's Consequences was amended for FR-18; ADR 0067's enumeration and
   ledger were not, so the rollout ADR is scoped one behaviour smaller than the requirement it
   governs — and FR-18's template is precisely the one that newly ungates.
2. ADR 0067 defines the target set as transports declaring a `test-configuration.json` **with
   `HasSupportTo*` keys**. FR-10/FR-11 delete those keys, so the ADR's membership test erases itself
   the moment the change lands.

Counts verified correct in both documents (AWS 4, AWS.V4 4, GCP 4, Kafka 2, RMQ.Async 2,
MSSQL/PostgreSQL/Redis/RocketMQ 1 each = 20). No identifier collisions: FR-18, AC-19, AC-20, AC-21
and OOS-6 are unused by either ADR.

### 7. Removing `bool setupDeadLetterQueue` is a breaking change to all twenty providers, stated only in a subclause of FR-18 (Score: 60)

ADR 0066 records it accurately as a breaking change requiring all twenty `*MessageGatewayProvider.cs`
implementations plus both interface templates to migrate. **requirements.md never requires it.** FR-1
says only that the interfaces "MUST be extended"; FR-1(1)/(2) are satisfiable by *adding* routing-key
parameters and leaving the bool. The sole statement of the replacement is a phrase inside FR-18 —
which finding 1 recommends withdrawing, taking the only statement of a twenty-file breaking change
with it. AC-1 does not verify it.

**Recommendation**: Add an FR-1 sub-item requiring removal of `bool setupDeadLetterQueue`, and extend
AC-1 to assert its absence.

### 8. FR-13's target-set definition admits five projects it means to exclude (Score: 45)

"one declared in a `tests/Paramore.Brighter.*.Tests/test-configuration.json`" — fourteen such files
exist; five (DynamoDB, DynamoDB.V4, MongoDb, MySQL, Sqlite) declare only `Outbox`/`Outboxes` and no
gateway. The named list of nine and the count of twenty are correct, so intent is clear, but the
literal wording is not the test the sentence claims.

**Recommendation**: "…declared under the `MessagingGateway` or `MessagingGateways` section of a
`tests/Paramore.Brighter.*.Tests/test-configuration.json`."

### Resolved from round 4

- **F1** — FR-12/AC-12 now scope the prohibition to delayed-requeue templates and name the three
  exemptions; ADR 0066:358-366 matches. Verified: exactly three templates call `Requeue` with no
  delay today.
- **F2** — the exhaustion template is now accounted for as FR-18/AC-19 and ADR 0066's Consequences.
  *Accounted for, but findings 1-4 dispute whether the disposition chosen is correct.*
- **F3** — ADR 0066:115-123 now says the identifiers are "retired identifiers … left as permanent
  gaps" and "this ADR is therefore the sole record"; the dangling FR-3 pointer is gone.
- **F4** — FR-13 defines the target set; OOS-6 excludes RMQ.Sync/MQTT (verified: both exist, neither
  has a `test-configuration.json`). Wording nit remains as finding 8; ADR divergence as finding 6.
- **F5** — AC-20/AC-21 added. *AC-20 introduces a new contradiction — finding 5.*
- **F6** — FR-1 regained its worked example incl. the Kafka/Redis key-name pair; ADR 0066's
  back-reference resolves again.
- **F7** — AC-8 restores `"MT_COMMAND"` and `"DeliveryError"`.
- **F8** — OOS-2's stray MUST is gone.
- **F9** — FR-9 states the existing template satisfies it once ungated.
- **F10** — FR-7's heading is "acknowledge and log" with a clause explaining the suffix.
- **F11** — NFR-3's title is "(No mechanism assertions)" with a pointer to FR-10/OOS-1.

Re-verified with no finding: every FR maps to an AC and every AC to an FR; the Coverage
Reconciliation table carries the FR-18 row; the retired-identifier note is accurate and none reused;
ADR 0066's AC-11/AC-12/AC-18/C-1/C-2/NFR-2 citations all still resolve correctly.

### Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 1 |
| 70-89 (High) | 2 |
| 50-69 (Medium) | 4 |
| 0-49 (Low) | 1 |

**Total findings**: 8 · **At or above threshold (60)**: 7

---

## Round 4 (2026-07-19) — first adversarial review of the rewritten document

**Threshold**: 60
**Verdict**: NEEDS WORK — 8 findings at or above threshold 60.

Context: requirements.md was rewritten from scratch on 2026-07-19 to strip four rounds of inline
amendment scar tissue, on the principle *requirements say WHAT and how it is verified; ADRs say WHY
and HOW*. Identifiers were deliberately **not** renumbered — FR-3, FR-1(4), NFR-4 and AC-3 are
retired gaps — to preserve ~130 cross-references in ADRs 0066/0067.

Findings 1, 2, 8 and 10 are **pre-existing defects the rewrite inherited**; findings 3, 6, 7 and 9
are **losses caused by the rewrite itself**.

### 1. FR-12's "no template may call `Requeue` without a non-null `TimeSpan`" contradicts FR-10 and FR-15 (Score: 95)

FR-12 closes with an absolute prohibition that two other requirements in the same document violate,
and that the current template set violates today in a template FR-10 explicitly preserves.

- **FR-10** states "The existing plain-requeue template becomes ungated and generates for every
  transport". That template — `When_requeuing_a_failed_message_should_receive_message_again.cs.liquid`
  — calls `_channel.Requeue(received);` with **no** argument (Reactor line 61; Proactor
  `await _channel.RequeueAsync(received);` line 66). So after FR-12's deletion, a template calling
  `Requeue` without a `TimeSpan` still exists, by design.
- **FR-15** *requires* a test that calls `channel.Requeue(M, null)`. A template implementing FR-15
  necessarily passes a **null** `TimeSpan`, directly contradicting "without a non-null `TimeSpan`".

AC-12 restates the same rule as an inspection assertion, so it is literally unsatisfiable as
written. ADR 0066 reproduces the contradiction verbatim and even names the offending template in the
same breath.

**Evidence**: requirements.md FR-12 vs FR-15;
`tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Reactor/When_requeuing_a_failed_message_should_receive_message_again.cs.liquid:61`;
`docs/adr/0066-conformance-test-provider-and-ungating.md:358-362`.

**Recommendation**: Narrow the rule to what it means — "no *delayed*-requeue template may call
`Requeue`/`RequeueAsync` without a non-null `TimeSpan`" — and exempt the plain-requeue template
(FR-10) and the FR-15 zero/null-boundary template. Amend AC-12 and ADR 0066:358-362 in step.

### 2. The existing `requeuing_a_message_too_many_times_should_move_to_dead_letter_queue` template is silently ungated and never addressed (Score: 90)

Retiring `HasSupportToDeadLetterQueue` and `HasSupportToRequeue` (FR-10) ungates more than the
plain-requeue template. `When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue.cs.liquid`
matches **both** the `dead_letter_queue` and `requeuing` gate substrings, so it is doubly gated
today and becomes fully ungated after FR-10 — generating for Kafka, PostgreSQL, MSSQL and every
other configuration where it has never run. requirements.md never mentions it: not in FR-10, not in
FR-12, not in the canonical FR set, not in Out of Scope.

Not cosmetic. It asserts a *requeue-count exhaustion → DLQ* behaviour that is not among the
canonical behaviours, and it also calls `_channel.Requeue(received)` with no delay argument
(Reactor:62), so it collides with FR-12/AC-12 as well. An implementer has no instruction on whether
to keep, fix, or delete it, and each choice materially changes what ships.

**Evidence**:
`tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/{Reactor,Proactor}/When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue.cs.liquid`
(Reactor:59-62); `MessagingGatewayGenerator.SkipTest` gating. *(Independently re-verified.)*

**Recommendation**: Add explicit disposition — a numbered FR covering requeue-count-exhaustion as a
canonical behaviour, an OOS entry, or an FR-12-style deletion — plus a matching AC.

### 3. ADR 0066 now asserts things about requirements.md that the rewrite made false (Score: 85)

The rewrite deleted the "Design decisions deferred to the ADR" closing paragraph and the inline
amended/withdrawn text for FR-1(4), FR-3, NFR-4 and AC-3. ADR 0066 depends on both, and its
justification for its single "deliberate departure" rests on text that no longer exists:

- "This is within the latitude requirements.md grants (… both **explicitly deferred to this ADR**)"
  — requirements.md no longer defers anything to any ADR. The deleted final paragraph did exactly
  that; it was a delegation of decision authority, not rationale.
- "FR-1(4), FR-3, NFR-4, AC-1 and AC-3 **are amended in requirements.md** to match" — they are not
  amended, they are deleted. AC-1 no longer contains the scheduler-carrying member the ADR says it
  "asked for".
- "see requirements.md FR-3, now folded into FR-2" (ADR line 205) is a dangling pointer.

The direction of authority is now circular: requirements.md says the rationale "is recorded in ADR
0066"; ADR 0066 says the requirements were amended to match it.

**Evidence**: `docs/adr/0066-conformance-test-provider-and-ungating.md:115-123`, `:205`;
requirements.md closing note.

**Recommendation**: Either restore a short "decisions delegated to ADR 0066/0067" line in
requirements.md (a normative delegation, not rationale), or amend ADR 0066:115-123 and :205 to say
the identifiers were *retired* and the ADR is now the sole record.

### 4. FR-13's target set — "every gateway configuration the generator targets" — is never defined (Score: 80)

FR-13 and AC-13 turn on a phrase the document never resolves. The Problem Statement says "every
transport"; FR-13 says "every gateway configuration the generator targets"; neither says what
determines membership.

`RMQ.Sync` and `MQTT` are real Brighter transports with gateways but have **no**
`test-configuration.json`, so they are not generator targets — yet Problem Statement item 3 cites
`RMQ.Sync` by name as having a "rich" hand-written suite, which reads as though it is in the
picture. The rewrite correctly dropped the false Azure/ASB claim but replaced it with silence rather
than a definition. ADR 0067 pins the set precisely (nine test projects, ~20 configurations,
`0067:34-37`) — verified correct — but the requirement does not.

**Evidence**: requirements.md FR-13, AC-13; `tests/Paramore.Brighter.*.Tests/test-configuration.json`
returns 14 files, 9 carrying messaging-gateway `HasSupportTo*` keys; no such file for RMQ.Sync or
MQTT.

**Recommendation**: Define the target set in FR-13 or Constraints — "a gateway configuration the
generator targets is one declared in a `tests/Paramore.Brighter.*.Tests/test-configuration.json`;
today nine projects, ~20 configurations" — and add an OOS entry that onboarding transports without
one (RMQ.Sync, MQTT) is out of scope.

### 5. NFR-2 and NFR-3 have no acceptance criteria (Score: 75)

Every FR maps to an AC, and NFR-1 maps to AC-15. NFR-2 (bounded retry loops) and NFR-3 (no mechanism
assertions) have none. NFR-3 is the most load-bearing constraint in the document — OOS-1 rests on
it, it justified retiring FR-3, and ADR 0066 cites it five times — yet nothing states how a reviewer
verifies the shipped templates comply.

**Evidence**: requirements.md AC-1 … AC-18; only AC-15 references an NFR.

**Recommendation**: Add ACs — e.g. NFR-3: "*Given* the generated templates, *when* their sources are
inspected, *then* no assertion references a scheduler, a native-delay API, a redrive policy or a
DLX"; NFR-2: "*then* every timing-dependent template uses a bounded receive-retry loop and none uses
a bare sleep-then-single-receive".

### 6. FR-1 has no concrete example, and FR-1(5) lost the one it had (Score: 70)

FR-1 is the only FR with no worked example, and the hardest to get right — it fans out to ~20
hand-written provider implementations. The rewrite dropped the example that made FR-1(5)
unambiguous: "`provider.RejectionMetadataKeys.OriginalTopic` returns `"OriginalTopic"` for Kafka and
`"originalTopic"` for Redis/SQS". That is a concrete input/output pair, not rationale. Without it,
"obtain the transport's rejection-metadata key names" does not convey that the divergence is
PascalCase-vs-camelCase per transport; C-2 asserts the names vary but gives no instance.

**Evidence**: requirements.md FR-1(5), AC-1; deleted example at
`git show HEAD:...requirements.md` lines 132-136; ADR 0066:272 ("This is exactly the FR-1(5)
example") now points at an example that no longer exists.

**Recommendation**: Restore one concrete example under FR-1 — the extended `CreateSubscription` call
with both routing keys, and the Kafka-vs-Redis key-name pair. ADR 0066:272's back-reference is
currently dangling.

### 7. FR-8 / AC-8 lost the concrete expected values for two of the five semantic fields (Score: 65)

Three of AC-8's five fields have a stated expected value (original topic = data topic; rejection
message = passed description; timestamp = parseable ISO-8601 within the last minute). **Original
message type** and **rejection reason** have none — "present and correct" is not directly
assertable. The previous version supplied `"MT_COMMAND"` and `"DeliveryError"`, which are concrete
expected outputs rather than rationale. FR-5, FR-6 and AC-18 *do* pin the rejection-reason string
for their cases, which makes the omission look like an oversight.

**Evidence**: requirements.md FR-8, AC-8; previous version AC-8 at `git show HEAD:...` lines 421-425.

**Recommendation**: Restore `"MT_COMMAND"` (given a command-typed test message) and `"DeliveryError"`
for the DeliveryError arm.

### 8. OOS-2 carries a MUST obligation with no owner and no acceptance criterion (Score: 60)

OOS-2 ends "MUST be captured as a separate issue". An out-of-scope section is the wrong place for a
live obligation, and no AC verifies it — unlike FR-13's deferral obligation, which AC-13 does. The
suite can ship fully conformant with OOS-2 unmet and nothing detects it. Pre-existing, not a rewrite
regression, but it survives into a document that otherwise maps obligations to ACs consistently.

**Evidence**: requirements.md OOS-2; contrast AC-13.

**Recommendation**: Move the obligation into a numbered FR with a matching AC, or drop the MUST and
record it as a task in tasks.md.

### 9. FR-9 no longer says whether it reuses the existing delayed-send template (Score: 55)

FR-9 describes exactly what
`When_reading_a_delayed_message_via_the_messaging_gateway_should_delay_delivery.cs.liquid` already
does — a template FR-10 ungates. The previous version said so explicitly; the rewrite dropped it. An
implementer cannot tell whether FR-9 means "keep and ungate the existing template" or "write a new
canonical one", and writing a new one would duplicate coverage.

**Evidence**: requirements.md FR-9; the template above; previous version lines 228-231.

**Recommendation**: State in FR-9 that the existing `delayed_message` template satisfies it once
ungated, as FR-10 already does for plain requeue.

### 10. FR-7's title and NFR-1's naming convention disagree (Score: 45)

FR-7 is titled "acknowledge and **continue**", but NFR-1 mandates the file name
`..._should_acknowledge_and_log`, as does the Coverage Reconciliation table and the hand-written
Kafka test. FR-7's body never mentions logging.

**Evidence**: requirements.md FR-7 heading vs NFR-1 third bullet;
`tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Reactor/When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log.cs`.

**Recommendation**: Align the heading, or note that `_and_log` is retained for naming continuity
while the assertion is acknowledge-and-continue.

### 11. NFR-3's title promises something its body does not cover (Score: 40)

NFR-3 is titled "(No mechanism assertions, **no new gates**)" but its body says nothing about gates
— that lives in OOS-1 and FR-10.

**Evidence**: requirements.md NFR-3.

**Recommendation**: Drop "no new gates" from the title, or add a clause cross-referencing OOS-1.

### Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 2 |
| 70-89 (High) | 4 |
| 50-69 (Medium) | 3 |
| 0-49 (Low) | 2 |

**Total findings**: 11 · **At or above threshold (60)**: 8

### Verification notes (no findings — these checks passed)

Every factual claim checked is accurate:

- `SkipTest` (`MessagingGatewayGenerator.cs:112-158`) gates on all three named properties.
- The `with_delay` template calls `_channel.Requeue(received);` with no delay, after `SendWithDelay`
  + `Thread.Sleep(6s)`, and lacks a retry loop.
- Gate values confirmed: Kafka all three `false` in both configurations; PostgreSQL DLQ+delayed
  `false`; MSSQL DLQ `false`; AWS `SqsStandard` and RocketMQ the only
  `HasSupportToDelayedMessages: true`.
- "Three of roughly twenty gateway configurations" is exact: `SqsStandard` in both AWS and AWS.V4
  (2) plus RocketMQ (1) = 3; the nine gateway projects declare exactly 20 configurations.
- GCP has no hand-written reject/requeue/nack/delay coverage.
- All nine Kafka tests in the Coverage Reconciliation table exist with the claimed Reactor/Proactor
  split; the metadata test is Reactor-only; the Nack two-message variant exists as
  `When_acking_later_message_it_should_not_skip_nacked_message`.
- The Azure/ASB correction is right — Azure was never a generator target.
- ADR ID cross-references resolve: every ID cited by ADRs 0066/0067 either still exists with
  unchanged meaning or is a retired ID the ADR treats as withdrawn — except finding 3.
- No FR or AC sneaks a mechanism assertion back past NFR-3.

---

## Revision 3 (2026-07-18) — coverage expansion from spec-owner feedback

Not a review pass — direct changes requested by the spec owner after revision 2:
- **Nack brought into scope.** OOS-3 (Nack out-of-scope) removed; new **FR-16/AC-17** — `Nack`/
  `NackAsync` releases a message for redelivery (sync + async), grounded in Kafka
  `When_nacking_a_message_it_should_be_redelivered` (+ async + two-message variant).
- **Coverage reconciled against the Kafka surface area** (spec owner: "we only have a single test
  per path?"). Enumerated the full `Paramore.Brighter.Kafka.Tests/MessagingGateway` suite; found a
  behavioural path with no FR → new **FR-17/AC-18** (reject with `RejectionReason.None`/unknown →
  DLQ, grounded in `..._unknown_reason_should_send_to_dlq`). Fallback-ladder definition extended
  (`None`/unspecified → DLQ). Added a **Coverage Reconciliation** section mapping every Kafka
  reject/requeue/Nack test to an FR and documenting deliberate exclusions (offset/header/error/
  wiring unit tests) as transport-internal / transitively covered. Noted the generated suite is
  *more* complete than Kafka's own (FR-14 parity adds async requeue-via-scheduler and async
  metadata, which Kafka lacks).
- **`MUST` trim** in the Objective/Test Boundary "Unit under test" bullet (spec-owner edit) — the
  channel-not-pump boundary is still stated in the section intro and OOS-5.

Document now has FR-1…FR-17 (each mapped to an AC; AC-15↔NFR-1) and AC-1…AC-18.

## Revision 2 (2026-07-18)

**Threshold**: 60
**Review verdict (as reviewed)**: NEEDS WORK — 3 findings ≥ 60
**Post-review status**: ALL 5 findings addressed in the current `requirements.md` (see resolutions below).

### Prior-finding verification (revision 1 → revision 2)
All 8 revision-1 findings confirmed RESOLVED by the re-review (metadata keys via provider,
producer `Scheduler`, FR-12 MAY, zero/null-delay FR-15, FR-13 auditable deferral, gateway-not-pump
Objective section, Nack OOS, RejectionReason wording).

### Revision-2 findings and resolutions

1. **Third gate `HasSupportToRequeue` not retired — contradicts FR-13 "none gated away" (was 74).**
   `SkipTest` gates `requeuing` on `HasSupportToRequeue` (`MessagingGatewayGenerator.cs:145`);
   Kafka declares it `false`, so canonical/plain requeue templates would silently skip on Kafka.
   **Resolution (user decision: retire it too):** FR-10 now retires all three gates and makes plain
   requeue universal; FR-11 adds Kafka's `HasSupportToRequeue:false` to the configs corrected;
   AC-10 verifies all three flags absent from `SkipTest`, `MessagingGatewayConfiguration`, and every
   `test-configuration.json`; problem statement and proposed solution updated.

2. **AC-12 replace-branch cited a nonexistent "AC-10 sibling check" — dangling reference (was 72).**
   **Resolution:** AC-12 replace-branch now inlines the verification ("a template-source inspection
   confirms no remaining messaging-gateway template calls `Requeue`/`RequeueAsync` without a
   `TimeSpan` delay argument"); cross-reference removed.

3. **FR-15/AC-16 "producer's scheduler is not engaged" over-specified mechanism, conflicting with
   NFR-3 (was 63).** **Resolution:** FR-15 and AC-16 now assert the observable outcome only (no delay
   window elapses); scheduler non-engagement explicitly not asserted, per NFR-3.

4. **FR-2/FR-3 "grounded in" citations point at consumer-surface tests while the doc mandates the
   channel surface (was 42, Low).** **Resolution:** Additional Context now notes the grounding tests
   drive the raw consumer surface and the canonical templates re-express the behaviour at the
   channel surface, so they adapt rather than copy verbatim.

5. **AC-1 did not explicitly cover FR-1 sub-item 2 (DLQ-only / invalid-only / neither) (was 45,
   Low).** **Resolution:** AC-1 now lists the three channel configurations (validated in use by
   AC-5/AC-6/AC-7).

### Revision-2 summary (as reviewed, before resolutions)

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 2 |
| 50-69 (Medium) | 1 |
| 0-49 (Low) | 2 |

**Total findings**: 5 · **At/above threshold (60)**: 3 · **All now addressed.**

Load-bearing claims verified by the reviewer against code: `IAmAChannelSync` exposes
`Reject`/`Requeue(Message, TimeSpan?)`/`Receive`/`Acknowledge`; `Channel.Reject`/`Requeue` forward
to the consumer which routes to DLQ/invalid from the subscription's producers (channel-surface
mandate realizable); Kafka `HeaderNames` PascalCase vs Redis/SQS camelCase with uniform values
(`MessageType.ToString()`, `RejectionReason.ToString()`); `IAmAMessageProducer.Scheduler` is
`IAmAMessageScheduler?`; `SkipTest` gates `requeuing` on `HasSupportToRequeue`.

---

## Revision 1 (2026-07-17) — superseded

Verdict: NEEDS WORK — 6 findings ≥ 60 (2 High, 5 Medium, 1 Low). Findings: (1) Kafka-specific
metadata keys / C-2 mis-location, (2) InMemoryScheduler vs spy contradiction, (3) FR-12
contradiction, (4) zero/null-delay boundary gap, (5) FR-13 unbounded escape hatch, (6) missing
gateway-not-pump objective + consumer/channel inconsistency, (7) Nack uncovered, (8) RejectionReason
wording. All addressed in revision 2.
