# Review: requirements — 0037-show-me (pass 10)

**Date**: 2026-09-23
**Threshold**: 60
**Verdict**: NEEDS WORK

3 findings at or above threshold 60. Address these before approving.

## Pass-9 resolution check

| Pass-9 # | Score | Resolved? | Note |
|---|---|---|---|
| 1 (anchored pattern drops heading/list forms) | 85 | Yes (owner scope) | Bold-lead-in only; Out of Scope (lines 189–196), FR-16 row 9, zero-id fixture all consistent. Verified: across 0030–0037 zero heading/numbered-list declarations; anchored counts 8–37, 0036 = 37, 0037 = 31 |
| 2 (merged PR → empty diff) | 80 | Yes | `--state open` (FR-20), FR-10 already-merged rule, AC-85, C-8 lapse note. Residue: delivered test script's behaviour when #4282 moves/merges undefined (finding 3) |
| 3 ("verbatim" vs Python/.NET) | 70 | Yes | Lines 127–134 now "POSIX ERE meaning; fixture-proven translation"; FR-21 language list consistent |
| 4 (who runs `gh pr list`) | 68 | Yes | Script issues it (FR-20, FR-18, C-10); ledger records every gh command line; AC-30/AC-73 assert from ledger |
| 5 (stderr "carries no diagnostics") | 66 | Yes | Universal claim deleted; `show-me-gate:`/`show-me-wordcount:` prefixed lines; command reads last prefixed line; script captures children's stderr |
| 6 (NFR-8 vs state 5) | 62 | Yes | NFR-8 state-5 exception; message → "No show-me.md was written." |
| 7 ("met by construction"; AC-33) | 62 | Partly | NFR-2 now a target and AC-33's clause calibration-only — but AC-59 still asserts the 400–2,000 range for an arbitrary run (finding 5), and the ceiling paragraph still says "provably reachable" (finding 10) |
| 8 (FR-15 "read in full") | 55 | Yes | Lines 723–726 |
| 9 ("no third stop") | 55 | Yes | Lines 251–257 |
| 10 (no exit-2 fixture) | 55 | Yes | No-tasks and unfinished fixtures added (zero-checkbox gate case still fixture-less; not filed) |
| 11 (AC-84 no setup) | 55 | Yes | Stand-in `gh` first on `PATH` |
| 12 (C-8 fixtures) | 50 | Dissolved | C-8 scoped to branch/PR/diff-derived criteria |
| 13 (open design question stale) | 50 | Yes | Lines 2136–2140 |
| 14 (provenance line count) | 45 | Yes | Five lines ≤ 110; sum re-derived 1,996 (checked) |
| 15 (word-count exit semantics) | 45 | Yes | FR-21 *Modes* has its own two-way rule including "not parseable" |

## Findings

### 1. "Whether a `release_notes.md` section for the spec exists" is a script-owned mechanical fact with no identification rule (Score: 68)

FR-21 (line 811) makes the script determine "whether a `release_notes.md` section for the spec exists". That boolean decides whether the read is mandatory (FR-7, lines 464–467), whether FR-16 row 5 / row 5a / neither applies, which of AC-14/AC-68/AC-69 holds, and which F2 tie-break rule governs grouping. Nowhere does the document say how a section is matched to a spec — heading text? spec id? directory slug? `.issue-number`? PR number? which heading level? what if more than one matches? The real file makes this live: sections self-identify inconsistently (`### Scoped lifetime per pipeline (spec 0036, #4256)` vs `### Kafka: … (#4264)` with no spec id), and spec ids are not unique (C-1) — `release_notes.md` has two different `(spec 0027)` sections (`Replay Outbox Messages on Inbox Duplicate`, `Box Schema Versioning and Migrations`) under two release headings. Two developers would write different matchers and disagree on row 5 vs corroboration for the same tree — breaking FR-21/NFR-1's "the script measures, deterministically" premise.

