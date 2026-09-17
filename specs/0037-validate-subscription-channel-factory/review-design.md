# Review: design — 0037-validate-subscription-channel-factory (ADR 0072, round 4)

**Date**: 2026-09-17
**Threshold**: 60
**Verdict**: NEEDS WORK

5 findings at or above threshold 60. Address these before approving.

> **Main-agent verification.** Per the standing rule that agent findings are claims, not facts, all
> eight were checked against the working tree before this file was written. **All eight hold; none
> was rejected** — the second consecutive round with no rejections, where rounds 1 and 2 each had
> several collapse on inspection. Specifically verified: C-8 requires **four** release notes
> (`requirements.md:221`) against the ADR's three at lines 511-512 and 654-655; AC-10b's `Message`
> assertion (`requirements.md:379`) is a **body** assertion absent from both licence lists (357-359,
> 677-679); the Forces claim at 103-106 is unqualified where Alternatives at 705-708 says "eleven of
> twelve"; the three deleted citations by count across the tidy commit — `NFR-2` **1 → 0**,
> `testing.md` **4 → 1**, `item 3` **1 → 0**; FR-5 item 3's text (`requirements.md:123`) does
> quantify over "every inner factory in the candidate factory set", so the vacuous-satisfaction claim
> **is correct** but appears nowhere in the ADR; `decider` is absent from
> `design_principles.md:15`'s stereotype list; and `DisposingSpecification` appears at both 286 and
> 760. Summary counts recomputed by hand and match.

## Round 3 disposition

1. **(74) The two message-body templates are not total — FIXED.** Lines 361-376 replace the pair of fixed templates with two independently varying clauses (`{declared-clause}` on `D`; `{handed-clause}` on arm × candidate set). Totality checked by hand: 2 declared forms × 3 handed forms covers all nine reachable cells. Every cell was rendered and checked: **AC-10a** (direct, null `D`) contains `no ChannelFactoryType` ✓ and no `configure a channel factory of type` ✓; **AC-10b** (combined, null `D`) ✓; **AC-10c** (empty set) renders `will be handed no channel factory at all — add a channel factory to the combined channel factory`, containing no `is one of:` ✓; **AC-12** (`{S}`) ✓; **AC-13/13a/13b/13c** "ends with" holds because the remedy is appended last ✓; **AC-14** holds via `DisplayName`'s backtick strip ✓; **AC-15**'s regex `(?<![.\w])ChannelFactory(?!Type)` finds no match in any of the six renderings ✓. The fix is sound.
2. **(64) "four remedy literals/templates" in five places — FIXED.** `grep -n "four\|Four"` now returns six hits (78, 114, 221, 469, 584, 787), every one a correct use ("four existing rules", "four things", "four families of host"). The Risks mitigation at 677-679 now reads "five remedy literals" and has gained AC-10a and AC-10c — though the list is still incomplete; see finding 2.
3. **(62) The AC-7 gloss vs the nested paragraph — FIXED.** Lines 399-402 now read "The rule does name it when an inner factory is itself a `CombinedChannelFactory`; AC-7's configuration has no nesting, so the criterion holds, and that case is the accepted limitation below." Reconciled, not re-scoped, exactly as round 3 asked.
4. **(55) The worked example's invented namespace — FIXED.** Line 412 now reads `Paramore.Brighter.Core.Tests.Validation.TestDoubles.DeclaredChannelFactory`.
5. **(52) The prescribed XML doc omits the thread-safety statement — FIXED.** Lines 203-208 add a `<remarks>` block, and line 234 points at it ("as its `<remarks>` states"). `<remarks>` usage matches `.agent_instructions/documentation.md:29`.
6. **(45) `Arm` never defined — FIXED.** Lines 304-305: "`Arm` is a private nested `enum { Direct, Combined }` on `ConsumerValidationRules`, and `ResolveCandidates` returns `(Arm, IReadOnlyList<Type>)`. Neither is public surface."
7. **(42) "Two lexical constraints" with three bullets, one stranded — FIXED.** Line 392 says "Three", and all three bullets are adjacent at 393-404.

