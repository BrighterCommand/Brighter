# Review: design — 0037-show-me (pass 2)

**Date**: 2026-09-25
**Threshold**: 60
**Verdict**: NEEDS WORK

**Note**: This spec carries a stale `.design-approved` marker from before the rescope. The ADRs were
rewritten afterwards (commit `410af5db1`), so this review is a real gate on the current design.

8 findings at or above threshold 60. Address these before approving.

## Findings

### 1. The risk step checks a section that does not exist yet (Score: 75)

In 0073, the risk step checks the rendered `## Risk assessment` section before the step ends,
inside its markers. In 0072, the Synthesiser writes all prose, and it runs after the risk step. So
the section the check reads does not exist when the check runs.

The obvious fixes are both blocked:

- Moving the check after the Synthesiser puts a level test outside the markers. 0073's own
  invariant check then fails.
- Having the risk step render the section contradicts "Synthesiser … writes all prose".

Two implementers would resolve this in incompatible ways. AC-23 depends on the resolution.

**Evidence**:

- 0073:276-280: "the Synthesiser writes the rationale around the level and the reason the risk step
  recorded. Before the risk step ends, still inside its markers, it checks the rendered section: the
  stated level is not below the maximum…"
- 0072:189: "Step 5 - Explainer, Classifier, risk step, Synthesiser"
- 0072:259: Synthesiser "Writes all prose"
- 0072:565: "Step 5's stages in the order *Explainer, Classifier, risk step, Synthesiser*"
- 0073:300-303: the check fails on any line outside the markers that holds a conditional and a
  level name.

**Recommendation**: Pick one design and state it in both ADRs. Either:

- the risk step renders the `**Overall risk:**` line and the raise sentence itself, and the
  Synthesiser only adds rationale, or
- a second marked region, placed after the Synthesiser, holds the AC-23 check.

Then update 0072's step order to match.

---

### 2. 0073 has no Implementation Approach, and no ADR sequences the markers or the FR-13 check (Score: 65)

0073 goes straight from `### Technology Choices` to `## Consequences`. The canonical skeleton
requires `### Implementation Approach`, and all three siblings have one.

Nothing in the set orders the work 0073 introduces:

- the `<!-- show-me:risk-step:begin/end -->` markers
- the forced-level table
- the invariant check and its marker-count assertion (AC-81)

0072 hosts the check but leaves it out of its eleven-step sequence.

**Evidence**:

- Heading list for 0073: line 312 `### Technology Choices`, then line 338 `## Consequences`.
- 0072:452-454: "The FR-13 invariant check runs over … show-me.md. Its assertion is 0073's to define."
- 0072:548-567: steps 1-11 never mention the markers or the invariant check. Step 10 lists only
  "Step 5's stages".

**Recommendation**: Add `### Implementation Approach` to 0073. It should order:

- the marker region
- the mapping procedure
- the test-script row for the invariant check, including its marker-count assertion and the AC-81
  literal line

Add a pointer to this in 0072's step 3 or step 10.

---

### 3. When `requirements.md` or `tasks.md` has to be extracted, nothing says how declarations are found without re-writing a pattern (Score: 62)

0072 plans for `requirements.md` to be read by extraction on the calibration run itself. It does not
say how the command finds "the declarations".

The ledger carries the declared-id set but no line locations. The natural implementation is a Grep
for bold `FR-`/`NFR-` lead-ins. That transcribes the declared-id pattern into the command file, which
the requirements forbid.

0072 already solved this for release notes: the script emits line ranges "so the command reads a
section by its line range without re-recognising it". It does not extend that fix to the one
extraction guaranteed to happen. The same gap applies to NFR-3's "checkbox lines, task-type tags"
extraction when `tasks.md` does not fit.

**Evidence**:

- 0072:360: "`requirements.md` | whole if it fits; otherwise the declarations and the paragraphs the
  Classifier needs"
- 0072:368-369: "At 273,674 bytes it does not fit, so it is read by extraction."
- 0072:303-305: "Section recognition … is therefore implemented once, in the script, and the command
  reads a section by its line range without re-recognising it."
- requirements.md:142-144: the pattern is implemented "in exactly one place — FR-21's measurement
  script — and never transcribed into the command file".

**Recommendation**: Either:

- have the script emit each declared id's line number, and the task-checkbox line numbers, as file
  fields, or
- state that extraction searches only for the literal id strings the ledger lists, and never for a
  form.

Whichever is chosen, add a matching NFR-9 assertion.

