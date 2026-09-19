# Review: requirements — show-me (pass 3)

**Date**: 2026-09-18
**Threshold**: 60
**Verdict**: NEEDS WORK

4 findings at or above threshold 60.

## Findings

### 1. F5's new Medium clause overlaps the Low column — a `Withdrawn` row with a decision *and* a follow-up matches two columns (Score: 68)

Pass 2's fix added a Medium disjunct without narrowing Low, so a `Withdrawn`-with-decision-and-follow-up row satisfies both Low ("`Withdrawn` rows that cite the withdrawal decision **and** a follow-up issue …") and Medium ("≥ 1 `Deferred`/`Dropped`/`Withdrawn` row that **does** state a follow-up …"). F5 is under-determined for exactly the case AC-45 exercises, and the explanatory paragraph concedes the collision in its own parenthetical while its closing sentence claims totality.

**Recommendation**: Restrict Low to "every row is `Shipped`" and let Medium's disjunct own all recorded-gap cases (including `Withdrawn`-with-follow-up), or exclude that case from Medium explicitly. State AC-45's resulting column outcome.

---

### 2. FR-9's and AC-17's pinned round-1 severity numbers are not derivable from the Definitions as written (Score: 68)

Round 1's three findings exist as both a numbered tracking-comment bullet and a separately-posted inline comment (different author login: `claude` vs `claude[bot]`), so `Finding`'s "inline comment **or** numbered item" wording and `Review round`'s same-author grouping rule don't resolve which text is "the finding" or whether it's one finding or two. Severity also depends on which text is read: the tracking bullet's prose ends "…so low blast radius…", which `Finding severity`'s literal "first such word in the title or first line" rule would score as Low; the inline comment's bold title has no severity word. The document currently asserts `(0/0/0/0/3 unclassified)` for round 1 without a rule that produces that answer over the alternative.

**Recommendation**: State a precedence rule for inline-vs-tracking-comment duplicates (the inline comment is the finding; the tracking-comment restatement is not a second finding) and confine severity matching to a leading/bracketed marker rather than any word in the first line.

---

### 3. The Additional Context still gives the exclusion reason pass 2 corrected in the Definitions (Score: 64)

The `Review round` definition now correctly says test (ii) does not fire for the 2026-08-28 pass because `tasks.md` predates it by ~15 hours. The Additional Context worked example, fifty lines later, still says the pass is excluded "because it reviewed the spec's planning documents **before `tasks.md` existed**" — the exact false claim that was corrected elsewhere. The two passages now give incompatible reasons for the same exclusion, and a reader is more likely to internalise the Additional Context version.

**Recommendation**: Replace with "because it reviewed the spec's planning documents rather than the implemented code — it self-identifies as design-only and every finding cites only `specs/`/`docs/adr/` paths".

---

### 4. FR-7's fix makes `release_notes.md` normative for item granularity while the same requirement says the command may never read it (Score: 60)

The corrected worked example adds a general rule: "the command must neither split one catalogue bullet into several items nor merge two catalogue bullets into one" — anchored to an input FR-7 itself makes optional ("may read it… never required, never a prerequisite"; FR-16 row 5 defines the no-catalogue path explicitly). Two conforming implementations — one that reads the catalogue, one that doesn't — will disagree on item count and therefore on F2's level.

**Recommendation**: Demote to a tie-break: follow the catalogue's grouping when one is read; when none is read, one bullet per distinct public-API or behavioural change as the ADRs state it.

---

## Summary

| Score Range | Count |
|---|---|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 5 |
| 0-49 (Low) | 3 |

**Total findings**: 8
**Findings at or above threshold (60)**: 4

## Below-threshold (recorded, not blocking)

- **F3 rationale contradicted by its own evidence (55)** — "most findings are unclassified" is refuted by the round data (8 of 11 carry a severity word); the conclusion (F3 rarely reaches High) still holds, restate the reason.
- **FR-16 row 15 blanks the base ref unnecessarily, and its "PR reference populated normally" claim is unreachable (48)** — the base ref is resolvable independently of the spec branch; and FR-20 has no branch name to query when the branch is undeterminable, so the PR reference can only read `none found`.
- **The design pass's "ten findings" phrasing collides with `Finding`'s own container-splitting clause (40)** — item 10 is a five-bullet "smaller items" bucket; say "ten numbered items" instead.
- **"fourth item" in FR-7's example is ambiguous with the catalogue's own bullet numbering (30)** — it's the fourth item in FR-7's enumeration, not catalogue bullet 4. Drop "fourth".

Also outstanding from pass 2 (not in scope for this pass, still open): FR-8's "least-shipped" direction, F3's un-tie-broken dual match, NFR-2's 400-word floor, FR-14's sub-three-file diff case.

## Verified correct (carried forward, plus this pass's new checks)

Pass-2 fixes for findings 2 (NFR-1/F3), 3 (metadata block, minor nits only), 6 (C-8's AC list) and the round-2/round-3 severity numbers were all confirmed correct against the actual PR and `release_notes.md`. The 517/76/393/24/14/0/10 bucket split, 82-task breakdown, 14 catalogue items (5 combined), `.adr-list`'s seven `.md` filenames, `.issue-number` 4256, and the six task-completion comments all re-verified clean.
