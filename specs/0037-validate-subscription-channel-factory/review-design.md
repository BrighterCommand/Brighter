# Review: design — 0037-validate-subscription-channel-factory (ADR 0073, round 5)

**Date**: 2026-09-22
**Threshold**: 60
**Verdict**: NEEDS WORK

8 findings at or above threshold 60. Address these before approving.

> **Verification note.** All eleven findings re-checked against the working tree; **all eleven hold,
> none rejected, and no grounding error in the reviewer's own evidence for the second round running.**
> Three are defects round 4's fixes introduced (findings 1, 2, 3), and **finding 2 is the more serious
> kind: a false claim that round 4 wrote into the approved `requirements.md` as part of the fourth
> amendment.** Finding 3 is the **fifth consecutive round** to find a stale copy of a rewritten passage
> — this time in the architecture diagram, the exact artefact round 5 was told to check and the main
> agent did not check before committing.
>
> **Threshold trend: 7 → 4 → 7 → 8 → 8. This has not converged.** The character of the findings has
> changed, though: rounds 1-4 found the design wrong; round 5 finds it under-specified and
> inconsistently described. See the note after the summary.

## Findings

### 1. `Subject`'s identity for a generic subject is now unspecified — round 4's fix deleted the only passage that pinned it, and step 2's synthetic generic assertion cannot be written without it (Score: 78)

`Sweep` returns `IReadOnlyList<(Type Subject, string? Reason)>`. For a subject that is an open generic
definition, the document never says whether `Subject` carries the **open definition** (`Foo<>`) or the
**closed construction** produced by the closing step (`Foo<Command>`). Round 4's finding 5 had to
establish this as a fact of the design ("where `Subject` values are closed constructions") in order to
argue about the ancestry assertion — and the fix removed that assertion, so the only passage from which
the reader could infer `Subject`'s identity is gone. What remains pulls both ways: the Subsumption
bullet says "the candidate set holds open definitions (`Foo<>`)" and subjects are "the candidates that
survive step 2", pointing to *open*; the Read-and-check bullet produces an instance from the closed type
and passes "the value and the type" to `Check`, pointing to *closed*.

Load-bearing in three places:

- **Implementation step 2** adds "a generic subscription that declares its own override", and the
  `Core.Tests` convention is `result.Single(e => e.Subject == typeof(X))`. For that synthetic, `typeof(X)`
  is either `typeof(X<>)` or `typeof(X<Command>)` — they do not compare equal, so one of the two
  assertions never matches. Two developers write different tests and one gets a green test finding
  nothing.
- **`AdditionalExpectedSubjects`** is a list of configured names rendered as `typeof(...)` literals. If a
  future second declaring subject is generic, the template must render either the open literal or a
  closed one with the representative argument appended, and a configuration string cannot express the
  closed form without the template supplying `Paramore.Brighter.Command`.
- **Ordering** is "by `Subject.FullName` using ordinal comparison", and `` Foo`1 `` sorts differently from
  `` Foo`1[[Paramore.Brighter.Command…]] ``; the reason text uses `Type.FullName`, so messages differ too.

**Evidence**: ADR:213 the signature; ADR:239-241 "the candidate set holds open definitions (`Foo<>`)";
ADR:250-251 "A surviving candidate that is an open generic definition is closed with `MakeGenericType`
before it can be read"; ADR:258-260 "The value and the type are passed to `Check`"; ADR:404-405
`result.Single(e => e.Subject == typeof(X))`. A grep for `Subject` returns no line stating which form
the entry carries — confirmed independently by the main agent.

**Recommendation**: Add one sentence to the `Sweep` contract fixing `Subject` as the type *as read* (the
closed construction for a generic subject) or as the candidate *as discovered* (the open definition) —
pick one. If closed, state that the template renders a generic `AdditionalExpectedSubjects` entry as
`typeof(Foo<Paramore.Brighter.Command>)` using the same representative argument as the closing step, and
that step 2's synthetic assertion uses the closed literal.

---

### 2. The exact-set assertion cannot detect a misaimed `SubscriptionType` — the expected set is derived from the same configuration value that aims the sweep. The false claim is in the ADR twice and in the amended requirements (Score: 72)

Of the four failure modes the ADR derives for the new assertion, three hold and one does not:

- *Subsumption over-reporting* — expected `{RocketSubscription}`, reported
  `{RocketSubscription, RocketMqSubscription<…>}` → **fails**. ✔
- *Subsumption inverted* — expected `{RocketSubscription}`, reported `{RocketMqSubscription<…>}`, `Reason`
  null → **fails**, and correctly noted that non-emptiness would not. ✔
