# Tasks — `/spec:show-me`

**Spec**: [requirements.md](requirements.md) · **Design**: [0072-show-me-command-resolution-and-output](../../docs/adr/0072-show-me-command-resolution-and-output.md), [0073-show-me-advisory-risk-model](../../docs/adr/0073-show-me-advisory-risk-model.md), [0077-show-me-visual-explanation](../../docs/adr/0077-show-me-visual-explanation.md), [0078-spec-family-machine-readable-forms](../../docs/adr/0078-spec-family-machine-readable-forms.md) · **Issue**: none (`specs/0037-show-me/.issue-number` is absent; the spec came from a wrap-up note in spec 0036)

This list replaces the pre-rescope task list completely. Numbering starts fresh, and every box starts unchecked. Every checkbox uses the tag-first form that FR-22 prescribes. Only the four tags exist.

## How to read this list

**Two shapes of behavioural task.** Both are tagged `TEST + IMPLEMENT`, and both carry the ⛔ gate line.

1. **Script behaviour** is test-first through `/test-first`. The code is in `.claude/commands/spec/show_me_facts.cs` or in the test script's harness. The "test" is not an xUnit file. It is one or more rows in `.claude/commands/spec/show_me_facts_tests.cs`, a C# file-based app with no test framework, run from the repository root as `dotnet run .claude/commands/spec/show_me_facts_tests.cs`. You write the row, watch it fail, and then write the script code that makes it pass (ADR 0072, Implementation Approach preamble).
2. **Command-file behaviour** has no `/test-first` line. This covers `show-me.md`, `write_release_notes.md` and the amendments to `/spec:requirements`, `/spec:tasks`, `/spec:review` and `/spec:design`. A markdown prompt executed by Claude Code has no unit that `/test-first` can build a test around, and NFR-9 limits the automated net to the measurement script plus the FR-13 check. Each of these tasks therefore gives a **Verify by** block after the gate, with three parts: (a) the exact invocations, (b) the exact fixture, and (c) the assertions taken from the ACs.
   - Three exceptions follow from the ADRs. The FR-13 invariant check is a test-script row (T13.1), so it does use `/test-first`. ADR 0072 Implementation Approach step 7 (unpinned ref resolution) is script code but takes the Verify-by shape (T6.1, T6.2), because fixture runs null every ref field by design (FR-21). ADR 0072 records this gap as a Negative. ADR 0072 Implementation Approach step 8 (the atomic write and the over-cap refusal) is script code verified by inspecting the write path (AC-83), so T7.1 also takes the Verify-by shape; its residue row is a regression guard that cannot be observed red, and the task says so.

**STRUCTURAL / DOC / PROJECT** tasks have no gate and no `/test-first`. Each one carries an acceptance check and a `Traces to:` line.

**Gear.** The ⛔ line states the default. The gear itself lives in `.current-gear` and is changed with `/spec:gear`, which can be scoped to one of the `## Phase N — …` headings below. Keep those headings stable.

## Shared rules (stated once; tasks cite them as R1–R8)

- **R1 — Baselines.**
  - Before T1.1, record `PRE=$(git rev-parse HEAD)`. Every "one added line" or "no other criterion changed" check diffs against `$PRE`, except where a task names a later baseline.
  - When T15.3's commit is made, record `POST_T15_3=$(git rev-parse HEAD)`. T15.6 diffs `review.md` and `design.md` against it, because T15.3 has already added two checks to `review.md`.
  - Every "`git status --porcelain` byte-identical" check compares two captures taken inside the same Verify block, one before the run and one after. Standing entries, such as a temporarily modified fixture, are therefore constant and cancel out.
- **R2 — Real fixtures are restored.**
  - A task that changes a tracked path in a real fixture restores it with `git checkout -- {path}` as the last step of its Verify block. This includes `specs/.current-spec`, which is tracked.
  - Untracked additions are deleted.
  - Each Verify block names every path it touches.
- **R3 — Generated output is removed.** Every `/spec:show-me` run against a real spec directory ends by deleting that directory's `show-me.md` and `.show-me-ledger.json`, unless the task's next step inspects them. A direct script run deletes the ledger it wrote.
- **R4 — The disposable clone.** Use it for ref manipulation, constructed spec branches, synthetic spec directories, and synthetic ADR or `release_notes.md` edits. Never do these things in the working repository.
  - **How a clone's refs are set up.** A `git clone` of a local repository mirrors the source's **local** heads. The clone's only local branch is the one checked out (`spec/show-me`), and every source branch `X` arrives as `refs/remotes/origin/X` only. Two consequences follow:
    - A revision in the clone must name a source branch as `origin/{name}` (for example `origin/spec/scoped-lifetime-per-pipeline~1`). A bare `spec/scoped-lifetime-per-pipeline~1` does not resolve there, because git does not look up a remote-only branch by its short name when it parses a revision.
    - The clone's `origin/master` is the source's local `master`, not the real `origin/master`. On 2026-09-26 that was 286 commits behind, and it gave merge base `09f5d988f` against `91d549be6` instead of `6145913a0`.
  - Build the clone with a setup script kept in the scratch directory, never committed. Its steps, in this order:
    1. `git clone <repo> "$SCRATCH/showme-clone"`. The clone starts on the committed `spec/show-me`.
    2. Point the clone's `origin/master` at the source's real `origin/master`: `git -C "$SCRATCH/showme-clone" update-ref refs/remotes/origin/master "$(git -C <repo> rev-parse origin/master)"`. A local clone shares the source's objects, so the commit is present. If `git -C "$SCRATCH/showme-clone" cat-file -e origin/master^{commit}` fails, run `git -C "$SCRATCH/showme-clone" fetch origin +refs/remotes/origin/master:refs/remotes/origin/master` instead. Then confirm that `git merge-base origin/master origin/spec/scoped-lifetime-per-pipeline` gives the same sha in the clone as in the source (in the source the name is the real remote-tracking ref; in the clone it mirrors the source's local branch, both at `91d549be6` on 2026-09-26).
    3. Build every R7 row that has a branch. Each `spec/{name}` is created off `origin/master` with one commit holding the row's source changes and its spec directory. K14's two branches are the exception: they are created off `spec/show-me` (see R7).
    4. On the working branch, write each row's `specs/NNNN-name/` (every row except K12 and K14) and each synthetic ADR, and **stage them without committing**. Staging makes them tracked for FR-17. Not committing means FR-10 rule 3 never resolves them to `HEAD`. Make K7's `release_notes.md` edit in the working tree and leave it unstaged. Apply the source changes of K5, K6 and K7 to the working tree as well, and leave them unstaged too (R7, *Source changes*).
    5. Apply K12's ref edits last.
  - **Clone figures are not calibration values.** Merge bases, diff figures, levels and ledger values measured in the clone depend on the clone's refs. Only the figures in the *Real fixtures* table below are calibration values, and no clone result is compared with them.
  - Start a Claude Code session **in the clone** so that its `.claude/settings.json` applies.
  - Rebuild the clone whenever the command or the script has changed since the last build. Delete the clone when you are done. Nothing built in it is ever pushed.
  - In the clone, `origin` is a filesystem path, so `gh pr list` fails and every clone run is FR-16 row 2 (`gh unavailable`) unless R5 is used.
- **R5 — Stand-in `gh`.** This is an executable `gh` in `$SCRATCH/fake-gh/`, placed first on the `PATH` of the shell or Claude Code session that starts the run. It answers `pr list …` with canned JSON and exits 0; variants exit 1, or sleep 60 s. Remove it afterwards.
- **R6 — Running things.**
  - Direct script runs use `dotnet run .claude/commands/spec/show_me_facts.cs -- {target} [options]` from the repository root. A `specs/…` target matches the T1.3 allow entry, and a fixture path prompts for permission, which is expected.
  - Command runs are made in a Claude Code session at the repository root. Restart the session after editing a command file so the new text is loaded.
  - Commit each task before building an R4 clone from it.
- **R7 — Clone fixtures (K1–K14).** The R4 setup script builds each of them from this table, and the table gives every file a K-fixture run needs. T15.3 and T15.4 build their own ad hoc directories in the clone, described in those tasks. Where a `spec/…` branch is given, the directory name without `NNNN-` equals the branch name after `spec/`.
  - **The default spec directory.** Unless a row says otherwise, a K directory holds exactly these files, so FR-3's gate passes on every one of them:
    - `requirements.md` declaring `**FR-1 — …**`, `**FR-2 — …**` and `**FR-3 — …**` in the bold lead-in form.
    - `tasks.md` in which every checkbox is checked and tag-first. It has one `- [x] **TEST + IMPLEMENT: T1.n — …**` line per declared id. Each line's `Traces to:` sub-line names that id, and its text says the id shipped as stated, so every id has `Shipped` evidence.
    - No `.adr-list`.
  - **Source changes** are committed on the row's branch. They are comment-only edits unless the row says otherwise.
    - The script measures them from the branch, but the Explainer reads participants in the **working tree** (ADR 0077: `wc -c`, `tail`/`head` and `grep -n -F` on paths that pass `git ls-files`). So a row whose outcome depends on what the Explainer reads must carry its source content there too.
    - K5, K6 and K7 are those rows. R4 step 4 applies their source changes to the working branch's working tree as unstaged edits, so the files stay tracked paths with the row's content. Without this, K7's five files keep their normal few-KB size, fit the reserve, and AC-58's exhaustion is never produced.
    - K5, K6 and K7 therefore use files that no other K row changes, and none shared with each other. The unstaged edits do not change any ledger value: the script's diff figures are measured between committed refs (merge base..measured head), not from the working tree, and no ledger field reads these source files.
  - **Synthetic ADRs** are written and staged on the working branch (R4 step 4). Each has YAML front matter (`title`, `status`), a `## Status` section reading `Accepted`, and a `## Consequences` section, so the script finds all three extract parts.

| K | Directory / refs | Content (beyond the default) | Used for |
|---|---|---|---|
| K1 | `specs/9001-small-change/`, `spec/small-change` | 2 files under `src/Paramore.Brighter/`: 1 subdirectory, 0 public-API lines. `.adr-list` = `0062-pg-advisory-lock-sha256.md` (1 resolved). No trigger fires. | AC-57, AC-26 (a complete 2-path list, FR-14) |
| K2 | `specs/9002-low-risk/`, `spec/low-risk` | 4 files in `src/Paramore.Brighter/`. `.adr-list` = `0062-pg-advisory-lock-sha256.md`. A variant has `.adr-list` deleted. | AC-21, AC-25 (Low), AC-27, AC-28, AC-40, AC-55 |
| K3 | `specs/9003-declarations/`, `spec/declarations` | AC-66's three shapes applied to real lines in files under `src/Paramore.Brighter/`: one added `public` declaration; one existing `protected` declaration removed; and one existing `public` method's parameter list changed, which gives a `-`/`+` pair. A removed line must exist at the merge base, so the removal and the change use lines already on `origin/master`. The removal and the change are the 2 breaking changes (F2 `Medium`). The default tasks keep F5 `Low`. | AC-66, AC-22 |
| K4 | `specs/9004-props/`, `spec/props` | `src/Directory.Build.props` plus 4 files in `src/Paramore.Brighter/` | AC-67 |
| K5 | `specs/9005-ten-dtos/`, `spec/ten-dtos` | One added `public` property on each of ten unrelated classes, one class per file, and all ten files in `src/Paramore.Brighter/`. That gives 10 files in 1 subdirectory and 10 public-API lines, so D2 fires and D1 does not. | AC-64 |
| K6 | `specs/9006-tree/`, `spec/tree` | 3–5 changed files in one namespace hierarchy under `src/Paramore.Brighter/`. `.adr-list` = `0062-pg-advisory-lock-sha256.md` and `0072-show-me-command-resolution-and-output.md` (2 resolved, so D3 fires). | AC-65, AC-60 |
| K7 | `specs/9007-budget/`, `spec/budget` | `tasks.md` is the default three lines padded with further checked `DOC` lines to about 940,000 B. There are 5 changed files across 2 `src/` subdirectories, each padded past 150,000 B with lines naming the one type the relationship concerns; the padding is also in the working tree (R7). The clone's `release_notes.md` gets a section under `## Master`, marked `<!-- spec: 9007-budget -->`, whose `#### Breaking changes` list exceeds 30,000 B. That edit is left unstaged. | AC-58, AC-68 |
| K8 | `specs/9008-wide/`, no branch | A 15-entry `.adr-list` of real ADRs, each named by its full filename. `tasks.md` is a copy of 0036's (229,159 B, 82 checked, tag-first). | AC-52 |
| K9 | `specs/9009-reconcile-17/`, no branch | `requirements.md` declares FR-1…FR-12 and NFR-1…NFR-5 in the bold lead-in form, including a `**FR-7.2 — …**` sub-clause and prose cross-references to other ids. `tasks.md` has one checked, tag-first line per top-level id: 16 show the id shipped as stated, and `FR-12`'s line records it as deferred to follow-up issue `#9009`. | AC-15 |
| K10 | `specs/9010-withdrawn/`, no branch | `requirements.md` declares `**FR-27 — …**` with `**FR-27.1 — …**`, `**FR-27.2 — …**` and `**FR-27.3 — …**`, and `**FR-28 — …**`. `tasks.md` has checked, tag-first lines showing FR-27.1, FR-27.2 and FR-28 shipped, plus a checked `DOC` line recording FR-27.3's withdrawal. `.adr-list` = `9010-withdraw-fr-27-3.md`. The synthetic ADR `docs/adr/9010-withdraw-fr-27-3.md` records in `## Consequences` that FR-27.3 is withdrawn and superseded by FR-28. | AC-45 |
| K11 | `specs/9011-twenty-eight/`, no branch | `requirements.md` declares FR-1…FR-20 and NFR-1…NFR-8 (28 ids). `tasks.md` has one checked, tag-first line per id: 26 show the id shipped as stated, `FR-5`'s line records a deviation it shipped with, and `FR-9`'s line records it as deferred to follow-up issue `#9011`. A variant `tasks.md` shows all 28 shipped as stated. | AC-54, AC-55 |
| K12 | ref-only edits, applied last | `git branch spec/sqs-cleanup origin/master`. `git update-ref refs/remotes/origin/spec/small-change origin/master`, which leaves K1's local `spec/small-change` unmerged. `git branch spec/scoped-lifetime-per-pipeline origin/spec/scoped-lifetime-per-pipeline~1`, which puts the local branch at `386efee78` while the remote-tracking ref stays at `91d549be6`. | AC-85, AC-47 |
| K13 | `specs/9015-two-breaks/`, no branch | `.adr-list` = `9015-two-breaks.md`. The synthetic ADR `docs/adr/9015-two-breaks.md` records two breaking changes in `## Consequences`, each with its classification and a migration. | AC-89, AC-90, AC-91, AC-92 |
| K14 | `specs/9020-tiny-thing/`, branches `wip/tiny-thing` and `other` | Both branches are made off the clone's `spec/show-me`, so the script is present when they are checked out. Each has one commit adding the default directory. It is **not** staged on the working branch, because a staged copy would conflict with the committed one on checkout. There is no `spec/tiny-thing`. | FR-10 rules 2 and 3 (T6.1) |

