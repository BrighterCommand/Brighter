# Review: design — 0037-validate-subscription-channel-factory (ADR 0072, round 7)

**Date**: 2026-09-18
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. Consider addressing lower-scored items.

> **Main-agent verification.** All four findings were checked against the working tree; **all four
> hold**, and unlike rounds 5 and 6 no supporting claim was wrong. Verified: `grep -c "AC-18"` on the
> ADR returns **0**; the Negative bullet at 678 does read "T1/T2/T3a/T3b" and omit T4; AC-26f's Then
> is "no findings are produced" (`requirements.md:508`) against `:446`'s claim of a second test site
> there; and the T4-filtering rationale does appear twice in one paragraph (439-442 and 447-448).
>
> **The central result was independently re-derived.** The ADR's set at 363-365 was extracted and
> counted mechanically: eleven members — AC-5, AC-7, AC-10a, AC-10b, AC-10c, AC-12, AC-13a, AC-13c,
> AC-14, AC-15, AC-30 — matching the reviewer's independently derived set **member for member**. The
> exclusions were spot-checked at source: AC-13 (`:408`) and AC-13b (`:421`) assert only "ends with
> the literal …", so the remedy discharges them; AC-10's own cross-reference (`:376`) says it
> "asserts the verdict, not the wording"; AC-26f (`:508`) produces no message at all. After three
> consecutive rounds of omission (round 4 at 65, round 5 at 66, round 6 at 64), **the set is
> correctly scoped**.
>
> Also confirmed: no gateway AC in the AC-25/AC-26 family asserts a `Message`, which is what makes
> finding 3 a real defect rather than a wording preference.

## Round 6 disposition

**1. (64) The body's constraint set is incomplete — AC-5 and AC-30 absent — FIXED.** `58c3a35e0` widens the set to eleven at ADR 363-365. "eleven" is now the only count word used of it — `grep -n "nine"` returns only line 400, "nine reachable cells", which is the settled FR-5 figure, not the set. The recommended purity gloss is present at 368-373 as the leading bullet, and all eleven members are glossed (368-373 AC-5/AC-30; 374-375 AC-7; 376-377 AC-10a/AC-10b/AC-10c; 378-379 AC-13a/AC-13c; 380 AC-12/AC-14/AC-15) — no member lost its gloss in the restructuring.

**2. (62) `requirements.md`'s amendment record says "re-opened once" above the second re-opening — FIXED.** `a8f089469` rewrites `:549` to "re-opened **twice** and re-approved", naming the round-2 re-opening as the first. The second still stands at `:556`; the two are now consistent. A pure wording correction — no requirement, Given/When/Then or decision touched.

**3. (40) "Four of those constraints" introduces four bullets naming six criteria — FIXED** by the second recommended route. Line 422 now reads "**These are the easiest of them to breach accidentally:**", the count dropped. The four bullets name AC-15, AC-13a/AC-13c, AC-10a/AC-10b and AC-7 — six of the eleven — and make no numeric claim. Every criterion cited is a member of the eleven; none from outside leaks in.

**4. (38) The "ends with" justification omits AC-10a/AC-10b/AC-10c — FIXED as recommended.** ADR 359-361 now names all seven, and all seven carry an "ends with" assertion in `requirements.md` (`:381`, `:386`, `:391`, `:408`, `:413`, `:421`, `:426`). Seven named, seven exist — the enumeration is total.

**Net: four FIXED, none regressed.** The round-6 diff touches only those three ADR passages; no decision text was altered.

## Independent derivation of the body's constraint set

Every acceptance criterion in `requirements.md` was enumerated without consulting the ADR's list, then the ADR's own inclusion test applied — *is the criterion satisfied by the fixed remedy literals alone, given the fixed `message : {body} {remedy}` production?* If no, it belongs.

**No message assertion at all → excluded.** AC-1 (`:327`, count/`Severity`/`Source` only), AC-2, AC-3, AC-4, AC-6, AC-8, AC-9, AC-11, AC-16, AC-17, AC-17a, AC-19, AC-20 to AC-24, AC-25(a-e), AC-26(a-e), AC-27, AC-29, AC-31. AC-26f (`:505-508`) is in this group — its Then is "no findings are produced", so there is no message to constrain. AC-18 (`:468`) asserts a *different* rule's message (`HandlerRegistered`'s). AC-28 (`:526`) constrains FR-12's predicate reason, which ADR 38-39 defers to ADR 0073.

**AC-10 → excluded, and the requirements say so explicitly** (`:376`: "this criterion asserts the verdict, not the wording").

**Only "ends with ⟨a normative remedy literal⟩" → excluded.** AC-13 (`:408`) and AC-13b (`:421`). Both are wholly discharged by the remedy literal plus the fixed append order, which the `message` production fixes and the body licence does not reach. Their `{D}`/`{F}` display names constrain `DisplayName`, not the body — the ADR treats that formatter separately (§3, 349-354, pinned by AC-12/AC-14/AC-15).

