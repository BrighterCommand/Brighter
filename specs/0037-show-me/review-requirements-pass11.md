# Review: requirements — 0037-show-me (pass 11)

**Date**: 2026-09-25
**Threshold**: 60
**Verdict**: NEEDS WORK

4 findings at or above threshold 60. Address these before approving.

## Pass-10 resolution check

| Pass-10 # | Score | Resolved? | Note |
|---|---|---|---|
| 1 (release-notes section identification) | 68 | Yes | *Marked release-notes section* definition (line 120): literal marker, full directory name, trimmed. FR-21 (836–839) sums count and `{m}` over multiple matches; NFR-9 asserts 1 for declared fixture, 0 for 0036. Residue: the `#### Breaking changes` list's end is undefined (finding 4) |
| 2 (fixtures outside `specs/`) | 64 | Yes | New *Fixture directory* definition (119); ledger at `{target directory}` (121); FR-21 nulls fields with `not a spec directory` (810–816); AC-43 reworded |
| 3 (calibration row drifts/red after merge) | 60 | Yes, with residue | Pinned pair (841–851) and *Why the calibration row is pinned* (1398–1406). Residue: C-8's bold rule and AC-79's Given still tie the row to FR-10/branch presence (finding 3); the row's release-notes assertion is unpinned (finding 5) |
| 4 (`.md` fixtures registered as commands) | 58 | Yes | Moved to `.claude/test-fixtures/show-me/` (1372–1376); Claude Code scans `.claude/commands/`, not `test-fixtures/`; directory does not yet exist, no collision |
| 5 (AC-59 range clause) | 58 | Yes | AC-59 now asserts count equals count with the 70 fenced lines deleted (1948–1950) |
| 6 (path-form `.adr-list`) | 55 | Yes | FR-16 row 7 covers `docs/adr/{filename}`; declared fixture asserts 2 resolved; both ADR files exist |
| 7 (AC-86 placeholder) | 50 | Yes | Placeholder templates explicitly exempt (2158–2161) |
| 8 (FR-10 rule-1 fall-through) | 45 | Yes | Lines 621–624 |
| 9 (540 figure) | 40 | Yes | Now "hundreds" (199); verified |
| 10 (ceiling provability) | 35 | Yes | 1190–1209 now "illustrative estimate — not a proof". AC-33 still asserts the range for calibration (finding 7) |
| 11 (FR-22 premise) | 35 | Yes | Line 948 |
| 12 (9999 called synthetic) | 30 | Yes (text) | No 9999 reference remains; the staged `specs/9999-show-me-fixture/` files are still in the index (finding 12) |
| 13 (FR-20 `gh pr diff` wording) | 30 | Yes | Lines 791–793 |

## Findings

### 1. Out of Scope still says `/spec:review` gains "only FR-22's two format checks" — FR-23 adds a third (Score: 62)

Stale text the pass-10 edit missed. The Out of Scope bullet on the PR review workflow ends: "`/spec:review` is a different thing, and gains only FR-22's two format checks." FR-23 (1006–1008), NFR-6 (1329–1331) and AC-91 (2194–2200) all add a *Design (ADR) Review Criteria* check to the same file, `.claude/commands/spec/review.md`. Out of Scope is where scope is ruled out, so an implementer could read "only" as forbidding FR-23's review check. The two sections cannot both hold.

**Evidence**: lines 1578–1580 vs 1006–1008 and AC-91; `.claude/commands/spec/review.md` line 160 `#### Design (ADR) Review Criteria`.

**Recommendation**: Reword to "gains only FR-22's two format checks and FR-23's release-notes check".

---

### 2. FR-23 leaves undefined what happens when the target spec already has an unmarked, hand-written section — the command will produce a duplicate (Score: 60)

FR-23 replaces a *marked* section in place, and says "no existing section is edited, re-ordered, or given a marker" (997–1001). A spec heading to merge can already have hand-written notes: spec 0036's `### Scoped lifetime per pipeline (spec 0036, #4256)` sits under `## Master` (release_notes.md line 5), and PR #4282 is OPEN. For such a spec, `/spec:write_release_notes` must add a second section describing the same change under the same release heading, since it may not touch the existing one. FR-23's own `/spec:review` check will recommend exactly this run for any spec whose ADRs record a breaking change and which has no marked section. The owner's accepted miss is a `/spec:show-me` reading decision; it does not cover the writer emitting duplicate release notes for library users. An LLM-driven command given this text could silently add, warn-and-add, or stop — two implementations diverging on a non-obvious case, with no AC.

**Evidence**: FR-23 997–1002; Out of Scope 1550 ("No section already in release_notes.md is rewritten…"); release_notes.md lines 3–5; `gh pr view 4282` → OPEN.

**Recommendation**: State the behaviour — e.g. the command looks for an unmarked `###` heading naming `(spec {NNNN}` under the first `##` and, if found, stops and tells the user to delete it or add the marker by hand; or explicitly accept the duplicate. Add an AC clause.

---

