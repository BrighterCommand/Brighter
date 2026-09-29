# Review: requirements — 0037-show-me (pass 13)

**Date**: 2026-09-25
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. Consider addressing lower-scored items.

*Reviewed at HEAD `50f6bc6af`. The `.requirements-approved` marker on disk is stale, so this review
treats the phase as open.*

## Pass-12 findings — resolution check

| # | Pass-12 score | Resolved? | Note |
|---|---|---|---|
| 1 (the "Only `/spec:write_release_notes` writes the marker" and Out of Scope "no section … rewritten") | 62 | Yes | Line 121's *Who marks* clause now allows a hand-added marker. Out of Scope (1603–1607) is now scoped to *unmarked* sections, and FR-23 1021–1024 agrees. No other sentence in the document still says only the command writes the marker (grep of "marker"/"marked"). Some residual over-statement is left in the new *Who marks* text (finding 4). |
| 2 (pinned run against a fixture directory) | 58 | Yes, in text | The precedence is stated at 813–815 ("unless the run is pinned") and 858–861 ("takes precedence … no field of a pinned run carries that reason"). Two gaps remain: no AC exercises it (finding 1), and the unpinned clause does not say what happens to base-ref (finding 2). |
| 3 (calibration row's file-derived figures) | 57 | Yes | This is now handled as a stated drift condition, per the owner's decision. NFR-9 1451–1457, AC-79 2149–2153 and C-8 1523–1525 agree with each other. The working tree was checked: `git diff --stat 91d549be6 HEAD -- specs/0036-…` is empty, and `91d549be6` is an ancestor of HEAD. |
| 4 (fenced `#` line ends the `{m}` list) | 54 | Yes | The Definitions now define heading and fence (121). The `{m}` rule cites that definition (843–846). The fixture now puts a fenced block holding a column-0 `- ` line and a column-0 `# ` line *between* the two list bullets (1435). An implementation that ignores fences gets `{m}` = 1, and one that doesn't skip fenced lines gets 3, so the fixture tells them apart. |
| 5 (the unmarked-section stop could match marked headings) | 50 | Yes | FR-23 1034–1047 adds "**not** followed immediately by a marker line". AC-95's third clause (2296–2298) covers a section marked for another directory. |
| 6 (FR-7 grouping when the marked section has no catalogue) | 45 | Yes | The tie-break is now gated on `{m}` > 0 (509–518). The fallback covers `{m}` null or 0, and AC-92 (2275) asserts the no-catalogue rule. |
| 7 (stop message silent about overwrite) | 42 | Yes | FR-23 1042–1044 and AC-95 2292–2294 now require the message to say this. |
| 8 (test clobbers a real ledger) | 40 | Yes | NFR-9 1422–1424 and AC-79 2163–2164 now say the test restores the ledger byte-for-byte. |
| 9 (AC-79 "every fixture"; definition scope) | 32 | Yes | AC-79 now says "every fixture *directory*" (2154). The definition now covers FR-21's release-notes path (121). |

## Findings

### 1. No AC covers FR-21's rules for the fields of a pinned run (Score: 55)

The pass-12 edit added normative rules for pinned runs:

- "the spec-branch, base-ref and PR fields are null with the reason `pinned`, whatever the target"
  (858);
- "takes precedence over the not-a-spec-directory rule above, so no field of a pinned run carries
  that reason" (859–861);
- the script "records in the ledger that the run was pinned" (856–857).

No acceptance criterion asserts any of them:

- AC-79 (2149–2167) lists what it checks for the pinned 0036 row: ids, checkboxes, tags, public-API
  count and `src/` count. It asserts no `pinned` reasons and no pinned flag.
- AC-93 covers only a sha that is not present locally.
- No NFR-9 row runs a *fixture directory* pinned, so the precedence rule that settled pass-12 #2 is
  never exercised.

An implementation that emits `not a spec directory` on a pinned fixture run, or that forgets the
pinned flag, would pass every stated criterion.

**Evidence**: FR-21 853–862. AC-79 2154–2162 and AC-93 2277–2281 are the only ACs that mention
pinning. The NFR-9 table (1426–1436) asserts no reason field for row 2.

**Recommendation**: Add to NFR-9 row 2 and AC-79: "branch, base-ref and PR fields null with the
reason `pinned`; the ledger records the run as pinned; no field carries `not a spec directory`."
Alternatively, add one pinned fixture-directory run that asserts the precedence.

---

### 2. For an unpinned fixture directory, FR-21 does not say whether base-ref is resolved or null (Score: 48)

The unpinned rule nulls "the branch, PR and diff fields" with the reason `not a spec directory`
(814–815). The pinned rule names **base-ref** explicitly alongside spec-branch and PR (858). By
contrast, the unpinned rule leaves base-ref out.

FR-16 row 15 (1080) establishes that base ref "does **not** depend on the spec branch and is still
resolved", which supports reading it as resolved on fixture runs. On the other hand, "branch fields"
plausibly includes it. NFR-9 row 1 (1428) and AC-79 (2160–2161) assert "branch, PR and diff fields
null", so the tester and the implementer can disagree about one field of AC-70's schema. The same
question applies to the commit count: is it a "diff field"?

**Evidence**: 813–815 vs 857–858; FR-16 row 15; NFR-9 row 1.

**Recommendation**: List the fields explicitly, e.g. "spec-branch, base-ref, PR, merge-base,
measured-head, commit-count and every diff-derived field are null with the reason
`not a spec directory`". Alternatively, state that base-ref is resolved normally.

---

### 3. FR-23 treats two sections marked for the target as an error, but `/spec:show-me` has no rule for them (Score: 40)

FR-23 now stops when "more than one section is marked for the target … it cannot tell which to
replace" (1032–1033). `/spec:show-me` accepts that state silently:

- FR-21 counts the sections and sums `{m}` "over them" (838–840).
- FR-7 speaks throughout of reading "the marked section", singular (490, 509).
- NFR-3 says a marked section "is read by targeted extraction of that section alone" (1328–1329).

With a count of 2, one implementer reads both sections and uses both catalogues for the tie-break.
Another reads the first section only, while the disagreement line compares `{n}` against a summed
`{m}`.

**Evidence**: 838–840, 490, 509–513, 1328–1329 vs 1032–1033.

**Recommendation**: State that when the count exceeds 1, every marked section is read and charged,
and their `#### Breaking changes` lists together form the catalogue. Alternatively, require FR-16 to
report the duplicate.

---

### 4. The new *Who marks* clause over-states two things (Score: 38)

Line 121 says: "The only other way a section becomes marked is a person adding the marker line by
hand beneath a hand-written section's heading". Two things in this document already contradict
"only":

- NFR-9's release-notes fixture (1435) is a marked section written wholesale by hand. It was not
  produced by FR-23 and was not added beneath an existing heading.
- A person can simply type a complete marked section.

The same clause also promises that the next run "replaces its body in FR-23's form and keeps only
its title" on the single condition "provided the section sits under the first `##` heading". FR-23
has two further stops that pre-empt that promise:

- more than one section marked for the target (1032–1033);
- another unmarked `(spec NNNN` heading under the first `##`, for example one belonging to the
  sibling `0036-generator-universal-rejection-tests` (1034–1047).

The Definitions table is normative, so this is the same class of defect as pass-12 #1, though far
less consequential.

**Evidence**: 121 vs 1435 and 1030–1047.

**Recommendation**: Replace the "only other way" sentence with "A section may also be marked by
hand". Replace the "provided …" clause with "subject to FR-23's stops".

---

### 5. Small boundary gaps in FR-23 (Score: 32)

- **No `requirements.md`.** The items come from ADRs and `requirements.md`, or from
  "`requirements.md` alone" when there is no `.adr-list` (1019–1021). FR-23 runs at design time
  without FR-3, so a target with neither an `.adr-list` nor a `requirements.md` is reachable, and
  its behaviour (write `No breaking changes.`, or stop) is unstated.
- **First `##` already released.** "The first `##` heading (the unreleased heading)" (1005–1006)
  assumes the first `##` is unreleased. Right after a release is cut, before a new `## Master` is
  added, the command would write under a released version. That is the thing the "released notes
  are not rewritten" stop exists to prevent.

**Evidence**: 1005–1006, 1019–1021, 1030–1031.

**Recommendation**: Add one line for each case: either a stop, or a statement of the assumed state.

---

### 6. "Every file-derived field is still read from the target directory" is literally false for two of the fields (Score: 30)

Line 861–862 says: "Pinning fixes the diff, not the files: every file-derived field is still read
from the target directory in the working tree." FR-21's own list of file-derived fields (815–816)
includes "`.adr-list` resolution" and "release-notes section". Those are resolved against
`docs/adr/` and the repository-root `release_notes.md` (or the release-notes path), not the target
directory. The intent is clear ("working tree, not the pinned head"), but the wording is wrong.

**Evidence**: 815–816, 861–862, 865–866.

**Recommendation**: "… is still read from the working tree, not from the pinned head."

---

### 7. Minor citation drift in the calibration-row condition (Score: 25)

- C-8 (1524–1525) now reads "subject to NFR-9's condition on the calibration spec's own files
  (NFR-9, AC-94)". AC-94 is the CONTRIBUTING merge-commit check. The file condition is asserted by
  AC-79's Given.
- NFR-9's prose names only one way the condition breaks: "unless #4282 first gains a commit that
  edits either file" (1455). AC-79's Given is general ("identical to their content at
  `91d549be6`") and also covers a later commit on `master` that edits those files.

**Evidence**: 1524–1525, 1451–1457, 2149–2153.

**Recommendation**: Cite AC-79 in C-8. In NFR-9, say "unless either file is edited after
`91d549be6`, whether on #4282 or later".

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 1 |
| 0-49 (Low) | 6 |

**Total findings**: 7
**Findings at or above threshold (60)**: 0

*Checked against the repo*:
- Declared-id counts with the stated POSIX pattern: 0037 = 32 and 0036 = 37.
- `git diff --stat 91d549be6 HEAD -- specs/0036-scoped-lifetime-per-pipeline` is empty, and
  `91d549be6` is an ancestor of HEAD.
- `gh pr view 4282` returns OPEN with head `91d549be6f83…`.
- `release_notes.md`: `## Master` is the first `##`. Line 5 is
  `### Scoped lifetime per pipeline (spec 0036, #4256)` with no marker, and its title under FR-23's
  rule would be "Scoped lifetime per pipeline". There are two `(spec 0027)` sections, one under
  `## Master` (34) and one under `## 10.7.0` (234). Every fence is at column 0.
- Both of the declared fixture's `.adr-list` targets exist: `0062-pg-advisory-lock-sha256.md` and
  `0072-show-me-command-resolution-and-output.md`. A different `0072-ambient-scope-adoption-seam.md`
  also exists, which is consistent with C-9.

*Findings reordered by score (descending) when recorded; content unchanged.*
