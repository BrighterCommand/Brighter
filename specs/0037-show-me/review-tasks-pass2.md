# Review: tasks — show-me (pass 2, fix verification)

**Date**: 2026-09-19

Pass 1 (`review-tasks.md`) found 16 findings, 10 at/above threshold 60 (3 Critical). A full revision
addressed all 16:

1. [95, Critical] Circular fixture dependency on `specs/0037-show-me/` — fixed with a new shared
   synthetic fixture (`specs/9999-show-me-fixture/`, tracked/staged, resolvable branch), created by
   new task T2.1 and torn down by T6.5, used by T2.2, T2.4, T2.5, T3.9, T4.2, T6.3 and (with temporary
   modifications) T3.3, T3.7, T3.8, T3.10.
2. [92, Critical] T3.6's 0034 row count — corrected to 15 (verified: 9 FRs + 6 NFRs), reframed as a
   third positive fixture for the list-lead-in declaration shape.
3. [90, Critical] T2.6's unresolvable path-prefixed `.adr-list` entry — the resolution mechanism now
   strips a leading `docs/adr/` before matching (T2.7), the real 0023 entry is stated as an FR-16
   row 7 case, and the shape is genuinely exercised via the new fixture's `.adr-list`.
4. [82, High] T5.4's unreachable all-Low synthetic fixture — replaced with a three-part discharge
   (structural grep, empirical same-fixture comparison, hand-evaluated factor sets); AC-21's full
   all-Low set is now explicitly hand-evaluated in T5.2 with the reason it can never be fixtured
   stated plainly.
5. [78, High] Generated `show-me.md` cleanup — a stated cleanup rule at the top of the file, applied
   to every task touching a shared real fixture; T5.5's "no pre-existing file" precondition is now
   explicit and ordering-dependent.
6. [75, High] Untracked synthetic fixtures vs FR-17 — the shared fixture is staged from creation;
   temporary per-task modifications are restored with `git checkout --`; T3.10's check now requires a
   non-zero resolved-link count.
7. [72, High] T4.3's false author-login claim — corrected: the real data shows all objects on PR #4282
   authored `claude` (no `claude[bot]`), so login-independence is now asserted by inspecting the
   command file's logic rather than by a nonexistent fixture mismatch.
8. [68, Medium] T2.5's false "zero declared ids" claim for spec 0027 — corrected to 18 (verified).
9. [65, Medium] T6.4's "checked out, or merged" — narrowed to merge-only, with an explicit
   `git rebase --onto` reset accounting for intervening commits.
10. [62, Medium] Phase 4's hand-evaluation of everything — restructured: a new T4.1 merges the
    calibration branch at the head of Phase 4, so T4.3/T4.4/T4.5/T4.6 now observe real
    `/spec:show-me scoped-lifetime` runs instead of hand-deriving the outcome.
11. [58] T4.1(now T4.2)'s "five markers" — corrected to eight, both groups enumerated with timestamps.
12. [55] T3.6 monolithic — split into T3.6 (mechanical row-set) and T3.7 (judged statuses), matching
    NFR-1's own determinism boundary.
13. [45] T1.1's non-executable acceptance check — replaced with a must-produce-no-output grep.
14. [40] T6.1's README placement — moved to after "Running the unattended loop", before "Complete
    Example".
15. [35] Stale `specs/0037-show-me/` fixture-table row — replaced with `specs/0005-defer-message-on-error/`, with an explicit AC-8 note.
16. [32] Scope-creep claim vs T6.4/T6.6 — both now cite ADR 0072/0073 *Decision* end-to-end with a
    stated reason (composing already-tested behaviour, not introducing new behaviour).

The revision also introduced two new infrastructure tasks (T2.1, T4.1) and split one (T3.6→T3.6+T3.7),
bringing the total from 37 to 40 tasks. Re-verified structurally: 35 VERIFY + 5 STRUCTURAL = 40 total
checkboxes; 35 gate lines (one per VERIFY task, none on STRUCTURAL); task ids run T1.1–T6.6 with no
gaps or duplicates; the coverage cross-reference tables were updated throughout to the new numbering.

Spot-checked independently: spec 0034 declares 9 FRs (`**FR-n [TAG] — …**`) + 6 NFRs = 15 ids
(confirmed via `grep -ocE '^\*\*FR-[0-9]+ \['`, corrected from an initial narrower pattern); spec 0027
declares 18 ids (confirmed); no existing `spec/show-me-fixture` branch or `specs/9999-*` directory
collides with the new fixture's naming.

**Verdict: fixes verified. Ready for approval.**