- **R8 — The (C-8) window.**
  - `specs/0036-scoped-lifetime-per-pipeline/` is in the working tree because merge commit `72882520a` brought it in.
  - PR #4282 was OPEN at head `91d549be6` on 2026-09-26.
  - Every (C-8) check must run while #4282 is open, and therefore before T18.1.

**Real fixtures (verified on `spec/show-me`, 2026-09-26)**

| Path | Property |
|---|---|
| `specs/0002-backstop-error-handler/`, `specs/0002-sqs-cleanup/`, `specs/0002-universal_scheduler_delay/` | ambiguity on `0002`. `sqs-cleanup` is unique; it is complete (6/6), has no `.issue-number`, and declares **0** ids in the bold lead-in form |
| `specs/0021-Expose Unacceptable Message Window/` | a name with spaces; complete (4/4) |
| `specs/0005-defer-message-on-error/` | `requirements.md` only (no `tasks.md`) |
| `specs/0023-asyncapi-document-generation/` | `tasks.md` with 0 checkboxes |
| `specs/0003-remove_clear_event_bus_calls/` | unfinished: 2 of 10 unchecked |
| `specs/0033-pg-advisory-lock-sha256/` | complete (5/5); 8 declared ids; `.issue-number` 4145; `.adr-list` = `0062-pg-advisory-lock-sha256.md`; no spec branch, so every FR-10 rule fails; no `(spec 0033` heading in `release_notes.md` |
| `specs/0036-scoped-lifetime-per-pipeline/` (C-8) | 82/82 tasks (62/12/2/6/0 by tag); 37 ids; 7 ADRs, among them `0070-…`/`0071-…`/`0072-…`/`0073-…`, whose numbers are duplicated in `docs/adr/`; `tasks.md` 229,159 B; `requirements.md` 273,674 B. Over `6145913a0..91d549be6`: 517 files (76/393/14/24/0/10), `src/` diff 303,715 B, 131 public API lines, 6 `src/` subdirectories, 363 commits, +45,284/−515 lines |
| `docs/adr/0037-*.md` | five files, which gives the C-9 ambiguity |
| `release_notes.md` | 118,145 B; first `##` is `## Master`, followed by an **unmarked** `### Scoped lifetime per pipeline (spec 0036, #4256)`; zero marker lines |

## Phase 1 — Configuration, fixtures and clean-up

*ADR 0072 Implementation Approach steps 1–2. Dependencies: none. T1.1 comes first.*

- [ ] **STRUCTURAL: T1.1 — Remove the staged pre-rescope fixture `specs/9999-show-me-fixture/`**
  - Do: `git rm -r --cached specs/9999-show-me-fixture/`, then `rm -rf specs/9999-show-me-fixture/`.
  - Acceptance: `git status --porcelain specs/` shows no `specs/9999-show-me-fixture/` entry; `ls specs/9999-show-me-fixture` fails; nothing is committed for it.
  - Traces to: NFR-9 (no synthetic fixture under `specs/`); ADR 0072 KC6 *Fixtures*.

- [ ] **STRUCTURAL: T1.2 — Add the exact-match `.gitignore` line for the fact ledger**
  - Do: append the single line `.show-me-ledger.json`.
  - Acceptance: `git diff $PRE -- .gitignore` shows exactly one added line and no edited line. After `touch specs/0033-pg-advisory-lock-sha256/.show-me-ledger.json`, `git check-ignore -v` on that path names the new line; then delete the file.
  - Traces to: FR-4, NFR-8, AC-74, AC-82; ADR 0072 *Where each artefact is touched*, IA 1.

- [ ] **STRUCTURAL: T1.3 — Add the one path-scoped allow-list entry**
  - Do: add `Bash(dotnet run .claude/commands/spec/show_me_facts.cs -- specs/:*)` to `allow` in `.claude/settings.json`, inserted as a new line **before the array's last element** (`"Bash(dotnet --list-runtimes)"` on 2026-09-26) and ending in a comma. Appending it after the last element would add a comma to that element's line and so edit a second line.
  - Acceptance: `git diff $PRE -- .claude/settings.json` shows exactly this one added line. The `gh` entries and the `deny` array are unchanged. No `Bash(dotnet run:*)` or other interpreter grant exists.
  - Traces to: FR-18, C-10, AC-82; ADR 0072 *Why the one allow-list entry names the script*, IA 1.

