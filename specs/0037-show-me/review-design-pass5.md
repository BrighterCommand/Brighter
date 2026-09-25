# Review: design — 0037-show-me (pass 5)

**Date**: 2026-09-25
**Threshold**: 60
**Verdict**: PASS

**Note**: This spec carries a stale `.design-approved` marker from a pre-rescope approval; the ADRs were
rewritten afterwards and last revised in `2efbe3018`. This review is a genuine gate on the current
design.

No findings at or above threshold 60. Consider addressing lower-scored items.

## Findings

### 1. The JSON rationale for C# leaves out how System.Text.Json behaves by default in a .NET 10 file-based app (Score: 55)

The ADR picks C# largely because "it has a real JSON serialiser". It says the ledger is "serialised
with `System.Text.Json`", and that the test script parses it "with the same serialiser". A probe with
SDK 10.0.401 (the version the ADR measured) found two default behaviours the ADR does not mention:

- **Reflection-based serialisation is disabled.** A file-based app defaults to AOT-compatible
  settings. `JsonSerializer.Serialize(new L())` on a plain class compiled with warnings IL2026 and
  IL3050, then crashed with `InvalidOperationException: Reflection-based serialization has been
  disabled for this application` (exit 134). This happened both in a bare directory and under copies
  of the repo's `Directory.Build.props` and `Directory.Packages.props`. The obvious way to write the
  ledger, or to deserialise it in the test script, therefore fails on the first test-first step. Each
  of these works:
  - `#:property PublishAot=false`;
  - `JsonObject`/`Utf8JsonWriter` for writing and `JsonDocument` for reading;
  - a source-generated context.
- **The default encoder escapes more than the ADR implies.**
  `JsonObject{["TEST + IMPLEMENT"]=62, ["t"]="T1.5 — x <y>"}` serialised as
  `{"TEST + IMPLEMENT":62,"t":"T1.5 — x <y>"}`.
  - The `by_tag` key for `TEST + IMPLEMENT` and every em dash in a gate-record title (0036's titles
    all contain `—`) reach the model as `\u` escapes.
  - The model must decode them before it copies the values into FR-9's tag line and FR-3's stop
    message.
  - A design whose premise is that escaping layers corrupt copied values should state this.

**Evidence**:

- 0072:354-355: "serialised with `System.Text.Json` with indentation on".
- 0072:671-672: "It has a real JSON serialiser. … `System.Text.Json` escapes quotes, backslashes and
  non-ASCII text without being asked."
- 0072:740-742: "it parses the ledger as JSON and asserts on fields, which C# does with the same
  serialiser the script writes with".
- 0072:378: `by_tag` holding the four tags.
- 0072:585: "From the ledger (copied, never recomputed)".
- Probe output, scratchpad `review5/probe/` and `review5/cpm/`.

**Recommendation**: Add a bullet under *Why the script is a C# file-based app* stating three things:

- The serialisation route: `JsonNode`/`Utf8JsonWriter`, or `#:property PublishAot=false`, and
  `JsonDocument` in the test script.
- The encoder: `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`, so `+`, `—` and `<` are written
  literally, or else a statement that the command decodes escapes before copying.
- That the IL2026/IL3050 compile warnings are among the standard-output noise *Why the payload travels
  in a file* already handles.

---

### 2. Ref resolution has no automated test, and the gap is not a stated consequence (Score: 50)

FR-10's three rules, the merged-candidate skip, the remote-versus-local divergence, FR-20's
highest-number PR choice and the head-present fallback are the most branch-heavy logic in the script.
Every diff field depends on the merge base and measured head they produce. Step 7 notes that fixture
runs null the ref fields, so the test script cannot assert them. Verification is left to
command-level criteria, several of which lapse when #4282 merges (AC-18, AC-47 are *(C-8)*).

NFR-9 exists because "an undetected defect in it silently falsifies `show-me.md`". Yet the Negative
list does not record that this part of the script has no regression net.

No alternative is considered either. For example, the test script could build a throwaway git
repository with `spec/*` branches and remote-tracking refs, put a stand-in `gh` first on `PATH` (the
device AC-84 already uses), and run the script from that repository's root. That would select inputs
without adding a fault hook.

**Evidence**:

- 0072:778-782: "Fixture runs null every ref field by design (FR-21), so the test script cannot assert
  these values; the step is verified against AC-18, AC-46, AC-47, AC-84 and AC-85".
