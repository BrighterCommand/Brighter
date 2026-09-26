# Review: tasks — 0037-show-me (pass 5)

**Date**: 2026-09-26
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. Consider addressing lower-scored items.

*Reviewed at `0afe1bdcc`. Pass 4 filed 6 findings, 2 of them at or above 60. `0afe1bdcc` resolves all six. Every site of each touched concept was grepped (R4 step 4, R7 *Source changes*, the K5/K6/K7 rows, T6.1's K14 checkout, T12.1, the *Judged paths* table, T14.4, T15.5, T17.5 and the coverage table); no new contradiction at or above the threshold.*

## Pass-4 findings — resolution check

1. **K7 padding not visible to the Explainer: resolved.**
   - R4 step 4 now says "Apply the source changes of K5, K6 and K7 to the working tree as well, and leave them unstaged too".
   - R7 now says the Explainer "reads participants in the **working tree**", and adds that K5, K6 and K7 "use files that no other K row changes".
   - K7's row adds "the padding is also in the working tree (R7)".
   - T6.1's K14 step now carries "the unstaged K5–K7 source edits" across the checkout. That holds because K14's branches are cut from `spec/show-me` and add only `specs/9020-…`.
2. **T15.5 steps not reset between AC-92 givens: resolved.**
   - T15.5 saves `$SCRATCH/release_notes.pre-T15.5.md` before the first run.
   - Each AC-92 given begins "Restore, …". The third given rebuilds the hand-written section and then adds the marker, which matches AC-92's "instead".
   - The task ends with a restore plus `git checkout -- docs/adr/9015-two-breaks.md`. That checkout restores from the index, so the ADR returns to its staged content, which is correct.
3. **AC-27 checked on a `gh unavailable` run: resolved.**
   - T14.4's AC-27 run uses an R5 stand-in that answers `[]`. It asserts `not available: no PR found for branch spec/low-risk` and `none found`, which matches FR-16 row 1 at requirements.md line 1097.
   - The failing-`gh` run is now "not used for AC-27".
   - T17.5 separates AC-27 (the stand-in) from AC-28 (`gh` failing).
4. **AC-57's first half asserted unconditionally on K1: resolved.** T12.1 now says "when the Explainer does not raise … When it raises, record the first half `unexercised on this run`". The *Judged paths* table has a new "AC-57 (first half)" row, and T17.5 says "Both halves of AC-57".
5. **T3.2 grammar read as both options required: resolved.** T3.2 now reads "`--pinned {base} {head}`, `--release-notes {path}`, or both, in either order". It also adds a `--release-notes`-only row: exit `0`, `pinned` false.
6. **AC-25 credited to T14.7: resolved.** The coverage cell reads `T17.5` only, and T14.7's `Traces to:` no longer lists AC-25.

## Findings

### 1. R4 step 4 does not say how to "apply" K5–K7's source changes, and the working tree differs from the branches' base in many of the candidate files (Score: 40)

The K5, K6 and K7 branches are cut from `origin/master`. The clone's `origin/master` is set to the source's real `origin/master`. The working tree those source changes are applied to is `spec/show-me`, which carries the 0036 merge.

Running `git diff --name-only origin/master HEAD -- src/` gives 198 files. 60 of them are in `src/Paramore.Brighter/`, which is where K5 (all ten files) and K6 must live. K7's two subdirectories are unnamed. So a K5–K7 file is quite likely to differ between the two trees. Two developers would then get different results:
- Replaying the branch diff (`git diff origin/master spec/ten-dtos -- src | git apply`) can fail on context.
- Copying the branch's whole file (`git checkout spec/ten-dtos -- <file>` followed by `git reset`) silently reverts that file's `spec/show-me` changes in the working tree.

The Explainer reads either result as valid, so AC-58/64/65 are unlikely to break. The step is still under-specified, and the new rule that the files be disjoint "from every other K row" does not cover files that the working branch itself changes.

A related edge case: T17.3 and T17.5's AC-25 run `0036` in the same clone. If a K5–K7 file is also one of 0036's participants, that run's Explainer reads the K content or K7's 150,000 B padding. Neither check depends on diagram content, which is why this stays low.

**Evidence**:
- R4 step 4: "Apply the source changes of K5, K6 and K7 to the working tree as well, and leave them unstaged too (R7, *Source changes*)."
- R7: "K5, K6 and K7 therefore use files that no other K row changes, and none shared with each other."
- R4 step 3: "Each `spec/{name}` is created off `origin/master`".

**Recommendation**: Add one sentence to R7:
- K5–K7 pick files for which `git diff --quiet origin/master HEAD -- {file}` succeeds in the clone. That means the file is identical on the branch base and the working branch, and is not among 0036's changed paths.
- Say that "apply" means `git checkout spec/{name} -- {files}` followed by `git restore --staged {files}`. On such files, a patch and a copy give the same result.

---

### 2. T3.2's new `--release-notes`-only row names no path (Score: 15)

T3.2's other `--release-notes` rows use the `--release-notes` option without naming a path. T4.1 names the fixture explicitly (`.claude/test-fixtures/show-me/release-notes.md`). T3.2 asserts only exit `0`, a ledger, and `pinned` false. It is unclear whether the path should be the fixture or an absent file (an absent file gives `present` false, per T4.1).

**Evidence**: T3.2: "The *declared* row with `--release-notes` alone, unpinned."

**Recommendation**: Name `.claude/test-fixtures/show-me/release-notes.md` in the row, as T4.1 does.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 0 |
| 0-49 (Low) | 2 |

**Total findings**: 2
**Findings at or above threshold (60)**: 0

### Checks that passed

- **Task list form:**
  - 59 checkboxes, all tag-first: 45 `TEST + IMPLEMENT`, 4 `STRUCTURAL`, 2 `DOC`, 8 `PROJECT`.
  - 45 gate lines and 15 `USE COMMAND` lines, unchanged from pass 4.
- **T15.5's restore sequence:**
  - The saved copy holds K7's section and no `9015` section. The clone is rebuilt before T15.5 is verified, because T15.5 changes `write_release_notes.md` (R4), so the claim about the saved copy holds.
  - The three givens now match AC-92 at requirements.md lines 2306–2314.
- **AC-27 and AC-28 are separated consistently** across T14.4, T17.4 ("AC-27 is checked on K2 in T17.5"), T17.5 and the coverage table.
- **AC-25 in T17.5:** both runs are made with `gh` failing. The phase-17 preamble still lists AC-25 among the runs that need 0036 unmerged.
- **AC-57:** its † marker, its coverage cell (T12.1, T12.2, T17.5) and its two *Judged paths* rows are consistent.
- **R7's statement that the edits change no ledger value** is consistent with ADR 0072's kinds-of-run table: diff fields are measured between refs, and file fields are read from the working tree.
