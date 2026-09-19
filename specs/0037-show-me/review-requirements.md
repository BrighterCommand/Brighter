# Review: requirements — show-me

**Date**: 2026-09-18
**Threshold**: 60
**Verdict**: NEEDS WORK

14 findings at or above threshold 60. Address these before approving.

## Findings

### 1. The spec 0036 review-history calibration is factually wrong — there were ≥4 review passes and ≥20 findings, not 3 rounds of 11, and at least one finding was explicitly acknowledged (Score: 90)

The document's single most-reused calibration number is "three rounds on spec 0036, 3 + 5 + 3 findings, all resolved, 0 open". Reading PR #4282 (`spec/scoped-lifetime-per-pipeline`) directly shows **four** distinct Claude review passes, not three:

- 2026-08-28 — "Review — Spec 0036 (design only), part 1 of 2" and "part 2 of 2": numbered findings **1 through 10**, where #10 is a "Smaller items" bucket containing five further distinct issues. That is 9–14 findings by the document's own rule ("One comment raising three distinct issues counts as three findings").
- 2026-09-16T17:43 — "implementation pass", 3 findings.
- 2026-09-18T07:44 — "Re-review", 5 new findings.
- 2026-09-18T11:39 — "Final re-review", 3 new findings (one labelled `*(nit)*`).

So under the document's own Definitions, spec 0036 has ≥4 rounds and ≥20 findings. Finding #9 of the 2026-08-28 round is titled *"ADR 0072 ladder row 8 is untested (already acknowledged)"* — an **acknowledged finding**, which by FR-11's own F3 rule forces `F3 = Medium`, not the `Low` the document asserts three separate times.

This breaks FR-9's worked example, AC-17, FR-11's F3 example, AC-20, the Problem Statement, and the Additional Context worked example.

**Evidence**: AC-17: "**Given** spec 0036's PR with three review rounds of 3, 5 and 3 findings, all resolved … a totals line of 11 findings / 11 resolved / 0 acknowledged / 0 open". Actual PR #4282 comment stream contains four review passes; the 2026-08-28 pass alone enumerates findings `### 1.` … `### 10.` plus five sub-bullets, and its `### 9.` is parenthesised "(already acknowledged)".

**Recommendation**: Re-derive the calibration from PR #4282 as it actually is, and state the counting explicitly (does a design-phase review on the PR count as a round? does a "Smaller items" bucket of five bullets count as five findings?). Recompute F3 for the worked example — with an acknowledged finding present, F3 is `Medium` under FR-11, and the "Overall risk: High" conclusion must be re-justified on F1/F2 alone. If design-phase reviews are meant to be excluded, add that exclusion to the `Finding` / `Review round` definitions.

---

### 2. The entire worked example lives on a different branch and does not exist in the tree where the command is being built; two of its ACs are unrunnable and one link target does not resolve (Score: 82)

`specs/0036-scoped-lifetime-per-pipeline/` does **not** exist on `master`, nor on the current working branch `spec/show-me`. `ls specs/` on the checked-out tree shows exactly one 0036 directory: `0036-generator-universal-rejection-tests`. The scoped-lifetime spec directory and ADRs 0070–0076 exist only on the unmerged branch `spec/scoped-lifetime-per-pipeline`.

Consequences:

- **AC-1 is false as written on the implementation branch.** In the tree where the tests will run, `0036` matches exactly one directory, so the correct behaviour is to resolve it, not report ambiguity. FR-1's *Example* has the same problem.
- **AC-12 and FR-6's example link do not resolve.** `docs/adr/` on this branch contains `0070-generator-owned-rejection-and-delay-conformance.md` and `0071-tdd-review-gear.md`. The path FR-6 gives as its worked example, `../../docs/adr/0070-per-pipeline-di-scope-for-mapper-and-transform-factories.md`, does not exist here.
- **ADR numbers are not unique either.** On `spec/scoped-lifetime-per-pipeline`, `docs/adr/` holds *both* `0070-generator-owned-...` and `0070-per-pipeline-di-scope-...`, and both `0071-tdd-review-gear.md` and `0071-pipeline-scope-handle-...`. FR-6 requires naming each ADR as "`[ADR 0070]`", which is ambiguous in that very tree.

**Evidence**: `git ls-tree -d --name-only spec/scoped-lifetime-per-pipeline specs/` lists both 0036 directories; `ls specs/` on the working tree lists only `0036-generator-universal-rejection-tests`. `git ls-tree --name-only spec/scoped-lifetime-per-pipeline docs/adr/ | grep 007` returns nine files including two numbered 0070 and two numbered 0071.

