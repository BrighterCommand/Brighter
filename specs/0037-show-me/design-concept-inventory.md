# Design concept inventory — spec 0037 (working file for the ADR redesign)

Built 2026-09-25 from the approved `requirements.md` (`e791cc101`). The rule: **each concept has one
owning ADR, which states it; every other ADR refers to it and never restates it.** A requirement
that is already fully stated in `requirements.md` is *discharged* by its owner (the owner names the
mechanism), not re-stated.

ADRs in the set after the redesign:

| ADR | Decides |
|---|---|
| `0072-show-me-command-resolution-and-output` | What the command is: the two artefacts and the seam between them, how it resolves its target, what it measures and reads, and the file it writes |
| `0073-show-me-advisory-risk-model` | How the advisory risk level is computed, and how it stays advisory |
| `0077-show-me-visual-explanation` | When the command draws a diagram, what it may draw, and which stage may read source to draw it |
| `0078-…` *(new)* | The forms the `/spec` family writes so that a tool can read them — declared ids, task tags, marked release-notes sections — and the `/spec:write_release_notes` command that writes the last |

## 1. Concepts and owners

### The seam and the command's structure — owner 0072

| # | Concept | Requirement source | Owner | Referred to by |
|---|---|---|---|---|
| 1 | Two artefacts, one seam: the script **measures**, the command **reads and judges** | FR-21 *The split*, NFR-1 | 0072 | 0073, 0077 (stage tables point here) |
| 2 | The seam's line, restated: *counting vs. reading* — not *shell vs. no shell*. The command may issue path-scoped `git diff` over the pinned shas and read files, but never computes a value the ledger carries (AC-70) | FR-18 ¶3, FR-14, NFR-3, Additional Context *Open design question* | 0072 | 0073 (Classifier inputs), 0077 (Explainer reads) |
| 3 | Stage vocabulary: Measurer (the script) · Classifier · Explainer · Synthesiser (the model). One stage table, stated once | — (design) | 0072 states the table; each stage's *rule* is owned by the ADR that introduces it | 0073, 0077 |
| 4 | Step order: gate → measure → read → explain → classify → synthesise → write → word count → report; print order ≠ computation order | FR-3 ordering, FR-5 | 0072 | 0073 (6.b after 6.a), 0077 (5D) |
| 5 | No sub-agent; runs in the main agent | — (design) | 0072 | 0077 (Explainer inline — refers, adds its own reason) |
| 6 | Command front matter (`allowed-tools`, description, argument-hint) | NFR-6, FR-18 | 0072 | — |

### Target resolution and the gate — owner 0072

| # | Concept | Source | Owner |
|---|---|---|---|
| 7 | Whole-argument spec id; three-rule match over `ls -1d specs/*/`; ambiguity/no-match stops | FR-1, C-1, AC-1/1a/2/3/35 | 0072 |
| 8 | No-argument: `specs/.current-spec` missing/empty/stale | FR-2, AC-4/5 | 0072 |
| 9 | Completeness gate; three FR-3 messages; **exit 2** + gate record distinguishes it from a tooling fault; gate above both writes | FR-3, FR-21, AC-6/7/8/71 | 0072 |
| 10 | The four stops and only four (FR-1, FR-2, FR-3, FR-21) | FR-3 ¶2 | 0072 |

### The measurement script — owner 0072