All seven round-3 findings are fixed. No regression was introduced by `6bfa7925b`.

## Tidy assessment

**Verdict on the hypothesis: the tidy half-worked. It eliminated the duplication it named, created no new contradiction, but deleted content it promised only to move, and left several duplicated facts untouched.**

**What it genuinely fixed.** Key Components §1's three rejected alternatives (instances, `CanRoute`, `InternalsVisibleTo`) are now stated once, in Alternatives Considered, with a one-line pointer at 259-260 whose count ("Three narrower or wider alternatives") matches the three bullets. The rejected "skip a null `ChannelFactoryType`" option became its own Alternatives bullet (740-744), and Key Components §2's pointer to it (line 322) resolves. `CanRoute`'s secondary pre-flight cost and the `InternalsVisibleTo` scope nuance did join their Alternatives bullets as claimed. AC-6's lack of a runtime counterpart did join the drift risk (667-671), replacing a genuinely dangling cross-reference ("for the reason given in the Decision") with the reason inline. **No dangling cross-reference was introduced by the move**: "Recorded under Risks" (228), "see Performance" (190), "see below" (167), "recorded under Negative" (345, 415), "see Alternatives Considered" (322), "the re-enumeration reason above" (500), "the CS0236 rule described above" (693) all still resolve.

**What it lost.** The commit claims "Nothing was deleted outright: content that existed only in Key Components moved out rather than being dropped." That is false in three places (finding 4). Most seriously, the ADR's *only* citation of **NFR-2** was in a Key Components sentence the tidy deleted; NFR-2 is now untraced anywhere in a 794-line design document whose largest section is about message quality.

**Where duplication was relocated rather than eliminated.** The tidy's own diagnosis still applies to at least four facts it did not touch — and finding 3 is an *already-drifted* pair of exactly that kind, surviving in the document today.

**Compression-induced imprecision**: one case, finding 3.

## Findings

### 1. C-13's release note is dropped from the implementation plan and from the release-note count, contradicting two other passages and requirements C-8 (Score: 78)

Requirements **C-8** is explicit that the release notes MUST carry **four** breaking-change notes: "the breaking-change notes for **C-10** …, **C-11** …, **C-12** … and **C-13** (an out-of-repo override returning `null` in a single-factory configuration), each naming its symptom and its remedy." C-13's own Obligations paragraph repeats it.

The ADR agrees with that twice — and then contradicts itself twice. The **Implementation Approach**, which is the artefact an implementer will actually work from, enumerates only three notes. The **Negative** consequences bullet counts only three. An implementer following step 6 ships three notes and breaches C-8.

**Evidence**: ADR lines 511-512:
```
6. **Release notes** for C-8's obligations — C-10, C-11 and C-12 as separate entries, with C-12
   flagged as the one `throwOnError: false` does not avoid.
```
and lines 654-655:
```
- **Release-note burden.** Three distinct breaking-change notes (C-10, C-11, C-12) for one feature is
  a lot to ask a reader of V10.X notes to absorb.
```
Against the ADR's own line 633 — "*but it is new breakage, not a converted failure, and it **carries its own release note***" — and lines 688-689 — "*it is recorded as **C-13** with **its own release-note obligation under C-8**, as NFR-6 requires*". Against requirements C-8 (`requirements.md:221`) and C-13's Obligations (`requirements.md:304`).

**Recommendation**: Change step 6 to "C-10, C-11, C-12 and C-13 as separate entries, with C-12 flagged as the one `throwOnError: false` does not avoid and C-13 flagged as one it does", and change the Negative bullet to "Four distinct breaking-change notes (C-10, C-11, C-12, C-13)".

---

### 2. The licence to revise the message body names five of the seven acceptance criteria that constrain it, and says so twice (Score: 65)

