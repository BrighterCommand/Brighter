# Review: design — 0036-universal-transport-conformance-tests

## Round 6 (2026-07-19) — first pass since the gating reversal and the round-8 requirements sweep

**Threshold**: 60
**Verdict**: NEEDS WORK — 3 findings at or above threshold 60. **No Critical findings** (third design
round running clean at 90+).

Findings by round: 6 → 12 → **6**. All four owed round-5 findings confirmed resolved.

### Round 5 remediation check

- **F3 (ledger row granularity / deferred FR-20 onboarding unrepresentable)** — **RESOLVED.** 0067
  now mandates a single placeholder row `<Project> / (not yet declared)` per un-onboarded transport,
  restricts its cells to the `Deferred` form, and states the cleanup gate is evaluated over all
  twelve targeted transports rather than over whichever rows exist.
- **F4 (Scope statements don't collectively own FR-19/FR-20/FR-21/AC-24)** — **RESOLVED.** 0067's
  Scope now claims FR-20/AC-23 and FR-21/AC-24; 0066's already claimed FR-12 and FR-19.
- **F5 (`RejectionMetadataKeys` has no defined home)** — **RESOLVED.** 0066 names
  `Templates/MessagingGateway/Shared/RejectionMetadataKeys.cs.liquid`, emitting once per gateway
  configuration into the parent namespace at `Generated/RejectionMetadataKeys.cs`, listed in Key
  Components, plus the `string.Empty` rule for unstamped fields. Verified plausible against the
  generator: `GenerateAsync` already loops per configuration, and C# namespace nesting makes the
  parent namespace visible from both variant namespaces without a `using`.
- **F9 (parity permits a cell the vocabulary can't express)** — **RESOLVED.** 0067 rules that partial
  parity is a single `Deferred -> #NNNN` cell whose issue names the lagging variant, keeping
  "exactly one of" intact.

**Reference verification**: all substantive code references resolve — `SkipTest` 122/127/132/145;
`MessagingGatewayConfiguration` 91/96/106; `IAmAChannelSync` 64/83; twelve gateway projects; twenty
configurations across nine wired projects; exactly three declaring `HasSupportToDelayedMessages: true`;
18 requeue-true, 16 DLQ∧requeue; generated copies 6/36/6/32 = 80; 40 provider-interface copies; 20
hand-written providers; zero occurrences of `setupDeadLetterQueue` in the 32 exhaustion copies;
`HandledCountReached` callers only `Reactor.cs:498`/`Proactor.cs:504`; all twelve cited ADR slugs
resolve, none cited by bare number.

### 1. Both ADRs instruct the cleanup to delete "the three gate branches"; there are four (Score: 70)

`SkipTest` has **four** branches keyed on the three gates, because `HasSupportToDelayedMessages` is
tested twice — `delayed_message` (122) and `with_delay` (127). requirements.md FR-10(4) and AC-10(c)
both call this out; the ADRs did not follow. 0066 said "loses three branches", "`SkipTest`'s three
gate branches", "the three gate branches below", "delete the three now-unreachable gate branches".
0066 contradicted itself: it enumerates **four** line ranges eight lines below the "three gate
branches" heading. An implementer following it literally leaves the `with_delay` branch behind and
fails AC-10(c).

**Evidence**: `0066:326, :423, :439, :455`; contrast `0066:425-429`. Verified at
`MessagingGatewayGenerator.cs:122,127,132,145`.

**Recommendation**: "the **four** gate branches (keyed on three gates)". "Three properties" and
"three config keys" are correct and stay.

---

### 2. 0067's References entry for 0066 still describes the superseded flip-then-fix sequencing (Score: 65)

0066's reciprocal entry was updated for the reversal; 0067's was not. It still asserts 0066 "retires
the three gates + config keys **in one change**" and frames 0067's job as sequencing "*around* that
flip" — contradicting 0066's Decision, its Step A/B/C staging, and 0067's own Context and step 6.
This is the paragraph a reader consults to learn how the two ADRs relate, and it told them the
opposite of what both bodies decide. A residual "-> flip" also survived in the sequencing summary.

**Evidence**: `0067:467`, `0067:273`; contrast `0066:630` and `0067:26-45, :332-337`.

**Recommendation**: Restate to match 0066:630; replace "-> flip" with "-> terminal cleanup". Uses of
"flip-then-fix" in Alternatives are naming a *rejected* option and are fine.

---

### 3. "Three of the four legacy templates generate today" — it is four of four (Score: 62)

All four generate. Verified by counting checked-in copies: delayed-message 6, plain requeue 36,
`with_delay` 6, exhaustion 32 — every one non-zero, totalling the 80 the deletion sweep depends on.
Both ADRs said "three of the four" and then enumerated four templates with four non-zero counts.
`0067:38` was sharpest: "three of the four generate for 16, 18 and 3 configurations respectively" —
three numbers for four templates, dropping delayed-message entirely. A reader could conclude that
template is inert and needs no generated-copy sweep, contradicting AC-10(b)'s "6 + 36 + 6 + 32".

⚠️ **This error originated in requirements round 8's prose** (which tabulated four non-zero counts
while saying "three of the four") and was propagated into both ADRs, requirements.md, the README and
the decision log during remediation. Corrected in all five.

**Evidence**: `0066:145-147`, `0067:38-39`, `0067:391-394`, `requirements.md:222`.

**Recommendation**: "**all four**" throughout; `0067:38-39` to "16, 18, 3 and 3 configurations".

---

### 4. Placeholder-row cell rule and ledger-seeding step 1 disagree about `Unknown` (Score: 55)

Step 1 seeds every configuration × behaviour to `Unknown`, and step 5 puts the FR-20 onboardings in
the fix phase — so ASB, MQTT and RMQ.Sync begin as unresolved work. But the placeholder rule said
their cells "may hold **only** the `Deferred` form", read literally requiring a signed-off issue for
every behaviour × transport before any onboarding is attempted. The following clause implies
`Unknown` is tolerated; the two were never reconciled at the point of decision.

**Evidence**: `0067:235-238` vs `0067:304-306`.

**Recommendation**: Permit transient `Unknown` on placeholder rows during the fix phase, resolving
only ever to `Deferred` while the row remains a placeholder.

---

### 5. 0066's Context undercounts the AWS-family gate mis-declarations its own Implementation Approach corrects (Score: 45)

Round 5's finding 10 was remediated in the Implementation Approach ("six of the eight AWS-family
configurations") but the Context paragraph kept the old wording naming only AWS. Verified: AWS.V4
declares the identical pattern. Cosmetic, but the two halves of one ADR disagreed.

**Evidence**: `0066:59-60`; contrast `:457-460`.

---

### 6. 0066 attributes the disappearance of the last `setupDeadLetterQueue` caller to FR-19 (Score: 40)

The Consequences bullet said the exhaustion template "FR-19 deletes — so after this change the
parameter has no callers at all". FR-19's deletion is at step C, long after FR-1(6). What removes
the last caller is the mandatory edit of the template's `.liquid` source in the FR-1 change, which
the Decision section correctly insists on. The wording invited exactly the misreading the round-8
sweep closed.

**Evidence**: `0066:547-550`.

---

### Round 6 summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 1 |
| 50-69 (Medium) | 3 |
| 0-49 (Low) | 2 |

**Total findings**: 6
**Findings at or above threshold (60)**: 3

**All six remediated** in the same pass (2026-07-19), including the two below threshold.

---

## Round 5 (2026-07-19) — first design pass since the round-7 requirements remediation

**Threshold**: 60
**Verdict**: NEEDS WORK — 9 findings at or above threshold 60. **No Critical findings.**

This round covered both ADRs against the *post-round-7* requirements.md (FR-21, AC-24, the FR-13
mapping table, the bounded FR-20(3), the reworded AC-13), and deliberately scrutinised the round-7
ADR edits themselves, which had been made an hour earlier and never adversarially read.

**Round-7 remediations verified as landed and not regressed**: all five `~20` phrasings gone from
both ADRs; FR-20 now appears seven times in 0066 including Scope, Key Components and Consequences;
all four narration passages gone; 0067's C-1 restatement carries the FR-20 test-side clause; the
`0038–0043` range replaced by slugs with the number-reuse hazard flagged in-text; the ledger Key
Component cites FR-21. **Prior design-round findings 1–6 all remain fixed.**

The new findings are not regressions from that work — they are pre-existing design gaps the earlier
rounds had not reached, plus two prose errors introduced by the round-7 edits themselves (findings
11 and 12).

### 1. Circular dependency: the flip gate demands ledger evidence the fix phase cannot produce, because the gates are still in force (Score: 78)

ADR 0067's central decision is fix-then-flip: every ledger cell must read `Pass`/`Fixed`/`Deferred`
*before* ADR 0066's gate-retirement change merges. But during the entire fix phase the three gates
are still live in `SkipTest`, which matches **filename substrings**. Any canonical template whose
name contains `requeuing`, `with_delay`, `delayed_message` or `dead_letter_queue` is therefore *not
generated* for a configuration declaring the corresponding gate `false` — which is exactly the
situation for the reference transport.

Verified: `tests/Paramore.Brighter.Kafka.Tests/test-configuration.json` declares all three gates
`false` for both Standard and PartitionKey. `MessagingGatewayGenerator.SkipTest` lines
122/127/132/145 skip on substring match. FR-9's template
(`When_reading_a_delayed_message_via_the_messaging_gateway_should_delay_delivery.cs.liquid`) contains
`delayed_message` and is skipped for Kafka today; FR-2's replacement delayed-requeue template will,
under NFR-1's naming convention, contain `requeuing` and/or `with_delay` and be skipped likewise. Yet
0067 makes FR-2 and FR-9 mandatory ledger *columns*, and FR-21 requires a `Pass` only "when the suite
has actually run against a broker".

So step 2 ("Run the generated canonical suite against Kafka … Mark both Kafka configuration rows
`Pass`") cannot be executed for the FR-2 and FR-9 columns, and step 5 blocks the flip until it has
been. The ADR notices the gates are still on — "Kafka's three mis-declared gates disappear with the
0066 config cleanup (the flip removes the keys outright)" — and then does not say how the gated
behaviours are exercised in the interim.

**Evidence**: `docs/adr/0067-…:235-239` (step 2) and `:255-258` (step 5);
`tools/Paramore.Brighter.Test.Generator/Generators/MessagingGatewayGenerator.cs:122,127,132,145`;
`tests/Paramore.Brighter.Kafka.Tests/test-configuration.json`.

**Recommendation**: State the mechanism explicitly. Either (a) split 0066's change so `SkipTest` gate
removal lands ahead of the config-key removal, letting the suite generate while the fix phase runs;
or (b) allow a per-transport temporary `true` flip of the gate values as the first act of each fix
stage, recorded in the ledger row; or (c) name the canonical templates so none matches a gate
substring, and state that as a naming constraint alongside NFR-1. Any of the three works; leaving it
unstated does not.

---

### 2. Neither ADR plans the checked-in generated `.cs` tree, which AC-12 and AC-22 make mandatory and FR-1(6) breaks (Score: 72)

Generated output is committed to this repository. Neither ADR's Key Components, Implementation
Approach, nor Consequences mentions the generated test tree.

Two consequences are unaddressed:

- **AC-12 and AC-22 explicitly require the orphan sweep** — six `with_delay` copies and thirty-two
  exhaustion copies under `tests/Paramore.Brighter.*.Tests/**/Generated/`, verified by count.
  Deleting the `.liquid` templates does not delete them. 0066 is the deciding ADR for FR-12 and
  argues FR-19 at length in Consequences, but neither deletion step includes the sweep.
- **FR-1(6) breaks the committed generated interface copies.** **Forty** checked-in
  `Generated/{Reactor,Proactor}/IAmAMessageGateway*Provider.cs` files declare
  `CreateSubscription(..., bool setupDeadLetterQueue = false)`. Removing the parameter makes the tree
  stale until every configuration is regenerated and re-committed. 0066's Negative section covers the
  twenty hand-written providers but not the forty generated copies.

**Evidence**: `docs/adr/0066-…:373-385` — no mention of generated copies; `requirements.md` AC-12 and
AC-22. Verified counts: 6, 32, and 40.

**Recommendation**: Add a Key Component and an Implementation Approach step to 0066 covering the
generated tree: delete the 38 orphan copies, and regenerate + re-commit all 20 configurations'
provider-interface copies as part of the same change.

---

### 3. The ledger's row granularity makes a deferred FR-20 onboarding unrepresentable — the case the design says is likeliest (Score: 72)

FR-21 and ADR 0067 both key a ledger row to a *declared* gateway configuration. FR-13 defines a
targeted gateway configuration as one declared in a `test-configuration.json`.

A transport whose FR-20 onboarding is deferred never gets a `test-configuration.json`, therefore
declares no configuration, therefore contributes no row. But FR-20 and AC-23 require precisely such a
transport to "carry a ledger row", AC-24 requires the ledger to cover "every configuration of all
twelve targeted transports", and 0067's flip gate ("no `Unknown` cells") is satisfied **vacuously**
if the transport has no rows at all — the deferral becomes invisible in exactly the artifact built to
make it visible.

0067 acknowledges this is the expected case ("ASB in particular is a cloud service whose CI story is
not solved here … the honest expectation is that one or more of the three lands as `Deferred` at flip
time") and then defines the rows as arriving only "once FR-20 wires them". Two developers resolve
this differently: a project-level placeholder row, a speculative `AzureServiceBus / <tbd>` row, or
omitting the transport and reading the gate as satisfied.

**Evidence**: `docs/adr/0067-…:165-172`, `:298-303`; `requirements.md` FR-13, FR-21, FR-20, AC-24.

**Recommendation**: Decide in 0067 how an un-onboarded transport occupies the ledger — e.g. a
mandatory placeholder row per targeted *transport* carrying no declared configuration, whose cells
may only read `Deferred -> #NNNN`, converted to per-configuration rows when the config lands — and
state that the flip gate is evaluated over all twelve transports, not over the rows that happen to
exist.

---

### 4. The two ADRs' Scope statements do not collectively account for FR-19, FR-20, FR-21 or AC-24, and disagree about FR-20 (Score: 68)

ADR 0066's Scope delegates "FR-13 and FR-20 in the sibling ADR [0067]". ADR 0067's Scope replies:
"This ADR decides rollout sequencing, the conformance-ledger + linked-issue Skip deferral mechanism,
and the fix-to-conform boundary (FR-13 / AC-13)." **FR-20 is not claimed.** The requirement 0066
hands off is not picked up by the document it hands it to.

Two further requirements have no owning Scope statement at all:

- **FR-19** (delete the exhaustion template) is argued at length in 0066's Consequences but appears in
  neither Scope, and 0066 explicitly routes template *content* to "generator work under this spec's
  tasks" — not where a deletion decision with a stated rationale belongs.
- **FR-21 / AC-24** (the conformance ledger) is the newest and most consequential process obligation.
  It is served by 0067's Key Components but claimed by neither Scope paragraph.

This matters because the Scope paragraphs are what a reader uses to find the deciding ADR for a
requirement — and because round 7's finding 4 moved the ledger *into* requirements.md precisely so
that boundary would be explicit.

**Evidence**: `docs/adr/0066-…:99-106`; `docs/adr/0067-…:113-117`; `requirements.md` FR-19, FR-21.

**Recommendation**: Extend 0067's Scope to name FR-20, FR-21 and AC-24 alongside FR-13/AC-13, and add
FR-19 to 0066's Scope — it belongs there, since the rationale and the `SkipTest` substring
interaction that motivates it already live in 0066.

---

### 5. `RejectionMetadataKeys` has no defined home, and the Reactor/Proactor split makes that decisive (Score: 65)

0066 specifies the type as "a new `RejectionMetadataKeys` record (in the generated test-support
namespace, per transport, **not** in `src/Paramore.Brighter` — C-2)" and says the property is "a
plain property shared by both interfaces". But the generator emits the two interfaces into
*different* namespaces and *different* directories per configuration — verified:
`namespace {{ Namespace }}.MessagingGateway{{ Prefix }}.Reactor;` versus `….Proactor`, output under
`Generated/Reactor/` and `Generated/Proactor/`.

"Shared by both interfaces" therefore requires the record to live somewhere neither template
currently writes to. The ADR does not say where, does not name a new template, and does not list it
in Key Components as a template addition. Implementations diverge: emit twice (two distinct types
with the same name, so nothing is shared and any common helper breaks), emit once into a parent
namespace (needs a generator output path the ADR does not authorise), or hand-write per transport
(contradicts "generated"). No error condition is specified either — what a provider returns for a
semantic field its gateway does not stamp, given FR-8 says such a transport "does not conform".

**Evidence**: `docs/adr/0066-…:196-207` and `:268-269`;
`tools/…/Templates/MessagingGateway/{Reactor,Proactor}/IAmAMessageGateway*Provider.cs.liquid`
namespace declarations.

**Recommendation**: Name the emitting template and the target namespace/directory in Key Components,
and state what a provider supplies for a field its gateway does not stamp (empty string, so the FR-8
assertion then fails as a genuine non-conformance).

---

### 6. The rollout sequence leaves AWS.V4's four configurations in no stage, and "RMQ" in stage (ii) is ambiguous (Score: 65)

Sequencing is 0067's primary job, and the ledger has twenty rows. Four of them belong to AWS.V4,
which is named in no stage: stage (i) is Kafka; stage (ii) is "AWS SQS, Redis, MSSQL, PostgreSQL,
RocketMQ … and RMQ"; stage (iii) is GCP, then ASB/MQTT/RMQ.Sync.

0067's own Context distinguishes the two AWS projects — "AWS (V3 and V4)" and "AWS and AWS.V4 four
each" — so "AWS SQS" in stage (ii) cannot safely be read as covering both. Separately, "RMQ" in stage
(ii) is not disambiguated: `RMQ.Async` is wired and belongs to the fix phase; `RMQ.Sync` is unwired
and is explicitly assigned to stage (iii). The stage-(ii) prose about the native DLX reads as if it
applies to RabbitMQ generally.

**Evidence**: `docs/adr/0067-…:40-45` vs `:139-149` (pipeline diagram) and `:240-247` (step 3).

**Recommendation**: Enumerate the stages by project name exactly as the Context does — `AWS`,
`AWS.V4`, `Redis`, `MSSQL`, `PostgresSQL`, `RocketMQ`, `RMQ.Async` in stage (ii) — and reserve
`RMQ.Sync` to stage (iii) by name.

---

### 7. ADR 0066 cites structure of FR-12 and AC-12 that does not exist (Score: 62)

Two references cannot be resolved against `requirements.md` as it now stands:

- "This is the AC-12 **replace** arm" — AC-12 has no arms; it is a single conjunction.
- "**Fix the `with_delay` template in place (FR-12 option a).**" — FR-12 has no lettered options. It
  states unconditionally that the template "MUST be deleted, superseded by FR-2."

0066's own Scope compounds this by describing item (3) as "**replacing** the broken `with_delay`
template (FR-12)", while its Decision and Technology Choices say "**Delete** (not fix)". These are
residues of an earlier fix-or-replace framing the requirement no longer offers, and a reader
following the citation finds nothing.

**Evidence**: `docs/adr/0066-…:100-101`, `:375`, `:471`; vs `requirements.md` FR-12 and AC-12.

**Recommendation**: Drop "option a" and "the AC-12 replace arm"; cite FR-12 and AC-12 as written.
Change Scope item (3) to "deleting the broken `with_delay` template (FR-12)" to match the Decision.

---

### 8. ADR 0066's Scope uses a requirement range that includes the retired FR-3 and omits FR-15 (Score: 62)

0066's Scope defers "the detailed *content* of each individual canonical template (FR-2…FR-9, FR-16,
FR-17)". The range `FR-2…FR-9` sweeps in **FR-3** — which the same ADR, forty lines later, declares a
retired identifier never reused. The range also omits **FR-15** (zero/null-delay requeue), a
canonical behaviour with its own template and its own ledger column.

Every other statement of the canonical set in all three documents uses the correct enumeration —
`requirements.md:80`, `docs/adr/0067-…:31` and `:175` all read "FR-2, FR-4 … FR-9, FR-15, FR-16,
FR-17". 0066's Scope is the one place that does not, and it is the paragraph a reader consults to
learn what the ADR decides.

**Evidence**: `docs/adr/0066-…:101-103`; contrast `:128-129`, `requirements.md:80`,
`docs/adr/0067-…:31`.

**Recommendation**: Replace with "FR-2, FR-4 … FR-9, FR-15, FR-16, FR-17".

---

### 9. The parity rule permits a ledger cell state the cell vocabulary cannot express (Score: 62)

0067 defines the cell vocabulary as closed: each cell holds "**exactly one** of" `Pass`, `Fixed
(#PR/commit)`, or `Deferred -> #NNNN (sign-off: @maintainer)`, plus a transient `Unknown`. A row is
one configuration; there is no variant axis.

The parity paragraph then says a partially-conforming behaviour is either `Unknown` "**or is split
into a deferral for the lagging variant**". Nothing says what a split cell looks like. Since
Reactor/Proactor parity (FR-14) applies to every canonical column on all twenty-plus rows, and the
async gateway paths are genuinely distinct code, partial conformance is a routine expected state, not
an edge case. Implementations diverge: split columns per variant (doubling the matrix), two values in
one cell (breaking "exactly one of"), or promote the whole cell to `Deferred` and lose the record
that one variant passes.

**Evidence**: `docs/adr/0067-…:182-188` (vocabulary) vs `:265-267` (parity).

**Recommendation**: Pick one. Either add a fourth cell form (`Pass (Reactor) / Deferred -> #NNNN
(Proactor)`) to the vocabulary explicitly, or state that partial parity is always recorded as a
single `Deferred` cell whose issue names the lagging variant.

---

### 10. The gate mis-declaration inventory undercounts AWS by half (Score: 45)

Both ADRs say "AWS declares `HasSupportToDelayedMessages: false` in three of its four gateway
configurations (the fourth declares `true`)". Verified against the JSON, `AWS.V4` declares the
identical pattern — `SnsStandard`, `SnsFifo`, `SqsFifo` all `false`, `SqsStandard` `true`. Across the
eight AWS-family configurations in the target set, **six** mis-declare, not three. Since both
documents elsewhere treat AWS and AWS.V4 as distinct projects contributing four configurations each,
"its four" reads as the AWS project only. Same class of omission as the MSSQL gap found in the prior
design round.

**Evidence**: `docs/adr/0066-…:59-60` and `:367-369`; `docs/adr/0067-…:59-61`;
`tests/Paramore.Brighter.AWS.V4.Tests/test-configuration.json`.

**Recommendation**: "AWS and AWS.V4 each declare it `false` in three of their four configurations —
six of the eight AWS-family configurations".

---

### 11. A nine-item project list is labelled "the twenty" (Score: 40)

0066's Negative section reads "the twenty wired today (Kafka, Redis, MSSQL, PostgreSQL, RMQ.Async,
AWS SNS/SQS ×2 versions, GCP, RocketMQ)". The parenthetical enumerates nine *projects*, not twenty
*configurations*. The distinction is one the ADR takes trouble to establish elsewhere, so the sloppy
apposition undercuts it at the one point a reader is counting. **Introduced by the round-7 edit.**

