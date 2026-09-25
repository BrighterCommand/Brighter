# Review: design — 0037-show-me (pass 4)

**Date**: 2026-09-25
**Threshold**: 60
**Verdict**: NEEDS WORK

**Note**: This spec carries a stale `.design-approved` marker from before the rescope. The ADRs were
rewritten after that approval and last revised in `f5ac771e1`/`fbfdae9bc`. This review is a genuine
gate on the current design.

1 finding at or above threshold 60. Address it before approving.

## Findings

### 1. FR-16 row 6 says the narrative is written from "the commits", but the design gives the command no way to read a commit (Score: 62)

When `.adr-list` is missing or empty, FR-16 row 6 and AC-40 require `## What changed and why` to be
"synthesised from `requirements.md`, `tasks.md` and the commits". The 0072 in-scope list claims every
FR-16 row. But the design gives the command no way to read commits:

- The read table (Key Components 3) lists every read and prices each one. It has no commit read.
- The `allowed-tools` list omits `git log`, even though it lists everything else the command runs.
- Alternative 2 rejects "the narrative from `git log --oneline`" as a *replacement* for judged prose.
  It never says whether commit messages are an *input*.

An implementer therefore has two options, and both are wrong:

- Skip the commits, which does not conform to AC-40.
- Add `git log` outside the fixed tool list. That read would be unwindowed and outside the byte
  budget.

A related gap: the `git history` row of `## Inputs used` is listed, but no rule says what makes it
`used` or `not available`. AC-27 needs it `used`. FR-16 row 12, where no range exists, presumably
needs it `not available`.

**Evidence**:

- requirements.md:1100 (FR-16 row 6): "`What changed and why` states `No ADRs recorded for this spec.`
  and is synthesised from `requirements.md`, `tasks.md` and the commits."
- requirements.md:1900-1903 (AC-40): "is synthesised from `requirements.md`, `tasks.md` and the
  commits".
- 0072:76-77: "FR-16 — every row's effect on a section, except the factor levels rows 8, 9 and 12
  force (0073) and row 12's diagram line (0077)."
- 0072:489-500: the read table has no commit or `git log` read.
- 0072:703-708: "`allowed-tools` lists what the command itself runs: `ls`, `cat`, `date`, `head`,
  `tail`, `test`, `wc`, `grep`, `git diff`, `git ls-files`, the script entry above, `Read` and
  `Write`."
- 0072:876-877: "…the narrative from `git log --oneline`" (Alternative 2).
- 0072:590-595: `## Inputs used` names a "git history" row, with no rule for its mark.
  requirements.md:1823-1826 (AC-27) needs it marked `used`.

**Recommendation**: Add a row to the read table. For example: `git log --format=%s
{merge base}..{measured head}` in unplanned windows, charged to the general allowance, only when
`.adr-list` is missing or empty and a diff was measured. Add `git log` to `allowed-tools`; the
existing `Bash(git log:*)` entry already covers it. Alternatively, state that the ledger's commit
count plus the diff reads are what row 6 means by "the commits", and say why. Either way, state what
marks the `git history` row `used` and what marks it `not available` (row 12).

---

### 2. The byte budget depends on the model keeping a running total, and the ADR does not say so (Score: 45)

Keeping to 1,048,576 bytes means the model has to add up more than 50 window sizes and subtract each
one from a remaining total. That is the same kind of model arithmetic that 0072's own defect table
records going wrong (16 against 8). 0077 says plainly that its diagram caps are "a model-checked
target, not a guarantee" with no mechanical backstop. 0072 gives the budget no such caveat. Its
Positive consequences say the budget "cannot" be exceeded by "a correct run", which takes the
arithmetic on trust. Line 463 also says "by the same arithmetic as before". That is either
revision-history residue or refers to text that does not exist earlier in the document.

**Evidence**:

- 0072:462-464: "The total therefore stays inside the budget by the same arithmetic as before:
  nothing is read whose size the bytes remaining do not cover."
- 0072:797-799: "**The budget is met by arithmetic.** Every read is priced before it is issued, so a
  correct run cannot exceed 1,048,576 bytes."
- 0077:350-352: "They are a model-checked target, not a guarantee. 0072's defect table shows the model
  can miscount."

**Recommendation**: Add a Negative consequence saying the running total is kept by the model and has
no mechanical backstop. Reword line 463 to state the rule without "as before".

---

### 3. The order of Step 4's reads is not fixed, so whether the release-notes section gets read depends on the implementer (Score: 45)

The requirements do not fix a read order ("this document specifies no read order"), so choosing one
falls to the design. 0072 fixes only that the existing `show-me.md` is read first. `requirements.md` is
read "all its planned windows if they fit … while bytes remain". A marked release-notes section is
read "unless the bytes remaining cannot cover every marked section".

- If `requirements.md` is read before the release-notes section, it can use up the general allowance,
  and the run lands in FR-16 row 5a.
