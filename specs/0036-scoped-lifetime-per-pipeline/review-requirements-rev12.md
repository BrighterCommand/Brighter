# Review: requirements (revision 12) — 0036-scoped-lifetime-per-pipeline

**Date**: 2026-08-01
**Threshold**: 60
**Verdict**: NEEDS WORK

1 finding at or above threshold 60. Address it before approving.

> Supersedes the revision-11 review. The disposition of all 4 revision-11 findings is recorded below.

## Disposition of revision-11 findings

| # | Rev-11 finding (short title) | Score | Disposition | Justification |
|---|------------------------------|-------|-------------|---------------|
| 1 | The two counting branches D19 exists to rescue still open with "the same host"; preamble over-reaches in the other direction | 75 | **Fixed** | All three recommended edits landed, verbatim in substance. AC-11 branch 3 (`:474`) now reads "**And given** a **second host of the same shape** — a fresh Brighter container, so all three latches start unlatched (**D19**, and the reading note above) — registering the **same** hand-rolled `IAmAScopeProvider` implementation type as the previous branch, configured to vary in one respect". AC-31 branch 2 (`:514`) now reads "**And given** a **second host of the same shape** — a fresh Brighter container, so FR-24.2's latch starts unlatched (**D19**, and the reading note above) — registering the **same** provider implementation type, with the affinity option **`AlwaysNew`**". `grep -no "the same host"` returns **12** lines / **13** hits; `:474` and `:514` are no longer among them. The preamble (`:406`) now carries the distinction: "and so does every branch that **changes the configuration** — a different provider, a different provider behaviour, or a different affinity. A clause that merely **repeats an operation** within a Then — \"a second `PublishAsync`\" (AC-11), \"a second deferred `Send`\" (AC-29), \"a second run of 100 messages\" (AC-20) — runs in the **same** host". Every counting branch was re-derived under the new rule (see Appendix): AC-11 branch 3 records exactly two entries in a fresh container, AC-31 branch 2 is no longer vacuous, and AC-11 `:476`, AC-29 `:573` and AC-20 `:637` all fall on the repeat-operation side and remain passable by a correct implementation. The rev-11 sub-finding about branch 3's provider identity is also discharged: `:474` says "the **same** hand-rolled `IAmAScopeProvider` implementation type as the previous branch", matching `:476`'s "both naming the same provider implementation type". |
| 2 | FR-23's worked example still said "in the same **process**" | 65 | **Fixed** | `:283` now reads "A second such deferred `Send` in the same host logs no further `Warning`." `grep -n "in the same process"` returns **1** hit, `:406`, where the usage ("unaffected by any AC or branch that ran earlier in the same process") is correct. The example is now consistent with FR-23's own rule on the same line ("once per Brighter container per provider implementation type") and with AC-29 `:573`. |
| 3 | Exclusivity paragraph claimed AC-29's and AC-31's counts "simultaneously satisfiable on the same host" | 55 | **Fixed** | `:244` now reads "This is what makes **AC-29's** \"exactly one `LogLevel.Warning`\" satisfiable at all: without it a stale ambient would be reported twice, under FR-23 *and* under FR-24.2, and AC-29's \"no entry naming either of FR-24's other two conditions\" would be false. AC-31 needs no tiebreak — a `null` return is unambiguously FR-24.2's condition and no other rule competes for it." Both claims check out against the ACs: AC-29 `:572` does say "and no entry naming either of FR-24's other two conditions", and AC-31 `:511`'s provider returns `null` (nothing offered), so neither FR-23 nor FR-24.4 can compete under the FR-24.4 → FR-23 → FR-24.2 order stated in the same paragraph. `grep "simultaneously satisfiable"` returns **0** hits. |
| 4 | Two unqualified spellings of the latch quantifier | 38 | **Fixed** | FR-24.2's headline (`:235`) now reads "Logged once at `Warning` **per Brighter container per provider implementation type**, per the diagnostic model below." AC-11 branch 2 (`:473`) now reads "FR-24.4's once-per-Brighter-container-per-provider-implementation-type entry (**D19**)", matching AC-29's spelling at `:574`. `grep -c "per Brighter container"` = **6** (`:235`, `:240`, `:279`, `:282`, `:285`, `:567`), plus the hyphenated forms at `:473` and `:574` and `:242`'s "scoped to the **Brighter container**". |