- *Candidate discovery finds nothing* — `{}` against a non-empty expected set → **fails**. ✔
- *"A `SubscriptionType` pointed at the wrong assembly … yields an empty or foreign set"* — **does not fail
  on that account.** `SubscriptionType` now does double duty: the template renders it both as
  `typeof(X).Assembly` (the locator) and as the expected subject. A misaim therefore moves the assembly
  under sweep and the expectation *together*, and the assertion is self-consistent — the set can never be
  "foreign", because the configuration is the only definition of what is foreign.

The misaim *is* caught, by three mechanisms the ADR does not credit. A cross-gateway misaim usually fails
to **compile**, because no gateway test project references a second gateway (C-9's topology). A misaim to
`Paramore.Brighter` itself (`Paramore.Brighter.Subscription`, reachable from all twelve) sweeps that
assembly and reports `{Subscription}` — matching the configuration exactly — failing only on the **reason**
check, because `Subscription.ChannelFactoryType` is `typeof(InMemoryChannelFactory)`. And the
thirteenth-gateway **audit** catches it structurally: the misaimed directory is named by zero
configurations and another by two. What the exact set genuinely adds over non-emptiness is the *wrong type
in the right assembly* case (e.g. MQTT's configuration naming `MqttSubscription<T>`), which the audit's
namespace comparison cannot see. That is the true division of labour.

This matters beyond tidiness: the "ask what fails it" derivation is this passage's whole justification, it
has been rewritten three times, and each previous version failed on one of its own enumerated claims.
**The same wrong claim was written into `requirements.md` as part of the fourth amendment**, so a future
reader finds two sources agreeing.

**Evidence**: ADR:290-293 "A `SubscriptionType` pointed at the wrong assembly, or candidate discovery that
finds nothing, yields an empty or foreign set — **fails**."; ADR:226-228 repeats it in the anti-vacuous
paragraph; ADR:323 "renders both as `typeof(X).Assembly` to locate the sweep and as the expected subject";
`requirements.md:521` "…which is a stronger guard than "at most once" and additionally catches a sweep
aimed at the wrong assembly."; `src/Paramore.Brighter/Subscription.cs:172`
`public virtual Type ChannelFactoryType => typeof(InMemoryChannelFactory);`.

**Recommendation**: Replace the wrong-assembly claim in all three places with what holds — the exact set
catches the *wrong subject in the swept assembly*; a *misaimed assembly* is caught by the missing project
reference, by the reason check, and by the thirteenth-gateway audit — and say plainly that the assertion
cannot catch it, because expectation and locator are one value. The `requirements.md` sentence is a
correction to the fourth amendment, not a fifth.

---

### 3. Fifth stale copy: the architecture diagram still states the contract round 4 replaced — "one entry per candidate" (Score: 72)

Round 4's finding 4 was this contract stated two ways. The fix rewrote the prose to "one entry per subject"
and defined the two terms as "used precisely from here on" — and left the diagram's copy of the superseded
wording untouched, eight lines above. A term is now used two ways in one document, in the passage a reader
skims first.

**Evidence**: ADR:153 — `│        one entry per candidate; Reason null = sound          │` against ADR:214-216
"**candidates** are the types step 1 finds, and **subjects** are the candidates that survive step 2's
subsumption. `Sweep` returns **one entry per subject**".

**Recommendation**: "one entry per subject; Reason null = sound". While there, reconcile "surviving
candidate" (:250) and "what it examined" (:224, :400) with the new vocabulary — a subsumed candidate *was*
examined and gets no entry, so "reports every subject it examined" is the accurate form.

---

### 4. Four of the six reason paths have no test anywhere in the design — including `Check`'s null branch and every reason `Sweep` owns (Score: 70)

Six ways a reason can be produced: `Check`'s **null**, **not-a-channel-factory** and **inherited-default**
branches, plus three that "belong to `Sweep` rather than `Check`" — bad arity/unsatisfied constraints, an
uninitialised instance that cannot be produced, and a read that throws. The document specifies assertions
for exactly two (AC-28's shapes, re-covered by the fourth sweep case). The other four are specified
behaviour with no specified test:

- **`Check`'s null branch** is invented by this ADR (FR-12 has two conditions) and is load-bearing three
  times: `MockSubscription` "reports it under the **null** branch"; `NullDeclaringSubscription` "is reported
  under the **null** branch by design"; and C-13's out-of-repo null override is the real shape it catches.
  Yet the `Core.Tests` cases are deliberately subject-scoped, so nothing asserts either entry. **A `Check`
  whose null branch returned `null` (sound) would pass every assertion in the design.**
- **A read that throws → a reason** is the behaviour the ADR defends at length against ADR 0064's "rules
  must not catch", on the ground that "Nothing is swallowed". No test asserts it, and no double throws on
  read — the ADR is explicit that `MockSubscription` "reads `null` rather than throwing". **An
  implementation that caught the exception and emitted a `null` reason satisfies the exact-set assertion,
  the twelve reason-null checks, and the fourth case.** A silent vacuous pass in the one place Risks says
  the mechanism must be actionable.
- The same swallow-to-null defect is undetectable for "an uninitialised instance cannot be produced". (A
  *silent skip* in either path **is** caught, because the subject goes missing from the exact set — worth
  saying, since it is the design's strongest new property.)

**Evidence**: ADR:196-204 the three named branches; ADR:262-272 "Three reasons belong to `Sweep` rather than
`Check` … Nothing is swallowed — the sweep still fails, with more information."; ADR:402-405 subject-scoped
assertions; ADR:425-434 the seam analysis, which names only "a `Sweep` that read the wrong property, passed
`null` to `Check`, or discarded the read value entirely"; ADR:553-561 Risks.

**Recommendation**: Add to step 2 (a) a `Check` case for the null branch with literal arguments, and (b) one
`Core.Tests` double whose `ChannelFactoryType` getter **throws** on an uninitialised instance, asserting its
entry carries a non-null reason naming the exception type — that single double converts the ADR-0064 rebuttal
from an argument into a tested property. If the arity/constraint and cannot-instantiate reasons are to stay
untested, say so and say why (a silent skip is caught by the exact set; a swallowed fault is not).

---

### 5. `AdditionalExpectedSubjects` is under-specified, and `SubscriptionType`'s new double duty imposes an unstated invariant (Score: 68)

Round 4 introduced this key in two sentences. Missing: its **type and default** (a `List<string>` of
fully-qualified names? null or empty when absent? — it is absent in all twelve today, so the template must
render a valid expected-set expression regardless); **what the template renders from it** (inferable as
further `typeof(...)` literals, but not for a generic subject — finding 1); **the thirteenth-gateway
audit's** relationship to it (the audit asserts each gateway directory is "named by exactly one
`GatewayConformance.SubscriptionType`" — if `AdditionalExpectedSubjects` counted, the very case the key
exists for would make a directory "named by two" and fail; the exclusion is right but unstated, and the
passages are 60 lines apart); and **the twelve-row table's** silence (headed `SubscriptionType` only,
introduced as "The twelve values", so absence is indistinguishable from omission).

The unstated invariant matters most. `SubscriptionType` used to be any subscription type in the gateway
assembly — a pure locator. It is now "the subscription type expected to be reported", which silently
requires the configured type to be a **subject**: to declare its own `ChannelFactoryType`, or at least be a
root candidate. Naming the generic derived type (`MqttSubscription<T>`), previously a perfectly good
locator, now produces a red test with a set-mismatch message that does not explain why.

**Evidence**: ADR:313-318 and :321-325 (the two introducing passages); ADR:348-361 (the one-column table);
ADR:377-383 (the audit — "named by exactly one `GatewayConformance.SubscriptionType`").

**Recommendation**: Give the key a type and default, state that the expected set is `SubscriptionType` ∪
`AdditionalExpectedSubjects`, state that the audit counts `SubscriptionType` only and why, note in the table
that the key is absent in all twelve today, and write the invariant down: `SubscriptionType` MUST name a type
that survives subsumption.

---

### 6. The exact-set assertion makes 5 of the 12 generated sweeps red until ADR 0072's corrections land, and no step records the ordering constraint (Score: 65)

The generated test asserts every `Reason` is `null`. Against the working tree that is false in five of twelve
assemblies, by this specification's own count: FR-7 (GcpPubSub) and FR-8 (MQTT) declare an
`IAmAMessageConsumerFactory`, so their subject reports **not-a-channel-factory**; FR-9 (AWSSQS), FR-10
(AWSSQS.V4) and FR-11 (Postgres) declare no override at all, so their non-generic base is a root candidate
reporting **inherited-default**. Step 4 commits the twelve generated files and step 6 adds the CI step, with
no statement that 0072's FR-7 to FR-11 corrections must merge first. Two developers sequence this
differently, and one turns the `build` job red on every pull request. The ADR is *aware* of the fact — Risks
says "all nine today, and all twelve once 0072 adds its three" — it never draws the scheduling conclusion,
which is the one thing an implementer needs.

**Evidence**: `requirements.md:160-176` FR-7 to FR-11; ADR:281-285 the reason-null assertion; ADR:554-557
Risks; ADR:468-480 (step 4) and :482 (step 6) — no ordering note.

**Recommendation**: Add a clause to step 4 or a Negative bullet: the twelve sweeps are red in five assemblies
until 0072's FR-7 to FR-11 land, so steps 4 and 6 sequence after those corrections — and say what happens if
0072 slips (the generated files may be committed; the CI step must not be enabled).

---

### 7. C-9's "identity-only … every `IAmAChannelFactory` member throws" rule is applied to three *subscription* doubles, which have no such members — and the two helper types the design needs are never named (Score: 62)

The `Core.Tests` paragraph says "Three doubles, all identity-only in C-9's sense — every `IAmAChannelFactory`
member throws:" and lists three **subscriptions**. A `Subscription` subclass implements no
`IAmAChannelFactory` member, so the obligation cannot bind, and C-9's rule is explicitly about the *channel
factory* doubles (it names `CreateSyncChannel`, `CreateAsyncChannel`, `CreateAsyncChannelAsync`). Meanwhile
the design silently requires two types it never names: the not-an-`IAmAChannelFactory` type the second double
declares, and the sound `IAmAChannelFactory` that AC-29's double declares — the latter being the one type
here to which C-9's throw-rule genuinely applies.

**Evidence**: ADR:385-400 the three-doubles list; `requirements.md:236` "Every `IAmAChannelFactory` member on
these doubles MUST therefore throw (`CreateSyncChannel`, `CreateAsyncChannel`, `CreateAsyncChannelAsync`)."

**Recommendation**: Split the list: three subscription doubles (no `IAmAChannelFactory` members, so C-9's
throw-rule is inapplicable — say so), plus the two types they declare — a non-factory marker type and one
sound channel-factory double to which C-9's rule *does* apply. Name both.

---

### 8. The Subsumption bullet quotes FR-12 truncated at the exact clause the fourth amendment added (Score: 62)

Round 4's finding 9 was a truncated FR-12 quote whose dropped clause changed its force; the fix quoted *that*
clause in full and left this one. The amended Scope reads "MUST be reported at most once **when the derived
type declares no `ChannelFactoryType` of its own**"; the ADR quotes "a base/derived pair … MUST be reported at
most once", the ellipsis swallowing the example and the condition landing outside the quotation marks. The
sentence after does convey the condition, so the design is not wrong — but the quoted requirement text is the
superseded wording, in the one paragraph where the conditional reading is the whole point.

**Evidence**: ADR:245-247; `requirements.md:193` as amended.

**Recommendation**: Quote the amended clause with its condition inside the quotation marks, as the template
paragraph already does.

---

### 9. The frontmatter `summary` describes dedup unconditionally and omits the decision's new centre of gravity (Score: 55)

`summary` says the sweep reads the property "de-duplicating base/derived pairs by subsumption" — not false,
but it is the pre-amendment framing, and a reader who stops there takes away the unconditional obligation that
cost round 4 a requirements amendment. It also predates the round-4 rewrite in substance: the
exact-subject-set assertion is now the design's principal claim and the summary says only that the tests "are
generated from a single new Liquid template"; `AdditionalExpectedSubjects` is absent.

**Evidence**: ADR:8.

**Recommendation**: "…de-duplicating a base/derived pair only where the derived type declares no override of
its own; the twelve generated per-gateway tests assert the reported subject set exactly, plus all reasons
null, and carry no reflection logic."

---

### 10. The CI step can pass vacuously if a generated file is missing (Score: 50)

The `build` job runs twelve projects with `--filter "FullyQualifiedName~GatewayChannelFactoryDeclarationTests"`.
If a project's generated file were absent the filter selects nothing, and `dotnet test` reports "No test
matches the given testcase filter" without failing on most runners — the guard's CI step then passes while
guarding eleven assemblies. The generated-tree audit in the same job does cover missing files, which is why
this is a 50, but the ADR nowhere records that the step's non-vacuity depends on a *different* step.

**Evidence**: ADR:364-366 the filter and `--no-build`; ADR:468-470 the audit as what catches absence.

**Recommendation**: One clause in the CI paragraph: the step's non-vacuity rests on the generated-tree audit,
which fails when any of the twelve files is missing; or pass a fail-on-no-tests switch where the runner
supports it.

---

### 11. Two term slips left by round 4's vocabulary change (Score: 40)

"an empty result would pass identically over twelve sound subscriptions and over zero candidates" (:226) — a
single gateway assembly has *one* subject, not twelve; the comparison is per-assembly. And :250 says "A
surviving candidate that is an open generic definition", where the paragraph above has just named that a
*subject*.

**Evidence**: ADR:224-230, ADR:250.

**Recommendation**: "over a sound assembly and over zero candidates"; "A subject that is an open generic
definition".

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 4 |
| 50-69 (Medium) | 6 |
| 0-49 (Low) | 1 |

**Total findings**: 11
**Findings at or above threshold (60)**: 8

## Convergence assessment

**Threshold counts: 7 → 4 → 7 → 8 → 8.** Five rounds, 50 findings, none rejected. But the *character* has
changed, and that is the signal worth acting on:

- **Rounds 1-4 found the design wrong** — a failures-only contract, a justification that collapsed, an
  assertion that could not fail, an assertion that failed correct behaviour.
- **Round 5 finds almost nothing wrong with the design.** Of eight at-threshold findings, one is a false
  claim (2), one is stale duplication (3), one is a real specification gap that matters (4), and the rest are
  under-specification (1, 5), a missing scheduling note (6), and quotation/description accuracy (7, 8).

That is the profile of a document whose *decisions* have settled and whose *prose* has not. The remedy 0072
used at the same point is on record: round 4 of that ADR diagnosed that Key Components drew 18 of 24 findings
from 46% of the document, and the `8d03b94c6` tidy re-derived that section wholesale (424 → 278 lines),
after which findings fell 5 → 2 → 2 → 0. **The same diagnosis fits here**, and patching site-by-site has now
produced a stale copy in five consecutive rounds.

## Round-4 fixes verified

1. **Finding 1 (80) — ancestry assertion rejects a correct result.** *Complete.* The assertion is gone,
   replaced by an exact-set assertion with no reflection, and the reason it was wrong is recorded against the
   Risks bullet it contradicted. The requirements amendment fixes the imprecise half in FR-12 Scope and AC-27,
   and the *Amendments* entry accurately describes what changed ("No other criterion is altered" — verified:
   `grep -n "at most once"` returns only :193, :519 and the amendment prose). Cosmetic: the fourth-amendment
   paragraph is inserted *before* the third's, so the section reads first, second, fourth, third.
2. **Finding 2 (75) — assertion never exercised; inverted subsumption missed.** *Complete, with one wrong
   claim introduced.* Exactly-one-subject is a real assertion over the twelve, and inverted subsumption now
   genuinely fails. But the misaimed-`SubscriptionType` failure mode does not hold (finding 2).
3. **Finding 3 (68) — fourth stale copy in the grounded references.** *Complete.* The
   `CombinedChannelFactory` line now matches Context. A **fifth** stale copy stands in the diagram (finding 3).
4. **Finding 4 (66) — `Sweep`'s contract stated two ways.** *Partial.* The prose fix is good and the
   non-emptiness claim is now proved (the proof is sound: ancestry is finite and acyclic, so a non-empty
   candidate set has a root, and a root is never dropped). The diagram retains the old wording (finding 3) and
   two term slips remain (finding 11).
5. **Finding 5 (65) — template's generic reduction under-specified.** *Dissolved, and it took the pinning with
   it.* With no reflection in the template there is no reduction to specify — but round-4 finding 5 was the
   only place stating that a generic `Subject` is the closed construction, and its deletion leaves that
   unspecified (finding 1).
6. **Finding 6 (62) — necessity principle unreconciled.** *Complete.* Context concedes the type is not
   necessary on the principle's own terms and states FR-12 as the overriding authority; References matches.
7. **Finding 7 (62) — `Core.Tests` enumeration incomplete.** *Complete.* All four of C-9's subscription
   doubles are named and the expression-bodied-`typeof` requirement is stated as a constraint. Verified:
   `MockSubscription` at `:85-87` is still the only `Subscription` subclass in `Core.Tests`, and is
   `public override Type ChannelFactoryType { get; }` assigned in the constructor. The surrounding paragraph's
   misapplication of C-9's throw-rule is pre-existing and untouched (finding 7).
8. **Finding 8 (60) — subsumption rule expressed twice.** *Complete, by dissolution.* Both the Positive bullet
   and the Risks mitigation are corrected to match, with an honest note that an earlier draft had put the rule
   in the template.

## Grounding sampled and correct

`Subscription.cs:172`/`:213`; `Command.cs:42`; `CombinedChannelFactory.cs:34`/`:46`/`:59`; `ci.yml:228`,
`:361`, `:708`; twelve `src/Paramore.Brighter.MessagingGateway.*` directories, no thirteenth; 24 non-abstract
gateway `Subscription` subclasses in twelve base/derived pairs; fourteen existing `test-configuration.json`
files with AzureServiceBus, MQTT and RMQ.Sync absent; the amended FR-12 Scope and AC-27 quoted correctly.
**No grounding errors found this round.**