**Recommendation**: Either (a) state explicitly that the calibration/AC fixtures assume `spec/scoped-lifetime-per-pipeline` is checked out or merged, and mark AC-1/AC-2/AC-12/AC-17/AC-18/AC-20 as branch-dependent, or (b) rewrite those ACs against fixtures that exist on `master` (e.g. `0002-*`, three of which share the id `0002`). Add an FR clause covering duplicate ADR numbers.

---

### 3. FR-14 contradicts FR-5 and FR-16 when the spec branch is not determinable (Score: 75)

FR-14 requires `## Where to look first` to list "Three to seven paths from the spec diff", and "Every path listed must exist in the spec diff". FR-5 says every section is "always present". But FR-16 defines a case — "Spec branch not determinable" — where **no diff is measured at all**, and has no row for `Where to look first`. The same hole exists for FR-7 (public-API diff lines).

**Evidence**: FR-14; FR-16's "Spec branch not determinable" row; AC-26 ("lists 3–7 paths, every path appears in the spec diff", unconditional).

**Recommendation**: Add FR-16 rows for `Where to look first` and `Breaking changes` under "no diff measured", with exact fallback text. Qualify AC-26 to "Given any successful run **with a measured diff**" and add a matching AC for the no-diff case.

---

### 4. NFR-1 demands determinism for values that are judgement-derived with no stated algorithm, including two that feed the overall risk level (Score: 74)

NFR-1 requires identical "breaking-change count" and "FR-8 status counts" across runs, but FR-7's breaking-change count comes from unalgorithmic synthesis, and FR-8's `Shipped`/`Shipped with deviation`/`Unverifiable` statuses require judging prose. These feed **F2** and **F5**, and therefore the overall level under FR-12. The requirement asserts determinism nothing in the document makes achievable.

**Evidence**: NFR-1; FR-7 (synthesis with no counting rule); FR-8 (judged statuses).

**Recommendation**: Weaken NFR-1 to cover only mechanically-countable fields (blast radius, task/commit/finding counts, CI state), and state F2/F5 are judgement-derived and may vary; or add a deterministic derivation rule for breaking-change items and pin FR-8's status decision procedure.

---

### 5. FR-18's claim about the existing `.claude/settings.json` allow-list is factually wrong, and F4 cannot be computed within it (Score: 72)

FR-18 claims read-only GitHub access "consistent with the repository's existing `.claude/settings.json` allow-list", including "read-only run queries". The actual `gh` allow-list entries are `gh pr view`, `gh pr list`, `gh pr diff`, `gh issue view`, `gh issue list` — no run-query command at all. FR-11's F4 and FR-16's CI-run row both require querying workflow runs, which the stated allow-list cannot do.

**Evidence**: `.claude/settings.json`'s `gh` entries (view/list/diff for PRs, view/list for issues — no `run`/`checks`/`api`); FR-18's claim of "read-only run queries".

**Recommendation**: Either name the specific run-query command and require `.claude/settings.json` to gain a matching entry as a deliverable, or drop F4's dependence on separate run queries and derive CI state from `gh pr view --json statusCheckRollup` (already within the allowed `gh pr view:*`), stating that explicitly.

---

### 6. PR discovery is never specified, and `gh pr diff` vs. `git diff` are treated as interchangeable when they can disagree (Score: 70)

The Definitions say `gh pr diff` is used "when a PR is found", but no FR specifies **how** a PR is found (by head branch? `.issue-number`? title?). FR-10 details branch resolution but stops there. The two diff sources are also not equivalent — `git diff <merge-base>..<tip>` moves as local `master` advances, while `gh pr diff` doesn't — yet AC-18 asserts fixed numbers regardless of source, and NFR-1's determinism claim is exposed to this.

**Evidence**: Definitions "Spec diff" row; AC-18; FR-10 has no PR-discovery counterpart.

**Recommendation**: Add an FR specifying PR discovery (e.g. `gh pr list --head <branch> --state all`, most-recent-wins, stated tie-break) and state which diff source wins when both are available, or require the section to state both when they differ.

---

### 7. F4's CI model does not match `.github/workflows/ci.yml`, and `skipped` maps to none of the three columns (Score: 70)

"Required CI job" is defined as any job in `ci.yml` that runs for pull requests — not what "required" means on GitHub (a branch-protection setting), and it collides with the file's actual shape: several jobs (`aws-ci`, `azure-ci`, `mongodb-ci`, etc.) are fork-gated and get `skipped` conclusions on many PRs; `release` never runs on a PR at all. F4's Low column requires "all required CI jobs concluded `success`" (unreachable with any skip); High requires failure/cancelled; Medium's list doesn't mention `skipped` either. Two developers will map `skipped` differently, and F4 feeds the overall level.