---

### 4. The ledger schema is not specified, so the contract between the two artefacts is implicit (Score: 62)

The ledger is the only channel between the script and the command. 0072 fixes only
`schema_version`, "one named field per FR-21 value" and "a sibling reason field". It names no
fields.

Several values the command must copy into `## Blast radius` and the metadata block appear in no
field list:

- FR-20's PR count `{k}`
- FR-16 row 12's "rules it tried"
- the skipped merged candidates (`{ref} is already merged into {base ref}`)
- FR-16 row 16's "PR head not present locally" state, with both shas
- the distinction between FR-16 row 1 (no PR found) and row 2 (gh unavailable)

0072 also says the Blast radius section comes from the ledger for "everything, including the
provenance lines", and the command may compute nothing countable. So these fields must exist, but two
developers would name and shape them differently. The command file and the test script both depend
on the names.

**Evidence**:

- 0072:278-281: "What the script measures is FR-21's list … this ADR does not restate it."
- 0072:321-322: "one named field per FR-21 value. A value that is not determinable is `null` with a
  sibling reason field"
- 0072:422: "`## Blast radius` (FR-10) | everything, including the provenance lines | nothing"
- requirements.md:1106: row 12 is "followed by the rules it tried"
- requirements.md:780: "`{k}` pull requests found for branch…"

**Recommendation**: Add a ledger field table to Key Components 2: field name, group (ref, diff or
file), type, and null-reason values. Include every value FR-10, FR-16 rows 1, 2, 12, 15 and 16, and
FR-20 need. List the refinement fields as well (see finding 7).

---

### 5. The shared sentence does not fit 0078 or the judged half of the output (Score: 62)

All four ADRs repeat: "the command states only what it has measured, names what it measured it from,
and changes nothing."

- **0078 decides that a command writes.** `/spec:write_release_notes` edits `release_notes.md`, and
  0078 also amends five command files. The sentence is false for it.
- **The output is not only measured.** 0072's own Context says half the output is judged. So "states
  only what it has measured" misstates the Classifier's statuses, the narrative and the diagrams.

The documentation rule says a unifying sentence that does not fit every sibling means the set is not
yet one decision.

**Evidence**:

- 0078:77-78: the unifying sentence
- 0078:111-114: "give the marked release-notes form one writer, `/spec:write_release_notes`, which
  replaces its own section in place"
- 0072:26-28: "The other half is judged — a narrative, a breaking-change list, a verdict on each
  requirement"
- documentation.md: "State the unifying rule once, in one sentence … If it will not fit in one
  sentence, it is not yet one decision."

**Recommendation**: Rewrite the sentence so it holds for all four ADRs. For example: "every value the
command states is either counted by one tested script or judged from evidence it names, and no tool
writes anything but the one file it owns." If that cannot be made true for 0078, take 0078 out of the
set's shared sentence and say why.

---

### 6. The design cannot tell FR-21's "absent" and "unreadable" states apart (Score: 62)

FR-21 and AC-72 require the tooling-fault message to name which of five states applies, including
`absent` and `unreadable`. 0072 decides these from "the exit status".

Under `dotnet run`, a missing or unreadable `.cs` file is not a status the script chooses. dotnet
itself fails with a non-zero code. So the message would read `exited {code}` instead of `absent`, and
AC-72's first two cases fail.

No pre-invocation probe (such as `test -f` / `test -r`) is specified, though `test` is in
`allowed-tools`.

**Evidence**:

- 0072:391: "FR-21 — tooling fault | Step 2, or Step 3 … | the exit status, or the unparseable record
  or ledger | FR-21's one message, naming which of the five states applies"
- requirements.md:968-976: the message form `{absent|unreadable|exited {code}|…}`
- AC-72 (requirements.md:2132-2140): all five states, each named.

**Recommendation**: Add to Key Components 4 a Step 2 pre-check: `test -f` for absent, then `test -r`
for unreadable, before invoking the script. Add each state's evidence as its own row in the stop
table.

---

### 7. The new ledger fields are not tested, contrary to 0072's own premise (Score: 60)

0072 adds these values to the script:

- F1's level
- whether each of D1, D2 and D3 fired
- each marked section's line range and byte size
- the `src/` diff's byte size

F1's thresholds and the D1-D3 tests are the countable logic NFR-1 lists. 0072's case for the script
is that "each one is an ordinary bug with an ordinary test".

Yet:

