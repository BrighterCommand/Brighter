# Review: tasks — 0037-show-me

**Date**: 2026-09-26
**Threshold**: 60
**Verdict**: NEEDS WORK

4 findings at or above threshold 60. Address these before approving.

*Reviewed at `1f2b0afcb` — the first review of the rewritten task list. `review-tasks.md` and
`review-tasks-pass2.md` reviewed the pre-rescope list.*

## Findings

### 1. K12 and R4 use a ref model that a fresh `git clone` does not produce, so AC-47's setup fails as written (Score: 75)

K12's third command is `git branch -f spec/scoped-lifetime-per-pipeline spec/scoped-lifetime-per-pipeline~1`, and R4 runs it in a clone built with `git clone <repo>`. A clone made that way has only one local branch, `spec/show-me`. The source's local branches arrive as `refs/remotes/origin/*`. So the name `spec/scoped-lifetime-per-pipeline~1` cannot be resolved in the clone. Git does not look up a remote-only branch this way when it parses a revision.

T6.1 uses this setup for AC-47, and so does T17.3. AC-47 is a (C-8) criterion, so it can only be run while PR #4282 is open. The requirements' AC-47 carries the same example, but the task list should give a command that works.

There is a second problem with the same cause. The clone's `origin/master` is the source's **local** `master`, and that is 286 commits behind the real `origin/master`. Every 0036 run in the clone (T6.1 K12, T17.3, the AC-25 comparison in T17.5) therefore measures from merge base `09f5d988f`, not `6145913a0`. R4 does not warn about this.

**Evidence**:
- Test on a remote-only branch: `git rev-parse --verify -q "add-messiging-gateway-tests~1"` gives `does not resolve`.
- `git rev-list --count master..origin/master` gives `286`.
- `git merge-base --is-ancestor 6145913a0 master` gives `NOT in local master`.
- `git merge-base master 91d549be6` gives `09f5d988f`.

**Recommendation**:
- In K12, write `git branch spec/scoped-lifetime-per-pipeline origin/spec/scoped-lifetime-per-pipeline~1`.
- Add a note to R4: the clone's `origin/*` refs mirror the source's local heads. Either bring the source's `master` up to date first, or run `git update-ref refs/remotes/origin/master <real origin/master sha>` in the clone.
- Say that clone-side merge bases and figures are not the calibration values.

---

### 2. T14.4 checks AC-27 on `0033`, where a correct implementation must mark git history `not available` (Score: 72)

T14.4 verifies AC-27 on `0033` and expects "the others are `used`". That includes the git history row. But `0033` has no spec branch, so no diff is measured.

ADR 0072 KC5 says the git history row is "`not available: spec branch not determinable` when none was (FR-16 row 12)". So a correct implementation fails this check. AC-27 itself also requires git history to be `used`, which means it needs a fixture where a diff **was** measured, there is no PR and there is no marked section.

**Evidence**:
- T14.4: "`0033`: AC-27 — the PR and release-notes rows are `not available` with reasons; the others are `used`".
- ADR 0072, lines 610–612: "The git history row is `used` when a spec diff was measured … and `not available: spec branch not determinable` when none was".
- `git log origin/master..HEAD -- specs/0033-pg-advisory-lock-sha256/` is empty, and `0033` has no `spec/pg-advisory-lock-sha256` ref.

**Recommendation**: Move AC-27 to a branch-resolving fixture with no PR, such as K2 in the clone or K1. On `0033`, assert the git history row as `not available: spec branch not determinable`.

---

### 3. K1 cannot satisfy AC-26, and T14.3's K1 check contradicts FR-14 and the task's own implementation line (Score: 65)

K1 changes only 2 files under `src/`. FR-14 draws paths from the `src/`-scoped diff. It uses `--name-only` only "when the spec diff touches no file under `src/`". So K1 can yield at most 2 paths, which is below the 3–7 that AC-26 requires.

T14.3 then checks that on K1 "the paths include the spec-directory files the fixture's branch commit carries". Those paths could only come from `--name-only`. That contradicts FR-14 and T14.3's own line: "take paths from the `src/` diff read, or from `--name-only` when there is no `src/` change". The R7 table still lists K1 as the AC-26 fixture.

