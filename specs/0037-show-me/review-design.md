# Review: design — 0037-show-me

**Date**: 2026-09-20
**Threshold**: 60
**Verdict**: NEEDS WORK

14 findings at or above threshold 60. Address these before approving.

**Note**: This spec carries a `.design-approved` marker from the pre-rescope approval (commit `205ba166b`, 2026-09-19), but ADR 0073 was rewritten and renamed and ADR 0077 added afterwards (commit `9bac0657a`). The marker is stale; this review is a genuine gate on the current design, not informational.

## Findings

### 1. ADR 0072 is still `Accepted` while specifying content the rescope cut (Score: 92)

ADR 0072's decision was described as "unchanged", but 0072 decides *the shape of the file the command writes*, and it still names F3, F4, the CI rollup, PR comments, review-round decomposition and FR-8's row-per-requirement table as live parts of that shape. It is the document an implementor will trust, and it currently instructs them to build the thing the rescope removed. Seven distinct instances:

**Evidence** (all `docs/adr/0072-show-me-command-resolution-and-output.md`):

- L70–73: "the task total and per-tag counts, **the CI tally**, **FR-8's row count** and the set of requirement ids, and factor levels F1 and **F4** must be **identical** between runs" — the CI tally is cut, `F4` is a retired id, and FR-8 no longer has rows.
- L74–75: "FR-8's per-requirement statuses and **the review-round decomposition** are named as 'judgement-derived synthesis'" — review-round decomposition is cut.
- L153–154 (Architecture Overview, Step 5): "`+ blast-radius stats · CI rollup · PR comments`" — both are forbidden inputs now (FR-18: "no query that retrieves PR comments, reviews or `statusCheckRollup` is permitted").
- L424 (Step 6's per-section table): "`## Did it ship what it said?` (FR-8) | the **row set and row count**; the count line's arithmetic | **each row's** status, paraphrase, evidence…" — FR-8 is now four parts (shipped-as-planned line, deviation entries, Part 3, count line) and `requirements.md` says explicitly "The old FR-8 rendered one table row per declared requirement id… The narrative shape does not."
- L409–412: "`requirements.md`'s declared-id grep gives FR-8's *row set* mechanically — which is why NFR-1 can require the **row count** and id set to be identical between runs".
- L501–502 (Negative): "Every run re-executes the whole `git`/`gh` sequence — branch resolution, PR list, PR diff, **rollup, comment fetch**".
- L507–508 (Negative): "because F2, **F3** and F5 feed FR-12's maximum" — `F3` is a retired id, and this is the only place in the three ADRs where a retired id is still treated as **live**. ADR 0073 L70–72 says "A `show-me.md` containing an `F3` or `F4` row is non-conforming."
- L57, L197, L619: "FR-16's **fifteen** degradation rows". FR-16 now has **fourteen** rows (1, 2, 5, 5a, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 — rows 3 and 4 retired, 5a added). 0072 never mentions row 5a at all.

**Recommendation**: Amend 0072 (it will need a Status/revision note of its own, or re-issuing as Proposed for re-approval). Rewrite the NFR-1 split at L70–78 against the current NFR-1; remove `CI rollup · PR comments` from the Architecture Overview; rewrite the FR-8 row of Step 6's table against FR-8's four parts; fix L507 to `F2 and F5`; change "fifteen" to "fourteen" in three places and add row 5a's behaviour.

---

### 2. `release_notes.md`'s read is charged to no budget in any ADR, so FR-16 row 5a and AC-68 are unreachable as designed (Score: 84)

FR-7 states: "The read is bounded like every other: it is **one file read against [the] budget**." FR-16 row 5a and AC-68 define a whole degradation path that fires when "the file-read budget was exhausted before that section is reached". No ADR designs it.

**Evidence**:
- ADR 0072 L406, the 25-read budget table: `| release_notes.md section | grep -n for the spec's heading, then grep -A for its bullets | **no read** |`. A `grep` extraction can never exhaust a 25-file read budget, so row 5a's precondition can never arise and AC-68 can never pass.
- ADR 0077 L224–225 states the order of spend in full — "`requirements.md` at Step 5, then the Explainer's participants at Step 5D, then `## Where to look first`'s on-demand reads at Step 6" — and `release_notes.md` is absent from it. `grep -n 'release_notes' docs/adr/0077-*.md` returns nothing.
- 0077's own worst-case model (L458–460) is correspondingly optimistic: "a participant set of twenty leaves four places once `requirements.md` is paid for" — 25 − 1 − 20 = 4 is arithmetically right but ignores `release_notes.md` (−1) and the `PROMPT.md`/`PROMPT-*.md` reads that 0077 L228–230 itself says "still occupies a place in the read set" (up to −7 in this repository).

**Recommendation**: Decide, in 0077's Key Components 2, where `release_notes.md` sits in the order of spend and that it charges one place; correct 0072's budget-table row from "no read" to "1"; and re-state the worst case including it and the `PROMPT` reads.

---

### 3. FR-9 (`## How it was built`) is decided by no ADR (Score: 80)

FR-9 is a required H2 section with defined content (task total, five per-tag counts, commit count, and a stated fallback line when the branch is not determinable). It appears in no ADR's scope.

**Evidence**:
- `grep -n 'FR-9' docs/adr/007{2,3,7}-show-me*.md` returns two hits, both in 0072, both **deferrals**: L313 "the same `grep` family, with the task-type tag added, supplies FR-9's per-tag counts **for the sibling ADR**", and L652 (References) "**Forthcoming sibling ADR** — review-history decomposition (the `Finding` / `Review round` / `Finding severity` definitions **and FR-9**) and the **five-factor** risk-scoring model".
- ADR 0073, the sibling that arrived, mentions FR-9 nowhere. Its Scope (L35–37) is "FR-11, FR-12, FR-13, NFR-1's judgement enumeration for the factors, and the `Advisory` definition".
- ADR 0072's own Step 6 per-section table (L420–427) has six rows — FR-6, FR-7, FR-8, FR-10, FR-14, FR-15 — and no row for `## How it was built`.
- FR-9's branch-not-determinable fallback (`Commits: not determinable — spec branch not resolved.`) is likewise designed nowhere; 0072 L333–338 enumerates FR-16 rows 12–15's effects and omits it.

That References bullet is also a stale cross-reference in its own right: it promises a "Forthcoming" sibling covering five factors and review history, two sections after 0072's own `### Where this ADR sits` map correctly names 0073 as the three-factor risk ADR.

**Recommendation**: Give FR-9 an owner — most naturally 0072, which already produces both counts — with a row in Step 6's table and the fallback line stated. Rewrite the "Forthcoming sibling ADR" References bullet to name 0073 and 0077 by slug.

---

### 4. ADR 0077 misstates its sibling's rule, and the misstatement is the stated reason for its central design choice (Score: 78)

ADR 0077 asserts four times that ADR 0072 forbids a *read* during Step 6, and uses that as the justification for the Explainer existing as a separate pre-synthesis stage. ADR 0072 forbids *shell calls* in Step 6 and explicitly allocates the remainder of the 25-file read budget to reads that happen there.

**Evidence**:
- 0077 L145–146: "It sits before synthesis because **ADR 0072 forbids a new read during Step 6**, and a diagram needs reads."
- 0077 L209–210: "**The Synthesiser** takes the ledger in and puts prose out, **with no new reads during Step 6**."
- 0077 L492–493 (Risks and Mitigations): "*Mitigation*: **ADR 0072 already forbids a new read during Step 6**".
- 0077 L539–541 (Alternative 3): "ADR 0072 fixes the Synthesiser as ledger in, prose out, with no shell call and **no new read during Step 6**."
- But ADR 0072 L123: "| **Synthesiser** | … | Runs **no shell call** and counts nothing |"; L160: "SYNTHESISE — ledger in, prose out; **no new shell calls**"; and L407, the budget table: "| files for `## Where to look first` | **`Read` on demand**, only where the diff path list is not self-explanatory | **the remainder** |".
- 0077 contradicts itself on the same page: L225 lists "`## Where to look first`'s **on-demand reads at Step 6**" as the third and last item in the order of spend.

The consequence is not cosmetic. The mitigation at L492–493 ("a convenience `Read` during Step 6 that no ledger row records") is void, and Alternative 3's rejection rationale rests on a constraint that does not exist.

**Recommendation**: Correct all four statements to "no new **shell call** during Step 6", and re-argue the Explainer's placement on the grounds that actually hold (the trigger values are Measurer output; the node ledger must exist before the block is rendered). Then re-check Alternative 3, whose rejection needs a different reason.

---

### 5. The Explainer's affordability guarantee is self-contradictory — the participant set cannot be enumerated before the first read (Score: 78)

ADR 0077's strongest claim is that a partial diagram cannot exist because affordability is tested before any read. The definition of the participant set defeats it.

**Evidence** — 0077 L258–264: "The Explainer names, **before it reads anything**, the complete set of files the relationship needs. Membership is bounded: a participant is a changed file in the spec diff, or a file named by an ADR extract already in the ledger, **or a file named by a participant already read**."

The third clause is only evaluable *after* reading. So either the set is not complete before the first read (and L266–269's "It does not start reading and then stop" has no defined behaviour for a mid-read discovery), or the third clause is dead and a relationship spanning a caller the diff did not touch cannot be drawn at all. Both readings break something the ADR asserts: L126–128 "Row 5 is tested before any read happens, so a partial picture has no way to exist", and L440–441 "Affordability is tested before the first read, so budget exhaustion is discovered while the cost of stopping is zero." The ladder gives no row for "the participant set grew during reading".

Two implementors would build differently here: one enumerates transitively-closed participants up front (requiring reads it is forbidden to make), the other reads incrementally and abandons mid-way (producing the partial state the ADR says is impossible).

**Recommendation**: Either drop the transitive clause and bound participants to diff paths plus ADR-extract-named files only, or add a ladder row for mid-read growth with a defined output (most naturally: the budget line, with the partial reads still charged and still appearing in `## Inputs used`).

---

### 6. ADR 0073's factor mapping has no path for FR-16's forced factor levels, while claiming totality (Score: 76)

FR-16 overrides two factor levels independently of the mapping table: rows 8 and 9 force `F5 = Medium` (requirements.md missing / declaring zero ids), row 12 forces `F1 = Medium` (spec branch not determinable). AC-16 and AC-19 test both. ADR 0073 handles neither.

**Evidence**:
- ADR 0073's shared procedure (L228–230): "For factor F with evidence set E: evaluate F's **High** condition over the whole of E… Otherwise F is **Low**." Plus L232–234: "FR-11's mapping is **total by construction**, so at least one column always holds."
- Applied to FR-16 row 12: no diff is measured, so the `src/` count is absent or zero, and the procedure yields **Low** where AC-19 requires **Medium**. Applied to rows 8/9: there are no deviation entries, so F5's Low column ("no deviation entries") holds, and the procedure yields **Low** where AC-16 requires **Medium**.
- `grep -n 'FR-16' docs/adr/0073-*.md` returns nothing — ADR 0073 never mentions FR-16.
- ADR 0072 L333–338 does describe the row 12–15 effects, but only on `Blast radius`, `Where to look first`, `Breaking changes` and the metadata block; it does not carry the F1 override either.

This is requirements pass-7 finding #11 (scored 58, knowingly unfixed) propagated into the design, where it scores higher: in `requirements.md` it was a missing cross-reference; in the ADR it is a mapping procedure that produces the wrong answer on two tested cases.

**Recommendation**: Add a step 0 to Key Components 3's procedure — "if FR-16 assigns this factor a level, take it and evaluate no column" — and state the two override sources by row number in Step 6.b.

---

### 7. ADR 0072's NFR-2 word-count `awk` does not exclude fenced blocks, and ADR 0077 asserts that it does (Score: 75)

NFR-2 exclusion (d) excludes "every line from an opening ``` fence through its matching closing fence, inclusive". ADR 0077's "Step 8 — no change" (L430–432) rests on this: "NFR-2's word count already excludes every line from an opening fence through its closing fence, so a diagram moves no counted word. The budget self-check is untouched." The published `awk` in 0072 has no fence handling at all.

**Evidence** — ADR 0072 L456–463:
```
awk 'BEGIN{b=0}
     /^## /{h=1}
     /^## Inputs used/{exit}
     h==1 && !/^[[:space:]]*\|/ {
       for (i=1;i<=NF;i++) if ($i ~ /[[:alnum:]]/) b++
     }
     END{print b}' "specs/{dir}/show-me.md"
```
Run against a minimal file containing one `## What changed and why` heading, three prose words and a 4-line mermaid block, it returns **16** where the correct answer is **8**. Two 40-line diagrams (AC-59's case) would add several hundred spurious counted words and drive Step 8 into the "revise and re-`Write`" path for a conforming file.

**Recommendation**: Add fence-state tracking to the `awk` in 0072 Step 8 (`/^```/{f=!f;next} f{next}`), and change 0077's "Step 8 — no change" to record that the check was extended.

---

### 8. ADR 0072's declared-id greps are broken as written — escaped `|` inside a markdown table cell (Score: 75)

This is the exact defect `requirements.md` recorded and fixed for itself ("they are given outside it because the pattern contains a `|` that a table cell cannot carry literally"). ADR 0072 still carries the escaped form, in the cell that specifies how FR-8's declared-id set is produced.

**Evidence** — ADR 0072 L403:
```
| `requirements.md` | `grep -nE '^\*\*(FR\|NFR)-[0-9]+'` and `grep -nE '^#{1,6}.*(FR\|NFR)-[0-9]+'` for declared ids, then a full `Read` for paraphrases | 1 |
```
Run verbatim against this spec's own `requirements.md`:
```
$ grep -cE '^\*\*(FR\|NFR)-[0-9]+' specs/0037-show-me/requirements.md
0                    (exit 1)
$ grep -cE '^\*\*(FR|NFR)-[0-9]+' specs/0037-show-me/requirements.md
28
```
In ERE, `(FR\|NFR)` matches the literal string `FR|NFR`. An implementing agent copying the raw ADR gets zero declared ids, `{total}` = 0, and `## Did it ship what it said?` collapses — and NFR-3's "report `Unverifiable`, never zero" rule will not fire, because the grep *succeeded* at matching nothing.

**Recommendation**: Move both patterns out of the table into a fenced block, as `requirements.md` does, with the `|` unescaped.

---

### 9. ADR 0073's FR-13 invariant check has no word boundaries and false-positives on ordinary prose (Score: 74)

ADR 0073 presents this as the external, mechanical check that makes "advisory" structural, and 0073 L358–360 says it "is run as an acceptance check (AC-25's structural half) rather than left as advice".

**Evidence** — ADR 0073 L263–265:
```bash
grep -nE '(if|when|unless).*(High|Low|Medium)' .claude/commands/spec/show-me.md   # must be empty
```
`(if|when|unless)` is unanchored, and BSD `grep` has no `\b` (a constraint ADR 0072 L257–259 itself records). `if` therefore matches inside `diff`, `classified`, `specify`, `verify`, `different`. Run against the current command file:
```
496: **unclassified**, risk-scored as Medium by F3 but reported in FR-9's own `{u}` slot — distinct from
728: no diff measured.` followed by the three rules tried — and still score F1 Medium, per Step 3.
787: unclassified and count as Medium; but even where a word is given, `nit`/`low`/`medium` dominate in
```
Three hits, none of them a conditional on the level; line 728 matches only because `diff` contains `if`. A command file whose whole job is to *render* `Low`/`Medium`/`High` and to describe a diff will essentially never produce an empty result, so a check stated as "must be empty" will be either permanently red or routinely waived — which is the same as not having it.

(Lines 496 and 787 also show the command file still carries the pre-rescope F3/severity implementation. That is implementation state, not an ADR defect, but it is the state the tasks revision has to unwind.)

**Recommendation**: Use a BSD-portable word-boundary form (`[[:<:]](if|when|unless)[[:>:]]`) or, better, replace the negative pattern with a positive assertion — the level appears in exactly two named sinks — since a substring grep cannot distinguish a conditional from a rendering.

---

### 10. ADR 0073 narrates its own revision history and the spec's phase (Score: 65)

`.agent_instructions/documentation.md` § *Writing tone* is explicit: "**State the rule that holds, not the revision that changed it.** *'FR-27.1 was amended in revision 28 to match'* records the requirements document's history; the ADR records what is now true… An ADR that repeats it dates itself." The heading itself names the spec phase.

**Evidence** (`docs/adr/0073-show-me-advisory-risk-model.md`):
- L55: `### This ADR was rescoped after implementation had begun` — a non-skeleton H3 inside `## Context`, whose title is ephemeral spec-phase state.
- L57–60: "The version of this ADR accepted on 2026-09-19 was titled *Review-History Decomposition and the Five-Factor Risk Model*, and the majority of it specified how to decompose…"
- L62–66: "That material was removed from the requirements on 2026-09-20… **The framing that settled it**: this command is the end-of-sprint demo, not a review." — the framing came from the authoring conversation; the sentence records who said what rather than what is true.
- L170: "The Classifier was introduced by **this ADR's previous version**"; L183: "Its contract is **unchanged from the previous version**"; L285: "**This is a change from the previous version of this ADR**, which needed `--json reviews` and `--json statusCheckRollup`"; L409–411, Alternative 3: "Both factors worked — F4's three-way `.conclusion // .state // .status` fallback was calibrated against PR #4282's heterogeneous 28-entry rollup"; L438: "The review-history and CI figures **the previous version of this ADR** calibrated against the same PR are recorded in git history."

The durable substance here is one rule — **F3 and F4 are retired identifiers and must never be reused** (L70–72) — and that rule must stay. Everything around it dates the document.

**Recommendation**: Delete the `### This ADR was rescoped…` section; move its one durable sentence (the F3/F4 retirement and its non-conformance consequence) into `### The forces` or Key Components 2, stated as a rule. Cut "unchanged from the previous version", "This is a change from the previous version", and Alternative 3's recounting of what the previous version calibrated — Alternative 3 can reject on scope without narrating that the rejected option was once implemented.

---

### 11. Heading drift across the set and from the canonical skeleton (Score: 65)

`.agent_instructions/documentation.md` § *ADR structure*: "use these headings verbatim, in this order, at this nesting level." Four divergences, one of which puts 0072 out of step with both its siblings.

**Evidence**:
- **`### Scope` is missing from all three.** Each uses a bold lead-in instead — 0072 L36, 0073 L35, 0077 L36: `**Scope**: …` — written as narrative rather than the required *Parent requirement* / *In scope* (one bullet per FR/NFR, each naming the mechanism) / *Out of scope* lists. The same section explicitly calls this out: "**A bold lead-in is a heading that was never promoted.**" It also costs real information: no ADR carries the requirement-by-requirement In-scope list that would have made findings 3 and 6 visible to its own author.
- **0072 has `### Architecture Overview` (L129) with a hand-drawn ASCII box diagram** where both siblings correctly carry `### The mechanism, end to end` (0073 L102, 0077 L111) followed by `### Where the pieces live` (0073 L126, 0077 L148). § *Diagrams in ADRs* opens "Prefer **mermaid** to ASCII art".
- **0072 has no `### The forces`**; 0073 (L75) and 0077 (L80) both do.
- **Only 0077 carries `### Terms`** (L46). ADR 0073 introduces "Classifier" — a domain word its Decision turns on, used 14 times — with no Terms block, contrary to "Where an ADR introduces a domain word its Decision turns on, that ADR owes a `### Terms` block in `## Context`, ahead of `### Scope`."

Orientation itself is fine in all three despite the waived roles table: 0073 leads Key Components with a stage table (L156–161) and the Decision with a rendered flowchart; 0077 leads with the seven-row decision ladder (L116–124), which is exactly the form § *Diagrams in ADRs* prescribes for "a protocol with more than about four decision points". 0072 leads Key Components with the two-stage table (L120–123). No section makes the reader assemble a picture from paragraphs first.

**Recommendation**: Promote the three `**Scope**:` lead-ins to `### Scope` with the three required lists; add `### The forces` to 0072 and split its `### Architecture Overview` into `### The mechanism, end to end` (mermaid) and `### Where the pieces live`; add a pointer-form `### Terms` to 0073 for `Classifier` and `Factor`.

---

### 12. ADR references are bare numbers throughout, in a corpus where 0072 and 0073 are dual-numbered (Score: 62)

`docs/adr/` on this branch contains **both** `0072-ambient-scope-adoption-seam.md` and `0072-show-me-command-resolution-and-output.md`, and **both** `0073-aspnet-core-request-scope-package.md` and `0073-show-me-advisory-risk-model.md`. `.agent_instructions/adr_frontmatter.md`'s rule — which all three ADRs cite in their own References — is that "the number is a non-unique ordering hint; identity is the filename stem".

**Evidence**:
- The `### Where this ADR sits` tables use bare numbers as link text in all three: `| **[0072](0072-show-me-command-resolution-and-output.md)** *(this one)* |` (0072 L46), `| [0073](0073-show-me-advisory-risk-model.md) |` (0072 L47), and the same shape at 0073 L48–50 and 0077 L62–64.
- Body prose does the same throughout, without the disambiguating href: 0073 L158 "| Measurer | **ADR 0072**, Steps 3–5 |", L160 "| Classifier | this ADR, Step 6.b |", L214 "ADR 0072 already defines"; 0077 L71 "**ADR 0073** decides how a level is computed", L152 `subgraph ADR0072["ADR 0072 - resolution and output"]`, L226 "ADR 0072's budget table". A reader who resolves "ADR 0072" in this repository lands on the ambient-scope-adoption ADR as readily as on this one.
- `docs/adr/index.md` is current and correct — it lists all three show-me ADRs (lines 113, 115, 119) by stem with the right statuses, and 0077 is present. The maps themselves are internally correct: all three list all three rows, each bolds its own row and marks it *(this one)*, and no map names the dead slug.

**Evidence that the dead slug is clean in `docs/adr/`**: `grep -rn '0073-show-me-review-history-and-risk-model' docs/ specs/` returns **no hits under `docs/adr/`** — only `specs/0037-show-me/tasks.md:3`, `tasks.md:636` (known, deferred to the tasks revision) and `review-requirements-pass7.md:259,268` (a dated record; correctly untouched).

**Recommendation**: Give the `Where this ADR sits` link text the filename stem (`[0072-show-me-command-resolution-and-output]`), and on first mention in each ADR's body use the stem or the full title before falling back to a short form.

---

### 13. ADR 0077's ladder row 3 does not terminate in output, contradicting the property the ADR reads off the ladder (Score: 60)

**Evidence** — 0077 L113–128. Row 3: "| 3 | D1, D2 and D3 all fail, and the Explainer raises under FR-6 (b) | states its one-sentence reason, **then continues at row 5** | **the diagram, plus that reason** |". Immediately below, L126: "Three properties read off the ladder. **Every row terminates in output**, so there is no path on which the command draws nothing and says nothing."

Row 3 is the one row that does not terminate; it hands off to row 5, which can produce the **budget line** instead of a diagram. Its third column therefore asserts an outcome the row cannot guarantee. The table is presented as "the whole of FR-6 (a), (b), (e) and (f)" (L113–114) and as the authoritative quoting of every fallback line (L340–341), so the error is in the section's orienting artefact.

There is a second, smaller ordering gap: row 3 jumps past row 4, so a raise whose evidence then fails to cohere has no stated outcome. FR-6 (b)'s stand-down is defined only for a *fired* test, so this may be intentional — but the ladder does not say so.

**Recommendation**: Change row 3's outcome column to "continues at row 5; carries whichever output row 5 or row 7 produces, plus that reason", and soften L126 to "every row terminates in output or in a later row of this ladder".

---

### 14. ADR 0077 reproduces the escaped-pipe-in-a-table-cell trap in a line the command must emit verbatim (Score: 60)

`requirements.md`'s FR-6 (e) gives the no-trigger line as `…across {b} director{y|ies}, …` (line 308), outside any table, and the Definitions section explains why patterns containing `|` are kept out of table cells. ADR 0077 quotes the same line inside its ladder table with the pipe escaped.

**Evidence** — `docs/adr/0077-show-me-visual-explanation.md` L119:
```
| 2 | … | reads nothing | `No diagram: {a} files changed under src/ across {b} director{y\|ies}, {c} changed public API declaration lines, {d} ADRs — no structural relationship to draw.` |
```
The ADR states at L340–341 that "AC-58, AC-61 and AC-65 quote the line text instead, so the text is the identifier that holds. **The ladder table above quotes each line in full for that reason.**" The table is therefore the ADR's authoritative copy of the text, and its raw form carries a backslash that must not appear in the emitted line. (`grep -c '&lt;\|&gt;\|&amp;'` is 0 for all three ADRs, so this is the only escaping defect of its kind besides finding 8.)

**Recommendation**: Move the five fallback lines out of the ladder's last column into a bulleted list beneath it, quoting each unescaped, and leave the table column naming the line by its FR-6 (e) name.

---

### 15. ADR 0077 claims to discharge FR-6 (a) in full but never states D1/D2/D3's thresholds (Score: 55)

**Evidence** — 0077 L36–39: "It discharges FR-6's `##### Visual explanation` **in full**, FR-14's optional-tree clause…". Its Key Components 5 table (L313–317) gives only each test's *input* and *producer*: "| D1 | files changed under `src/`, and distinct immediate subdirectories of `src/` | Step 5 blast radius |". The numeric thresholds (≥ 5 files **and** ≥ 2 subdirectories; ≥ 10 public API declaration lines; ≥ 2 resolved `.adr-list` entries) appear nowhere in the ADR. L321 gives the calibration *values* ("76 files across six subdirectories, 131 changed declaration lines, and seven resolved ADRs") without the thresholds they are compared against, so a reader cannot check the claim "all three fire" from the ADR alone.

The related `Immediate subdirectory of src/` boundary case *is* handled well: L397–398 explains the `NF>2` guard against `src/Directory.Build.props`, and the stated `awk` is correct (verified below).

**Recommendation**: Add the three thresholds to the Key Components 5 table as a fourth column, or state explicitly that FR-6 (a)'s thresholds are re-decided nowhere and are read from `requirements.md`.

---

### 16. ADR 0072's public-API pattern differs from the one `requirements.md` says must be used verbatim (Score: 55)

**Evidence** — `requirements.md`'s Definitions state the canonical pattern in a fenced block and add "Implementations must use the extended form above **verbatim**" and "Every consumer of this number reads this one definition, so the three can never disagree":
```
^[+-][[:space:]]*(public|protected)[[:space:]]
```
ADR 0072 states a different one, three times: L260–262 "`^[+-][[:space:]]*(public|protected)[^[:alnum:]_]` for a public API declaration line"; L382 (git source); L385 (`^[+-][ \t]*(public|protected)[^A-Za-z0-9_]`, PR source). The command file has copied 0072's form (`.claude/commands/spec/show-me.md:66,269,272`).

I ran both against the calibration diff (merge base `6145913a0`, branch `spec/scoped-lifetime-per-pipeline`): **both return 131, and `diff` of the two match sets is empty.** So there is no observed behavioural divergence, which is why this is Medium and not higher — but it is exactly the drift the "one definition, three consumers" clause exists to prevent, and `[^[:alnum:]_]` additionally matches `public(`, `public:` and `public.`, which the canonical pattern does not.

**Recommendation**: Replace all three occurrences in 0072 (and the corresponding command-file lines) with the canonical `[[:space:]]` form, and state that the pattern is quoted from the Definitions rather than restated.

---

### 17. One `Where this ADR sits` row is worded differently from its two siblings (Score: 45)

**Evidence**: ADR 0072 L48 describes 0077 as "When the command draws a diagram, what it may draw, and **who is allowed to read code** to draw it". ADR 0073 L50 and ADR 0077 L64 both say "…and **which stage may read code** to draw it". The 0072 and 0073 rows are otherwise word-identical across all three maps. The unifying sentence *is* identical in all three (one occurrence each, verified by grep).

**Recommendation**: Make 0072's third row match its siblings word for word.

---

### 18. NFR-4 (works offline) is addressed in substance but named by no ADR (Score: 45)

**Evidence**: `grep -n 'NFR-4' docs/adr/007{2,3,7}-show-me*.md` returns nothing. The behaviour is designed — 0072 L349–350 handles "a non-zero exit from `gh` (unavailable, unauthenticated, offline) → no PR, FR-16 rows 1–2", and the `git diff` fallback is the default diff source — but no ADR traces it to the NFR, so a coverage check by id shows a gap. The other unmentioned NFRs are fine: NFR-5 (0072 L213, L449), NFR-6 (0072 L94, L173, L478) and NFR-8 (0072 L99, L467, L536) are all cited.

**Recommendation**: Cite NFR-4 alongside FR-16 rows 1–2 in 0072, ideally in the `### Scope` In-scope list this review asks for in finding 11.

---

### 19. `Where the pieces live` in 0077 omits the Measurer's charge against the read set (Score: 40)

**Evidence**: 0077 L166–172 draws `EX --> RS` and `S --> RS` and reads two conclusions off them (L175–177: "The Explainer and the Synthesiser both charge the same read set, which is why there is one budget"). But L224–225's order of spend begins with "`requirements.md` at **Step 5**", which ADR 0072's budget table (L403) charges as a full `Read` — a Measurer-stage read. There is no `M --> RS` edge. I rendered the diagram to PNG and inspected it: it is legible and otherwise faithful, but a reader counting chargers off the picture counts two where the text names three (four once `release_notes.md` is placed — finding 2).

**Recommendation**: Add `M --> RS` and adjust the sentence to "three stages charge the same read set".

---

### 20. Dead-slug references remain in `tasks.md` (context only) (Score: 35)

**Evidence**: `specs/0037-show-me/tasks.md:3` still names "ADR 0073 — Review-History Decomposition and the Five-Factor Risk Model (`0073-show-me-review-history-and-risk-model`)" with a link to the non-existent `docs/adr/0073-show-me-review-history-and-risk-model.md`, and `tasks.md:636` names the same dead filename in T6's fixture description. Both are known and deliberately deferred to the tasks revision, and `review-requirements-pass7.md:259,268` is a dated record that must not be updated. Reported for completeness only; nothing in `docs/adr/` carries the dead slug.

**Recommendation**: None for this phase — fold into the `tasks.md` revision, which must also add T-tasks for FR-9's section and remove the F3/F4/review-history tasks the command file was built from.

---

## Verification log

**Mermaid renders** — all four blocks extracted to the scratchpad and rendered with `npx -y -p @mermaid-js/mermaid-cli@11 mmdc`. Network was available and `npx` ran.

| Block | ADR, lines | `mmdc` exit | SVG |
|---|---|---|---|
| `0073…-d1` (`flowchart TD`, the risk mechanism) | 0073 L104–118 | 0 | 27,165 bytes — PASS |
| `0073…-d2` (`flowchart LR`, where the pieces live) | 0073 L128–144 | 0 | 16,360 bytes — PASS |
| `0077…-d1` (`sequenceDiagram`) | 0077 L132–143 | 0 | 25,152 bytes — PASS |
| `0077…-d2` (`flowchart LR`, four subgraphs) | 0077 L150–173 | 0 | 21,516 bytes — PASS |

**Visual inspection** — rendered `0073-d1` and `0077-d2` to PNG at `-w 1600 -b white` and read both images. `0073-d1` is legible: `A --> B & C & D` fans out correctly to all three factor nodes, the single decision diamond has both labelled edges, and the two sinks match the prose's "exactly two arrows". No stray `**bold**` in any label, no swallowed angle bracket. `0077-d2` is legible; the only defect found by looking is finding 19 (missing `M --> RS`).

**Escaped-markdown grep** — `grep -c '&lt;\|&gt;\|&amp;'`: `0072-show-me-command-resolution-and-output.md` → **0**; `0073-show-me-advisory-risk-model.md` → **0**; `0077-show-me-visual-explanation.md` → **0**. All pass.

**Dead-slug grep** — `grep -rn '0073-show-me-review-history-and-risk-model' docs/ specs/ .claude/` → 4 hits, **none in `docs/adr/`**: `specs/0037-show-me/tasks.md:3`, `:636` (deferred), `specs/0037-show-me/review-requirements-pass7.md:259`, `:268` (dated record, correctly untouched).

**`PROMPT` grep** — 3 hits across the three ADRs (0072 L215, L217; 0077 L228), all substantive rules about FR-17's tracking test and the read set. No reference to `PROMPT.md` as ephemeral working state. Pass.

**Conversation-participant grep** — `grep -niE "at the user's|the user (explicitly|chose|asked|wanted|directed)|per the user|as discussed|we agreed|in conversation|reviewer asked|review round|review pass"`: 5 hits, none naming a participant. Four are substantive uses of "review round(s)" as a domain noun (0072 L30, L501, L632, L651); one is the heading "Why the format follows the relationship, not the author" (0077 L365), which is about the diagram's author, not a conversation participant. The tone defect that *is* present is revision-history narration, filed as finding 10.

**`F3`/`F4` grep** — 10 hits. Nine in 0073 are correct (retirement statement L70–72, Negative L333, Risks L361, Alternative 3 L404–411). One in 0072 (L507) treats `F3` as live, and one (L72) treats `F4` as live — finding 1.

**`Where this ADR sits` maps** — all three present, each lists all three ADRs, each bolds its own row and marks it *(this one)*, unifying sentence present exactly once in each and byte-identical. `docs/adr/index.md` is current (0077 present at line 119, 0073's new title at 115). Only defects: bare-number link text (finding 12) and one reworded cell (finding 17).

**Calibration figures re-derived** — merge base `git merge-base origin/master spec/scoped-lifetime-per-pipeline` = `6145913a0ae63c638bd5267c90b45c0b1f32ecfe`, matching the figure the ADRs cite. I measured `6145913a0..spec/scoped-lifetime-per-pipeline` (**not** `origin/master..HEAD`, which returns 535 files because this branch carries the `--no-ff` merge of `spec/scoped-lifetime-per-pipeline` plus spec 0037's own paperwork):

| Figure | ADR claims | Measured | |
|---|---|---|---|
| total files changed | 517 | **517** | ✓ |
| under `src/` | 76 | **76** | ✓ |
| `tests/` / `docs/` / `specs/` / `.github/` / `other` | 393 / 14 / 24 / 0 / 10 | **393 / 14 / 24 / 0 / 10** (sum 517) | ✓ |
| distinct immediate subdirs of `src/` (`awk -F/ '$1=="src" && NF>2 …'`) | 6 | **6** | ✓ |
| changed public API declaration lines | 131 | **131** | ✓ |
| `.adr-list` entries | 7 | **7** | ✓ |
| candidate ranking, first two | `HandlerLifetimeScope.cs` (11), `ScopeConfigurationRules.cs` (7) | **11 and 7, in that order** | ✓ |

Every calibration figure in ADR 0077 checks out exactly, including the per-file ranking claim at L254–256.

**Shell commands run from the ADRs**:
- 0072 L403 declared-id greps → **0 matches** as written, **28** unescaped (finding 8).
- 0072 L456–463 NFR-2 `awk` → **16** on a fixture whose correct count is **8** (finding 7).
- 0072 L382 API-declaration pipeline → 131, identical match set to `requirements.md`'s canonical pattern (finding 16 is conformance-only).
- 0073 L263–265 FR-13 invariant grep → **3 false positives** on the current command file (finding 9).
- 0077 L293–295 caps checks → verified correct on a two-diagram fixture (fence count 4, block lengths 4 and 3, long-line count 0).
- 0077 L394 subdirectory `awk` → 6, correct, and the `NF>2` guard behaves as documented.

**Repository claims checked**: `.claude/settings.json`'s `gh` allow-list is exactly `gh pr view`, `gh pr list`, `gh pr diff`, `gh issue view`, `gh issue list` — 0072 L81–85 correct. 0072's "eleven files under `.claude/commands/spec/`, nine carrying `argument-hint`" is correct for the pre-existing family (12 command files today, of which `show-me.md` is this spec's own and `status.md`/`tasks.md` carry no hint). `docs/adr/0072-ambient-scope-adoption-seam.md`, `0073-aspnet-core-request-scope-package.md`, `0071-tdd-review-gear.md`, `.agent_instructions/adr_frontmatter.md`, `.claude/commands/spec/{status,review,switch,README}.md` all exist. `generate_adr_index.awk` exists at `.claude/commands/adr/generate_adr_index.awk` — 0072 cites it by bare filename without a path, so this is **not** a broken reference.

**Not checked / could not check**: nothing. All renders, greps and shell snippets ran.

**Concerns from the brief that did NOT hold**: the diagram render check is clean (all four pass and both inspected renders are faithful); the escaped-markdown check is clean; no `docs/adr/*.md` file names the dead slug; `docs/adr/index.md` is current; the unifying sentence is byte-identical across all three; the stage tables in 0073 and 0077 are word-identical apart from the this-ADR marker; determinism (NFR-1) vs. the Explainer's read right is handled honestly rather than hand-waved (0077 L212–214, L467–469 name the judged fields and cite NFR-1 rather than promising sensible judgement); the ranking's three sort keys do make the order total; and requirements pass-7 finding #13 (AC-60 over-asserting) has **not** propagated — 0077's Step 7 assertions trace to FR-17 and FR-14, not to AC-60.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 1 |
| 70-89 (High) | 8 |
| 50-69 (Medium) | 7 |
| 0-49 (Low) | 4 |

**Total findings**: 20
**Findings at or above threshold (60)**: 14