- the Implementation Approach's diff step names no assertion on F1 or the D outcomes;
- no row asserts the line ranges or byte sizes, which the command relies on for its reads and budget
  checks.

An off-by-one at F1's 10/11 or 50/51 boundary, or at D1's ≥ 5 / ≥ 2, would pass every listed test.

**Evidence**:

- 0072:295-308: the script "also emits … factor F1's level, and whether each of D1, D2 and D3
  fired", plus line numbers and byte sizes.
- 0072:557-558, step 6: "Pinned diff fields: buckets, net lines, `src/` subdirectories, public-API
  lines, commit count."
- 0072:129-130: "In an executable artefact, each one is an ordinary bug with an ordinary test."
- 0072:545-546: "Each behavioural step is test-first."

**Recommendation**: Extend step 6 and the fixture plan to assert F1's level and the D1-D3 outcomes on
the calibration row (F1 `High`; D1, D2 and D3 fire), plus a boundary fixture. Assert the declared
fixture's section line range and byte size, and the pinned diff's `src/` byte size. NFR-9 says "at
minimum", so this is additive.

---

### 8. The ADRs record review history and earlier drafts (Score: 60)

The tone rules forbid referring to review back-and-forth and to revision history. The set does both:

- **0072:119-120:** "This spec's own design review found four defects of that kind in earlier drafts
  of this command". This grounds the motivating table in the review process instead of in the defects
  themselves.
- **0072:637:** "Leave the counting where earlier drafts had it".
- **0072:642-644:** "The claim that a script would be 'a new category' in this family does not hold
  either". This rebuts an argument from an earlier round.
- **0073:378:** "Risk: F3 or F4 is revived by someone reading an earlier version of this ADR or of
  the command file".

**Evidence**: The lines above; documentation.md § *Writing tone*: "Do not reference ephemeral
working state … unresolved review back-and-forth"; § *Sentence construction*: "State the rule that
holds, not the revision that changed it."

**Recommendation**:

- Present the defect table as defects of prose-embedded patterns: "a pattern embedded in prose
  produced these results".
- Describe Alternative 1 by its mechanism, not as "earlier drafts".
- Drop the "new category" rebuttal.
- Recast 0073's risk as "F3 or F4 is re-introduced", with the same mitigation.

---

### 9. FR-7's `wc -c` probe before the release-notes read is dropped (Score: 55)

FR-7 requires `release_notes.md`'s size to be measured with `wc -c` before the marked section is
extracted. 0072 prices the read from the ledger's section byte size and never mentions the probe. The
replacement may be better, but it departs from a stated requirement without saying so.

**Evidence**:

- requirements.md:493-494: "it is a targeted extraction of the marked section, charged in bytes
  against NFR-3's budget, with the file's size measured by `wc -c` first"
- 0072:344-346: "the `src/`-scoped diff's size and each marked section's size come from the ledger"

**Recommendation**: Either keep the `wc -c` probe as well, or state that the section's ledger size is
FR-7's size check and name FR-7 as the requirement satisfied.

---

### 10. The per-line FR-13 check misses a branch split across lines (Score: 55)

The check matches a conditional and a level name on one line. The command file is markdown wrapped
at about 100 columns, so "if four or more items,⏎ the level is High" outside the markers passes.
0073's mitigation for "a later edit adds an action on the level" assumes the check catches it.

**Evidence**:

- 0073:300-303: "no line of `.claude/commands/spec/show-me.md` contains both a conditional keyword …
  and a level name"
- 0073:375-376: "*Mitigation*: outside the markers, the invariant check fails."

**Recommendation**: Match over paragraphs or sentences (blank-line-delimited blocks) instead of
lines, or state the line-based weakness under Negative consequences and weaken the mitigation's claim
to match.

---

### 11. The row-5 path tree has a minimum size but no maximum (Score: 50)

0077 says the Synthesiser draws FR-14's list from the tree's changed nodes and "cannot be handed a
tree of files its list omits". That forces the tree's changed nodes to equal the path list. FR-14
caps that list at 7, but 0077 states only the floor of 3. A tree with 10 changed nodes is unresolved.

**Evidence**:

- 0077:299-303: "the Synthesiser cannot be handed a tree of files its list omits. The Explainer
  therefore never elects a tree with fewer than three changed nodes"
- requirements.md:728: "three to seven paths"

**Recommendation**: State that a row-5 tree has 3-7 changed nodes, or define what happens to the
extra nodes.

---

### 12. 0073's Context says seven of eight sections can be answered directly (Score: 45)

