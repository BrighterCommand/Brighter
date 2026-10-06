# Review: requirements — 0037-show-me (pass 12)

**Date**: 2026-09-25
**Threshold**: 60
**Verdict**: NEEDS WORK

1 finding at or above threshold 60. Address these before approving.

*Reviewed at HEAD `655cf49e4`. The `.requirements-approved` marker on disk is stale (it predates the
2026-09-21/22 amendments); this review treats the phase as open.*

## Pass-11 findings — resolution check

| # | Pass-11 score | Resolved? | Note |
|---|---|---|---|
| 1 (Out of Scope "only FR-22's two format checks") | 62 | Yes | Lines 1614–1615 now read "gains only FR-22's two format checks and FR-23's release-notes check". |
| 2 (duplicate section beside an unmarked hand-written one) | 60 | Mostly | The stop-and-ask case (1026–1033) and AC-95 are in place. Two things are left: the edit made the Definitions sentence "Only `/spec:write_release_notes` writes the marker" false (finding 1), and the stop condition's wording is loose (finding 5). |
| 3 (C-8 bold rule and AC-79 Given contradict the pinned row) | 62 | Yes, with residue | AC-79 has lost its *(C-8)* marker and its Given now names the pinned preconditions (2129–2132). C-8's bold rule is scoped to unpinned criteria (1504–1507). Residue: the row's file-derived figures are still read live from the working tree, so the "survives the merge" claim only half holds (finding 3). |
| 4 (`{m}` list end undefined) | 60 | Yes, with residue | The *`{m}` rule* (842–849) now defines where the list ends, what fenced lines do, and the null/0 cases, and the fixture gained a `#### Usage` subsection. Residue: a `#` line inside a fence ends the list, and nothing tests fence-skipping inside the list (finding 4). |
| 5 (0036 row reads the live `release_notes.md`) | 55 | Yes | The assertion was dropped (1414). |
| 6 ("three artefacts"; AC-53 does not cover the new command) | 50 | Yes | "Four artefacts" at 71 and 1342. AC-53 now covers `write_release_notes.md` front matter and the README catalogue entry (1930–1939). |
| 7 (AC-33 calibration range) | 50 | Yes | 1815–1816. |
| 8 (AC-79 omits NFR-9 facts) | 45 | Yes | AC-79 now asserts "every fact NFR-9's table states" (2135–2140). |
| 9 (AC-14/68/69 "a section") | 40 | Yes | Each now says "*marked*". |
| 10 (FR-23 boundary cases) | 40 | Yes | Covered now: no or empty `.adr-list`, no `##` heading, a section under a released heading, `{title}`, and the stop-message substitution (997–1033, AC-95). |
| 11 (pinned-run field values) | 35 | Yes, but introduced a contradiction | 855–857 settle the fields, but "accepted with any target, fixture directories included" now conflicts with 811–814 (finding 2). |
| 12 (leftover figures and working state) | 30 | Partly | `5 items` (693) and the 14-bullet proxy label (1225) are fixed. `specs/9999-show-me-fixture/` is still staged (finding 10). |

## Findings

### 1. The pass-11 edit made two statements about release-notes markers false: "Only `/spec:write_release_notes` writes the marker" and "no section already in `release_notes.md` is rewritten … by any command" (Score: 62)

To resolve pass-11 #2, FR-23 now tells the user to add the marker **by hand** (1029–1031). AC-92
(2250–2251) and AC-95 (2270) both have the marker "added by hand", and Out of Scope now says
"marking one is left to a person" (1586–1587). The *Marked release-notes section* definition was not
updated. It still says "Only `/spec:write_release_notes` writes the marker (FR-23)" (line 121). The
Definitions table is the normative place for this term, and FR-23, AC-92 and AC-95 now contradict it.

The same bullet in Out of Scope also says "No section already in `release_notes.md` is rewritten,
re-formatted or given a marker by any command" (1583–1584). But AC-95's second clause has
`/spec:write_release_notes` replace a section that already existed, hand-written, "in place in
FR-23's form" (2270–2271) once a person has marked it. Spec 0036's section at `release_notes.md`
lines 5–33 is exactly that case. Read literally, the Out of Scope sentence forbids AC-95's expected
outcome.

