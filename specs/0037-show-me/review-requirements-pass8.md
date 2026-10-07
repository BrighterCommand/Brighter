# Review: requirements — 0037-show-me (pass 8)

**Date**: 2026-09-22
**Threshold**: 60
**Verdict**: NEEDS WORK

14 findings at or above threshold 60. Address these before approving.

**Context**: this is the first review of the post-amendment document (`c32117d97`, `0fe1deb85`,
`d1110292a` — the measurement script, the byte budget, the ledger contract). The
`.requirements-approved` marker on disk predates all three and is stale. Passes 1–7 reviewed earlier
baselines and are not superseded by this file.

## Findings

### 1. FR-21's exit-code contract has no legal state for FR-3's gate-failure path, and AC-71 is unsatisfiable as written (Score: 88)

FR-3's new *Ordering* paragraph makes the script run **before** the gate and says that on a failing gate the script writes no ledger and reports the gate facts on stderr. FR-21 then defines the exit code as a two-valued function of whether a ledger was written, and defines *any* non-zero exit as a tooling fault with a fixed message. The incomplete-spec case therefore has no conforming outcome:

- Script exits **non-zero** (it wrote no ledger, as the contract says it must in that case) → FR-21's failure mode fires and the command prints `/spec:show-me could not run its measurement script (…): … exited {code}. No file was written. **This is a tooling fault, not a fault in spec {dir}** — re-run after restoring the script.` That is the opposite of the truth and the opposite of what FR-3 requires.
- Script exits **`0`** having written no ledger → it violates FR-21's own sentence, and the command then finds no parseable ledger → FR-21's `ledger was not a single JSON object` stop fires. Wrong message again.

No third exit state is defined anywhere in the document (FR-21 is the only place exit codes are discussed).

**Evidence**:
- FR-21: "It exits `0` when it wrote a complete, parseable ledger and non-zero otherwise, so the exit code — not any parsing of the script's output — is what tells the command whether to proceed."
- FR-21: "If the script is absent, is unreadable, **exits non-zero**, or exits `0` having written a ledger the command cannot parse … the command **stops without writing or modifying any file** … and prints exactly: `… This is a tooling fault, not a fault in spec {dir} …`"
- FR-3 (Ordering): "on a spec that fails this gate the script writes **no** ledger at all, reporting the gate facts on its standard error stream instead (FR-21)".
- AC-71: "**Given** a target spec with an unchecked task … **then** it prints the FR-3 stop message" — directly contradicted by AC-72, which requires FR-21's stop message for a non-zero exit.

**Recommendation**: define a **third** exit status for "ran correctly, gate not passed" (e.g. exit `2` = gate facts on stderr, no ledger, not a tooling fault), and rewrite FR-21's exit sentence as a three-way mapping. Then state explicitly in FR-3 and FR-21 that the command distinguishes gate-stop from tooling-stop by that code, and add the code to AC-71 and AC-72.

---

### 2. FR-18's claim that every git command the document names is already allow-listed is factually false — `git merge-base` is not in `.claude/settings.json` (Score: 85)

FR-18 asserts, as a verified fact, that the only allow-list change this spec needs is the one path-scoped script entry. It is wrong: the Definitions table defines *Merge base* as `git merge-base <base ref> <spec branch tip>`, FR-10 requires the merge-base sha to be printed in two places, and `git merge-base` has **no** allow-list entry. `git check-ignore -v`, which AC-30 and AC-74 name as their verification mechanism, is also absent.

This is load-bearing: C-10 says the narrow entry exists "because `/spec:show-me` is meant to complete in one turn **without a permission prompt in the middle of a measurement**" — and a `git merge-base` call would produce exactly that prompt. AC-82 asserts the settings file gains **exactly one** entry that was not present before, so a correct implementation either fails AC-82 or prompts mid-run.