**Evidence**: `.github/workflows/ci.yml`'s fork-gated `if:` conditions on several jobs; FR-11's F4 row enumerates only success/failure/cancelled/pending/not-determinable.

**Recommendation**: Redefine using something checkable (e.g. every non-`SKIPPED` check in `gh pr view --json statusCheckRollup`), and add explicit mappings for `skipped`, `neutral`, `action_required`.

---

### 8. The finding-severity model does not match how reviews are actually written, and F3's `High` column is effectively unreachable (Score: 68)

"Finding severity" is defined as the stated severity word (`Critical`/`High`/`Medium`/`Low`), but PR #4282's actual reviews use lowercase, sometimes-qualified severities (`*(medium)*`, `*(low — a question, not a defect)*`) and include **`nit`**, which is outside the defined scale. `.github/workflows/claude-code-review.yml`'s prompt never asks for severities at all (C-3 half-acknowledges this). With unclassified → Medium and real reviews rarely producing High/Critical, F3's High branch can essentially never fire from this repo's own review workflow — a factor whose High branch is unreachable can't raise the overall risk level via that path.

**Evidence**: PR #4282 comment headings using `*(nit)*` and qualified lowercase severities; `claude-code-review.yml`'s prompt has no severity instruction.

**Recommendation**: Define matching as case-insensitive, map `nit` and unrecognised words explicitly, and either justify the unreachable High branch or restate F3 in terms this repo's reviews actually produce (open/acknowledged/resolved counts, severity as optional refinement).

---

### 9. "Review round = one tracking comment" is falsified by the calibration PR in both directions (Score: 68)

On PR #4282: the 2026-08-28 review was posted as two comments ("part 1 of 2"/"part 2 of 2") 29 seconds apart with one continuous numbering — under the literal definition that's two rounds; it's one. Separately, six short non-review `@claude` "task finished" comments exist on the same PR — each is a tracking comment but none is a review round; counting them would report far more rounds than actually occurred. C-3 also names only `claude-code-review.yml`, but every round on the calibration PR came through `claude.yml` (the `@claude` comment path).

**Evidence**: Two-part review posted 29 seconds apart with continuous numbering; six "Claude finished @iancooper's task" comments dated 2026-09-16; C-3 names only the label workflow.

**Recommendation**: Define a round by content (a tracking comment containing at least one finding, plus any immediately following comment continuing the same numbered sequence), state how non-review tracking comments are excluded, and extend C-3 to cover `claude.yml`.

---

### 10. FR-1 does not define argument parsing, and a real spec directory in this repository contains spaces (Score: 66)

`specs/0021-Expose Unacceptable Message Window/` contains two spaces. `/spec:show-me 0021-Expose Unacceptable Message Window` is four `$ARGUMENTS` words, and the document never says whether the argument is the first whitespace-delimited token or the whole of `$ARGUMENTS`. Under the token reading, FR-1's own prescribed remedy ("re-run with the full directory name") is unachievable for that directory.

**Evidence**: `ls specs/` shows `0021-Expose Unacceptable Message Window`; FR-1 never states the argument-capture rule.

**Recommendation**: State that the argument is the whole of `$ARGUMENTS` trimmed (or that quoting is required), and add an AC covering a directory name with spaces. Add a clause excluding non-directory entries (`README.md`, `dlq-review-findings.md`) from candidacy.

---

### 11. FR-8 has no status for a withdrawn/superseded requirement and no rule for sub-numbered requirements — both occur in the calibration spec (Score: 64)

Spec 0036's own review comments include a requirement withdrawn mid-spec ("NFR-1's withdrawal understates the blast radius") — not `Deferred`, `Dropped`, or `Shipped` — and cite sub-numbered requirements (`FR-27.3`) that FR-8 never says how to count (own row? folded into the parent? ignored?), which changes the row count and F5.

**Evidence**: PR #4282 review headings referencing an NFR withdrawal and `FR-27.3`; FR-8's closed status set; AC-15's "exactly 17 rows".

**Recommendation**: Add a `Withdrawn`/`Superseded` status with a defined F5 treatment, and state the granularity rule for sub-numbered requirements. Add an AC for a `requirements.md` containing `FR-n.m`.

---

### 12. FR-10 does not say which branch wins when a local and a remote branch share the name (Score: 62)

FR-10 rule 1 says "a local or remote branch named `spec/{…}`" without precedence. For spec 0036 both `spec/scoped-lifetime-per-pipeline` and `remotes/origin/spec/scoped-lifetime-per-pipeline` exist and can diverge, changing head sha, commit count, and every blast-radius number that NFR-1 requires to be stable. The `origin/master` vs. `master` base-ref fallback has the same silent staleness issue.

**Evidence**: Definitions "Base ref"; FR-10 rule 1; `git branch -a` shows both local and remote forms for the calibration branch.