**Evidence**: `docs/adr/0066-…:409-411`.

**Recommendation**: "the twenty configurations wired today, across nine projects (Kafka, Redis, …)".

---

### 12. "Roughly twenty" and a wrong count survive in requirements.md and the spec README (Score: 40)

Round 7 replaced every `~20` in the ADRs with an exact count, but the sibling documents were not
swept. `requirements.md:28-29` still says "the delayed-delivery test runs for three of **roughly
twenty** gateway configurations", and `README.md` says "only **two** of roughly twenty gateway
configurations declare `HasSupportToDelayedMessages: true`". The correct count is **three**
(`AWS/SqsStandard`, `AWS.V4/SqsStandard`, `RocketMQ`), confirmed independently by the six checked-in
`with_delay` generated copies (3 configurations × 2 variants). requirements.md's "three" is right;
**the README's "two" is wrong**, and both retain the "roughly" the ADRs no longer use. The README
error was **introduced by the round-7 edit**.

**Evidence**: `requirements.md:28-29`; `README.md:15-17`; verified against the
`test-configuration.json` files declaring gateways.

**Recommendation**: "three of the twenty gateway configurations" in both; fix the README's "two".

---

### Round 5 summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 3 |
| 50-69 (Medium) | 6 |
| 0-49 (Low) | 3 |