**Evidence**:
```
$ grep -cE 'merge-base|check-ignore' .claude/settings.json
0

$ python3 -c "import json;d=json.load(open('.claude/settings.json'));print([e for e in d['permissions']['allow'] if e.startswith('Bash(git'))])"
['Bash(git status:*)','Bash(git log:*)','Bash(git diff:*)','Bash(git show:*)','Bash(git branch:*)',
 'Bash(git remote:*)','Bash(git tag:*)','Bash(git blame:*)','Bash(git describe:*)','Bash(git rev-parse:*)',
 'Bash(git ls-files:*)','Bash(git config --get:*)']
```
FR-18's text: "every `git` command this document names is already covered by the existing `git status`/`log`/`diff`/`show`/`branch`/`rev-parse`/`ls-files` entries. **No other addition, removal or widening of the allow-list is in scope.**"
Document line 88: "| **Merge base** | `git merge-base <base ref> <spec branch tip>`. |"

**Recommendation**: either (a) correct FR-18/C-10/Out-of-Scope/AC-82 to account for a second added entry (`Bash(git merge-base:*)`, and `Bash(git check-ignore:*)` if the AC verification is to run unprompted), or (b) redefine *Merge base* in terms of an already-permitted command and say so. Do not leave the false "already covered" claim standing.

---

### 3. NFR-3's diff ban makes FR-20's preferred PR-diff source unusable — `gh pr diff` accepts no pathspec (Score: 82)

NFR-3 states an absolute ban: no `git diff` **or `gh pr diff`** may be issued "without either a pathspec restricting it or a summary-only flag". `gh pr diff` has no pathspec parameter at all — its only flags are `--allow-escape-sequences`, `--color`, `--exclude`, `--name-only`, `--patch`, `--web` (verified). So the **only** NFR-3-legal form of `gh pr diff` is `--name-only`, which yields file names and nothing else: no `+`/`-` content lines, hence no public-API declaration-line count, and no net-lines-per-bucket.

But FR-20 makes the PR diff the winner whenever `gh pr diff {n}` succeeds, FR-10 requires net lines and the public-API count from "the spec diff", and FR-21 lists both among the script's mandatory ledger fields. On the document's own calibration case this is not hypothetical: spec 0036 has PR #4282, so the PR diff wins, and its full patch is 4,081,673 bytes — which NFR-3 itself says "exceeds the context window outright".

Note also that NFR-3's worked affordability example silently assumes the *losing* source: it budgets "the `src/`-scoped diff — 303,715 B", which is a `git diff … -- src/` construct, for a spec whose PR exists.

**Evidence**:
```
$ gh pr diff --help
USAGE  gh pr diff [<number> | <url> | <branch>] [flags]
FLAGS
      --allow-escape-sequences
      --color string
  -e, --exclude patterns   Exclude files matching glob patterns from the diff
      --name-only
      --patch
  -w, --web
```
NFR-3: "**The full diff is banned** … No `git diff` or `gh pr diff` may be issued without either a pathspec restricting it or a summary-only flag."
FR-20: "when a PR is discovered **and** `gh pr diff {n}` succeeds, the PR diff **is** the spec diff."

**Recommendation**: state explicitly how a PR-sourced diff is bounded — name `--exclude` (and which patterns) as the PR-source equivalent of a pathspec, or say that the PR source is used for identity/metadata while a pathspec-scoped `git diff` supplies the measured text, and reconcile that with FR-20's "the two are never mixed". Also decide, and state, whether NFR-3's ban applies to a command the *script* issues and pipes to a counter without bringing bytes into context — the charged-bytes definition says the script's unemitted reads cost nothing, which appears to exempt it, while the ban's wording does not.

---

### 4. The ledger carries no diff text, `gh pr diff` may run at most once, and NFR-3 still charges the command a 303,715-byte diff read — that second fetch has no legal source (Score: 80)

FR-4 justifies the ledger's existence as preventing a second remote fetch, then immediately removes the only thing that would: "It holds the measured JSON only: it **never caches fetched diff text**." FR-18 and AC-30/AC-73 then bind `gh pr diff` to **at most once per run**.

