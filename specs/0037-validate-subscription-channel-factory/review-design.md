# Review: design — 0037-validate-subscription-channel-factory (ADR 0072, round 5)

**Date**: 2026-09-17
**Threshold**: 60
**Verdict**: NEEDS WORK

2 findings at or above threshold 60. Address these before approving.

> **Main-agent verification.** Per the standing rule that agent findings are claims, not facts, all
> four were checked against the working tree before this file was written. **All four hold**, and
> every cited line number was confirmed accurate — which mattered more than usual this round: the
> review agent stalled once and produced its final output on resume **without making any further
> tool calls**, so all of its line references came from its pre-stall reading and could have gone
> stale. They had not.
>
> **One supporting claim in finding 1 is wrong and is corrected below**: the agent stated that `D5`
> "appears nowhere in … `PROMPT.md`". It appears there six times. The finding's substance is
> unaffected — `D5` is absent from `requirements.md` (0 hits), which is the document the spec's
> amendment convention governs, and absent from the spec directory entirely (0 hits). `PROMPT.md` is
> gitignored session state, not a specification artefact, so recording a maintainer decision only
> there is precisely the gap the finding identifies.
>
> Also verified: `requirements.md:553-565` defines **D1, D3, D4, D2** and no fifth decision; AC-7's
> text (`requirements.md:357`) does constrain the handed clause; the ADR's three `D5` citations are
> at 409, 422 and 425; the `body :` grammar at line 374 does include `{remedy}` against line 359's
> body/remedy split; and the D1/D2 argument does appear three times (485, 730, 735). Summary counts
> recomputed by hand and match.

## Round 4 disposition

1. **(78) C-13's release note dropped from the implementation plan and the count — FIXED.** ADR lines 530-531 now read "6. **Release notes** for C-8's obligations — C-10, C-11, C-12 and C-13 as separate entries, with C-12 flagged as the one `throwOnError: false` does not avoid, and C-13 flagged as one it does." Line 673 now reads "**Release-note burden.** Four distinct breaking-change notes (C-10, C-11, C-12, C-13)". Both match `requirements.md:221` (C-8) and C-13's Obligations (`requirements.md:304`).

2. **(65) The body-revision licence named five of the constraining criteria, twice — FIXED as recommended.** Lines 362-367 state the set **once** as eight, and the Risks mitigation at 698-701 refers to it ("defined once in Key Components §3 rather than restated here, so the two cannot drift apart") instead of restating it. The three criteria round 4 named as missing (AC-10b, AC-13a, AC-13c) are all present. The residual scoping defect is finding 2 below — a different defect from the one round 4 filed.

3. **(64) Forces' unqualified "certain failure on every start" — FIXED.** Lines 102-106 now carry the qualifier, name MQTT as the twelfth transport and C-11, and reduce the duplicate argument to a pointer that resolves (Alternatives 725-731).

