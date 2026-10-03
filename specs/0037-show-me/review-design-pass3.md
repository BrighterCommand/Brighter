# Review: design — 0037-show-me (pass 3)

**Date**: 2026-09-25
**Threshold**: 60
**Verdict**: NEEDS WORK

**Note**: This spec carries a stale `.design-approved` marker from a pre-rescope approval; the ADRs were
rewritten afterwards and revised in `07b593d36`. This review is a genuine gate on the current design.

3 findings at or above threshold 60. Address these before approving.

## Findings

### 1. The path-scoped allow-list entry can be redirected to another C# file, so the ADR's security claim is false and AC-82 fails (Score: 90)

0072 chooses the entry `Bash(dotnet run .claude/commands/spec/show_me_facts.cs:*)`. It then claims, as
a measurement "on this repository", that arguments after the path cannot widen the entry. Run with SDK
10.0.401 from the repository root, `dotnet run a.cs --file b.cs` ran **b.cs** (it printed `B RAN` and
exited `0`). `dotnet run a.cs --project b.cs` tried to load b.cs as a project, and did not run a.cs
either.

So the entry lets the model run `dotnet run .claude/commands/spec/show_me_facts.cs --file {any}.cs`.
That runs any C# program, and a C# program can start `curl`, `wget` or `ssh`, so the `deny` list can be
bypassed. FR-18 exists to prevent exactly this. AC-82 requires a demonstration that "the entry does not
permit a second, differently-named program in that same language to run", and the design as written
fails it. The ADR's rationale rests on a measurement that does not reproduce.

**Evidence**:

- 0072:605-608: "Arguments after the path cannot widen it: measured on this repository,
  `dotnet run {file}.cs --project {other}` and `--file {other}` still run `{file}.cs`, passing the rest
  to it as arguments."
- 0072:564: the entry string.
- requirements.md:1166-1167: the entry must permit "that one program and nothing else".
- requirements.md:2219-2223 (AC-82): "demonstrated by showing that the entry does not permit a second,
  differently-named program in that same language to run".
- Reproduction, in the scratchpad: `a.cs` writes `A:{args}` to stderr and exits 2; `b.cs` writes
  `B RAN` and exits 0. `dotnet run $S/a.cs --file $S/b.cs` printed `B RAN` with exit=0.
  `dotnet run $S/a.cs -- --file $S/b.cs` printed `A:--file|…/b.cs` with exit=2.

**Recommendation**: Put `--` straight after the script path, both in the entry and in every invocation:
`Bash(dotnet run .claude/commands/spec/show_me_facts.cs -- :*)` or an equivalent form. Then every later
token reaches the script as an argument. `--` was verified to make `--file` inert. Re-measure the other
`dotnet run` options (`--project`, `-p:`/`--property`, `--launch-profile`) with that form. Replace
0072:605-608 with the measured result. Add a Risks entry saying that the entry's scope depends on
`dotnet run`'s option parsing, which can change between SDK versions.

---

### 2. The read design assumes a read brings in exactly `wc -c` bytes in one call, which the command's own tools do not do (Score: 70)

0072 prices every read by `wc -c` and prescribes several reads as "whole" single reads:

- `tasks.md`, whole;
- the `src/`-scoped diff (303,715 B), "whole, as one `git diff`".

0078 has `/spec:write_release_notes` "Read `release_notes.md`" (118,145 B) to find every heading and
marker. The ADRs never say which tool carries a read, or how a tool limit changes the charged bytes.

