# Review: design — 0037-validate-subscription-channel-factory (ADR 0072, round 6)

**Date**: 2026-09-17
**Threshold**: 60
**Verdict**: NEEDS WORK

2 findings at or above threshold 60. Address these before approving.

> **Main-agent verification.** All four findings were checked against the working tree before this
> file was written. **All four hold in substance.** Verified: AC-5 (`requirements.md:347`) asserts
> "byte-identical `Message`" and AC-30 (`:540`) "byte-identical messages across the two runs", so both
> are message-level assertions; the ADR's own line 187 warns that "Had the rule compared instances
> (`ReferenceEquals`) or **carried the instance into the message**, the second case would have been
> fragile", which is exactly AC-5's fragility and confirms finding 1's mechanism; `requirements.md:549`
> does still say "re-opened **once**" seven lines above the second re-opening at `:556`; line 412 says
> "Four" over four bullets; and AC-10a/AC-10b/AC-10c do carry "ends with" assertions
> (`requirements.md:381`, `:386`, `:391`) that line 359 omits.
>
> **One arithmetic claim in finding 3 is wrong and is corrected below**: the four bullets name
> **six** of the nine criteria (AC-15, AC-13a, AC-13c, AC-10a, AC-10b, AC-7), not seven. The
> count mismatch the finding identifies is real; only its figure was off.
>
> **Note on finding 1 — it overturns round 5.** Round 5 classified every AC and concluded AC-7 was
> the *only* omission, explicitly dismissing AC-5 and AC-30 as "a determinism property, not breakable
> by re-wording". Round 6 re-did the classification independently and reached a different answer, with
> a sound argument: the property is not breakable by re-wording but *is* breakable by re-sourcing the
> body's inputs, which the revision licence also permits. The main agent accepted round 5's
> classification at the time; that was wrong, and this round corrects it.

## Round 5 disposition

1. **(70) `D5` cited three times but defined nowhere in the specification — FIXED.** `44231f6f4` adds **D5** to *Maintainer decisions already taken* (`requirements.md:565`), records the re-opening under *Amendments after approval* (`:556-558`), and adds cross-references at AC-7 (`:359`) and AC-10 (`:376`). No Given/When/Then was altered — the diff is 9 pure insertions. The ADR's two remaining `D5` citations (423, 428) now both *state* what D5 says rather than merely citing it, and both agree with `requirements.md:565`.

2. **(66) AC-7's exclusion from the constraint set was a non sequitur — FIXED as recommended.** ADR line 363 now reads "**AC-7, AC-10a, AC-10b, AC-10c, AC-12, AC-13a, AC-13c, AC-14 and AC-15**". The contradicted sentence "All eight constrain the *body*, not the remedy" is gone, replaced at 364 by "None of the nine is satisfied by the fixed remedy literals alone". The D5 carve-out is replaced by 421-426: "**D5 does not relax it**: D5 accepts only that the *nested* case renders an inner `CombinedChannelFactory` in `{F-list}`, a configuration AC-7 does not cover." That matches `requirements.md:565` and `:359`.

3. **(55) "Body" defined two ways in adjacent paragraphs — FIXED**, by the second of the two recommended routes. Lines 377-378 now read `message : {body} {remedy}` and `body : Subscription type '{S}' {declared-clause} but {handed-clause}`, so the revision licence at 362-363 no longer reads as covering the normative literals.

4. **(45) The D1/D2 "corrections are load-bearing" argument stated three times — FIXED.** §5 (490-496) is reduced to its decision plus a pointer. The enumeration of transports and samples now appears only at Alternatives 738-741; Forces 102-103 carries one clause plus a pointer. `grep -n "load-bearing"` → one hit (493).

**Net: four FIXED, none regressed.**

## What the two new commits changed

**D5's definition, and whether the documents agree.** They agree closely. `requirements.md:565` and ADR 428-437 give the same three elements: the nested-composite message may name `Paramore.Brighter.CombinedChannelFactory` among the types handed; the reason is that filtering it would empty the candidate set and select T4; it is a message-quality limitation, not a verdict change. The mechanics were re-derived against `src/Paramore.Brighter/CombinedChannelFactory.cs:34-38`: a subscription declaring `typeof(CombinedChannelFactory)` *is* selected by the outer composite and then rejected by the inner one at the `throw` on line 37 — both accounts are correct.

**Does D5 license what the ADR does, and no more?** Yes. Its scope is `{F-list}`'s content in the nested case, and the ADR uses it for exactly the handed clause and the T2 remedy's `{F-list}`, both covered by `requirements.md:565`. The third citation, which bounded AC-7, is gone.