**Evidence**: FR-21 line 811; FR-7 lines 464–483; FR-16 rows 5/5a (lines 945–946). `git show origin/spec/scoped-lifetime-per-pipeline:release_notes.md | grep -n '^#'` → line 5 `### Scoped lifetime per pipeline (spec 0036, #4256)`, line 34 `### Replay Outbox Messages on Inbox Duplicate (spec 0027)`, line 73 `### Kafka: … (#4264)`, line 234 `### Box Schema Versioning and Migrations (spec 0027)`.

**Recommendation**: State the identification rule (e.g. "a `###` heading under any `##` release heading whose text contains `(spec NNNN` for the target's four-digit id, or the spec's `.issue-number` as `#n`"), state the multi-match outcome (all read, or ambiguous → row 5 with stated reason), and add an NFR-9 assertion (0036 → exists; 9999 → absent).

---

### 2. The synthetic fixtures live outside `specs/`, but the script's argument, the ledger path and FR-10 are defined only for spec directories under `specs/`; AC-43 cites a fixture the command cannot target (Score: 64)

The owner's decision places three synthetic fixture directories beside the test script in `.claude/commands/spec/`. The text has not been reconciled with that:
- Definitions: a *spec directory* is "a directory directly under `specs/`" (line 97); the *fact ledger* is `specs/{target spec directory}/.show-me-ledger.json` (line 114); FR-10 rule (3) tests commits touching `specs/{spec directory}/` (line 603).
- FR-21 says the script takes "the target spec directory as its argument". Nothing says the argument may be an arbitrary path, or where the ledger goes when it is (beside the fixture? a new `specs/{basename}/`?).
- NFR-9/AC-79 assert "no ledger created" for the two exit-`2` fixtures — the location to check is undefined.
- AC-43 offers "NFR-9's zero-id fixture" as its example Given, and AC-43 runs the *command*; FR-1 considers "only directory entries directly under `specs/`" (line 207), so `/spec:show-me` can never target that fixture — it prints "No spec matches". The example is unconstructible as cited.

**Evidence**: Definitions lines 97, 114; FR-1 line 207; FR-10 line 603; FR-21 line 790; NFR-9 table rows 3–5 (lines 1291–1293); AC-43 line 1719; AC-79 line 1985.