**Total findings**: 12
**Findings at or above threshold (60)**: 9

**Source claims spot-checked and correct**: `SkipTest` gates at 122/127/132/145;
`MessagingGatewayConfiguration` properties at 91/96/106; `IAmAChannelSync` Reject 64 / Requeue 83;
`InMemoryScheduler` line 285; `OutboxProducerMediator` 458/483; the `with_delay` Reactor template's
bare `_channel.Requeue(received);` at line 62; the verbatim `CreateSubscription` quote; the exhaustion
template as the sole positional `true` caller of the bool; Kafka `HeaderNames` PascalCase vs
Redis/SQS `RefreshMetadata` camelCase including `OriginalType`/`originalMessageType`; six
`IAmAChannelFactoryWithScheduler` gateways; 20 providers, 32 + 6 generated copies; SQS/Postgres native
delay, GCP Pull and Stream both ignoring it, RocketMQ's commented-out `ChangeInvisibleDuration`; and
all twelve cited ADR slugs resolve.

---

## Round 4 (2026-07-19) — headed "re-review 3" in its original text

**Date**: 2026-07-19
**Threshold**: 60
**Verdict at time of review**: NEEDS WORK (2 findings ≥ 60)
**Status after remediation**: all 6 findings addressed in commit `2745b2095` — **re-verified as still
fixed in round 5**