**Does the ADR honour "licenses nothing about AC-7"?** Yes, explicitly (423-426), and AC-7 is now inside the set.

**Is the nine-member set complete?** No — finding 1. AC-5 and AC-30 pass the ADR's own inclusion test and are absent.

**Is "None of the nine is satisfied by the fixed remedy literals alone" true of all nine?** Yes, verified one by one. The nearest miss is AC-7, whose positive half ("contains the display names of both inner factories in constructor order") *is* delivered by T3b's `{F-list}` — but its prohibition half ranges over the whole message and only the handed clause can breach it, so the criterion as a whole is not satisfied by the remedy alone. AC-10c is the same shape. The claim stands.

**Grammar-split consistency.** Consistent throughout. Every downstream passage uses `body` in the narrow sense (391-399, 402, 413-414, 419, 421, 443-447, and Risks 704-707), and line 359's "The message is a body plus one of FR-5's five remedy literals" now matches the `message` production exactly. One residual under-enumeration in that sentence is finding 4.

**The AC-7 / AC-10 cross-references in `requirements.md`.** Both accurate. AC-7's Given inherits AC-6's flat composite, so `:359`'s "This configuration is flat, so the prohibition binds without exception" is correct. AC-10's Given is the nested composite and it asserts only "exactly one `Error`" with no message assertion and no runtime companion, so `:376`'s "this criterion asserts the verdict, not the wording" is correct — and it is what ADR 696 relies on in calling AC-10's nested agreement point "unpinned at runtime".

**Internal counts** — all checked against what is listed: the nine at 363 count nine; "five remedy literals" (359/360), "five templates" (528) and "five ordered, total conditions" (331) match FR-5's T4/T3a/T3b/T1/T2; "Four distinct breaking-change notes" (679) matches step 6 (536-537) and C-8; "Five documented exceptions to NFR-6" (107) matches NFR-6; "five of them are accepted breakage" (620) matches the five C-numbered Negative bullets; "three private statics" (296) matches the table; the five corrections and their targets (488-491) match FR-7 to FR-11. Two enumerations do not match — findings 3 and 4.

**Cross-references.** All resolve to text saying what the referrer claims: 105 → 731-737; 228 → 718-724; 259 → 742/752/761; 286 → 793; 347 → 669-675; 322 → 773; 438 → 659-664; 494 → 738-741; 715 → 654-658; 706 → §3 at 336.

**Grounding re-verified.** `CombinedChannelFactory.cs` has `private readonly IReadOnlyList<IAmAChannelFactory> _factories = factories.ToList();` on a primary-constructor class, exact-equality routing at 34, the throw at 37, and **no `using System;`** — so the ADR's normative shape (194-231) and its "needs `using System;`" (219) are both correct. `grep -rn "FactoryTypes" src/` returns nothing, so the property is genuinely new.

## Findings

### 1. The body's constraint set is still incomplete: AC-5 and AC-30 assert over the whole message, pass the ADR's own inclusion test, and are absent (Score: 64)

The set at ADR 362-364 is the document's stated bound on future body revisions, and line 364 gives the inclusion test explicitly: a criterion belongs if it is "not satisfied by the fixed remedy literals alone, so each is something a revision of the body can break". Two approved criteria pass that test and are not in the set.

AC-5 and AC-30 both assert **byte-identical `Message`** — an assertion over the whole message, body included, which no remedy literal satisfies on its own. A revision of the body can break either. The ADR *says so itself*, two hundred lines earlier: at 187-188 it observes that "Had the rule compared instances (`ReferenceEquals`) or **carried the instance into the message**, the second case would have been fragile" — the second case being a null `options.DefaultChannelFactory`, where the DI path substitutes "a different instance of the same type" (185-186). Carrying anything instance-derived, or anything derived from *whether* the factory came from the subscription or the default, into the body breaks AC-5 while satisfying all nine listed criteria. A concrete example inside the stated licence: revising the handed clause to "but no per-subscription factory is configured, so it will be handed '{F}'" renders differently before and after `DispatchBuilder`'s back-fill and fails AC-5, yet breaches none of the nine.

This is the third consecutive round in which this block's constraint set has been found mis-scoped (round 4 at 65, round 5 at 66). The pattern each time is the same: the criteria a reviewer named were added and the classification rule was not applied exhaustively.