| # | Concept | Source | Owner |
|---|---|---|---|
| 11 | Language (C# file-based app), filename, invocation, allow-list entry string, file mode, first-line marker | FR-21, NFR-6, FR-18, C-10, AC-53, AC-82 | 0072 |
| 12 | Exit statuses 0 / 2 / other (measuring mode); 0 / other (word-count mode) | FR-21 | 0072 |
| 13 | stderr records `show-me-gate:` / `show-me-wordcount:` — last prefixed line wins; children's stderr captured | FR-21 | 0072 |
| 14 | Nothing parsed from stdout (toolchain pollution) | FR-21, AC-70 | 0072 |
| 15 | Ref / diff / file field groups; the **kinds of run** table; pinned flag; null + reason | FR-21 | 0072 (discharges; does not restate the table) |
| 16 | Pinned invocation (pinned pair, release-notes path) — inputs, not fault hooks; never passed by the command | FR-21, AC-93 | 0072 |
| 17 | Every stated pattern implemented exactly once, in the script; any dialect translation written beside the POSIX original | Definitions, FR-21 | 0072 (the *implementation* rule) — the *patterns* live only in `requirements.md`; **no ADR transcribes a pattern** |
| 18 | `{m}` counting rule; marked-section *recognition* (heading/fence/extent rules) | FR-21 *The {m} rule*, Definitions | 0072 (reader side) — the *form* is 0078's |
| 19 | Ledger cap 65,536 B | FR-21 | 0072 |
| 20 | Atomic write; no temp residue; fifth failure state leaves the replaced ledger | FR-21, AC-83, NFR-8 | 0072 |
| 21 | Word-count mode: NFR-2's rule executed by the script; report, never revise, never re-invoke | FR-21 *Modes*, NFR-2, AC-33/80 | 0072 |
| 22 | The one failure mode and its exact message; no inline fallback | FR-21, AC-72 | 0072 |
| 23 | Offline: exit 0 with PR fields null | FR-21, NFR-4 | 0072 |

### Refs, PR, diff — owner 0072

| # | Concept | Source | Owner |
|---|---|---|---|
| 24 | Spec-branch rules 1–3, remote-tracking wins, merged candidate skipped and named | FR-10, C-4, AC-47/85 | 0072 |
| 25 | Base ref `origin/master` else `master` | FR-10 | 0072 |
| 26 | PR discovery: the **script** runs `gh pr list … --state open`, exact `headRefName` filter, highest number wins, gh record in ledger | FR-20, FR-18, AC-46 | 0072 |
| 27 | Measured head (PR `headRefOid` if present locally, else branch tip); merge base computed by the script only | Definitions, FR-20, AC-84 | 0072 |
| 28 | One spec diff per run, pinned shas; `gh pr diff` never used; full diff banned (script too) | FR-20, NFR-3, AC-73/77 | 0072 |

### Reads and the byte budget — owner 0072 (the reserve's *spending* is 0077's)

| # | Concept | Source | Owner |
|---|---|---|---|
| 29 | 1,048,576 charged bytes per run; `wc -c` before every full read, charged nothing; script JSON charged, its unemitted reads not | NFR-3, Definitions *Charged bytes*, AC-52/76 | 0072 |
| 30 | 100,000 B reserve, spendable only on diagram source reads | NFR-3, FR-6 (f) | 0072 states the budget and reserve; **0077 owns which reads draw on it and the budget line** |
| 31 | Degradation: chunk / targeted extraction, never skip; `Unverifiable` / `not available`, never zero | NFR-3, AC-52 | 0072 |
| 32 | The command's own evidence reads: `tasks.md` in full, `src/`-scoped diff, ADR extracts (front matter + Status + Consequences), `requirements.md` by extraction when unaffordable, marked release-notes section(s) together-or-none | NFR-3, FR-7, FR-14, AC-68/69/78 | 0072 |
| 33 | The read log — the in-context record of every read and its charged bytes (replaces 0077's cardinality "read set"; **not** the fact ledger, which the command never writes) | NFR-3, FR-15, AC-63 | 0072 |

### The output file — owner 0072 (section by section)

| # | Concept | Source | Owner |
|---|---|---|---|
| 34 | Two writes; `.show-me-ledger.json` gitignored exact-match; replace wholly; no staging | FR-4, NFR-8, AC-9/10/30/74 | 0072 |
| 35 | H1, metadata block, eight H2s in order | FR-5, AC-11 | 0072 |
| 36 | FR-6 narrative: 150–600 words, every `.adr-list` ADR by slug + title + Status, gloss rule | FR-6 ¶1–3, C-9, AC-12/51 | 0072 |
| 37 | ADR references by filename stem, never bare number (whole file) | FR-6, C-9 | 0072 |
| 38 | FR-7 section: item rules, ≤ 40 words, classification set, count line, marked-section read obligation, tie-break, disagreement line, rows 5/5a/14 lines | FR-7, AC-13/14/68/69/92 | 0072 (section shape and read obligation); **item judgement is the Classifier's (0073)** |
| 39 | FR-8 section: four parts, collapse rules, partition invariant, count line, status set, sub-clause precedence | FR-8, AC-15/45/54/55 | 0072 (section shape and the mechanical id list); **status judgement is the Classifier's (0073)** |
| 40 | **FR-9 section** — task shape + commit count + the not-determinable fallback line; **no review/CI** (review-design #3) | FR-9, AC-17 | **0072** |
| 41 | FR-10 section: six-bucket table, the prose lines, provenance lines | FR-10, AC-18/19 | 0072 |
| 42 | FR-14 path list: 3–7 paths from the pinned diff, ≤ 25-word reasons, row 13 fallback | FR-14, AC-26/36 | 0072 (the tree is 0077's) |
| 43 | FR-15 Inputs used: rows, marks, no script/ledger/review/CI rows | FR-15, AC-27/75 | 0072 |
| 44 | FR-16 rows 1, 2, 5, 5a, 6, 7, 10, 11, 12, 13, 14, 15, 16 (the section effects) | FR-16 | 0072 (rows 8/9/12's **factor** effects are 0073's; row 12's **diagram** line is 0077's) |
| 45 | FR-17 / NFR-5: tracked-paths-only, `git ls-files --error-unmatch` before a path is written; PROMPT never cited | FR-17, NFR-5, AC-29/41/75 | 0072 |
| 46 | FR-18 writes / GitHub / allow-list | FR-18, C-10, AC-30/82 | 0072 |
| 47 | FR-19 session report incl. word count or `word count unavailable` | FR-19, AC-31 | 0072 |
| 48 | NFR-1 mechanical vs judged list | NFR-1, AC-32 | 0072 (each judged field's owner is named there) |
| 49 | NFR-7 traceability (prose half) | NFR-7, AC-34 | 0072 (the diagram half is 0077's) |
| 50 | NFR-4 offline (review-design #18) | NFR-4, AC-28 | 0072 |

### Test script and repository changes — owner 0072

| # | Concept | Source | Owner |
|---|---|---|---|
| 51 | Sibling test script: language, invocation, mode; fixtures under `.claude/test-fixtures/show-me/`; ledger restore rule; residue check | NFR-9, AC-79/80/81/83 | 0072 |
| 52 | Calibration row pinned to `6145913a0` / `91d549be6`; (C-8) lapse on merge | NFR-9, C-8 | 0072 |
| 53 | FR-13 invariant check over the command file — the *mechanism* lives in the test script | NFR-9, AC-81 | 0072 hosts the check; **0073 owns what it asserts** |
| 54 | `CONTRIBUTING.md` merge-commit paragraph | NFR-9, AC-94 | 0072 |
| 55 | `.gitignore` line, `settings.json` entry | FR-4, FR-18, AC-82 | 0072 |

### Risk — owner 0073

| # | Concept | Source | Owner |
|---|---|---|---|
| 56 | Three factors F1/F2/F5, thresholds, highest-matching-column rule, one shared mapping procedure | FR-11, AC-20/21 | 0073 |
| 57 | **FR-16's forced levels**: rows 8 and 9 → F5 Medium, row 12 → F1 Medium (review-design #6) | FR-16, AC-16/19/43 | 0073 |
| 58 | F3/F4 retired and never reused | FR-11 | 0073 (as a rule, no history narration — review-design #10) |
| 59 | Overall = max; raise with reason, never lower; `**Overall risk: …**` line; 2–5 sentences | FR-12, AC-22/23 | 0073 |
| 60 | Advisory by construction; verbatim sentence; level has exactly two sinks; no branch on the level | FR-13, Definitions *Advisory*, AC-24/25 | 0073 |
| 61 | The Classifier: judges FR-7 items and FR-8 statuses once; F2/F5 read what the sections wrote (handoff rule). Its **inputs change**: the command's own evidence reads (#32), not "Measurer extracts" — the ledger now carries no text | FR-7, FR-8, NFR-1 | 0073 |
| 62 | What FR-13's invariant check asserts (the test script hosts it, #53) | NFR-9, AC-81 | 0073 |

### Diagrams — owner 0077

| # | Concept | Source | Owner |
|---|---|---|---|
| 63 | Trigger D1/D2/D3 read from the ledger; thresholds by pointer to FR-6 (a) (review-design #15) | FR-6 (a), AC-56/57/67 | 0077 |
| 64 | Raise / stand-down, and the five named fallback lines (by name, never by ordinal; quoted outside tables — review-design #14) | FR-6 (b), (e), AC-57/58/61/64/65 | 0077 |
| 65 | Format by relationship kind; attribution; tracked paths in diagrams | FR-6 (c), NFR-7, AC-56/60 | 0077 |
| 66 | Caps: ≤ 2 diagrams, ≤ 40 lines, ≤ 100 columns | FR-6 (d), AC-59 | 0077 |
| 67 | Diagram source reads draw on the reserve; affordability by `wc -c` before each read; budget line on exhaustion; **byte-denominated**, not a 25-file set (review-design #2, #5) | FR-6 (f), NFR-3, AC-58/63/78 | 0077 |
| 68 | Optional tree in `## Where to look first`; `(unchanged)` nodes; placement decided before synthesis | FR-14, AC-60/65 | 0077 |
| 69 | Diagram rows in `## Inputs used` (one per source file read, `used (targeted extraction)` form) | FR-15 | 0077 (row content) — 0072 owns the table |
| 70 | Row 12's no-diff line; trigger not evaluated | FR-16 row 12, AC-61 | 0077 |

### Formats the `/spec` family writes — owner 0078 (new)

| # | Concept | Source | Owner |
|---|---|---|---|
| 71 | Declaration form `**FR-n — title.**` (bold lead-in, optional list item, `FR-n.m` sub-clauses); legacy forms not recognised | FR-22, Definitions, AC-86 | 0078 |
| 72 | Task form `**TAG: T1.1 — …**` — tag first, four tags, template line per tag | FR-22, C-2, AC-87 | 0078 |
| 73 | `/spec:review` format checks (requirements, tasks) | FR-22, AC-88 | 0078 |
| 74 | Marked release-notes section form: `###` heading, marker line, summary, `#### Breaking changes` or `No breaking changes.`, optional `####` | FR-23, Definitions *Marked release-notes section* | 0078 (the form) — 0072 owns reading it (#18) |
| 75 | `/spec:write_release_notes`: resolution by FR-1/FR-2 rules, first `##` heading, replace in place, every stop incl. "stop and ask" | FR-23, AC-89/90/95 | 0078 |
| 76 | `/spec:design` step + `/spec:review` design check for a breaking change with no marked section | FR-23, AC-91 | 0078 |
| 77 | The form-vs-pattern rule: producers carry forms by example, never a pattern | FR-22 ¶ last, AC-86/87 | 0078 (refers to 0072 #17 for the exactly-once rule) |
| 78 | Who marks; unmarked sections not migrated or inferred | Definitions, Out of Scope | 0078 |

## 2. Design decisions the rewrite has to make (not fixed by requirements)

✅ **Owner confirmed D-a/D-b/D-c (count vs. read), D-d (C# test script) and D-g (0078 owns the writer) on 2026-09-25.** D-e and D-f are mechanical and taken as proposed.

| # | Decision | Proposed | Why it is open |
|---|---|---|---|
| D-a | Where the seam runs now that the command issues its own `git diff` reads | *Counting vs. reading*: the script owns every value NFR-1 lists; the command may read (files, pinned-sha path-scoped diffs) under the byte budget but computes no ledger value. The "no shell call in synthesis" rule is withdrawn | `requirements.md` *Open design question* hands it to the ADR |
| D-b | Where the diagram's reads sit | The Explainer, after the command's evidence reads, before classification/synthesis; only stage that reads source files outside the spec's artefacts and the `src/` diff; spends the reserve | Same open question |
| D-c | The Classifier's inputs | The command's evidence reads (ADR extracts, `src/` diff, marked section, `requirements.md` extracts, `tasks.md`), not "Measurer extracts" | Its old contract named a ledger that no longer carries text |
| D-d | Test script language | C# file-based app (`show_me_facts_tests.cs`), same reasons as the measurement script (every contributor has the SDK; Windows) | NFR-9 leaves it open |
| D-e | Atomic-write mechanism and temp-file name | Temp file in the target directory + `File.Move(…, overwrite: true)`; temp deleted in `finally`; residue on a killed process is a named risk | FR-21 fixes the property, not the mechanism |
| D-f | Record names in context | "read log" (bytes charged per read) and "node list" (one row per drawn node) — neither is called a *ledger*, so the fact ledger has one meaning | Vocabulary collision |
| D-g | 0078 also owns `/spec:write_release_notes`'s behaviour | Yes — the command exists only to write that form | Owner said "format contracts"; the command is the writer of one |

## 3. `review-design.md` (2026-09-20) findings against this inventory

| # | Score | Status after the redesign |
|---|---|---|
| 1 | 92 | Closed by rewrite of 0072's Decision/Consequences; 0072 → `Proposed` |
| 2 | 84 | **Superseded** — budget is bytes (#29–33); the marked section's read is charged and can be exhausted (row 5a reachable) |
| 3 | 80 | Closed — FR-9 owned by 0072 (#40) |
| 4 | 78 | Closed by D-a/D-b — the rule becomes "no *computed* value", and the Explainer's placement is re-argued |
| 5 | 78 | **Dissolved** — no participant-count set; affordability is per read by `wc -c` against the reserve (#67) |
| 6 | 76 | Closed — forced levels owned by 0073 (#57) |
| 7 | 75 | **Superseded** — word count is the script's mode; no ADR carries an `awk` |
| 8 | 75 | **Superseded** — no ADR transcribes a pattern (#17) |
| 9 | 74 | Closed by #53/#62 — check lives in the test script with no substring false-positive (AC-81) |
| 10 | 65 | Closed by rewrite of 0073 (no revision narration) |
| 11 | 65 | Closed — `### Scope` lists, `### The forces`, mermaid mechanism, `### Terms` in each ADR that coins a word |
| 12 | 62 | Closed — stems in link text and first mention |
| 13 | 60 | Closed by rewrite of 0077's ladder |
| 14 | 60 | Closed — fallback lines quoted outside tables (#64) |
| 15 | 55 | Closed — thresholds by pointer (#63) |
| 16 | 55 | **Superseded** — no ADR states the pattern (#17) |
| 17 | 45 | Closed — one wording for the map row |
| 18 | 45 | Closed — NFR-4 in 0072's Scope (#50) |
| 19 | 40 | Closed by the new "where the pieces live" diagram |
| 20 | 35 | Deferred to the `tasks.md` revision (unchanged) |
