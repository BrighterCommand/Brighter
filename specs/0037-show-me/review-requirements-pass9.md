# Review: requirements — 0037-show-me (pass 9)

**Date**: 2026-09-23
**Threshold**: 60
**Verdict**: NEEDS WORK

7 findings at or above threshold 60. Address these before approving.

**Context**: This pass reviews `specs/0037-show-me/requirements.md` at HEAD `5e0737fe5`, the commit that answers pass 8's 14 findings at or above 60. The on-disk `.requirements-approved` predates this commit and three earlier amendments, so it is stale. Twelve of pass 8's fourteen findings at or above 60 are fully resolved and one is resolved with a residue (see the disposition table). The other one, #5 (the declared-id regex), was fixed in a way that introduced a regression. The amendment also created several new defects. The worst are a declared-id pattern that no longer recognises heading-style declarations, which real specs on `master` use, and a switch from `gh pr diff` to `git diff {merge-base(origin/master, headRefOid)}..{headRefOid}` that measures an empty diff for any PR already merged.

## Findings

### 1. The new anchored declared-id pattern drops heading and ordered-list declarations that the old rule counted and real specs use (Score: 85)

Pass 8 #5 asked for the declared-id rule to be anchored. The amendment anchored it to a bold lead-in only: `^[[:space:]]*(-[[:space:]]+)?\*\*(FR|NFR)-[0-9]+`. The text it replaced explicitly counted headings too: "it appears at the start of a requirement's **heading** or bold lead-in". Headings, numbered-list lead-ins (`1. **FR-1:`) and plain list items (`- FR-1:`) no longer declare anything. Several specs on `master` use exactly those forms, so the pattern reports **0** declared ids for them. FR-16 row 9 then makes the command write "requirements.md declares no numbered requirements — nothing to reconcile." and set F5 = Medium. Both statements are false, and NFR-7 forbids false statements. NFR-1 makes this count mechanical, so the error is deterministic: every run gets it wrong.