**Evidence**: ADR lines 362-364:
```
362	**The body's constraint set — stated once here, referred to everywhere else.** The body may be
363	revised as long as **AC-7, AC-10a, AC-10b, AC-10c, AC-12, AC-13a, AC-13c, AC-14 and AC-15** continue
364	to hold. None of the nine is satisfied by the fixed remedy literals alone, so each is something a
365	revision of the body can break: …
```
Against `requirements.md:344-347` (AC-5): "Then both evaluations produce identical findings — same count, same `Source`, byte-identical `Message`." and `requirements.md:537-540` (AC-30): "with byte-identical messages across the two runs." The ADR itself classes both as message-level assertions at 555-564 and identifies the body-content choice that would break AC-5 at 187-188.

**Recommendation**: Widen the set to eleven — "AC-5, AC-7, AC-10a, AC-10b, AC-10c, AC-12, AC-13a, AC-13c, AC-14, AC-15 and AC-30" — and add one gloss saying what they constrain, e.g. "AC-5 and AC-30 require the message to be a pure function of `({S}, D, arm, candidate types)`: a body revision must not interpolate an instance, an identity hash, or anything that distinguishes a back-filled `ChannelFactory` from a resolved default." Update "None of the nine" and "Four of those constraints" (finding 3) to match.

---

### 2. `requirements.md`'s amendment record now contradicts itself: "re-opened once" immediately above the second re-opening (Score: 62)

`44231f6f4` appended the second re-opening to the *Amendments after approval* bullet without updating that bullet's opening sentence, which still says the document was re-opened **once**. The two statements are seven lines apart inside the same bullet and cannot both be true. This is the recurring defect class of this series — a revision adds text and leaves a contradicting statement in place — and it is now in the approved requirements artefact rather than in the ADR, which makes it the authoritative record of how many times the approval gate has been re-opened.

**Evidence**: `requirements.md:549` and `:556`:
```
549	- **Amendments after approval.** This document was approved, then re-opened once and re-approved. The round-2 adversarial review of ADR 0072 found that the ADR was deciding things the requirements owned. …
…
556	  A second, narrower re-opening followed the round-5 review, which found ADR 0072 citing a **D5** that this document had never recorded — and using it to bound an approved acceptance criterion. Restricted to exactly that:
```
`git show 44231f6f4` confirms line 549 is untouched by the amendment (9 insertions, none in that sentence).

**Recommendation**: One-word fix in the convention the bullet already uses: "This document was approved, then re-opened **twice** and re-approved. The first re-opening followed the round-2 adversarial review of ADR 0072, which found that the ADR was deciding things the requirements owned. …".

---

### 3. "Four of those constraints" introduces four bullets that name six of the nine (Score: 40)

Line 412 says four of the set's members are easy to breach accidentally; the bullets that follow name AC-15, AC-13a/AC-13c, AC-10a/AC-10b and AC-7 — **six** of the nine criteria in four bullets. The number counts bullets, but "those constraints" refers to the nine enumerated at 363, so a reader counting criteria gets six. The same mismatch existed in the round-5 text ("Three of those constraints" over three bullets naming five) and was carried forward when AC-7 was folded in.

> *Main-agent correction*: the review agent said the bullets name "seven of the nine". They name six — AC-15, AC-13a, AC-13c, AC-10a, AC-10b, AC-7. The mismatch is real; the figure was wrong.

**Evidence**: ADR line 412 against lines 413-426.

**Recommendation**: "Four of those are easy to breach accidentally, in these groups:" — or drop the count: "These are the easiest to breach accidentally:".

---

### 4. The "ends with" justification names AC-13/13a/13b/13c and omits AC-10a, AC-10b and AC-10c, which carry the same assertion (Score: 38)

Line 359 justifies appending the remedy last by reference to four criteria's "ends with" assertions. Three more criteria — the very ones the round-2 amendment created, and which this sub-section was largely rewritten to accommodate — carry "ends with" assertions that depend on the same append order.

**Evidence**: ADR lines 359-360 ("appended last so AC-13/13a/13b/13c's 'ends with' assertions hold") against `requirements.md:381` ("ends with the T3a literal naming `DeclaredChannelFactory`"), `:386` ("ends with the T3b literal listing both inner factories' display names") and `:391` ("whose `Message` ends with the literal `— add a channel factory to the combined channel factory`").

**Recommendation**: "…appended last so the 'ends with' assertions of AC-10a, AC-10b, AC-10c, AC-13, AC-13a, AC-13b and AC-13c hold."

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 2 |
| 0-49 (Low) | 2 |

**Total findings**: 4
**Findings at or above threshold (60)**: 2