`## What changed and why`, `## Breaking changes`, `## Did it ship what it said?` and
`## Where to look first` are all judged. 0073's own next section, and 0072's Context, say so.

**Evidence**: 0073:25: "Seven of its eight sections report things the repository can be asked
directly." 0072:26-28 says half the output is judged.

**Recommendation**: Rewrite the sentence, e.g. "Every other section presents the evidence the risk
word summarises."

---

### 13. 0072's sequence diagram leaves out two stop paths (Score: 45)

The diagram shows only the exit-`2` and other-status stops. It does not show:

- Step 1's FR-1 and FR-2 stops;
- FR-21's "exit 2 with an unparseable gate record" (tooling fault, not FR-3);
- the Step 3 unparseable-ledger stop.

The prose "four exits" does not map cleanly onto what is drawn.

**Evidence**: 0072:176-197; 0072:197 "The run has four exits"; 0072:391, where Step 3 is a stop
site.

**Recommendation**: Add an `alt` for Step 1's stops and a Step 3 parse-failure branch, or turn the
exits into a small ladder table.

---

### 14. No stage owns FR-8 Part 3, `Shipped beyond the requirements` (Score: 40)

Part 3 is a judgement over `tasks.md` against the declared ids. 0073's Classifier covers items and
statuses only, and 0072's FR-8 row names statuses, tallies and Part 1.

**Evidence**: requirements.md:588-589; 0072:420; 0073:199-202.

**Recommendation**: Assign Part 3 to the Classifier, or to the Synthesiser, in one of the two tables.

---

### 15. Bold-lead paragraphs that should be `####` headings (Score: 40)

**Evidence**: 0072:261 "**The seam runs between counting and reading.**"; 0072:327 "**The write is
atomic.**"; 0072:371 "**Two reads are obligations, not options.**"; 0072:377 "**The full diff is
never read.**"; 0078:276 "**Who marks a section.**"; 0078:302 "**The stop at row 7.**"; 0078:311
"**The first `##` heading is taken to be the unreleased one.**"

**Recommendation**: Promote each to a `####` heading, per *ADR readability*.

---

### 16. 0078 labels behaviour changes as "Structural" (Score: 40)

Requiring a declaration form in `/spec:requirements`, adding review checks and adding a
`/spec:design` step all change how those commands behave. Tidy First reserves "structural" for
changes that preserve behaviour.

**Evidence**: 0078:374: "The amendments are documentation, so each is one structural commit". Steps
1-3, 5 and 7.

**Recommendation**: Label the amendment steps "Behavioural" or "Documentation", but not
"Structural".

---

### 17. The temp file's fixed name does not address two runs at once (Score: 40)

With a fixed temp name in the target directory, two sessions running on the same spec can overwrite
or delete each other's temp file between write and move. After a crash, a leaked temp file is also
untracked and not ignored, so it shows in `git status`, against FR-4's "no temporary file left
behind".

**Evidence**: 0072:616-621.

**Recommendation**: Use a per-process unique temp name. Keep the exact-match `.gitignore` line, and
state that concurrent runs are unsupported or last-writer-wins.

---

### 18. 0077 describes the Explainer-to-Synthesiser handoff two ways (Score: 40)

**Evidence**:

- 0077:214-215: "The Synthesiser reaches a diagram's contents only through the node list"
- 0077:181: "E-->>S: a rendered block, its target section and its node list"
- 0077:123-124: "The Synthesiser places what it was given and draws nothing itself."
- The flowchart shows only NL→S. It shows no block and no named-line edge.

**Recommendation**: State one handoff: the rendered block (built from the node list) or a named line.
Add that edge to the flowchart.

---

### 19. 0072's Decision holds three decisions, and its title no longer names them (Score: 40)

**Evidence**:

- 0072:158-160: split script and command; draw the line at counting; decide every stop before either
  write.
- 0072:3: title "Target Resolution and Output Shape".

**Recommendation**: Retitle the ADR around the counting/reading seam. Consider moving "stops decided
before writes" into Key Components as a consequence of the seam.

---

### 20. Front-matter summaries run past two sentences (Score: 35)

**Evidence**: adr_frontmatter.md:52 says a summary is "One or two sentences". 0072 line 8 has 4
sentences, 0073 has 3, 0077 has 4 and 0078 has 3.

**Recommendation**: Cut each to two sentences and regenerate `docs/adr/index.md`.

---

### 21. 0078 says each document has exactly one producer (Score: 35)