**Evidence**:
- R7: "K1 | … 2 files under `src/Paramore.Brighter/` … | AC-57, AC-26".
- T14.3: "K1: the paths include the spec-directory files the fixture's branch commit carries."
- FR-14: "— only when the spec diff touches no file under `src/` — from `git diff --name-only`".

**Recommendation**: Remove AC-26 from K1 and drop the K1 check in T14.3, or give K1 at least 3 `src/` files. Also record, or raise with the requirements owner, the gap: FR-14 does not define what happens when fewer than 3 `src/` files changed.

---

### 4. The K-fixture table leaves out content the runs depend on, and two fixtures are missing from it (Score: 60)

R4 builds "each K-fixture" from R7's table, but the table omits files that the gate and the asserted outcomes need:
- **No `tasks.md` stated for K3, K4, K5, K6, K9 or K10.** FR-3's gate would stop every `/spec:show-me` run on them with the `tasks.md`-absent message.
- **K9 (AC-15) and K10 (AC-45)** need task evidence for their statuses, and none is described.
- **K10's `.adr-list` is not described.** The Classifier reads only ADR extracts located through `.adr-list`, so without an entry naming `9010-withdraw-fr-27-3.md` the withdrawal cannot be cited. That synthetic ADR also needs front matter, `## Status` and `## Consequences` for its extract windows.
- **Two fixtures are used but not in R7:**
  - `specs/9015-two-breaks/` (T15.5, T15.6). It also needs `tasks.md` for the `/spec:show-me` runs in AC-92.
  - `specs/9020-tiny-thing/` with the branches `wip/tiny-thing` and `other` (T6.1).

  R4's setup script is defined only over R7.

**Evidence**:
- R7 rows: K9 `FR-1…FR-12 and NFR-1…NFR-5 …` (no `tasks.md`); K10 `FR-27 with FR-27.1–.3; a staged synthetic ADR …` (no `tasks.md`, no `.adr-list`); K3–K6 give only source changes and `.adr-list`.
- T15.5: "Clone, with `specs/9015-two-breaks/` …". T6.1: "checking out `wip/tiny-thing` with a commit touching `specs/9020-tiny-thing/`".

**Recommendation**: For each K row, give the complete file set. That includes a fully checked, tag-first `tasks.md` whose lines evidence the intended statuses, and K10's `.adr-list`. Add K13 (9015) and K14 (9020) to R7 so the R4 script builds them.

---

### 5. T7.1 and T8.2 are test-first tasks whose tests pass as soon as they are written (Score: 55)

**T7.1.** Its only new row is the residue check. But T3.1 already implements the whole write path: "Write through `.show-me-ledger.json.tmp` … `File.Move(…, overwrite: true)`, and delete the temporary file in `finally`", "Check the serialised size before writing". T3.1's test already asserts "No `.show-me-ledger.json.tmp` remains". T7.1's implementation block restates the same code, so its row is green on arrival.

**T8.2.** T8.1 already emits `in_range` and asserts `in-range false`. The only implementation T8.2 gives is "add the in-range flag with inclusive bounds, and nothing else", which is already done.

Neither task can observe RED, which CLAUDE.md requires in both gears.

**Recommendation**:
- Either move the over-cap refusal and `finally` clean-up out of T3.1 into T7.1, keeping T3.1 to a plain write, or turn T7.1 into a STRUCTURAL inspection task for AC-83 plus the residue row.
- Fold T8.2 into T8.1, or move the `in_range` computation into T8.2.

---

### 6. Criteria that depend on a judged path may never be exercised, and the coverage table counts them as covered (Score: 55)

Several criteria are checked only if the model happens to take a judged path:
- AC-63: "If a second diagram is drawn" (T12.2), "when two diagrams are drawn" (T17.2).
- AC-65: "when ladder row 5 was elected" (T14.3).
- AC-60: "when a tree is present".
- AC-64: "when a stand-down is taken" (T12.2).
- AC-57's second half: "when the Explainer raises".
- AC-59's two-diagram, 70-line case.

No fixture forces these paths, and no rule says what to record if a path was not taken. The table still claims each is covered. One claim is false: AC-63 lists T10.1, whose body never mentions AC-63.

**Evidence**: coverage row "AC-63 | T10.1, T12.2, T17.2". T10.1's Verify block cites only AC-52, AC-76, AC-77 and AC-78.