The ADR draws a deliberate line between the normative remedy literals and the body wording it owns, and attaches to the body a closed list of criteria that must keep holding. That list is the document's contract with future editors — round 3 scored the same sentence at 64 for exactly this reason, and the fix added AC-10a and AC-10c but stopped there. Two more criteria constrain the body and are still missing:

- **AC-10b** requires the combined-arm null-`D` message to "contain the literal `no ChannelFactoryType`". That is a body assertion, not a remedy assertion — T3b contains no such literal.
- **AC-13a and AC-13c** require that the message "contains **no** occurrence of the substring `configure a channel factory of type`". That is a prohibition on the *body*, which is why the ADR lists it as one of its three lexical constraints three paragraphs later.

So the ADR's own §3 names AC-13a/AC-13c as a body constraint while its summary sentence and its Risks mitigation license revising the body without them. Under the stated licence a future editor could rewrite the handed clause as "but the configured channel factory of type X will be handed to it" and break AC-13a/AC-13c, or drop the null-`D` declared clause in the combined arm and break AC-10b — both while believing they were inside the licence.

**Evidence**: ADR lines 357-359:
```
**The message is a body plus one of FR-5's five remedy literals**, appended last so AC-13/13a/13b/13c's
"ends with" assertions hold. Only the five literals are normative; the body below is this ADR's,
constrained by AC-10a, AC-10c, AC-12, AC-14 and AC-15.
```
and the same list repeated at lines 677-679. Against ADR lines 403-404 ("*AC-13a and AC-13c forbid the substring `configure a channel factory of type` anywhere in a T3a/T3b message, so **the body must not** paraphrase the suppressed half*") and requirements AC-10b (`requirements.md:379`).

Note that the list appearing in two places is itself the tidy's failure mode: the round-3 fix had to patch both copies, and both are now wrong in the same way.

**Recommendation**: Make both read "AC-10a, AC-10b, AC-10c, AC-12, AC-13a, AC-13c, AC-14 and AC-15", or — better, given the drift history — state the list once and have the Risks mitigation refer to it.

---

### 3. Forces states an unqualified "certain failure on every start" that C-11 and the qualified duplicate in Alternatives both contradict (Score: 64)

The same argument — why this ADR chooses `Error` where ADR 0064 chose `Warning` — is stated twice. The Alternatives copy carries the qualifier that makes it true; the Forces copy does not, and the unqualified version is false of MQTT, the one transport the ADR devotes an entire Negative bullet to precisely because its mismatch is **neither** a failure **nor** a wrong bus.

C-11 (`requirements.md:280`) is unambiguous: "`new Subscription<T>(…)` handed the MQTT `ChannelFactory` consumes MQTT **correctly today** — it is not a latent failure like the eleven downcasting transports (C-3)." The ADR's own Negative bullet at 611-617 repeats it. So a channel-factory mismatch is *not* "a certain failure … on every start", and the parenthetical escape hatch does not rescue it either: MQTT with a plain `Subscription<T>` consumes the *right* bus, not a silently wrong one.

**Evidence**: ADR Forces, lines 103-106:
```
  the five corrections ship with it. This is the direct inverse of ADR 0064, which chose a
  non-blocking `Warning` for its two rule families; the difference is that 0064's conditions are
  deferred and conditional, whereas a channel-factory mismatch is a certain failure (or, worse, a
  silently wrong bus) on every start.
```
Against ADR Alternatives, lines 705-708, which states the same argument correctly ("*fails on **every** start for eleven of twelve transports, or silently consumes the wrong bus*"), and against ADR lines 611-613 and requirements C-3/C-11.

**Recommendation**: Add the qualifier to the Forces bullet ("…fails on every start for eleven of twelve transports, or silently consumes the wrong bus; MQTT, the twelfth, is C-11"), or delete the Forces copy and let the Alternatives bullet carry the argument — which is what the tidy did everywhere else.

---