- 0072:820-846: the Negative list has no entry for this.
- requirements.md:1431-1432 (NFR-9 rationale).
- requirements.md:1553-1556 (the C-8 criteria lapse once #4282 merges).

**Recommendation**: Add a Negative saying ref resolution and PR selection are verified only by command
runs, several of which lapse after merge. Either add an Alternative that records why a constructed
temporary repository was rejected, or adopt one as further test-script rows.

---

### 3. 0078 does not say how `/spec:write_release_notes` reads its item sources, or what tools it declares (Score: 45)

0078 treats `release_notes.md`'s size carefully: 118,145 B, read by `Grep` line numbers and then
`Read` with an offset. It says nothing comparable about the inputs the items are judged from:

- how `.adr-list` entries resolve (full filename, the `docs/adr/` path form, an ambiguous bare number
  under C-9). This command does not run the script, so 0072's resolution is not available to it;
- how ADR *Consequences* sections (0036's ADRs average about 100 KB) and `requirements.md` are read.
  0036's `requirements.md` is 273,674 B, which is over the 256 KB limit 0072 records for `Read`
  without an offset;
- which tools the new command's `allowed-tools` lists. 0072 enumerates its own command's list; 0078
  says only "the family's front matter: `allowed-tools`, …".

**Evidence**:

- 0078:286-289.
- 0078:297 (step 5: "Judge the breaking-change items and write the section").
- 0078:274-275: "The items come from the ADRs' *Consequences* sections and `requirements.md`".
- 0078:300-304: `release_notes.md`'s read mechanism.
- 0072:441: the `Read` limits.
- 0072:715-717: 0072's enumerated `allowed-tools`.

**Recommendation**: Add the following to Key Components 5:

- the `.adr-list` resolution rule, pointing to FR-16 row 7;
- the extract-by-heading read for *Consequences*, with `Grep` for the heading line and then `Read`
  with an offset;
- `requirements.md` read by offset;
- the `allowed-tools` list.

---

### 4. No test row covers "a file directly under `src/` contributes no subdirectory" (Score: 40)

The Definitions call this "a real case and not a hypothetical", and AC-67 asserts it with
`src/Directory.Build.props`. The pinned D1 rows test "5 or more files under `src/`, in 1
subdirectory", but none requires a file directly under `src/`. The calibration diff contains no such
file: `git diff --name-only 6145913a0..91d549be6 -- src/ | awk -F/ 'NF==2'` is empty. AC-67 can then
be met only by a command run over a spec whose diff has that exact shape.

**Evidence**:

- requirements.md:114 (*Immediate subdirectory of `src/`*).
- requirements.md:2094-2098 (AC-67).
- 0072:772-774 (the D1 rows).

**Recommendation**: Make the "D1, too few subdirectories" row a pinned pair from `master` that changes
`src/Directory.Build.props` plus four files in one project. Assert 5 files, `src_subdirectory_count`
1 and `triggers.d1` `false`.

---

### 5. The existing `show-me.md` read cannot be priced before it is issued (Score: 25)

The window rule says every read is charged "the size it was priced at before it was issued". The
existing `show-me.md` is charged "its `wc -c` plus Read's line-number prefix", and the prefix is not
known until the call returns.

**Evidence**:

- 0072:463-464.
- 0072:499.

**Recommendation**: Price the prefix from `wc -l`, for example a fixed number of bytes per line. Or read
the existing file with a Bash window and state that `Write`'s read requirement is met by one `Read`
of line 1.

---

### 6. Small tone and readability items (Score: 20)

- 0078:83: the column header "Written today by" dates the table. This is the class pass 4 flagged in
  0072.
- 0072:411: "which the first run will measure" is future working state.
- 0077:338: "Row 4 has run out of budget" is not true of an abandoned raise, which row 4 also covers
  ("reading shows no relationship after all", 0077:139).
- 0077:229: "The one front-matter entry it relies on, `grep`". The Explainer also relies on `wc`,
  `tail`, `head` and `git ls-files`.
- 0078:115-120: the Decision paragraph introduces `/spec:write_release_notes` twice.
- About 25 prose lines exceed 104 characters. Examples: 0072:485 (160), 0072:580 (170), 0072:734
  (165), 0073:298 (153), 0077:352 (145), 0078:155 (139).

**Recommendation**: Fix each as listed. Re-wrap the long lines.

---

## Status of pass-4 findings

1. Commits as an input — **closed**. Commit-subject row at 0072:506; `git log` in `allowed-tools` at
   0072:715-717; git history row rule at 0072:606-608.
2. Budget running total is model-kept — **closed**. 0072:463-466 reworded; Positive at 0072:812-814
   qualified; Negative at 0072:826-829.
3. Read order — **closed**. 0072:484-487: the table is in read order, with release notes before
   `requirements.md`.
4. Missing or hung `gh` — **closed**. 0072:319-321: cannot start, non-zero exit or over 30 s is row 2.
5. Ledger parse judged by the model — **closed**, stated as a model-checked target at 0072:826-829.
6. 0078 plan looseness — **closed**. Second `Grep` for the literal marker at 0078:296; step 6 labelled
   Structural; re-tagging moved to 0078:403-404.
7. Synthesiser *Terms* entry — **closed** (0072:34-36).
8. Small items — **closed** for 0072: dated table ("measured 2026-09-25"), Decision sentence (21
   words), stray blank line, 0078 heading, and the Out-of-scope link. A similar "today" remains in
   0078:83 (finding 6).

## Verification log

- **Mermaid**:
  - All 7 blocks (0072 ×2, 0073 ×2, 0077 ×2, 0078 ×1) extracted to `scratchpad/review5/` and rendered
    with `npx -y -p @mermaid-js/mermaid-cli@11 mmdc`. All exited 0.
  - 0072's sequence diagram and 0073's decision flowchart rendered to PNG at 1600 px and inspected:
    readable, no clipped labels.
  - No `;` in either `sequenceDiagram`, and no `<`/`>` in any label.
- **Entities**: `grep -c '&lt;\|&gt;\|&amp;'` returns 0 for all four ADRs.
- **Structure**:
  - H2/H3 skeleton identical and in canonical order in all four ADRs.
  - `### Where this ADR sits` lists all four ADRs and bolds its own row.
  - The unifying-sentence paragraph has the same md5 (`27abd8a0…`) in all four.
  - The index lists all four with front matter matching.
- **Language probe** (fences stripped, units split at `.:;`, `|` and line breaks): units of 40 words or
  more appear only in the front-matter summaries (worst 55, 0073), plus one 41-word ledger-table cell
  in 0072.
- **Coverage**:
  - Every FR (1-23) and every NFR (1-9) is cited in at least one ADR.
  - C-4, C-5, C-6 and C-8 are uncited. That is acceptable: they are context for requirements the ADRs
    discharge.
  - 47 ACs are uncited. The design-relevant gaps are AC-66 (implicitly covered by the calibration
    figure of 131) and AC-67 (finding 4).
- **Probes**:
  - `dotnet run p.cs -- specs/x --file y.cs` (the argv was printed first) ran `p.cs`, not `y.cs`, and
    wrote nothing beside the file.
  - `System.Text.Json` reflection serialisation is disabled by default, and the default encoder
    escapes `+`, `—` and `<` (finding 1).
  - The repo's `Directory.Packages.props` enables central package management. Its
    `GlobalPackageReference`s are conditioned on net462/net472 or on `ContinuousIntegrationBuild`, so
    they do not attach to the script.
- **Codebase**:
  - `settings.json` has `Bash(git log:*)`, `head`, `tail`, `wc`, `grep`, `ls`, `cat`, `git diff` and
    `git ls-files`. It has no `date` or `test` entry; those are covered by the command's
    `allowed-tools`, as 0072 implies.
  - The `deny` list holds curl, wget and ssh.
  - `generate_adr_index.awk` is mode 100644.
  - `release_notes.md` is 118,145 B; its first `##` is `## Master` (unique, line 3); it has 115 heading
    or fence lines (under `Grep` limits) and 0 markers.
  - `tasks.md` has `##### DO NOT Format Tasks Like This` at line 133, and the only non-matching
    checkbox lines sit inside it.
  - `review.md` has all three criteria lists.
  - `switch.md` pre-executes `ls -d specs/*/`.
  - `0062-pg-advisory-lock-sha256.md` and `0071-tdd-review-gear.md` exist.
  - 0 tracked `.py` files; 30 CI jobs, all on `ubuntu-latest`.
- **Expected-absent paths**: `show_me_facts.cs`, `write_release_notes.md` and `.claude/test-fixtures/`
  are absent, as expected.
- **Tone grep**: no conversation-participant or revision-history phrases. "the user" appears only in
  0078 as the runtime user, which is legitimate. Flagged items are in finding 6.
- **Out of scope for this review, but noticed**: the git index has `specs/9999-show-me-fixture/`
  staged. A fake spec directory under `specs/` is what NFR-9 forbids. *(Main-agent note when
  recording: this is a known, deliberate staged-not-committed fixture from the pre-rescope
  implementation's task T2.1, torn down by T6.5. It is to be replaced by the
  `.claude/test-fixtures/show-me/` fixtures in the `tasks.md` revision.)*
- **Not checked**:
  - Claude Code's `:*` prefix matching (settled).
  - The compile-time figures.
  - Whether `Write` accepts a partial prior `Read`.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 2 |
| 0-49 (Low) | 4 |

**Total findings**: 6
**Findings at or above threshold (60)**: 0