**Evidence** (`/usr/bin/grep -oE` with the stated pattern, compared with a heading-form pattern):
```
anchored-bold  heading-form  spec
0              19            specs/0023-Pipeline-Validation-At-Startup   (#### FR-1: Handler Pipeline ...)
0              9             specs/0023-box_database_migration          (#### FR-1: Unified Box ...)
0              -             specs/0021-Error-Examples                  (1. **FR-1**: Each sample MUST ...)
0              -             specs/0004-transport-scheduler-wiring      (- FR-1: `RmqMessageConsumerFactory` ...)
3              -             specs/0020-DontAckAction                   (1. **FR-1: DontAckAction exception** ... FR-2..FR-5 missed)
```
`git branch -a --contains 4d1aeff4a` (the last commit to touch 0023-Pipeline-Validation's requirements.md) includes `master`. Diff of the old FR-8 text: "`-` … i.e. it appears at the start of a requirement's heading or bold lead-in (`**FR-7 — …**`)".

**Recommendation**: Widen the pattern to cover every declaration form actually in use: an ATX heading (`^#+[[:space:]]+`), an ordered or unordered list marker (`([0-9]+\.|-)[[:space:]]+`), and an optional `**`. Still require the id to be the first token after that prefix. Add one of the 0023 specs to NFR-9's fixture table with its expected count (19 or 9). Update the "measured … 3, 8, 0 and 37" sentence if the widening changes any figure.

---

### 2. Measuring from `merge-base(origin/master, headRefOid)` gives an empty spec diff for any merged PR, which `--state all` explicitly discovers (Score: 80)

FR-20 still discovers PRs with `--state all`, so merged PRs are included, and the measured head is now that PR's `headRefOid`. Once a PR is merged, its head is an ancestor of `origin/master`. The merge base is then the head itself, and the spec diff is empty. The run gets 0 files in every bucket, F1 `Low`, D1 and D2 not firing, and no paths for FR-14. FR-14's 3–7-path rule (AC-26) cannot be satisfied, and no FR-16 row describes the case, so the output is quietly wrong. The dropped `gh pr diff` did not have this problem because it returns a merged PR's own diff. FR-4 invites exactly this situation ("regenerated … whenever the picture changes"), and nothing restricts the command to open PRs. It also puts an expiry date on the calibration fixture. AC-18, AC-20, AC-56 and AC-78 all assume 517/76 files for spec 0036. They start failing once PR #4282 (currently OPEN) merges and `origin/master` is fetched, which C-8 already anticipates ("branches it has been merged into").

**Evidence**:
```
$ gh pr view 4039 --json headRefOid,state   → ce8bb9ecf…, MERGED
$ git merge-base origin/master ce8bb9ecf…   → ce8bb9ecf…   (the head itself)
$ git diff --name-only ce8bb9ecf..ce8bb9ecf | wc -l → 0
$ git merge-base --is-ancestor ce8bb9ecf origin/master → true
```
FR-20: "`gh pr list --head … --state all --json number,url,headRefName,headRefOid,createdAt`"; "the spec diff is `git diff {merge base}..{measured head}`".

**Recommendation**: Choose and state a rule. One option is to drop merged and closed PRs from discovery (`--state open`). Another is to use the PR's `baseRefOid` (or the first parent of `mergeCommit`) as the diff base for a merged PR. A third is to add an FR-16 row and AC for "spec diff is empty (head already merged)" with defined text and a defined F1. Also give C-8 a note, or a pinned base, so the calibration ACs survive the merge of #4282.

---

### 3. "Use the pattern verbatim" contradicts "a Python script or a small .NET tool would satisfy every requirement": both engines silently match nothing on POSIX bracket classes (Score: 70)

All four stated patterns now use `[[:space:]]`, and the document requires "Implementations must use the extended form above verbatim" and "never transcribed". FR-21 then says "A shell script, an awk program, a Python script and a small .NET tool would all satisfy every requirement stated here." Neither Python `re` nor .NET `Regex` supports POSIX bracket classes. Used verbatim, the public-API pattern returns false on a plain `+    public void Foo()`, with no error. That is the same silent-zero failure NFR-9 exists to catch. Python also prints a `FutureWarning` to stderr (see finding 5). This does not re-raise "language not specified". The defect is that two of the four languages the text names as acceptable cannot meet the text's own "verbatim" rule.

**Evidence**:
```
python3: re.search(r"^[+-][[:space:]]*(public|protected)[[:space:]]", "+    public void Foo()") → False
         (stderr: FutureWarning: Possible nested set at position 2)
dotnet run rx.cs: Regex.IsMatch("+    public void Foo()", same pattern) → False
/usr/bin/grep -cE same pattern → 1
```

**Recommendation**: State that the patterns carry POSIX ERE *semantics*: an implementation either runs them through a POSIX ERE engine (`grep -E`, `awk`) or uses a stated, fixture-tested translation. Either relax "verbatim", or narrow the list of languages that can satisfy the requirement. Keep NFR-9's per-pattern fixtures as the guard.

---

### 4. It is contradictory whether the command or the script runs `gh pr list`, so AC-30 and AC-73's "invoked at most once" cannot be observed (Score: 68)

FR-20 still says "*PR discovery*: **the command** runs `gh pr list …`". FR-18 and C-10 justify it by the existing `Bash(gh pr list:*)` allow-list entry, which only matters if the command issues it. Against that, the new *Measured head* definition says it is "Resolved once per run, **by the measurement script**". FR-21 "What the script owns" includes "PR discovery under FR-20". FR-18 now says "the `git` and `gh` processes the measurement script starts are that program's own children and need no entry". Two developers would build different things: the command runs `gh` and passes `headRefOid` into the script as an argument, or the script runs `gh` itself. AC-30 and AC-73 check "the commands the run issued" for `gh pr list` at most once and `gh pr diff` never. If the script issues them, they never appear in the command's tool log, and no AC says how a script's children are to be observed.

**Evidence**: FR-20 line 722 ("the command runs"); Definitions line 98 ("by the measurement script"); FR-18 lines 958–960; AC-30 "every `gh` invocation is `gh pr list`, invoked at most once"; AC-73.

**Recommendation**: Name a single issuer. If it is the script, reword FR-20 and C-10, and have AC-30 and AC-73 assert from a stated observable, such as a `gh_invocations` field in the ledger or a `PATH`-shimmed `gh` in the test script. If it is the command, add the script's head-sha input to FR-21.

---

### 5. The amendment moves parsed data onto stderr on the claim that stderr "carries no toolchain diagnostics", which is false (Score: 66)

FR-21 now routes both payloads the command must parse to stderr: FR-3's gate facts (exit `2`) and the word-count result. The justification: stdout is unsafe because toolchains write diagnostics there "only when the script is recompiled", while stderr "carries no toolchain diagnostics". That is false for the named candidate languages, and for the script's own children, which inherit stderr unless redirected. Python emits compile-time `FutureWarning`/`SyntaxWarning` on stderr, and does so on the very `[[:space:]]` patterns this spec mandates. It happens only when the bytecode is not cached, which is exactly the "fresh checkout" failure the text uses to reject stdout. `gh` writes auth and network errors to stderr. The stderr payload format is also undefined. The result is a bad outcome for a correctly functioning script: a correct exit `2` whose stderr also carries a warning hits "gate facts were not parseable", and the user is told it is a tooling fault.

**Evidence**:
```
$ python3 sw.py 1>/dev/null        # file compiles "^[[:space:]]*-[[:space:]]\[[ xX]\]"
sw.py:2: FutureWarning: Possible nested set at position 2      ← stderr
$ GH_TOKEN=bad gh pr list … 1>/dev/null
HTTP 401: Bad credentials …                                     ← stderr
```
FR-21: "travel on **standard error**, which carries no toolchain diagnostics".

**Recommendation**: Delete the universal claim. Either write the gate facts and the word-count result to a file as well (a second ledger-like file, or a gate-only ledger), or define a delimited, self-identifying stderr record (for example, one line prefixed `show-me-gate: {json}`) that the command extracts while ignoring every other stderr line. State that the script must suppress or redirect its children's stderr.

---

### 6. NFR-8 and the tooling-fault message ("No file was written") contradict FR-21 and AC-72 for the "exit 0, unparseable ledger" state (Score: 62)

In FR-21's fifth failure state, the script exits `0` "having written a ledger". So a ledger was created, or a previous one replaced. AC-72 acknowledges this by limiting its no-ledger guarantee to "the first four states". NFR-8 still says, with no exception: "A stop under FR-1, FR-2, FR-3 or FR-21 must leave the repository byte-for-byte unchanged, ledger included — no ledger is created and no existing ledger is touched on a stop." The fixed stop message prints "No file was written." in that state, which is false.

**Evidence**: NFR-8 line 1178–1180; FR-21 message line 857; AC-72 "in the first four states no ledger is created".

**Recommendation**: Either require the command to delete or restore the ledger in state 5 (and extend AC-72 to cover it), or add a state-5 exception to NFR-8 and change the message's "No file was written" for that state (e.g. "No show-me.md was written").

---

### 7. The new claim that NFR-2's range is "met by construction", and AC-33's second clause, do not hold: two contributing sections are uncapped in item count (Score: 62)

Pass 8 #8 was settled as report-and-continue, which is fine. The new text then asserts "NFR-2's range is met by construction — by the per-section caps its arithmetic rests on". AC-33 turns that into a test: "given a run in which every section stayed within the caps NFR-2's arithmetic rests on, then that count is between 400 and 2,000". But the arithmetic rests on non-caps: "14 breaking-change bullets" (the calibration count, not a limit) and deviation entries "uncapped in count … ≤ 150 in practice". Take a conforming spec with 40 breaking-change items at 40 words each (1,600) plus a 600-word narrative. It obeys every stated cap and still fails AC-33's second clause. NFR-2 also still says the body "**must** be between 400 and 2,000".

**Evidence**: NFR-2 lines 1017–1018, 1033–1044; FR-21 *Modes* line 842; AC-33.

**Recommendation**: Either cap the number of FR-7 items and FR-8 deviation entries (with an overflow line), or downgrade NFR-2's upper bound to a reported target. Drop "met by construction" and AC-33's second clause.

---

### 8. FR-15 still records diagram sources only when "read in full" (pass 8 #15 carried forward, unaddressed) (Score: 55)

The text is unchanged. A diagram source read by targeted extraction under NFR-3's *Degradation* gets no `## Inputs used` row, yet AC-56 requires every diagram symbol to be in a file "recorded as read in `## Inputs used`".

**Evidence**: FR-15 line 703–704 "one row per source file read in full for a diagram (FR-6)".

**Recommendation**: As in pass 8: "one row per source file read for a diagram, marked `used` or `used (targeted extraction)`".

---

### 9. FR-3 says "No third stop exists", but FR-1, FR-2, NFR-8 and FR-19 define four kinds of stop (Score: 55)

The amendment edited this sentence and kept "There is exactly one other stop … No third stop exists". FR-1 (ambiguous or no match) and FR-2 (no usable current spec) are also stops, and NFR-8 and FR-19 list "a stop under FR-1, FR-2, FR-3 or FR-21".

**Evidence**: FR-3 lines 235–240; NFR-8 line 1178; FR-19 line 971.

**Recommendation**: Reword as "FR-3's gate is the only stop caused by the resolved spec's content; the others are FR-1/FR-2's resolution stops and FR-21's tooling stop."

---

### 10. NFR-9's test script never exercises the new exit-`2` contract (Score: 55)

The three-way exit status is the core of the pass-8 #1 fix, and the stderr gate-facts payload is the new parsed channel. Yet every NFR-9 fixture is a complete spec, so the test script never observes exit `2` or its payload. AC-71 names no fixture ("a target spec with an unchecked task"). Cheap fixtures already exist: `specs/0005-defer-message-on-error/` (no `tasks.md`, on `master`) covers the "absent" case, and a two-line synthetic `tasks.md` would cover the unchecked case.

**Evidence**: NFR-9 fixture table (seven rows, none incomplete); AC-79 asserts "zero unchecked in each".

**Recommendation**: Add at least one exit-`2` fixture row (absent `tasks.md`, and unchecked tasks with titles) that asserts status `2`, no ledger, and the parsed gate facts.

---

### 11. AC-84 gives no way to construct its Given (Score: 55)

The new AC needs "a spec whose PR is discovered but whose `headRefOid` names a commit that is not present in the local repository". No fixture or setup is specified. AC-47 shows the document knows how to specify a constructed setup. This one needs either a disposable clone that predates the PR head, or a stubbed `gh`.

**Evidence**: AC-84 (whole text); compare AC-47's "constructed setup step".

**Recommendation**: State the setup, e.g. a disposable clone whose remote-tracking spec branch is reset behind the PR head with the PR-head objects absent, or a `PATH`-shimmed `gh` returning a known absent sha.

---

### 12. C-8 requires every fixture cited by a criterion to resolve under FR-10, but two of AC-79's fixtures do not (Score: 50)

C-8: "**A fixture cited by any criterion must resolve under FR-10 on the branch the criterion is run on**." `specs/0033-pg-advisory-lock-sha256/` and `specs/0002-sqs-cleanup/` have no `spec/…` branch, local or remote. Neither matches the checked-out branch, and neither has commits in `origin/master..HEAD`, so FR-10 finds no branch for either. AC-79 works anyway, because it asserts only task and id fields. The constraint as written is still violated.

**Evidence**: `git rev-parse --verify -q spec/pg-advisory-lock-sha256` and `spec/sqs-cleanup`, local and `origin/` forms: all empty; `git log origin/master..HEAD -- {dir}`: empty. `spec/show-me-fixture` does exist for 9999.

**Recommendation**: Limit C-8's rule to criteria that assert branch- or diff-derived values, or state that AC-79's fixtures are expected to resolve to "not determinable".

---

### 13. The "Open design question" paragraph is stale: the command now reads the diff itself, which crosses ADR 0072's Measurer/Synthesiser line too (Score: 50)

The paragraph says FR-6's diagram is "the one part of `show-me.md` that may need to read source code the rest of the command never opens". It presents the diagram as the only thing crossing ADR 0072's line. After this amendment, the command itself issues the `src/`-scoped `git diff` (FR-4, FR-14, NFR-3's 303,715 B). ADR 0072 forbids that: its Synthesiser "runs no measurement", and its line 334 keeps every `git diff` in the script. The design question now has two crossings, and the requirements name only one.

**Evidence**: requirements lines 1981–1996; ADR 0072 lines 142, 147, 334.

**Recommendation**: Add the command's own scoped diff reads to the open question handed to the ADR amendment.

---

### 14. NFR-2's ceiling arithmetic still budgets "three" Blast-radius provenance lines at ≤ 70 words; FR-10 now permits up to five (Score: 45)

The amendment added the PR-head-differs line, the row-16 line (about 25 words) and the PR-count line. The sum still fits under 2,000 (headroom 44), but the stated line count and the ≤ 70 figure are stale.

**Evidence**: NFR-2 line 1039–1040; FR-10 lines 608–616; FR-20 PR-count line.

**Recommendation**: Recount the lines and restate the figure.

---

### 15. The word-count mode's exit and parse semantics are not covered by the three-meaning exit list (Score: 45)

"Its exit status has exactly three meanings" defines `0` and `2` in terms of the FR-3 precondition, which does not apply to the second, word-count invocation. The text also does not say what happens on exit `0` with an unparseable stderr result.

**Evidence**: FR-21 lines 798–806 compared with *Modes* lines 834–846.

**Recommendation**: Scope the three-way list to the measuring invocation, and give the word-count invocation its own two-way rule, including "unparseable ⇒ `word count unavailable`".

## Pass-8 disposition

| Pass-8 # | Resolved? | Note |
|---|---|---|
| 1 (exit-code contract) | Yes | Three-way status defined, and AC-71/72 updated. Residue: NFR-8 and the stop message conflict with state 5 (finding 6); stderr channel (finding 5) |
| 2 (`git merge-base` not allow-listed) | Yes | Moved into the script; FR-18/C-10 narrowed; AC-30 checks the command never issues it |
| 3 (`gh pr diff` has no pathspec) | Yes | `gh pr diff` dropped. The replacement introduces the merged-PR empty-diff defect (finding 2) |
| 4 (second diff fetch has no source) | Yes | Command reads scoped `git diff` over the pinned shas; FR-4/FR-14 updated |
| 5 (declared-id regex dialect/anchor) | Regressed | Dialect and anchor added, but heading and ordered-list declarations are now excluded (finding 1) |
| 6 (C-8 markers) | Yes | AC-77/AC-79 marked; C-8 enumeration removed |
| 7 (checkbox pattern) | Yes | POSIX form in a fenced block; in the exactly-once list |
| 8 (word count not acted on) | Yes | Report-and-continue stated; FR-19/AC-31/AC-33 updated. New "met by construction" overclaim (finding 7) |
| 9 (unnamed fixtures) | Yes | Word-count fixtures and FR-13 line named; AC-83 changed to inspection with a stated reason |
| 10 (F2 determinism claim) | Yes | FR-7 now narrows to the read decision |
| 11 (Proposed Solution stale) | Yes | New "What this spec delivers" paragraph |
| 12 (tag extraction rule) | Yes | Tag pattern with `[+]`, `untagged` defined, per-tag fixture figures verified |
| 13 (28 vs 37) | Yes | NFR-2 and FR-11 now say 37; historical 28 attributed to 0037 |
| 14 (BSD grep claim) | Yes | False clause deleted; replacement wording accurate |
| 15 (FR-15 "read in full") | No | Unchanged (finding 8) |
| 16 (interpreter-reads-script ambiguity) | No | Unchanged; below threshold, not re-filed |

## Verification log

| Check | Command | Result |
|---|---|---|
| Amendment scope | `git diff 5e0737fe5~1 5e0737fe5 --stat` | requirements.md only, +286/−138 |
| `headRefOid` valid field and PR #4282 head | `gh pr list --head spec/scoped-lifetime-per-pipeline --state all --json number,url,headRefName,headRefOid,createdAt,state` | valid; #4282 OPEN, head `91d549be6`, equal to local and origin branch tips |
| Calibration merge base | `git merge-base origin/master origin/spec/scoped-lifetime-per-pipeline` | `6145913a0`, correct (local `master` is at `09f5d988f`; origin/master is used, as Base ref says) |
| Blast-radius buckets over `6145913a0..91d549be6` | `git diff --name-only` bucketed | 517; src 76, tests 393, specs 24, docs 14, other 10 (.github 0): **correct** |
| `src/` subdirectories / commits | awk distinct `$2`; `git rev-list --count` | 6 / 363: **correct** |
| Byte figures | `git diff … \| wc -c` full / `-- src/`; `--stat` | 4,081,673 / 303,715 / 31,929: **exact** |
| Public-API count (info) | `git diff … -- src/ \| grep -cE` stated pattern | 131 |
| Fixture figures, BSD grep 2.6.0 and ugrep 7.8.4 (the machine's `grep`) | stated checkbox, tag and declared-id patterns | 9999: 2/0/3, 2 untagged; 0033: 5/0/8, 3 T+I + 2 untagged; 0002-sqs: 6/0/0, 5 T+I + 1 untagged; 0036: 82/0/37, 62/12/2/6/0; 0037: 30 ids: **all match NFR-9**. GNU grep not installed, so the document's "GNU grep" claim was not independently verified |
| Declared-id pattern vs other `master` specs | pattern compared with `^#+ (FR\|NFR)-n` over `specs/*/requirements.md` | 0023-Pipeline-Validation 0 vs 19 headings; 0023-box_database_migration 0 vs 9; 0021-Error-Examples 0 (`1. **FR-1**`); 0004 0 (`- FR-1:`); 0020 3 (`1. **FR-1:`): **finding 1** |
| Merged-PR behaviour | `gh pr view 4039`; `git merge-base origin/master ce8bb9ecf` | merge base = head; 0 files: **finding 2** |
| POSIX classes in Python/.NET | `python3 re.search`; `dotnet run rx.cs` | both False (grep -E: 1); Python writes FutureWarning to stderr: **findings 3, 5** |
| `gh` stderr on auth failure | `GH_TOKEN=bad gh pr list … 1>/dev/null` | "HTTP 401: Bad credentials" on stderr: **finding 5** |
| Allow-list | python over `.claude/settings.json` | git: status/log/diff/show/branch/remote/tag/blame/describe/rev-parse/ls-files/config --get; gh: pr view/list/diff, issue view/list; `Bash(wc:*)` present; no interpreter or path-scoped entries; no merge-base/check-ignore. deny has curl/wget/ssh. **FR-18/C-10 claims correct** |
| `.gitignore` | `grep -nE 'PROMPT\|sqlite\|show-me'` | `*.sqlite` 255, `PROMPT.md` 353, `PROMPT-*.md` 354, no ledger entry yet (expected) |
| 9999 fixture tracking | `git ls-files -s`; `git cat-file -e HEAD:…` | in the index (so "tracked") but **not committed at HEAD**; branch `spec/show-me-fixture` (local and origin) exists, so it resolves under FR-10 |
| 0033 / 0002-sqs resolvable under FR-10 | `git rev-parse --verify` on `spec/…` and `origin/spec/…`; `git log origin/master..HEAD -- dir` | neither resolves: **finding 12** |
| Checkboxes inside fences | awk over all `specs/*/tasks.md` | none: no false gate hits |
| Tag-pattern coverage | per-spec counts | template style matches; 0037's own `**T1.1 — STRUCTURAL:` style gives 40 untagged (by rule; not filed) |
| ADR 0072 vs requirements | `grep -n` Measurer/Synthesiser/`gh pr diff` | ADR still has `gh pr diff` and "Synthesiser runs no measurement" (design-phase staleness; finding 13 covers the requirements side) |
| FR-16 rows → ACs | manual | rows 1,2,5,5a,6–16 each have an AC (row 16 → AC-84) |
| Stale `gh pr diff` / exit / four-item references | `grep -nE` | none stale; all remaining `gh pr diff` mentions are "not used" statements or the AC-81 `if`-in-`diff` fixture |
| Main-agent spot check of findings 1 and 2 | re-ran the declared-id pattern over `specs/0023-Pipeline-Validation-At-Startup` and `specs/0020-DontAckAction`; `gh pr view 4039` + `git merge-base origin/master {head}` | 0 ids vs `#### FR-1:` headings; 0020 matches only 3 ids; merge base of merged PR #4039 = its own head: **both confirmed** |

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 3 |
| 50-69 (Medium) | 10 |
| 0-49 (Low) | 2 |

**Total findings**: 15
**Findings at or above threshold (60)**: 7