### 4. The tidy deleted three requirement and project-rule citations that existed only in Key Components, contrary to its own "nothing was deleted outright" claim (Score: 62)

`git diff 6bfa7925b 8d03b94c6` shows these three citations removed from Key Components and added nowhere:

**(a) NFR-2 — now uncited anywhere in the ADR.** `grep -c -- "NFR-2"` returns 0 post-tidy and 1 pre-tidy. The deleted sentence was the ADR's sole trace to the requirement that governs its largest section.

**(b) `.agent_instructions/testing.md` § *Test Scope and Isolation* — dropped from two decisions.** It grounded the rejection of `IReadOnlyList<IAmAChannelFactory> Factories` and the decision to keep `DisplayName` a private static. Both now rest on unsupported assertions — "A formatter with one caller does not earn permanent public surface" (line 344) and "it hands out capability … where the rule demonstrably needs only knowledge" (line 717). The project rule is real and says exactly what the deleted text quoted (`testing.md:103-104`). CLAUDE.md treats these files as rules to consult, not to reason around; the `InternalsVisibleTo` bullet still cites testing.md, so the document is now inconsistent about whether narrow-surface decisions are grounded in the rule or in taste.

**(c) FR-5 item 3 — now uncited.** The deleted sentence was the ADR's only demonstration that the rule needs *types*, not *instances*: "FR-3's combined arm is `f.GetType() == D`, **FR-5 item 3 needs display names of types**, and AC-9's companion assertion calls `CreateSyncChannel` on the **composite**, not on an inner factory." That enumeration is the evidence for "No caller in this feature needs an instance", which is all the Alternatives bullet now asserts.

**Evidence**: removed hunks in `git diff 6bfa7925b 8d03b94c6`; counts verified independently — `NFR-2` 1 → 0, `testing.md` 4 → 1, `item 3` 1 → 0. Against the tidy commit message: "*Nothing was deleted outright: content that existed only in Key Components moved out rather than being dropped.*"

**Recommendation**: Restore the three citations in their new homes — NFR-2 in the `{F-list}`/T4 paragraph (417-420) or in Risks; the testing.md § *Test Scope and Isolation* quote in the "Expose `Factories`" Alternatives bullet and in §3's private-static sentence; FR-5 item 3's role in the "Expose `Factories`" bullet.

---

### 5. The reasoning that the empty-candidate body clause needs no FR-5 amendment exists only in the commit message, not in the ADR (Score: 60)

`6bfa7925b`'s commit message says: "*FR-5 item 3 is satisfied vacuously by the empty set (zero types), so no requirements amendment is needed.*" **The claim was checked and it holds.** FR-5 item 3 reads "the type(s) it will actually be handed … in the combined arm the `Type.FullName` of **every inner factory in the candidate factory set**" — a universal quantification, vacuously true over an empty set. That is materially different from FR-5 item 2, which named a single required type and *was* therefore unsatisfiable when `D` is null and *did* require an amendment. So no requirements change is needed here.

But the ADR does not say any of that. `grep -n "item 3"` on the ADR returns nothing (the tidy deleted the only hit — finding 4(c)), and the nearest thing to a statement, line 386, argues the wrong point: it explains why the body cannot render `one of ''`, which is an AC-10c concern, not an FR-5-item-3 concern. Meanwhile the very next section goes out of its way to declare exactly this kind of interaction: "**This changed the requirements, and the requirements were amended rather than deviated from**" (line 324). A reader comparing FR-5 item 2's explicit null clause against item 3's silence about the empty set will reasonably ask whether the ADR quietly took a second normative decision — which is precisely what round 2 caught it doing. A decision record whose justification lives in a git commit message is not a decision record.

**Evidence**: ADR lines 361-363 and 386. Note that line 362 attributes the empty candidate set to **T4** — a *remedy* template — never to item 3, which is the item that demands types in the body. Against `requirements.md:123` (FR-5 item 3) and `requirements.md:122` (item 2, which shows what an explicit amendment for this class of gap looks like).