- [ ] **STRUCTURAL: T1.4 — Create NFR-9's fixture tree under `.claude/test-fixtures/show-me/`**
  - Do: create these tracked files, each with the content NFR-9's table states:
    - `declared/`: `requirements.md` declaring `**FR-1 — …**`, `- **FR-2 — …**` and `**NFR-1 — …**`, plus one prose cross-reference; `tasks.md` of two checked, untagged lines; `.adr-list` of `0062-pg-advisory-lock-sha256.md` and `docs/adr/0072-show-me-command-resolution-and-output.md`.
    - `zero-id/`: `requirements.md` with prose-only ids and one legacy `#### FR-9: …` heading; `tasks.md` of three checked lines with the lead-ins `**TEST + IMPLEMENT: …**`, `**DOC TIDY: …**` and `**T1.1 — STRUCTURAL: …**`; no `.adr-list`.
    - `no-tasks/`: `requirements.md` only.
    - `adr-unresolved/`: `tasks.md` of one checked, untagged line; no `requirements.md`; an `.adr-list` of five entries, one per line. They are: `0000-no-such-adr.md`, a missing file; `0037`, a bare number matching the five `0037-*` files; `0062`, a bare number matching exactly one file (`0062-pg-advisory-lock-sha256.md`, the only `0062-*` on 2026-09-26); `adr/0062-pg-advisory-lock-sha256.md`, a path prefix other than `docs/adr/`; and `0062-pg-advisory-lock-sha256.md`, which resolves. NFR-9's table is a minimum, and this fixture gives FR-16 row 7's unresolved branches a failing row (T3.6).
    - `unfinished/`: `tasks.md` with one checked line plus the unchecked lines titled `**DOC: Alpha**` and `**DOC: Beta**`.
    - `release-notes.md`: the release-notes fixture file, laid out exactly as NFR-9's row states. It has one section marked `<!-- spec: declared -->` with 2 top-level bullets, 1 indented sub-bullet, and between the bullets a fence containing a column-0 `- ` line and a column-0 `# ` line. It then has `#### Usage` with 2 bullets and a fence holding a column-0 `- ` line. Last comes an unmarked section with 3 bullets.
    - `wordcount-eight.md`: exactly 8 counted tokens; one fenced block holding 8 more; an H1 and metadata block, a `|`-leading line, and an `## Inputs used` tail, each holding tokens NFR-2 excludes.
    - `wordcount-conforming.md`: a counted body of a known exact total between 400 and 2,000 (record the total in a comment line inside the fixture's excluded tail).
    - `probe.cs`: a file-based app that writes nothing and exits **77**, a status no other row uses.
  - Acceptance:
    - `git ls-files .claude/test-fixtures/show-me/` lists every file. No file is named `show-me.md`.
    - No `.md` file sits under `.claude/commands/` for a fixture, and `/spec:status` shows no new spec.
  - Traces to: NFR-9 fixture table, FR-16 row 7 (`adr-unresolved/`), AC-42 (ledger half), AC-79, AC-80, AC-81 (literal line lives in the test script, T13.1), AC-83; ADR 0072 KC6, IA 2.

## Phase 2 — Test-script harness

*ADR 0072 IA 3. Depends on Phase 1.*

- [ ] **TEST + IMPLEMENT: T2.1 — The test script runs fixture rows, reports each failed assertion by row, and exits non-zero on failure**
  - **USE COMMAND**: `/test-first when the show-me test script runs a fixture row whose measurement fails it should print the failed assertion and exit non-zero`
  - Test script: `.claude/commands/spec/show_me_facts_tests.cs` (new)
  - Test row(s): the NFR-9 *declared* row, unpinned, asserting only exit `0` and a ledger that parses as one JSON object. It is red because `show_me_facts.cs` does not exist yet.
  - Test should verify: the run prints `FAIL declared: …` naming the failed assertion, and exits non-zero.
  - Not verified here: the harness's save-and-restore of ledgers and AC-79's perturbation clause. Both need a script that writes a ledger and a row that can pass, so both are added and verified in T3.1, the first row that writes a ledger.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Be a C# file-based app, committed at mode `100644`, with no first-line marker. It uses no test framework and references nothing under `src/` or `tests/`.
    - Hold a table of rows. Each row starts `dotnet run .claude/commands/spec/show_me_facts.cs -- {args}` as a child process from the repository root, with both streams captured. It never parses stdout, and it reads only the last stderr line carrying a given prefix.
    - Parse ledgers with `JsonDocument`. Print each failed assertion with its row name, then a summary, and set the exit code.
  - Traces to: NFR-9, AC-79; ADR 0072 KC6, *Why the test script is also C#*, *Why the payload travels in a file*, IA 3 (invocation, parsing, reporting, exit code).

## Phase 3 — Script file fields and the gate

*ADR 0072 IA 4, plus the harness's ledger save-and-restore from IA 3. Depends on T2.1. Tasks run in order. The atomic write and the over-cap refusal of IA 8 are T7.1's.*

- [ ] **TEST + IMPLEMENT: T3.1 — Measuring a fixture directory writes one well-formed JSON ledger with every ref and diff field null for `not a spec directory`, and the test script leaves every ledger as it found it**
  - **USE COMMAND**: `/test-first when the measurement script measures a fixture directory it should write a single JSON ledger whose ref and diff fields are null with reason not a spec directory`
  - Test script: `.claude/commands/spec/show_me_facts_tests.cs`
  - Test row(s): the *declared* row, unpinned.
  - Test should verify:
    - Exit `0`, and `declared/.show-me-ledger.json` parses as a single object with `schema_version` 1, `target` as given and `pinned` false.
    - Every ref field (`spec_branch`, `rules_tried`, `local_divergence`, `base`, `pr`, `pr_count`, `measured_head`, `merge_base`) and every diff field (`buckets`, `src_subdirectory_count`, `public_api_lines`, `commits`, `src_diff`, `f1_level`, `triggers`) is null, with `null_reasons` reading `not a spec directory`.
    - `gh_commands` is empty. The ledger is ≤ 65,536 B and is indented (more than one line).
    - **Harness checks.** They are moved here from T2.1, because this is the first row that writes a ledger.
      - A dummy `.show-me-ledger.json` planted in `declared/` before the test run is byte-identical afterwards (`cmp` against a copy saved first).
      - With nothing planted, no ledger remains in `declared/` afterwards.
      - Changing any one expected value of this row to a wrong value makes the row print which assertion failed, and the script exits non-zero (AC-79's perturbation clause). Revert the change afterwards.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Create `.claude/commands/spec/show_me_facts.cs` as a C# file-based app at mode `100644` with no marker.
    - Build the ledger as a `JsonObject` and write it with `ToJsonString`, using `WriteIndented` and `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`. Use no reflection-based `JsonSerializer`.
    - Treat any target not directly under `specs/` as FR-21's second kind of run.
    - Write the ledger with a plain, direct write to `.show-me-ledger.json`. T7.1 replaces this with the atomic replacement, its `finally` clean-up and the over-cap refusal.
    - Write data to no stream except the two stderr records.
    - In the test script's harness, save each target's ledger bytes before a row. Afterwards, restore them, or delete a ledger the row created.
  - Traces to: FR-21 (artefact, ledger contract, kinds of run), FR-4, NFR-1, NFR-6, NFR-9 (ledgers left as found), AC-70 (ledger half), AC-79; ADR 0072 KC1, KC2, *Why the script is a C# file-based app*, IA 3 (save and restore), IA 4.

- [ ] **TEST + IMPLEMENT: T3.2 — The script accepts exactly its argument grammar, `--` keeps `dotnet run` from reading later options, and a pinned sha that is not a local commit is a tooling fault**
  - **USE COMMAND**: `/test-first when the measurement script is given an argument outside its grammar or an absent pinned sha it should exit with a tooling-fault status and write no ledger`
  - Test script: `.claude/commands/spec/show_me_facts_tests.cs`
  - Test row(s):
    - Usage errors: no argument; `--bogus`; `--pinned` with one sha.
    - The **`--` probe row**: `-- specs/x --file .claude/test-fixtures/show-me/probe.cs`.
    - `declared/` pinned to an all-zero 40-hex sha.
    - The *declared* row pinned to `6145913a0 91d549be6` with `--release-notes`, in both option orders.
    - The *declared* row with `--release-notes` alone, unpinned.
    - The calibration row `specs/0036-scoped-lifetime-per-pipeline/` pinned to the same pair.
  - Test should verify:
    - Usage errors and the zero sha exit with a status other than `0`, `2` or `77`, and create no ledger (AC-93, second half). The probe row's status is not 77, so the probe did not run. The row prints `dotnet --version`.
    - The `--release-notes`-only row exits `0`, writes a ledger, and has `pinned` false.
    - On both pinned rows: `pinned` true; `merge_base` and `measured_head` hold the pinned shas, with `measured_head.source` `pinned`; every other ref field is null with reason `pinned`; no field carries `not a spec directory`. The diff-field assertions belong to Phase 5.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Parse exactly the forms in ADR 0072 KC1's table: the target alone; the target with `--pinned {base} {head}`, `--release-notes {path}`, or both, in either order; and `{file} --word-count`. Everything else is a usage error that exits with a fixed status other than `0`/`2`.
    - Confirm each pinned sha with a `git cat-file -e {sha}^{commit}` child process.
    - For pinned runs, apply the pinned row of the *kinds of run* table, which takes precedence over the target's kind.
  - Risk mitigation: the probe row is the tripwire for a later SDK that re-parses options after `--`.
  - Traces to: FR-21 (pinned invocation, inputs not hooks), FR-18, AC-82 (why `--` matters), AC-93; ADR 0072 KC1, KC6 *Arguments are checked*, *Why the one allow-list entry names the script*, Risks (SDK change), IA 4.

- [ ] **TEST + IMPLEMENT: T3.3 — Task checkboxes, per-tag counts and `tasks.md` windows are counted by the stated patterns, and copied text survives the encoder**
  - **USE COMMAND**: `/test-first when the measurement script counts task checkboxes it should report total, unchecked and per-tag counts that sum to the total, with tasks.md windows`
  - Test script: `.claude/commands/spec/show_me_facts_tests.cs`
  - Test row(s): *declared*, *zero-id*, and the calibration row (pinned).
  - Test should verify:
    - *declared*: 2 checkboxes, 0 unchecked; `by_tag` = 2 `untagged`.
    - *zero-id*: 3 checkboxes, 0 unchecked; 1 `TEST + IMPLEMENT`, 2 `untagged` (`DOC TIDY` and the drifted `T1.1 — STRUCTURAL` form).
    - Calibration: 82 checkboxes, 0 unchecked; 62/12/2/6/0.
    - In every row, the four tags plus `untagged` sum to the total.
    - `tasks.bytes` equals the file size (calibration 229,159). The windows are contiguous from line 1 to the end, each ≤ 25,000 B, and their bytes sum to `tasks.bytes`.
    - The calibration ledger's **raw text** contains `"TEST + IMPLEMENT"` literally, which proves the encoder is not the default one.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Implement the task-checkbox and task-type-tag patterns once each, translated to .NET with the POSIX original quoted beside each. Never copy a pattern into any markdown file.
    - Take a title as the remainder after the match, trimmed. `first_unchecked` holds at most three titles.
    - Share one window helper: whole lines, at most 25,000 B per window, and a single longer line forms its own window flagged `oversize`.
  - Traces to: FR-9, FR-3 (counts), FR-21 (patterns once, locating fields), C-2, NFR-1, NFR-9 rows 1, 2 and 4, AC-79; ADR 0072 KC1, KC2 `tasks`, KC6 *The encoder is checked*, IA 4.

- [ ] **TEST + IMPLEMENT: T3.4 — An unfinished or taskless target exits `2`, writes no ledger, and emits one parseable gate record**
  - **USE COMMAND**: `/test-first when the measurement script's target fails the completeness gate it should exit 2 with a show-me-gate record on stderr and write no ledger`
  - Test script: `.claude/commands/spec/show_me_facts_tests.cs`
  - Test row(s): *no-tasks*; *unfinished*; *unfinished* again with a planted ledger.
  - Test should verify:
    - Each row exits `2`, and no ledger exists afterwards where none existed.
    - The last `show-me-gate: ` stderr line parses as one JSON object. For *no-tasks* its case is `tasks.md` absent. For *unfinished* it records unchecked `{n}` = 2, `{total}` = 3, and titles `**DOC: Alpha**` and `**DOC: Beta**`.
    - A planted ledger is byte-identical immediately after the run (AC-71, second half, at script level).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Evaluate FR-3's three cases (absent, zero checkboxes, unchecked) from the file fields **before** any ref resolution or `gh` call.
    - Emit the record as a single-line JSON object with no indentation, relaxed encoder, and fixed field names such as `case`, `unchecked`, `total` and `first_unchecked`, which T9.2 reads.
    - Capture children's stderr so no child line can carry the prefix.
  - Traces to: FR-3, FR-21 (exit `2`, gate record, stderr rule), NFR-8, AC-7, AC-71; ADR 0072 KC1 *stderr records*, KC4, IA 4.

- [ ] **TEST + IMPLEMENT: T3.5 — Declared ids are the distinct anchored lead-in ids, in FR-then-NFR order, each located by its paragraph's windows**
  - **USE COMMAND**: `/test-first when the measurement script reads requirements.md it should report the distinct declared ids in FR-then-NFR order with each declaration's paragraph windows`
  - Test script: `.claude/commands/spec/show_me_facts_tests.cs`
  - Test row(s): *declared*, *zero-id*, and the calibration row.
  - Test should verify:
    - *declared*: `declared_ids` = `FR-1, FR-2, NFR-1`, and `declared_total` 3. `declarations` has 3 entries. Their first line, last line and bytes are computed by hand from the fixture under KC1's paragraph rule and written into the row.
    - *declared*: `requirements.present` true, and its bytes and windows are correct.
    - *zero-id*: `declared_total` 0, with an empty list rather than null; this is FR-16 row 9.
    - Calibration: 37.
    - **Escaped-pipe regression**: the declared-id count must be non-zero for every fixture that declares ids. The row states that the count was once 0 where the answer was 28.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Implement the declared-id pattern once, translated, with the POSIX original quoted.
    - Fold a sub-numbered id into its parent and keep distinct ids in numeric order.
    - Run a paragraph to the line before the next declaration, or to the next heading of level three or higher, or to the end of the file.
    - When `requirements.md` is absent, make the `requirements` size and windows, `declared_ids`, `declared_total` and `declarations` null with `not present`.
  - Traces to: FR-8 (counting rule, folding), FR-21, FR-16 rows 8–9, NFR-1, NFR-9 (regression 1), AC-43 (script half), AC-70, AC-79; ADR 0072 KC1 *locates everything … in parts*, KC2, IA 4.

- [ ] **TEST + IMPLEMENT: T3.6 — `.adr-list` entries resolve by FR-16 row 7's rule, each with its extract windows; a bare number never resolves; and `adr_resolved_count` counts only resolved entries**
  - **USE COMMAND**: `/test-first when the measurement script resolves adr-list entries it should resolve only full filenames and docs/adr paths, give every other entry its row 7 reason, locate each resolved ADR's extract, and count resolved entries`
  - Test script: `.claude/commands/spec/show_me_facts_tests.cs`
  - Test row(s): *declared*, *zero-id*, *adr-unresolved*, and the calibration row.
  - Test should verify:
    - *declared*: two entries, both resolved, the second through the `docs/adr/` path form. `reason` and `matches` are null. Each `extract` has front-matter, `## Status` and `## Consequences` parts with bytes > 0. `adr_resolved_count` is 2.
    - *zero-id* has no `.adr-list`, so `adr_list` is `[]` and `adr_resolved_count` is 0, a measured zero.
    - *adr-unresolved*: five entries, in file order.
      - `0000-no-such-adr.md`: `path` null, `reason` `ADR file not found`.
      - `0037`: `path` null, `reason` `ambiguous ADR number`, and `matches` listing exactly the five `0037-*` filenames.
      - `0062`: `path` null and `reason` `ADR file not found`, although it matches exactly one file.
      - `adr/0062-pg-advisory-lock-sha256.md`: `path` null and `reason` `ADR file not found`.
      - `0062-pg-advisory-lock-sha256.md`: resolved, with its three extract parts.
      - `adr_resolved_count` is **1**. The row is red until the unresolved branches exist.
    - Calibration: 7. This includes names whose numbers are duplicated in `docs/adr/`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Resolve a full filename, or `docs/adr/{filename}`, to that file when it exists. Nothing else resolves. This is FR-16 row 7 ("If an entry is instead a bare number (or otherwise doesn't match any file): … `ADR file not found`") and ADR 0078 KC5 ("anything else does not resolve").
    - Give a bare number that matches more than one `docs/adr/` file `ambiguous ADR number`, listing the `matches`. Give every other unresolved entry `ADR file not found`, including a bare number that matches exactly one file.
    - Count in `adr_resolved_count` only entries that resolved.
    - Recognise headings and fences once. T14.1 repeats the not-found and ambiguous cases by direct run and at command level.
  - Traces to: FR-16 rows 6–7, FR-6 D3's input, FR-21, C-9, AC-42 (ledger half); ADR 0072 KC1, KC2 `adr_list`/`adr_resolved_count`, IA 4.

## Phase 4 — Marked release-notes sections

*ADR 0072 IA 5, reading the form set by ADR 0078 KC4. Depends on Phase 3.*

- [ ] **TEST + IMPLEMENT: T4.1 — Only sections marked for the target are counted, and `{m}` counts only the top-level bullets of their `#### Breaking changes` lists**
  - **USE COMMAND**: `/test-first when the measurement script reads a release-notes file it should count sections marked for the target and their top-level breaking-change bullets only`
  - Test script: `.claude/commands/spec/show_me_facts_tests.cs`
  - Test row(s):
    - Both *declared* runs (unpinned and pinned) with `--release-notes .claude/test-fixtures/show-me/release-notes.md`.
    - *zero-id*, which reads the repository-root `release_notes.md`.
  - Test should verify:
    - *declared*: `release_notes` = `{present: true, count: 1, m: 2}`. The section's first line, last line, bytes and windows equal hand-computed values. The fenced `# ` line ends no list, and the sub-bullet, the fenced lines, the `#### Usage` bullets and the unmarked section are not counted.
    - *zero-id*: `count` 0 and `m` null. The real file's unmarked sections, 0036's `(spec 0036` heading among them, are not counted.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Apply the *Marked release-notes section* Definition: headings outside fences only; a section runs to the next `##`/`###` heading or the end of the file; the marker is compared literally, after trimming, with `<!-- spec: {target directory name} -->`.
    - Apply FR-21's `{m}` rule: `null` versus `0`, and fenced lines skipped. An absent file gives `present` false and `count` 0.
  - Traces to: FR-7, FR-21 (`{m}` rule), NFR-1, AC-92 (script half), AC-79; ADR 0072 KC2 `release_notes`, IA 5; ADR 0078 KC4.

## Phase 5 — Pinned diff fields and threshold boundaries

*ADR 0072 IA 6. Depends on Phases 3 and 4: T5.1's pinned *declared* row compares its file fields with the unpinned row's (T3.3–T3.6, T4.1), and T5.4's D3 reads `adr_resolved_count` (T3.6).*

**How to choose a boundary pair.** This applies to T5.2–T5.4 and was settled for T5.3's named pair.

- Search `git log --first-parent --format=%H origin/master` for commits A and B, with A an ancestor of B, whose `A..B` diff has the required value. Start with adjacent first-parent commits and widen from there.
- Measure the value **independently of the script**: `git diff --name-only A..B -- src/ | wc -l` for file counts, the second path segment for subdirectories, and the public-API pattern as stated in `requirements.md` § *Definitions* under `grep -E` for declaration lines.
- Record in a comment beside the row the two full shas, the measured value, the command used and the date. `master`'s history is never rewritten, so the pair stays reachable.
- Every boundary row targets `.claude/test-fixtures/show-me/declared/`. Only its diff fields are asserted.

- [ ] **TEST + IMPLEMENT: T5.1 — Pinned runs measure buckets, net lines, `src/` subdirectories, public-API lines, commits and the `src/`-scoped diff's windows over exactly the pinned pair**
  - **USE COMMAND**: `/test-first when the measurement script is pinned to the calibration pair it should report 517 files bucketed 76/393/14/24/0/10, 131 public API lines, 6 src subdirectories and 363 commits`
  - Test script: `.claude/commands/spec/show_me_facts_tests.cs`
  - Test row(s): the calibration row, and the pinned *declared* row.
  - Test should verify:
    - Calibration buckets: `src/` 76, `tests/` 393, `docs/` 14, `specs/` 24, `.github/` 0, `other` 10, `total` 517. Total +45,284/−515, of which `src/` is +3,641/−264, measured on 2026-09-26 with independent `git diff --numstat`.
    - `src_subdirectory_count` 6, `public_api_lines` 131, `commits` 363.
    - `src_diff.command` names both pinned shas and the `src/` pathspec. `src_diff.bytes` is 303,715. The windows are contiguous, each ≤ 25,000 B, and sum to 303,715.
    - The calibration ledger's size is printed and is ≤ 65,536. This is the first measurement of ADR 0072's under-20 KB estimate, and a risk check on the cap's headroom.
    - Pinned *declared*: `buckets.src.files` 76; its file fields equal those of its unpinned row.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Take net lines from `--numstat`, treating binary `-` as 0. Take buckets from `--name-only` by first path segment. Take the public-API count from the `src/`-scoped diff under the translated pattern, counted per diff line.
    - Count subdirectories per the Definition, so a file directly under `src/` contributes none. Count commits with `rev-list --count`.
    - Never run a `git diff` without a pathspec or a summary flag.
  - Traces to: FR-9, FR-10, FR-21, NFR-1, NFR-3 (full-diff ban), NFR-9 rows 2–3, AC-18 values, AC-66 (counting rule), AC-79; ADR 0072 KC1, KC2, IA 6.

- [ ] **TEST + IMPLEMENT: T5.2 — `f1_level` applies FR-11's F1 thresholds exactly at 10/11 and 50/51**
  - **USE COMMAND**: `/test-first when the measurement script measures 10, 11, 50 and 51 src files it should report f1_level Low, Medium, Medium and High`
  - Test script: `.claude/commands/spec/show_me_facts_tests.cs`
  - Test row(s): the four F1 boundary rows, plus `f1_level` on the calibration row.
  - Test should verify: `Low`, `Medium`, `Medium`, `High`, with each row's `src/` file count equal to its recorded value; calibration `High`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: compute `f1_level` once, from the `src/` bucket, as a diff field that is null whenever the diff fields are null.
  - Traces to: FR-11 (F1), NFR-1, AC-79; ADR 0072 KC1 *values NFR-1 derives*, IA 6 table rows 1–2; ADR 0073 KC2.

- [ ] **TEST + IMPLEMENT: T5.3 — `triggers.d1` fires only at ≥ 5 `src/` files across ≥ 2 immediate subdirectories, and a file directly under `src/` contributes no subdirectory**
  - **USE COMMAND**: `/test-first when the measurement script evaluates D1 it should fire only for at least five src files across at least two immediate subdirectories`
  - Test script: `.claude/commands/spec/show_me_facts_tests.cs`
  - Test row(s): the three D1 boundary rows (4 files across ≥ 2 subdirectories; ≥ 5 files in 1; 5 across 2), the named pair `c53875f3c..5247862cd`, and the calibration row.
  - Test should verify:
    - D1 is `false`, `false`, `true` on the three boundary rows.
    - The named pair gives 3 `src/` files and `src_subdirectory_count` **1**, not 2. On 2026-09-26 that diff was `src/Directory.Build.props` plus two files in `src/Paramore.Brighter.MessagingGateway.Kafka/`.
    - Calibration: `true`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: evaluate D1 once from the two diff fields, exactly as FR-6 (a) states it.
  - Traces to: FR-6 (a) D1, Definitions (*Immediate subdirectory of `src/`*), NFR-1, AC-67 (script half); ADR 0072 IA 6 table rows 3–5 and 7; ADR 0077 KC4.

- [ ] **TEST + IMPLEMENT: T5.4 — `triggers.d2` fires at ≥ 10 public-API declaration lines and `triggers.d3` at ≥ 2 resolved ADRs**
  - **USE COMMAND**: `/test-first when the measurement script evaluates D2 and D3 it should fire D2 at ten public API lines and D3 at two resolved adr-list entries`
  - Test script: `.claude/commands/spec/show_me_facts_tests.cs`
  - Test row(s): the two D2 boundary rows, the pinned *declared* row (D3), and the calibration row.
  - Test should verify: D2 is `false` at 9 and `true` at 10, with `public_api_lines` as recorded. Pinned *declared*: D3 `true` (2 resolved). Calibration: D2 and D3 both `true`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: evaluate D2 from `public_api_lines` and D3 from `adr_resolved_count`. All three triggers are diff fields, so they are null when no diff was measured.
  - Traces to: FR-6 (a) D2/D3, NFR-1; ADR 0072 KC1, KC2 `triggers`, IA 6 table row 6 (D3 has no boundary row); ADR 0077 KC4.

## Phase 6 — Unpinned ref fields

*ADR 0072 IA 7. This is script code verified at command level. Depends on Phase 5. (C-8) parts need R8.*

- [ ] **TEST + IMPLEMENT: T6.1 — The script resolves the spec branch by FR-10's ordered rules, skipping merged candidates, and records every rule tried and any local divergence**
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Verify by (R6 direct runs; R3 cleanup):
    - (C-8) `-- specs/0036-scoped-lifetime-per-pipeline`:
      - `spec_branch.ref` = `refs/remotes/origin/spec/scoped-lifetime-per-pipeline`; both refs are at `91d549be6`, so `local_divergence` is null.
      - `base` = `origin/master` with its sha. `merge_base` starts `6145913a0`.
      - The diff fields are 517 files / 76 `src/` / 131 API lines / 6 subdirectories / 363 commits.
    - `-- specs/0033-pg-advisory-lock-sha256`:
      - `rules_tried` lists all three rules with their outcomes, and `base` is resolved.
      - Every other ref field and every diff field is null with `spec branch not determinable`. `gh_commands` is empty (AC-19, script half).
    - In the clone (R4; the refs are K12's, and clone figures are not calibration values):
      - `spec/sqs-cleanup` at `origin/master`: not determinable, and `rules_tried` names `{ref} is already merged into origin/master` (AC-85).
      - A merged `origin/spec/small-change` over an unmerged local `spec/small-change`: the local ref is chosen, and the skipped remote ref is named.
      - (C-8) The local `spec/scoped-lifetime-per-pipeline` at `386efee78` (`origin/spec/scoped-lifetime-per-pipeline~1`): the remote-tracking ref at `91d549be6` wins, and `local_divergence` = `{spec/scoped-lifetime-per-pipeline, 386efee7880e32428a96a96ede6d44a21541e48c}` (AC-47).
    - In the clone, K14:
      - With `wip/tiny-thing` checked out, `-- specs/9020-tiny-thing` resolves by rule 2 to `refs/heads/wip/tiny-thing`.
      - With `other` checked out, it resolves by rule 3 to `HEAD`.
      - Check out `spec/show-me` again afterwards; the staged K directories and the unstaged K5–K7 source edits move across with the checkout.
  - Implementation should:
    - Apply rule 1 (remote-tracking first, then local), rule 2, then rule 3. A candidate whose tip is already contained is skipped, with `git merge-base --is-ancestor` as a child process.
    - Choose the base ref as `origin/master`, else `master`.
    - Make `rules_tried` strings in FR-10's wording, and set the nulls and reasons per ADR 0072 KC2. Capture all children's streams.
  - Traces to: FR-10, FR-16 rows 12 and 15, FR-21 (kinds-of-run row 1), C-4, AC-18, AC-19, AC-47, AC-85; ADR 0072 KC2 (`rules_tried`, `local_divergence`, `base`), Negative (ref resolution has no automated test), IA 7.

- [ ] **TEST + IMPLEMENT: T6.2 — The script makes one `gh pr list` query, keeps exact-name open PRs, takes the highest number, uses the PR head only when it is present locally, and treats any `gh` failure as no PR**
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Verify by (R6 direct runs on `specs/0036-scoped-lifetime-per-pipeline`; R5; R3):
    - (C-8) With the live `gh`:
      - `pr` = `{4282, url, head_sha 91d549be6…, head_present: true}`, `pr_count` 1, and `measured_head` = `{91d549be6…, pr_head}`.
      - `gh_commands` is exactly one entry: `gh pr list --head spec/scoped-lifetime-per-pipeline --state open --json number,url,headRefName,headRefOid,createdAt`.
    - Stand-in `gh` returning #4200 and #4282 (#4282's head is `91d549be6`): `pr.number` 4282, `pr_count` 2 (AC-46).
    - Stand-in returning #4282 with a well-formed head sha for which `git cat-file -e {sha}^{commit}` fails: `head_present` false, `measured_head.source` `branch_tip`, and no fetch was issued (AC-84).
    - Stand-in returning #4282 with `headRefOid` `386efee7880e32428a96a96ede6d44a21541e48c` (`91d549be6~1`, present locally but not the branch tip): `head_present` true, `measured_head` = `{386efee78…, pr_head}`, and `merge_base` is computed against that head. This is FR-10's PR-head-differs case, whose line T11.3 checks.
    - Stand-in returning only a PR whose `headRefName` differs: `pr_count` 0, with reason `no PR found for branch spec/scoped-lifetime-per-pipeline` (row 1).
    - `gh` removed from `PATH`, a stand-in that exits 1, and a stand-in that sleeps 60 s: each run exits `0`, `pr` is null with `gh unavailable`, `pr_count` is null, the measured head is the branch tip, and the sleeping case ends within about 30 s plus the compile time (row 2, NFR-4, AC-28 ledger half).
  - Implementation should:
    - Strip any remote prefix from the branch name, and keep only exact `headRefName` matches. When several remain, the highest number wins.
    - Check that the head is present with `git cat-file -e`. Kill `gh` after 30 s. Record every `gh` command line in order.
    - Never fetch. Compute the merge base against the measured head.
  - Traces to: FR-20, FR-18, FR-16 rows 1, 2 and 16, NFR-4, AC-18, AC-28, AC-30 (ledger record), AC-46, AC-73, AC-84; ADR 0072 KC1 (children, `gh` outcomes), KC2 (`pr`, `pr_count`, `measured_head`, `gh_commands`), IA 7.

## Phase 7 — Write safety

*ADR 0072 IA 8, "behavioural, partly by inspection". Depends on Phase 6. This task takes the Verify-by shape (see *How to read this list*).*

- [ ] **TEST + IMPLEMENT: T7.1 — The ledger appears only by an atomic replacement, an over-cap ledger is refused before anything is written, and no run leaves a temporary or partial artefact**
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Verify by:
    - **Inspection of the write path (AC-83; ADR 0072 IA 8).** Read the code in `show_me_facts.cs` that writes the ledger, and confirm each of these by line:
      - the ledger is serialised in memory;
      - a serialised size above 65,536 B exits `1` before any file is created;
      - the bytes are written to `.show-me-ledger.json.tmp` in the same directory;
      - `File.Move(…, overwrite: true)` is the only operation that touches `.show-me-ledger.json`;
      - the temporary file is deleted in `finally`;
      - no `Append`, `Truncate`, in-place open or `File.Write*` call names the ledger path.
      - Record the inspection, naming each line, in the commit message.
    - **Residue row (a regression guard).** Add one test-script row, run after all other rows. It covers every fixture directory, the calibration directory and the `specs/x` probe path, and asserts that no `.show-me-ledger.json.tmp` and no untracked file remains, apart from ledgers the harness restored.
      - This row **cannot be observed red**. T3.1's plain write creates no temporary file, and provoking a partial write would need the fault hook NFR-9 rules out.
      - It is written, seen green, and kept to guard against a later change that leaves residue.
    - `dotnet run .claude/commands/spec/show_me_facts_tests.cs` exits `0`, with every earlier row still green after the write path is replaced.
  - Implementation should: replace T3.1's plain write. Serialise in memory. When the size exceeds 65,536 B, exit `1` before creating any file. Otherwise write to `.tmp`, then `File.Move(…, overwrite: true)`, and delete the temporary file in `finally`. No `Append`/`Truncate` or in-place write touches the ledger path.
  - Traces to: FR-21 (atomic write, cap), FR-4, NFR-8, NFR-9 (*What it does not test*, residue), AC-83; ADR 0072 *The write is atomic*, Risks (killed script leaves its temporary file), IA 8.

## Phase 8 — Word-count mode

*ADR 0072 IA 9. Depends on T3.2.*

- [ ] **TEST + IMPLEMENT: T8.1 — Word-count mode counts only NFR-2's counted body and excludes every fenced line**
  - **USE COMMAND**: `/test-first when the measurement script word-counts a file it should count only NFR-2's counted body and report 8 for the eight-token fixture`
  - Test script: `.claude/commands/spec/show_me_facts_tests.cs`
  - Test row(s): `-- .claude/test-fixtures/show-me/wordcount-eight.md --word-count`, and a missing file with `--word-count`.
  - Test should verify:
    - Exit `0`. The last `show-me-wordcount: ` line reports total **8**, and the excluded fenced-line count equal to the fixture's fence lines including fences. The in-range flag is T8.2's.
    - **Fence regression**: the row states that the total must be 8, not 16.
    - No file is created and no ledger is touched in the fixture directory.
    - The missing file exits with a status other than `0` and emits no record.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Apply NFR-2's rule: exclude the H1 and metadata above the first H2, lines beginning with `|` after trimming, everything from `## Inputs used` onwards, and fences inclusive. Count only tokens containing an alphanumeric character.
    - Emit a single-line JSON record with the fixed field names `total` and `excluded_fence_lines`, which T14.7 reads. T8.2 adds `in_range`.
  - Traces to: NFR-2, FR-21 (*Modes*), NFR-9 (regression 2), AC-80, AC-59 (mechanism); ADR 0072 KC1 stderr records, IA 9.

- [ ] **TEST + IMPLEMENT: T8.2 — Word-count mode reports whether the total is inside 400–2,000, with inclusive bounds**
  - **USE COMMAND**: `/test-first when the measurement script word-counts a file it should report in_range true for the conforming fixture and false for the eight-token fixture`
  - Test script: `.claude/commands/spec/show_me_facts_tests.cs`
  - Test row(s): `-- .claude/test-fixtures/show-me/wordcount-conforming.md --word-count`, and T8.1's eight-token row extended with an `in_range` assertion.
  - Test should verify:
    - Conforming: exit `0`; total equals the fixture's recorded count; `in_range` `true`.
    - Eight-token: `in_range` `false`.
    - Both rows are red until the record carries `in_range`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: compute `in_range` as 400 ≤ `total` ≤ 2,000 and add it to the record, and nothing else.
  - Traces to: NFR-2, FR-21 (*Modes*), AC-80; ADR 0072 IA 9.

## Phase 9 — Command file: resolution, gate and stops

*ADR 0072 IA 10, Steps 1–3. Depends on Phases 1–8, committed. This phase **rewrites** `.claude/commands/spec/show-me.md`.*

- [ ] **TEST + IMPLEMENT: T9.1 — `/spec:show-me` resolves its whole argument over the pre-listed spec directories, or stops with FR-1's or FR-2's exact message**
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Verify by:
    - Run:
      - `/spec:show-me 0002`, `/spec:show-me kafka-widget`, `/spec:show-me README.md`.
      - `/spec:show-me` with `specs/.current-spec` in four states: deleted; empty; whitespace only; naming `9999-gone`.
      - (C-8) `/spec:show-me 0036`.
    - Fixture: the real directories. `specs/.current-spec` is restored under R2.
    - Check:
      - AC-1a (first half): all three `0002-*` names are listed, with a request for the full name.
      - AC-3: no spec matches, and the output points at `/spec:status`.
      - AC-35 (second half): `README.md` matches nothing.
      - AC-5: the message names `missing`, `empty` or `stale: names '9999-gone'` as the case is, and offers both remedies.
      - AC-1 (C-8): both 0036 directories are named.
      - Every run: no `show-me.md` and no ledger is created, and `git status --porcelain` is unchanged.
      - The successful resolutions (AC-1a `sqs-cleanup`, AC-2, AC-4, AC-35's unquoted name) are checked in T9.2.
  - Implementation should:
    - Replace the whole pre-rescope body; git history keeps it.
    - Front matter: `allowed-tools` exactly as ADR 0072 *Technology Choices* lists them (`ls`, `cat`, `date`, `head`, `tail`, `test`, `wc`, `grep`, `git diff`, `git log`, `git ls-files`, the script entry, `Read`, `Write`), with no `gh`, no `git merge-base` and no interpreter. Also `description`, and `argument-hint: [spec-id]`.
    - Use `$ARGUMENTS`, trimmed and unquoted as a whole, with a pre-executed `ls -1d specs/*/` listing and FR-1's three rules applied in the model.
    - Print FR-1's and FR-2's messages verbatim. Run in the main agent, with no sub-agent.
  - Traces to: FR-1, FR-2, FR-18 (front matter), NFR-6, C-1, AC-1, AC-1a, AC-3, AC-5, AC-35; ADR 0072 KC4, *Why there is no sub-agent*, *allowed-tools* paragraph, Risks (argument split), IA 10.

- [ ] **TEST + IMPLEMENT: T9.2 — Step 2 probes the script, invokes it with the target only, and turns each exit status into exactly one outcome: continue, FR-3's stop, or FR-21's tooling fault**
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Verify by (R2, R3; `git status` compared per R1 for every stop):
    - Successful resolution:
      - `/spec:show-me sqs-cleanup` invokes `-- specs/0002-sqs-cleanup` (AC-1a).
      - `/spec:show-me 0021-Expose Unacceptable Message Window`, unquoted, invokes `-- specs/"0021-Expose Unacceptable Message Window"` with no permission prompt (AC-35).
      - (C-8) `/spec:show-me scoped-lifetime` (AC-2), and a bare `/spec:show-me` with `.current-spec` = `0036-scoped-lifetime-per-pipeline` (AC-4), both target `specs/0036-scoped-lifetime-per-pipeline`.
    - FR-3's stops:
      - `0005-defer-message-on-error` gives the `tasks.md`-absent message (AC-8).
      - `0023-asyncapi-document-generation` gives the zero-checkbox message.
      - `0003-remove_clear_event_bus_calls` gives `2 of 10` and both titles.
      - The same `0003` run with a planted `show-me.md` and ledger leaves both byte-identical (AC-7, AC-71). Without a planted ledger, none exists afterwards (AC-71).
      - AC-6: uncheck two boxes of 0036's `tasks.md` in the working tree. The message reads `2 of 82` and lists both titles. Restore the file.
    - AC-72, states 1–4:
      - absent: move the script to the scratch directory;
      - unreadable: `chmod 000`;
      - `exited {code}`: temporarily edit the script to return 3;
      - `gate facts were not parseable`: temporarily edit the gate record to malformed JSON, with target `0003`.
      - Each gives FR-21's exact message naming its state, no `show-me.md`, and no ledger created, while a planted ledger is unchanged. Restore with `mv`, `chmod 644` and `git checkout`.
    - AC-93 (first half): inspection shows the measuring invocation passes only `-- specs/{dir}`, and never `--pinned` or `--release-notes`.
  - Implementation should:
    - Run `test -f` and `test -r` on the script path before invoking it. The invocation string must match the allow entry, with spaced names quoted after `specs/`.
    - On `2`, read the last `show-me-gate: ` line and print FR-3's message. On any other status other than `0`, print FR-21's message.
    - Never compute a value inline and never write a partial file.
  - Traces to: FR-3, FR-19 (stop prints only the message), FR-21 (failure mode), NFR-8, AC-1a, AC-2, AC-4, AC-6, AC-7, AC-8, AC-35, AC-71, AC-72, AC-93; ADR 0072 KC4, allow-list paragraph (spaced names), IA 10.

- [ ] **TEST + IMPLEMENT: T9.3 — Step 3 reads the ledger in sized windows, stops on anything but one JSON object, and afterwards copies every counted value by field name**
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Verify by:
    - AC-72, fifth state: temporarily edit the script to write `{} {}` and exit `0`. Running `/spec:show-me 0033-pg-advisory-lock-sha256` prints `ledger was not a single JSON object`. No `show-me.md` is written, the replaced ledger stays in place, and `git status` is identical. Delete the ledger and `git checkout` the script afterwards.
    - AC-70: in a `0033` run, the transcript shows the ledger priced with `wc -c` and read by `tail`/`head` windows, and nothing is parsed from the script's stdout. Search the command file for counting pipelines (`wc -l`, `grep -c`, `awk`, `sort | uniq`, `--numstat` arithmetic): there are none. Every counted value names its ledger field.
  - Implementation should: read the ledger as unplanned windows of at most 25,000 B each; parse it once; map fields to sections through one table in the command file; and turn `null_reasons` into FR-16 rows.
  - Traces to: FR-21, NFR-1, NFR-8, AC-70, AC-72; ADR 0072 *The seam runs between counting and reading*, KC3 (unplanned windows), KC4 (fifth state), Risks (model computes a count), IA 10.

## Phase 10 — Command file: evidence reads and the read log

*ADR 0072 IA 10, Step 4. Depends on Phase 9.*

- [ ] **TEST + IMPLEMENT: T10.1 — Every read is priced before it is issued, made as a window of at most 25,000 B, charged to one in-context read log, and kept inside 1,048,576 B, with the last 100,000 B reserved**
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Verify by:
    - `0033` run, with a planted `show-me.md`:
      - `test -f` then `Read` is used once for that file, priced at `wc -c` + 8 × (`wc -l` + 1) and not used as evidence.
      - Every other read is preceded by `wc -c` on the same path or priced from ledger `bytes`, and made with `tail | head`.
      - The read log's running total is ≤ 1,048,576 (AC-76).
    - Clone K8 (AC-52): `tasks.md` is read in full through all its windows (229,159 B). 15 targeted ADR extracts are charged. The total is within budget, and no value is zero or omitted because of a skip.
    - (C-8) `0036` run:
      - Every `git diff` issued names the ledger's two shas plus a pathspec or summary flag, and no `gh pr diff` is issued. The `src/`-scoped diff is read with `src_diff.command` (AC-77).
      - `tasks.md` is read in full. ADR extracts are charged. `requirements.md` is read by declaration windows. At least 100,000 B remain when source reads would begin (AC-78, reads half).
  - Implementation should:
    - Apply KC3's window rules 1–4: planned windows from ledger lists; unplanned windows sized by piping into `wc -c` starting at 200 lines and halving; `oversize` lines not read; truncated output treated as not read but charged.
    - Keep the read log with path, method and bytes. Reserve 100,000 B for the Explainer. Never read `PROMPT*.md`.
  - Traces to: NFR-3, FR-6 (f) (reserve size), FR-17, AC-52, AC-76, AC-77, AC-78; ADR 0072 KC3 *How a read is made*, *The read log*, *The full diff is never read*, IA 10.

- [ ] **TEST + IMPLEMENT: T10.2 — Step 4 reads its inputs in KC3's order, always reads a marked release-notes section when every such section fits, and records a present-but-unread section as row 5a**
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Verify by:
    - AC-69, with a temporary hand-marked section (R2):
      - Add under `## Master` in `release_notes.md` a section `### Probe (spec 0033)` followed by `<!-- spec: 0033-pg-advisory-lock-sha256 -->` and a `#### Breaking changes` list of 1 bullet.
      - Run `/spec:show-me 0033-pg-advisory-lock-sha256`. The ledger shows `count` 1 and `m` 1. The transcript shows `wc -c release_notes.md`, then the section's windows read and charged.
      - Restore with `git checkout -- release_notes.md`.
    - AC-68 in K7 (clone): the read log shows `tasks.md` consuming the general allowance and the marked section skipped because its bytes exceed the remainder. The run records row 5a.
    - K2 variant with `.adr-list` deleted: commit subjects are read with `git log --format='%h %s' {merge base}..{measured head}`. On `0033` with `.adr-list` emptied (no diff), no `git log` is issued.
    - (C-8) On `0036`, `requirements.md` paragraphs are read in declaration order. Any paragraph not afforded is noted as the reason for `Unverifiable`.
  - Implementation should:
    - Follow KC3's read table in order: the existing `show-me.md`; `.issue-number`; `.adr-list`; `tasks.md`; the `src/` diff; `--name-only` only when there is no `src/` change; ADR extracts; marked sections (all or none); commit subjects only under row 6 with a diff; `requirements.md` whole, or else by declarations.
    - When a read needed for FR-7 or FR-8 evidence fails, give `Unverifiable` or `not available` with a reason, never zero.
  - Traces to: FR-7 (read obligation, all-or-none), FR-16 rows 5, 5a and 6, NFR-3 (degradation), AC-68, AC-69; ADR 0072 KC3 table, *Two reads are obligations, not options*, IA 10.

## Phase 11 — Command file: output skeleton and the one Write

*ADR 0072 IA 10, the Synthesiser's ledger-copied part and Step 6. Depends on Phase 10. Tasks run in order. From here on, every Verify block inspects a written `show-me.md`. Judged sections are filled in by Phases 12–14.*

- [ ] **TEST + IMPLEMENT: T11.1 — The command checks every path it names is tracked and writes `show-me.md` in one `Write`, with FR-5's header and the eight headings in order**
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Verify by:
    - `0033` (no branch):
      - AC-9: the file is created, and `git status` gains only `show-me.md`.
      - Re-run for AC-10: the file is replaced, with no `show-me-2.md` or `.bak`.
      - AC-19, metadata part: the three metadata lines read `undetermined`; the base ref is named; the PR line reads `none found`.
    - `0002-sqs-cleanup`: the metadata issue line reads `none` (AC-44).
    - AC-74: `git check-ignore -v` names the ledger, `git ls-files --error-unmatch` on it fails, and a second run replaces it.
    - Clone K2: AC-11's headings are verbatim and in order, with the full metadata block.
  - Implementation should:
    - Write the H1 and metadata block: `date +%F`, `.issue-number`, the full ref, short shas, and the PR.
    - Write the eight H2 headings verbatim.
    - Run `git ls-files --error-unmatch` on every path before writing it. Use `test -f` to know whether the file is created or replaced, `Read` before `Write` when it exists, and one `Write`. Never stage anything.
  - Traces to: FR-4, FR-5, FR-16 rows 1, 10 and 15, FR-17, NFR-5, NFR-8, AC-9, AC-10, AC-11, AC-19, AC-44, AC-74; ADR 0072 KC5, IA 10 (Step 6).

- [ ] **TEST + IMPLEMENT: T11.2 — How it was built copies the five tag counts and the commit count from the ledger, or gives FR-9's fallback line, and nothing else**
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Verify by:
    - `0033`: `Commits: not determinable — spec branch not resolved.` appears, and the task shape is still reported (AC-19, How it was built part).
    - `0002-sqs-cleanup`: the five tag counts equal the ledger's `by_tag` values and sum to 6.
    - (C-8) `0036`: AC-17 — 82 tasks with the 62/12/2/6/0 split and 363 commits, and no review or CI content.
  - Implementation should: write How it was built as the five tag counts plus the commit count, or the fallback line when the diff fields are null. Carry no review history and no CI state.
  - Traces to: FR-9, FR-16 row 12, NFR-1, AC-17, AC-19; ADR 0072 KC5, IA 10.

- [ ] **TEST + IMPLEMENT: T11.3 — Blast radius copies the six buckets, the API and subdirectory lines, FR-10's provenance lines and FR-20's PR lines wholly from the ledger**
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Verify by:
    - `0033`: Blast radius shows `Spec branch not determinable — no diff measured.` plus the rules tried (AC-19, Blast radius part).
    - Clone (R4; clone figures are not calibration values):
      - K4: AC-67 reports 5 files and 1 subdirectory, and a second run gives the identical pair.
      - K3: AC-66 reports 4.
      - K12: AC-85's `already merged` line.
    - (C-8) `0036`:
      - AC-18: six buckets summing to 517, 6 subdirectories, `Measured from git diff … (PR #4282 head)`, and the `Ref used:` line.
      - With R5: the AC-46 PR-count line, and the AC-84 row-16 line naming both shas.
      - With R5 returning #4282 with `headRefOid` `386efee7880e32428a96a96ede6d44a21541e48c` (T6.2's case): the line `Spec branch tip 91d549be6… differs from PR #4282 head 386efee78…; measured the PR head.` appears, and `Measured from …` names `PR #4282 head`.
  - Implementation should: write Blast radius as
    - a pipe table of all six buckets plus a totals row;
    - the API and subdirectory prose lines;
    - FR-10's provenance lines (measured-from, PR head differs, ref used, local divergence);
    - FR-20's PR-count line;
    - the row 12 and row 16 lines.
  - Traces to: FR-10, FR-16 rows 12 and 16, FR-20, NFR-1, AC-18, AC-19, AC-46, AC-66, AC-67, AC-84, AC-85; ADR 0072 KC5, IA 10.

## Phase 12 — Command file: the Explainer

*ADR 0077 IA 1–2, inside ADR 0072 IA 10's Step 5. Depends on Phase 11.*

- [ ] **TEST + IMPLEMENT: T12.1 — The command states the six-row ladder, quotes the five named lines by name, and gives the node-list row shape, so an undiagrammable run carries exactly one named line**
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Verify by:
    - `0033`: What changed and why has no fenced block and carries exactly `No diagram: spec branch not determinable, so no change could be drawn.`, and the triggers are not evaluated (AC-61).
    - K1 (clone), when the Explainer does not raise: the no-trigger line names `2`, `1` directory, `0` and `1` (AC-57, first half). When it raises, record the first half `unexercised on this run` in the task's commit message and check the second half in T12.2.
    - Inspection: the five lines are quoted verbatim outside any table and referenced by name, never by ordinal.
  - Implementation should: add the ladder as ADR 0077's *mechanism* table; the lines with the no-trigger values copied from the ledger (`src/` files, `src_subdirectory_count`, `public_api_lines`, `adr_resolved_count`); and the node-list row with one licensing source.
  - Traces to: FR-6 (a), (b), (e), FR-16 row 12, NFR-1, AC-57, AC-61; ADR 0077 *mechanism*, KC3, IA 1.

- [ ] **TEST + IMPLEMENT: T12.2 — The Explainer probes, reads or extracts, or abandons; charges every read; and renders only nodes it has a licensing source for**
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Verify by:
    - (C-8) `0036`:
      - The transcript's node list gives one source for every node. Participants pass `git ls-files` and are probed before they are read. Reads are charged, and at least 100,000 B remain at the start (AC-78).
      - What changed and why carries exactly one block: ≤ 40 lines, ≤ 100 columns, Mermaid for a flow and a plain block for a hierarchy, and no `No diagram:` line (AC-56).
      - If a second diagram is drawn, both spend from one reserve (AC-63).
    - K5 (clone): when a stand-down is taken, there is exactly one stand-down line naming D2 with a one-sentence reason, and no invented relationship (AC-64).
    - K7 (clone): the budget line, no fenced block, and no unread type named (AC-58).
    - K1: when the Explainer raises, one diagram plus a one-sentence reason and no `No diagram:` line (AC-57, second half).
    - Record which ladder row each run took, since stand-down and raise are judgements. A judged path that was not taken is recorded as `unexercised on this run` in the task's commit message, not as passed (Phase 17's *Judged paths* rule).
  - Implementation should:
    - Read the trigger fields and never re-evaluate them.
    - Spend in order: probe the known set, probe each file found while reading, then read whole, extract with a sized `grep -n -F` for literal member names, or abandon.
    - Keep the node list. Render from it, following the Mermaid trap list.
    - Hand over the block or line with its target section and, for `## Where to look first`, three to seven changed paths.
  - Traces to: FR-6 (b), (c), (f), FR-14 (optional tree), NFR-3 (reserve), NFR-7, AC-56, AC-57, AC-58, AC-63, AC-64, AC-78; ADR 0077 KC1, KC2, KC3, KC4, KC5, IA 2.

## Phase 13 — Command file: Classifier and the risk step

*ADR 0073 IA 1–5, inside ADR 0072 IA 10's Step 5. Depends on Phase 12.*

- [ ] **TEST + IMPLEMENT: T13.1 — The FR-13 invariant check finds no paragraph outside the risk-step markers that joins a conditional keyword to a level name, and requires exactly one marker pair**
  - **USE COMMAND**: `/test-first when the show-me test script checks the command file it should report no paragraph outside the risk-step markers containing both a whole-word conditional and a capitalised level name`
  - Test script: `.claude/commands/spec/show_me_facts_tests.cs`
  - Test row(s): the FR-13 row over `.claude/commands/spec/show-me.md`; the AC-81 literal line held in the test script; one positive self-check paragraph held in the test script.
  - Test should verify:
    - The marked region is removed with its markers, and the rest is split into blank-line paragraphs. No paragraph contains both a whole-word `if`/`when`/`unless`/`else`/`otherwise` (any case) and a whole-word `Low`/`Medium`/`High` (capitalised).
    - There is exactly one begin marker and one end marker, begin first. The row is red now, because there are no markers.
    - The literal line containing `git diff` and `gh pr diff` gives zero matches (AC-81).
    - The self-check paragraph `If the level is High, stop.` is reported, which proves the check can fire.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: add the check to the test script and nothing to the measurement script.
  - Traces to: FR-13, NFR-9 (invariant), AC-81; ADR 0073 KC5, *Where each artefact is touched*, IA 1; ADR 0072 KC6.

- [ ] **TEST + IMPLEMENT: T13.2 — The risk step is fenced by its two marker lines at Step 5, between the Classifier and the Synthesiser**
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Verify by: `dotnet run .claude/commands/spec/show_me_facts_tests.cs` exits `0` with the FR-13 row green. `/spec:show-me 0033-pg-advisory-lock-sha256` still writes the file.
  - Implementation should:
    - Add Step 5's stage headings in the order Explainer, Classifier (filled by T13.5), risk step, Synthesiser.
    - Wrap the risk step in `<!-- show-me:risk-step:begin -->` and `<!-- show-me:risk-step:end -->`, and move any text that tests a level inside them.
  - Traces to: FR-13, *Advisory* (Definitions), AC-81; ADR 0073 KC5, IA 2; ADR 0072 stage table.

- [ ] **TEST + IMPLEMENT: T13.3 — Inside the markers: F1 is copied from the ledger, the forced levels apply first, and F2 and F5 take their highest matching column over all their evidence**
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Verify by:
    - `0033`: the F1 row reads `no diff measured — spec branch not determinable` at `Medium`.
    - `0002-sqs-cleanup`: F5 `0 declared ids in the bold lead-in form` at `Medium` (AC-43, F5 half).
    - `0033` with `requirements.md` moved aside (R2): F5 is `Medium` with the row-8 cell (AC-16, F5 half).
    - The table has exactly the rows F1, F2 and F5, with no `F3` or `F4` row. The FR-13 row stays green.
    - The F2 and F5 mapping on judged evidence is verified in T13.5.
  - Implementation should: copy `f1_level`, or use row 12's `Medium` when it is null; state the forced-level table; apply the four-step mapping procedure; and give each factor-table row a measured value and a level.
  - Traces to: FR-11, FR-16 rows 8, 9 and 12, NFR-1, AC-16, AC-20 (shape), AC-43; ADR 0073 KC2, KC3, IA 3.

- [ ] **TEST + IMPLEMENT: T13.4 — Inside the markers: the overall level is the maximum, a raise needs its sentence, and the `**Overall risk: …**` line and FR-13's sentence are written, then checked**
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Verify by:
    - `0033` produces a `**Overall risk: {level}**` line on its own line, and FR-13's sentence verbatim. Until the Classifier exists (T13.5), F2 and F5 have no judged inputs, so this task asserts only F1 = `Medium` and that the stated level is not below the maximum of the three rows as written. T13.5 checks `0033`'s level against its three rows.
    - The transcript shows the two self-checks run: the stated level is not below the maximum, and a raise has its sentence (AC-23).
    - The FR-13 row stays green.
  - Implementation should: compute the maximum over Low < Medium < High; allow a raise only with a first rationale sentence naming what the factors miss; write FR-13's sentence as a literal; and hand the list of factors at the maximum to the Synthesiser.
  - Traces to: FR-12, FR-13, AC-23, AC-24 (sentence); ADR 0073 KC4, IA 4.

- [ ] **TEST + IMPLEMENT: T13.5 — The Classifier judges each breaking-change item, requirement status and uncovered piece of work once, with evidence, and tallies only its own judgements**
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Verify by (clone, then (C-8)):
    - K2: `No deviations`; F1, F2 and F5 all `Low` (AC-21, AC-55).
    - K3: F2 `Medium`, F1 `Low`, F5 `Low`, and `**Overall risk: Medium**` (AC-22).
    - K9: 17 ids with statuses (AC-15).
    - K10: `FR-27` is `Withdrawn`, citing the ADR and its superseding requirement, and F5 is `Medium` (AC-45).
    - K11: 26 `Shipped`, one `Shipped with deviation`, one `Deferred` with an issue number (AC-54).
    - `0033`: F2 and F5 each cite their evidence, and `**Overall risk: …**` states the maximum of the three rows. F1 is forced to `Medium` (row 12), so the level is `Medium` unless an F2 or F5 row reads `High` and cites the evidence for it.
    - (C-8) `0036`: F1 `High` citing 76, F2 `High` citing an item tally of at least 4 (AC-20).
    - Every item and every status names its evidence, and statuses are assigned only to ledger ids.
  - Implementation should:
    - Read only what the read log holds.
    - Follow the catalogue's bullet boundaries when a marked section with `m` > 0 was read, and the no-catalogue rule otherwise.
    - Emit `{n}`, the per-status tallies and Part 3's list with task ids.
  - Traces to: FR-7, FR-8, FR-11, NFR-1, NFR-7, AC-15, AC-20, AC-21, AC-22, AC-45, AC-54, AC-55; ADR 0073 KC1, IA 5 (Classifier part).

## Phase 14 — Command file: judged sections, pre-Write checks, word count and report

*Covers the rest of ADR 0072 IA 10, ADR 0077 IA 3–5, and ADR 0073 IA 5's rationale and FR-19 part. Depends on Phase 13.*

- [ ] **TEST + IMPLEMENT: T14.1 — What changed and why names every ADR by stem, title, Status and relative link, and places the Explainer's block or line; Breaking changes renders the Classifier's items with a count line and the defined absence lines**
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Verify by:
    - (C-8) `0036`:
      - AC-12: 150–600 prose words, fences excluded. All seven ADRs appear by stem, title and Status. Links resolve. There is no bare `ADR 0070`.
      - AC-51: the duplicated 0070, 0071, 0072 and 0073 numbers resolve to the files `.adr-list` named.
      - AC-13: items are ≤ 40 words, each with a classification set and a migration, followed by `Total breaking-change items: {n}`.
      - AC-14: row 5's line appears, and `release_notes.md` is unchanged.
    - `0033` (no diff): AC-37's row 14 line, the count line, and F2 computed from the listed items.
    - K2 with `.adr-list` deleted: AC-40's `No ADRs recorded for this spec.`; the ledger's `triggers.d3` is `false`, and `triggers.d1` and `triggers.d2` are evaluated (non-null). On K2 both are `false` (4 files, 0 public-API lines). F1 is unchanged, and no factor row cites the absence.
    - `0033` with `.adr-list` temporarily extended (R2) by a missing filename and the bare number `0037`:
      - Run the script directly: the ledger shows `ADR file not found`, `ambiguous ADR number` with 5 matches, and `adr_resolved_count` 1.
      - Run `/spec:show-me 0033-pg-advisory-lock-sha256`. What changed and why names both entries with row 7's texts, and the remaining ADR is still named and linked.
      - `## Inputs used` marks both unresolved entries `not available`.
      - Neither counts toward D3: `adr_resolved_count` is 1, not 3. (No diff is measured on `0033`, so `triggers` is null.)
      - No risk factor changes: F1 is unchanged, and no F2 or F5 row cites either unresolved entry as evidence (AC-42). F2 and F5 are judged, so they are not compared across runs (NFR-1).
      - Restore `.adr-list` with `git checkout` (R2).
    - T10.2's temporary one-bullet marked section, when `{n}` ≠ 1: the disagreement line `release_notes.md records 1 items; this summary identifies {n}`.
  - Implementation should:
    - What changed and why: glossed type names; the row 6 and row 7 texts; and placement of the Explainer's output for this section (ADR 0077 IA 3, first part).
    - Breaking changes: rows 5, 5a and 14 lines; the none line; and the disagreement line only when `m` is non-null and differs from `{n}`.
  - Traces to: FR-6 (narrative), FR-7, FR-16 rows 5, 5a, 6, 7 and 14, C-9, AC-12, AC-13, AC-14, AC-37, AC-40, AC-42, AC-51; ADR 0072 KC5; ADR 0077 IA 3.

- [ ] **TEST + IMPLEMENT: T14.2 — Did it ship what it said? renders the Classifier's statuses as one collapsed shipped-as-planned line, one entry per deviation, Part 3 and a count line, keeping the partition invariant**
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Verify by:
    - K9: AC-15 — total 17; every id appears exactly once across Parts 1 and 2; the count terms sum to 17.
    - K10: AC-45 — one `FR-27` entry naming `FR-27.3`, and a `Withdrawn:` term.
    - K11: AC-54's exact Part 1 and count lines. The K11 variant gives AC-55's `No deviations: …`.
    - `0033` with `requirements.md` moved aside: AC-16's exact line.
    - `0002-sqs-cleanup`: AC-43's exact line, with no shipped-as-planned line and no count line.
    - Expanding each Part 1 id list back to ids gives exactly the `Shipped` ids and never an undeclared one.
  - Implementation should:
    - Apply FR-8's two collapse rules: integer adjacency, and ranges only for runs of three or more.
    - Add the follow-up for `Deferred`, `Dropped` and `Withdrawn`, give Part 3 a cap of 5 entries, and write rows 8 and 9 verbatim.
  - Traces to: FR-8, FR-16 rows 8–9, AC-15, AC-16, AC-43, AC-45, AC-54, AC-55; ADR 0072 KC5; ADR 0073 KC1.

- [ ] **TEST + IMPLEMENT: T14.3 — Where to look first lists three to seven spec-diff paths with reasons, or every path when its source holds fewer than three, taking them from the Explainer's handover when it drew a tree there, or gives row 13's line when there is no diff**
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Verify by:
    - (C-8) `0036`: AC-26 — 3–7 paths, all in the diff, with reasons of ≤ 25 words. AC-60 when a tree is present — `(unchanged)` marks, no path slot used, and no `No diagram:` line.
    - K1 (clone): AC-26 — the list holds exactly K1's 2 `src/` paths, each with a reason of ≤ 25 words. It holds no spec-directory path, because the spec diff touches `src/` and `--name-only` is therefore not the source. The 2-path list is complete under FR-14, not a shortfall, and the section carries no tree.
    - `0033`: AC-36's exact row 13 line, with no paths and no diagram.
    - K6: when ladder row 5 was elected, AC-65 — a tree here and the placed-elsewhere line in What changed and why. Record which row was elected.
  - Implementation should:
    - Take paths from the `src/` diff read, or from `--name-only` when there is no `src/` change.
    - When that source holds fewer than three paths, list every one of them, each with its reason, and draw no tree (FR-14).
    - Use the Explainer's handed-over paths when it drew a diagram for this section (ADR 0077 IA 3, second part). The Explainer elects a tree only over three to seven changed paths.
    - Write row 13 verbatim.
  - Traces to: FR-14 (including the fewer-than-three rule), FR-6 (e) placed-elsewhere line, FR-16 row 13, AC-26, AC-36, AC-60, AC-65; ADR 0077 KC5, IA 3; ADR 0072 KC5.

- [ ] **TEST + IMPLEMENT: T14.4 — Inputs used gives exactly FR-15's rows plus one per Explainer source file, each marked from the read log, and never a row for the ledger, the script, review or CI**
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Verify by:
    - K2 in the clone, which resolves `spec/low-risk` so a diff is measured, and has no marked section, run with an R5 stand-in that answers `pr list …` with `[]` and exits 0, so there is no PR (FR-16 row 1): AC-27 — the pull-request row reads `not available: no PR found for branch spec/low-risk`, the metadata block's PR reference reads `none found`, `release_notes.md` is `not available`, and `tasks.md`, `requirements.md`, `.adr-list` and git history are `used`. There is no review or CI row.
    - `0033`: the git history row reads `not available: spec branch not determinable` (ADR 0072 KC5), and the PR and release-notes rows are `not available` with reasons. AC-41 — no `PROMPT` string anywhere. AC-75 — no `.show-me-ledger.json`, no script path, and no rows for either.
    - `0002-sqs-cleanup`: `.issue-number` is `not available: not present` (AC-44).
    - K2 in the clone with no stand-in, where `gh` fails (R4): the `gh unavailable` row, with F1 unchanged (AC-28). This run is not used for AC-27.
    - T10.2's section gives the row `used` (AC-69). K7 gives `not available: read budget exhausted before release_notes.md section could be read` (AC-68).
    - (C-8) `0036`: Explainer rows are marked `used` or `used (targeted extraction)`.
  - Implementation should: produce the fixed row set, the git history row per KC5, one Explainer row per source file however it was read (ADR 0077 IA 4), and no row for `.current-spec`, the existing `show-me.md` or `PROMPT*`.
  - Traces to: FR-15, FR-16 rows 1, 2, 5, 5a, 10, 11 and 12, FR-17, AC-27, AC-28, AC-41, AC-44, AC-68, AC-69, AC-75; ADR 0072 KC5; ADR 0077 IA 4.

- [ ] **TEST + IMPLEMENT: T14.5 — The rationale has 2–5 sentences around the risk step's lines and names the factors at the maximum without comparing levels**
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Verify by: K3's rationale references F2 (AC-22). (C-8) `0036`'s names F1 and F2. On a raised run, the raising sentence comes first (AC-23). A raise is a judged path, so when no run raises, this clause is recorded `unexercised on this run` in the task's commit message (Phase 17). The FR-13 row stays green.
  - Implementation should: place the risk step's lines in the order ADR 0073 KC4 gives; state factor levels only as the table shows them; and put no conditional on a level outside the markers.
  - Traces to: FR-12, FR-13, AC-22, AC-23; ADR 0073 KC4, IA 5 (rationale part).

- [ ] **TEST + IMPLEMENT: T14.6 — Before the `Write`, the command checks its own diagrams and every path it names, so no over-cap block and no untracked path is ever written**
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Verify by:
    - `0033` with an untracked `PROMPT.md` and `PROMPT-history.md` added (R2): no occurrence of either anywhere, including diagrams, and every relative link resolves to a tracked path (AC-29).
    - (C-8) `0036`: at most two fenced blocks, each ≤ 40 lines and ≤ 100 characters; block-or-line exclusivity; every path-shaped label tracked (AC-59, caps part).
    - On every `show-me.md` this task writes (`0033` and `0036`):
      - NFR-5: `grep -F "$(git rev-parse --show-toplevel)"` and `grep -F "$HOME"` find nothing. `grep -E 'gh[pousr]_[A-Za-z0-9]{20,}|github_pat_'` finds nothing. Every path written is relative to the repository root.
      - NFR-6: the number of lines that open or close a fence (lines starting with three backticks after trimming) is even, and pairing them in order leaves no block open at the end of the file.
  - Implementation should: add the four checks from ADR 0077 KC6 over the assembled text, and the FR-17 test on every path, node and link. Write every path relative to the repository root.
  - Traces to: FR-6 (d), FR-17, NFR-5, NFR-6 (closed fences), AC-29, AC-59; ADR 0077 KC6, IA 5; ADR 0072 KC5.

- [ ] **TEST + IMPLEMENT: T14.7 — After the `Write`, the command word-counts the file once and reports the path, created or replaced, the copied overall level, the advisory reminder and the word count, whatever the level**
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Verify by:
    - `0033`: AC-31. For AC-33, the total equals a direct `--word-count` run on the same file.
    - (C-8) `0036` at `High`: AC-24 — the file is written, the report is complete, and there is no error, refusal, marker, label or comment.
    - AC-59: delete the fenced lines into a scratch copy. Word-counting it by hand gives the same total.
    - With the word-count mode temporarily edited to exit 3, the report reads `word count unavailable: measurement script exited 3` and the file stays. Restore with `git checkout`.
    - The FR-13 row is green.
  - Implementation should:
    - Invoke only `-- specs/{dir}/show-me.md --word-count` and read the last `show-me-wordcount: ` line.
    - Never revise the file and never invoke a third time.
    - Copy the level from the `**Overall risk:**` line without testing it (ADR 0073 IA 5, FR-19 part), and print only the stop message on a stop.
  - Traces to: FR-19, FR-13, FR-21 (*Modes*), NFR-2, AC-24, AC-31, AC-33, AC-59; ADR 0072 IA 10 Steps 7–8; ADR 0073 KC5, IA 5.

## Phase 15 — Spec-family forms

*ADR 0078 IA 1–5. Depends only on Phase 1, but it follows Phase 14 so that T15.5 can use a working `/spec:show-me`. The AC checks run each stated pattern by copying it from `requirements.md` § Definitions at verification time. No pattern is ever typed into a task or a command file.*

- [ ] **TEST + IMPLEMENT: T15.1 — `/spec:requirements` requires the bold lead-in declaration form, shown by concrete examples that match the declared-id pattern**
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Verify by:
    - `grep -nE '{declared-id pattern}' .claude/commands/spec/requirements.md` matches every concrete-number example (`**FR-3 — …**`, `- **NFR-1 — …**`, `**FR-27.3 — …**`). The placeholder `**FR-{n} — {title}.**` is exempt.
    - The heading and numbered-list forms are shown only with placeholders and are stated as not recognised by `/spec:show-me`.
    - A search for each pattern's distinctive text finds none.
    - `git diff $PRE` is additive only (AC-86).
  - Implementation should: add the form and its examples to the template and the quality bar, without removing or renumbering anything.
  - Traces to: FR-22, NFR-6, AC-86; ADR 0078 KC1, IA 1.

- [ ] **TEST + IMPLEMENT: T15.2 — `/spec:tasks` gives one template line per tag, requires the tag first with any id after the colon, and lists the drifted form only under *DO NOT***
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Verify by: running the task-type tag pattern over `.claude/commands/spec/tasks.md` matches every example checkbox line outside the *DO NOT Format Tasks Like This* block; the four tag lines are present; the drifted `T1.1 — STRUCTURAL` form appears only inside that block; no pattern text is present; `git diff $PRE` is additive (AC-87).
  - Implementation should: add the four template lines from ADR 0078 KC2 and the drifted form to the *DO NOT* block.
  - Traces to: FR-22, C-2, NFR-6, AC-87; ADR 0078 KC2, IA 2.

- [ ] **TEST + IMPLEMENT: T15.3 — `/spec:review` treats a non-lead-in requirement declaration and a non-tag-first task checkbox as findings**
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Verify by:
    - `git diff $PRE -- .claude/commands/spec/review.md` shows one added check under *Requirements Review Criteria* and one under *Tasks Review Criteria*, and no other criterion removed or reworded (AC-88).
    - In the clone, `/spec:review requirements` and `/spec:review tasks` on a scratch spec holding one `#### FR-1:` heading and one drifted task line each report the finding.
  - Implementation should: add the two checks, worded by example.
  - Traces to: FR-22, AC-88; ADR 0078 KC3, IA 3.

- [ ] **TEST + IMPLEMENT: T15.4 — `/spec:write_release_notes` resolves its target as `/spec:show-me` does and stops without writing on every ladder row from 1 to 7**
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Verify by (`shasum release_notes.md` and `git status` taken before and after each case; R2):
    - Real repository:
      - A bare invocation with no usable `.current-spec`, and `0002`: the messages name `/spec:write_release_notes` (AC-95, last part).
      - `0036-scoped-lifetime-per-pipeline` stops naming `### Scoped lifetime per pipeline (spec 0036, #4256)`, asks the user to delete the section or mark it, and says marking hands over the body (AC-95, first part).
      - `0036-generator-universal-rejection-tests` also stops, because of the shared id.
    - Clone:
      - `release_notes.md` deleted: the command stops and creates no file (AC-90).
      - A file with no `##`.
      - A target with no `.adr-list` and no `requirements.md`.
      - Two sections marked for `0033-pg-advisory-lock-sha256`.
      - A marked section under `## 10.7.0`.
      - In each case the message names the case and nothing is written (AC-95).
  - Implementation should:
    - Create the new file `.claude/commands/spec/write_release_notes.md`. Its front matter: `allowed-tools` of `Bash(ls:*)`, `Read`, `Grep`, `Glob` and `Edit`; `description`; `argument-hint: [spec-id]`.
    - Resolve by FR-1/FR-2, with no FR-3 check.
    - Find headings and fence lines with one `Grep` that returns line numbers, discarding headings inside fences. Find markers with a literal `<!-- spec: ` `Grep`, and match them exactly after trimming.
    - Evaluate ladder rows 1–7 in order.
  - Traces to: FR-23 (stops), FR-1, FR-2, NFR-6, AC-53 (front matter), AC-90, AC-95; ADR 0078 *mechanism* ladder, KC5, IA 4.

- [ ] **TEST + IMPLEMENT: T15.5 — `/spec:write_release_notes` writes or replaces exactly one marked section under the first `##`, in the fixed form, with one exact-match `Edit`**
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Verify by:
    - Clone, with K13 (`specs/9015-two-breaks/` and its staged synthetic ADR recording two breaks). Before the first run, save `release_notes.md` as `$SCRATCH/release_notes.pre-T15.5.md`. That copy holds K7's unstaged section and no section for `9015`. Every "restore" below copies it back over `release_notes.md`.
      - AC-89: one section directly under `## Master`; the marker on the next line; a summary; two bullets, each with an italic classification set and a migration. `diff` of the saved copy against the file afterwards shows one added hunk, and `git diff --cached --quiet -- release_notes.md` succeeds, so `release_notes.md` is not staged. (R4 stages the fixture directory and the synthetic ADR, and K7's section is an unstaged edit, so neither a whole-index check nor a plain `git diff` would isolate this run.)
      - Edit the ADR to one break and re-run for AC-90: one section, one bullet, all other bytes unchanged.
      - An ADR with no break gives `No breaking changes.`
    - AC-92 in the clone. Each given starts from a restore, so no section written by an earlier step is present:
      - Restore, then add a hand-written `### X (spec 9015)` section with no marker and no `#### Breaking changes`. The script run directly records `count` 0 and `m` null, and `/spec:show-me` reports row 5.
      - Restore, edit K13's ADR to three breaks, and run `/spec:write_release_notes`: `count` 1, `m` 3.
      - Restore, add the same hand-written section, then add the marker line `<!-- spec: 9015-two-breaks -->` by hand beneath its heading: `count` 1, `m` null. `/spec:show-me` reads it, gives no disagreement line, and groups items by the no-catalogue rule.
      - Finish with a restore, and `git checkout -- docs/adr/9015-two-breaks.md` to return the ADR to its staged content.
    - Real repository, with R2 restore:
      - Hand-mark 0036's section. Running the command replaces it in place, keeps the title `Scoped lifetime per pipeline`, and leaves no second section (AC-95).
      - Mark it instead for `0036-generator-universal-rejection-tests`. The command leaves it byte-identical and writes the target's own section (AC-95).
  - Implementation should:
    - Write the form in ADR 0078 KC4, keeping the existing title on replacement.
    - Take items from the Consequences sections and `requirements.md`. Resolve `.adr-list` entries by row 7's rule with `Glob`; an unresolved entry is named in the closing message, not treated as a stop.
    - Read at most 200 lines per call, halving the limit when refused.
    - Replace (row 8) or insert after the first `##` line (row 9) with one exact-match `Edit`. Never stage anything.
  - Traces to: FR-23 (form, replacement), FR-7 (the reader), AC-89, AC-90, AC-92, AC-95; ADR 0078 KC4, KC5, IA 4.

- [ ] **TEST + IMPLEMENT: T15.6 — `/spec:design` recommends `/spec:write_release_notes` when an ADR records a break, and `/spec:review`'s design criteria flag a break with no marked section**
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Verify by:
    - `git diff $POST_T15_3 -- .claude/commands/spec/design.md .claude/commands/spec/review.md` (R1) shows one added step and one added check, with nothing else removed or reworded (AC-91). `$PRE` is not used here, because T15.3 has already added two checks to `review.md`.
    - In the clone, `/spec:review design` on K13 (`9015-two-breaks`) with its section deleted reports the finding and recommends `/spec:write_release_notes`.
  - Implementation should: add the `/spec:design` step (tell the user and recommend the command, but do not run it) and the *Design (ADR) Review Criteria* check.
  - Traces to: FR-23, AC-91; ADR 0078 KC3, KC6, IA 5.

## Phase 16 — Documentation

*ADR 0072 IA 11 and ADR 0078 IA 6. Depends on Phases 14–15.*

- [ ] **DOC: T16.1 — Catalogue `/spec:show-me`, `/spec:write_release_notes` and both scripts in `.claude/commands/spec/README.md`**
  - Do:
    - Add command sections for both commands.
    - Add rows to the *Sub-agents & model policy* table (no sub-agent).
    - Document `show_me_facts.cs`: what it emits (the ledger, exit statuses and the two stderr records), how it is invoked, and that a user never invokes it directly.
    - Document `show_me_facts_tests.cs`: how to run it, what it covers, and that it is not part of CI.
  - Acceptance: the README clauses of AC-53 hold, links resolve, and the diff is additive.
  - Traces to: NFR-6, AC-53; ADR 0072 *Where each artefact is touched*, IA 11; ADR 0078 IA 6.

- [ ] **DOC: T16.2 — Ask for merge commits in `CONTRIBUTING.md`, and say why**
  - Do: add one paragraph under *Submitting Changes*. It asks that pull requests are merged with a merge commit, not squashed or rebased, because tooling pins commit shas from a merged branch's history and only a merge commit keeps them reachable.
  - Acceptance: AC-94 holds, and `git diff $PRE -- CONTRIBUTING.md` adds one paragraph only.
  - Traces to: NFR-9, AC-94; ADR 0072 KC6, Risks (calibration commits unreachable), IA 11.

## Phase 17 — Acceptance on the finished command

*Depends on Phases 1–16. T17.2 and T17.3 are the (C-8) criteria, and T17.5's AC-25 comparison and R5 stand-in reruns also need 0036 unmerged (FR-10 skips a merged candidate); all three must finish while #4282 is OPEN (R8). Each task re-runs, on the final command, the Verify-by set-ups named in earlier tasks.*

**The acceptance record.** Each T17.x task hands back, when its box is ticked, a list of the ACs it checked. Each AC is marked `held`, `defect ({owning task})` or `unexercised on this run`.

**Judged paths.** Some criteria apply only when the model takes a judged path. A run that does not take the path neither passes nor fails them. When the path was not taken, record the AC as `unexercised on this run`, never as `held`. Re-running to force a path is not required, and nothing is simulated. The table names the fixture that makes each path likely, where one exists.

| AC | Judged path | Fixture that makes it likely |
|---|---|---|
| AC-57 (first half) | the Explainer does not raise with no trigger | K1 |
| AC-57 (second half) | the Explainer raises with no trigger | none; K1 is built so that no trigger fires, and a raise there is a judgement |
| AC-59 | two diagrams totalling about 70 fenced lines | (C-8) `0036` (D1, D2 and D3 all fire) |
| AC-60 | a tree in `## Where to look first` | K6; (C-8) `0036` |
| AC-63 | one diagram in each of the two sections | (C-8) `0036` |
| AC-64 | the stand-down on D2 | K5 |
| AC-65 | ladder row 5 elected | K6 |
| AC-23 (raise clause) | a raise above the maximum | none |

- [ ] **PROJECT: T17.1 — Pre-flight: confirm the (C-8) window and the calibration preconditions**
  - Do:
    - Confirm `gh pr view 4282 --json state` returns `OPEN`.
    - Confirm `git diff --quiet 91d549be6 HEAD -- specs/0036-scoped-lifetime-per-pipeline/requirements.md specs/0036-scoped-lifetime-per-pipeline/tasks.md` succeeds, and that `git cat-file -e` succeeds for `6145913a0` and `91d549be6`.
    - Check that `dotnet --version` is 10.x, and that `dotnet run .claude/commands/spec/show_me_facts_tests.cs` exits `0`.
  - Risk handling:
    - If #4282 has merged, the (C-8) criteria have lapsed. Record which could not be exercised and do not simulate them.
    - If either 0036 file changed, re-measure the calibration row's file figures (37; 82; 62/12/2/6/0) with independent `grep -E` and update the row. If the change came from new #4282 commits, re-pin to the new head, re-measure the diff figures, and record the date.
  - Traces to: C-8, NFR-9 (*Why the calibration row is pinned*), AC-79.

- [ ] **PROJECT: T17.2 — (C-8) Run `/spec:show-me` on spec 0036 while PR #4282 is open**
  - Do:
    - Run `/spec:show-me 0036-scoped-lifetime-per-pipeline` twice, capturing `git status --porcelain` before and after each run.
    - Check AC-1, AC-2, AC-4 (resolution); AC-11; AC-12; AC-13; AC-14; AC-17; AC-18; AC-20; AC-24; AC-26; AC-30 (the only `git status` change is `show-me.md`; the ledger's `gh_commands` holds one `gh pr list … --state open`; no command-issued `gh` or `git merge-base`); AC-31; AC-32 (the two runs' mechanical fields are identical, and each run's F2 and F5 match its own evidence); AC-33; AC-34; AC-51; AC-56; AC-59, AC-60 and AC-63 (judged paths: each is recorded `unexercised on this run` when neither run took it); AC-62; AC-73; AC-74; AC-75; AC-76; AC-77; AC-78.
    - Copy the output to the scratch directory for the user, since it may inform #4282, then apply R3.
  - Acceptance: the acceptance record marks every listed AC `held`, `defect ({owning task})`, or, for a judged path not taken, `unexercised on this run`.
  - Traces to: C-8, FR-1–FR-21, NFR-1–NFR-8, and the ACs listed.

- [ ] **PROJECT: T17.3 — (C-8) AC-47 in the clone: a local branch that diverges from the remote-tracking ref**
  - Do: in R4 with K12's local `spec/scoped-lifetime-per-pipeline` at `386efee78`, run `/spec:show-me 0036-scoped-lifetime-per-pipeline`. Check that the remote-tracking ref (`91d549be6`) is measured, that the metadata and Blast radius name that full ref and its sha together with the base ref and merge base, and that the local-divergence line names `386efee78`. The merge base is the clone's (R4); it is not compared with the calibration value.
  - Traces to: FR-10, C-4, C-8, AC-47.

- [ ] **PROJECT: T17.4 — Regression sweep over the real no-diff fixtures**
  - Do: re-run the Verify set-ups of T9.1–T9.3, T11.1–T11.3, T12.1, T13.3, T14.1–T14.4, T14.6 and T14.7 on the real fixtures they name (`0002-*`, `0003`, `0005`, `0021`, `0023`, `0033`, plus `README.md` and `kafka-widget`). Check AC-1a, AC-3, AC-5, AC-6, AC-7, AC-8, AC-9, AC-10, AC-16, AC-19, AC-29, AC-35, AC-36, AC-37, AC-41, AC-42, AC-43, AC-44, AC-61, AC-70, AC-71, AC-72 and AC-93. AC-27 is checked on K2 in T17.5.
  - Traces to: FR-1–FR-3, FR-16, FR-17, FR-21, NFR-8, and the ACs listed.

- [ ] **PROJECT: T17.5 — Regression sweep over the clone fixtures K1–K14**
  - Do:
    - Rebuild R4 from the final commit and re-run the K-fixture set-ups, including K14's rule 2 and rule 3 runs. Check AC-15, AC-21, AC-22, AC-26 (K1), AC-27 (K2, with T14.4's `[]` stand-in), AC-28 (K2, `gh` failing), AC-40, AC-45, AC-52, AC-54, AC-55, AC-57, AC-58, AC-60, AC-64, AC-65, AC-66, AC-67, AC-68 and AC-85. Both halves of AC-57, AC-60, AC-64 and AC-65 follow the *Judged paths* rule.
    - (C-8 window, R8) For AC-25, compare the K2 run with `gh` failing (`Low`) with a `0036` run (`High`) in the same clone, also with `gh` failing, so both runs share one `gh` condition. The only difference in side effects is the text of `show-me.md`: the same shape of commands, one written path, and no marker, label or comment. The clone's `0036` figures are not calibration values (R4).
    - (C-8 window, R8) Rerun the R5 stand-in cases on the real repository for AC-46, AC-84 and FR-10's PR-head-differs line (T6.2, T11.3).
  - Traces to: FR-6, FR-8, FR-10–FR-13, FR-20, NFR-3, and the ACs listed.

- [ ] **PROJECT: T17.6 — End to end with a real marked section written by `/spec:write_release_notes`**
  - Do:
    - Run `/spec:write_release_notes 0033-pg-advisory-lock-sha256`, then `/spec:show-me 0033-pg-advisory-lock-sha256`.
    - Check AC-69: the section is read, the `release_notes.md` row is `used`, and neither row 5's nor row 5a's line appears. When `{m}` ≠ `{n}`, the disagreement line is present.
    - Tear down with `git checkout -- release_notes.md` and R3.
  - Traces to: FR-7, FR-15, FR-23, AC-69, AC-92.

- [ ] **PROJECT: T17.7 — Conventions and permission boundaries**
  - Do:
    - `git clone` into the scratch directory with no build, restore or install step. From its root, run `dotnet run .claude/commands/spec/show_me_facts.cs -- specs/0033-pg-advisory-lock-sha256`, which exits `0`, and the test script, which exits `0` (AC-53).
    - `git ls-files --stage` shows `100644` for both scripts, and there is exactly one of each in `.claude/commands/spec/`.
    - Both new command files have the family's front matter.
    - AC-82:
      - `git diff $PRE -- .claude/settings.json` shows only the T1.3 line.
      - In a Claude Code session, `dotnet run $SCRATCH/other.cs -- specs/x` and `dotnet run .claude/commands/spec/show_me_facts.cs --file x.cs` both raise a permission prompt.
      - The `.gitignore` diff is one exact line.
    - `git diff --stat $PRE -- src tests .github` is empty. `release_notes.md` matches `$PRE`.
  - Traces to: NFR-6, FR-18, C-10, AC-53, AC-82, AC-94 (inspection); ADR 0072 *Where each artefact is touched* ("Deliberately unchanged").

## Phase 18 — Close-out after PR #4282 merges

*Depends on T17.1–T17.3 and T17.5 being finished and on PR #4282 having merged.*

- [ ] **PROJECT: T18.1 — Merge `origin/master` into `spec/show-me` so that the spec's own diff no longer carries spec 0036, then re-run the test script**
  - Do:
    - `git fetch origin`, then `git merge origin/master` on `spec/show-me`, and resolve any conflicts.
    - Confirm that `git merge-base --is-ancestor 91d549be6 origin/master` succeeds (it was a merge commit), and that `git diff --stat origin/master...HEAD` lists only this spec's files.
    - Re-run `dotnet run .claude/commands/spec/show_me_facts_tests.cs`, which must exit `0` (AC-79 after the merge).
  - Risk handling:
    - If #4282 was squashed or rebased, `91d549be6` stays reachable only through this branch's history. Record that, and raise the AC-94 risk with the user.
    - If 0036's `requirements.md` or `tasks.md` changed on master after `91d549be6`, re-measure the calibration row's file figures as in T17.1.
  - Traces to: C-8, NFR-9 (*Why the calibration row is pinned*), AC-79, AC-94; ADR 0072 Risks (calibration commits unreachable).

## Coverage cross-reference

### Requirements → tasks

| Id | Tasks | Id | Tasks |
|---|---|---|---|
| FR-1 | T9.1, T9.2, T17.2, T17.4 | FR-17 | T10.1, T11.1, T14.4, T14.6 |
| FR-2 | T9.1, T9.2 | FR-18 | T1.3, T3.2, T6.2, T9.1, T17.2, T17.7 |
| FR-3 | T3.3, T3.4, T9.2 | FR-19 | T9.2, T14.7 |
| FR-4 | T1.2, T3.1, T7.1, T11.1 | FR-20 | T6.2, T11.3 |
| FR-5 | T11.1 | FR-21 | T2.1–T8.2, T9.2, T9.3, T14.7 |
| FR-6 | T5.3, T5.4, T10.1, T12.1, T12.2, T14.1, T14.3, T14.6 | FR-22 | T15.1, T15.2, T15.3 (and the form of this file) |
| FR-7 | T4.1, T10.2, T13.5, T14.1 | FR-23 | T15.4, T15.5, T15.6, T16.1, T17.6 |
| FR-8 | T3.5, T13.5, T14.2 | NFR-1 | T3.1–T5.4, T9.3, T11.2, T11.3, T17.2 |
| FR-9 | T3.3, T5.1, T11.2 | NFR-2 | T8.1, T8.2, T14.7 |
| FR-10 | T5.1, T6.1, T6.2, T11.3, T17.3 | NFR-3 | T5.1, T10.1, T10.2, T12.2 |
| FR-11 | T5.2, T13.3, T13.5 | NFR-4 | T6.2, T14.4 |
| FR-12 | T13.4, T14.5 | NFR-5 | T11.1, T14.6 |
| FR-13 | T13.1–T13.4, T14.5, T14.7 | NFR-6 | T3.1, T9.1, T14.6, T15.1–T15.4, T16.1, T17.7 |
| FR-14 | T12.2, T14.3 | NFR-7 | T12.2, T13.5, T17.2 |
| FR-15 | T14.4 | NFR-8 | T3.4, T7.1, T9.2, T9.3, T11.1 |
| FR-16 | T1.4, T3.5, T3.6, T6.1, T6.2, T10.2, T11.1–T11.3, T12.1, T13.3, T14.1–T14.4 | NFR-9 | T1.1, T1.4, T2.1–T8.2, T13.1, T16.2, T17.1, T18.1 |

All 32 declared ids have at least one task.

### Acceptance criteria → tasks

(C-8) criteria are **bold**. † marks a criterion, or a clause of one, that applies only on a judged path (Phase 17's *Judged paths* table). When the path is not taken it is recorded `unexercised on this run`; the table's claim is that a task checks it whenever the path is taken. Retired criteria (AC-38, 39, 48, 49, 50) need no task.

| AC | Tasks | AC | Tasks | AC | Tasks | AC | Tasks |
|---|---|---|---|---|---|---|---|
| **AC-1** | T9.1, T17.2 | AC-23 † | T13.4, T14.5 | **AC-51** | T14.1, T17.2 | AC-74 | T1.2, T11.1, T17.2 |
| AC-1a | T9.1, T9.2, T17.4 | AC-24 | T13.4, T14.7, T17.2 | AC-52 | T10.1, T17.5 | AC-75 | T14.4, T17.2 |
| **AC-2** | T9.2, T17.2 | AC-25 | T17.5 | AC-53 | T15.4, T16.1, T17.7 | AC-76 | T10.1, T17.2 |
| AC-3 | T9.1, T17.4 | AC-26 | T14.3, T17.2, T17.5 | AC-54 | T13.5, T14.2, T17.5 | **AC-77** | T10.1, T17.2 |
| **AC-4** | T9.2, T17.2 | AC-27 | T14.4, T17.5 | AC-55 | T13.5, T14.2, T17.5 | **AC-78** | T10.1, T12.2, T17.2 |
| AC-5 | T9.1, T17.4 | AC-28 | T6.2, T14.4, T17.5 | **AC-56** | T12.2, T17.2 | AC-79 | T2.1, T3.1, T3.3, T3.5, T4.1, T5.1, T5.2, T17.1, T18.1 |
| AC-6 | T9.2, T17.4 | AC-29 | T14.6, T17.4 | AC-57 † | T12.1, T12.2, T17.5 | AC-80 | T8.1, T8.2 |
| AC-7 | T3.4, T9.2, T17.4 | AC-30 | T6.2, T17.2 | AC-58 | T12.2, T17.5 | AC-81 | T13.1, T13.2 |
| AC-8 | T9.2, T17.4 | AC-31 | T14.7, T17.2 | AC-59 † | T8.1, T14.6, T14.7, T17.2 | AC-82 | T1.2, T1.3, T3.2, T17.7 |
| AC-9 | T11.1, T17.4 | AC-32 | T17.2 | AC-60 † | T14.3, T17.2, T17.5 | AC-83 | T7.1 |
| AC-10 | T11.1, T17.4 | AC-33 | T14.7, T17.2 | AC-61 | T12.1, T17.4 | AC-84 | T6.2, T11.3, T17.5 |
| AC-11 | T11.1, T17.2 | AC-34 | T17.2 | AC-62 | T17.2 | AC-85 | T6.1, T11.3, T17.5 |
| **AC-12** | T14.1, T17.2 | AC-35 | T9.1, T9.2, T17.4 | AC-63 † | T12.2, T17.2 | AC-86 | T15.1 |
| AC-13 | T14.1, T17.2 | AC-36 | T14.3, T17.4 | AC-64 † | T12.2, T17.5 | AC-87 | T15.2 |
| AC-14 | T14.1, T17.2 | AC-37 | T14.1, T17.4 | AC-65 † | T14.3, T17.5 | AC-88 | T15.3 |
| AC-15 | T13.5, T14.2, T17.5 | AC-40 | T14.1, T17.5 | AC-66 | T5.1, T11.3, T17.5 | AC-89 | T15.5 |
| AC-16 | T13.3, T14.2, T17.4 | AC-41 | T14.4, T17.4 | AC-67 | T5.3, T11.3, T17.5 | AC-90 | T15.4, T15.5 |
| **AC-17** | T11.2, T17.2 | AC-42 | T3.6, T14.1, T17.4 | AC-68 | T10.2, T14.4, T17.5 | AC-91 | T15.6 |
| **AC-18** | T5.1, T6.1, T6.2, T11.3, T17.2 | AC-43 | T3.5, T13.3, T14.2, T17.4 | AC-69 | T10.2, T14.4, T17.6 | AC-92 | T4.1, T15.5, T17.6 |
| AC-19 | T6.1, T11.1–T11.3, T17.4 | AC-44 | T11.1, T14.4, T17.4 | AC-70 | T3.1, T3.5, T9.3, T17.4 | AC-93 | T3.2, T9.2, T17.4 |
| **AC-20** | T13.3, T13.5, T17.2 | AC-45 | T13.5, T14.2, T17.5 | AC-71 | T3.4, T9.2, T17.4 | AC-94 | T16.2, T17.7, T18.1 |
| AC-21 | T13.5, T17.5 | AC-46 | T6.2, T11.3, T17.5 | AC-72 | T9.2, T9.3, T17.4 | AC-95 | T15.4, T15.5 |
| AC-22 | T13.5, T14.5, T17.5 | **AC-47** | T6.1, T17.3 | AC-73 | T6.2, T17.2 |  |  |

Every one of the 91 live ACs is covered. The 12 (C-8) criteria are AC-1, 2, 4, 12, 17, 18, 20, 47, 51, 56, 77 and 78. They are exercised in T17.2 and T17.3 inside R8's window, and they lapse when #4282 merges. AC-79 is pinned and survives the merge (T18.1).

### ADR decisions and Implementation Approach steps → tasks

| ADR | Decision / step | Tasks |
|---|---|---|
| 0072 | IA 1 (`.gitignore`, settings) | T1.2, T1.3 |
| 0072 | IA 2 (fixture tree) | T1.4 (plus T1.1, the staged-fixture removal) |
| 0072 | IA 3 (harness) | T2.1, T3.1 (ledger save and restore) |
| 0072 | IA 4 (file fields, grammar, gate) | T3.1–T3.6 |
| 0072 | IA 5 (marked sections, `{m}`) | T4.1 |
| 0072 | IA 6 (pinned diff fields, 10 boundary rows) | T5.1–T5.4 |
| 0072 | IA 7 (unpinned ref fields) | T6.1, T6.2 |
| 0072 | IA 8 (atomic write, cap, residue) | T7.1 |
| 0072 | IA 9 (word count) | T8.1, T8.2 |
| 0072 | IA 10 (command file, Steps 1–8) | T9.1–T9.3, T10.1–T10.2, T11.1–T11.3, T12.x, T13.x, T14.1–T14.7 |
| 0072 | IA 11 (README, CONTRIBUTING) | T16.1, T16.2 |
| 0072 | KC1 script / KC2 ledger | T3.1–T6.2, T7.1, T8.1 |
| 0072 | KC3 reads and read log | T9.3, T10.1, T10.2 |
| 0072 | KC4 gate and four stops | T3.4, T9.1–T9.3 |
| 0072 | KC5 output document | T11.1–T11.3, T14.1–T14.4, T14.6 |
| 0072 | KC6 test script and fixtures | T1.4, T2.1, T3.2, T3.3, T7.1, T13.1, T16.2 |
| 0072 | Technology Choices (C# app and JSON, payload in a file, allow entry, `allowed-tools`, no sub-agent) | T3.1, T2.1, T1.3, T3.2, T9.1 |
| 0072 | Risks (SDK `--`, killed tmp, prefixes, argument split, calibration reachability, model counting) | T3.2, T7.1, T3.4, T9.1, T16.2/T18.1, T9.3/T17.2 |
| 0073 | IA 1 (FR-13 row) · IA 2 (markers) · IA 3 (factors, forced, mapping) · IA 4 (max, raise, sentence, checks) · IA 5 (Classifier; rationale; FR-19 copy) | T13.1 · T13.2 · T13.3 · T13.4 · T13.5, T14.5, T14.7 |
| 0073 | KC1–KC5 | T13.5, T13.3, T13.3, T13.4/T14.5, T13.1/T13.2/T14.7 |
| 0077 | IA 1 (ladder, lines, node list) · IA 2 (Explainer) · IA 3 (placement) · IA 4 (Inputs used rows) · IA 5 (pre-Write checks) | T12.1 · T12.2 · T14.1, T14.3 · T14.4 · T14.6 |
| 0077 | KC1–KC6 | T12.2 (KC1–KC4), T14.3 (KC5), T14.6 (KC6) |
| 0078 | IA 1 · IA 2 · IA 3 · IA 4 · IA 5 · IA 6 | T15.1 · T15.2 · T15.3 · T15.4, T15.5 · T15.6 · T16.1 |
| 0078 | KC1–KC6 | T15.1, T15.2, T15.3/T15.6, T15.5, T15.4/T15.5, T15.6 |

Every Implementation Approach step of the four ADRs has at least one task.

### Scope-creep check

Every task traces to a requirement or an ADR decision. The four that might look extra are these:

- **T1.1** is the settled removal of the staged fixture. It traces to NFR-9's "not under `specs/`" rule.
- **T17.1–T17.7** are acceptance runs. They add no artefact.
- **T18.1** traces to C-8, NFR-9's pinning rationale and AC-79/AC-94.

No task adds anything under `src/`, `tests/` or CI, and no task edits `release_notes.md` permanently.
