# Tasks — Routing slips for the spec workflow

**Spec**: [requirements.md](requirements.md) · **Design**: [ADR 0089](../../docs/adr/0089-slips-resolver-and-routing.md), [ADR 0090](../../docs/adr/0090-approval-sub-flows.md), [ADR 0091](../../docs/adr/0091-slip-creation-tamper-detection-and-route-change.md), [ADR 0092](../../docs/adr/0092-routing-fixtures-and-test-scripts.md) (all Accepted) · **Issue**: [#4661](https://github.com/BrighterCommand/Brighter/issues/4661)

## How to read this list

### Test-first for this spec (owner decision, scoped to spec 0040 only)

This rule applies to spec 0040 only. It changes nothing in `CLAUDE.md` or `.agent_instructions/testing.md`, and it applies to no other spec.

- **C# scripts and fixtures stay test-first.** That covers `resolve_route.cs`, `show_me_facts.cs`, `resolve_route_tests.cs`, `spec_text_tests.cs`, and the fixture tree under `.claude/test-fixtures/routing/`. Each task states one or both of two routes, one per part (new test code, and data rows):
  - **`/tdd:test-first <behaviour>`**, when the task writes new test code (harness, generated cases, checks).
  - **RED-first row**, when the test is data: an expectation file under `.claude/test-fixtures/routing/expect/`, or a registry row in `spec_text_tests.cs`. You write the row, run the script, and see it fail for the stated reason before you write any code.
- **Command, skill and README markdown are behavioural-text or structural tasks, not `/tdd:test-first` tasks.** That covers `.claude/commands/**/*.md`, the READMEs and `.agent_instructions/*.md`. Verification is one of three things, which each task names:
  - (a) the 0092 text-comparison registry row, added first in its own `test:` commit and seen failing;
  - (b) the sibling ADR's grep (AC-9, AC-79, AC-40);
  - (c) a named hand step.
  - The ⛔ line comes after the verification row has been seen failing.
- **These still hold, unchanged:**
  - RED first.
  - The full regression suite (S1).
  - The two-commit shape: a `feat:`/`test:` commit, then a separate `docs:` commit that ticks the box. Where 0092 step 9 requires a registry row in "the commit just before" a copy, a task has three commits: `test:` (red row), `feat:` (copy), `docs:` (tick).
  - The gear defaults to `review-before`. This list does not encode the gear. Shift it only with `/spec:gear`, which can be scoped to one of the `## Phase N — …` headings below. Keep those headings stable.
- **Characterisation.** Sometimes a new row or check passes the first time it runs. When that happens, see it fail under the **named mutation** given in the task, applied to production code (never the test), and then revert. Exceptions: a check whose subject is a tracked non-code file it inspects (T1.3's `.gitignore`, T8.8's `.claude/flows/`, T9.1's fixtures), where the mutation changes that file in the working tree and it is restored and confirmed unmodified; and T10.3, whose RED is a named macOS hand step. Never commit the mutation (testing.md *Characterisation*). The four-tag rule keeps the tag at `TEST + IMPLEMENT`. A **Characterise** bullet carries testing.md's `CHARACTERISE` label and names the mutation.

### Tags

`TEST + IMPLEMENT` is a behaviour, with a ⛔ gate. `STRUCTURAL` adds files nothing reads yet. `DOC` is a README or instructions prose change with no comparison row. `PROJECT` is a baseline, a verification run or a hand check that adds no committed artefact other than the tick.

### Shared rules (cited as S1–S8)

- **S1 — Regression suite.** Run every one of these that exists, from the repository root, before each `feat:`/`test:` commit:
  - `dotnet run .claude/commands/spec/resolve_route_tests.cs`
  - `dotnet run .claude/commands/spec/spec_text_tests.cs`
  - `dotnet run .claude/commands/spec/show_me_facts_tests.cs`

  Nothing under `src/` or `tests/` changes, so no solution build or xUnit run is needed. A task that unexpectedly touches either must stop and ask.
- **S2 — Recompile check.** After any edit to `resolve_route.cs`, the next `resolve_route_tests.cs` run is the recompile. Its pre-check must report no `the resolver printed lines other than record lines` FAIL, because the resolver compiles with no warnings (0089 *deterministic*). Never run the resolver by hand between the edit and that run.
- **S3 — Fixture records.** Write every tracked `.flow.record` with 0091's installation steps (`tr -d '\r'`, `shasum -a 256` or `sha256sum`, `date +%Y-%m-%d`, quoted here-document). A case whose flow has no definition (`unknown-flow`, `spike-step`, `spike-gate-also`, `spike-gate-also-approved`, `evaluation-ready`, `copy-invalid`) gets a hand-written CR-free `.flow`, and its record is written with steps 2–4 only.
- **S4 — Line citations.** ADR line numbers refer to files as they are before this spec. Once an earlier task has edited a file, find the cited text by its content, not its number. This matters most in `approve.md` and `.claude/commands/spec/README.md`.
- **S5 — Restart after a command edit.** Restart the Claude Code session after editing a command file and before any hand step that runs that command.
- **S6 — Fixture-only commits.** The `light` and `bugfix` commits (T7.1, T7.2) touch only `.claude/test-fixtures/routing/`. `git diff --stat HEAD~1 -- .claude/commands/ bugfixes/` must be empty. If a row fails, follow the T7 contingency: fix the resolver first, in its own commit, then make the fixture commit again.
- **S7 — Scratch branches.** Scratch and throwaway branches are never pushed or merged, and are deleted after use. Notes that only one task needs go in the session scratchpad. Notes a later session needs (T0.1's `BASE` and `PRE`, T13.5's baseline) go in `specs/0040-routing-slips/baselines.md`, which is untracked, is never staged, and is named in `PROMPT.md`. Neither is ever committed.
- **S8 — Partial matcher.** An array expectation must match in length (0092 *Expectations*). Each `violations` expectation therefore lists every violation its fixture raises, in rule order.

## Open items needing a ruling (before the phase named)

- **O-1 (RULED: recommended option adopted): no RED row exists before the fixture commits for gates, `next.detours` and `stray_markers`.**
  - 0092 step 5 needs the `light` and `bugfix` commits to pass with no resolver edit. But none of 0092 step 4's cases has a gate, a detour reported in `next`, or a non-empty `stray_markers`.
    - The rule fixtures run in flow form, which has no `gates` or `next`.
    - `flows-edited/`, the only other hand-off detour, lands in step 7.
    - `legacy-stray-ignored` lands with `bugfix`.
  - So 0089 step 5's gate, detour and stray code would be written with no failing row, which is speculative code. If it is deferred instead, the fixture commits fail and the FR-25/FR-28 experiment fails.
  - **Recommended:** make four changes, using fixtures the ADR already names (T6.6, T6.8, T6.9) and one hand-written case:
    1. `spike-step`'s hand-written slip, whose content the ADR leaves open, carries a gate and a detour attached to a step.
    2. `evaluation-ready` carries a stray `.requirements-approved`.
    3. `flows-edited/` moves forward from 0092 step 7, with one extra expectation: `legacy-all-approved` on it.
    4. A second hand-written gate slip, `spike-gate-also`, whose gate command also approves the step it produces, in a fresh and an approved state (T6.8). It makes `also_approves_produced: true`, `previous_approved: true`, the non-`/spec:approve` marker-writer reflected in `approve_steps` and `known_kinds`, and the R10 exemption (T5.12, guarded by a named mutation) covered before Phase 7.
  - **Alternative:** see these rows fail against uncommitted `light`/`bugfix` fixtures, commit the resolver first, then make the fixture commits. This is the ADR's own fallback, but it records the experiment as having needed a resolver edit.
- **O-2 (RULED: keep C1–C10, add a clause-mapping column, one method per row): AC-41 and the C1–C10 checklist.**
  - AC-41 asks for "one yes/no item for each FR-19 clause", and each item names "whether" it is checked by a fixture or by a statement.
  - FR-19 has nine clauses. 0091's checklist has ten items: C10 is extra, carrying 0090's item (a). C1, C3, C5 and C8 name *both* a fixture and a statement.
  - Ruling: keep 0091's C1–C10 and add a column naming the FR-19 clause each item covers, with one checking method per row (T11.3).
- **O-3 (RULED: merge 0090 steps 3 and 4 into one commit, T14.1): `touch` versus the identical sub-flow-instructions copy.**
  - 0090 step 3 keeps `touch` for the marker for one commit. But the identical `sub-flow-instructions` copy that the same commit adds says (step 7) "append the provenance line".
  - For that one commit, `approve.md` cannot both carry an identical copy and keep `touch` without contradicting itself.
  - Ruling: merge 0090 steps 3 and 4 into one commit (T14.1). 0090 step 7 compares the state before step 3 with the state after step 4, so the comparison is not affected.
- **O-4 (RULED: agreed; no task): accepted departures, not amended in the requirements.**
  - 0090 narrows the `design` sub-flow against requirements Out of Scope, FR-29's "structural refactoring", AC-24 and AC-77. A stricter ADR selection rule and frontmatter checks can now stop an approval, and so on.
  - 0091's `/spec:route` ladder row 5 narrows FR-17's already-on-target stop and AC-37.
  - Both are recorded here as risks. No task amends `requirements.md`.

## Risks and mitigations

| # | Risk | Mitigation (task) |
| --- | --- | --- |
| K1 | `--all` budget is thin (about 21,000 of the 24,000 characters estimated for 47 specs; summary lines ≤650). | T8.6 fails, with the measured size, before the output is cut. If it fails, **stop and ask**: the remedy (paging, retiring specs) is undecided (0089). `route unavailable` keeps status usable. |
| K2 | A compiler warning is taken for the report. | S2. T4.1's pre-check runs as the recompile. Consumers read prefixed lines only. |
| K3 | NFR-4 timing only warns and exits 0. | T8.7 prints a `WARN` with the duration. T17.4 has a person read it (AC-51). |
| K4 | A fixture commit needs a resolver edit, so the experiment fails. | O-1 and S6. If it happens, record the finding in the commit message and in the PR. |
| K5 | A fixture record goes stale. | T9.1's integrity check. S3. |
| K6 | Many command-side ACs are verified only by the Constraints stand-in (a resolver row plus text comparison), with no hand step: AC-10, 11, 14, 17, 23, 28, 42, 66, 67, 69, 82, 83, 86. | This is accepted by Constraints bullet 1. The reviewer may extend the T17.3 scratch-branch run. |
| K7 | AC-40's literal search for `--flow` also matches `--flows-dir` in `route.md` and the README. | T17.4 searches with `--flow([^s-]\|$)`; every match must be prose denying the option. |
| K8 | This spec is driven by `/spec:implement`, which T13.7 and T13.8 rewrite. | 0040 is a legacy `feature` spec, so it resolves to the hand-off. S5: restart after T13.7 and T13.8. A broken resolver halts the drivers, so S1 must be green before any command task. |
| K9 | README line drift. | S4. |
| K10 | Phantom expectations: a partial match asserts only what was written. | Each task lists the fields to pin. Review checks them against the AC. |
| K11 | O-4 departures. | Recorded. No task. |

---

## Phase 0 — Baselines

*Dependencies: none.*

- [x] **PROJECT: T0.1 — Record baselines before any change**
  - Do:
    - Record `BASE=$(git merge-base HEAD origin/master)` and `PRE=$(git rev-parse HEAD)` in `specs/0040-routing-slips/baselines.md` (untracked; name it in `PROMPT.md`). `BASE` can always be recomputed with `git merge-base HEAD origin/master`, and `PRE` is not needed to rebuild a baseline: the files this spec edits are unchanged at that merge base.
    - Confirm that `dotnet --version` is 10.x.
    - Confirm that `.claude/flows/` and `.claude/test-fixtures/routing/` do not exist.
    - Confirm that `specs/.current-spec` names `0040-routing-slips`.
    - Run `dotnet run .claude/commands/spec/show_me_facts_tests.cs` and record that it passes.
  - Verification: all five facts are recorded. Nothing is committed apart from the tick.
  - Traces to: AC-6 and AC-46 (diff baselines); NFR-6.

## Phase 1 — Text-comparison script

*0092 step 1. Comes before 0090 step 1. Depends on T0.1.*

- [x] **TEST + IMPLEMENT: T1.1 — `spec_text_tests.cs` fails, naming the block, when a registered text block is missing from its canonical file or a required copy is absent**
  - **USE COMMAND**: `/tdd:test-first when the spec text test script finds a registered routing-text block missing from its canonical file or from a required copy it should print a FAIL line naming the block and file and exit 1`
  - Test file: `.claude/commands/spec/spec_text_tests.cs` (new).
  - Test should verify:
    - The registry holds `adr-selection-rule`: canonical file `.claude/commands/adr/README.md`; required copies `adr/accept.md`, `adr/retire-superseded.md`, `adr/report-approval.md`.
    - A run today prints a `FAIL` for the missing canonical block and for each missing file, ends with `{f} failed assertion(s) across {n} check(s).`, and exits `1`.
    - Each failure states which of these it is: canonical block missing, canonical block not unique, copy missing, copy duplicated, or copy differs (compared byte for byte after reading CRLF as LF).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Be a file-based app at mode `100644` with no shebang, using the base class library only.
    - Put the registry at the top and a `// Harness` region at the bottom, holding the process runner (`ArgumentList`), the CRLF-to-LF reader and the FAIL/summary reporter.
    - Extract the text between `<!-- routing-text:{id}:begin -->` and `<!-- routing-text:{id}:end -->`.
    - Compare any other file under `.claude/commands/` that carries a block marker too, not only the files the registry lists.
  - Commit: `test:`. The run is expected to stay red until T2.1–T2.5.
  - Traces to: 0092 step 1, *Text blocks*, *The two scripts*, *Harness*; FR-24 (no framework, FAIL lines, non-zero exit); 0090 *What 0092 needs*. Depends on: T0.1.

- [x] **TEST + IMPLEMENT: T1.2 — The template-table parser splits cells on `|` only outside CommonMark code spans, and the `/adr:*` template rows are registered**
  - **USE COMMAND**: `/tdd:test-first when a template text cell holds a lone backtick inside a double-backtick code span the table parser should keep it as one cell`
  - Test file: `.claude/commands/spec/spec_text_tests.cs`.
  - Test should verify:
    - The parser self-test passes on embedded literal copies of 0091's *Route usage* and *Spec name* rows. A naive split that toggles on each backtick merges their cells, and the self-test fails.
    - Registered `templates` expectations: *Supersession question* is in `adr/retire-superseded.md` and *Report* is in `adr/report-approval.md`, each compared with the cell of the same Template name in `adr/README.md`'s canonical `templates` block.
    - Both are red today, because the files are absent.
    - A `templates` block holding a template that the registry does not expect for that file is a FAIL.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Match backtick runs of equal length (CommonMark).
    - Parse a canonical file's `| Template | Printed by | Text |` tables and each copy's `| Template | Text |` table.
  - Traces to: 0092 step 1, *Text blocks*; 0090 *Message templates*. Depends on: T1.1.

- [x] **TEST + IMPLEMENT: T1.3 — The ignore check proves git would track a spec's `.flow`, `.flow.record` and `route-changes.md`, and would ignore them for a `.log` name**
  - **USE COMMAND**: `/tdd:test-first when git check-ignore --no-index is asked about a spec's routing files it should exit 1 for each including a directory name with a space and exit 0 for a name ending .log`
  - Test file: `.claude/commands/spec/spec_text_tests.cs`.
  - Test should verify:
    - `git check-ignore -q --no-index` exits `1` for `specs/9999-check ignore probe/.flow`, `…/.flow.record` and `…/route-changes.md`.
    - It exits `0` for `specs/9999-probe.log/.flow`.
  - **Characterise**: this passes on landing, because the rules already exist. Named mutation: temporarily append `route-changes.md` to `.gitignore`. The check must FAIL on that probe. Revert, and confirm that `git status` shows `.gitignore` unmodified.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: run git through the harness runner with `ArgumentList`, so that a name with spaces is passed safely.
  - Traces to: 0092 step 1, *Further checks*; 0091 *What 0092 needs* (ignore check); NFR-5; AC-52 (support). Depends on: T1.1.

## Phase 2 — The `design` sub-flow commands

*0090 step 1. This is the prerequisite for 0089 step 1. Depends on Phase 1. No copy lands before its canonical block.*

- [x] **DOC: T2.1 — Write the canonical ADR selection rule and the `/adr:*` templates in `.claude/commands/adr/README.md`**
  - Do:
    - Add `<!-- routing-text:adr-selection-rule:begin/end -->` around 0090's three-step rule.
    - Add a `templates` block holding the `| Template | Printed by | Text |` rows *Supersession question* and *Report*, word for word from 0090 *Message templates*.
    - Describe the four commands in a short list. The full README rewording is T16.2.
  - Verification: `spec_text_tests.cs` no longer reports a missing canonical block. It still reports the missing copies, which T2.2–T2.5 add.
  - Traces to: 0090 step 1 (README part), *The `design` sub-flow commands*; 0092 step 1. Depends on: T1.2.

- [x] **TEST + IMPLEMENT: T2.2 — `/adr:accept` accepts each selected ADR in its actions pass and prints `approved-artefacts:`; its questions pass checks frontmatter and writes nothing**
  - Verification row (RED-first, already failing from T1.1): the `adr-selection-rule` copy in `adr/accept.md` is missing. No `/tdd:test-first` applies, because this is command text (spec-0040 rule).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Create `.claude/commands/adr/accept.md` with `## Arguments`, `## Questions` and `## Actions`, using 0090's argument form and protocol lines.
    - Take its text from `approve.md:38-46`.
    - Questions: the selection faults, the no-frontmatter fault, and the six read-only checks of `write_adr_metadata` Step 3, each with the fix named.
    - Actions: `write_adr_metadata docs/adr/{name} status Accepted`, skipping an ADR already `Accepted` in both places, or already `Superseded` by another selected ADR. Then print `approved-artefacts:` listing every selected ADR.
    - Carry the marked identical copy of `adr-selection-rule`.
    - `allowed-tools: Read, Glob, Bash(grep:*), Skill`.
  - Verify after:
    - S1 shows `accept.md`'s rows green.
    - Hand step: run `/adr:accept actions "specs/0040-routing-slips" design .adr-list` on the current branch. ADRs 0089–0092 are already `Accepted`, so `git status` stays unchanged (idempotence); this part passes for a no-op. The check that can fail is the output: it must end with `approved-artefacts:` listing all four, then `sub-flow-done: /adr:accept actions`. Then, on a throwaway branch (S7): copy one ADR under a new number, set it to `Proposed` in both places, write a temporary `.adr-list` naming it, run the command, check the status changed in both places, and delete the branch.
  - Traces to: 0090 step 1, *Failure and re-runs*, *Arguments and answers*, *The protocol lines*; FR-29. Depends on: T2.1.

- [x] **TEST + IMPLEMENT: T2.3 — `/adr:retire-superseded` asks the supersession question in its questions pass, only when the answer is not obvious, and supersedes or deprecates in its actions pass**
  - Verification rows (RED-first, failing since T1.1 and T1.2): the `adr-selection-rule` copy and the *Supersession question* template copy in `adr/retire-superseded.md`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Take its text from `approve.md:48-54`.
    - Follow 0090's ask-only-when-needed rule: `none` with no question; `supersede:{stem}` with no question for exactly one existing, un-`§`, not-otherwise-superseded target; otherwise the question, offering at most three ADRs, with a validated free-text answer.
    - Print one `answer: /adr:retire-superseded.{stem}=…` line per selected ADR.
    - Actions: `supersede --by` or `deprecate` through `write_adr_metadata`, skipping a target already retired.
    - `allowed-tools: Read, Glob, Bash(grep:*), AskUserQuestion, Skill`.
  - Verify after: S1 shows this file's rows green. Hand step now: run `/adr:retire-superseded questions "specs/0040-routing-slips" design .adr-list` and check it prints the supersession question or a `none` answer line per ADR and writes nothing. Full behaviour is verified by T17.1 items 2, 7 and 11.
  - Traces to: 0090 step 1, *The `design` sub-flow commands*; FR-29 (questions before writes, idempotence). Depends on: T2.1.

- [x] **TEST + IMPLEMENT: T2.4 — `/adr:regenerate-index` rebuilds `docs/adr/index.md` byte-identically, with or without sub-flow arguments**
  - Verification: there is no registered block. Named hand step: first hand-edit one line of `docs/adr/index.md` in the working tree, then run `/adr:regenerate-index` with no arguments, then `git diff --exit-code docs/adr/index.md` (clean). The command must have repaired the edit. If the diff is not clean, run `git restore docs/adr/index.md` and treat the step as failed.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Take its text from `approve.md:56-57` and `:102-112`.
    - Questions: nothing to ask. Print `sub-flow-done`.
    - Actions: `awk -f .claude/commands/adr/generate_adr_index.awk docs/adr/[0-9]*.md > docs/adr/index.md`. If awk exits non-zero, print `sub-flow-failed … awk exited {code}`.
    - `allowed-tools: Bash(awk:*)`.
  - Traces to: 0090 step 1; FR-29. Depends on: T2.1.

- [x] **TEST + IMPLEMENT: T2.5 — `/adr:report-approval` prints the four report lines and the commit reminder, and writes nothing**
  - Verification rows (RED-first): the `adr-selection-rule` copy and the *Report* template copy in `adr/report-approval.md`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Take its text from `approve.md:60-61`.
    - Actions read the retire answers.
    - `allowed-tools: Read, Glob, Bash(grep:*)`.
  - Verify after:
    - S1: `spec_text_tests.cs` is now fully green.
    - Hand step: `/adr:report-approval actions "specs/0040-routing-slips" design .adr-list --answer "/adr:retire-superseded.0089-slips-resolver-and-routing=none"` prints the four report lines and the commit reminder (check each line is present) and leaves `git status` unchanged.
  - Traces to: 0090 step 1; FR-29 (report moves into the sub-flow). Depends on: T2.1.

## Phase 3 — Flow definitions

*0089 steps 1–2. Step 1's prerequisite (0090 step 1) is met by Phase 2.*

- [x] **STRUCTURAL: T3.1 — Add `.claude/flows/feature.flow` and `.claude/flows/approvals.md`**
  - Do:
    - `feature.flow` is exactly 0089's *slip format* block: FR-4's table, `hand-off: /spec:implement`, and no detours.
    - `approvals.md` has the header `# approvals`, then `| requirements | (empty) |`, `| design | /adr:accept, /adr:retire-superseded, /adr:regenerate-index, /adr:report-approval |` and `| tasks | (empty) |`.
    - Use LF only, with no BOM and no trailing whitespace.
  - Acceptance:
    - No command reads either file yet.
    - S1 is unchanged.
    - Opened in an IDE, `feature.flow` shows the name, the three steps with every column, and the hand-off (AC-2).
  - Traces to: 0089 step 2; FR-1, FR-2, FR-4, FR-29 (shipped table); NFR-2; AC-2. Depends on: T2.2–T2.5 (so that R12 (c) resolves).

## Phase 4 — Resolver harness and skeleton

*0092 step 2, then 0089 step 3. Depends on T3.1.*

- [x] **TEST + IMPLEMENT: T4.1 — `resolve_route_tests.cs` pre-checks that the resolver runs, proves its matcher can fail, runs every expectation twice, and checks that nothing it reads was written**
  - **USE COMMAND**: `/tdd:test-first when the route resolver test script runs and the resolver does not exist it should print FAIL the resolver did not run, skip every expectation and exit 1`
  - Test file: `.claude/commands/spec/resolve_route_tests.cs` (new). Fixture: `.claude/test-fixtures/routing/expect/bad-mode.json` (new): `run: ["--bogus"]`, `exit: 1`.
  - Test should verify:
    - The pre-check runs `--definition feature` against `.claude/flows` and requires a `route-report:` line. If there is none: one FAIL, `the resolver did not run`, and every expectation is skipped. An extra stdout line gives its own FAIL, `the resolver printed lines other than record lines`.
    - Matcher self-test: a known mismatch must be reported, or the run fails.
    - An exit-`1` expectation requires empty stdout and no record line.
    - Each expectation runs twice, and the two stdouts must be byte-identical (NFR-1, AC-7).
    - Every file under the fixture root and `specs/` is hashed before and after, and the hashes must match (NFR-3, AC-7).
    - The known top-level keys are `run`, `flows`, `exit` and `report` (T10.0 adds `show_me` and `ledger`). An unknown key, or unreadable JSON, is a FAIL for that file.
    - Today: the pre-check FAILs and the script exits `1`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Make the specs root always `{fixture root}/specs`.
    - Resolve `flows` to a directory under the fixture root, or to `.claude/flows` when it is absent.
    - Use partial matching: objects by key, arrays by length and element, scalars by equality.
    - Print the summary line and set the exit status as 0092 *The two scripts* states.
    - Duplicate the three harness helpers from `spec_text_tests.cs` rather than sharing them (0092 *Harness*).
  - Traces to: 0092 step 2, *Expectations*, *Repository checks* (matcher self-test, read-only); FR-24; NFR-1, NFR-3; AC-7, AC-47. Depends on: T3.1.

- [x] **TEST + IMPLEMENT: T4.2 — `resolve_route.cs` parses its argument grammar, exits `1` with empty stdout on a usage error, and prints one `route-report:` record line for `--definition`**
  - **USE COMMAND**: none. This is the RED-first route: T4.1's pre-check and `bad-mode.json` are already failing.
  - Test file: `resolve_route_tests.cs` (unchanged); `expect/bad-mode.json`.
  - Test should verify: the pre-check passes, with record lines only (S2); `bad-mode` passes; the two-run determinism holds.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Create `.claude/commands/spec/resolve_route.cs` at mode `100644` with no shebang.
    - Add `Invocation`: `{specs-root} {mode} [--flows-dir {dir}]`; the modes `--spec`, `--current`, `--all`, `--definition`; anything else exits `1`.
    - Add the exit constants, modelled on `show_me_facts.cs:10-12`.
    - Add `ReportWriter`: a `JsonObject` with fixed key order, written with `ToJsonString`, no indentation, `UnsafeRelaxedJsonEscaping`, a `route-report: ` prefix and `\n`.
    - Make the `--definition` report carry only `schema_version` and `mode`, so that T5.1 starts red.
    - Compile with no warnings.
    - 0089 calls this step "structural". It is tagged here because it turns rows green.
  - Traces to: 0089 step 3, *The resolver's command line*, *The report is deterministic*, *Why the report travels on standard output behind a prefix*; NFR-1, NFR-2. Depends on: T4.1.

## Phase 5 — Definitions and validity rules

*A task that adds a rule may append that rule's violation to earlier expectations that it also trips (S8); that is not a failure of the earlier task. A violation that names several steps has `step: null` (0089, *The validity rules*). 0092 step 3 with 0089 step 4, one rule at a time. Each rule's fixture is `flows-rules/{rule}.flow` with expectation `expect/definition-{rule}.json` (`run: ["--definition", "{stem}"]`, `flows: "flows-rules"`), added red before the code (RED-first row). Each expectation (except the valid-definition rows) asserts `valid: false` and the full `violations` list (S8): `rule`, `code`, `step`, and the message from 0089's rule table. Depends on Phase 4.*

- [x] **TEST + IMPLEMENT: T5.1 — A valid definition is reported in flow form, with its source, presence and no violations, against the primary fixture directory**
  - **USE COMMAND**: none — RED-first rows: `expect/definition-feature.json` (`flows: "flows"`).
  - Fixture (new):
    - `.claude/test-fixtures/routing/flows/feature.flow`, a copy of the shipped file.
    - `flows/approvals.md`: `requirements` `(empty)`, `design` (the shipped row), `tasks` `(empty)`, `evaluation` `(empty)`.
  - Test should verify: `flow: {name: "feature", source: "definition", definition_present: true}`, `valid: true`, `violations: []`, and `stop: null`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `SlipReader`'s accepting path. Read the file as bytes and decode with a decoder that keeps a BOM. Convert CRLF to LF. Ignore trailing spaces and tabs.
    - Build the flow-form report from `schema_version`, `mode`, `flow`, `valid`, `violations` and `stop`. `stop` stays `null` until T8.2.
  - Traces to: 0089 step 4, *The slip format*, *Key Components*; 0092 step 3; FR-1, FR-2, FR-6 (flow query); NFR-2. Depends on: T4.2.

- [x] **TEST + IMPLEMENT: T5.2 — R0: malformed syntax, a missing definition and a bad flow name are each violation R0, and R0 suppresses the other slip rules**
  - **USE COMMAND**: none — RED-first rows: `definition-r00-malformed`, `definition-nosuch`, `definition-bad-name`.
  - Fixture: `flows-rules/r00-malformed.flow`, with a prose line after the table. Also add `flows-rules/approvals.md`, holding entries for the three known kinds only.
  - Test should verify:
    - `r00`: one R0 violation, `{file} line {n} is not valid slip syntax: …`. R1–R11 and R13–R16 are not evaluated.
    - `nosuch`: R0 with `step: null`, detail `definition missing`, message `Flow nosuch has no definition at {flows-dir}/nosuch.flow.`, and `definition_present: false`.
    - `bad-name` (`--definition Bad_Name`): R0, detail `bad name`. Exit `0`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: apply every grammar rule from 0089's *slip format* list (1–8), including backticked commands, a detour before the hand-off, and a table row after a blank line.
  - Traces to: 0089 R0, *The validity rules* (R0 short-circuit); FR-3; AC-3 (support). Depends on: T5.1.

- [x] **TEST + IMPLEMENT: T5.3 — R1: a slip with no steps is invalid**
  - **USE COMMAND**: none — RED-first row `definition-r01-no-steps`.
  - Fixture: `flows-rules/r01-no-steps.flow`, with a header row, a delimiter row and no data rows.
  - Test should verify: R1 `no-steps`, `Flow r01-no-steps has no steps.`, and `step: null`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: add R1 to `SlipValidator`.
  - Traces to: FR-3; AC-3; FR-24 (zero steps). Depends on: T5.2.

- [x] **TEST + IMPLEMENT: T5.4 — R2: duplicate step names are invalid, naming the step**
  - **USE COMMAND**: none — RED-first row `definition-r02-duplicate-step`.
  - Fixture: `flows-rules/r02-duplicate-step.flow`.
  - Test should verify: R2 `duplicate-step` names the duplicated step, plus any further rule the fixture raises (S8).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: add R2.
  - Traces to: FR-3; AC-3; FR-24. Depends on: T5.3.

- [x] **TEST + IMPLEMENT: T5.5 — R3: two steps naming one marker is invalid**
  - **USE COMMAND**: none — RED-first row `definition-r03-duplicate-marker`.
  - Fixture: `flows-rules/r03-duplicate-marker.flow`.
  - Test should verify: R3 `duplicate-marker`, `Marker {marker} is named by more than one step ({steps}).`
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: add R3.
  - Traces to: FR-3; AC-3. Depends on: T5.4.

- [x] **TEST + IMPLEMENT: T5.6 — R4: a producer or marker-writer that does not resolve under `.claude/commands/` is invalid**
  - **USE COMMAND**: none — RED-first row `definition-r04-unknown-command`.
  - Fixture: `flows-rules/r04-unknown-command.flow`, whose `design` step names `/spec:designx`.
  - Test should verify: R4, `Step design names a command that does not exist, /spec:designx.`
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: add `CommandCatalogue`, which resolves `/{family}:{name}` by ordinal comparison against a directory listing, so `/spec:readme` does not match `README.md`. Check every producer and marker-writer through it, comparing the full command text with space runs collapsed.
  - Traces to: FR-3, FR-24 (nonexistent command); AC-3. Depends on: T5.5.

- [x] **TEST + IMPLEMENT: T5.7 — R5: one producer named by two steps is invalid**
  - **USE COMMAND**: none — RED-first row `definition-r05-shared-producer`.
  - Fixture: `flows-rules/r05-shared-producer.flow`.
  - Test should verify: R5 `shared-producer`, naming the command and both steps.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: add R5.
  - Traces to: FR-3; AC-3. Depends on: T5.6.

- [x] **TEST + IMPLEMENT: T5.8 — R6: a step with an empty Kind cell is invalid**
  - **USE COMMAND**: none — RED-first row `definition-r06-missing-kind`.
  - Fixture: `flows-rules/r06-missing-kind.flow`.
  - Test should verify: R6 `missing-kind`, `Step {step} declares no artefact kind.` (not R0).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: add R6.
  - Traces to: FR-3; AC-53. Depends on: T5.7.

- [x] **TEST + IMPLEMENT: T5.9 — R7: a known-kind step must name its kind's artefact**
  - **USE COMMAND**: none — RED-first row `definition-r07-known-kind-artefact`.
  - Fixture: `flows-rules/r07-known-kind-artefact.flow`, a `tasks`-kind step whose artefact is not `tasks.md` (AC-53). This also raises R13, so the expectation lists R7 then R13 (S8).
  - Test should verify: R7, `Step {step} is of kind tasks but does not name tasks.md.`
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: add R7.
  - Traces to: FR-3; AC-53. Depends on: T5.8.

- [x] **TEST + IMPLEMENT: T5.10 — R8: two steps of one known kind are invalid**
  - **USE COMMAND**: none — RED-first row `definition-r08-duplicate-known-kind`.
  - Fixture: `flows-rules/r08-duplicate-known-kind.flow`, with two `requirements` steps.
  - Test should verify: R8, `More than one step is of kind requirements ({steps}).`
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: add R8.
  - Traces to: FR-3; AC-53. Depends on: T5.9.

- [x] **TEST + IMPLEMENT: T5.11 — R9: a command that approves step N and produces a step other than N+1 is invalid**
  - **USE COMMAND**: none — RED-first row `definition-r09-gate-not-adjacent`.
  - Fixture: `flows-rules/r09-gate-not-adjacent.flow`, with the marker-writer of step 1 named as the producer of step 3 (AC-53).
  - Test should verify: R9, `{command} approves step {n} but produces step {m}, which does not immediately follow it.`
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: add R9.
  - Traces to: FR-3, FR-27 (structural); AC-53. Depends on: T5.10.

- [x] **TEST + IMPLEMENT: T5.12 — R10: a step that produces and approves itself with no gate is invalid**
  - **USE COMMAND**: none — RED-first rows `definition-r10-self-approval` and `definition-r10-exemption`.
  - Fixture:
    - `flows-rules/r10-self-approval.flow`, whose first step names one command as both producer and marker-writer (AC-72).
    - `flows-rules/r10-exemption.flow`, valid (it passes on landing, so it is characterised, not RED): the second step's producer and marker-writer are one command, which is also the first step's marker-writer.
  - Test should verify: R10, `Step {step} is produced and approved by {command} with no gate before it.` For `r10-exemption`: `valid: true`, no violations.
  - **Characterise** `definition-r10-exemption`: named mutation: implement R10 without the previous-marker-writer exemption; the row must FAIL. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: add R10. It is satisfied only when the command is also the previous step's marker-writer.
  - Traces to: FR-3, FR-27; AC-72; FR-24. Depends on: T5.11.

- [x] **TEST + IMPLEMENT: T5.13 — R11: a table row after the hand-off is invalid, and does not join `steps`**
  - **USE COMMAND**: none — RED-first row `definition-r11-step-after-hand-off`.
  - Fixture: `flows-rules/r11-step-after-hand-off.flow`.
  - Test should verify: R11 only, `A step follows the hand-off at line {n}.` The late row does not feed R2, R5 or R13.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: add R11.
  - Traces to: FR-3; FR-24. Depends on: T5.12.

- [x] **TEST + IMPLEMENT: T5.14 — R12: a kind with no entry, a duplicate entry, or an unreadable or unresolvable table makes every slip resolved against it invalid, with violations in rule-then-slip order**
  - **USE COMMAND**: none — RED-first rows `definition-r12-approval-entry` (`flows-rules`), `definition-feature-duplicate-entry` (`flows: "flows-duplicate-entry"`) and `definition-feature-invalid` (`flows: "flows-invalid"`).
  - Fixture (new):
    - `flows-rules/r12-approval-entry.flow`, with a step of a kind that `flows-rules/approvals.md` lacks.
    - `flows-duplicate-entry/` (a `feature.flow` copy, and `approvals.md` with two `tasks` rows).
    - `flows-invalid/` (`feature.flow` naming `/spec:designx`, and `approvals.md` with a duplicate `tasks` row and a missing command).
    - `flows-spike-entry/approvals.md`: the primary's rows plus `| spike | (empty) |`. It is used in T6.7.
  - Test should verify:
    - R12 (a) names the step and the table.
    - R12 (b) names the kind.
    - `flows-invalid` lists R4, R12 (b) and R12 (c), in that order (rule id, then slip order).
    - `definition-feature-shipped` (no `flows` key): the shipped `feature.flow` and `approvals.md` are valid, with R12 (c) resolving the four `/adr:*` commands.
  - **Characterise** `definition-feature-shipped` (a green control row): named mutation: make R12 (c) reject every command; the row must FAIL. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: add `ApprovalTableReader`, then R12 (a), (b) and (c), and evaluate (b) and (c) even when R0 fires.
  - Traces to: FR-3, FR-29; AC-74 (definition side); FR-24 (two entries). Depends on: T5.13.

- [x] **TEST + IMPLEMENT: T5.15 — R13: the last step must be kind `tasks`, with artefact `tasks.md` and marker `.tasks-approved`**
  - **USE COMMAND**: none — RED-first row `definition-r13-last-not-tasks`.
  - Fixture: `flows-rules/r13-last-not-tasks.flow`.
  - Test should verify: R13 with 0089's message.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: add R13.
  - Traces to: FR-3; AC-3; FR-24. Depends on: T5.14.

- [x] **TEST + IMPLEMENT: T5.16 — R14: a hand-off other than `/spec:implement` is invalid**
  - **USE COMMAND**: none — RED-first row `definition-r14-hand-off-not-implement`.
  - Fixture: `flows-rules/r14-hand-off-not-implement.flow`.
  - Test should verify: R14, `The hand-off is {command}, not /spec:implement.`
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: add R14.
  - Traces to: FR-3; AC-3. Depends on: T5.15.

- [x] **TEST + IMPLEMENT: T5.17 — R15: a detour with an unknown command, or with an unknown attach point or return point, is invalid, naming the detour**
  - **USE COMMAND**: none — RED-first rows `definition-r15-detour-command` and `definition-r15-detour-return`.
  - Fixture: `flows-rules/r15-detour-command.flow` and `r15-detour-return.flow`.
  - Test should verify: R15, `Detour {command} at line {n}: {which part} is unknown.`
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: parse `detour:` lines in file order, and add R15.
  - Traces to: FR-3, FR-30; AC-85. Depends on: T5.16.

- [x] **TEST + IMPLEMENT: T5.18 — R16: a definition whose declared name differs from its filename stem is invalid**
  - **USE COMMAND**: none — RED-first row `definition-r16-name-mismatch`.
  - Fixture: `flows-rules/r16-name-mismatch.flow`, which declares another name. It is the only rule fixture that does not declare its own stem.
  - Test should verify: R16, `{file} declares flow {name}, which differs from its filename.`
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: add R16, for definitions and for `feature.flow` when a legacy spec resolves against it.
  - Traces to: FR-1, FR-3; AC-3. Depends on: T5.17.

## Phase 6 — One-spec resolution

*Shared rule from Phase 5: a task that adds a rule or ladder row may append its effect to earlier expectations. T6.1 also adds a row for R16 on a legacy spec: `legacy-nothing` against a flows directory whose `feature.flow` declares another name. 0092 step 4 with 0089 step 5. Each case is a tracked directory under `.claude/test-fixtures/routing/specs/{case}/`, holding a `README.md` that names its purpose. Each expectation is `expect/{case}.json` (or `expect/{case}-{flows}.json` (`{flows}` without its `flows-` prefix) when the same case runs against more than one flows directory, as for `spike-step-spike-entry`; `shipped-legacy-nothing` is the shipped-table row of `legacy-nothing`) with `run: ["--spec", "{case}"]`, added red before the code (except characterised control rows). Records follow S3. **T6.6, T6.8 and T6.9 carry the O-1 fixtures.** Depends on Phase 5.*

- [x] **TEST + IMPLEMENT: T6.1 — A legacy spec with nothing resolves to `feature` from the default source, with every route field FR-6 lists; an unknown `--spec` exits `1`**
  - **USE COMMAND**: none — RED-first rows `legacy-nothing` (`flows: "flows"`), `shipped-legacy-nothing` (no `flows` key), and `spec-unknown` (`run: ["--spec", "no-such-case"]`, `exit: 1`).
  - Fixture: `specs/legacy-nothing/`, holding `README.md` only.
  - Test should verify:
    - `spec` is `.claude/test-fixtures/routing/specs/legacy-nothing`, with forward slashes. `flow` is `{feature, default, true}`, and `valid` is true.
    - `tampered` is `{false, null, null}`, and `route_change` is `null`.
    - `steps` holds three entries, with producers, marker-writers, `artefacts_present: false`, `approved: false`, `gate: null`, and preconditions. `requirements` holds. `design` fails with ``…`/spec:design` needs step `requirements` approved — run `/spec:approve requirements`.`` (AC-11, resolver side).
    - `known_kinds` holds all three. `gates`, `stray_markers` and `out_of_order` are empty.
    - `next` is `{step: "requirements", kind: "requirements", commands: ["/spec:requirements"], unattended_driver: null, detours: []}`.
    - `hand_off.precondition` fails, listing all three steps.
    - `approve_steps` is `[requirements, design, tasks]`.
    - `shipped-legacy-nothing`: `approval_subflow` is `[]`, then the four `/adr:*` commands in order, then `[]` (AC-75).
    - `spec-unknown`: exit `1` with empty stdout.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `--spec`. Spec directories are the immediate non-dot subdirectories, matched by ordinal comparison. Strip a trailing `/` from the root.
    - Add `SpecStateReader`: markers by `File.Exists`, and `.adr-list` present only when it holds a non-blank line.
    - Add `RouteResolver`: next-step ladder row 3 only, preconditions and their messages (including the hand-off precondition's list of unapproved steps and its AC-65 message), `known_kinds`, `approve_steps` and `approval_subflow`. Rows 2, 4, 1 and 5 arrive in T6.3, T6.4, T6.5 and T6.6.
    - Add a `RouteRecordInspector` stub returning `{false, null, null}` and `null`, as 0089 step 7 says until 0091.
  - Traces to: 0089 step 5, *The route report*, *The next-step ladder*; FR-4, FR-5, FR-6, FR-8, FR-11, FR-29; AC-11 (resolver side), AC-75. Depends on: T5.18.

- [x] **TEST + IMPLEMENT: T6.2 — A marker counts as approved whatever it holds, and preconditions and the hand-off list the unapproved steps in slip order**
  - **USE COMMAND**: none — rows `legacy-requirements-approved` and `legacy-design-approved`, which pass on landing because T6.1 already reads markers by existence. **Characterise** them (see below).
  - Fixture:
    - `legacy-requirements-approved/`: `requirements.md` and an empty `.requirements-approved`.
    - `legacy-design-approved/`: also `.adr-list` naming an ADR, and a free-text `.design-approved`.
  - Test should verify:
    - The first case: `next.commands` is `["/spec:design"]` (AC-20, resolver side), and `hand_off.precondition.unapproved` is `[design, tasks]` in that order, with 0089's message (AC-65).
    - The second case: `requirements` and `design` are both approved (AC-5), the `tasks` precondition holds, and `next` is `["/spec:tasks"]`.
  - **Characterise**: named mutation: in `SpecStateReader`, treat a zero-byte marker as not approved. `legacy-requirements-approved` must FAIL (AC-5). Revert. T6.1 owns the hand-off message (AC-65); this task adds no code.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: none, because a marker's content is never read.
  - Traces to: FR-5, FR-8, FR-9, FR-11; AC-5, AC-20, AC-65. Depends on: T6.1.

- [x] **TEST + IMPLEMENT: T6.3 — With every step approved, the next step is the hand-off, with the unattended driver and no detour on `feature`**
  - **USE COMMAND**: none — RED-first row `legacy-all-approved`.
  - Fixture: `legacy-all-approved/`, with all three artefacts, all three markers, and `tasks.md` all checked.
  - Test should verify: `next` is `{step: null, kind: null, commands: ["/spec:implement"], unattended_driver: "/spec:ralph-implement", detours: []}`, and `hand_off.precondition.holds` is true (AC-84, `feature` half; FR-11).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: add ladder row 2.
  - Traces to: FR-9, FR-11, FR-30; AC-84. Depends on: T6.2.

- [x] **TEST + IMPLEMENT: T6.4 — An out-of-order approval is reported, and next, the preconditions and the hand-off each use only their own rule**
  - **USE COMMAND**: none — RED-first rows `legacy-out-of-order` and `legacy-tasks-approved-only`.
  - Fixture:
    - `legacy-out-of-order/`, holding `requirements.md` and `.design-approved`.
    - `legacy-tasks-approved-only/`, holding `requirements.md`, `.requirements-approved`, `design.md`, `.adr-list`, `tasks.md` and `.tasks-approved` (no `.design-approved`).
  - Test should verify:
    - `out_of_order` is `[{approved: "design", earlier_unapproved: ["requirements"]}]`.
    - `next.commands` is `["/spec:review requirements", "/spec:approve requirements"]` (AC-22, AC-43).
    - The `tasks` precondition holds, and the hand-off fails (AC-43).
    - `legacy-tasks-approved-only`: `out_of_order` is `[{approved: "tasks", earlier_unapproved: ["design"]}]`, and `approve_steps` still lists `tasks`, so `/spec:approve tasks` meets no new precondition (AC-13, resolver side).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: compute `out_of_order`, and add next-step ladder row 4 (`/spec:review {kind}`, then the marker-writer).
  - Traces to: FR-21, FR-11, FR-8; AC-13, AC-22, AC-43. Depends on: T6.3.

- [x] **TEST + IMPLEMENT: T6.5 — A spec's own `.flow` is followed: valid when it matches, invalid when it names a since-deleted command, and followed with a notice when its flow has no definition**
  - **USE COMMAND**: none — RED-first rows `feature-slip`, `copy-invalid` and `unknown-flow`, each with `flows: "flows"`.
  - Fixture:
    - `feature-slip/`: a `.flow` copied from the fixture `feature.flow`, a record (S3), and `.requirements-approved`.
    - `copy-invalid/`: a hand-written `.flow` naming `/spec:designx`, and a record (S3).
    - `unknown-flow/`: a hand-written `# flow: retired` slip (otherwise `feature`'s steps), and a record.
  - Test should verify:
    - `feature-slip`: `flow.source` is `slip`, valid, and `route_change` is `null`.
    - `copy-invalid`: `valid: false`, with R4 naming `/spec:designx` (AC-70, resolver side), and `next` is `{step: null, kind: null, commands: [], unattended_driver: null, detours: []}` (ladder row 1). Its `stop` is asserted in T8.2.
    - `unknown-flow`: `flow: {retired, slip, definition_present: false}`, `valid: true`, and the copy's steps are followed (AC-44).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: validate the spec's copy with R1–R15, and R0. R16 applies to definitions only. Add ladder row 1: an invalid slip has an empty `next`.
  - Traces to: FR-22, FR-3, FR-6; AC-44, AC-70. Depends on: T6.4.

- [x] **TEST + IMPLEMENT: T6.6 — A flow-specific kind is valid with an empty table entry, its next step is its marker-writer alone, and a stray intent marker is reported without changing any decision**
  - **USE COMMAND**: none — RED-first row `evaluation-ready` (`flows: "flows"`; the shipped table has no `evaluation` entry).
  - Fixture: `evaluation-ready/`.
    - A hand-written slip: an `evaluation` step (kind `evaluation`, producer `/refactor:smells`, artefact `evaluation.md`, marker `.evaluation-approved`, marker-writer `/spec:approve evaluation`), then a `tasks` step.
    - `evaluation.md` is present.
    - **Per O-1:** also a stray `.requirements-approved`, which is an intent marker through the primary `feature.flow` copy.
  - Test should verify:
    - `valid: true`. The step's kind is `evaluation` with `known_kind: false`.
    - `next.commands` is `["/spec:approve evaluation"]`, with no `/spec:review` (ladder row 5).
    - `known_kinds.requirements` and `known_kinds.design` are `null` (AC-54).
    - `approve_steps` is `[evaluation, tasks]`.
    - `stray_markers` is `[".requirements-approved"]`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add ladder row 5.
    - Build the intent-marker set from every `*.flow` in the flows directory that passes R0.
    - Never report ignored markers.
  - Traces to: FR-3, FR-10, FR-11, FR-21, FR-29; AC-54, AC-30 (logic). Depends on: T6.5.

- [x] **TEST + IMPLEMENT: T6.7 — Faults in the approval table make a spec's slip invalid, and give `approval_subflow: null` to the steps of the faulty kind**
  - **USE COMMAND**: none — RED-first rows `spike-step` (`flows`) and `legacy-nothing-duplicate-entry` (`flows: "flows-duplicate-entry"`), plus the green control row `spike-step-spike-entry` (`flows: "flows-spike-entry"`). **Characterise** the control: named mutation: make R12 (a) ignore the table's entries; the control row must FAIL. Revert.
  - Fixture: `spike-step/`, a hand-written slip with a `spike`-kind step, then `tasks`, and its record. The slip content is extended in T6.8 per O-1.
  - Test should verify:
    - On `flows`: R12 (a) names step `spike` and the missing entry, and that step has `approval_subflow: null` (AC-74).
    - On `flows-spike-entry`: valid (AC-74).
    - `legacy-nothing` on `flows-duplicate-entry`: R12 (b), and the `tasks` step has `approval_subflow: null`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: set `approval_subflow` to `null` only for the affected kind, or for every step when the table is missing or malformed.
  - Traces to: FR-3, FR-29; AC-74; FR-24. Depends on: T6.6.

- [x] **TEST + IMPLEMENT: T6.8 — A gate is reported in `gates` and on the step it produces, with `previous_approved` and `also_approves_produced`**
  - **USE COMMAND**: none — RED-first rows `spike-step-spike-entry` (extended), `spike-gate-also` and `spike-gate-also-approved` (both `flows: "flows-spike-entry"`).
  - Fixture (per O-1):
    - Change `spike-step/.flow` so that: `spike` is produced by `/refactor:smells` and approved by `/refactor:tidy-first`; `tasks` is produced by `/refactor:tidy-first` and approved by `/spec:approve tasks`; re-write the record (S3).
    - New `spike-gate-also/`: a hand-written slip in which `spike` is produced by `/refactor:smells` and approved by `/refactor:tidy-first`, and `tasks` is produced by `/refactor:tidy-first` and approved by `/refactor:tidy-first` (the R10 exemption), with its record (S3).
    - New `spike-gate-also-approved/`: the same slip plus `spike.md`, `.spike-approved` and `tasks.md`.
  - Test should verify:
    - `gates` is `[{command: "/refactor:tidy-first", approves: "spike", produces: "tasks", also_approves_produced: false}]`.
    - `steps[tasks].gate` is `{command, approves: "spike", previous_approved: false, also_approves_produced: false}`.
    - Still valid under R9 and R10.
    - `spike-gate-also`: valid (R10 exemption); `gates` is `[{command: "/refactor:tidy-first", approves: "spike", produces: "tasks", also_approves_produced: true}]` (AC-55); `approve_steps` is `[]` (AC-58); `known_kinds.tasks` is `{step: "tasks", producer: "/refactor:tidy-first", …}` (AC-61).
    - `spike-gate-also-approved`: `steps[tasks].gate.previous_approved` is true (AC-73).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: derive gates from the slip (no declaration is needed, FR-27), compute `also_approves_produced` and `previous_approved`, list in `approve_steps` only the steps whose marker-writer is `/spec:approve`, and take `known_kinds.{kind}.producer` from the slip.
  - Traces to: FR-6 (gate reporting), FR-27; AC-55, AC-58, AC-61, AC-73 (resolver side); 0089 step 5 (gates). Depends on: T6.7.

- [x] **TEST + IMPLEMENT: T6.9 — Detours attached to the next step, or to the hand-off, are reported in file order with their return points**
  - **USE COMMAND**: none — RED-first rows `legacy-all-approved-edited` (`flows: "flows-edited"`), and `spike-step-spike-entry` extended.
  - Fixture (per O-1):
    - Bring forward `flows-edited/` from 0092 step 7: `feature.flow` plus `detour: /refactor:smells @ hand-off -> hand-off`, and `approvals.md` as in the primary.
    - Add `detour: /refactor:tidy-comments @ spike -> spike` to `spike-step/.flow`, and re-write its record.
  - Test should verify:
    - `legacy-all-approved` on `flows-edited`: `next.detours` is `[{command: "/refactor:smells", return_to: "hand-off"}]`.
    - `spike-step-spike-entry`: `next.detours` holds the `spike` detour.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: fill `next.detours` in rows 2–5, and leave it empty in row 1 (asserted in T6.5).
  - Traces to: FR-30, FR-11; AC-84 (logic). Depends on: T6.8.

- [x] **TEST + IMPLEMENT: T6.10 — The repository's unusual legacy specs 0036, 0031 and 0028 each resolve to the hand-off, with `.code-approved` ignored**
  - **USE COMMAND**: `/tdd:test-first when the resolver resolves specs 0036 0031 and 0028 from the repository specs root it should report feature default valid with the hand-off and no stray marker`
  - Test file: `resolve_route_tests.cs`, existing-specs check in `--spec` form, against the root `specs/` and the shipped flows.
  - Test should verify: for each of the three: `feature`, `default`, valid, `stray_markers: []`, and `next.commands` `["/spec:implement"]` (AC-4 subset, AC-71).
  - **Characterise**: this passes on arrival. Apply these named mutations in turn:
    - (m1) In `RouteResolver`, choose the next step as "first step whose artefacts are missing" instead of "first unapproved step". 0031 (no `.adr-list`) and 0036 (no `tasks.md`) must FAIL.
    - (m2) In `SpecStateReader`, add every `.*-approved` file to `stray_markers`. 0028 must FAIL.
    - Revert both.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: add only the harness check. No resolver change.
  - Traces to: 0089 step 5 (run against legacy specs); 0092 step 4; FR-5, FR-21, FR-24; AC-4, AC-71. Depends on: T6.9.

## Phase 7 — Fixture-only flow experiments

*0092 step 5. Each task is one fixture-only commit. **Contingency for both tasks:**
- If any row fails, first check the expectation itself (for example a missing `flows: "flows"` key); an expectation written wrongly is fixed before the fixture commit and is not a resolver fault. If a correctly written row still fails, stop. Do not change the fixtures to make them pass.
- Add a new `TEST + IMPLEMENT` task before this one. Its RED is the failing row, run against the uncommitted fixture. It fixes `resolve_route.cs` in its own `feat:` commit.
- Then make the fixture commit again on top.
- Record in the commit message and in the PR that the FR-25 or FR-28 experiment needed a resolver edit.

Depends on Phase 6.*

- [x] **TEST + IMPLEMENT: T7.1 — Adding `light` takes one definition and its fixtures, and every `light` assertion of FR-25 passes with no change under `.claude/commands/`**
  - **USE COMMAND**: none — RED-first is not available, because the rows are expected green. **Characterise** with these named mutations to `resolve_route.cs` (never committed):
    - (m1) Make the first step's precondition require a `requirements`-kind step approved. `light-nothing`'s `design` precondition must FAIL.
    - (m2) Drop hand-off detours from `next`. `light-done` must FAIL.
    - Revert. `git status` must show `resolve_route.cs` unmodified before committing.
  - Fixture (new):
    - `flows/light.flow`: FR-2's slip plus `detour: /refactor:smells @ hand-off -> hand-off`.
    - Cases with `.flow` and record (S3): `light-nothing`; `light-adr-list` (`.adr-list` naming `0092-routing-fixtures-and-test-scripts.md`); `light-design-approved` (plus `.design-approved`); `light-done` (plus `tasks.md` all checked, `.tasks-approved`).
    - Expectations: one per case, plus `definition-light`, each with `flows: "flows"` (without it the harness uses the shipped directory, which has no `light` definition).
  - Test should verify:
    - `light-nothing`: flow `light`; steps `[design, tasks]`; next `/spec:design`; `design` precondition holds; `known_kinds.requirements` null (AC-12, resolver side); `approve_steps` `[design, tasks]` (AC-23, AC-66, resolver side).
    - `light-adr-list`: next is `/spec:review design`, then `/spec:approve design`, with `next.kind` `design` (AC-18, resolver side).
    - `light-design-approved`: next is `/spec:tasks` (AC-21); `tasks` precondition holds; `known_kinds.requirements` is null; the hand-off fails naming `tasks` and `/spec:approve tasks` (AC-8).
    - `light-done`: the hand-off holds (AC-15); next is `/spec:implement`, with driver `/spec:ralph-implement` and detour `/refactor:smells` returning to `hand-off` (AC-84).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: none. This is fixture-only. `git diff --stat HEAD~1 -- .claude/commands/` is empty after the commit (AC-48).
  - Traces to: 0092 step 5; FR-25, FR-2, FR-30; AC-8, AC-12, AC-15, AC-18, AC-21, AC-23, AC-48, AC-66, AC-84. Depends on: T6.10.

- [x] **TEST + IMPLEMENT: T7.2 — Adding `bugfix` takes one definition, one empty `diagnosis` entry and its fixtures, and every FR-28 and gate-state assertion passes with no change under `.claude/commands/` or `bugfixes/`**
  - **USE COMMAND**: none — **Characterise** with these named mutations to `resolve_route.cs` (never committed):
    - (m1) Treat every kind as reviewable in ladder row 4. `bugfix-diagnosed` must FAIL with `/spec:review diagnosis`.
    - (m2) Compute `also_approves_produced` as always false. `bugfix-nothing`'s `gates` must FAIL.
    - Revert.
  - Fixture (new):
    - `flows/bugfix.flow`: FR-28's table.
    - A `| diagnosis | (empty) |` row in `flows/approvals.md` and in `flows-spike-entry/approvals.md` (the latter kept in step with the primary).
    - Cases: `bugfix-nothing`, `bugfix-diagnosed` (`bugfix.md`), `bugfix-leftover-tasks` (`bugfix.md`, `tasks.md`), `bugfix-confirmed` (`bugfix.md`, `.confirm-approved`), `bugfix-tasks-written` (also `tasks.md`), `bugfix-tasks-approved-only` (`bugfix.md`, `tasks.md`, `.tasks-approved`), and `bugfix-done` (both markers, `tasks.md` all checked).
    - `legacy-stray-ignored`: all three `feature` markers, plus `.code-approved` and `.confirm-approved`.
    - Expectations: one per case, plus `definition-bugfix`, each with `flows: "flows"`.
    - Each `bugfix-*` case holds a `.flow` copied from `flows/bugfix.flow` and a record (S3). `legacy-stray-ignored` holds no `.flow`.
  - Test should verify:
    - `definition-bugfix` and `bugfix-nothing`: valid; `diagnosis` is flow-specific with `approval_subflow: []` (AC-78); `gates` is `[{"/bugfix:confirm", approves: "diagnosis", produces: "tasks", also_approves_produced: true}]` (AC-55); `known_kinds` has `requirements` and `design` null and `tasks` = `{step: "tasks", producer: "/bugfix:confirm", …}` (AC-61); `approve_steps` is `[]`, so `/spec:approve tasks` meets another marker-writer (AC-58); next is `/bugfix:triage`.
    - `bugfix-diagnosed`: next is `["/bugfix:confirm"]` (AC-59); the `tasks` precondition fails naming `diagnosis` (AC-56, before).
    - `bugfix-leftover-tasks`: next is `/bugfix:confirm`, and `steps[tasks].artefacts_present` is true (AC-89).
    - `bugfix-confirmed`: next is `/bugfix:confirm` (AC-57), and the `tasks` precondition holds (AC-56, after).
    - `bugfix-tasks-written`: next is `["/spec:review tasks", "/bugfix:confirm"]`, and `steps[tasks].gate.previous_approved` is true (AC-73).
    - `bugfix-tasks-approved-only`: `out_of_order` is reported; the hand-off fails naming `diagnosis` (AC-60).
    - `bugfix-done`: the hand-off holds, and no `requirements` or `design` step is reported (AC-60; AC-83, resolver side).
    - `legacy-stray-ignored`: `stray_markers` is `[".confirm-approved"]`, and `.code-approved` is not reported.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: none. This is fixture-only. `git diff --stat HEAD~1 -- .claude/commands/ bugfixes/` is empty (AC-63).
  - Traces to: 0092 step 5, *Coverage* (0090's ladder rows 1–5); FR-27, FR-28, FR-21, FR-29; AC-55 to AC-61, AC-63, AC-73, AC-78, AC-83, AC-89. Depends on: T7.1.

## Phase 8 — Current spec, the stop ladder, and every spec

*0092 step 6 with 0089 step 6. Depends on Phase 7.*

- [x] **TEST + IMPLEMENT: T8.1 — `--current` classifies `.current-spec` as missing, empty or stale, stops with FR-20's recoveries, and otherwise resolves the named spec**
  - **USE COMMAND**: `/tdd:test-first when the resolver runs --current with specs/.current-spec missing zero-byte whitespace-only naming 0099-gone or naming a valid spec with a trailing newline it should report current-spec-missing empty empty stale or ok`
  - Test file: `resolve_route_tests.cs` generated `.current-spec` roots, under `Directory.CreateTempSubdirectory()`, deleted in a `finally`.
  - Test should verify:
    - The no-spec form carries only `schema_version`, `mode`, `current_spec`, `spec: null` and `stop`.
    - `stop.reason` takes the three state names. The message names `/spec:switch` and `/spec:new`, and for stale also the value `0099-gone`, using `show-me.md:51-56`'s state words.
    - The `ok` root resolves the named spec, with `current_spec {ok, value}` (AC-42; AC-39 and AC-82, state side).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: add `CurrentSpecLocator` and stop-ladder rows 1–3. The stale check is an ordinal comparison against the directory list.
  - Traces to: 0089 step 6, *The stop ladder*; FR-20, FR-6; AC-39, AC-42, AC-82. Depends on: T7.2.

- [x] **TEST + IMPLEMENT: T8.2 — An invalid slip sets `stop` to `invalid`, with the recovery that fits where the fault lies**
  - **USE COMMAND**: none — RED-first rows: extend `copy-invalid` and `definition-r04-unknown-command`; add `legacy-nothing-invalid` (`flows: "flows-invalid"`).
  - Test should verify:
    - `copy-invalid`: `stop.reason` is `invalid`, and the message quotes the R4 violation and names the re-copy with the placeholder `{route-change command}` and a reason (AC-70, resolver side).
    - Flow form (`r04`): the only recovery is to fix or add the definition.
    - A legacy spec on `flows-invalid`: fix `feature.flow` or `approvals.md`, with no re-copy.
    - `violations` stay in full in each report.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: add stop row 5 in spec, current and flow forms. The variant where `flow.name` is null has no listed case: implement it from 0089's text, and note the gap in the commit.
  - Traces to: 0089 *The stop ladder* row 5; FR-7 (stop), FR-22; AC-10 and AC-70 (resolver side). Depends on: T8.1.

- [x] **TEST + IMPLEMENT: T8.3 — With no `.claude/commands` tree in the working directory, the resolver exits `1` with empty stdout**
  - **USE COMMAND**: `/tdd:test-first when the resolver is started by absolute script path from a temporary working directory with no .claude/commands it should exit 1 and print no record line`
  - Test file: `resolve_route_tests.cs`, the generated no-command-tree case.
  - Test should verify: exit `1`, empty stdout, no `route-report:` line.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: treat a missing `.claude/commands` as a tooling fault.
  - Traces to: 0089 *The resolver's command line*; 0092 *Generated cases*. Depends on: T8.2.

- [x] **TEST + IMPLEMENT: T8.4 — `--all` prints one `route-current:` line and one small summary line per spec, and every legacy spec under `specs/` resolves to the step the feature oracle computes**
  - **USE COMMAND**: `/tdd:test-first when the resolver runs --all over the repository specs root it should print one route-current line and one summary line per directory whose next step matches the feature oracle for every spec without a .flow`
  - Test file: `resolve_route_tests.cs`, existing-specs check moved to `--all`, plus the feature oracle.
  - Test should verify:
    - The `route-current:` line is `{schema_version, mode: "all", current_spec, specs: count, shared_violations}`.
    - Each summary line carries `spec`, `flow`, `valid`, `steps` (`{name, kind, state}`, where `state` is one of `pending`, `ready`, `approved`, `approved-no-artefacts`; 0036's `tasks` is `approved-no-artefacts`) and `next`. Optional fields appear only when they differ from their default.
    - For every directory without `.flow`: `feature`, `default`, valid, with no `tampered`, `stray_markers` or `violations` key, and `next` equal to the oracle.
    - 0036, 0031 and 0028 are pinned to the hand-off by name. `0021-Expose Unacceptable Message Window` is listed.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: resolve every spec in one process (NFR-4). Order lines by ordinal directory name.
  - Traces to: 0089 step 6, *The route report* (`--all`); 0092 step 6, *Repository checks*; FR-15, FR-24, FR-5; NFR-4, NFR-6; AC-4, AC-29 (resolver side). Depends on: T8.3.

- [x] **TEST + IMPLEMENT: T8.5 — A shared violation (R12, or `feature.flow` for a legacy spec) is reported once on the `route-current:` line and on no summary line**
  - **USE COMMAND**: `/tdd:test-first when the resolver runs --all over specs with --flows-dir flows-invalid it should list each shared violation once in shared_violations and omit it from every summary line`
  - Test file: `resolve_route_tests.cs` (`--all` over `specs/` with `flows-invalid`).
  - Test should verify:
    - `shared_violations` holds each distinct full record once.
    - Every summary line has `valid: false` and no shared rule in `violations`.
    - A fault copied into a spec's own `.flow` stays on that spec's line: `copy-invalid`, in `--all` over the fixture root.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: dedupe shared violations, and abbreviate the line's `violations` to `{rule, code, step}` and `stop` to `{reason}`.
  - Traces to: 0089 *The route report* (shared violations); FR-15. *(This assertion is explicit here; 0092 implies it only through the flows-invalid size run — see the scope-creep check.)* Depends on: T8.4.

- [x] **TEST + IMPLEMENT: T8.6 — `--all` output stays inside the shell tool's budget: no summary line over 650 characters, and the whole output under 24,000, for healthy and for all-invalid inputs**
  - **USE COMMAND**: `/tdd:test-first when the resolver runs --all over specs with the shipped flows and with flows-invalid it should print no summary line over 650 characters and under 24000 characters in total`
  - Test file: `resolve_route_tests.cs`, size checks:
    - both limits over `specs/` with `.claude/flows` and with `flows-invalid`;
    - the line limit only over `{fixture root}/specs` with `flows`.
  - Test should verify: the FAIL message gives the measured size and says that the remedy is undecided.
  - **Characterise**: named mutation: in the summary writer, emit every optional field with its default value. At least one limit must FAIL. If neither fails, stop and ask. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: add the checks only. **If the healthy run fails the total (risk K1), stop and ask.** Do not page, truncate or drop specs.
  - Traces to: 0089 *The route report* (size), Risks; 0092 step 6, *Repository checks* (size). Depends on: T8.5.

- [x] **TEST + IMPLEMENT: T8.7 — The `--all` run over `specs/` is timed after the build, and a run of 10 seconds or more prints a `WARN` with the duration without failing**
  - **USE COMMAND**: `/tdd:test-first when the timed --all run over specs takes ten seconds or longer it should print a WARN line with the duration and not change the exit status`
  - Test file: `resolve_route_tests.cs`, timing check, run after the pre-check has built the script.
  - **Characterise**: named mutation: `Thread.Sleep(10_500)` at the start of `--all` in `resolve_route.cs`. A `WARN` appears and the exit status is unchanged. Revert, and record the real duration in the commit message.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: use a `Stopwatch` around the existing-specs `--all` run.
  - Traces to: NFR-4; AC-51 (a human reads the WARN: T17.4); 0092 step 6. Depends on: T8.6.

- [x] **TEST + IMPLEMENT: T8.8 — The shipped flow directory holds `feature.flow` and `approvals.md`, and no `light` or `bugfix` definition**
  - **USE COMMAND**: `/tdd:test-first when the shipped .claude/flows directory is listed it should hold feature.flow and approvals.md and no light.flow or bugfix.flow`
  - Test file: `resolve_route_tests.cs`, AC-1 check.
  - **Characterise**: named mutation: copy `light.flow` from the fixture `flows/` into `.claude/flows/`. The check must FAIL. Delete the copy, and confirm `git status` shows nothing under `.claude/` (the untracked `specs/0040-routing-slips/baselines.md` is expected).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: allow other shipped flows.
  - Traces to: FR-1; AC-1; 0092 *Repository checks*. Depends on: T8.7.

## Phase 9 — Creation records and tamper detection

*0092 step 7 with 0091 step 1 (= 0089 step 7). Depends on Phase 8.*

- [x] **TEST + IMPLEMENT: T9.1 — Every tracked `.flow.record` outside the named tamper cases holds the SHA-256 of its case's normalised `.flow` and its flow name**
  - **USE COMMAND**: `/tdd:test-first when a tracked fixture .flow.record does not match the SHA-256 of its case's CRLF-normalised .flow or line 1's flow name it should FAIL naming the case and telling the author to re-run the installation steps`
  - Test file: `resolve_route_tests.cs`, record integrity check; it exempts `tamper-a-route-changed` and `tamper-b-record-only`.
  - **Characterise**: named mutation: change one character of `specs/feature-slip/.flow` in the working tree. The check must FAIL naming `feature-slip`. Restore it with `git restore`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: use `SHA256` from the base class library.
  - Traces to: 0092 *Generated cases* (integrity); 0091 *What 0092 needs*; FR-16 (support). Depends on: T8.8.

- [x] **TEST + IMPLEMENT: T9.2 — Tamper case (a): a `.flow` whose hash differs from its record is tampered, reporting the recorded flow**
  - **USE COMMAND**: `/tdd:test-first when a spec's .flow differs from its creation record by one character a trailing space or a BOM the resolver should report tampered case a with the recorded flow`
  - Test file: `resolve_route_tests.cs`, generated `gen-one-char`, `gen-trailing-space` and `gen-bom`. Tracked case `tamper-a-route-changed` with expectation `tamper-a-route-changed`: a non-matching record, nothing approved, and a log whose last entry has a 200-character reason.
  - Test should verify:
    - `tamper-a-route-changed`: `tampered` is `{true, "a", …}` with the recorded flow. Its `route_change` is asserted in T9.8, where the log is first parsed.
    - `gen-one-char`: `{true, "a", "feature"}` (AC-31, resolver side).
    - `gen-trailing-space`: case (a) and valid.
    - `gen-bom`: case (a), and R0 on line 1.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: add `RouteRecordInspector` with record parsing (three patterns, CRLF and trailing whitespace tolerated) and a raw-byte SHA-256 of `.flow`.
  - Traces to: 0091 step 1, *The creation record*, *`RouteRecordInspector`*; FR-16 (a); AC-31. Depends on: T9.1.

- [x] **TEST + IMPLEMENT: T9.3 — A `.flow` that differs from its record only by CRLF line endings is not tampered, and a spec directory name with spaces resolves exactly**
  - **USE COMMAND**: `/tdd:test-first when a spec's .flow differs from its record only by CRLF line endings the resolver should report it not tampered and valid`
  - Test file: `resolve_route_tests.cs`, generated `gen-crlf` and `gen-name with spaces`.
  - Test should verify:
    - `gen-crlf`: `tampered.value` false and valid (AC-32).
    - `gen-name with spaces`: valid, not tampered, and `--spec` and `spec` exact.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: replace each CR LF pair with LF before hashing, and normalise nothing else.
  - Traces to: FR-16 (a, CRLF clause), FR-24; AC-32. Depends on: T9.2.

- [x] **TEST + IMPLEMENT: T9.4 — Tamper case (b): a record with no `.flow` is tampered, resolves like a legacy spec, and reports the recorded flow**
  - **USE COMMAND**: none — RED-first row `tamper-b-record-only`.
  - Fixture: `tamper-b-record-only/`, holding a record with `flow: light` and no `.flow`.
  - Test should verify: `tampered {true, "b", "light"}`; `flow.name` `feature`, source `default`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: add the case (b) row of the inspector table.
  - Traces to: FR-16 (b); AC-33. Depends on: T9.3.

- [x] **TEST + IMPLEMENT: T9.5 — Tamper case (c): a `.flow` with no record, or with an unreadable record, is tampered; a legacy spec is not**
  - **USE COMMAND**: `/tdd:test-first when a spec's .flow has an unreadable creation record the resolver should report tampered case c` for the generated `gen-unreadable-record` (`sha256: nothex`) in the harness. RED-first row for the tracked expectation `tamper-c-no-record` (fixture: `tamper-c-no-record/` holds a `.flow` copied from the fixture `feature.flow` and no record).
  - Test should verify: `{true, "c", null}` for both cases. The `legacy-*` rows still report `{false, null, null}` (AC-33).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: treat an unreadable record as case (c), not as a fault.
  - Traces to: FR-16 (c), FR-5 (legacy not tampered); AC-33. Depends on: T9.4.

- [x] **TEST + IMPLEMENT: T9.6 — Editing a flow definition after a spec started never makes that spec tampered, and the spec follows its own copy**
  - **USE COMMAND**: none — RED-first row `feature-slip-edited` (`flows: "flows-edited"`). `flows-edited/` already exists from T6.9.
  - Test should verify: not tampered; `next.detours` is empty, because the spec's own `.flow` has no detour (AC-34).
  - **Characterise**: named mutation: make the inspector hash `{flows-dir}/{flow}.flow` instead of the spec's `.flow`. The row must FAIL. Revert.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: none, unless the row is red.
  - Traces to: FR-16 (last sentence), FR-22; AC-34. Depends on: T9.5.

- [x] **TEST + IMPLEMENT: T9.7 — A tampered slip stops with the case's recoveries ahead of any invalid stop, and the invalid stop now names `/spec:route`**
  - **USE COMMAND**: `/tdd:test-first when a tampered spec also has an R0 violation the resolver should stop as tampered with the case's recoveries` for the generated `gen-bom` `stop` assertion in the harness. RED-first rows for the tracked expectations: extend `tamper-b-record-only`, `tamper-c-no-record` and `copy-invalid`.
  - Test should verify:
    - `stop.reason` is `tampered`, with 0091's message for each of (a), (b) and (c), including `git restore --source=HEAD -- "{spec}/.flow"` for (a) and (b).
    - `gen-bom`, which has R0 and is also tampered, stops as `tampered`.
    - `copy-invalid`'s message reads `/spec:route feature <reason>`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: fill stop row 4. Replace the `{route-change command}` placeholder in row 5, using `/spec:route <flow> <reason>` when `flow.name` is null.
  - Traces to: 0091 *The tampered stop*; FR-16, FR-7, FR-17 (recovery); AC-31 and AC-80 (resolver side). Depends on: T9.6.

- [x] **TEST + IMPLEMENT: T9.8 — The latest route change is read from the last entry of `route-changes.md`, and a missing or malformed log gives `null`**
  - **USE COMMAND**: `/tdd:test-first when a spec's route-changes log has two entries or a malformed entry the resolver should report the last entry or null` for the generated `gen-log-two` and `gen-log-malformed` in the harness. RED-first row for the tracked expectation `route-changed-light` (`flows: "flows"`).
  - Fixture: `route-changed-light/`.
    - A `light` slip and its record.
    - `route-changes.md` with one entry, `feature -> light`.
    - `.requirements-approved`, `.adr-list` and `.design-approved`.
  - Test should verify:
    - `route-changed-light`: flow `light`; `route_change {date, from: "feature", to: "light", reason}`; `stray_markers` `[".requirements-approved"]`; `design` approved; `tasks` pending; next is `/spec:tasks` (AC-36, AC-90, AC-30 read side).
    - `gen-log-two`: the second entry is reported (AC-81, read side).
    - `gen-log-malformed`: `null`. `feature-slip`, which has no log, still gives `null`.
    - `tamper-a-route-changed` (extend its T9.2 expectation): `route_change` carries the logged entry, with its 200-character `reason` uncut in the full report.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: add 0091's heading and reason patterns. The log never affects `valid`, `tampered` or `stop`.
  - Traces to: 0091 *The route-change log*; FR-17, FR-15, FR-21; AC-30, AC-36, AC-81, AC-90. Depends on: T9.7.

- [x] **TEST + IMPLEMENT: T9.9 — An `--all` summary line carries `tampered` and a route change with its reason cut to 60 characters, and stays within 650 characters for the worst tracked case**
  - **USE COMMAND**: `/tdd:test-first when the all-mode summary line for the tamper-a-route-changed case is measured it should carry tampered without the recorded flow and a route change reason cut to 60 characters within 650 characters` (extends the T8.6 line-limit run over `{fixture root}/specs` in the harness).
  - Test should verify:
    - `tampered {value, case}` without `recorded_flow`.
    - `route_change.reason` is the first 60 characters followed by `…`.
    - Line length ≤650.
    - The measured length is recorded in `tamper-a-route-changed/README.md`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: abbreviate in the summary writer only.
  - Traces to: 0089 *The route report* (summary abbreviations); 0091 *What 0092 needs* (`--all` summary); FR-15. Depends on: T9.8.

## Phase 10 — show-me's route probe

*0092 step 8 with 0089 step 8. Depends on Phase 9.*

- [x] **TEST + IMPLEMENT: T10.0 — The test harness runs `show_me_facts.cs` for a `show_me` expectation, partial-matches its ledger, restores the ledger afterwards, and leaves the ledger out of the read-only hash**
  - **USE COMMAND**: `/tdd:test-first when an expectation names a show_me case the harness should run show_me_facts partial-match the ledger and delete or restore it without failing the read-only hash check`
  - Test file: `resolve_route_tests.cs`. Fixture: `expect/show-me-runner.json` (new): `"show_me": "legacy-all-approved"`, `"ledger": {"schema_version": 1}`.
  - Test should verify:
    - Today the row FAILs, first because `show_me` and `ledger` are unknown top-level keys (T4.1), then, once they are accepted, because the harness has no show-me runner.
    - After the change it passes, and the ledger file `show_me_facts.cs` writes is deleted or restored as `show_me_facts_tests.cs` does.
    - A ledger written under the fixture root does not fail the read-only hash check (NFR-3, 0092 *Repository checks*).
    - A `ledger` key that does not match is a FAIL.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: add `show_me` and `ledger` to the known expectation keys, then the runner, the partial ledger matcher and the hash exclusion, in `resolve_route_tests.cs` (0092 *Expectations*). The runner passes `--flows-dir` only when the expectation has a `flows` key, because `show_me_facts.cs` rejects that option until T10.1; `show-me-runner.json` has none. No change to `show_me_facts.cs`.
  - Traces to: 0092 step 8, *Expectations*, *Repository checks* (read-only, ledgers excluded); FR-23; NFR-3. Depends on: T9.9.

- [x] **TEST + IMPLEMENT: T10.1 — `show_me_facts.cs` takes `--flows-dir`, runs the resolver as a child process after its completeness gate, and records `route` in the ledger; for a legacy spec everything else is unchanged**
  - **USE COMMAND**: none — RED-first row `show-me-legacy-all-approved` (`"show_me": "legacy-all-approved"`, `"flows": "flows"`, partial `"ledger": {"route": {"flow": "feature", "has_requirements_step": true}, "schema_version": 1}`).
  - Test should verify: the row fails today because `show_me_facts.cs` rejects `--flows-dir` and the ledger has no `route` field (the runner exists from T10.0). After the change, `dotnet run .claude/commands/spec/show_me_facts_tests.cs` passes, and `git diff --exit-code $BASE -- .claude/commands/spec/show_me_facts_tests.cs .claude/test-fixtures/show-me/` is clean (AC-46).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add `--flows-dir {path}` to the option loop (`show_me_facts.cs:45-70`). A missing path is a usage error, exit `1`.
    - Add `RouteProbe`: split the target into root and leaf, use `ArgumentList`, take the last `route-report:` line, and run after the gate (`:72-100`).
    - Add the ledger `route {flow, has_requirements_step}`, with `null` plus `null_reasons.route` on R0. `schema_version` stays `1`.
  - Traces to: 0089 step 8, *`show_me_facts.cs` and the route probe*; FR-23; NFR-6; AC-46. Depends on: T10.0.

- [x] **TEST + IMPLEMENT: T10.2 — For a slip with no `requirements`-kind step, show-me does not read `requirements.md`, never reports `declared_total` 0, and gives the route as its null reason**
  - **USE COMMAND**: none — RED-first rows `show-me-light-done` and `show-me-bugfix-done`, each with `flows: "flows"` (the shipped directory has neither definition).
  - Test should verify:
    - `route.has_requirements_step` is false.
    - `requirements` is `{present: false, bytes: null, windows: null}`.
    - `declared_ids`, `declared_total` and `declarations` are `null`, with each null reason reading `not on this spec's route` (AC-45 ledger side, AC-62).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: branch the `requirements.md` block (`show_me_facts.cs:272-304`) on `has_requirements_step`.
  - Traces to: FR-23, FR-10; AC-45, AC-62. Depends on: T10.1.

- [x] **TEST + IMPLEMENT: T10.3 — A resolver failure inside show-me is a tooling fault that writes no ledger** <!-- accepted as characterised by the owner: hand step green on arrival, named mutation does not turn it red because ParseRouteReport's missing-line check overlaps -->
  - **USE COMMAND**: none — no expectation row, because the fault depends on a case-insensitive file system. On a case-sensitive one `show_me_facts.cs`'s completeness gate (`:75-79`) exits `GateNotPassedExitCode` before the resolver runs. Named hand step, on macOS: run `dotnet run .claude/commands/spec/show_me_facts.cs -- .claude/test-fixtures/routing/specs/Light-Done --flows-dir .claude/test-fixtures/routing/flows` before the change and record the result. That is the RED: before the change it must not exit `ToolingFaultExitCode` with no ledger written (it falls through to the `git` and `gh` calls or writes a ledger). If it already does, characterise: named mutation: make `RouteProbe` ignore a non-zero resolver exit, and the step must then fail. After the change it must exit `ToolingFaultExitCode` and write no ledger.
  - Test should verify: the resolver exits `1` on the case-mismatched leaf; `show_me_facts.cs` exits `ToolingFaultExitCode`, and no ledger is written.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: treat a non-zero exit, a missing record line or a line that does not parse as a fault, before any `git` or `gh` call.
  - Traces to: 0089 *route probe* error conditions. *(This check is not in 0092's case list — see the scope-creep check.)* Depends on: T10.2.

- [x] **TEST + IMPLEMENT: T10.4 — `/spec:show-me` writes the route line in row 8 and `not available: not on this spec's route` in Inputs-used row 1 when the route has no requirements step**
  - Verification:
    - No `/tdd:test-first`, because this is command text. There is no registry row: `show-me.md` is not compared.
    - Named hand step, run here and again as T17.3 item 4: run `show_me_facts.cs` on `light-done` with `--flows-dir`, apply `show-me.md` row 8 and Inputs-used row 1 to that ledger by hand, then delete the ledger.
    - S1: `show_me_facts_tests.cs` passes.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - In `.claude/commands/spec/show-me.md`, add the `route.has_requirements_step == false` branch at `:447` and `:559-560`. F5 stays `Medium`, with a measured-value cell naming the route.
    - Make Inputs-used row 1 (`:689-691`) read exactly `not available: not on this spec's route`.
    - Leave every other row unchanged.
  - Traces to: 0089 step 8 (show-me.md); FR-23; AC-45 (command side). Depends on: T10.2.

## Phase 11 — Canonical texts in the READMEs

*0089 step 9 opening documentation commit, then 0090 step 2 (with 0092 step 9's gate-ladder and provenance checks), then 0091 step 2. No copy lands before its canonical block. Depends on Phase 10.*

- [x] **DOC: T11.1 — Write the canonical route instructions, closing instructions and 0089 message templates in `.claude/commands/spec/README.md`**
  - Do:
    - In *Supporting scripts*, add `routing-text:route-instructions` and `routing-text:closing-instructions` blocks, word for word from 0089 *How each command consults the resolver*.
    - Add a `routing-text:templates` block holding 0089's `| Template | Printed by | Text |` table: Next-step line, Not on the route, Another producer, Step not on the slip (both forms), Another marker-writer, Phase not on the route, Step with no review criteria, No requirements step, No design step, Ambiguous spec id, No spec matches, Artefacts missing, Flow not usable.
  - Acceptance: S1 is green. No copy exists yet, and no command file changes.
  - Traces to: 0089 step 9 (opening documentation commit); 0092 step 9 (canonical order); FR-7, FR-11, FR-26. Depends on: T10.4.

- [x] **TEST + IMPLEMENT: T11.2 — The canonical gate ladder has exactly 0090's five rows, and every provenance line the canonical commands describe matches 0090's pattern**
  - **USE COMMAND**: `/tdd:test-first when the canonical gate-run-contract block or provenance-commands block is checked it should require exactly 0090's five pinned starting states and every approved line to match the provenance pattern while rejecting a 39-digit sha an uppercase sha an empty artefacts field and a trailing space`
  - Test file: `spec_text_tests.cs`:
    - gate-ladder check;
    - provenance-pattern check, which first requires the `provenance-commands` block to exist;
    - registry entries `gate-run-contract` (canonical only), and `sub-flow-instructions` and `provenance-commands` (canonical; their copies come in T14).
  - Test should verify:
    - Today these are red, because the blocks are absent.
    - Afterwards: five rows, with Starting-state cells equal to the pinned text.
    - The worked `approved:` lines and the substituted `echo` line match `^approved: step=[a-z][a-z0-9-]* artefacts=[^ ]+ sha=([0-9a-f]{40}|[0-9a-f]{64}|unavailable)$`, and the four negative strings do not.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: make the 0090 step 2 documentation change in `.claude/commands/spec/README.md`:
    - the `sub-flow-instructions` block (steps 1–7);
    - the protocol lines;
    - the approver templates (No usable entry, Sub-flow failed ×2, Step approved) and the four gate templates, as tables inside the existing `templates` block;
    - the `gate-run-contract` block (the five-row table and the eight-step order);
    - the provenance-line notes and the `provenance-commands` block.
  - Traces to: 0090 step 2, *The sub-flow instructions*, *The gate run contract*, *The provenance commands*, *Message templates*; 0092 step 9 (checks land with 0090 step 2), *Further checks*; FR-13, FR-27, FR-29, FR-26. Depends on: T11.1.

- [x] **DOC: T11.3 — Write 0091's canonical installation steps, write order, record and log formats, templates, tamper recoveries, the `/spec:route` entry, and the *Starter commands* checklist**
  - Do: in `.claude/commands/spec/README.md`:
    - add `routing-text:installation-steps` and `routing-text:write-order` blocks;
    - add the `.flow.record` and `route-changes.md` formats;
    - add 0091's template table inside `templates`;
    - add the tamper recoveries;
    - add a `/spec:route` entry beside `/spec:gear`;
    - add a new **Starter commands** section with checklist items C1–C10, each yes/no, with a column naming the FR-19 clause it covers (C10 maps to the approval-table/sub-flow clause) and exactly one checking method, Fixture or Statement; an item that 0091 marks as both is split into two rows, so every row names one method; and the rule that a starter edits no `/spec:*` file (checked with `git diff --stat -- .claude/commands/spec/`).
  - Acceptance: S1 is green. O-2 is ruled (mapping column, one method per row). Read the checklist against AC-41: every FR-19 clause appears in the column.
  - Traces to: 0091 step 2, *Installation steps*, *The write order*, *Message templates*, *The starter checklist*; FR-19, FR-26; AC-41. Depends on: T11.2 (0091 step 2 must follow 0090 step 2).

## Phase 12 — The reporters

*0089 step 9.1, reporters first. T12.0 writes the tooling-fault check first. Each command task:
1. adds its registry rows and the file's expected template set to `spec_text_tests.cs`, commits them as `test:`, and sees them fail (0092 step 9);
2. passes the gate;
3. edits the command;
4. runs S1.

Depends on Phase 11.*

- [x] **TEST + IMPLEMENT: T12.0 — The tooling-fault sentence quoted in the canonical route instructions is required verbatim in each command that uses it**
  - **USE COMMAND**: `/tdd:test-first when a command that uses the route report lacks the tooling-fault sentence quoted in the canonical route instructions the text check should FAIL naming the file`
  - Test file: `spec_text_tests.cs`. The check takes its file list from a registry. This task registers `status.md`; T12.2 adds `switch.md`, T13.10 `new.md` and T15.1 `route.md`, each in its own `test:` commit before the file's copy lands.
  - Test should verify (the run stays red until T12.1 lands `status.md`'s copy, as T1.1 stays red until Phase 2):
    - The check extracts the quoted sentence from the `route-instructions` block (T11.1). It FAILs if the block has no quoted sentence.
    - Today it FAILs for `status.md`, because the sentence is absent.
    - A file with the sentence changed by one character FAILs.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should: add the extraction and the verbatim comparison to `spec_text_tests.cs` (0092 *Further checks*). No command file changes here.
  - Traces to: 0092 step 9, *Further checks* (tooling-fault message); FR-26; 0089 *How each command consults the resolver*. Depends on: T11.3.

- [x] **TEST + IMPLEMENT: T12.1 — `/spec:status` reports every spec's flow and source, steps, faults, route change and next step from one `--all` run, and never stops on a slip fault**
  - Verification rows (RED-first, `test:` commit): `status.md` must carry the *Next-step line* template, and (through T12.0's check) the verbatim tooling-fault sentence. No `/tdd:test-first`: command text.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Replace `status.md:16-21`, `:45`, `:47-51`, `:53-54`, `:60` and `:71` with an `--all` run and 0089's `--all` rule:
      - name violations by rule, code and step;
      - print shared violations once;
      - list a spec with no parsed line as `route unavailable`;
      - add a note when fewer lines parse than `specs` says.
    - Report the stray and out-of-order markers, tampered state, a missing definition, the route change and its reason, and the `.current-spec` state.
    - Keep the gear block (`:56-58`).
    - Read ADR statuses from `docs/adr/` itself for a `design` step.
    - Add `allowed-tools: Bash(dotnet run .claude/commands/spec/resolve_route.cs -- specs/:*)`.
    - Never fall back to reading markers.
  - Verify after:
    - S1 green.
    - `grep -n -e '.requirements-approved' -e '.design-approved' .claude/commands/spec/status.md` is empty.
    - Hand: run `/spec:status` after S5. Spec 0040 shows `feature (default — no .flow)` and today's step information (AC-29).
  - Traces to: 0089 step 9.1; FR-15, FR-11, FR-7, FR-20, FR-21, FR-22; AC-13, AC-20, AC-29, AC-30, AC-44. Depends on: T12.0.

- [x] **TEST + IMPLEMENT: T12.2 — `/spec:switch` picks a spec by show-me's ordered match, writes `.current-spec` only after a successful `--spec` run, and shows the same report and next step as status**
  - Verification rows (RED-first): `switch.md` must carry the *Next-step line*, *Ambiguous spec id* and *No spec matches* templates, and (add `switch.md` to T12.0's check first) the tooling-fault sentence.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Replace `switch.md:15-18`.
    - Match in order: exact directory name, then exact four-digit id, then case-insensitive substring, as prose.
    - Run `--spec "{dir}"` before the write. A fault leaves `.current-spec` as it was.
    - Add the resolver `allowed-tools` entry.
  - Verify after: S1 green. Hand: `/spec:switch 0002` prints the ambiguity template and writes nothing (`git status` unchanged).
  - Traces to: 0089 step 9.1, *`/spec:switch`*; FR-15, FR-11; AC-90 (command side via the template copy). Depends on: T12.1.

## Phase 13 — The route-following commands and `/spec:new`'s check

*0089 steps 9.2–9.9, then step 10. Each task follows the Phase 12 shape: (1) RED registry rows committed as `test:` and seen failing, (2) gate, (3) edit, (4) S1. Where a command carries `route-instructions`, its row is registered in step (1). Depends on Phase 12.*

- [x] **TEST + IMPLEMENT: T13.1 — `/spec:review` takes its phase from `next.kind`, accepts an explicit phase only when the slip has that kind, and leaves out FR criteria with no `requirements` step**
  - Verification rows (RED-first): `review.md` must carry `route-instructions`, and the templates *Next-step line*, *Phase not on the route*, *Step with no review criteria* and *No requirements step*.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Replace `review.md:30` (auto-detect).
    - Gate the FR criteria and `requirements.md` reads at `:57`, `:68`, `:78`, `:182-185`, `:294` and `:316` on `known_kinds.requirements`.
    - Change `:462`'s approve reminder to the next-step line.
    - Make `:469-471` list `steps`.
    - Always accept `code`.
    - Add the resolver `allowed-tools` entry.
  - Verify after: S1 green; marker grep empty for this file. Hand: T17.3 item 5 (AC-18, AC-59).
  - Traces to: 0089 step 9.2; FR-14, FR-10, FR-11, FR-7; AC-18, AC-19, AC-28, AC-59, AC-67. Depends on: T12.2.

- [x] **TEST + IMPLEMENT: T13.2 — `/spec:requirements` no longer creates a spec, stops on an unusable `.current-spec`, and stops when its kind is not on the route or has another producer**
  - Verification rows (RED-first): `requirements.md` must carry `route-instructions`, `closing-instructions`, and the templates *Next-step line*, *Not on the route* and *Another producer*.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Remove `requirements.md:29-33` and the `Bash(mkdir:*)` grant at `:2`.
    - Add the route instructions.
    - Add the `known_kinds.requirements` branches (null → not on the route; another producer → another-producer template).
    - Use the step precondition.
    - Replace `:207-208` with the closing instructions.
    - Add the resolver `allowed-tools` entry.
  - Verify after: S1 green; marker grep empty. Hand: T17.2 item 10 (AC-39), T17.3 item 5 (AC-12).
  - Traces to: 0089 step 9.3; FR-18, FR-20, FR-8, FR-11; AC-12, AC-39, AC-42. Depends on: T13.1.

- [x] **TEST + IMPLEMENT: T13.3 — `/spec:design` checks its step and precondition through the resolver, and with no `requirements` step cites the issue, else the README, as the ADR's parent**
  - Verification rows (RED-first): `design.md` must carry `route-instructions`, `closing-instructions`, and the templates *Next-step line*, *Not on the route* and *Another producer*.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Replace `design.md:31-37` with the route instructions and precondition.
    - Add the resolver `allowed-tools` entry (`Bash(dotnet run .claude/commands/spec/resolve_route.cs -- specs/:*)`) at `:2`.
    - Gate the inputs and FR-id lines at `:97`, `:108` and `:164-166` on `known_kinds.requirements`. With no `requirements` step, the Parent Requirement is `.issue-number` if present, else `README.md`, and no FR ids are claimed.
    - Replace `:424` with the closing instructions.
    - Stop before launching any sub-agent on a stop.
  - Verify after: S1 green; marker grep empty.
  - Traces to: 0089 step 9.4; FR-8, FR-10, FR-11, FR-7; AC-10, AC-11, AC-17, AC-86. Depends on: T13.2.

- [x] **TEST + IMPLEMENT: T13.4 — `/spec:tasks` checks FR coverage only with a `requirements` step and reads ADRs only with a `design` step**
  - Verification rows (RED-first): `tasks.md` must carry `route-instructions`, `closing-instructions`, and the templates *Next-step line*, *Not on the route* and *Another producer*.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Replace `tasks.md:24-31`.
    - Choose the sub-agent inputs at `:46-47` by kind.
    - At `:172-173`, check coverage against ADR decisions only when there is no `requirements` step.
    - Replace `:205-206` with the closing instructions.
    - Add the resolver `allowed-tools` entry.
  - Verify after: S1 green; marker grep empty. Hand: T17.3 item 5 (AC-16), T17.2 items 3 and 8 (AC-31, AC-80).
  - Traces to: 0089 step 9.5; FR-8, FR-10, FR-11, FR-7; AC-16, AC-19, AC-31, AC-70, AC-80. Depends on: T13.3.

- [!] **PROJECT: T13.5 — Record the baseline outcome of today's `/spec:approve design`, before `approve.md` changes** <!-- RALPH-SKIPPED: unrecognised task shape (PROJECT hand step), needs /spec:implement; T17.1 rebuilds the baseline from the merge-base approve.md if baselines.md lacks it -->
  - Do:
    - On a throwaway branch from `HEAD` (S7), build 0090 step 7's scenario: a `feature` spec with two `Proposed` ADRs, one recording "Supersedes" an `Accepted` ADR.
    - Run today's `/spec:approve design`, and record items 1–6: statuses, `Superseded by` link, index diff, marker, report text, and `/spec:approve design {number}`.
    - Also note item 7's behaviour (`chmod a-w` on the superseded ADR) as an uncompared baseline.
    - Save the notes in `specs/0040-routing-slips/baselines.md` (untracked, never staged). Delete the branch.
  - Acceptance: the notes exist. This runs before T13.6, so the file is the original one, which also satisfies "before 0090 step 3".
  - Traces to: 0090 step 7 (baseline); AC-24, AC-77. Depends on: T13.4.

- [x] **TEST + IMPLEMENT: T13.6 — `/spec:approve {step}` accepts only steps on the slip whose marker-writer it is, checks their artefacts through the resolver, and prints the next step from a second report**
  - Verification rows (RED-first): `approve.md` must carry `route-instructions` and `closing-instructions`, and its first-stage template set: *Next-step line*, *Step not on the slip* (both forms), *Another marker-writer* and *Artefacts missing*.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Replace `approve.md:15-28`, `:64`, `:81-87` (the two-driver advice now comes from `next`) and `:95-100`, using `approve_steps`.
    - Make `:32`, `:33` and `:59` generic: the presence check through `artefacts_present`, the artefact read, and the marker write to `steps[step].marker`.
    - Add the resolver `allowed-tools` entry (`Bash(dotnet run .claude/commands/spec/resolve_route.cs -- specs/:*)`) at `:2`.
    - Leave the `design`-specific `:34-58`, `:60-61` and `:102` for T14.1.
    - Keep `:62-63` and `:66-79` unchanged.
  - Verify after: S1 green; marker grep empty for `approve.md`.
  - Traces to: 0089 step 9.6; FR-12, FR-11, FR-8 (no new precondition); AC-13, AC-21, AC-23, AC-58 (command side), AC-66. Depends on: T13.5.

- [ ] **TEST + IMPLEMENT: T13.7 — `/spec:implement` starts only when the hand-off precondition holds, and runs its ADR-`Accepted` check and ADR reads only with a `design` step**
  - Verification rows (RED-first): `implement.md` must carry `route-instructions`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Replace `implement.md:65-70` and `:113-118` with the route instructions and `hand_off.precondition.message`.
    - Gate the ADR check and ADR reads on `known_kinds.design`.
    - Add no new `allowed-tools`, because `Bash(dotnet:*)` is already granted.
  - Verify after: S1 green; marker grep empty; restart (S5, risk K8). Hand: `/spec:implement` on spec 0040 still starts.
  - Traces to: 0089 step 9.7; FR-9, FR-10; AC-14, AC-15, AC-83. Depends on: T13.6.

- [ ] **TEST + IMPLEMENT: T13.8 — `/spec:ralph-implement` uses the same hand-off precondition, and passes `requirements.md` and ADR paths to sub-agents only for kinds on the slip**
  - Verification rows (RED-first): `ralph-implement.md` must carry `route-instructions`.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Replace `ralph-implement.md:152-155` and `:222`.
    - Add no ADR-status check (FR-9).
    - Add no new `allowed-tools`, because `Bash(dotnet:*)` is already granted.
  - Verify after: S1 green; marker grep empty; restart (S5).
  - Traces to: 0089 step 9.7; FR-9, FR-10; AC-15, AC-19, AC-60 (command side). Depends on: T13.7.

- [ ] **TEST + IMPLEMENT: T13.9 — `/adr:adr` proceeds unlinked on a missing, empty or stale `.current-spec`, and with a linked spec stops when the route has no design step**
  - Verification rows (RED-first): `adr/adr.md` must carry `route-instructions`, `closing-instructions`, and the templates *Next-step line* and *No design step*.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add the one exemption sentence before the route instructions.
    - Replace `adr.md:40-50` and `:345`.
    - With no `design` step, leave `.adr-list` untouched.
    - Skip the closing instructions when there is no linked spec.
    - Add no new `allowed-tools`, because `adr.md` already grants `Bash` (0089 *Per-command `allowed-tools` entry*).
  - Verify after: S1 green; marker grep empty.
  - Traces to: 0089 step 9.8; FR-10, FR-20, FR-11; AC-82. Depends on: T13.8.

- [ ] **TEST + IMPLEMENT: T13.10 — `/spec:new` checks the `feature` definition first, and creates nothing when it is missing or invalid**
  - Verification rows (RED-first): `new.md` must carry the *Flow not usable* template (first stage) and the tooling-fault sentence (register `new.md` in T12.0's check first).
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Add the `--definition feature` check before step 1 (`new.md:15`).
    - Add the resolver `allowed-tools` entry at `new.md:2`.
    - Leave the rest for T15.2.
  - Verify after: S1 green.
  - Traces to: 0089 step 9.9; FR-18 (check side); AC-69. Depends on: T13.9.

- [ ] **PROJECT: T13.11 — Verify that no command file names a literal marker, and that every 0089 copy matches its canonical text**
  - Do:
    - Run `grep -l -e '.requirements-approved' -e '.design-approved' .claude/commands/spec/*.md .claude/commands/adr/adr.md`. It must match `README.md` only (AC-9).
    - Run `spec_text_tests.cs`. It must be green, with the route instructions in 8 files, the closing instructions in 5, and every 0089 template copy.
  - Traces to: 0089 step 10; FR-7; AC-9. Depends on: T13.10.

## Phase 14 — The approval sub-flow in `/spec:approve`

*0090 steps 3 and 4 (merged, per O-3) and 6, with 0092 step 9's staged rows. Depends on Phase 13.*

- [ ] **TEST + IMPLEMENT: T14.1 — `/spec:approve` runs the step's sub-flow from the table in two passes, writes no marker unless every command prints `sub-flow-done`, and appends one provenance line to the marker**
  - Merged per the O-3 ruling (0090 steps 3 and 4 in one commit). Provenance behaviour: keeps existing marker content; the sha is recorded as `unavailable` when HEAD cannot be resolved.
  - Verification rows (RED-first): `approve.md` must carry `sub-flow-instructions`, `provenance-commands`, and its templates: *No usable entry*, *Sub-flow failed, questions*, *Sub-flow failed, actions*, *Step approved*.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Replace the per-ADR status and supersession prose (`approve.md:34-58`) and the report (`:60-61`) with the sub-flow instructions.
    - Pass `--select` words from after the step name.
    - Remove the index-regeneration section (`:102-112`) and the `:130` note.
    - Replace the `touch` (`approve.md:59`) with the three provenance commands.
    - Add `Bash(git rev-parse:*)` and `Bash(tail:*)` at `:2`.
    - Set `argument-hint: <step> [selector]` (`:4`).
  - Verify after: S1 green, including the provenance pattern. Behaviour: T17.1 (items 8–10 for provenance).
  - Traces to: 0090 steps 3 and 4, *The provenance line*; FR-12, FR-13, FR-29; AC-24, AC-25, AC-26, AC-27, AC-76, AC-77 (verified in T17.1). Depends on: T13.11.

- [ ] **PROJECT: T14.2 — Verify that `/spec:approve` holds no ADR-specific instruction**
  - Do: run `grep -n -e write_adr_metadata -e supersede -e deprecate -e generate_adr_index -e index.md -e Accepted -e '\.adr-list' -e 'ADRs approved' .claude/commands/spec/approve.md`. It must match nothing (AC-79).
  - Traces to: 0090 step 6; FR-12, FR-29; AC-79. Depends on: T14.1.

## Phase 15 — Slip creation and route change

*0091 steps 3 and 4, with 0092 step 9's rows. Depends on Phase 14.*

- [ ] **TEST + IMPLEMENT: T15.1 — `/spec:route {flow} [reason]` changes or repairs a route through 0091's ladder, writing in an order that an interrupted run can recover from**
  - Verification rows (RED-first; they fail because the file is absent): `route.md` must carry `installation-steps`, `write-order`, the tooling-fault sentence (register `route.md` in T12.0's check first), and the templates *Next-step line*, *Route usage*, *Route spec name*, *Route target not usable*, *Already on target*, *No reason*, *Route changed*, *Installation failed* and *Post-check*.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Create `.claude/commands/spec/route.md`.
    - Implement ladder rows 1–10: argument parsing with `--flows-dir` anywhere after the target; character refusals; the `--current` and `--definition` checks; row 5 including the log-consistency narrowing (O-4); the `AskUserQuestion` reason question.
    - Implement write order W0–W5 and the post-check.
    - Carry no closing instructions.
    - Set `allowed-tools` exactly as 0091 lists them.
  - Verify after: S1 green. Hand: T17.2 items 4–9 and 11.
  - Traces to: 0091 step 3, *`/spec:route`*, *The write order*; FR-17, FR-16 (repair), NFR-5; AC-35, AC-36, AC-37, AC-68, AC-80, AC-81. Depends on: T14.2.

- [ ] **TEST + IMPLEMENT: T15.2 — `/spec:new {name}` numbers the spec highest+1, refuses unsafe or ignored names, creates it atomically, installs the `feature` slip and record, and verifies the result**
  - Verification rows (RED-first): `new.md` must carry `installation-steps`, and its second-stage templates: *Next-step line*, *New spec failed*, *Start failed*, *Spec name*, *Spec exists*, *Spec ignored* and *Post-check*.
  - **⛔ APPROVAL GATE — STOP HERE and WAIT FOR USER APPROVAL in IDE before implementing** *(fires in the `review-before` gear, which is the default)*
  - Implementation should:
    - Apply the name rule.
    - Replace the numbering rule at `new.md:15`, correcting it to at least four digits.
    - Run the ignore check with `git check-ignore -q --no-index`.
    - Use `mkdir` with no `-p`.
    - Run the installation steps.
    - Write `README.md`, then write `.current-spec` last.
    - Run the `--spec` post-check.
    - Add the grants 0091 lists.
    - Offer no `--flow` option.
  - Verify after: S1 green. Hand: T17.2 items 1 and 2.
  - Traces to: 0091 step 4, *`/spec:new`*; FR-18, NFR-5; AC-38, AC-40, AC-52, AC-87, AC-88. Depends on: T15.1 and T13.10.

## Phase 16 — Documentation

*0089 step 11, 0090 step 5, 0091 step 5, 0092 step 10 (documentation part). Each DOC task must leave `spec_text_tests.cs` green: canonical blocks are not reworded. Depends on Phase 15.*

- [ ] **DOC: T16.1 — Document the route model in `.claude/commands/spec/README.md`**
  - Do:
    - (11.1) Add the resolver's invocation and modes in *Supporting scripts*.
    - (11.2) Add a route-model section: flows, definitions and `.flow`; `feature` as the default; the legacy rule; intent, stray and ignored markers; known and flow-specific kinds; gates; the approval table, including how a flow adds an entry.
    - (11.3) Label the Workflow diagram as the `feature` flow.
    - (11.4) Restate the marker lists (today `:403-405` and `:609-612`) as slip-based preconditions, and drop the ADR-status line for `/spec:ralph-implement`.
    - (11.5) Add `.flow` to File Structure.
  - Acceptance: AC-49 topics are present for 0089's share. S1 green.
  - Traces to: 0089 step 11; FR-26, FR-5; AC-49. Depends on: T15.2.

- [ ] **DOC: T16.2 — Name the `design` sub-flow wherever the docs credit `/spec:approve` alone**
  - Do:
    - Update `.agent_instructions/adr_frontmatter.md:78-79` and `:159`.
    - Update `.claude/commands/adr/README.md:51-52`, `:64-66` and `:182-194`.
    - Update `.claude/commands/adr/write_adr_metadata.md:116` (callers become `/adr:accept` and `/adr:retire-superseded`).
    - Update `.claude/commands/spec/README.md:233` and `:244-260` (the approve entry) and `:817-819` (marker comments).
  - Acceptance: S1 green.
  - Traces to: 0090 step 5; FR-26, FR-29. Depends on: T16.1.

- [ ] **DOC: T16.3 — Document `/spec:new`, `/spec:requirements` and `/spec:route`, and the tracked routing files**
  - Do:
    - In `.claude/commands/spec/README.md`:
      - the `/spec:new` entry (`:151-163`);
      - the `/spec:requirements` entry (`:166-191`): drop "Creates `specs/` directory…", and say it needs a current spec;
      - File Structure: `.flow.record` and `route-changes.md`, marked tracked.
    - List `/spec:route` in `.claude/commands/README.md` (`:162-168`) and `.agent_instructions/skills_overview.md` (`:45-46`).
  - Acceptance: S1 green.
  - Traces to: 0091 step 5; FR-26, FR-17, FR-18; NFR-5. Depends on: T16.2.

- [ ] **DOC: T16.4 — Document how to run the two routing test scripts and how a flow adds cases, and record FR-28's four migration differences**
  - Do: in `.claude/commands/spec/README.md`:
    - In *Supporting scripts*, describe `resolve_route_tests.cs` and `spec_text_tests.cs`, how to run them, that they are not run in CI, the fixture tree, and how a flow adds cases and expectations without editing either script.
    - Add a section recording location, gating, commands and gate output as decisions for a later `/bugfix:*` migration.
  - Acceptance: AC-49 is complete with T16.1–T16.3. AC-64.
  - Traces to: 0092 step 10 (documentation); FR-26, FR-28, FR-24; AC-49, AC-64. Depends on: T16.3.

## Phase 17 — Acceptance by hand and final checks

*0090 step 7, 0091 step 6, 0092 step 10 (hand items). Depends on Phase 16. S5 and S7 apply.*

- [ ] **PROJECT: T17.1 — Confirm that a `feature` design approval ends as it did before the extraction, and that provenance, failure and re-run behave as 0090 states**
  - Do: on a throwaway branch with T13.5's scenario, run 0090 step 7 items 1–11:
    - Compare items 1–6 with the T13.5 baseline in `baselines.md`. If it is missing, rebuild it on a throwaway branch from `$(git merge-base HEAD origin/master):.claude/commands/spec/approve.md`.
    - Item 7: `chmod a-w` stops the approval naming `/adr:retire-superseded`, with no marker. A re-run after `chmod u+w` completes with one provenance line.
    - Item 8: with `GIT_DIR` set to a directory that does not exist, the line records `sha=unavailable`.
    - Item 9: `/spec:approve tasks` adds one line naming `tasks`, `tasks.md` and the sha.
    - Item 10: free-text marker content is kept.
    - Item 11: re-approving leaves one `Superseded by` link.
  - Acceptance: every item matches. Record any O-4 difference as accepted.
  - Traces to: 0090 step 7; FR-12, FR-13, FR-29; AC-24, AC-25, AC-26, AC-27, AC-76, AC-77. Depends on: T16.4.

- [ ] **PROJECT: T17.2 — Confirm `/spec:new`, the tamper stops and `/spec:route` end to end**
  - Do: on a throwaway branch, run 0091 step 6 items 1–11:
    - 1: `/spec:new foo` gives `.flow`, a record, `README.md`, `feature`/`slip`/valid, and stageable files.
    - 2: the spec-exists race, refused names, the `PATH`-shadowed hash failure.
    - 3: cases (a), CRLF, (b) and (c); `/spec:tasks` stops naming both recoveries.
    - 4: no-reason dismiss changes nothing.
    - 5: `light` with reason R1, then `feature` with R2, against `--flows-dir .claude/test-fixtures/routing/flows`; status shows R1, then R2.
    - 6: unknown, already-on-target and invalid targets stop.
    - 7: legacy and tampered specs are repaired.
    - 8: AC-80.
    - 9: the write order interrupted after W1–W4.
    - 10: `/spec:requirements 123` with no `.current-spec`.
    - 11: the case (b) re-copy.
  - Acceptance: each item as stated in 0091.
  - Traces to: 0091 step 6; FR-16, FR-17, FR-18, FR-20, NFR-5; AC-31 to AC-39, AC-52, AC-68, AC-80, AC-81, AC-87, AC-88. Depends on: T17.1.

- [ ] **PROJECT: T17.3 — Run 0092's hand checks: a broken expectation, show-me unchanged, a CRLF checkout, show-me on `light-done`, and the scratch-branch command runs**
  - Do:
    - 1: change one value in an expectation. The script prints a FAIL and exits `1`. Revert (AC-47).
    - 2: `show_me_facts_tests.cs` passes, and `git diff --exit-code $BASE -- .claude/commands/spec/show_me_facts_tests.cs .claude/test-fixtures/show-me/` is clean (AC-46).
    - 3: both scripts pass on a `core.autocrlf=true` checkout.
    - 4: run `dotnet run .claude/commands/spec/show_me_facts.cs -- .claude/test-fixtures/routing/specs/light-done --flows-dir .claude/test-fixtures/routing/flows`, apply `show-me.md` row 8 and Inputs-used row 1 to the ledger by hand, then delete the ledger (AC-45, command side).
    - 5: on a scratch branch, copy `light.flow` and `bugfix.flow` into `.claude/flows/`, add the `diagnosis` entry, copy `light-nothing`, `light-adr-list`, `light-design-approved` and `bugfix-diagnosed` into `specs/` with their records, and select each with `/spec:switch`. Then check AC-12 (`/spec:requirements`), AC-16 (`/spec:tasks`), AC-18 (`/spec:review`) and AC-59 (`/spec:review`). Delete the branch.
  - Acceptance: each item passes.
  - Traces to: 0092 step 10 items 1–5; FR-23, FR-24, FR-25, FR-28; AC-12, AC-16, AC-18, AC-45, AC-46, AC-47, AC-59. Depends on: T17.2.

- [ ] **PROJECT: T17.4 — Final repository checks before the PR**
  - Do:
    - S1: all three scripts green. Read the timing line and record whether a `WARN` appeared and the duration (AC-51).
    - AC-6: `git diff --stat $BASE -- specs/ ':(exclude)specs/0040-routing-slips'` is empty.
    - AC-9: re-run the grep.
    - AC-40: `grep -n -E -e '--flow([^s-]|$)' .claude/commands/spec/*.md` lists only prose that denies the option (such as "offer no `--flow` option"); record each match, and any match that defines or accepts a `--flow` option fails (risk K7).
    - AC-50: `git diff --stat $BASE -- '*.csproj' '*.props' global.json` is empty, and no `#:package` directive appears in the three scripts.
    - AC-1: listing `.claude/flows/` shows `feature.flow` and `approvals.md` only.
    - These are unchanged against `$BASE`: `.claude/settings.json`, `gear.md`, `write_release_notes.md`, `.claude/commands/bugfix/`, `bugfixes/`, `generate_adr_index.awk`, `.gitattributes`, `.gitignore`, `CLAUDE.md`.
    - `git ls-files -s` shows mode `100644` for `resolve_route.cs`, `resolve_route_tests.cs` and `spec_text_tests.cs`, and none starts with `#!`.
  - Acceptance: all hold. Do not add a release-notes task (spec 0037 precedent).
  - Traces to: 0089 *unchanged* list and step 10; 0091 and 0092 unchanged lists; NFR-2, NFR-4, NFR-6; AC-1, AC-6, AC-9, AC-40, AC-50, AC-51. Depends on: T17.3.

---

## Commit plan (interleave)

| # | Task(s) | ADR step | Commits (each followed by a `docs:` tick) |
| --- | --- | --- | --- |
| 1 | T0.1 | — | tick only |
| 2 | T1.1–T1.3 | 0092 s1 (before 0090 s1) | `test:` ×3 (T1.1/T1.2 red until Phase 2) |
| 3 | T2.1 | 0090 s1, canonical part | `docs:` |
| 4 | T2.2–T2.5 | 0090 s1 (prerequisite for 0089 s1) | `feat:` ×4 |
| 5 | T3.1 | 0089 s1 satisfied, s2 | `refactor:` (structural: nothing reads the files yet) |
| 6 | T4.1 | 0092 s2 (before 0089 s3) | `test:` (red) |
| 7 | T4.2 | 0089 s3 | `feat:` |
| 8 | T5.1–T5.18 | 0092 s3 rides 0089 s4, one rule at a time | `feat:` per rule (expectation and code together) |
| 9 | T6.1–T6.10 | 0092 s4 rides 0089 s5 (O-1) | `feat:` per task (T6.2 `test:` only, as it changes no code); T6.10 `test:` |
| 10 | T7.1, T7.2 | 0092 s5 (fixture-only) | `test:` ×2, with empty diff over `.claude/commands/` (and `bugfixes/`) |
| 11 | T8.1–T8.8 | 0092 s6 rides 0089 s6 | `feat:`/`test:` per task |
| 12 | T9.1–T9.9 | 0092 s7 rides 0091 s1 (= 0089 s7) | `feat:`/`test:` per task |
| 13 | T10.0–T10.3 | 0092 s8 rides 0089 s8 | T10.0 `test:`; T10.1–T10.3 `feat:` |
| 14 | T10.4 | 0089 s8 (`show-me.md`) | `feat:` |
| 15 | T11.1 | 0089 s9 opening documentation commit | `docs:` |
| 16 | T11.2 | 0090 s2 + 0092 s9 (ladder and provenance checks) | `feat:` |
| 17 | T11.3 | 0091 s2 (after 0090 s2) | `docs:` |
| 18 | T12.0–T12.2 | 0092 s9 rows, then 0089 s9.1 | T12.0 `test:`; T12.1–T12.2 `test:` + `feat:` each |
| 19 | T13.1–T13.4 | 0092 s9 rows, then 0089 s9.2–9.5 | `test:` + `feat:` each |
| 20 | T13.5 | 0090 s7 baseline | tick only |
| 21 | T13.6 | 0092 s9 rows, then 0089 s9.6 | `test:` + `feat:` |
| 22 | T13.7–T13.9 | 0092 s9 rows, then 0089 s9.7–9.8 | `test:` + `feat:` each |
| 23 | T13.10 | 0092 s9 rows, then 0089 s9.9 | `test:` + `feat:` |
| 24 | T13.11 | 0089 s10 | tick only |
| 25 | T14.1 | 0092 s9 rows, then 0090 s3 and s4 merged (after 0089 s9.6) | `test:` + `feat:` |
| 26 | T14.2 | 0090 s6 | tick only |
| 27 | T15.1, T15.2 | 0092 s9 rows, then 0091 s3, s4 (s4 after 0089 s9.9) | `test:` + `feat:` each |
| 28 | T16.1–T16.4 | 0089 s11, 0090 s5, 0091 s5, 0092 s10 (documentation) | `docs:` ×4 |
| 29 | T17.1–T17.4 | 0090 s7, 0091 s6, 0092 s10 (hand), final | tick only |

## Coverage cross-reference

### Requirements → tasks

| Req | Tasks |
| --- | --- |
| FR-1 | T3.1, T5.18, T8.8, T7.1/T7.2 (not shipped), T17.4 |
| FR-2 | T3.1, T5.1, T5.2, T7.1 |
| FR-3 | T5.1–T5.18, T6.5, T6.7, T8.2 |
| FR-4 | T3.1, T6.1–T6.3, T8.4 |
| FR-5 | T6.1, T6.2, T6.10, T8.4, T9.5, T16.1, T17.4 (AC-6) |
| FR-6 | T4.2, T5.1, T6.1–T6.9, T8.1, T8.4, T9.2–T9.8 |
| FR-7 | T8.2, T9.7, T11.1, T12.1–T13.10, T13.11 (AC-9) |
| FR-8 | T6.1, T6.2, T13.2–T13.4, T13.6 |
| FR-9 | T6.2, T6.3, T7.1, T7.2, T13.7, T13.8 |
| FR-10 | T6.6, T10.2, T13.1, T13.3, T13.4, T13.7–T13.9 |
| FR-11 | T6.1–T6.4, T6.6, T6.9, T11.1, T12.1, T12.2, T13.1–T13.6, T13.9 |
| FR-12 | T6.1 (`approve_steps`), T7.2, T13.6, T14.1, T14.2 |
| FR-13 | T11.2, T14.1, T17.1 |
| FR-14 | T13.1, T7.1, T7.2 (resolver side) |
| FR-15 | T8.4, T8.5, T9.8, T9.9, T12.1, T12.2 |
| FR-16 | T9.1–T9.7, T17.2 |
| FR-17 | T9.7, T9.8, T11.3, T15.1, T16.3, T17.2 |
| FR-18 | T13.2, T13.10, T15.2, T16.3, T17.2 |
| FR-19 | T11.3 (checklist; O-2) |
| FR-20 | T8.1, T13.2, T13.9, T17.2 |
| FR-21 | T6.4, T6.6, T6.10, T7.2, T9.8 |
| FR-22 | T6.5, T8.2, T9.6 |
| FR-23 | T10.1–T10.4, T17.3 |
| FR-24 | T1.1, T4.1, Phases 5–9, T17.3 |
| FR-25 | T7.1, T17.3 |
| FR-26 | T11.1–T11.3, T16.1–T16.4 |
| FR-27 | T5.11, T5.12, T6.8, T7.2, T11.2 |
| FR-28 | T7.2, T16.4, T17.3 |
| FR-29 | T2.1–T2.5, T3.1, T5.14, T6.1 (AC-75), T6.7, T11.2, T14.1, T17.1 |
| FR-30 | T5.17, T6.9, T7.1 |
| NFR-1 | T4.1, T4.2 |
| NFR-2 | T3.1, T4.2, T17.4 |
| NFR-3 | T4.1 |
| NFR-4 | T8.4, T8.7, T17.4 |
| NFR-5 | T1.3, T15.1, T15.2, T16.3, T17.2 |
| NFR-6 | T6.10, T8.4, T10.1, T17.3, T17.4 |

### Acceptance criteria → tasks (compact)

| ACs | Tasks |
| --- | --- |
| AC-1 | T8.8, T17.4 |
| AC-2 | T3.1 |
| AC-3 | T5.2–T5.18 |
| AC-4 | T6.10, T8.4 |
| AC-5 | T6.2 |
| AC-6 | T17.4 |
| AC-7 | T4.1 |
| AC-8 | T7.1 |
| AC-9 | T13.11, T17.4 |
| AC-10, AC-11 | T8.2, T6.1, T13.3 |
| AC-12 | T7.1, T13.2, T17.3 |
| AC-13 | T6.4 (`legacy-tasks-approved-only`), T13.6, T12.1 |
| AC-14, AC-15 | T13.7, T7.1 |
| AC-16 | T13.4, T17.3 |
| AC-17 | T13.3 |
| AC-18 | T7.1, T13.1, T17.3 |
| AC-19 | T13.1, T13.4, T13.8 |
| AC-20, AC-21, AC-22 | T6.2, T7.1, T6.4, T12.1, T13.6 |
| AC-23 | T7.1, T13.6 |
| AC-24 | T13.5, T14.1, T17.1 |
| AC-25 to AC-27 | T14.1, T17.1 |
| AC-28 | T13.1 |
| AC-29, AC-30 | T8.4, T6.6, T9.8, T12.1 |
| AC-31 to AC-34 | T9.2–T9.7, T17.2 |
| AC-35 to AC-37 | T15.1, T17.2 |
| AC-38 | T15.2, T17.2 |
| AC-39 | T8.1, T13.2, T17.2 |
| AC-40 | T15.2, T17.4 |
| AC-41 | T11.3 (O-2) |
| AC-42 | T8.1, T13.2, T13.3 |
| AC-43 | T6.4 |
| AC-44 | T6.5, T12.1 |
| AC-45 | T10.2, T10.4, T17.3 |
| AC-46 | T10.1, T17.3 |
| AC-47 | T4.1, T17.3 |
| AC-48 | T7.1 |
| AC-49 | T16.1–T16.4 |
| AC-50 | T17.4 |
| AC-51 | T8.7, T17.4 |
| AC-52 | T15.2, T17.2 |
| AC-53 | T5.8–T5.11 |
| AC-54 | T6.6 |
| AC-55 to AC-61, AC-63 | T6.8, T7.2 (AC-58 and AC-61 command sides: T13.6, T13.4) |
| AC-62 | T10.2 |
| AC-64 | T16.4 |
| AC-65 | T6.2 |
| AC-66, AC-67 | T7.1, T13.6, T13.1 |
| AC-68 | T15.1, T17.2 |
| AC-69 | T13.10 |
| AC-70 | T6.5, T8.2 |
| AC-71 | T6.10 |
| AC-72 | T5.12 |
| AC-73 | T6.8, T7.2 |
| AC-74 | T5.14, T6.7 |
| AC-75 | T6.1 |
| AC-76, AC-77 | T14.1, T17.1 |
| AC-78 | T7.2 |
| AC-79 | T14.2 |
| AC-80, AC-81 | T9.7, T9.8, T15.1, T17.2 |
| AC-82 | T8.1, T13.9 |
| AC-83 | T7.2, T13.7 |
| AC-84 | T6.3, T7.1 |
| AC-85 | T5.17 |
| AC-86 | T13.3 |
| AC-87, AC-88 | T15.2, T17.2 |
| AC-89 | T7.2 |
| AC-90 | T9.8, T12.2 |

### ADR decisions → tasks

**ADR 0089**

| Decision | Tasks |
| --- | --- |
| Strict Markdown slip format | T3.1, T5.1, T5.2 |
| Separate approval table `approvals.md` | T3.1, T5.1, T5.14 |
| Validity rules R0–R16, violation ordering | T5.2–T5.18 |
| C# file-based resolver, `100644`, no warnings | T4.2, S2 |
| Record-line prefix on stdout | T4.1, T4.2 |
| One complete report and its schema | T6.1–T6.9 |
| Four modes and `--flows-dir` | T4.2, T6.1, T8.1, T8.4 |
| Next-step ladder | T6.1–T6.6 |
| Stop ladder | T8.1, T8.2, T9.7 |
| `--all` summary and budget | T8.4–T8.6, T9.9 |
| Deterministic serialisation | T4.2, T4.1 |
| `show_me_facts.cs` as a child-process caller (`RouteProbe`) | T10.1–T10.3 |
| Canonical route, closing and template texts | T11.1 |
| Per-command `allowed-tools` entry | T12.1–T13.10 |
| How each command consults the resolver | T12.1–T13.10 |
| `/spec:switch` match rule and write-after-resolve | T12.2 |
| README route model | T16.1 |
| Unchanged-files list | T17.4 |

**ADR 0090**

| Decision | Tasks |
| --- | --- |
| Approval stages and sub-flow instructions | T11.2, T14.1 |
| Two passes through the Skill tool | T2.2–T2.5, T14.1 |
| Arguments and answers | T2.2–T2.5 |
| Protocol lines | T2.2–T2.5, T11.2 |
| Failure, re-run and idempotence | T2.2–T2.5, T17.1 |
| Provenance line and commands | T11.2, T14.1 |
| Gate run contract (canonical only; no gate ships) | T11.2, T7.2 (resolver states) |
| Four `/adr:*` commands and the selection rule | T2.1–T2.5, T3.1 |
| Templates | T2.1, T11.2, T14.1 |
| Documentation touch-ups | T16.2 |
| `approve.md` holds no kind-specific text | T14.2 |
| *A second sub-flow* (`change`) | Illustrative only. No task, by design |

**ADR 0091**

| Decision | Tasks |
| --- | --- |
| `.flow.record` with SHA-256 of the LF content | T9.1–T9.3, S3 |
| `RouteRecordInspector` case table | T9.2–T9.5 |
| Tampered stop messages | T9.7 |
| Route-change log | T9.8 |
| Installation steps | T11.3, T15.1, T15.2 |
| `/spec:route` ladder, including the row-5 narrowing | T15.1 (O-4) |
| Write order | T11.3, T15.1, T17.2 |
| `/spec:new` procedure | T15.2 |
| Templates | T11.3, T15.1, T15.2 |
| Starter checklist C1–C10 | T11.3 (O-2) |
| README, `commands/README` and `skills_overview` updates | T16.3 |
| Corrections to 0089's text | Already made in the Accepted ADRs. No task |

**ADR 0092**

| Decision | Tasks |
| --- | --- |
| Two scripts | T1.1, T4.1 |
| Fixture tree layout | Phases 5–9 |
| JSON partial expectations | T4.1 |
| Generated cases | T8.1, T8.3, T9.2–T9.8 |
| Repository checks (existing specs and oracle, timing WARN, size, read-only, AC-1, matcher self-test) | T4.1, T6.10, T8.4–T8.8 |
| Text-block markers and registry | T1.1, Phases 12–15 |
| CommonMark table parser | T1.2 |
| Tooling-fault, gate-ladder, provenance and ignore checks | T1.3, T11.2, T12.0, T12.1, T12.2, T13.10, T15.1 |
| Duplicated helpers | T1.1, T4.1 |
| No fixture gate or sub-flow command | Honoured: no task adds one |
| Canonical-text order | Phase 11 ordering |
| Documentation and hand checks | T16.4, T17.3 |

### Gaps and scope check

- **No FR or ADR decision without a task.** NFR-4 and AC-51 rest on a human reading a WARN (T17.4), as 0092 accepts.
- **Command-side ACs with stand-in verification only** (risk K6): AC-10, 11, 14, 17, 23, 28, 42, 66, 67, 69, 82, 83, 86.
- **Tasks or content beyond the ADRs' explicit lists.** Each traces to an FR or ADR decision, but needs reviewer attention:
  - **T6.6, T6.8 and T6.9 (O-1):**
    - `evaluation-ready` gains a stray `.requirements-approved`.
    - `spike-step`'s slip gains a gate and a detour.
    - `flows-edited/` moves forward from 0092 step 7.
    - The expectations `legacy-all-approved-edited` and `spike-step-spike-entry` are new.
    - `spike-gate-also` and `spike-gate-also-approved` (T6.8), `legacy-tasks-approved-only` (T6.4, AC-13), `definition-r10-exemption` (T5.12), `definition-feature-shipped` (T5.14) and `show-me-runner` (T10.0) are new cases.

    These trace to FR-6, FR-21, FR-27, FR-30 and 0089 step 5.
  - **T10.0:** the runner passes `--flows-dir` only when the expectation has a `flows` key (0092 *Expectations* says it always does), because `show_me_facts.cs` rejects the option until T10.1. This departure is recorded here.
  - **T8.5:** an explicit shared-violations assertion, tracing to 0089 *The route report* and FR-15.
  - **T10.3:** a macOS-only hand step for a resolver failure inside show-me, tracing to 0089's RouteProbe error conditions.
  - **T2.2, T2.3, T2.4, T2.5, T10.3, T10.4, T12.1, T12.2, T13.7:** small direct-run hand steps. Each is verification only and adds no artefact.
  - **T0.1 and T13.5:** baselines, tracing to AC-6, AC-46 and 0090 step 7.
- **Not scheduled, by instruction:** no release-notes task, no `CLAUDE.md` edit, no amendment to `requirements.md` (O-4), and no gear encoded.