**Recommendation**: For each such AC, name a fixture that makes the path likely, or add a rule to T17.x: "if the path was not taken, record the AC as unexercised in the acceptance record". Correct the AC-63 row.

---

### 7. T14.1 says D1 and D2 fire on K2, which they cannot (Score: 50)

K2 is "4 comment-only files in one `src/` subdirectory". So D1 is false (4 < 5 files), and D2 is false (0 public-API lines). T14.1 says: "K2 with `.adr-list` deleted: AC-40's `No ADRs recorded for this spec.`; D3 does not fire, while D1 and D2 do."

AC-40 only requires that D1 and D2 are "still evaluated". Read as written, the task asserts `true` for both and would fail a correct run.

**Recommendation**: Reword to "D3 is `false`; D1 and D2 are evaluated (non-null in the ledger)".

---

### 8. T3.6 leaves a bare number with one match unspecified, and puts untested code in a test-first task (Score: 50)

T3.6 says: "For a bare number, zero matches gives `ADR file not found`; more than one gives `ambiguous ADR number`". It does not say what a bare number that matches exactly one file does.

ADR 0078 KC5 says "anything else does not resolve". The task title, "`adr_resolved_count` counts only single-file matches", invites the opposite reading. Two developers could implement this differently.

The not-found and ambiguous branches are also implemented in T3.6 with no test-script row. The task defers them to "the direct run in T14.1's Verify block". That is code with no failing test in a test-first task.

**Recommendation**:
- State the single-match case explicitly: not resolved, with reason `ADR file not found`, per ADR 0078 and FR-16 row 7.
- Add a fixture row (for example a second `.adr-list` variant in a fixture directory) that exercises the not-found and ambiguous reasons and `matches`.

---

### 9. AC-42 is only partly verified (Score: 45)

AC-42 also requires that:
- both unresolved entries are marked `not available` in `## Inputs used`;
- neither counts toward D3;
- no risk factor changes.

T14.1 checks only the ledger reasons, the section naming both entries, and the remaining link. T14.4 does not list AC-42. T17.4 only re-runs T14.1's set-up.

**Recommendation**: Add these three assertions to T14.1's `0033` `.adr-list` case, or to T14.4.

---

### 10. Two `$PRE`-based checks cannot hold as written (Score: 45)

- **T15.6.** "`git diff $PRE` on `design.md` and `review.md` shows one added step and one added check". But `$PRE` comes before T15.3, which already added two checks to `review.md`, so this diff shows three.
- **T15.5.** "`git diff` shows one added hunk and nothing staged". But R4 requires the fixture directory and the synthetic ADR to be **staged** in the clone.

**Recommendation**: Diff T15.6 against the commit that ended T15.3. Change T15.5 to "`release_notes.md` is not staged".

---

### 11. Two of T2.1's three checks cannot be shown when T2.1 runs (Score: 45)

At T2.1, `show_me_facts.cs` does not exist. So:
- "a planted ledger is byte-identical afterwards" proves nothing, because nothing writes it;
- the "perturbation clause" cannot be shown, because every row already fails.

The harness's save-and-restore and its assertion reporting are first really exercised in T3.1 and T3.4, but those tasks do not re-check them.

**Recommendation**: Move the save-and-restore and perturbation checks into T3.1's verification (the first row that writes a ledger), or have T2.1 use a stand-in target script.

---

### 12. Phase 5's stated dependency is too weak (Score: 40)

Phase 5 says "Depends on T3.2". But:
- T5.1 asserts that the pinned *declared* row's "file fields equal those of its unpinned row", which needs T3.3–T3.6 and T4.1;
- T5.4's D3 needs `adr_resolved_count`, from T3.6.

**Recommendation**: State "Depends on Phases 3 and 4".

---

### 13. FR-10's "PR head differs" line is never exercised (Score: 40)

T11.1 implements `Spec branch tip {sha} differs from PR #{n} head {sha}; measured the PR head.`, but no run produces that state. On the live 0036 run, the PR head equals the branch tip. No stand-in `gh` in T6.2 returns a head that is present locally but differs from the tip.

**Recommendation**: Add an R5 case to T6.2 and T11.1: a stand-in returns `91d549be6~1` as `headRefOid`. Assert that `measured_head.source` is `pr_head` and that the line appears.