**Recommendation**: State that the script accepts any directory path, writes the ledger at `{that path}/.show-me-ledger.json`, and that FR-10/FR-20 evaluate to not-determinable / no PR for a directory outside `specs/` (assert it in NFR-9's rows). Reword AC-43 so its Given is a spec directory under `specs/`, or note the zero-id fixture exercises AC-43's counting only through the script.

---

### 3. NFR-9's calibration row pins figures that change whenever PR #4282 moves, and turns the delivered test script permanently red when it merges — the test's behaviour in either case is undefined (Score: 60)

Not disputing the owner's C-8 lapse decision — the problem is a delivered artefact. NFR-9's 0036 row asserts **131** public-API lines and **76** `src/` files "over merge base `6145913a0`". But the script takes only a spec directory and measures to PR #4282's live `headRefOid` (FR-20); the test cannot pin that merge base or head. Any review-response push touching `src/` to the still-open PR, or a merge of `master` into it (moving the merge base), changes 131/76 and fails AC-79. Once #4282 merges, FR-10 no longer resolves the branch → the public-API/`src/` fields are null → AC-79's test script exits non-zero forever. Nothing tells the test script to skip diff-derived assertions when the calibration branch is absent/merged, so the C-8 lapse leaves a permanently red regression net rather than a quiet lapse.

**Evidence**: NFR-9 row 2 (line 1290); FR-20 lines 761–766; C-8 lines 1365–1367 ("the *(C-8)* criteria lapse — by design"); AC-79 ("exits `0` when every assertion holds"). Verified today: PR #4282 OPEN at `91d549be6`, merge base `6145913a0`, 131/76 match.

**Recommendation**: Define the test script's behaviour: e.g. the 0036 diff-derived assertions run only while `origin/spec/scoped-lifetime-per-pipeline` resolves at the recorded head `91d549be6`; otherwise they are reported *skipped* with a reason and do not fail the run. The task/id (file-only) assertions always run.

---

### 4. Markdown fixtures placed beside the test script in `.claude/commands/spec/` will be registered as slash commands (Score: 58)

Claude Code registers every `.md` under `.claude/commands/` (including subdirectories) as a namespaced command — this session's own skill list shows `spec:README`, `adr:README`, `tdd:README`, proving plain README files are picked up. NFR-9's three synthetic directories must contain `requirements.md` / `tasks.md` (the script reads those names), and the word-count fixtures are markdown documents with an H1, fences and an `## Inputs used` tail. All would appear as invocable `/spec:…` commands whose "prompt" is fixture text. NFR-9 carefully prevents a fixture being "mistaken for a spec or for a generated deliverable" (lines 1301–1303), but not for a command.

**Evidence**: NFR-9 lines 1291–1295, 1301–1303; session skill listing includes `spec:README` (from `.claude/commands/spec/README.md`).

**Recommendation**: Acknowledge and pick a mitigation — e.g. word-count fixtures use a non-`.md` extension, or the fixture dirs sit beside the test script under a name/location Claude Code does not scan — and state it. If the owner prefers the current location, record that the registration is accepted.

---

### 5. AC-59 still asserts the 400–2,000 range as a guarantee for any two-diagram run (pass-9 #7 residue) (Score: 58)

NFR-2 is now explicitly "a target … not a guarantee", and AC-33's range clause was narrowed to the calibration case. AC-59 still reads "Given a successful run that produced two diagrams … then the NFR-2 word count … is still between 400 and 2,000". A conforming run with many breaking changes (which NFR-2 now says can exceed 2,000) fails that AC while meeting every requirement. AC-59's real intent is fenced-line exclusion.

**Evidence**: NFR-2 lines 1087–1091; AC-59 lines 1835–1839.

**Recommendation**: Replace the range clause with "the NFR-2 count equals the count with the 70 fenced lines excluded" (or "does not increase by any token inside a fence"); keep the size and count caps.

---

### 6. Path-form `.adr-list` entries are not covered by the resolution rule — and a fixture uses one (Score: 55)

FR-16 row 7 defines resolution for a full filename (ordinary) and a bare number, plus "otherwise doesn't match any file". `specs/9999-show-me-fixture/.adr-list` contains `docs/adr/0072-show-me-command-resolution-and-output.md` (a repo-relative path), as does `specs/0023-asyncapi-document-generation/.adr-list`. One implementer strips the directory and resolves; another reports "ADR file not found" — changing D3, a mechanical NFR-1 field owned by the script. NFR-9 asserts nothing about 9999's ADR resolution, so the fixture does not settle it.

**Evidence**: FR-16 row 7 (line 948); `cat specs/9999-show-me-fixture/.adr-list` → `0062-pg-advisory-lock-sha256.md`, `docs/adr/0072-show-me-command-resolution-and-output.md`.

**Recommendation**: State whether a `docs/adr/`-prefixed entry resolves; add the 9999 resolved-ADR count (and D3 outcome) to NFR-9's row.

---

### 7. AC-86's "every example declaration line matches the declared-id pattern" conflicts with the placeholder form FR-22 prescribes (Score: 50)

FR-22 has `/spec:requirements` require `**FR-{n} — {title}.**` / `**FR-{n}.{m} — …**`. A template line in that form starts `**FR-{` and never matches `(FR|NFR)-[0-9]+`. AC-86 requires "every example declaration line it presents" to match when the pattern is run over the file. Whether a placeholder template line counts as an "example declaration line" is ambiguous — one implementer writes `{n}` templates and fails AC-86; another writes only concrete examples.

**Evidence**: FR-22 lines 913–917; AC-86 lines 2040–2045.

**Recommendation**: Either state the amended file presents the form only through concrete-number examples, or scope AC-86 to lines whose id is numeric and exempt placeholder templates explicitly.

---

### 8. FR-10 rule (1) does not say whether a merged remote-tracking branch falls back to an unmerged local branch of the same name (Score: 45)

Rule (1) says the remote-tracking branch "wins" when both exist; the new containment clause says "a candidate … whose tip is already contained in the base ref does not resolve". If `origin/spec/x` is contained but local `spec/x` carries new commits, is the local ref then tried before rule (2), or does rule (1) fail outright? Which ref is named in row 12's "`{ref}` is already merged"?

**Evidence**: FR-10 lines 598–606.

**Recommendation**: One sentence fixing the order (e.g. "each rule-(1) candidate is tried remote-first; a contained candidate is skipped and the next is tried; every skipped candidate is named").

---

### 9. The "540 occurrences" figure for the unanchored pattern is wrong (Score: 40)

Line 192 says the unanchored `(FR|NFR)-[0-9]+` "finds **540** occurrences in this document". At HEAD, `grep -oE` gives **667** occurrences (620 at `5e0737fe5`); `grep -cE` gives 476 lines. Neither is 540. The other figures in the same sentence (8–37, 3, 37, 31) verify.

**Evidence**: `/usr/bin/grep -oE '(FR|NFR)-[0-9]+' specs/0037-show-me/requirements.md | wc -l` → 667.

**Recommendation**: Restate the figure or drop the exact number ("hundreds").

---

### 10. The ceiling paragraph still argues provability, with 4 words of headroom and uncounted lines (Score: 35)

Lines 1109–1126 still say the caps exist "so this arithmetic holds" and "without them the ceiling was not provably reachable" — guarantee language contradicting the target framing three paragraphs earlier. The sum is 1,996 (4 words headroom) and omits lines that do count: FR-7's release-notes disagreement line, the FR-12 raise reason, FR-16 row 5/5a/7 lines, and FR-14 bullet path tokens.

**Evidence**: NFR-2 lines 1109–1126.

**Recommendation**: Present the sum as an illustrative calibration estimate; drop "provably".

---

### 11. FR-22's premise misstates `/spec:requirements` (Score: 35)

FR-22 says "`/spec:requirements` says only that FRs *may* be numbered". The file does say "may number" (line 142), but it also lists "numbered functional requirements" under Completeness (line 132) and validates "FRs numbered" (line 148). What it lacks is a prescribed declaration *form*, not a numbering requirement.

**Evidence**: `.claude/commands/spec/requirements.md` lines 132, 142, 148.

**Recommendation**: Reword to "prescribes no declaration form".

---

### 12. `specs/9999-show-me-fixture/` is called "NFR-9's synthetic fixture", but NFR-9 says no synthetic fixture lives under `specs/` (Score: 30)

Line 191 calls 9999 "NFR-9's synthetic fixture"; NFR-9 (lines 1300–1302) reserves "synthetic" for the directories beside the test script and states "No synthetic fixture lives under `specs/`".

**Evidence**: line 191 vs lines 1300–1302.

**Recommendation**: "3 for `specs/9999-show-me-fixture/`".

---

### 13. FR-20 says NFR-3 "permits" a `gh pr diff --name-only` form, but NFR-3 and FR-18 say it is never issued (Score: 30)

Lines 772–774: "`gh pr diff` is not used. It accepts no pathspec, so its only form NFR-3 permits is `--name-only`". NFR-3 (lines 1184–1185) says `gh pr diff` "is not issued at all", and FR-18/AC-30 enforce "never". The parenthetical implies a permitted form exists.

**Evidence**: FR-20 lines 772–774; NFR-3 lines 1182–1185.

**Recommendation**: Reword as rationale: "even its `--name-only` form would carry no `+`/`-` lines, so it is not used at all."

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 7 |
| 0-49 (Low) | 6 |

**Total findings**: 13
**Findings at or above threshold (60)**: 3
