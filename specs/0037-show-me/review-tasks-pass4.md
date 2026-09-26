# Review: tasks — 0037-show-me

**Date**: 2026-09-26
**Threshold**: 60
**Verdict**: NEEDS WORK

2 findings at or above threshold 60. Address these before approving.

*Reviewed at `41a0e8dbe`, pass 4. Pass 3 filed 19 findings (4 at or above 60) against `1f2b0afcb`. All 19 are resolved by `41a0e8dbe`, and finding 3's requirements half is resolved by the FR-14 amendment in `dc84640aa`. None is re-filed here. Two new problems reach the threshold. The first is an old K7 design gap that the new "Source changes are committed on the row's branch only" rule now states outright. The second is a sequencing gap in T15.5.*

## Findings

### 1. K7's padded source files exist only on `spec/budget`, but the Explainer reads the working tree, so AC-58's budget exhaustion is not produced (Score: 70)

K7 is meant to exhaust the budget, including the 100,000 B reserve, before the Explainer can read the types (AC-58). Its source files are "each padded past 150,000 B". R7 now says "**Source changes** are committed on the row's branch only". R4 step 4 stages only the spec directory on the working branch. So in the clone's working tree (`spec/show-me`), those 5 files keep their normal size, a few KB each.

ADR 0077 has the Explainer probe and read participants in the working tree, using `wc -c`, `tail`/`head` and `grep -n -F` on paths that have passed `git ls-files`. It does not read them at the measured head. Here is what a correct run does:
- `tasks.md` (940,000 B) and the `src/` diff use up most of the general allowance.
- The Explainer then probes five small working-tree files against the ~100 KB reserve.
- They fit, so it reads them and may draw a diagram.

T12.2's unconditional check "K7 (clone): the budget line, no fenced block, and no unread type named (AC-58)" would then fail. It is not in the *Judged paths* table either.

