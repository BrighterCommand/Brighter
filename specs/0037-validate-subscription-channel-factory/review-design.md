# Review: design — 0037-validate-subscription-channel-factory (ADR 0073, round 8 — narrow: round-7 fixes)

**Date**: 2026-09-22
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. The round-7 fixes hold.

> **Scope.** Deliberately narrow: this round reviewed only the four round-7 fixes and what they touch, not
> the whole ADR. It exists because **a fix had introduced an at-threshold defect in every round from 3 to
> 7 without exception**, three of them assertions that cannot fail. Its one question was whether round 7's
> relocation of the constructed generic base produced a fourth. **It did not.**
>
> **Threshold trend: 7 → 4 → 7 → 8 → 8 → 5 → 2 → 0.** All five findings below were applied as an
> editorial pass in `8b2f9e4e3` rather than deferred.

## Findings

### 1. Step 2's `{Base<>, Derived}` / `{Base<>}` reads as a whole-assembly set assertion, which the subject-scoped convention forbids (Score: 55)

Step 2 stated the failure mode as a set comparison. But the `Core.Tests` sweep is over the whole
`Paramore.Brighter.Core.Tests` assembly, which the ADR itself says holds `MockSubscription`, ADR 0072's
four `Validation/TestDoubles/` subscriptions, this design's three doubles, the other pair, the closing-path
generic, the throwing double and the bad-constraints generic. The reported set for that sweep is never
`{Base<>}`. Read pair-locally the sentence is correct and the fault is still detected, so this is wording,
not a design defect — but it is the one new sentence in the document that describes an assertion, and it
describes it in a form the document elsewhere rules out. Related: the convention names
`result.Single(e => e.Subject == typeof(X))` as the idiom, which cannot express the assertion this pair
needs (the *absence* of an entry).

**Evidence**: "a broken reduction leaves `Derived` unsubsumed and the asserted set becomes
`{Base<>, Derived}` instead of `{Base<>}`." Against: "**The `Core.Tests` cases sweep the whole assembly and
assert subject-scoped.** … each assertion selects the entry it is about —
`result.Single(e => e.Subject == typeof(X))`."

**Recommendation**: Restate subject-scoped — no entry has `Subject == typeof(Derived)`, with
`typeof(Base<>)` present — and note that an absence assertion is the one case reading the filtered sequence
rather than `Single(...)`.

---

### 2. Step 2's false lead clause survives with the correction appended to it (Score: 52)

Fix 2 corrected the three sites claiming no shipped assembly contains either subsumption shape. At the
step-2 site the correction was *appended* to the false clause rather than replacing it, producing a
sentence that contradicts itself across its own em-dash. Net information right, no implementer would build
a different set of synthetics — but it is the same "stale clause left standing" pattern seven consecutive
rounds have flagged, in the passage the fix edited.

**Evidence**: "**Five** further synthetics are added here, because no shipped assembly instantiates any of
these shapes — the *no-override* subsumption shape is universal among the twelve, but its
constructed-generic-base variant, and every other shape below, has no shipped instance:"

**Recommendation**: Replace the lead clause rather than qualifying it.

---

### 3. Whether the no-override pair's `Base<T>` declares its own override is unspecified (Score: 35)

The pair is specified as `Derived : Base<Command>`, `Derived` declaring nothing. Nothing says whether
`Base<T>` declares an override. Both choices leave the subsumption assertion working (`Base<>` is a root
candidate either way, since no `Subscription` ancestor lives in `Core.Tests`), but they differ in what
`Base<>`'s entry carries — a null `Reason` if it declares a sound factory, an **inherited-default** reason
if it declares nothing — and if it does declare, the pair silently duplicates the dedicated closing-path
synthetic.

**Recommendation**: `Base<T>` declares a sound override, reusing the existing sound `IAmAChannelFactory`
double, so `Base<>`'s entry carries a null reason and the assertion is about subsumption only.

---

### 4. "The ancestry comparison runs only when …" is an evaluation-order claim; the load-bearing claim is stronger (Score: 28)

Step 2 justified the pair choice by saying the ancestry comparison *runs* only when the derived type
declares no override. True of a short-circuiting `&&` but not of an implementation that computes the
ancestor match first and conjoins afterwards. The conclusion is unaffected — on the declares-its-own pair
the match is not load-bearing, because conjunct 1 is false — so the justification is safest stated that way.

**Recommendation**: "…is load-bearing only when the derived type declares no override of its own: on the
*declares-its-own* pair the first conjunct is already false…"

---

### 5. Fix 4's split leaves a short orphan first line (Score: 20)

The `SharedGenerator` paragraph opened with a half-width line, an artifact of splitting without
re-wrapping. Nothing was lost.