## Findings

### 1. The document's own revision number is stale: line 7 says "Revision: 11" while the revision-history table, the README and the change itself say 12 (Score: 62)

Revision 12 added row **12** to the revision history (`:808`) describing exactly the four rev-11 fixes, and the README status line reads "revision 12, reviewed ten times". The header at line 7 was not updated, so the document identifies itself as **revision 11** — the revision that was just reviewed and found to have four defects.

This is a self-referential contradiction, not a typo in prose: every review round's disposition table, the review brief, and the README status line are keyed to this number, and the header explicitly points the reader at the revision history ("see [Revision history](#revision-history) at the end of this document") — which immediately disagrees with it. A reader or a future review agent taking the header at face value would read the revision-12 text against the revision-11 change log. `grep -n "revision 1[12]\|Revision.*: 1[12]"` returns exactly **one** hit in the whole file, so this is the only site and the fix is one character.

**Evidence**: Line 7: "**Revision**: 11 — see [Revision history](#revision-history) at the end of this document. Decisions are cited by number throughout (**D0**–**D19**); they are listed in [Decisions](#decisions-d0d19)."
Line 808 (revision-history table, first data row): "| **12** | rev-11 review — 4 findings, 2 at or above threshold, none Critical | **No new decisions.** Both blocking findings were the same shape: **D19 was swept onto the requirements but not onto the Acceptance Criteria or the examples.** …"

**Recommendation**: "**Revision**: 12 — see [Revision history](#revision-history) at the end of this document." Nothing else on the line changes; the `D0`–`D19` range and the anchor are both still correct.

---

### 2. FR-24's diagnostic model says AC-11 and AC-31 "each re-uses one provider implementation type across its branches" — true of AC-31 and of the two AC-11 branches that count, but not of AC-11's first branch (Score: 30)

This is the sentence rev-11 finding 1 asked to be either kept or restricted, and revision 12 kept it by making AC-11 branch 3 re-use branch 2's type (`:474`) — which is the right resolution for the argument the sentence is making. Read literally, though, "each re-uses one provider implementation type **across its branches**" is still false of AC-11 taken whole: branch 1 (`:468`) is "an opted-in ASP.NET application", i.e. the ASP.NET provider's implementation type, while branches 2 and 3 register a hand-rolled type (`:471`, `:474`).

It is a nit rather than a defect because branch 1 asserts no count, so the latch-sharing claim only ever needed to hold of branches 2 and 3, and it does. No test changes either way.

**Evidence**: `:242`: "A latch static to the process would make AC-31's `AlwaysNew` branch vacuous and AC-11's third branch unsatisfiable by a *correct* implementation, since each re-uses one provider implementation type across its branches."
`:471` (AC-11 branch 2): "**And given** the same application except that its ambient source is a hand-rolled `IAmAScopeProvider` reading the same request ambient — registered as the **only** `IAmAScopeProvider` descriptor, in place of the ASP.NET one".

**Recommendation**: "…since AC-31 re-uses one provider implementation type across both its branches and AC-11's third branch re-uses its second branch's." Or simply "…since each re-uses one provider implementation type across the branches that assert a count."

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 1 |
| 0-49 (Low) | 1 |

**Total findings**: 2
**Findings at or above threshold (60)**: 1

