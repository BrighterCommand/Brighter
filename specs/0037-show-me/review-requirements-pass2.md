# Review: requirements — show-me (pass 2)

**Date**: 2026-09-18
**Threshold**: 60
**Verdict**: NEEDS WORK

7 findings at or above threshold 60. Address these before approving.

## Findings

### 1. F5's column mapping is not total — a `Deferred`/`Dropped` row *with* a recorded follow-up matches no column (Score: 72)

FR-11's F5 row reads:

- **Low**: "every numbered requirement is `Shipped`; or `Withdrawn` rows that cite the withdrawal decision **and** a follow-up issue or superseding requirement, with all others `Shipped`"
- **Medium**: "≥ 1 `Shipped with deviation` or `Unverifiable`"
- **High**: "≥ 1 `Deferred`, `Dropped`, or `Withdrawn` row stating `no follow-up recorded`"

Take a reconciliation with one `Deferred` row citing follow-up issue #1234 and every other row `Shipped`. It is not Low (Low admits only `Shipped` plus `Withdrawn`-with-follow-up). It is not Medium (no `Shipped with deviation`, no `Unverifiable`). It is not High (a follow-up *is* recorded). F5 is undefined. FR-8 explicitly requires that "`Deferred`/`Dropped`/`Withdrawn` rows must state whether a follow-up issue exists and its number", i.e. the document expects `Deferred`-with-follow-up to be a normal outcome.

This matters more than a cosmetic gap because F5 feeds FR-12's maximum, and the revision went out of its way to assert totality for the other factors — F4 "is a total mapping" and the severity rule "is total". F5 got no such treatment and does not have the property.

**Evidence**: FR-11 factor table, row F5; FR-8's follow-up clause; FR-11's totality claims for F4 and severity.

**Recommendation**: Make F5 total — e.g. put `Deferred`/`Dropped`/`Withdrawn` **with** a recorded follow-up in Medium alongside `Shipped with deviation`/`Unverifiable`, keep `no follow-up recorded` in High, and add a catch-all sentence. Add an AC exercising a `Deferred` row with a follow-up issue.

---

### 2. NFR-1 still claims determinism for F3 and for finding/round counts that the document's own definitions make judgement-derived (Score: 68)

NFR-1 lists as *mechanically countable* and required-identical: "the review-round count, per-round finding counts, the severity split, and the resolved / acknowledged / open split", and concludes "factor levels F1, F3 and F4 … derive purely from those counts". But every input to F3 is defined in judgement terms: `Finding`'s container/sub-issue split, `Review round`'s "asserts a defect" test, and `Resolved finding`'s "author's reply states was addressed by the fix made for another finding" clause (exactly round 2's finding #2 on the real PR — "closed for free"). F3 sits on the same footing NFR-1 now concedes for F2 and F5, but is asserted deterministic anyway; AC-32 encodes the wrong claim.

**Evidence**: NFR-1 bullets and exclusion list; Definitions `Finding`, `Review round`, `Resolved finding`; Additional Context "F1, F3, F4 … fully deterministic"; AC-32.

**Recommendation**: Move F3 (and the round/finding/resolution counts) into NFR-1's judgement-derived paragraph with the same treatment as F2/F5, or add a genuinely mechanical decomposition rule and drop the container-splitting clause. Update AC-32 to match.

---

### 3. FR-5/AC-11 mandate metadata fields that cannot exist in the "spec branch not determinable" run FR-16/AC-19 defines as successful (Score: 66)

FR-5 requires the metadata block to state "spec branch (the full ref actually used), head commit sha, base ref and merge-base sha", and AC-11 asserts this for "**Given** any successful run" — but FR-16 row 12/AC-19 define a successful run in which the branch is not determinable, with no ref, no head sha, no merge base. FR-16 has rows for `Blast radius`, `Where to look first` and `Breaking changes` in that state, but none for the metadata block, and AC-11 is unconditional.

**Evidence**: FR-5; AC-11 ("any successful run"); FR-16 row 12; AC-19 ("the run still succeeds").

**Recommendation**: Add an FR-16 row (or extend row 12) giving the exact metadata-block text when the branch is not determinable, and qualify AC-11 to "any successful run in which a spec branch was resolved", with a companion AC for the degraded metadata block.

---

### 4. FR-9's mandated round-line format and its own worked example / AC-17 disagree — the severity split is required by one and absent from both others (Score: 62)