---

### 14. T11.1 is too large (Score: 40)

T11.1 covers the header, `## How it was built`, `## Blast radius` (six provenance and PR lines), the tracked-path and `Write` mechanics, and 19 ACs. It runs across real, clone and (C-8) fixtures. That is unlikely to fit one session.

**Recommendation**: Split it into three tasks: header plus the `Write`; How it was built; Blast radius.

---

### 15. NFR-5's secrets and absolute-path rule and NFR-6's closed-fence rule are never checked (Score: 35)

The coverage table maps NFR-5 to T11.1 and T14.6. Neither checks that `show-me.md` contains no absolute machine-local path or token. Nothing checks that fenced blocks are closed (NFR-6).

**Recommendation**: Add both checks to T14.6, for example a search for the repository's absolute root path and a count of opening against closing fences.

---

### 16. T13.3 and T13.4 check `0033`'s levels before the Classifier exists (Score: 35)

T13.4 asserts that "`0033` produces `**Overall risk: Medium**`". F2 and F5 come from the Classifier, which arrives only in T13.5. Until then they have no inputs, and afterwards they are judged. This order follows ADR 0073, but the assertion is fixed, not conditional.

**Recommendation**: Assert only F1 = `Medium` and "stated level ≥ maximum" until T13.5, and defer the exact overall level.

---

### 17. T1.3's "exactly this one added line" needs an unstated placement (Score: 25)

Appending the entry at the end of the `allow` array adds a comma to `"Bash(dotnet --list-runtimes)"`, which also modifies that line.

**Recommendation**: Say to insert the entry before the last element.

---

### 18. K5 may fire D1 as well, which breaks "naming D2" (Score: 25)

K5's ten classes have no stated location. If they span two or more `src/` subdirectories, D1 fires too, and the stand-down line would name D1 and D2.

**Recommendation**: Place K5's classes in one subdirectory, or loosen the assertion.

---

### 19. Small inconsistencies in the coverage tables (Score: 20)

- AC-56 lists T14.1, but T14.1's body does not check it.
- AC-58, AC-60, AC-64 and AC-65 omit T17.5, which re-checks them.
- T5.2–T5.4 cite "IA 6 table rows 8–9 / 10", but ADR 0072's IA 6 table has 7 rows.

**Recommendation**: Correct the table cells.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 2 |
| 50-69 (Medium) | 6 |
| 0-49 (Low) | 11 |

**Total findings**: 19
**Findings at or above threshold (60)**: 4

### Checks that passed

- **Checkbox form:** all 57 checkboxes are tag-first (43/4/8/2 by tag).
- **Gates:** the 43 `TEST + IMPLEMENT` tasks each have a gate line, and 16 of them carry `/test-first`.
- **Retired ACs:** the 5 retired ACs are excluded correctly.
- **Declared ids and ACs:** the 32 declared ids and the 12 (C-8) ACs match `requirements.md`.
- **Implementation Approach steps:** every IA step of the four ADRs maps to a task.
- **Calibration figures:** every figure checks out.
  - Tasks: 82 total, 0 unchecked, 62/12/2/6 by tag.
  - Ids and file sizes: 37 ids; 229,159 B `tasks.md`; 273,674 B `requirements.md`.
  - Diff: 517 files bucketed 76/393/14/24/0/10, +45,284/−515, `src/` +3,641/−264, 303,715 B, 131 API lines, 6 subdirectories, 363 commits.
  - Merge base `6145913a0`; PR #4282 OPEN at `91d549be6`.
- **Real fixtures:** these all check out.
  - Completion counts: `0002-sqs-cleanup` 6/6 with 0 ids and no `.issue-number`; `0021` 4/4; `0003` 2 of 10 unchecked; `0023` 0 checkboxes.
  - `0033`: 5/5, 8 ids, issue 4145, no branch.
  - Other paths: five `0037-*` ADRs; `release_notes.md` 118,145 B with an unmarked 0036 heading.
  - Commits and ADRs: the `c53875f3c..5247862cd` pair gives 3 files in 1 subdirectory; duplicate ADR numbers 0070–0073.
- **Patterns:** no stated pattern is transcribed into the task list.