The round got the substance right, and the round's dominant risk did not materialise. The new configuration-change / repeat-operation rule at `:406` was the classic setting for this document's signature failure mode — a new rule stated over a family of existing clauses — and it survives the walk: every counting AC and every branch within one (AC-11's three, AC-19, AC-20's two runs, AC-29, AC-31's two) falls on exactly one side of the rule, the side it falls on is the one that makes its count correct, and the trigger list ("a different provider, a different provider behaviour, or a different affinity") is exhaustive over what those branches actually vary. The two rewritten Givens are now implementable and mutually consistent with `:476` and with FR-24's diagnostic model; `:244`'s replacement sentence is accurate against both ACs it names and against the stated evaluation order; and FR-23's example is the last residual of the old quantifier, now gone. Both remaining findings are editorial and neither requires a user **decision** — finding 1 is a one-character correction to the header, finding 2 a clause tightening. **On content, the requirements have converged and are ready to approve once the header is corrected.**

## Appendix — citations verified this round

Read in full: `requirements.md` lines 1–816 (seventeen chunked reads, no gaps), `README.md` (125 lines), and the revision-11 review including its Appendix.

Document-internal sweeps run this round, with actual hit counts:

- `grep -n "per process"` — **6** hits: `:109` ("one artefact per process from the root provider", `Singleton`, correct), `:174` and `:178` (FR-1/FR-2 titles, "not per process", the Defect-1 framing, correct), `:224` ("per pipeline rather than per process", correct), `:242` (four occurrences: "rather than to the process", "indistinguishable from once per process", "a test process builds several", "A latch static to the process" — all D19's own explanatory prose, correct), `:810` (revision history). No normative residual.
- `grep -n "in the same process"` — **1** hit, `:406`, correct usage. (Was 2 in rev-11; `:283` fixed.)
- `grep -n "life of the process"` — **1** hit, `:288` (FR-20, mapper caching — the deliberate survival).
- `grep -n "once per provider"` — **1** hit, `:811`, revision-history prose about revision 9. Not normative.
- `grep -c "per Brighter container"` — **6**: `:235`, `:240`, `:279`, `:282`, `:285`, `:567`; plus `:242`'s "scoped to the **Brighter container**", the hyphenated forms at `:473` and `:574`, and `:784`'s "per host".
- `grep -no "the same host"` — **13** hits over 12 lines. Classified individually: `:279` (FR-18, "a consumer running in the same host" — unrelated), `:283` (FR-23 example, now correct), `:473` and `:476` (AC-11 repeat-operation clauses, correct), `:558` (AC-18, config-changing rebuild — not a `Warning`-count AC, its "exactly one decision" is a recorder count, per-recorder by construction), `:565` (AC-19, "an `IHostedService` in the same host" — unrelated), `:573` (AC-29 repeat, correct), `:589` (AC-27, `throwOnError` rebuild — validation output, per-host by construction), `:615` (AC-42 rebuild, same), `:637` (AC-20 repeat, correct), `:808` ×2 and `:809` (revision history).
- `grep -nio "lifetime scope"` — **2** hits, `:30` (Terms preamble, saying it is not used) and `:800` (prior-art gloss on ADR 0039). Exactly the two stated deliberate survivals.
- `grep -n "LogLevel.Warning"` — **6** hits: `:244`, `:473`, `:476`, `:513`, `:567`, `:572`; plus the bare-`Warning` counts at `:515`, `:574`, `:637`. Every one falls inside AC-11, AC-19, AC-20, AC-29 or AC-31, so the preamble's list of counting ACs at `:406` is still **complete**. AC-32/AC-42/AC-43's "exactly one warning" assertions are *validation* findings, not FR-24 diagnostic latches, and are per-host by construction.
- `grep -no "second host of the same shape"` — **3** hits: `:474`, `:514`, `:808` (revision history).
- `grep -n "revision 1[12]\|Revision.*: 1[12]"` — **1** hit, `:7` (**finding 1**).
- `grep "simultaneously satisfiable"` — **0** hits (rev-11 finding 3's text is gone).
- Counts: **27** FRs, **10** NFRs, **48** ACs (1–48 contiguous, none missing), **21** constraints, **14** OOS entries → 27+10+48+21+14 = **120** normative bullets. All match the expected totals; nothing was added or lost.
- README status line ("revision 12, reviewed ten times (20, 17, 11, 16, 13, 14, 9, 7, 4, then 4 findings)") checked against the revision-history table: ten reviews (rev-1, rev-2, rev-4, rev-5, rev-6, rev-7, rev-8, rev-9, rev-10, rev-11), counts match in order.

Reasoning verified by hand, not by grep:

- **Every counting AC and branch walked against the new `:406` rule.** AC-11 branch 1 (no count, not a counting branch); branch 2 (changes the provider → own host; one `PublishAsync`, two subscribers, two `AlwaysNew` asks each returning an ambient → one FR-24.4 entry, latched; provider never returns nothing so FR-24.2 cannot fire — count of one holds); branch 3 (changes provider behaviour → own host, and says so explicitly; one `Send` = one `JoinAmbient` ask returning `null` → FR-24.2, plus two `AlwaysNew` asks returning an ambient → FR-24.4 latched to one = **exactly two**, as asserted, and only in a fresh container). AC-19 (single branch, own host; two `JoinAmbient` asks both returning nothing → one FR-24.2 entry). AC-20 (two runs differing in affinity → two hosts; `JoinAmbient` run's 100 messages × two pipelines = 200 asks → one FR-24.2 entry; `AlwaysNew` run → zero via the carve-out). AC-29 (single branch, own host; stale ambient → one FR-23 entry, and no FR-24.2 entry arises because the controller action itself issues no Brighter call). AC-31 (branch 1 own host, one FR-24.2 entry; branch 2 fresh container + different affinity → two `AlwaysNew` asks returning `null` → zero, and an implementation that warns on every `AlwaysNew` ask records ≥1 and fails — **no longer vacuous**).
- **Repeat-operation clauses.** Four exist: `:473`, `:476`, `:573`, `:637`. All four are explicitly qualified "in the same host" in situ, and all four are correct there because the relevant latch is already spent. The preamble's illustrative list names three of them (`:473` for AC-11, `:573`, `:637`); AC-11's second repeat clause at `:476` ("repeating both operations in the same host") is not in the list but is covered by the general rule and by its own explicit qualifier, so no ambiguity results. Noted, not filed.
- **No branch falls on neither side or ambiguously on both.** The only branches that both change configuration and re-run an operation — AC-31 branch 2 ("the two `Send` calls") and AC-11 branch 3 ("repeating both operations") — build a new host and then repeat *within* it, which both sides of the rule agree on.
- `:244`'s two new claims checked against AC-29 `:572` and AC-31 `:511` verbatim, and against the FR-24.4 → FR-23 → FR-24.2 order stated in the same paragraph. Both hold.
- D19's "Where it lands" row (`:784`) checked against the actual sweep — FR-18, FR-19, FR-23, FR-24, the AC preamble, AC-11, AC-19, AC-20, AC-29, AC-31 — complete. `grep -no "D19"` returns hits at `:7`, `:235`, `:240`, `:242`, `:279`, `:282`, `:285`, `:406`, `:473`, `:474`, `:514`, `:567`, `:574`, `:637`, `:757`, `:784`, `:808` ×2, `:809`; every normative site is a member of that list.

Main-agent validation of the sub-agent's output:

- Coverage script re-run: FRs **27** · NFRs **10** · ACs **48**; no FR without an AC, no NFR without an AC, no AC gaps. No-loss check: **120** normative bullets, unchanged from revision 11. All seven template headings survive.
- Every evidence quote in both findings re-verified verbatim at its cited line: `:7` (`**Revision**: 11` — the finding is real), `:242`, `:471`, plus the disposition-table anchors `:244` and `:283`. All present exactly as quoted.
- Score bands recounted by hand from the findings as listed: 0 Critical, 0 High, 1 Medium, 1 Low; 2 total; **1** at or above threshold 60. The verdict line's count matches.

Deliberately **not** re-verified, per the brief: every codebase citation in the revision-11 review's Appendix and the lists it inherits — `CommandProcessor.cs`, `PipelineBuilder.cs`, `ServiceProviderLifetimeScope.cs`, `ServiceProviderMapperFactory.cs`, `ServiceProviderTransformerFactory.cs`, `ServiceProviderHandlerFactory.cs`, `RequestHandlerAttribute.cs`, `TransformAttributeBase.cs`, `ClaimCheckTransformer.cs`, the six factory interfaces, the four registration entry points and their `IOptions`/`TryAdd` behaviour, `BrighterOptions` defaults, `Proactor.cs`, `TransformPipelineBuilderAsync.cs`, the validation-machinery lines, and the ADR slugs. The author's claim that revision 12 introduced **no new codebase claims** is consistent with everything read: the changed passages (`:235`, `:244`, `:283`, `:406`, `:473`–`:476`, `:514`, `:808`) contain no file/line reference that was not already present in revision 11.