**Recommendation**: State precedence explicitly (remote-tracking preferred, or vice versa) and require `## Blast radius` to name the exact ref and sha used.

---

### 13. FR-16's absence table is incomplete, and only four of its nine rows have an acceptance criterion (Score: 62)

Missing rows: an ADR named in `.adr-list` whose file doesn't exist (not hypothetical — this repo currently has duplicate ADR numbers across branches, see Finding 2); `requirements.md` present but with zero numbered requirements (AC-15's "sums to 17" has no zero case); `.issue-number` missing (listed as an `Inputs used` row in FR-15 but absent from FR-16). Of FR-16's nine rows, only four have a matching AC — none for "PR exists but no review round ran", "no CI run found", "`.adr-list` missing/empty", or "`PROMPT.md` absent", and the first two directly set risk factors.

**Evidence**: FR-16's nine-row table vs. AC-14/AC-16/AC-19/AC-28 (the only ACs citing a specific FR-16 row).

**Recommendation**: Add the missing rows with exact fallback text and factor consequences, and add one AC per FR-16 row.

---

### 14. NFR-3 and NFR-6 have no acceptance criteria, and NFR-3's file-read budget collides with FR-6/FR-8's stated obligations (Score: 60)

Neither NFR-3 (Bounded cost) nor NFR-6 (Convention conformance) is cited by any AC. NFR-3's "at most 20 individual files" cap is tight against FR-6 (reads every ADR's front matter and body — 7 files for spec 0036 alone), FR-7 (ADR Consequences sections), and FR-8 (per-requirement `tasks.md` evidence) combined — a spec with 15+ ADRs could blow the budget before reaching `tasks.md`, and NFR-3 doesn't say what happens when targeted extraction doesn't suffice.

**Evidence**: No AC parenthetical cites NFR-3 or NFR-6; NFR-3's 20-file cap vs. FR-6/FR-7/FR-8's combined read requirements.

**Recommendation**: Add an AC for each NFR. For NFR-3, either raise/parameterise the cap (e.g. "20 files plus one per ADR in `.adr-list`") or state the degradation when the cap binds, with a matching AC.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 1 |
| 70-89 (High) | 6 |
| 50-69 (Medium) | 12 |
| 0-49 (Low) | 2 |

**Total findings**: 21
**Findings at or above threshold (60)**: 14

## Findings below threshold (recorded for completeness, not blocking)

- **FR-7 classification should allow combined values (Score: 55)** — `release_notes.md`'s actual spec-0036 entries use combined markers (e.g. *"source and binary"*) on 5 of 14 items; FR-7/AC-13 currently force an exclusive single choice.
- **Worked-example bucket counts don't sum to the stated total (Score: 52)** — 393+76+24+14 = 507, not the stated 517; the missing 10 are `other`-bucket files. Add the `other`/`.github/` figures to the worked example.
- **FR-7's worked example misdescribes the calibration change (Score: 52)** — the "eight interfaces gaining `CreatePipelineScope()`" phrasing conflates two separate `release_notes.md` catalogue items (`CreatePipelineScope()` on seven interfaces; `PipelineScope` + `IAsyncDisposable` on `IAmALifetime`).
- **FR-17's stated rationale is wrong for `PROMPT-*.md` companions (Score: 50)** — only `PROMPT.md` itself is gitignored; the companion files are merely untracked. Restate the rule as "not tracked in git" rather than "gitignored".
- **"No non-zero exit" / "exited successfully" isn't testable for a slash command (Score: 45)** — there's no process exit status to assert on; replace with an observable property (completes and reports per FR-19, no error/refusal).
- **Problem Statement's commit count is slightly off (Score: 40)** — states "360 commit messages"; measured count is 363. Say "over 360" or correct it.

## Verified correct (recorded so a future reviewer doesn't re-check)

Spec 0036's `tasks.md`: exactly 82 checkboxes, 0 unchecked, 229,159 bytes. `.adr-list` names exactly ADRs 0070–0076. `.issue-number` is 4256 (PR #4282). Branch diff: exactly 517 files / 76 `src/` / 393 `tests/` / 24 `specs/` / 14 `docs/`. `release_notes.md` exists at the root with the `## Master` heading convention and catalogues exactly 14 breaking-change items for spec 0036. The FR-3 checkbox regex yields exactly 82 on that file. `specs/0037-show-me/` has no `tasks.md` (AC-8 holds). `.claude/commands/spec/*.md` use the assumed front matter and `$ARGUMENTS`. `.requirements-approved`/`.design-approved`/`.tasks-approved`/`.code-approved`, `.adr-list`, `.issue-number`, `.current-spec` are all real conventions. `adr:read_adr_metadata` exists.