- If the release-notes section is read first, it is read.

AC-68 and AC-69 can therefore come out differently for two conforming implementations.

**Evidence**:

- 0072:489-500: the table does not say it is in read order.
- 0072:492: the existing `show-me.md` is read "as the first of Step 4's reads".
- 0072:517-518: the marked-section read obligation.
- requirements.md:1329-1331: "…this document specifies no read order".
- requirements.md:2110-2114 (AC-69).

**Recommendation**: State that Step 4 reads in the table's row order, or give an explicit order. Put
the marked release-notes section before `requirements.md`, because FR-7 makes that read an obligation.

---

### 4. The "no PR" rule covers a `gh` that exits non-zero, but not a missing `gh` or one that hangs (Score: 40)

NFR-4 requires the script to exit `0` with no `gh` at all. 0072 maps only two cases: a query that
returns no matching PR, and a non-zero `gh` exit. If `gh` is not installed, the child process fails to
start. .NET raises an exception in that case, and an implementer who follows the stated rule lets it
escape as a tooling fault. No timeout is stated for an offline `gh` either.

**Evidence**:

- 0072:319-320: "A `gh` query that returns no matching PR is FR-16 row 1; a non-zero `gh` exit is
  row 2."
- requirements.md:965-966: "With no network and no `gh` the script still exits `0`".

**Recommendation**: Extend the rule: "a `gh` that cannot be started, exits non-zero, or does not
finish within {n} s is row 2."

---

### 5. The fifth failure state ("ledger was not a single JSON object") is judged by the model over split windows (Score: 35)

The ledger is indented JSON of up to 65,536 bytes, read as several unplanned windows. Nothing
mechanical decides whether it parses. The model judges it across window boundaries. AC-72's fifth
state therefore rests on inspection that is not stated as such.

**Evidence**:

- 0072:195-197: "opt the ledger is not a single JSON object".
- 0072:491: "The ledger | unplanned windows".
- 0072:545.

**Recommendation**: State this as a model check with its limits, as 0077 does for the caps. Or have
the command confirm `schema_version` and a closing brace as a minimum test.

---

### 6. Two parts of 0078's implementation plan are loose (Score: 30)

- Step 4 of the `/spec:write_release_notes` procedure says "Find every section marked for the
  target", but step 2's single `Grep` returns only headings and fences. The ADR does not say how
  marker lines are found, or how "immediately after a `###` heading" is checked.
- Implementation Approach step 7 is conditional ("when it is next revised"), so it cannot be placed
  in commit order. Step 7 and step 6 are also labelled "Documentation", which is neither Tidy First
  category.

**Evidence**:

- 0078:294: step 2's `Grep` finds `##`/`###` headings and fence lines.
- 0078:296: "Find every section marked for the target…"
- 0078:400-401.