## Prior Findings — Status

| # | Prior finding | Prior score | Status |
|---|---------------|-------------|--------|
| 1 | FR-2/FR-3 mechanism-conditionality vs seam coverage | 75 | Partially resolved → now addressed |
| 2 | Alternative 4 under-argued | 65 | Resolved |
| 3 | Gate-inventory inaccuracies | 50 | Resolved |
| 4 | DLQ/invalid read semantics | 50 | Resolved |

Verified this round: `IAmAChannelFactoryWithScheduler` is implemented by exactly six gateways
(Kafka, MQTT, MsSql, Redis, RMQ.Async, RMQ.Sync); the target set is 9 projects / **20** gateway
configurations matching 20 `*MessageGatewayProvider.cs` files, of which 6 can carry a scheduler.
`SqsMessageConsumer.RequeueAsync` issues `ChangeMessageVisibilityAsync` (line 402);
`PostgresMessageConsumer.Requeue` binds the delay as a query parameter (line 459). Orphan sweep for
`CreateChannelWith*Scheduler` / `SpyScheduledChannel` / `SpySchedulerAsync` was clean. Every gate
value asserted across the three documents checks out against the JSON. The `MT_NONE`-on-empty read
contract is sufficient for AC-5/AC-18.

## Findings (all now addressed)