Observed in this review: `Read` on spec 0036's `tasks.md` returned lines 1-369 of 1,628. The tool
reported "93531 tokens, cap 25000". A whole read of `tasks.md` is therefore four or more calls, not one.
`Read` also adds `cat -n` line-number prefixes, so the charged bytes (requirements.md:123, "the bytes …
a read brings into the command's context") exceed `wc -c`. By Claude Code's documented default, Bash
output is middle-truncated at 30,000 characters (`BASH_MAX_OUTPUT_LENGTH`; not exercised here). A
303,715-byte `git diff` through Bash would then reach the model about 90% missing, with no signal. That
is the silent omission NFR-3's "chunk, never skip" forbids, and the charged figure recorded in the read
log would be false.

Two implementers would build different read mechanisms, and different charged-byte accounting, from
this text.

Side observation, a requirements-level issue and not scored here: 229,159 B / 93,531 tokens ≈ 2.45
B/token. NFR-3's budget rationale assumes about 3.5 B/token (requirements.md:1300-1301), so 1,048,576 B
is roughly 428K tokens, not the ~300K stated.

**Evidence**:

- 0072:434-435: "`tasks.md` | whole …"; "The `src/`-scoped diff … | whole, as one `git diff` with a
  `src/` pathspec".
- 0072:409-411: every read is "charged … and recorded in the read log with its path, how it was read,
  and the bytes charged".
- 0072:611-613: `allowed-tools` holds both `cat` and `Read`, with no rule for which one carries a read.
- 0078:294: "Read `release_notes.md` and find its `##` and `###` headings, ignoring any `#` line inside a
  fenced block".
- requirements.md:1368-1374: "must never silently omit data".

**Recommendation**: Add a read-mechanism rule to 0072 Key Components 3. Name the tool for each kind of
read. Define "whole" as consecutive bounded windows (Read `offset`/`limit`, or `git diff … | sed -n`
ranges) that each fit the tool's limits. Define charged bytes as the bytes each window actually
brought in, including any prefixes. For the diff, name a chunking method that stays inside the Bash
output limit. Apply the same rule to 0078's `release_notes.md` scan, where fence tracking must survive
across windows.

---

### 3. The no-trigger line's `{d}` (resolved ADR count) has no ledger field, so the model must count it (Score: 62)

0077 says the values `{a}`–`{d}` in the no-trigger line are "the ledger's, copied". It also says the
Explainer takes "the values they tested" from the ledger. The ledger has fields for `{a}` (`buckets`
src files), `{b}` (`src_subdirectory_count`) and `{c}` (`public_api_lines`). For D3 it holds only the
boolean `triggers.d3` and the per-entry `adr_list` array. The number of entries that resolved, which D3
tests and AC-57 requires ("`1` ADR"), is not a field.

To write the line, the model must count non-null `path` entries. That is a count over `.adr-list`,
which 0072's own *Terms* define as mechanically countable ("`.adr-list`" is in the artefact list), and
the seam forbids the model from computing one. One implementer would count in the model and another
would add a field.

**Evidence**:

- 0077:154: "The values `{a}` to `{d}` in the no-trigger line are the ledger's, copied."
- 0077:238: input "The ledger's D1, D2 and D3 outcomes and the values they tested".
- 0072:367: `triggers` holds `{d1, d2, d3}`, each `true` or `false`.
- 0072:371: `adr_list` holds one entry per `.adr-list` line.
- 0072:49-51 (Terms): mechanically countable covers counts over "`.adr-list`".
- requirements.md:2025-2026 (AC-57): "`1` ADR".

**Recommendation**: Add a diff-group field such as `adr_resolved_count`, the value D3 tests. Assert it
in 0072 Implementation Approach step 4 (the *declared* fixture resolves 2) and on the calibration row
(7).

---

### 4. 0077's attribution table cannot license a node for a declaration visible in the diff, which contradicts its own Context and narrows FR-6 (c) (Score: 55)

0077's Context table says the `src/`-scoped diff truthfully supports drawing "which declarations
changed, in which files". Its attribution table then licenses, from the diff, only a node naming a
**file**. It says "a type node needs a read or an ADR". The source file must be read by the Explainer,
and the diff is the command's read, not the Explainer's.

FR-6 (c) and AC-56 accept a node that "appears in the spec diff". The two tables disagree. One
implementer would draw a changed interface seen in a diff hunk, and another would refuse to.

**Evidence**:

- 0077:94: "the `src/`-scoped diff | which declarations changed, in which files".
- 0077:282-287: "a path in the spec diff | that file" … "A diff path licenses a node naming the
  **file**, not a type inside it; a type node needs a read or an ADR."
- requirements.md:407-409: "it appears in the spec diff, in an ADR named in `.adr-list`, or in a file
  the command read".
- requirements.md:2017-2018 (AC-56).

**Recommendation**: Either add a source row, "a declaration line in the `src/`-scoped diff | that type
or member", or delete the Context row and state the narrowing as a deliberate choice, with its reason,
under Technology Choices.

---

### 5. The measurement script's command-line grammar is not fixed, and the word-count call as sketched does not fit AC-93 (Score: 55)

0072 fixes the ledger's field names because "the script, the command file and the test script" depend
on them. It leaves the argument grammar open:

- "two optional test inputs … Either may be given without the other", with no rule for telling a
  release-notes path from a sha;
- "a word-count switch" with no name;
- the word-count mode's invocation given as "the same, with a word-count switch and the path of the
  file to count", which leaves open whether a target directory is passed too.

The command file and the test script both depend on this contract. AC-93 also requires that every
invocation in the command file "passes only a target directory". The word-count call passes a switch
and a file path, and the ADR does not reconcile the two.

**Evidence**:

- 0072:283-284: "two optional test inputs … Either may be given without the other (FR-21)."
- 0072:288-290: the mode table rows.
- 0072:345-347: the field names are "the contract between the script, the command file and the test
  script".
- requirements.md:2312-2314 (AC-93).

**Recommendation**: Add a small invocation-contract table to Key Components 1. For each mode give the
exact argument form, with named options, for example `--pinned {base} {head}`,
`--release-notes {path}` and `--word-count {file}`. State that AC-93 applies to the measuring
invocation, and that the word-count invocation passes only `--word-count` and the written path.

---

### 6. `## Inputs used` is described as "one row per input the read log records", which conflicts with the exclusions and leaves some marks undefined (Score: 50)

The read log records the ledger read, `specs/.current-spec`, any `PROMPT.md` read, and
`requirements.md` paragraphs or `tasks.md` chunks. 0072 says `## Inputs used` has one row per input the
read log records. It then says the ledger has no row, and that an untracked `PROMPT.md` "gets no row".
It is also silent on `.current-spec`, which FR-15 does not list.

Two further gaps:

- FR-15 defines `used (targeted extraction)` only for diagram sources, and 0072 does not say which mark
  a `requirements.md` read by paragraphs, or a chunked `tasks.md`, gets.
- A `PROMPT.md` read is not in the read table and is not priced in the calibration arithmetic, although
  every read is charged.

**Evidence**:

- 0072:519: "one row per input the read log records".
- 0072:524-528: no row for the ledger; `PROMPT.md` "is never cited and gets no row".
- 0072:433, 440: the ledger and `specs/.current-spec` are in the read table.
- requirements.md:750-752: `used (targeted extraction)` is "for a diagram source read by extraction".
- requirements.md:2027 (AC-27): `requirements.md` is marked `used`.

**Recommendation**: State the projection as FR-15's fixed row set plus one row per Explainer source.
List the reads that never produce a row: the ledger, `.current-spec` and an untracked `PROMPT*`. State
the mark for extracted `requirements.md` and chunked `tasks.md`. Add `PROMPT.md` to the read table, or
state that it is not read.

---

### 7. The implementation plan claims every behavioural step is test-first, but three steps have no feasible test, and the ledger cap has no error condition (Score: 50)

0072 says "Each behavioural step is test-first". Three steps do not fit that:

- **Step 8** tests "the atomic write" test-first. NFR-9 says atomicity is verified by inspection and
  not by the test script.
- **Step 7** tests unpinned ref fields, PR discovery and divergence test-first. NFR-9's fixtures
  deliberately null every ref field on fixture runs, so asserted values do not depend on local refs,
  and the ADR names no fixture or fake `gh` that could exercise these fields.
- **Step 10**, the command file, has no test-script row except the FR-13 check.

The 65,536-byte cap also has no stated behaviour when the serialised object exceeds it (fail with a
tooling-fault status, or write anyway). `declarations` grows with the number of declared ids, so the
cap is not unreachable.

**Evidence**:

- 0072:638-639: "Each behavioural step is test-first".
- 0072:668-671: steps 7 and 8.
- requirements.md:1494-1499: atomicity is verified by inspection (AC-83), not by the script.
- requirements.md:867-870: fixture runs null the base ref so values do not depend on local refs.
- 0072:388-389: "far below the 65,536-byte cap", with no over-cap rule.

**Recommendation**: Mark step 8's atomicity part as inspection-verified per AC-83, and keep the cap and
residue rows. Name how step 7 is tested (a scratch repository, a stand-in `gh` on `PATH` as AC-84 does,
or manual verification) or say it is not test-first. State the over-cap behaviour, for example "exit 1,
no ledger", and add a row for it.

---

### 8. The diagram caps are counted by the model, against the seam's own rationale (Score: 40)

0077 has the model check "at most 40 lines" and "no line exceeds 100 characters" over its own output
before the `Write`. 0072's motivating table shows the model miscounting (16 vs 8). The caps are not
ledger values, so the unifying sentence is not breached. Still, the design relies on the model counting
accurately at the point it most distrusts it, and names no backstop. The word-count mode runs after the
write and could report cap breaches, but it is not used for that.

**Evidence**: 0077:330-339; 0072:125-134.

**Recommendation**: Either state the caps as a model-checked target, as a Negative consequence, or
extend the word-count mode to report the fenced-block count and line and column overruns in its record
(it reads the file already).

---

### 9. 0073's lines table conflates "stated level" and "maximum", and leaves forced rows' measured values undefined (Score: 40)

"the factor or factors whose level equals the stated maximum" mixes two defined terms. On a raise, no
factor equals the stated level. The Synthesiser must still "name at least the factor or factors that
set the level", and FR-12's wording assumes a factor set it.

Separately, "Each row states its measured value" is unresolved for forced rows. The ADR does not say
what F5's value cell says when `requirements.md` is missing, or what F1's says under FR-16 row 12.

**Evidence**: 0073:288, 0073:291, 0073:239-241, 0073:260-264.

**Recommendation**: Say "equals the maximum". Say what the Synthesiser names on a raise. Give the value
cell text for each forced row.

---

### 10. Key Components structure is uneven across the set (Score: 35)

In 0072, unnumbered `####` sub-topics sit between the numbered components at the same level: "The write
is atomic" after "2.", then "Two reads are obligations", "The full diff is never read" and "The read
log" after "3.". Cross-references such as "Key Components 3 states it" (0072:45) therefore point at a
heading that does not structurally contain the read log.

0073 and 0077 have no "Where each … is touched" table, although both change `show-me.md` and the test
script.

0077 Implementation Approach step 1 labels adding instructions to the command file "Structural". This
is the same labelling issue pass 2 raised against 0078.

**Evidence**: 0072 headings at lines 260-555; 0073:192-193 and 0077:225-226 give prose instead of a
table; 0077:382.

**Recommendation**: Nest the sub-topics as `#####` under their numbered component. Add short touch
tables to 0073 and 0077. Relabel 0077 step 1 "Behavioural".

---

### 11. The Decision bold sentences run 45-70 words (Score: 35)

**Evidence**: 0073:118-121 (about 58 words, five clauses); 0077:118-121 (about 70 words); 0078:112-115
(about 45 words). documentation.md:172: "no more than about 25 words".

**Recommendation**: Cut each to the rule it turns on, and move the clauses into the shape paragraph.

---

### 12. The generation date and FR-19's created-or-replaced have no named source (Score: 30)

FR-5 needs an ISO-8601 generation date and FR-19 needs "created or replaced". Neither is in the ledger.
`allowed-tools` has no `date`, and no step probes `show-me.md` before the `Write`.

**Evidence**: requirements.md:309-312, 1193-1195; 0072:611-613.

**Recommendation**: Name the source of each. For example, the script records `generated_at` in the
ledger, and Step 6 runs `test -f` before the `Write`.

---

### 13. Two ledger conventions are unassigned (Score: 30)

The null reason `not present` belongs to no field. The shape of `release_notes` when
`release_notes.md` is absent is not stated (a zero `count`, or null with a reason), and FR-7 says the
file is "never required to exist".

**Evidence**: 0072:376-377, 0072:372.

**Recommendation**: Say which field carries `not present`, and state `release_notes` for an absent file.

---

### 14. The "Nothing is written above the `exit 0` branch" invariant gives an exception that is not above that branch (Score: 25)

FR-21's fifth failure state happens inside the `exit 0` branch. The invariant is true as stated, and
the "one exception" is attached to the wrong claim.

**Evidence**: 0072:209-213.

**Recommendation**: Attach the exception to "A stopped run therefore leaves the repository unchanged",
or state the invariant as "a stop leaves the repository unchanged, except …".

---

## Status of pass-2 findings

1. Risk step checks a non-existent section — **closed**. 0073:283-303: the risk step writes its own
   lines and checks them inside the markers.
2. 0073 lacks Implementation Approach — **closed**. 0073:363-384; 0072:674-677 points to it.
3. Extraction without re-writing a pattern — **closed**. 0072:325-333 declaration ranges;
   0072:426-429 tasks chunking; asserted at 0072:647-648.
4. Ledger schema unspecified — **closed**. 0072:349-386. The D3 count gap is new finding 3.
5. Unifying sentence — **closed**. It was rewritten, and it is byte-identical in all four ADRs (md5
   checked).
6. absent/unreadable indistinguishable — **closed**. 0072:477-485.
7. New fields untested — **closed**. 0072:649-667, with boundary rows.
8. Review history and drafts — **closed**. A grep for history and conversation phrases finds no hits.
9. FR-7 `wc -c` probe — **closed**. 0072:414-416.
10. Per-line FR-13 check — **closed**. 0073:321-326 uses paragraphs.
11. Row-5 tree maximum — **closed**. 0077:313-316.
12. 0073 "seven of eight" — **closed**. 0073:24-25.
13. Diagram missing stops — **closed**. 0072:180-203.
14. Part 3 owner — **closed**. 0073:207; 0072:514.
15. Bold leads to `####` — **closed**. Nesting is now uneven (finding 10).
16. 0078 "Structural" — **closed**. 0078:381-393. The same issue recurs in 0077 (finding 10).
17. Temp file name and concurrency — **closed, with the risk accepted**. The fixed name is kept,
    concurrency is stated unsupported, and a leaked temp file is a named risk (0072:398-400, 729-734).
18. 0077 handoff two ways — **closed**. 0077:183-184, 216.
19. 0072 title — **closed**. Retitled "The Measurement Seam and Output of /spec:show-me".
20. Summaries too long — **closed**. Two sentences each; `docs/adr/index.md` rows 113-120 match.
21. "exactly one producer" — **closed**. 0078:188.
22. Defect table FR-13 row — **closed**. 0072:129.
23. Level's two arrows — **closed**. 0073:149-150.
24. `/spec:gear` read-and-report — **closed**. 0072:626.

## Verification log

- **Mermaid:** all 7 blocks (0072 ×2, 0073 ×2, 0077 ×2, 0078 ×1) extracted and rendered with
  `npx -y -p @mermaid-js/mermaid-cli@11 mmdc`. All 7 exited 0 and produced an SVG. 0072's sequence
  diagram and 0073's decision flowchart were rendered to PNG at `-w 1600 -b white` and inspected. Both
  are readable, and 0073 has one decision node, so no ladder table is needed.
- **Escaped entities:** `grep -c '&lt;\|&gt;\|&amp;'` = 0 for all four ADRs.
- **Headings:** the H2/H3 skeleton is identical across all four and in canonical order. The stage table
  replaces the roles table (settled). `### Where this ADR sits` is present in all four, lists all four,
  and bolds the ADR's own row with *(this one)*. The unifying sentence is identical (md5).
- **dotnet run (finding 1):** tested with SDK 10.0.401 from the repository root and from the
  scratchpad, using two throwaway `.cs` files in the scratchpad. `--file` redirects to the other
  program; `--project` tries to load the other file as a project; `--` makes both inert.
- **Tool limits (finding 2):** `Read` on `specs/0036-scoped-lifetime-per-pipeline/tasks.md` returned 369
  of 1,628 lines ("93531 tokens, cap 25000"). The Bash 30,000-character truncation is Claude Code's
  documented default and was not exercised. Sizes measured: `tasks.md` 229,159 B, 1,627 lines; 0036
  `requirements.md` 273,674 B, 10 lines over 2,000 characters; `src/` diff over `6145913a0..91d549be6`
  303,715 B, 5,440 lines, which matches the requirements' figure; `release_notes.md` 118,145 B, 28 `##`
  headings, first `## Master`.
- **Codebase claims confirmed:** `settings.json` has `Bash(wc:*)`, `Bash(git diff:*)` and
  `Bash(git ls-files:*)`, and no interpreter, `test` or `git merge-base` entry. The `deny` list holds
  curl, wget and ssh. `test` is supplied through `allowed-tools`, as `status.md` and `switch.md` do.
  `generate_adr_index.awk` is mode 100644. `switch.md` pre-executes `ls -d specs/*/`. CI installs 9.0.x
  and 10.0.x, and there is no `global.json`. There are 0 tracked `.py` files. The root
  `Directory.Build.props` exists. `0062-pg-advisory-lock-sha256.md` (the fixture's `.adr-list` entry)
  exists. In `.claude/commands/spec/tasks.md`, the only non-conforming checkbox lines are inside *DO
  NOT Format Tasks Like This* (lines 137 and 141), so AC-87 is achievable additively. `6145913a0` is an
  ancestor of `91d549be6`.
- **Expected-absent paths:** `show_me_facts.cs`, `show_me_facts_tests.cs`, `write_release_notes.md` and
  `.claude/test-fixtures/` are absent, as expected.
- **Tone:** grepped for history, conversation-participant and review phrases. No hits. "the user" in
  0078 refers to the command's runtime user, which is legitimate.
- **Not checked:**
  - whether Claude Code's `:*` prefix matching would also admit `show_me_facts.cs.other.cs`;
  - MSBuild `-p:` injection through `dotnet run`;
  - the `Directory.Build.props` non-interference claim for a file-based app placed under `.claude/`;
  - the `dotnet run` timing figures.

### Main-agent re-verification of finding 1 (added when recording this review)

Finding 1 contradicted the measurement 0072 cites, so it was re-run before this file was written.

- **Why 0072's measurement was wrong:** it came from a probe run in zsh with the extra arguments held in
  an unquoted variable. zsh does not word-split such a variable, so the script received
  `--file {path}` as one argument, and `dotnet run` never saw a `--file` option.
- **Run with the arguments split** (SDK 10.0.401, from the repository root):
  - `dotnet run a.cs --file b.cs` runs **b.cs**, exit `0`.
  - `dotnet run --file b.cs` runs b.cs.
  - `dotnet run a.cs --file=b.cs` runs b.cs.
  - The finding stands at 90.
- **With `--` after the script path:** `dotnet run a.cs -- …` passes every later token to a.cs as an
  argument, and consumes the `--` itself. This was checked for `--file b.cs`, `--project b.cs`,
  `-p:Foo=1`, `--launch-profile x` and `--no-build`. a.cs ran in every case, and its exit status `2`
  came through.
- **Prefix matching is not verified.** Whether Claude Code's `:*` matching requires a word boundary is
  still unknown. An entry ending `… show_me_facts.cs --` may therefore also match
  `… show_me_facts.cs --file …`. The fix should not rely on a word boundary.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 1 |
| 70-89 (High) | 1 |
| 50-69 (Medium) | 5 |
| 0-49 (Low) | 7 |

**Total findings**: 14
**Findings at or above threshold (60)**: 3