**Pass the test → included.** Two categories, matching the ones previously misclassified:

*Whole-message assertions (a prohibition or containment ranging over body + remedy):*
- **AC-7** (`:357`) — "does not name `Paramore.Brighter.CombinedChannelFactory` as the type the subscription will be handed". Its *positive* half is delivered by T3b's `{F-list}`, but the prohibition is whole-message → belongs.
- **AC-10a** (`:381`) — requires the literal `no ChannelFactoryType` (no remedy template contains it) *and* prohibits `configure a channel factory of type` message-wide.
- **AC-10b** (`:386`) — requires `no ChannelFactoryType`.
- **AC-10c** (`:391`) — prohibits `is one of:` message-wide.
- **AC-12** (`:403`) — requires the subscription's own runtime display name, which appears only in the body's `{S}`.
- **AC-13a** (`:413`), **AC-13c** (`:426`) — "contains **no** occurrence of the substring `configure a channel factory of type`".
- **AC-14** (`:431`) — no `Version=`, `Culture=`, `PublicKeyToken=` or backtick-arity suffix anywhere in the message.
- **AC-15** (`:436`) — the `(?<![.\w])ChannelFactory(?!Type)` regex must find no match anywhere in the message.

*Determinism/purity assertions over the whole message:*
- **AC-5** (`:347`) — "byte-identical `Message`" either side of the back-fill.
- **AC-30** (`:540`) — "byte-identical messages across the two runs".

**Derived set: AC-5, AC-7, AC-10a, AC-10b, AC-10c, AC-12, AC-13a, AC-13c, AC-14, AC-15, AC-30 — eleven.** Exactly the ADR's list at 363-365, member for member. **No criterion belongs and is missing; no listed criterion fails the test.**

**The leading gloss's tuple.** ADR 368 requires the message to be "a pure function of `({S}, D, arm, candidate types)`", checked against the body grammar (387-398) and FR-5's five templates (`requirements.md:136-142`):

- *Anything the body legitimately renders outside the tuple?* No. The body renders `{S}`, `{D}`, `{F}` (= `candidates[0]`; in the direct-arm null-F case a `typeof`, per ADR 155-156), `{F-list}` (= candidates joined), and fixed prose. The remedy renders `{D}`, `{F}`, `{F-list}` only. Nothing needs `Subscription.Name` — FR-5's four items (`:121-124`) do not require it, and the name lives in `Source`. Nothing needs the factory *instance*.
- *Anything in the tuple the body must not use?* No. All four elements are invariant across `DispatchBuilder`'s back-fill and across two runs, so purity in the tuple **entails** AC-5 and AC-30 rather than conflicting with them.
- The tuple is slightly *stronger* than AC-5/AC-30 strictly demand (a body rendering `subscription.Name` would also be invariant), but it errs toward safety and cannot license a breach. `arm` is genuinely needed as a separate element: a direct arm with one candidate is otherwise indistinguishable from a combined arm with one candidate — the same reason ADR 308-310 gives for `IsCompatible` taking the arm explicitly.

The tuple is correct.

**Restructuring scrutiny.** The bullet rewrite introduced no inconsistency: no gloss dropped, no non-member cited in the "easiest to breach" list, no new duplicate created. Downstream passages (391-399, 402-412, 419-420, 443-449, 453-458, 716-717) all still use `body` in the narrow sense fixed by the `message`/`body` split, and Risks 716-717 continues to point at §3 rather than restating the set.

**Grounding and counts re-verified independently.** `TypeExtensions` and `ReflectionExtensions` are both `internal`; `IsAssignableTo` sits inside `namespace System;` guarded by `#if NETSTANDARD2_0`, so ADR 518-520 is right. `InternalsVisibleTo`'s only occurrence in `src/` is the comment at `SpannerBoxMigrationRunner.cs:131`, as ADR 782 claims. `PipelineDiagnosticWriter.cs` renders bare `Type.Name` at 123, 126, 129, 143, 155, 161. `Specification.cs` has exactly three constructors (83, 95, 107). `ConsumerValidationRules.cs` has exactly the four named rules (46, 99, 114, 142). `ServiceCollectionExtensions.cs` has exactly four `AddSingleton<ISpecification<Subscription>>` registrations (201, 207, 213, 215), so "a fifth" is right. `Subscription.cs:172` is `public virtual Type ChannelFactoryType => typeof(InMemoryChannelFactory);`. All four `.agent_instructions/testing.md` quotes are verbatim (`:103`, `:104`, `:111`, `:117`, under `## Test Scope and Isolation` at `:98`). Counts: eleven ✓; five remedy literals ✓; four release notes ✓; five NFR-6 exceptions ✓; three private statics ✓; five corrections and targets ✓; "nine reachable cells" ✓.