`release_notes.md` has hand-written sections, and 0078 itself says so.

**Evidence**: 0078:187 "Each document has exactly one producer." 0078:449-450 "hand-written sections
from specs and non-spec changes sit side by side in it."

**Recommendation**: Change it to "each form has exactly one producing command".

---

### 22. The defect table misstates the FR-13 false positive (Score: 30)

**Evidence**:

- 0072:126: "FR-13 invariant check | 3 hits | 0"
- requirements.md:1510-1511: "a false-positive class already observed three times"

**Recommendation**: Reword the table row to match the requirement.

---

### 23. The 0073 flowchart does not show the level's "two arrows" (Score: 30)

**Evidence**:

- 0073:159-160: "The level leaves by two arrows, both renderings."
- The flowchart chains OUT --> REP, so the report is drawn from the written section. The level does
  not leave by two separate edges.

**Recommendation**: Draw two edges from the level node, or reword the invariant.

---

### 24. `/spec:gear` is called a read-and-report command (Score: 25)

**Evidence**: 0072:532 "`/spec:status` and `/spec:gear`, the read-and-report commands".
`/spec:gear` writes `.current-gear`.

**Recommendation**: Say "commands that run in the main agent" instead.

---

## Verification log

- **Diagrams:** all 7 mermaid blocks (0072 ×2, 0073 ×2, 0077 ×2, 0078 ×1) extracted and rendered
  with `npx -y -p @mermaid-js/mermaid-cli@11 mmdc`. All 7 exited 0 and produced an `.svg`. 0073's
  decision flowchart and 0072's sequence diagram rendered to PNG at 1600 wide. 0073's PNG, the most
  complex flowchart, was inspected: it is readable and has a single decision node. Nothing needed
  converting to a ladder table.
- **Escaped markdown:** `grep -c '&lt;\|&gt;\|&amp;'` returned 0 for all four ADRs.
- **Referenced paths that exist:** `.claude/commands/spec/{status,switch,README,gear,requirements,tasks,review,design,show-me}.md`,
  `.claude/commands/adr/generate_adr_index.awk` (mode `100644` confirmed), `.claude/settings.json`,
  `.agent_instructions/adr_frontmatter.md`, `docs/adr/0071-tdd-review-gear.md`, `release_notes.md`,
  `CONTRIBUTING.md`, the root `Directory.Build.props`.
- **Expected-absent paths, confirmed absent:** `show_me_facts.cs`, `write_release_notes.md`,
  `.claude/test-fixtures/show-me/`.
- **Codebase claims confirmed:**
  - `settings.json` has `Bash(wc:*)`, `Bash(git diff:*)` and `Bash(git ls-files:*)`; no interpreter
    or `git merge-base` entry; the `deny` list holds curl/wget/ssh.
  - `review.md` has *Requirements*, *Design (ADR)* and *Tasks Review Criteria*.
  - `tasks.md` has a *DO NOT Format Tasks Like This* block.
  - `release_notes.md`'s first `##` is `## Master`.
  - No `.py` files are tracked; `generate-test.sh` and `generate-test.ps1` exist, and
    `CONTRIBUTING.md` has Linux/macOS and Windows paths.
  - CI installs 9.0.x and 10.0.x; there is no `global.json`.
  - The README sub-agent policy states "clean context" and "one-shot".
  - `docs/adr/index.md` carries rows for all four ADRs.
- **Heading skeleton:** compared across the set. 0073 lacks `### Implementation Approach`. The stage
  table stands in for the roles table, as settled.
- **Where this ADR sits:** present in all four, lists all four, own row bolded and marked
  *(this one)*. The unifying sentence is identical in all four (its accuracy is finding 5).
- **Tone:** grepped for history and conversation phrases. Hits are recorded in finding 8. No
  references to conversation participants were found.
- **Readability:** a rough sentence-length and bold-lead count (not the full three-instrument
  measure). Each ADR has 1-2 units over 25 words; the Decision bold sentences run 40-52 words.
  Bold-lead prose paragraphs are listed in finding 15.
- **Not checked:**
  - `dotnet run` against file-based apps, and the `--project`/`--file` argument-passing claim
    (0072:512-514).
  - Whether Claude Code's `:*` prefix matching treats `show_me_facts.cs:*` as a word-boundary match.
  - The calibration figures were not re-measured.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 1 |
| 50-69 (Medium) | 10 |
| 0-49 (Low) | 13 |

**Total findings**: 24
**Findings at or above threshold (60)**: 8