### 1. "The other 14 delay natively" is factually wrong for GCP (×4) and RocketMQ (Score: 75) — FIXED

The claim was generalized from the two configurations that had been verified (SQS, PostgreSQL) and
is false for five of the fourteen. `GcpPullMessageConsumer.Requeue` (line 283) ignores `delay` and
calls `ModifyAckDeadline(subscriptionName, [ackId], 0)`; its own XML doc reads *"An optional delay
(not used by Pub/Sub)"* — redelivery timing is governed by the subscription's RetryPolicy.
`GcpPubSubStreamMessageConsumer.Requeue` (line 217) is the same. `RocketMessageConsumer.Requeue`
(line 179) is a **no-op returning `true`**, its `ChangeInvisibleDuration` call commented out
pending an upstream RocketMQ C# client fix.

The conclusion survives (these transports have neither a scheduler seam nor a native requeue delay),
but it understated rollout risk: the mechanism-agnostic FR-2 will **fail on 5 configurations** at the
flip, and 0067 sequenced RocketMQ among transports expected to need only localized `Fixed` work when
it is in fact blocked on a third-party dependency.

**Resolution**: corrected in 0066 ("Why there is no scheduler member" now distinguishes the nine that
delay natively from the five that do neither), in requirements.md FR-3's withdrawal rationale, and in
0067 §Context, which now seeds GCP ×4 and RocketMQ as known FR-2 non-conformances with RocketMQ
flagged as a likely signed-off `Deferred` row.