Yet the command demonstrably needs diff text of its own: FR-14 must list 3–7 paths from the spec diff with per-path reasons (judgement, so not in the ledger — FR-21's field list carries counts only, no changed-file list); FR-7 derives breaking changes partly from "the public-API declaration lines in the spec diff"; and NFR-3's own worst-case accounting explicitly charges **"the `src/`-scoped diff (303,715)"** to the command's byte budget. With the script's single `gh pr diff` already spent, the command can only get that text by (a) a second `gh pr diff` — forbidden by FR-18 and AC-73 — or (b) a `git diff`, which is a *different* source from the one measured, forbidden by FR-20's "Exactly one source is used and named; the two are never mixed or averaged."

**Evidence**:
- FR-4: "exists so that one run resolves and fetches each remote input at most once rather than invoking `gh pr diff` a second time … It holds the measured JSON only: it never caches fetched diff text."
- FR-18: "Each of those two `gh` commands is invoked **at most once per run**".
- AC-73: "`gh pr diff` was invoked **at most once**".
- NFR-3: "the `src/`-scoped diff (303,715) … = **694,377 B**" charged to the run.

**Recommendation**: resolve the seam explicitly. Either permit the ledger to carry the scoped diff text (and raise/replace FR-21's 65,536-byte cap accordingly), or state that the script writes the scoped diff to a second, separately-named working file (which would change FR-4's "exactly two paths are written"), or relax `gh pr diff` to "at most twice" and fix AC-30/AC-73. Whichever is chosen, FR-21's field list must be extended with the changed-file list that FR-14 needs.

---

### 5. FR-8's declared-id regex is given with no dialect and no anchor, while FR-21 now requires it implemented exactly once (Score: 78)

The document spends a full page fixing the dialect and escaping of the **public API declaration line** pattern, and FR-21 names FR-8's declared-id regex as the *second* pattern implemented exactly once in the script. That second pattern gets none of the same care, and it is under-specified in two independent ways.

**(a) No dialect.** The pattern is written `\b(FR|NFR)-(\d+)\b`. `\b` and `\d` are not POSIX ERE. The public-API rule states its dialect ("**POSIX extended**, evaluated as `grep -E`") precisely because the document says a pattern without one is not portable; this one carries no dialect at all, and the language of the script is deliberately unfixed, so the implementer has no way to know which engine's semantics the requirement means.

**(b) No anchor, while the prose requires one.** FR-8 says an id counts only when it "appears at the **start** of a requirement's heading or bold lead-in (`**FR-7 — …**`), **not merely cross-referenced** in another requirement's prose", and AC-15 asserts that cross-references declare nothing. The regex as stated has no anchor and matches every occurrence anywhere. NFR-1 then leans on the regex as the whole definition — "the ids are extracted by FR-8's stated regex over `requirements.md`, which is a match, not a judgement" — which is false of the pattern as printed. Measured on this document: the unanchored pattern yields **540** matches; a declaration-anchored pattern yields **30**.

**Evidence**:
```
$ grep -oE '(FR|NFR)-[0-9]+' specs/0037-show-me/requirements.md | wc -l
     540
$ grep -oE '\*\*(FR|NFR)-[0-9]+' specs/0037-show-me/requirements.md | sort -u | wc -l
      30
```
FR-8: "a numbered requirement is an id matching `\b(FR|NFR)-(\d+)\b` that is **declared** in the spec's `requirements.md`".
FR-21: "The **Public API declaration line** rule's POSIX extended pattern (Definitions) and **FR-8's declared-id regex** are each implemented **once**, in this script".

**Recommendation**: give the declared-id pattern the same treatment as the public-API pattern — state it in a fenced block, name its dialect, and write the *anchored* form that actually implements "declared" (something matching a line-leading `**FR-n`/heading form). Then fix NFR-1's sentence so it refers to the anchored pattern.

---

### 6. AC-77 and AC-79 depend on the spec-0036 calibration fixture but carry no `(C-8)` marker, and C-8's enumeration is now stale (Score: 72)

Pass 7's finding #4 was exactly this defect class and was addressed before approval; the amendment reintroduced it. C-8 states that "**the inline *(C-8)* marker on each such AC is the single source of truth** for which criteria this covers", and then enumerates the marked set as "(AC-1, AC-2, AC-4, AC-12, AC-17, AC-18, AC-20, AC-47, AC-51, AC-56)".

- **AC-77** (new, `*(NFR-3, FR-10, FR-20)*`) has as its Given "a spec whose diff touches 517 files and whose full diff is 4,081,673 bytes" — uniquely spec 0036 on the calibration branch — and carries **no** `(C-8)` marker. Run on `master` its Given state is unreachable.
- **AC-79** (new, `*(NFR-9)*`) asserts the 0036 figures "37 … 82" and mentions C-8 only in its body prose, not its citation.
- **AC-78** (new) *is* marked `(C-8)`, so C-8's own parenthetical list is now incomplete.

**Evidence**:
```
$ grep -nE '^\*\*AC-[0-9]+\*\* \*\([^)]*C-8' specs/0037-show-me/requirements.md
1248:**AC-1  1261:**AC-2  1269:**AC-4  1306:**AC-12  1339:**AC-17  1345:**AC-18
1360:**AC-20  1510:**AC-47  1528:**AC-51  1571:**AC-56  1726:**AC-78
```
AC-77's citation line 1720: `**AC-77** *(NFR-3, FR-10, FR-20)*` — no C-8.
AC-79's citation line 1733: `**AC-79** *(NFR-9)*` — no C-8.

**Recommendation**: add `(C-8)` to AC-77 and AC-79, and update C-8's parenthetical list to include AC-77, AC-78 and AC-79 (or delete the enumeration entirely, since C-8 already says the inline markers are authoritative).

---

### 7. The Task checkbox pattern is the third mechanically critical regex, lives in a markdown table cell in non-POSIX form, and FR-21 omits it from the "implemented exactly once" rule (Score: 70)

FR-21's exactly-once rule names two patterns. It misses the one the script needs most: the *Task checkbox* pattern, which produces FR-3's gate facts, FR-9's task total and NFR-9's per-fixture checkbox assertions. That pattern is stated **inside a Definitions table cell** — the exact location the document elsewhere says a pattern must never live — and is written `^\s*-\s\[[ xX]\]`, using `\s`, which the document's own portability paragraph calls out as a non-POSIX construct. No dialect is named for it.

**Evidence**:
Definitions, line 84: "| **Task checkbox** | A line in `tasks.md` matching `^\s*-\s\[[ xX]\]`. |"
The document's own rule, lines 113–116: "Implementations must use the extended form above verbatim, **inside a fenced block rather than a table cell** … The pattern is implemented in exactly one place — FR-21's measurement script — and **never transcribed into … a table cell**, because a pattern carried in markdown prose acquires that prose's escaping and a pattern written twice drifts into two patterns."
FR-21 lists only: "The **Public API declaration line** rule's POSIX extended pattern … and FR-8's declared-id regex".

**Recommendation**: add the checkbox pattern to FR-21's exactly-once list, restate it in POSIX ERE (`^[[:space:]]*-[[:space:]]\[[ xX]\]`) in a fenced block outside the table, and have the Definitions row point at that block rather than carrying the pattern.

---

### 8. Nothing acts on NFR-2's word-count result — the check is executed and then discarded (Score: 70)

NFR-2 was amended to claim the budget is now enforced rather than aspirational: "**FR-21's measurement script is that script** — it applies this rule to the generated file in its word-count mode and reports the total, **so the check is executed rather than merely defined**." But no requirement says what happens when the reported total is outside 400–2,000.

The gap is closed off from both sides. FR-13 requires the command to "always write the file, always complete its run and report per FR-19 … with **no error message and no refusal**". FR-19's report is fixed at four items (path, created/replaced, level, advisory reminder) and does not include the word count. FR-21's *Modes* caps the script at "**at most twice per run**", so a rewrite could not be re-checked even if one were permitted. AC-33 asserts the count is in range, but no requirement gives the command any means of making that true.

**Evidence**: NFR-2 (quoted above); FR-21 *Modes*: "once after it is written, to apply NFR-2's mechanical word-count rule … and report — on standard error … the counted total, the number of excluded fenced-block lines, and **whether the total is inside the 400–2,000 range**."

**Recommendation**: state the consequence. Either (a) the command revises the narrative and re-checks (which requires raising the "at most twice" cap and adding an AC for the revise-and-recheck loop), or (b) the result is reported to the caller as part of FR-19 and the run completes regardless — in which case say so, and soften AC-33 to assert the *report*, not the conformance.

---

### 9. AC-80, AC-81 and AC-83 each require a fixture NFR-9 does not name (Score: 68)

NFR-9 says the test script "invokes the measurement script against **named, tracked fixtures**" and then names four spec directories. Three of the new acceptance criteria need fixtures that are nowhere named:

- **AC-80** needs "a `show-me.md` whose counted body holds exactly **8** tokens outside its fenced blocks and whose single fenced block holds **8** further tokens", and then "the same file … grown to a conforming body". No such fixture exists or is specified, and none of the four named spec directories has a `show-me.md`.
- **AC-81** needs "a fixture line containing `git diff` and `gh pr diff` and no conditional".
- **AC-83** needs "a measurement script that **fails partway through** writing its ledger" — a deliberately broken variant of the delivered script.

NFR-9's "at minimum" leaves room to add fixtures, but a test cannot be written from a Given whose fixture is unspecified, and NFR-9's *two regressions it must specifically pin* names the word-count case (AC-80) as one of the two things the test exists for.

**Evidence**: NFR-9 fixture table lists only `specs/9999-show-me-fixture/`, `specs/0033-pg-advisory-lock-sha256/`, `specs/0002-sqs-cleanup/`, `specs/0036-scoped-lifetime-per-pipeline/`. All four resolve and all four measured figures are correct (see verification log) — the gap is the three unnamed ones.

**Recommendation**: extend NFR-9's fixture table with the word-count fixture, the FR-13-invariant fixture line, and the failure-injection mechanism AC-83 needs (or state that AC-83 is verified by inspection of the atomic-write mechanism rather than by the test script).

---

### 10. FR-7 claims the mandatory `release_notes.md` read "makes F2 deterministic"; NFR-1 explicitly says F2 is not required to be identical between runs (Score: 65)

FR-7's justification for making the read non-optional asserts determinism for F2. NFR-1's second list — the judgement-derived fields — names F2 first, and says so twice more ("Because F2 and F5 feed FR-12's maximum, the **overall level may in principle vary with them**"). The two statements cannot both be true.

**Evidence**:
- FR-7: "Presence and reading are therefore the same state … and **what makes F2 deterministic between runs (NFR-1)**: two runs over the same tree cannot reach different item counts by one of them declining to read."
- NFR-1: "The remaining fields are **judgement-derived and are not required to be identical between runs**: — FR-7's breaking-change item list and its count (**and therefore F2**);"

**Recommendation**: narrow FR-7's claim to what it actually establishes — that the *read decision* is deterministic, removing one source of variance — and drop the "makes F2 deterministic" phrasing and the `(NFR-1)` citation that endorses it.

---

### 11. The Proposed Solution still describes the pre-amendment command: "writes one markdown file", no ledger, no measurement script, no test script, no settings or gitignore change (Score: 62)

The Proposed Solution is the section a reader reaches first, and it survived all three amendments untouched. It describes the command as reading three artefact kinds and writing one file. The document now delivers **three** artefacts (NFR-6), writes **two** files (FR-4), requires a `.gitignore` line and an allow-list entry (Out of Scope), and makes `release_notes.md` a mandatory read (FR-7) — none of which appears.

**Evidence**: Proposed Solution, lines 49–51: "It reads the spec's own artefacts (ADRs, `requirements.md`, `tasks.md`), its git history, and — when it exists — its pull request's diff, **and writes one markdown file**, `specs/NNNN-name/show-me.md`, containing:". Compare FR-4's heading: "**The command writes exactly two files** — the deliverable `show-me.md` and the gitignored *fact ledger*".

**Recommendation**: add a short paragraph to the Proposed Solution naming the three delivered artefacts, the two written files, the mandatory `release_notes.md` read, and the two single-line configuration changes.

---

### 12. FR-9's task-type tags have no extraction rule, yet FR-21 owns the counts and NFR-1 requires them deterministic (Score: 62)

FR-9 requires "the count per task-type tag found in `tasks.md` (`TEST + IMPLEMENT`, `STRUCTURAL`, `PROJECT`, `DOC`, plus `untagged`)" and NFR-1 puts the per-tag counts in the mechanically-deterministic list, produced by FR-21's script. But no rule says how a tag is recognised — whether it must be inside `**…**`, whether it must be followed by `:`, whether the match is case-sensitive, or how a checkbox line with none of the four is classified `untagged`. C-2 gives a single style example, not a rule. Two implementers will disagree on, for example, a line containing the words "TEST" and "IMPLEMENT" separately, or a `DOC` appearing in a task title.

There is a further escaping trap the document has not noticed: `TEST + IMPLEMENT` contains `+`, a quantifier in ERE — precisely the class of defect FR-21's exactly-once rule and NFR-9's `\|` regression exist to catch, and neither covers it.

**Evidence**: FR-9 (quoted); NFR-1: "the task total and **per-tag counts**, and the commit count (both `## How it was built`, FR-9);"; FR-21: "`tasks.md`'s checkbox total, checked count, unchecked count, first three unchecked titles and **per-tag counts**". No AC asserts a per-tag breakdown value, and NFR-9's fixture table asserts none.

**Recommendation**: state the tag-recognition rule as a pattern (fenced, dialect named, added to FR-21's exactly-once list), define `untagged`, and add at least one per-tag assertion to NFR-9's fixture table.

---

### 13. The calibration case's declared-id count is stated as 28; spec 0036 declares 37 (Score: 60)

NFR-2's 2,000-word ceiling arithmetic is explicitly computed "for the calibration case", and the calibration case is spec 0036 throughout the document (C-8, the *Worked example* section). Its declared-id figure is wrong. FR-11's worked example makes the same substitution, mixing 0036's real F1/F2 values with a non-0036 denominator.

**Evidence**:
```
$ grep -oE '\*\*(FR|NFR)-[0-9]+' specs/0036-scoped-lifetime-per-pipeline/requirements.md | sort -u | wc -l
      37
```
NFR-2 line 928: "FR-8's shipped-as-planned line (≤ 40) and its deviation entries (uncapped in count but, for **28 declared ids** with at most a handful of deviations in practice, ≤ 150)".
FR-11 line 586: "(e.g. `76 files under src/`, `14 items`, `2 deviation entries of **28** requirements: …`)" — the first two figures are spec 0036's, verified; the third is not.
NFR-9's own fixture table, measured 2026-09-21, gives 0036 as **37** — so the document contradicts itself.

**Recommendation**: change both to 37, or state which spec the 28 refers to. (NFR-9's separate historical note at line 1100, "the pattern once returned **0** where the answer was **28**", is consistent with spec 0037's own pre-amendment id count and needs no change beyond saying which document it was measured on — 0037 now declares 30.)

---

### 14. The document's stated reason for choosing POSIX ERE — that `\s`/`\b` are "absent from the BSD `grep` that macOS ships" — is false as measured (Score: 60)

The paragraph fixing the public-API pattern gives two justifications. The first is correct; the second is not. This matters beyond cosmetics: the false half is what an implementer would rely on when deciding how much dialect care the *other* patterns need (findings 5 and 7), and it makes the whole paragraph look less trustworthy than it is.

**Evidence**:
```
$ /usr/bin/grep --version
grep (BSD grep, GNU compatible) 2.6.0-FreeBSD

$ /usr/bin/grep -cE '\bpublic\b' api.txt      # \b works
1
$ /usr/bin/grep -cE '^[+]\s+public' api.txt    # \s works
1

# the FIRST justification does verify:
$ /usr/bin/grep -c '^[+-][[:space:]]*(public|protected)[[:space:]]' api.txt   # BRE, unescaped
0
$ /usr/bin/grep -cE '^[+-][[:space:]]*(public|protected)[[:space:]]' api.txt  # ERE
2
```
Document, lines 109–111: "Written for a *basic*-regex `grep`, the parentheses and an escaped `\|` are read as literal characters and the pattern matches neither declaration; **and `\s`/`\b` are GNU extensions absent from the BSD `grep` that macOS ships.**"

**Recommendation**: delete or correct the second clause. The BRE justification stands on its own and is sufficient.

---

### 15. FR-15's diagram-source row is defined only for files "read in full", but NFR-3's degradation permits targeted extraction (Score: 55)

FR-15 requires "one row per **source file read in full** for a diagram (FR-6)". NFR-3's *Degradation* rule says a file whose measured size exceeds the bytes remaining "is **not** read in full; it is read by targeted extraction or bounded chunks instead". A diagram source read by extraction therefore gets no `## Inputs used` row — while AC-56 asserts that every symbol a diagram names "appears in the spec diff, in one of the seven ADRs, **or in a file recorded as read in `## Inputs used`**". That assertion is unsatisfiable for an extraction-read source, and NFR-7's traceability obligation has a matching hole.

**Evidence**: FR-15 and NFR-3 *Degradation* as quoted; AC-56 (quoted).

**Recommendation**: change FR-15's row to "one row per source file read for a diagram, marked `used` or `used (targeted extraction)`", so every diagram source is recorded regardless of how it was read.

---

### 16. FR-18's "entry naming the interpreter rather than the program" is ambiguous for the interpreter-reads-script form NFR-6 explicitly contemplates (Score: 45)

NFR-6 anticipates a 100644 artefact "where an interpreter is named and reads it, as `.claude/commands/adr/generate_adr_index.awk` is" (verified: that file is mode `100644`). For that form the allow-list entry must necessarily begin with the interpreter. FR-18's absolute phrasing ("an entry naming the *interpreter* rather than the *program*") and AC-82's "no added entry permits running arbitrary programs in the script's language" are both satisfied by such a string, but a careful reader could conclude the interpreter-reads-script form is forbidden outright.

**Evidence**:
```
$ git ls-files --stage .claude/commands/adr/generate_adr_index.awk
100644 500691c06c6cc9080c56effd6c8c5d4fefa0a091 0 .claude/commands/adr/generate_adr_index.awk
```
FR-18: "**A bare interpreter grant is forbidden** … an entry naming the *interpreter* rather than the *program* would confer arbitrary command execution".

**Recommendation**: add one clause: "an entry whose string begins with an interpreter is permitted so long as the script path is part of the fixed prefix — what is forbidden is a prefix that ends at the interpreter."

## Verification log

| Check | Command | Result |
|---|---|---|
| NFR-9 fixture 1 | `grep -c` on `specs/9999-show-me-fixture/tasks.md`; declared ids in its `requirements.md` | 2 checkboxes, 0 unchecked; 3 declared ids (FR-1, FR-2, NFR-1) — **table correct** |
| NFR-9 fixture 2 | `specs/0033-pg-advisory-lock-sha256/` | 8 declared ids; 5 checkboxes, 0 unchecked — **correct** |
| NFR-9 fixture 3 | `specs/0002-sqs-cleanup/` | 0 declared ids; 6 checkboxes, 0 unchecked — **correct** |
| NFR-9 fixture 4 | `specs/0036-scoped-lifetime-per-pipeline/` | 37 declared ids; 82 checkboxes, 0 unchecked — **correct** (and contradicts the "28" in NFR-2/FR-11, finding 13) |
| Spec 0037's own declared ids | `grep -oE '\*\*(FR\|NFR)-[0-9]+' … \| sort -u \| wc -l` | **30** |
| Blast-radius figures | `git diff --name-only 6145913a0..spec/scoped-lifetime-per-pipeline` bucketed | 517 total; src 76, tests 393, docs 14, specs 24, .github 0, other 10 — **all correct, sums to 517** |
| `src/` subdirectory count | `awk -F/ 'NF>2{print $2}' \| sort -u \| wc -l` | **6** — correct |
| Commit count | `git rev-list --count 6145913a0..spec/scoped-lifetime-per-pipeline` | **363** — correct |
| NFR-3 byte figures | `git diff … \| wc -c` (full, `-- src/`, `--stat`); `wc -c` on tasks.md, requirements.md, release_notes.md | 4,081,673 / 303,715 / 31,929 / 229,159 / 273,674 / 118,145 — **every figure exact** |
| Allow-list `gh` entries | `.claude/settings.json` | exactly `gh pr view/list/diff`, `gh issue view/list` — **C-10 correct** |
| `Bash(wc:*)` present | same | **present — FR-18/C-10 correct** |
| No interpreter entries | same | no `bash`/`sh`/`awk`/`python3`/`dotnet run` (only `dotnet --version/--info/--list-*`) — **C-10 correct** |
| `deny` list | same | contains `Bash(curl:*)`, `Bash(wget:*)`, `Bash(ssh:*)` — **FR-18's rationale correct** |
| `git merge-base` allow-listed | `grep -cE 'merge-base\|check-ignore' .claude/settings.json` | **0 — NOT PRESENT — finding 2** |
| `.gitignore` PROMPT entries | `grep -nE 'PROMPT' .gitignore` | `PROMPT.md` (exact) at 353, `PROMPT-*.md` (glob) at 354 — **FR-17 correct (pass 7 #1 fixed)** |
| `.gitignore` `*.sqlite` | same | present at 255 — **FR-4's rationale correct** |
| `.show-me-ledger.json` not yet ignored | same | absent, as expected pre-implementation |
| `specs/0005-defer-message-on-error/` | `ls -a` | `requirements.md` only — **C-8/AC-8 correct** |
| `0002-*` and `0021-Expose…` dirs | `ls -d` | all four present — **FR-1/AC-1a/AC-35 correct** |
| ADR duplicate numbers | `ls docs/adr/ \| grep -oE '^[0-9]{4}' \| uniq -c` | 0037×5, 0057×4, plus 0038–0043, 0051, 0053, 0054, 0061, 0063, 0064 (and 0070–0073 on this branch) — **C-9 correct** |
| awk precedent file mode | `git ls-files --stage .claude/commands/adr/generate_adr_index.awk` | `100644` — **NFR-6/AC-53 correct** |
| BSD grep `\s`/`\b`/`\d` | `/usr/bin/grep -cE '\bpublic\b'` etc. | **all supported — document's claim false (finding 14)** |
| BSD grep BRE with `(a\|b)` | `/usr/bin/grep -c '^[+-][[:space:]]*(public\|protected)…'` | 0 matches — **document's BRE claim correct** |
| Public-API POSIX ERE pattern | `/usr/bin/grep -cE '^[+-][[:space:]]*(public\|protected)[[:space:]]'` | correctly excludes `internal` — **pattern works as stated** |
| `gh pr diff` flags | `gh pr diff --help` | no pathspec argument; only `--allow-escape-sequences/--color/--exclude/--name-only/--patch/--web` — **finding 3** |
| Amendment-added ACs | `git diff c32117d97~1..d1110292a` | AC-7, 30, 52, 53, 58, 68, 70–83 — confirms AC-77/79/80/81/83 are new |
| C-8 inline markers | `grep -nE '^\*\*AC-[0-9]+\*\* \*\([^)]*C-8'` | AC-1,2,4,12,17,18,20,47,51,56,**78** — C-8's prose list omits AC-78; AC-77/79 unmarked — **finding 6** |
| Approval marker staleness | `git log -1 -- specs/0037-show-me/.requirements-approved` | `dba76452f`, predating `c32117d97`/`0fe1deb85`/`d1110292a` — **confirmed stale** |
| Prior-pass dispositions | `grep '^### ' review-requirements-pass7.md` | none of findings 1–16 above duplicates a pass-7 finding; finding 6 is a regression of pass-7 #4; findings 5 and 7 are distinct from pass-7 #9 (which concerned the public-API pattern only, now fixed) |

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 8 |
| 50-69 (Medium) | 7 |
| 0-49 (Low) | 1 |

**Total findings**: 16
**Findings at or above threshold (60)**: 14