4. **(62) Three citations deleted by the tidy — FIXED.** All three restored and all three accurate:
   - **NFR-2** at line 436 — `requirements.md:203` does require each message to state a remedy. Correct claim, and in the T4 paragraph where round 4 recommended it.
   - **`.agent_instructions/testing.md` § *Test Scope and Isolation*** at lines 345-346 and 743-745. Quoted text checked against `testing.md:103-104`; character-identical apart from a lower-case initial for mid-sentence use, including the source's own `it's` solecism. Both attachments are to surface-narrowing decisions, which is what the rule governs.
   - **FR-5 item 3** at line 739 — matches `requirements.md:123` and restores the evidence for the sentence it supports.

5. **(60) The empty-set/FR-5-item-3 vacuity reasoning lived only in a commit message — FIXED.** Lines 400-406 add the paragraph, in the right section, with the quotation and the item-2 contrast.

6. **(52) Four facts still stated twice — PARTIALLY FIXED (three of four).**
   - `DisposingSpecification` — FIXED. §2 line 286 is now a pointer; the reason lives once at 787-789.
   - Nested-composite limitation — FIXED. §3 (425-431) keeps the rendering fact and defers the consequence to Negative (653-658). The T2 mechanics were re-checked by hand against `CombinedChannelFactory.cs:34/37`: declaring `typeof(CombinedChannelFactory)` is selected by the outer composite and rejected by the inner one — the ADR's claim is correct.
   - C-13's "only reader in `src/`" — FIXED. One hit, line 648 (Negative); Risks 709-710 refers to it.
   - **The D1/D2 "corrections are load-bearing" argument — NOT deduplicated.** See finding 4.

7. **(45) "decider" is not a project stereotype — FIXED.** Line 247 now reads `service provider`, which is in `design_principles.md:15`'s list.

8. **(42) "cannot be written with the approved double set at all" overstated — FIXED.** Lines 687-689 carry exactly the recommended softening.

**Net: six FIXED, one PARTIALLY FIXED (6), one FIXED with a new adjacent defect (2). No regression introduced by `f06e98471` in the passages it touched.**

## Hypotheses under test

**1. The body constraint set — the eight listed are correctly scoped, but the set is incomplete, and the AC-7 carve-out does not hold.**

Every AC in `requirements.md` was classified by whether a revision of the body wording could break it:

| AC | Message assertion | Body constraint? |
|---|---|---|
| AC-1, AC-2/3/4/6/8/9/10/11, AC-16-19, AC-25/26/26f, AC-27-29, AC-31 | none (or severity/`Source`/count only) | no |
| AC-5, AC-30 | byte-identical messages across runs | no — a determinism property, not breakable by re-wording |
| AC-7 | display names of both inner factories, and **does not name `Paramore.Brighter.CombinedChannelFactory` as the type the subscription will be handed** | **yes — and absent from the set** |
| AC-10a, AC-10b | literal `no ChannelFactoryType`; no `configure a channel factory of type` | yes ✓ in set |
| AC-10c | ends with T4; no `is one of:` | yes ✓ in set |
| AC-12 | display name of the subscription's own type | yes ✓ in set |
| AC-13, AC-13b | "ends with" a remedy literal only | no — correctly excluded |
| AC-13a, AC-13c | no `configure a channel factory of type` | yes ✓ in set |
| AC-14 | no `Version=`/`Culture=`/`PublicKeyToken=`/backtick arity | yes ✓ in set |
| AC-15 | the `ChannelFactory` token regex | yes ✓ in set |

All eight listed ACs genuinely constrain the body — none is wrongly included — and exactly one constraining criterion is missing: **AC-7**. Its exclusion does not survive scrutiny; see finding 2.

**2. The four deduplications — three lose nothing and every pointer resolves; the fourth was not performed.** For the three that were done, each target was checked and each section still stands on its own: §2 states which shapes are not used and why the collapsed constructor is wrong, deferring only the `DisposingSpecification` reason; §3 keeps the rendering fact that the Negative bullet does not restate; Risks keeps the direct-vs-combined asymmetry that Negative's C-13 bullet does not state in those terms. No information was lost.

**3. The three restored citations — restored accurately, in the right places, supporting the claims they are attached to.** Verified character-by-character against `.agent_instructions/testing.md:103-104` and `requirements.md:203`/`:123`. One observation short of a finding: the ADR acknowledges that the `InternalsVisibleTo` rule "is framed around testing and our caller is production code in another assembly" (760-762), but applies the *Test Scope and Isolation* rule twice (345, 743) to production surface without that acknowledgement. The rule's text is not testing-specific, so the application is defensible; the asymmetry in framing is a nit.

**4. The FR-5-item-3 vacuity paragraph — independently re-verified as correct, and the paragraph does establish it.** `requirements.md:123` quantifies universally over the candidate factory set, vacuously satisfied at cardinality zero. Item 2 (`requirements.md:122`) names one required type, which is why the null-`D` case needed the round-2 amendment. The ADR's quotation is exact, the contrast is drawn correctly, and the conclusion follows. FR-5's own T4 paragraph (`requirements.md:146`) independently confirms the empty-set case was already contemplated normatively on the remedy side. No requirements amendment is needed for this.

## Findings

### 1. `D5` is cited three times as a settled decision but is defined nowhere in the specification, and it is the sole stated justification for narrowing an approved acceptance criterion (Score: 70)

`requirements.md` enumerates the maintainer decisions the design may not re-open — **D1**, **D2**, **D3**, **D4** — under "Maintainer decisions already taken (not to be re-opened by design or implementation)" (`requirements.md:553-565`), each with its statement and rationale. The ADR invokes a **D5** three times in the same register, and uses it to carry real weight: at line 422 it is the *entire* reason AC-7 is excluded from the body's constraint set.

`D5` appears nowhere in `requirements.md` (0 hits) or anywhere else in the spec directory (0 hits). Repo-wide, the only other `D5`s are the unrelated decisions in ADR 0060 and ADR 0061. A reader of the parent requirements cannot resolve the reference, and the ADR never states what D5 says — line 425's parenthetical "(D5)" is the closest thing to a definition and it is a citation, not a statement.

> *Main-agent correction*: the review agent also claimed D5 appears nowhere in `PROMPT.md`. It appears there six times. That does not rescue the ADR: `PROMPT.md` is gitignored session state, explicitly not a specification artefact, so a maintainer decision recorded only there is invisible to the requirements, to the review history, and to a fresh reader of the ADR.

This is exactly the class of case this spec has a stated convention for. The round-2 amendment cycle established that when the design needs a decision the requirements own, `requirements.md` is amended rather than the ADR deviating — the ADR itself says so at lines 324-335 ("**This changed the requirements, and the requirements were amended rather than deviated from.**") and the requirements record it at 543-551 ("Amendments after approval"). D5 rules on how an approved acceptance criterion (AC-7) and an approved no-recursion rule (FR-3/AC-10) are to be satisfied. **D4 — a decision of identical shape, also raised by an adversarial review — was recorded in `requirements.md` and reflected in FR-3, C-13, C-9 and three new ACs. D5 was not recorded anywhere in the spec.**

**Evidence**: ADR lines 408-409, 422-423 and 425:
```
408	**Three of those constraints are easy to breach accidentally, plus AC-7, which the set does not
409	carry because D5 governs it:**
…
422	  that case is the accepted limitation below. AC-7 is not in the body's constraint set because D5
423	  accepts that one rendering breaches its spirit while satisfying its letter.
…
425	**The nested case names a type that does not route, and we accept that (D5).** `FactoryTypes`
```
Against `requirements.md:553-565`, which defines D1, D3, D4 and D2 and no others, and `grep -rn "D5" specs/0037-validate-subscription-channel-factory/` → no matches.

The acceptance itself is not being re-opened. The defect is that the acceptance is unrecorded and its identifier undefined, while the ADR leans on it to bound an approved criterion.

**Recommendation**: Amend `requirements.md` — add **D5** to "Maintainer decisions already taken" (stating that the nested-composite rendering may name `Paramore.Brighter.CombinedChannelFactory` in `{F-list}` because filtering it would empty the candidate list and select T4, which says less about what is configured), add it to "Amendments after approval", and note against AC-7/AC-10 that this is the accepted reading. Then the ADR's three citations resolve. This is the round-2 pattern applied to the decision that produced it.

---

### 2. AC-7's exclusion from the body's constraint set is a non sequitur, and the ADR's own text three lines earlier contradicts it (Score: 66)

The constraint set is the document's contract with future editors: "The body may be revised as long as **[the eight]** continue to hold" (362-363). Round 4 scored the incompleteness of that list at 65 and it was widened to eight. AC-7 is the ninth, and it was deliberately left out with a justification that does not work.

Three statements in the same block cannot all stand:

- Line 364 asserts of the eight: "**All eight constrain the *body*, not the remedy**" — placing AC-7, which is excluded, outside the class of body constraints.
- Line 408 puts AC-7 *inside* that class: "Three of those constraints are easy to breach accidentally, **plus AC-7**", and bullets it alongside AC-15, AC-13a/13c and AC-10a/10b — the three genuine body constraints.
- Lines 418-419 then state what AC-7 constrains, and it is unambiguously the body: "AC-7 forbids naming `Paramore.Brighter.CombinedChannelFactory` **as the type the subscription will be handed**". "The type the subscription will be handed" is the `{handed-clause}` at line 380-383. No remedy template can breach it; only the body can.

The stated reason for exclusion — "D5 accepts that one rendering breaches its spirit while satisfying its letter" (422-423) — is about the **nested** configuration. The ADR says four words earlier that AC-7's configuration is *not* nested: "AC-7's configuration has no nesting, so the criterion holds" (421). So in AC-7's own configuration the criterion holds fully and must keep holding: it is an approved, unwaived acceptance criterion. D5's acceptance of a *different* configuration's rendering is no licence to drop AC-7 from the set of things a body revision must preserve. As written, a future editor is licensed to rewrite the handed clause as, say, "but the combined channel factory `Paramore.Brighter.CombinedChannelFactory` will be handed it, offering one of '{F-list}'" — inside the stated licence, and AC-7 fails.

Relatedly, line 364's "not the remedy" is overstated for two of the eight it *does* list: line 414 says of AC-13a/AC-13c "the prohibition is on **the whole message**", and AC-14's and AC-15's assertions likewise range over the whole message. The sentence reads as a claim about which criteria bite on the body versus the remedy, and in that reading it is both loose and the premise on which AC-7 is excluded.

**Evidence**: ADR lines 362-367, 408-409 and 418-423, against `requirements.md:357` (AC-7):
```
Then exactly one `Error` is produced for `greeting-sub`, whose `Message` contains the display names
of both inner factories in constructor order, and does not name
`Paramore.Brighter.CombinedChannelFactory` as the type the subscription will be handed.
```

**Recommendation**: Add AC-7 to the set — "AC-7, AC-10a, AC-10b, AC-10c, AC-12, AC-13a, AC-13c, AC-14 and AC-15" (nine) — and replace the D5 carve-out with the accurate statement: "AC-7 constrains the handed clause, and holds in AC-7's own non-nested configuration; D5 accepts only that the nested case renders an inner `CombinedChannelFactory` in `{F-list}`, which AC-7 does not cover." Also drop or qualify "not the remedy" in the summary sentence, since AC-13a/13c, AC-14 and AC-15 range over the whole message — the point being made is that the fixed remedy literals alone do not satisfy them.

---

### 3. "Body" is defined two ways in adjacent paragraphs, and the wider definition would extend the revision licence over the normative remedy literals (Score: 55)

Line 359 defines the message as body + remedy, with the remedy explicitly outside what this ADR owns: "**The message is a body plus one of FR-5's five remedy literals** … Only the five literals are normative; the body below is this ADR's." The grammar three paragraphs later defines `body` as *including* the remedy:

```
374	body            : Subscription type '{S}' {declared-clause} but {handed-clause} {remedy}
```

The licence sentence at 362-363 — "**The body** may be revised as long as [the constraint set] continues to hold" — is then ambiguous about whether the remedy literals are inside the revisable region, which is the one thing lines 359-360 and 698-699 say they are not. In context the intent is clear, but the term the licence turns on is defined inconsistently in the same sub-section, and this is the third consecutive round in which a defect has been found in this block.

**Evidence**: ADR lines 359-360 against line 374; and Risks lines 698-701.

**Recommendation**: Rename the production at 374 to `message :` (leaving `{remedy}` in it, since the append order is what makes the "ends with" assertions hold), or restate it as `body : Subscription type '{S}' {declared-clause} but {handed-clause}` and `message : {body} {remedy}`. The latter also makes line 359's split and lines 433-439's "reaches `{F-list}` on neither side" argument read more cleanly.

---

### 4. The D1/D2 "the corrections are load-bearing" argument is still stated three times — the one deduplication round 4 asked for that was not performed (Score: 45)

`f06e98471` reduced the Forces copy to a pointer at Alternatives, which is the right move and resolves. But the argument itself still appears in full twice more, plus a third statement of its conclusion:

- Key Components §5, 485-490: "they are **load-bearing for D1**: without them the rule would report an `Error` for correct, working AWS SQS and Postgres consumers … including four families of in-repo sample."
- Alternatives, 732-735: "Under `Error` severity with `throwOnError: true` the rule would refuse startup to correct AWS SQS, AWS SQS V4, Postgres, GCP Pub/Sub and MQTT hosts … The corrections are not a follow-up; they are the precondition for the severity."
- Alternatives, 730-731: "The in-repo false positives that would have made `Error` unsafe are removed by D2, which is why the two ADRs can reach opposite conclusions from the same framework."

All three are currently consistent (§5's "four families of in-repo sample" matches C-1's four sample paths at `requirements.md:212`). The cost remains prospective and is exactly what round-4 finding 3 was: the Forces copy had already drifted before it was fixed. Note that §5's copy and Alternatives' copy already differ in scope — §5 names "AWS SQS and Postgres", Alternatives names all five transports — which is the first millimetre of the same drift.

**Evidence**: lines 485-490, 725-735; `grep -n "load-bearing"` → one hit (485), `grep -n "precondition for the severity"` → one hit (735).

**Recommendation**: Apply the pattern used for `DisposingSpecification` and C-13: let the Alternatives bullet carry the argument once, and reduce §5 to its decision — "They carry no design decision, but they are load-bearing for D1 (argued under Alternatives Considered). Their cost is C-12, recorded below."

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 1 |
| 50-69 (Medium) | 2 |
| 0-49 (Low) | 1 |

**Total findings**: 4
**Findings at or above threshold (60)**: 2