FR-9 specifies the round line verbatim including a `({c} Critical, {h} High, {m} Medium, {l} Low, {u} unclassified)` parenthetical. Its worked example drops the whole parenthetical, and AC-17 (the acceptance criterion for this exact output) requires only the finding counts and resolved/acknowledged/open splits — no severity breakdown. A test written from AC-17 passes on output that omits a field FR-9 makes mandatory. Also unresolved: the Definitions say an unclassified finding "is counted as Medium", yet the format has separate `{m}`/`{u}` slots, and FR-9 never says how `{m}` and `{u}` differ.

**Evidence**: FR-9 format line vs. its example and AC-17; Definitions `Finding severity`; AC-49.

**Recommendation**: Add the severity parenthetical to FR-9's example and to AC-17's Then, and state that `{m}` counts only explicitly-Medium findings while `{u}` counts unclassified ones (which F3 treats as Medium for risk purposes only).

---

### 5. FR-7's spec-0036 breaking-change example still misdescribes the actual `release_notes.md` catalogue (Score: 60)

FR-7's example decomposes the calibration change into "`CreatePipelineScope()` added to seven interfaces" plus a separate `PipelineScope` item plus a separate `IAsyncDisposable` item. The actual catalogue decomposes it differently: item 2 is six mapper/transformer factories/registries gaining `CreatePipelineScope()`; item 6 is one bullet covering both `IAmAHandlerFactory.CreatePipelineScope()` **and** `IAmALifetime.PipelineScope` together; item 7 is `IAmALifetime : IAsyncDisposable`. `IAmALifetime.PipelineScope` is not a separate item — it's bundled with `IAmAHandlerFactory`'s member in one bullet. The 14-item total and "5 of 14 combined" claims are correct; only this decomposition is wrong, and it's prescriptive text teaching the counting rule from a decomposition the fixture doesn't contain.

**Evidence**: FR-7 example; `release_notes.md` spec-0036 section, bullets 2, 6, 7 on the calibration branch.

**Recommendation**: Quote the catalogue's real split (six interfaces / `IAmAHandlerFactory`+`IAmALifetime.PipelineScope` combined / `IAmALifetime : IAsyncDisposable`), or drop the specific bullets and keep only the counting rule plus the verified total of 14.

---

### 6. The claim that the 2026-08-28 pass is "excluded by all three tests" is false — `tasks.md` predates that review by fifteen hours (Score: 60)