**Recommendation**: Re-wrap.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 2 |
| 0-49 (Low) | 3 |

**Total findings**: 5
**Findings at or above threshold (60)**: 0

## The subsumption-pair walk

The pair as specified: `Base<T> : Subscription` (concrete, generic), `Derived : Base<Command>` (concrete,
non-generic, declaring no `ChannelFactoryType`). Swept assembly: `Paramore.Brighter.Core.Tests`.

**Step 1, candidates.** `Assembly.GetTypes()` yields the *definition* `typeof(Base<>)` and `Derived` — it
never returns constructions, so `Base<Command>` is never in the candidate set. Both are non-abstract and
both base chains reach `Paramore.Brighter.Subscription`, so both are candidates. Per D10 `Subject` is the
candidate *as discovered*, so the literal for the generic base is `typeof(Base<>)`: consistent with the
ADR's asserted spelling.

**Step 2 on `Derived`, correct reduction.**
- Conjunct 1 — `Derived` declares no `ChannelFactoryType` (`DeclaredOnly` finds nothing): **true**. The
  ancestry comparison is therefore reached. *This is the difference from the round-6 arrangement, where
  conjunct 1 was false and the comparison was never load-bearing.*
- Conjunct 2 — the base chain yields `Base<Command>`, a constructed generic; reduced,
  `Base<Command>.GetGenericTypeDefinition() == typeof(Base<>)`, which is in the candidate set: **true**.
- `Derived` is dropped.

**Step 2 on `Base<>`.** Its chain yields `Subscription` (and `object`). `Subscription` lives in
`Paramore.Brighter`, not the swept assembly, so "a candidate in the same assembly" is false — `Base<>` is a
root and is never dropped. Reported subject under a correct reduction: **`{Base<>}`**.

**Step 2 on `Derived`, broken reduction** (ancestor compared unreduced): `typeof(Base<Command>)` is not
`typeof(Base<>)` and no other chain member is a candidate in `Core.Tests`, so conjunct 2 is **false** and
`Derived` is not dropped. It is then read and reported with a `Reason` matching its base's. Reported
subjects: **`{Base<>, Derived}`**.

**Does the assertion distinguish them?** **Yes.** "The derived is subsumed" is satisfied only in the first
case. The assertion can fail, and it fails on exactly the fault the constructed generic base was added to
catch. **This is the first of the four "constructed generic base" placements in this ADR that is actually
load-bearing.**

**Third behaviours checked and excluded.** `Base<>` cannot itself be subsumed (no candidate ancestor in the
swept assembly). No alternative candidate ancestor can drop `Derived` under a broken reduction, for the same
reason. D11's inclusive candidacy is inert here — it bites only in `Paramore.Brighter`, where `Subscription`
itself is present. The only way the assertion fails for the wrong reason is an abstract `Base<T>`, and the
non-abstract requirement is stated plainly in step 1 — a build-time trap, not a design gap.

## Round-7 fixes verified

- **Fix 1 — constructed generic base moved to the no-override pair**: **complete and substantively
  correct.** The walk confirms it. Stated at all three sites with the reason, consistently; no site still
  attaches it to the declares-its-own pair. Two wording imprecisions (findings 1, 4), neither blocking.
- **Fix 2 — shipped-shape fact restated**: **correct and verified against the codebase**, incomplete at one
  site. All twelve pairs are `X<T> : X` with a **non-generic** base — no constructed generic base ships.
  All nine shipped overrides are on the non-generic base, so **no generic type declares its own override**
  and the declares-its-own pair has no shipped instance. All twelve generic derived types declare nothing —
  the no-override shape is universal, including in AWSSQS, AWSSQS.V4 and Postgres where the base declares
  nothing either and is still the single root subject. The step-2 site retained its false lead clause
  (finding 2).
- **Fix 3 — bad-constraints justification**: **complete and coherent.** The blind spot is located where it
  lives — in the generated sweeps, whose expectation comes from configuration — and the remedy follows. It
  no longer offers a reason that describes the synthetic itself, and does not contradict the subject-scoped
  convention.
- **Fix 4 — step 4 paragraph split**: **complete.** No text lost; ragged first line only (finding 5).

## Approval recommendation

**Approve.** The round exists to answer one question — whether fix 1 produced a fourth assertion that
cannot fail — and the answer is no. Fix 2's corrected fact is verified true against all twelve shipped
gateways. Fixes 3 and 4 are clean.

Five findings, all below threshold, none design-changing: two Medium wording items and three nits. Worth a
single editorial pass, but they do not warrant another review round, and none would cause two developers to
build different tests.