**Recommendation**: Name the second lookup (a `Grep` for the literal marker, checked against step
2's heading line numbers). Drop step 7, or make it a concrete commit.

---

### 7. 0072's *Terms* entry for Synthesiser disagrees with its stage table (Score: 30)

The *Terms* entry defines the Synthesiser as the model "in the stages that write prose". The stage
table defines it as one stage, and the risk step also writes lines (the factor table, the
`**Overall risk**` line, FR-13's sentence).

**Evidence**:

- 0072:34-35: "the executing model … in the stages that write prose".
- 0072:271-272: the stage table.

**Recommendation**: "the stage that writes all prose other than the risk step's lines and the
Explainer's blocks. The stage table states its rule."

---

### 8. Small readability and tone items (Score: 25)

- 0072:438: "as of Claude Code today" dates the table. Use a version or date.
- 0072:161-163: the Decision's bold sentence is about 33 words against a limit of about 25.
- 0072:811: a stray blank line splits the Negative bullet list.
- 0072:36-39: "Its rule is / stated in" is broken across lines oddly.
- 0078:336: the heading is "Where each file is touched", while the three siblings use "Where each
  artefact is touched".
- 0072:101: the Out of scope bullet names 0078 as plain code, not as a link.

**Recommendation**: Fix each as listed.

---

## Status of pass-3 findings

1. Allow-list redirect via `--file` — **closed**. The entry is now `… show_me_facts.cs -- specs/:*`
   (0072:642, 679-693). The grammar is fixed (0072:287-299), there is a probe test row
   (0072:617-623), and there is an SDK-change risk (0072:843-849).
2. Read mechanism versus tool limits — **closed**. The window rule (0072:434-478) and 0078's
   `Grep`-then-`Read` with offset (0078:300-306).
3. D3 count field — **closed**. `adr_resolved_count` (0072:382), used at 0077:155-157 and asserted at
   0072:743-744.
4. Attribution table versus a diff declaration — **closed** (0077:292, 296-298).
5. Argument grammar — **closed** (0072:287-303).
6. `## Inputs used` projection — **closed** (0072:590-599).
7. Test-first claims and the cap error — **closed** (0072:733-735, 764-771; over-cap exit 1 at
   407-409).
8. Caps counted by the model — **closed** (0077:350-352, 432-434).
9. Stated level versus maximum, and forced-row values — **closed** (0073:261-265, 289, 296-298).
10. Uneven Key Components — **mostly closed**. `#####` nesting under 2 and 3, touch tables in 0073 and
    0077, and 0077 step 1 relabelled. The unnumbered `#### The seam runs between counting and
    reading` still sits between the stage table and component 1 (0072:274).
11. Long Decision sentences — **closed** for 0073, 0077 and 0078 (about 23-26 words each). 0072's own
    sentence is about 33 words (finding 8).
12. Generation date and created-or-replaced — **closed** (`date +%F`, `test -f` at 0072:567-577,
    704).
13. Ledger conventions — **closed** (0072:383, 394-396).
14. Exception attached to the wrong invariant — **closed** (0072:210-214).

## Verification log

- **Mermaid**: all 7 blocks (0072 ×2, 0073 ×2, 0077 ×2, 0078 ×1) extracted to the scratchpad
  `review4/` directory and rendered with `npx -y -p @mermaid-js/mermaid-cli@11 mmdc`. All exited 0 and
  produced an SVG. 0072's sequence diagram, 0077's structure flowchart and 0073's decision flowchart
  were rendered to PNG at 1600 px and two of them inspected. They are readable, with no clipped
  labels. No `;` in any `sequenceDiagram`, and no `<`/`>` in labels.
- **Escaped entities**: `grep -c '&lt;\|&gt;\|&amp;'` returns 0 for all four ADRs.
- **Headings**: the H2/H3 skeleton is identical and in canonical order across all four.
  `### Where this ADR sits` is present in all four, lists all four and bolds the ADR's own row. The
  unifying sentence is byte-identical in all four (the md5 of the paragraph is the same).
- **Readability**: an approximate *Language* probe (fences stripped, units split at `.:;` and `|`)
  found only 1-2 units of 40 words or more per ADR, mostly in the front-matter summaries. Worst: 55
  words, in 0073's summary.
- **Probe on requirements size**: the declaration paragraphs in spec 0036's `requirements.md`,
  measured with 0072's paragraph rule, total 87,065 B. That is inside the 254,199 B left after the
  fixed reads, so the calibration run leaves no id `Unverifiable` for budget reasons. The probe
  printed its argv to confirm it received the path.
- **Codebase checks**:
  - `settings.json` has `Bash(head:*)`, `Bash(tail:*)`, `Bash(wc:*)`, `Bash(grep:*)`,
    `Bash(git diff:*)`, `Bash(git log:*)` and `Bash(git ls-files:*)`. It has no interpreter entry, no
    `test` entry, no `date` entry and no `git merge-base` entry. The `deny` list holds curl, wget and
    ssh.
  - `.gitignore` has `PROMPT.md` and `PROMPT-*.md` and no ledger line yet (expected).
  - `generate_adr_index.awk` is mode 100644.
  - `switch.md` pre-executes `ls -d specs/*/`.
  - README states the clean-context and one-shot sub-agent points.
  - `tasks.md` has *DO NOT Format Tasks Like This* at line 133.
  - `review.md` has all three criteria lists.
  - `release_notes.md` is 118,145 B, its first `##` is `## Master`, and it holds two `(spec 0027)`
    headings.
  - `0062-pg-advisory-lock-sha256.md` and `0071-tdd-review-gear.md` exist.
  - There are 0 tracked `.py` files. Both `.sh` and `.ps1` scripts exist, and CONTRIBUTING has
    Linux/macOS and Windows sections.
  - CI installs `10.0.x`, and there is no `global.json`.
  - The root `Directory.Build.props` only sets LangVersion, Nullable, NoWarn and reference-assembly
    properties.
- **Expected-absent paths**: `show_me_facts.cs`, `show_me_facts_tests.cs`, `write_release_notes.md`
  and `.claude/test-fixtures/` are absent, as expected. The existing `.claude/commands/spec/show-me.md`
  is present, and 0072 marks it as "Rewritten".
- **Tone**: grepped for revision, conversation-participant and history phrases. "the user" appears
  only in 0078, meaning the command's runtime user, which is legitimate. "as before" (0072:463) and
  "today" (0072:438) are flagged in findings 2 and 8.
- **Not checked**:
  - The `dotnet run … --` probe was not re-run; it is a settled measurement.
  - Whether a file-based app placed under `.claude/` is disturbed by the root props; that would need
    writing into the working tree.
  - The compile timing figures.
  - Claude Code's `Read` and `Bash` output limits, and its `:*` prefix-matching semantics.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 1 |
| 0-49 (Low) | 7 |

**Total findings**: 8
**Findings at or above threshold (60)**: 1