### 3. C-8's bold rule and AC-79's Given contradict the pinned calibration row (Score: 62)

C-8 now says NFR-9's calibration row "is the exception: it is pinned … so it survives the merge" (1472–1473), but the next sentence — old text — still states as a rule: "**A fixture cited by any criterion that asserts a branch-, PR- or diff-derived value must resolve under FR-10 on the branch the criterion is run on**" (1473–1474). AC-79 asserts diff-derived values (131/76); after #4282 merges, 0036's branch tip is contained in the base ref and does not resolve under FR-10 (619–621), so the bold rule forbids the very criterion the exception keeps alive. AC-79 also still carries the *(C-8)* marker and its Given is "with the calibration branch present (C-8)" (2092), while C-8 says every *(C-8)* criterion lapses when #4282 merges. After the branch is deleted, AC-79's Given is unsatisfiable. As written, AC-79 either lapses (contradicting NFR-9 1400–1404 and AC-94's rationale) or survives (contradicting C-8); two testers would disagree on whether AC-79 still applies post-merge.

**Evidence**: C-8 1463–1476; AC-79 2091–2094; NFR-9 1398–1406.

**Recommendation**: Replace AC-79's Given with the pinned run's actual preconditions — `6145913a0` and `91d549be6` present locally, and `specs/0036-scoped-lifetime-per-pipeline/` in the working tree — and drop its *(C-8)* marker (or mark it as the named exception). Scope C-8's bold rule to unpinned criteria.

---

### 4. `{m}` counts bullets "in the `#### Breaking changes` list", but the list's end is undefined — and FR-23 itself permits bulleted `####` subsections after it (Score: 60)

The definition bounds the *section* (from `###` to the next `##`/`###`, line 120). FR-21 counts "top-level bullets (lines beginning `- `) in each one's `#### Breaking changes` list" (837–838) without saying where that list stops. FR-23 permits "further `####` subsections of free text (usage notes, per-interface migration detail)" after it (994–995), and existing notes of that kind are bulleted (release_notes.md lines 38, 62, 133). One implementer counts `- ` lines to the next heading of any level; another counts to the section's end (which is the only bound the document gives) and includes usage-note bullets. `{m}` is declared mechanical (NFR-1, 1138–1139) and feeds FR-7's disagreement line. NFR-9's release-notes fixture (1388) has no bulleted `####` subsection after the list, so the test doesn't pin the choice. Also unstated: a marked section with **no** `#### Breaking changes` heading (`{m}` = 0 or null? AC-92 fixes null only for zero sections), and column-0 `- ` lines inside a fenced migration example.

**Evidence**: line 120; FR-21 836–839; FR-23 991–995; NFR-9 1388; release_notes.md 38, 62, 133.

**Recommendation**: Define the list as running from the `#### Breaking changes` heading to the next heading of any level (`#`–`####`), with fenced lines excluded; state the value for a marked section lacking the heading; add a bulleted `####` subsection after the list in the release-notes fixture and assert it is not counted.

---

### 5. The 0036 row's "0 marked release-notes sections" reads the live root `release_notes.md`, not a pinned input (Score: 55)

The pinning rationale (1398–1404) exists so the calibration row cannot drift, and the owner decision says the row is pinned via test-only inputs (pinned pair *and* release-notes path). But the row as written (1382) passes only the pinned pair, so per FR-21 (847–848) it reads the repository-root `release_notes.md`. The first time anyone runs `/spec:write_release_notes 0036` — nothing forbids it, and finding 2 shows it would *add* a section — that assertion flips to 1. Same live-state drift the pinning was meant to remove.

**Evidence**: NFR-9 row 2 (1382); FR-21 841–848.