The `Review round` definition's phase-exclusion clause claims the design-only pass is "excluded by all three tests". Test (ii) is "posted before the commit that first adds `tasks.md`". On the calibration branch, `tasks.md` was added at 2026-08-28 00:43 (commit `761d083c3`); the design review was posted ~15 hours later at 2026-08-28T15:24:52Z, with the review's own opening line stating it read `tasks.md`. Test (ii) is false for the one case used to demonstrate it. The outcome is unaffected (tests are OR'd and test (i)/(iii) both fire), but an implementer calibrating test (ii) against this case will conclude they've misimplemented it. Test (ii) is also a weak discriminator generally, since `/spec:design` review passes routinely run after `tasks.md` exists in this repo's workflow.

**Evidence**: Definitions `Review round` (c); `git log --diff-filter=A -- specs/0036-scoped-lifetime-per-pipeline/tasks.md` → `761d083c3 2026-08-28 00:43:21 +0100`; PR #4282 comment `createdAt 2026-08-28T15:24:52Z`.

**Recommendation**: Correct the parenthetical to say it's excluded by tests (i) and (iii), and that test (ii) does not fire because `tasks.md` was added earlier the same day. Consider demoting test (ii) to a corroborating signal.

---

### 7. C-8's enumeration of branch-dependent acceptance criteria is both over- and under-inclusive (Score: 60)

C-8's list of ACs requiring the calibration branch includes **AC-48**, which is not branch-dependent (its Given is a hypothetical rollup, citing FR-11 only, and runs anywhere) — and omits **AC-4**, which is branch-dependent (its Given, "`specs/.current-spec` contains `0036-scoped-lifetime-per-pipeline`", hits FR-2's stale-spec stop on any tree where that directory doesn't exist) but is neither listed in C-8 nor marked `(C-8)`. A reader trusting C-8 sets up the fixture branch for a test that doesn't need it and omits it for one that does.

**Evidence**: C-8's enumeration; AC-48's Given (hypothetical rollup); AC-4's Given vs. FR-2's stop condition.

**Recommendation**: Drop AC-48 from C-8's list, add AC-4 and mark it `(C-8)`, and make the inline `(C-8)` markers the single source of truth rather than maintaining a duplicate enumeration.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 1 |
| 50-69 (Medium) | 13 |
| 0-49 (Low) | 4 |

**Total findings**: 18
**Findings at or above threshold (60)**: 7

## Findings below threshold (recorded, not blocking)

- **NFR-2's 400-word floor squeezes against maximal FR-16 degradation (55)** — with several FR-16 rows firing at once, most sections reduce to fixed one-liners, so the floor is reachable only via a long narrative from thin inputs.
- **FR-8's "least-shipped" direction is ambiguous (55)** — could read as min or max under the stated precedence ordering. State it as "furthest right in that ordering wins".
- **The `Review round` exclusion example collides with the qualifying comments' own header (52)** — all three counted rounds are themselves posted inside "Claude finished @user's task" comments, the exact string quoted as the exclusion marker; rule (b)'s content test saves it, but a warning sentence would help.
- **AC-45's Given omits FR-27.1's outcome (52)** — with FR-27.1 unspecified, the precedence rule doesn't determine the row status. State all three sub-clause outcomes.
- **FR-14 has no rule for a spec diff touching fewer than three files (52)** — "three to seven paths" is unsatisfiable for a two-file diff. Add a floor clause.
- **No tie-break when a factor matches two columns (50)** — e.g. F3 with one acknowledged (Medium) and one open Critical (High) finding simultaneously.
- **FR-20's PR discovery has no branch name under FR-10 rule 3 (50)** — rule 3 resolves to `HEAD`, but `gh pr list --head` needs a name.
- **A finding titled `*(Blocker)*` is classified Medium (45)** — falls outside the defined severity words into the Medium catch-all, which is probably not intended for a word reviewers use to mean "High/Critical".
- **FR-9's totals line has no stated format (45)** — the per-round line is given verbatim; the totals line is only described in prose.
- **The round-grouping example's numbers don't match the case it describes (42)** — "part 1 ends at 6, part 2 begins at 7"; on PR #4282 part 1 actually ends at 5 and part 2 begins at 6 (AC-50's "1–10" is correct).
- **`.adr-list` holds filenames, not "filename stems" (40)** — spec 0036's `.adr-list` lines include the `.md` extension; AC-12/FR-16 row 7 describe a format the fixture doesn't use.
- **Issue #4357 is CLOSED (30)** — Out of Scope/Additional Context describe it as open/tracked; it shipped as ADR 0071/`/spec:gear`.

## Verified correct on this pass (so a third reviewer needn't re-check)

- **PR #4282 review history**: exactly three implementation review passes, 3+5+3=11 findings, 9 resolved/2 acknowledged/0 open (9+2+0=11); exactly six non-review task-completion comments; exactly one specification-phase pass. FR-9's arithmetic, AC-17, AC-20 all consistent.
- **F3 = Medium for the calibration case** follows exactly from the F3 row as written; no stale "F3 = Low" survives anywhere (all 16 mentions agree).
- **Blast radius**: 517 files = 76 src/ + 393 tests/ + 14 docs/ + 24 specs/ + 0 .github/ + 10 other; 363 commits. AC-18 and the worked example are exact.
- **AC-49's severity walkthrough is correct** (0/0/1/2 + 2 unclassified, no off-by-one).
- **ADR duplicates on `master`**: exactly the 14 numbers C-9 names (0037 ×5, 0057 ×4); calibration branch has two 0070s and two 0071s as claimed.
- **C-10's allow-list** matches exactly; FR-18's "no new allow-list entry" holds.
- **FR-17's gitignore claim** is correct (only literal `PROMPT.md` is gitignored).
- **Master-resident fixtures** (three `0002-*`, the spaced directory, README.md/dlq-review-findings.md, `.current-spec`, tasks.md-less 0037) all exist as assumed.
- **Spec 0036 fixtures**: `.adr-list` names 0070–0076; `tasks.md` 229,159 bytes, 82/0 checkboxes; `.issue-number` 4256; `release_notes.md` 14 items, 5 combined.
- **NFR-2's word-budget arithmetic** is sound (~1,700 words, under the 2,000 cap).
- **AC↔FR coverage**: every FR/NFR has ≥1 AC; all 14 FR-16 rows have a cited AC (AC-27's mapping to row 1 is the one weak spot, covered mostly indirectly via AC-28).
- **FR-20's insertion is clean**, no id collisions.
- **No unrequested scope**: every new element traces to a pass-1 finding.

**Limitations**: none material — the calibration branch was reachable and every branch-dependent claim was verified directly.