This is the same class of stale text the pass-10 edit left behind (pass-11 #1, scored 62).

**Evidence**: line 121 "Only `/spec:write_release_notes` writes the marker (FR-23)" (`sed -n 121p`
confirms; the sentence dates from pass 10). Against it: FR-23 1026–1033, AC-92 2250–2253, AC-95
2266–2271, Out of Scope 1583–1587.

**Recommendation**: Line 121 → "The marker is written by `/spec:write_release_notes`, or added by
hand to a hand-written section (FR-23)". Out of Scope → "No command rewrites, re-formats or marks an
*unmarked* section; once a person marks one, it is a marked section and FR-23 replaces it in place."

---

### 2. FR-21 contradicts itself on a pinned run against a fixture directory (Score: 58)

Lines 811–814 are unconditional: for a target not directly under `specs/`, "the branch, PR and diff
fields are null with the reason `not a spec directory`". The pass-11 edit added at 855–857: on a
pinned run "the spec-branch, base-ref and PR fields are null with the reason `pinned`; and every
diff-derived field is measured over the pair. A pinned pair is accepted with any target, fixture
directories included."

For a pinned fixture-directory target, the diff fields are therefore both null and measured, and the
reason is both `not a spec directory` and `pinned`. Two implementations will produce different
ledgers, and AC-70's "one named field per value" schema will differ between them. No NFR-9 row
exercises the combination, so no test pins it. The sentence that allows the combination was added
specifically to settle pass-11 #11, so the ambiguity is new.

**Evidence**: FR-21 811–814 vs 855–857. NFR-9 row 1 (1413) asserts `not a spec directory` for an
unpinned fixture only.

**Recommendation**: State the precedence, e.g. "A pinned pair overrides the not-a-spec-directory
rule for the diff fields; branch/PR fields carry `pinned`". Or forbid a pinned pair with a fixture
target (tooling fault).

---

### 3. The calibration row's file-derived figures are not pinned, so "the figures hold … survive the merge" is only half true (Score: 57)

NFR-9 (1430–1438) and AC-79 (2129–2132) claim the 0036 row holds "before or after it merges". But
only the diff figures (131, 76) are measured over the pinned pair. The ids (37), checkboxes (82/0)
and per-tag counts (62/12/2/6) are read from `specs/0036-scoped-lifetime-per-pipeline/` "in the
working tree" (1436–1438).

PR #4282 is still OPEN (`gh pr view 4282` → OPEN, head `91d549be6`). Spec 0036's review-response
passes have previously added tasks to its `tasks.md`. Any further push to #4282 that touches its
`tasks.md` or `requirements.md` changes 82 or 37. Once that reaches a branch the test runs on, the
row fails permanently even though both pinned shas are still reachable. This is the live-state drift
the pinning was introduced to remove (pass-11 #5 was the same class for the release-notes figure).
Today's working tree was verified to match `91d549be6`: 82/0, 62/12/2/6, 37 ids.

**Evidence**: NFR-9 1414, 1430–1438; AC-79 2129–2137; `git diff --stat 91d549be6 HEAD --
specs/0036-scoped-lifetime-per-pipeline` is empty today.

**Recommendation**: On a pinned run, read the file-derived fields from the pinned head
(`git show {head}:{path}`). Alternatively, state the drift risk honestly and drop "before or after
it merges" from AC-79's Given.

---

### 4. The `{m}` rule ends the list at "a line beginning `#`" even inside a fence, and the fixture never tests fence-skipping inside the list (Score: 54)

The rule (842–845) ends the list at "the next heading of any level (a line beginning `#`)" and says
"within it, lines inside a fenced block are skipped". It does not say that a `#` line inside a fence
is not a heading. A C# migration snippet inside the list with `#if`, `#region` or `#pragma` at
column 0, or a shell comment in a fence, would end the list early under one reading and not under
the other. The section boundary in the Definitions (line 121, "to the next `##` or `###` heading")
has the same gap.

The release-notes fixture (1420) puts its fenced block inside the later `#### Usage` subsection,
where the heading rule already excludes it. So an implementation that never skips fenced lines within
the list still yields `{m}` = 2 and passes. The fence clause of the rule is therefore untested. No
such lines exist in today's `release_notes.md` (checked with awk), so this is latent, but `{m}` is
declared mechanical (NFR-1).

**Evidence**: 842–849; line 121; NFR-9 row 1420.

**Recommendation**: "A heading is a line outside any fenced block that begins with one to six `#`
followed by a space". Also move (or add) a fenced block with a column-0 `- ` line and a `#` line
*inside* the `#### Breaking changes` list of the fixture, still asserting `{m}` = 2.

---

### 5. FR-23's "unmarked section" stop condition does not say it excludes marked headings, including another same-id spec's marked section (Score: 50)

The bold label says "an unmarked section may already describe this spec". The operative condition
that follows is only "a `###` heading under the first `##` heading contains `(spec {NNNN}`"
(1026–1027). Read literally, it matches:

- the target's own previously written marked section, whose heading FR-23 writes as
  `(spec {NNNN}…)` (1002), which would break replace-in-place on every re-run;
- a section **marked** for a different directory with the same id.

The second case is real: `specs/0036-generator-universal-rejection-tests/` and
`specs/0036-scoped-lifetime-per-pipeline/` both exist. "The match is on the id alone, so a different
spec sharing the id also stops the command" (1031–1033) invites the broader reading. AC-95 only
exercises a heading with no marker at all. One implementer stops on any unmarked heading; another
stops on any heading not carrying the target's own marker.

**Evidence**: FR-23 1002, 1026–1033; AC-95 2266–2268; `ls specs | grep 0036`.

**Recommendation**: "a `###` heading under the first `##`, **not immediately followed by any marker
line**, contains `(spec {NNNN}`". Add an AC-95 clause: a section marked for another same-id
directory does not stop the command.

---

### 6. With a hand-marked section that has no `#### Breaking changes` list, FR-7's grouping rule has no catalogue to follow (Score: 45)

AC-92's new third clause (2250–2253) has a marked section with no `#### Breaking changes` heading.
The command "reads the section", `{m}` is null, and no disagreement line appears. FR-7's grouping
rule (509–517) says that whenever a section is read, "the command follows the catalogue's own bullet
boundaries", and it falls back only when no section is read. Spec 0036's hand-written section has
bullets that do not sit under a `#### Breaking changes` heading. One implementer groups by those
bullets; another applies the no-catalogue fallback because the mechanical list is null.

**Evidence**: FR-7 509–517; `{m}` rule 846–849; AC-92 2250–2253; `release_notes.md` 5–33.

**Recommendation**: State that the grouping rule uses the catalogue only when `{m}` is not null, and
otherwise applies the no-catalogue fallback.

---

### 7. The FR-23 stop message invites the user to mark a hand-written section without saying the next run overwrites it (Score: 42)

The stop offers "delete that section or add the marker … then re-run — after which the section is …
replaced in place" (1029–1031). Replacement keeps only the title (1003–1004). For spec 0036 that
means discarding a curated 14-item catalogue (`release_notes.md` 5–33) in favour of a derived list.
The requirement does not oblige the message to say so, so a user may mark the section expecting it
to be kept.

**Evidence**: 1003–1004, 1029–1031; AC-95 2270–2271.

**Recommendation**: Require the stop message to state that marking the section hands its body to
the command, which regenerates it.

---

### 8. The test script's pinned 0036 run writes its ledger into a real spec directory (Score: 40)

The ledger is written beside the target (815–817), so NFR-9's 0036 row writes
`specs/0036-scoped-lifetime-per-pipeline/.show-me-ledger.json`. That is the same path a real
`/spec:show-me 0036` run uses, and the stated first real use is on #4282. The test atomically
replaces any existing real ledger with a pinned one, then "removes every ledger its own runs created"
(1408–1409, AC-79 2142). It is undefined whether a replaced pre-existing ledger counts as "created"
and is deleted, or is left holding pinned values. The claim that directories are "left as it found
them" does not hold for that directory.

**Evidence**: 815–817, 1408–1409, 2142.

**Recommendation**: Have the test preserve and restore a pre-existing ledger for that target, or say
explicitly that a pre-existing ledger in a spec-directory target is removed.

---

### 9. Two small inconsistencies in the fixture and definition wording (Score: 32)

- AC-79 says the script is invoked "against every fixture in NFR-9's table — [0036] … and the four
  fixture directories" (2133–2135). The table has eight rows, including two word-count files, the
  release-notes file and an inline line; those are covered by AC-80 and AC-81, not AC-79.
- The *Marked release-notes section* definition is scoped to "the repository-root
  `release_notes.md`" (121). FR-21's release-notes path (860) means marked sections are also looked
  for in another file.

**Evidence**: 2133–2135 vs 1411–1421; 121 vs 860.

**Recommendation**: "every fixture *directory* in NFR-9's table". Definition: "… of
`release_notes.md` (or the file named by FR-21's release-notes path)".

---

### 10. `specs/9999-show-me-fixture/` is still staged (Score: 30)

This is unchanged since pass 11 #12. `git status --short` shows
`A specs/9999-show-me-fixture/{.adr-list,requirements.md,tasks.md}`, while NFR-9 (1404–1408) rules
out fixtures under `specs/`. Committing the index as it stands would ship the fake spec the text
forbids.

**Evidence**: `git status --short`.

**Recommendation**: Unstage or delete those files before the next commit.

*Note (main agent)*: the staged-not-committed state is deliberate. Pre-rescope task T2.1 creates the
files that way and T6.5 tears them down, and every commit on this branch is made by pathspec. What
happens to the fixture belongs to the `tasks.md` revision, not to `requirements.md`.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 5 |
| 0-49 (Low) | 5 |

**Total findings**: 10
**Findings at or above threshold (60)**: 1

*Checked against the repo*:
- Declared-id counts: 0037 = 32, 0036 = 37.
- 0036 working-tree `tasks.md`: 82 checkboxes, 0 unchecked; tags 62/12/2/6, 0 untagged. It is
  identical to `91d549be6`.
- `spec/show-me` contains `91d549be6`. PR #4282 is OPEN with head `91d549be6`.
- `release_notes.md`: `## Master` is the first `##`. The 0036 section at line 5 has no marker. There
  are two `(spec 0027)` sections (lines 34 and 234), no `<!-- spec:` markers, and no `#`-leading or
  `- `-leading lines inside fences.
- `.claude/settings.json` has `Bash(wc:*)`, and its `gh` entries are exactly the five C-10 lists.
- `.claude/test-fixtures/` does not exist yet.
- The NFR-2 ceiling sum is 1,996, and AC-18's buckets sum to 517.