**Recommendation**: Invoke the 0036 row with a release-notes path naming a tracked fixture copy of the hand-written (unmarked) 0036 section, or drop the marked-section assertion from that row (the declared fixture's unmarked section already covers the zero-for-unmarked case).

---

### 6. NFR-6 and AC-53 still count "three artefacts"; the new `/spec:write_release_notes` command's conventions have no AC (Score: 50)

NFR-6 opens "This spec delivers three artefacts and all three follow the conventions…" (1310), and the Proposed Solution says "Three artefacts" (71) — both now incomplete, since FR-23 delivers a fourth command file. NFR-6's new bullet (1332–1333) requires `write_release_notes.md` to follow `/spec:*` conventions and be catalogued in `.claude/commands/spec/README.md`, but AC-53 checks conventions/README only for "the three delivered artefacts" and `/spec:show-me`, and AC-89–91 check behaviour, not front matter or catalogue. A stated requirement with no AC.

**Evidence**: 71, 1310, 1332–1333; AC-53 1894–1904.

**Recommendation**: "four artefacts" (or list them); extend AC-53 (or add an AC-91 clause) to cover `write_release_notes.md`'s front matter (`allowed-tools`, `description`, `argument-hint`), `$ARGUMENTS` use and README catalogue entry.

---

### 7. AC-33 still asserts 400–2,000 words for the calibration run, which the document now says is unproven (Score: 50)

NFR-2 now calls the calibration sum "an illustrative estimate … not a proof", names lines it omits (1190–1193), sits at 1,996 (4 words headroom), and assumes "14 breaking-change bullets" (1194) — but under FR-7 the calibration list is now ADR-derived and of unknown size ("≥ 4"). AC-33 still says "**and given** the calibration case (C-8), **then** that count is between 400 and 2,000" (1779–1780). A conforming calibration run at 2,030 words fails AC-33 while meeting every requirement — the owner's "reported target" framing contradicted.

**Evidence**: NFR-2 1167–1172, 1190–1209; AC-33 1775–1780.

**Recommendation**: Drop AC-33's calibration clause, or turn it into "the report states whether the total is inside the range".

---

### 8. AC-79's assertion list omits two facts NFR-9's table asserts (Score: 45)

NFR-9's table asserts, for the declared fixture, "branch, PR and diff fields null with the reason `not a spec directory`", and for 0036 "**0** marked release-notes sections". AC-79's enumerated "asserts" list includes neither, so FR-21's non-spec-directory behaviour (810–814) has no AC naming it, and an implementer working to AC-79 may omit both.

**Evidence**: NFR-9 1381–1382; AC-79 2095–2099.

**Recommendation**: Add both to AC-79's list, or say AC-79 asserts every fact NFR-9's table states.

---

### 9. AC-14, AC-68 and AC-69 still say "a section" where FR-7 now means "a marked section" (Score: 40)

AC-69's Given — "`release_notes.md` contains a section" — read literally with an unmarked section expects `used` and no row-5 line, while AC-92 expects row 5 for the same input. The Definitions row (120) resolves it for a careful reader, but these are the ACs a tester builds fixtures from, and a hand-written section is the natural fixture to build.

**Evidence**: AC-14 1661; AC-68 2008–2009; AC-69 2018; AC-92 2202–2205.

**Recommendation**: "a *marked* section (Definitions)" in all three Givens.

---

### 10. FR-23's boundary cases are unspecified (Score: 40)

Not stated: no or empty `.adr-list` (write `No breaking changes.`, stop, or derive from `requirements.md` alone?); `release_notes.md` with no `##` heading; a marked section already sitting under an older release heading (e.g. `## 10.7.0`) — replaced there (rewriting released notes) or moved?; where `{title}` comes from; and stop messages — "resolving its target exactly as FR-1/FR-2 do" imports FR-2's stop message, which literally tells the user to run `/spec:show-me …` (241–243).

**Evidence**: FR-23 984–1002; FR-2 241–243.

**Recommendation**: State each outcome; give FR-23 its own FR-1/FR-2 message text.

---

### 11. Pinned-run behaviour for the branch/PR fields is ambiguous (Score: 35)

A pinned run "performs no branch resolution for the diff, issues no `gh` query" (843) but does not say whether the spec-branch, base-ref and PR fields are null, resolved as usual (branch only), or carry a `pinned` reason — nor whether a pinned pair is legal with a fixture-directory target (which otherwise nulls diff fields). The ledger schema, and hence AC-70's "one named field per value", would differ between implementations.

**Evidence**: FR-21 841–851.

**Recommendation**: One sentence, e.g. "on a pinned run the branch and PR fields are null with the reason `pinned`, and the merge-base/head fields are the pinned shas".

---

### 12. Leftover figures and working-state residue (Score: 30)

- FR-11's citation example `9 items` (692) sits beside 0036's `76 files under src/`, but the document derives no 9 anywhere; elsewhere 0036's F2 figure is "≥ 4".
- The NFR-2 estimate still assumes 14 bullets (1194).
- `specs/9999-show-me-fixture/` (`requirements.md`, `tasks.md`, `.adr-list`) is still staged. NFR-9 now says no fixture lives under `specs/`; committing the index as-is would ship the fake spec the text rules out.

**Evidence**: 692, 1194; `git status --short` → `A specs/9999-show-me-fixture/…`.

**Recommendation**: Use a neutral example figure; label the 14 as the hand-written catalogue's count; unstage or delete the 9999 files.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 7 |
| 0-49 (Low) | 5 |

**Total findings**: 12
**Findings at or above threshold (60)**: 4

*Verified against the repo*: anchored declared-id counts 0037 = 32, 0036 = 37, 0030–0037 range 8–37; 0036 `tasks.md` 82/0 with tags 62/12/2/6; `6145913a0..91d549be6` → 76 `src/` files, 131 public-API lines, `git merge-base origin/master 91d549be6` = `6145913a0`; PR #4282 OPEN at `91d549be6`; `docs/adr/0062-pg-advisory-lock-sha256.md` and `docs/adr/0072-show-me-command-resolution-and-output.md` exist; `.claude/commands/spec/{design,review,requirements,tasks}.md` exist, `review.md` has *Design (ADR) Review Criteria* at line 160; `.claude/test-fixtures/` does not yet exist; `release_notes.md`'s first `##` is `## Master`, it has two `(spec 0027)` sections and no `<!-- spec:` markers.