### 2. FR-3 withdrawal incomplete: live design text still directed implementers to the withdrawn arm (Score: 70) — FIXED

The withdrawal landed in the Decision, interface sketch, "Why there is no scheduler member",
Alternative 4 and 0067's ledger rationale, but not in Consequences, Risks, Alternative 3, References
or several spots in requirements.md. Two were live instructions: 0066's Negative bullet required a
provider to supply *"a scheduler-backed channel"*, and its 0067 reference told the ledger to track
*"the in-memory scheduler arm"* — neither exists in the design.

**Resolution**: swept all `FR-2/FR-3` pairings to `FR-2`; removed the scheduler-backed-channel and
in-memory-arm clauses; restated Alternative 3 against the amended FR-2; put the
`0037-universal-scheduler-delay` gloss in the past tense; dropped the producer `Scheduler` from
requirements.md's Objective and Test Boundary; corrected OOS-3's transitive-proof justification
(scheduler forwarding is **not** proven transitively now, so those wiring tests are excluded as
OOS-2 work rather than as redundant); and corrected the Kafka coverage-gap paragraph, which had
claimed the generated suite closes an async scheduler gap it no longer closes.

### 3. AC-1 demanded "separate members" the design does not provide (Score: 50) — FIXED

AC-1 required *"separate members exist to create channels configured with a DLQ only, an invalid
channel only, and neither"*; 0066 provides one `CreateSubscription` with two nullable defaulted
routing-key parameters — same expressiveness, fewer members. **Resolution**: AC-1 reworded to
"the provider exposes a means to …", naming the nullable-parameter approach.

### 4. Read contract covered "queue empty" but not "queue not configured" (Score: 45) — FIXED

**Resolution**: contract extended — reading a DLQ or invalid channel the subscription does not
configure also returns `MT_NONE` rather than throwing; and the FR-5/FR-17 templates are stated to
configure both routing keys and assert the message landed on one and not the other.

### 5. MSSQL's mis-declared `HasSupportToDeadLetterQueue: false` absent from the inventory (Score: 35) — FIXED

`tests/Paramore.Brighter.MSSQL.Tests/test-configuration.json` declares it `false` despite ADR
`0040-mssql-dlq-brighter-managed`. **Resolution**: added to 0067 §Context, requirements.md FR-11 and
§Additional Context.

### 6. Alternative 4 conflated six *gateways* with six *configurations* (Score: 30) — FIXED

**Resolution**: split the two facts — six gateways implement the interface, four are in the target
set, contributing 6 of the ~20 target configurations; MQTT and RMQ.Sync noted as out of scope.

## Summary (as reviewed)

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 2 |
| 50-69 (Medium) | 1 |
| 0-49 (Low) | 3 |

**Total findings**: 6
**Findings at or above threshold (60)**: 2
**All six addressed** in commit `2745b2095`.