## Findings

### 1. The Implementation Approach's per-step acceptance-criteria map omits AC-18 (Score: 42)

The Implementation Approach assigns criteria to steps, and AC-18 is assigned to none. It is an FR-6 criterion in the same "Severity and blocking" group as AC-16/AC-17/AC-17a and needs the same host harness, so step 4 is where it belongs. `grep -n "AC-18"` on the ADR returns nothing — it appears nowhere in the document. Its *substance* is covered (line 620: "no change to the four existing rules' severity, `Source`, `Message` or blocking behaviour (FR-6)"), which is why this is low rather than a gap in the decision; but an implementer using the step list as the coverage map will miss it. AC-30 is likewise unassigned to a step, though it is at least named in Testing Strategy (573-574).

**Evidence**: ADR 539-541:
```
539	   AC-1 to AC-13c and AC-19 all exercise this against C-9's doubles with no container and no host.
540	4. **Registration** in `RegisterConsumerValidationSpecs`. AC-16, AC-17 and AC-17a then exercise the
541	   host-start behaviour in `tests/Paramore.Brighter.Extensions.Tests`, …
```
Against `requirements.md:465-468` (AC-18, FR-6, "When `ValidatePipelines(throwOnError: true)` runs" — a host-level Given).

**Recommendation**: In step 4, "AC-16, AC-17, AC-17a and AC-18 then exercise the host-start behaviour…", and add AC-30 to step 3's list.

---

### 2. The Negative bullet on message pinning lists four of the five normative literals, omitting T4 (Score: 42)

The ADR states five times that there are exactly five normative remedy literals (359-360, 331, the §3 heading at 337, 538). The Negative consequence recording the resulting maintenance cost names only four. T4 is pinned by AC-10c's "ends with the literal `— add a channel factory to the combined channel factory`" (`requirements.md:391`) exactly as the other four are, so it carries the same "effectively public API" cost. This is the same under-enumeration class the last two rounds found at lines 359 and 422.

**Evidence**: ADR 678-680:
```
678	- **The finding message is pinned by literal assertions.** T1/T2/T3a/T3b and AC-15's token rule mean
679	  the message text is effectively public API. Improving the wording later breaks tests, and AC-15's
680	  `ChannelFactoryType` carve-out is a constraint future editors will not guess.
```
Against ADR 359-360 ("one of FR-5's five remedy literals") and `requirements.md:138` (T4's literal).

**Recommendation**: "T1/T2/T3a/T3b/T4 and AC-15's token rule…", or "all five remedy literals and AC-15's token rule…".

---

### 3. `requirements.md:446` claims AC-26f's test sites assert the AC-15 token regex, but AC-26f produces no message to assert it against (Score: 40)

AC-15's normative regex assertion is said to have two test sites, the second being "for AC-26f, the gateway projects". But AC-26f's Then is "no findings are produced" — there is no `Message` in that scenario, so the regex has nothing to run against and the assertion is vacuous or unwritable at that site. The two statements cannot both be operable as written. Pre-existing (it arrived with the round-2 amendment) and in `requirements.md` rather than the ADR, but the ADR relies on the claim at 536-537 and 539.

*Main-agent note*: no AC in the AC-25/AC-26 gateway family asserts a `Message` at all — their Thens are an assignability check, "exactly one inner factory is selected", and "no findings are produced" — so there is no criterion the second site could be re-pointed at. The fix is to drop it.

**Evidence**: `requirements.md:446`:
```
446	and MUST find no match. Two test sites assert this rule (`Core.Tests` and, for AC-26f, the gateway projects); writing it any other way is a defect in the test, not in the message.
```
Against `requirements.md:505-508` (AC-26f): "Then no findings are produced."

**Recommendation**: Drop the second test site — "Asserted in `Core.Tests`".

---

### 4. The nested-composite paragraph states its T4-filtering rationale twice (Score: 32)

Within one paragraph the reason for not filtering the inner `CombinedChannelFactory` out of `{F-list}` is given in full twice, six lines apart, in near-identical wording. Pre-existing and self-consistent — redundancy, not contradiction — but it is the kind of restatement the round-5 fixes were meant to remove document-wide.

**Evidence**: ADR 441-442 and 447-448:
```
441	though that type does not route, because filtering it out would empty the candidate set and select
442	T4, whose wording says less about what is configured. …
…
447	distinction bites exactly here. Filtering the nested type out would empty `{F-list}` and select T4,
448	which says *less* about what is configured, so the rendering is left alone.
```

**Recommendation**: Delete the second sentence (447-448); the first already carries the reason, and 449's pointer to Negative carries the consequence.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 0 |
| 0-49 (Low) | 4 |

**Total findings**: 4
**Findings at or above threshold (60)**: 0