**Recommendation**: One sentence beside the clause table, e.g. "FR-5 item 3 quantifies over the candidate factory set, so an empty set satisfies it vacuously — unlike item 2, whose single required type forced the round-2 amendment. `will be handed no channel factory at all` is therefore body wording this ADR owns, not a deviation." This is a documentation fix, not a requirements amendment.

---

### 6. Four facts are still stated in two or more places, which is the failure mode the tidy was performed to remove (Score: 52)

The tidy eliminated the duplication in Key Components §1 but left these standing, three of them straddling the same Key Components / Consequences boundary:

- **The `DisposingSpecification` rejection** — Key Components §2, lines 285-286 **and** Alternatives, lines 760-762. This is the same class of duplication the tidy removed from §1, in the very next sub-section.
- **The nested-composite limitation** — Key Components §3, lines 406-415 (ten lines) **and** Negative, lines 636-639 (four lines).
- **C-13's argument** — Risks, lines 683-689 **and** Negative, lines 628-633, both carrying "`CombinedChannelFactory` is the only reader of `ChannelFactoryType` in `src/`" and the D3 acceptance.
- **The D1/D2 "corrections are load-bearing" argument** — Forces 103-106, Key Components §5 465-471, Alternatives 704-710, Alternatives 711-714: four copies. Finding 3 is one of these four having already drifted.

All four are currently consistent. The cost is prospective: each is a place where the next round's fix has to land twice.

**Evidence**: line pairs cited above; `grep -n "C-13"` returns 11 hits across Forces, Key Components, Negative, Risks and Alternatives.

**Recommendation**: Apply §1's pattern to §2 and §3 — state the decision and point at the record. The `DisposingSpecification` sentence in §2 can become "see Alternatives Considered"; §3's nested-composite paragraph can compress to its rendering fact and point at the Negative bullet.

---

### 7. "decider" is not one of the project's stereotypes (Score: 45)

The responsibility table applies Responsibility-Driven Design stereotypes, which `.agent_instructions/design_principles.md:15` enumerates as "information holder, structurer, service provider, coordinator, controller, interfacer". "decider" is not among them. `information holder` and `structurer / coordinator` on the first two rows are correct; the rule's row invents a term. The closest project-sanctioned stereotype for a rule that answers a question about objects it is handed is **service provider**.

**Evidence**: ADR line 247: `| The rule | decider | *deciding* whether those two are compatible **for the purpose of validation** |`, against `.agent_instructions/design_principles.md:15`.

**Recommendation**: `| The rule | service provider | *deciding* whether those two are compatible … |`, keeping the *deciding* verb in the responsibility column where it belongs.

---

### 8. "cannot be written with the approved double set at all" overstates what the doubles forbid (Score: 42)

The Risks entry's conclusion — the positive direction should be pinned when a non-throwing double exists — is right, and C-9 does make a "the composite successfully routes" assertion impossible. But a weaker positive assertion *is* writable today: `CreateSyncChannel` on a correctly-configured composite throws the double's own exception rather than `ConfigurationException`, which pins that routing selected an inner factory. C-9 requires the doubles' members to throw but does not fix the exception type, so the distinction is observable. The ADR's absolute "cannot be written … at all" invites a reader to stop looking.

**Evidence**: ADR lines 667-670: "*C-9's doubles throw on every member, so the mirror-image assertion — a correct multi-bus configuration where the rule is silent **and** the composite successfully routes — **cannot be written with the approved double set at all**.*"

**Recommendation**: Soften to "cannot be written as a successful-routing assertion; the most it can assert is that the composite throws the inner double's exception rather than `ConfigurationException`, which pins selection but not dispatch."

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 1 |
| 50-69 (Medium) | 5 |
| 0-49 (Low) | 2 |

**Total findings**: 8
**Findings at or above threshold (60)**: 5