The same gap exists for K5 and K6, but it does not break their checks. The Explainer reads files that lack the changes (for example, K5's ten added properties). That weakens the fidelity of AC-64 and AC-65 without failing them.

**Evidence**:
- R7: "**Source changes** are committed on the row's branch only."
- K7 row: "5 changed files across 2 `src/` subdirectories, each padded past 150,000 B …"
- ADR 0077, lines 262–275: "Before its first read, it runs `wc -c` on every participant … each is sized with `wc -c` before it is read … An extraction is located by a `grep -n -F`".
- T12.2: "K7 (clone): the budget line, no fenced block, and no unread type named (AC-58)."

**Recommendation**: Make the padding visible to working-tree reads. For example, apply K7's padded source edits in the clone's working tree as well, left unstaged like K7's `release_notes.md` edit, so they stay tracked paths with oversized content. Another option is to run K7 with `spec/budget` checked out. But K14's note already explains why a staged copy conflicts on checkout, so that route needs K7's directory left unstaged. State which one R4 does. Say in R7 that the Explainer reads the working tree, so any K row whose outcome depends on source content must carry that content there.

---

### 2. T15.5's AC-92 steps have no reset between them, so the first one cannot give `count` 0 (Score: 60)

T15.5 runs AC-89, then AC-90, then "An ADR with no break", all in one clone. Each writes or replaces the section **marked** for `9015-two-breaks` under `## Master`. The AC-92 block comes next:
- "A hand-written `### X (spec 9015)` section: the script run directly records `count` 0 and `m` null."

The command-written marked section is still present, so a correct script reports `count` 1. AC-92's premise is "a spec whose **only** `release_notes.md` section is hand-written with no marker".

The third step, "Hand-mark a section that has no `#### Breaking changes`: `count` 1, `m` null", has the same problem. After the second step, the three-break marked section still exists, so the result is two marked sections: `count` 2, and `/spec:write_release_notes` would stop on ladder row 5.

**Evidence**:
- T15.5 runs AC-89, AC-90 and "An ADR with no break gives `No breaking changes.`" first, then "A hand-written `### X (spec 9015)` section: … `count` 0 and `m` null".
- AC-92: "**Given** a spec whose only `release_notes.md` section is hand-written with no marker …; **and given** instead the marker line added by hand beneath the hand-written section's heading …".

**Recommendation**: Before each AC-92 given, restore the clone's `release_notes.md` to its state from before T15.5, keeping K7's unstaged section. Name a saved copy for that, such as the one AC-89's `diff` already takes. The third given ("instead") must start from the hand-written section alone, not from the second given's result.

---

### 3. FR-16 row 1 is never produced at command level, and AC-27 is checked on a `gh unavailable` run (Score: 45)

T14.4 checks AC-27 on K2 in the clone "where `gh` fails". That is FR-16 **row 2** (`not available: gh unavailable`), not row 1. AC-27's premise is "a run with no PR", and it traces FR-16 rows 1 and 5. The same K2 run is also T14.4's AC-28 case.

Row 1's output text is `Inputs used`: `not available: no PR found for branch {branch}`, with the metadata PR line `none found` on a resolved branch. No command run produces it. T6.2 reaches row 1 only in the ledger, through a stand-in with a different `headRefName`. `0033` gets `none found` through row 15, not row 1.

**Evidence**:
- T14.4: "K2 in the clone, which resolves `spec/low-risk` … has no PR (R4: `gh` fails): AC-27 …"
- It then says: "K2 in the clone, where `gh` fails: the `gh unavailable` row … (AC-28)".
- R4: "every clone run is FR-16 row 2 (`gh unavailable`) unless R5 is used."

**Recommendation**: Run K2 for AC-27 with an R5 stand-in that answers `[]` and exits 0. Assert the `no PR found for branch spec/low-risk` row and the `none found` metadata line. Keep the failing-`gh` run for AC-28 only.

---

### 4. T12.1 asserts AC-57's first half on K1 unconditionally, though it holds only when the Explainer does not raise (Score: 35)

AC-57's first half applies "**when** the command runs **and** it does not exercise FR-6 (b)'s raise". T12.1 asserts the `No diagram:` line on K1 with no condition. T12.2 and the *Judged paths* table treat the raise on K1 as a legitimate judgement. So a correct run that raises fails T12.1.

**Evidence**: T12.1: "K1 (clone): the no-trigger line names `2`, `1` directory, `0` and `1` (AC-57, first half)."

**Recommendation**: Add "when the Explainer does not raise; otherwise record the first half `unexercised on this run`".

---

### 5. T3.2's grammar wording reads as if both test options are required together (Score: 20)

T3.2 says to "Parse exactly the three forms in ADR 0072 KC1's table: `--pinned` and `--release-notes` in either order, and `--word-count`". ADR 0072 line 290 allows "`--pinned …`, `--release-notes {path}`, or both, in either order". T4.1 relies on the unpinned `--release-notes`-only form. Read literally, T3.2 makes that form a usage error. T4.1 would catch the mistake, but only later.

**Recommendation**: Quote the ADR: "`--pinned`, `--release-notes`, or both, in either order". Consider adding the `--release-notes`-only form as a passing T3.2 row.

---

### 6. The coverage table credits AC-25 to T14.7, whose Verify block does not check it (Score: 20)

The AC-25 row reads `T14.7, T17.5`. T14.7 lists AC-25 only in its `Traces to:` line ("AC-25 (no side effect varies)"). None of its checks compares two runs' side effects. Only T17.5 checks it.

**Recommendation**: Reduce the cell to T17.5, or add the comparison to T14.7.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 1 |
| 50-69 (Medium) | 1 |
| 0-49 (Low) | 4 |

**Total findings**: 6
**Findings at or above threshold (60)**: 2

### Checks that passed

- **Pass-3 findings:** all 19 are resolved.
  - The clone's refs use `origin/` forms, and `origin/master` is reset.
  - AC-27 moved to K2. K1 now has a complete 2-path list. AC-40 and AC-42 are fully asserted.
  - T3.1 does a plain write. T7.1 uses inspection plus a residue guard. `in_range` moved to T8.2. There is an *adr-unresolved* fixture.
  - T11.1 is split into three tasks. `POST_T15_3` exists. T15.5 is checked with `--cached`.
  - Phase 5's dependency is fixed. T6.2 and T11.3 have the PR-head-differs case.
  - NFR-5 and NFR-6 checks are in T14.6. T13.4 is conditional. T1.3's placement is stated. K5 is in one subdirectory. The IA 6 row numbers are correct.
- **FR-14 amendment (`dc84640aa`):** it is carried through consistently.
  - In the requirements: FR-14, the optional-tree rule, FR-6 (d) and AC-26.
  - In ADR 0072's KC5 table and ADR 0077's tree rule.
  - In the task list: T12.2, T14.3 and the K1 row. AC-60 keeps 3–7, which is consistent with "no tree under three".
- **Task list form:**
  - 59 checkboxes, all tag-first: 45 `TEST + IMPLEMENT`, 4 `STRUCTURAL`, 2 `DOC`, 8 `PROJECT`.
  - 45 gate lines and 15 `/test-first` lines. The 15 are T2.1, T3.1–T3.6, T4.1, T5.1–T5.4, T8.1, T8.2 and T13.1.
  - Every task id referenced exists.
- **Acceptance criteria:** 96 AC headers minus 5 retired gives 91 live, which matches the table. The 12 (C-8) ACs are listed correctly. FR-16 rows 3 and 4 are correctly retired.
- **Refs and PR state:**
  - Local and `origin/spec/scoped-lifetime-per-pipeline` are both at `91d549be6`, so `local_divergence` is null on the real run.
  - `91d549be6~1` is `386efee7880e32428a96a96ede6d44a21541e48c`.
  - `merge-base origin/master 91d549be6` is `6145913a0`. Local `master` is 286 behind.
  - PR #4282 is `OPEN` at `91d549be6`.
- **Calibration figures:** all re-measured and correct.
  - 517 files, +45,284/−515. `src/` has 76 files, +3,641/−264, and a 303,715 B diff. 363 commits.
  - `tasks.md` is 229,159 B and `requirements.md` is 273,674 B. `git diff --quiet 91d549be6 HEAD` holds on both.
  - `release_notes.md` is 118,145 B.
- **Fixture facts:**
  - `docs/adr/0062-*` is unique, so the `0062` bare-number row is valid. `0037-*` has 5 files.
  - `0033`'s `.adr-list` is `0062-pg-advisory-lock-sha256.md`, and 0036's `.adr-list` has 7 entries.
  - The last `allow` element is `Bash(dotnet --list-runtimes)`.
  - `0036-generator-universal-rejection-tests` has `requirements.md`, so T15.4 reaches ladder row 7.
  - No commit in `origin/master..HEAD` touches `specs/0002-sqs-cleanup/`, so AC-85's premise holds for K12.
- **K14:** rules 2 and 3 resolve as T6.1 states under FR-10's wording.
- **ADR alignment:**
  - ADR 0072's IA 6 table has 7 rows and 10 boundary rows, which matches T5.2–T5.4.
  - T3.6's bare-number rule matches FR-16 row 7 and ADR 0078 KC5.
  - F2's 1–3 → `Medium` band makes K3's AC-22 robust.
