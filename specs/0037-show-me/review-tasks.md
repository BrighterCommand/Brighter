# Review: tasks — show-me

**Date**: 2026-09-19
**Threshold**: 60
**Verdict**: NEEDS WORK

10 findings at or above threshold 60. Address these before approving.

## Findings

### 1. Six `VERIFY` tasks use `specs/0037-show-me/` as a *successful-run* fixture, but its own `tasks.md` is unfinished — the FR-3 gate those tasks build will refuse every one of those runs (Score: 95)

`specs/0037-show-me/tasks.md` now exists with 37 checkboxes, all unchecked. FR-3/Step 2 therefore stops `/spec:show-me 0037-show-me` and writes nothing. Six tasks (T2.1, T2.3, T2.4, T3.8, T4.1, T6.3) depend on that invocation producing a document, and it is the *only* fixture on this branch with a resolvable spec branch — so the dependency is circular until every task in the file is ticked (which only T6.6 accounts for).

**Recommendation**: Use a synthetic `specs/9999-show-me-fixture/` with a committed two-checkbox `tasks.md` on a throwaway ref (T2.1 already builds most of this machinery) consistently for T2.1/T2.3/T2.4/T3.8/T4.1/T6.3, reserving `specs/0037-show-me/` for T6.6's deliberate two-state dogfood.

---

### 2. T3.6's `0034` row-count assertion contradicts the task's own three-shape counting rule (Score: 92)

`specs/0034-failed-delivery-context/requirements.md` declares NFR-1…NFR-6 in exactly the third bold-list-item shape T3.6 itself mandates matching. The correct row count is **15** (FR-1…FR-9 + NFR-1…NFR-6), not the "9, not 15" the task asserts.

**Recommendation**: Change the fixture claim and assertion to 15 rows; 0034 becomes a second positive fixture for the third declaration shape.

---

### 3. T2.6's path-prefixed `.adr-list` fixture points at a file that does not exist, and the stated resolution mechanism cannot match it anyway (Score: 90)

`specs/0023-asyncapi-document-generation/.adr-list` holds `docs/adr/0040-asyncapi-document-generation.md`, which does not exist in `docs/adr/` (only `0040-add-the-specification-pattern.md` and `0040-mssql-dlq-brighter-managed.md` do). T2.6's stated `ls docs/adr/ | grep -E "^{entry}"` also cannot match a path-prefixed entry by construction. The entry is never actually verified in T2.6's own commands (0023 is unusable anyway — its `tasks.md` has 0 checkboxes, so FR-3 refuses it).

**Recommendation**: Either drop the path-prefixed tolerance and let FR-16 row 7 handle it, or fix the resolution mechanism to strip a leading `docs/adr/`, state the 0023 entry resolves to nothing, and add the path-prefixed shape to the synthetic `9999` fixture's `.adr-list` instead.

---

### 4. T5.4's "all-Low synthetic fixture" is unreachable, so AC-25 cannot be run as specified — and AC-21 is never actually verified anywhere (Score: 82)

A synthetic fixture has no PR, so FR-16 row 1 forces F3 = Medium and F4 = Medium — the "Low-shaped" run comes out Medium, not Low, so AC-25's Low-vs-High comparison cannot be constructed as written. Separately, no task hand-evaluates or observes the full all-Low factor set AC-21 requires; T5.2's "AC-21 shape" check is a shape assertion, not AC-21 itself.

**Recommendation**: Replace T5.4's Low/High comparison with two hand-evaluated factor sets (as T5.3 already does for AC-22) plus a real Medium-vs-Medium side-effect comparison on `sqs-cleanup`. Add an explicit hand-evaluation of AC-21's full all-Low set to T5.2.

---

### 5. Generated `show-me.md` files are never cleaned up, which breaks T5.5's "no pre-existing file" fixture assumption (Score: 78)

`specs/0033-pg-advisory-lock-sha256/show-me.md` is written by T3.1, T3.2, T3.5, T4.5, T5.2 and T5.3 before T5.5 runs, but T5.5 requires "no pre-existing `show-me.md` on the first run" for its AC-9 "created" assertion. Only T3.1 mentions cleanup, and defers the decision rather than making it. The same applies to `specs/0002-sqs-cleanup/show-me.md` and `specs/0034-failed-delivery-context/show-me.md`.

**Recommendation**: Resolve T3.1's open decision to "delete", add a one-line cleanup instruction to every task that writes into a shared fixture directory, and either re-order so T5.5 is first to write into its fixture or point it at an untouched one.

---

### 6. Every synthetic `9999-show-me-fixture` run collides with the FR-17 tracked-path rule, making its own checks vacuous (Score: 75)

T3.9 requires every written path to be tested with `git ls-files --error-unmatch` and dropped/replaced if untracked. The synthetic fixture's own files are never staged, so they're untracked — under the stated rule the metadata block's spec-directory line, `## Inputs used` rows naming them, and FR-16 row 13's exact fallback line would all have to be dropped or rewritten, making T3.9's own "every link resolves" check pass trivially (nothing left to check).

**Recommendation**: State in each synthetic-fixture task that the fixture must be `git add`-ed before the run and `git rm --cached` on cleanup, or add an explicit carve-out for the target spec's own directory. Make T3.9's check assert a non-zero link count.

---

### 7. T4.3's "differing author login" assertion is false against the live PR (Score: 72)

T4.3 and T4.1 claim round 1's tracking comment and its inline review submissions are authored under different bot logins (`claude` vs `claude[bot]`). Live data disagrees: all three submissions and the tracking comment are authored `claude`. The underlying rule (author login is not a test) is sound, but the verification asserts a fixture property that doesn't hold, so passing it literally proves nothing.

**Recommendation**: Restate the check as "the three submissions are attached by the 15-minute window alone, with author login not consulted", noting the logins happen to match on this PR so login-independence is asserted by inspecting the command file rather than by the fixture.

---

### 8. T2.5's claim that spec 0027's `requirements.md` declares zero numbered ids is false (Score: 68)

`specs/0027-box-schema-versioning-and-migrations/requirements.md` declares 18 ids (FR-1…FR-12, NFR-1…NFR-6) in the list-lead-in shape, not zero. This doesn't invalidate T2.5's core NFR-3 budget check, but an implementer sizing the budget from this task will plan for a no-table run and get an 18-row reconciliation plus a full `Read` instead.

**Recommendation**: Correct the fixture line; 0027 becomes a useful third fixture for the list-lead-in declaration shape.

---

### 9. T6.4's "checked out, or merged" instruction only works in the merge form (Score: 65)

Checking out `spec/scoped-lifetime-per-pipeline` removes `.claude/commands/spec/show-me.md` and `specs/0037-show-me/` from the tree, so `/spec:show-me` wouldn't exist to invoke. Only the merge path is viable, and T6.4 doesn't say so — it closes with "restore the working branch afterwards", which describes the checkout path instead.

**Recommendation**: Say "merged into `spec/show-me`" unconditionally, drop "checked out", and replace "restore the working branch" with "reset the merge (`git reset --hard` to the pre-merge sha) afterwards".

---

### 10. A cluster of behavioural checks is "hand-evaluate the rule" — read the prompt file and trust it — for the whole of Phase 4 until the second-to-last task (Score: 62)

T2.3, T4.2, T4.3, T4.4, T4.5, T5.1 and T5.2 all verify by applying the procedure themselves rather than observing the command apply it. Some of this is unavoidable (FR-18 forbids creating a second PR; a rollup can't be synthesised), but T4.2/T4.3/T4.4 are avoidable once the calibration branch is merged — as it happens for T6.4, which currently runs second-to-last.

**Recommendation**: Move the calibration-branch merge to the front of Phase 4 so T4.2/T4.3/T4.4 can assert against a real `/spec:show-me scoped-lifetime` run's output. Keep hand-evaluation only where no fixture can exist (AC-46, AC-48, AC-49, FR-11's collective cases).

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 3 |
| 70-89 (High) | 4 |
| 50-69 (Medium) | 5 |
| 0-49 (Low) | 4 |

**Total findings**: 16
**Findings at or above threshold (60)**: 10

## Below-threshold (recorded, not blocking)

- **[58]** T4.1's skeleton acceptance check says "five" severity markers; the skeleton actually carries eight (round 2's five plus round 3's three). Fix the count.
- **[55]** T3.6 is monolithic — one approval gate covering the mechanical row-set extraction (deterministic per NFR-1) and the judged status-assignment logic (exempted by NFR-1) together across four fixtures. Split into two tasks.
- **[45]** T1.1's acceptance check (`grep -c 'gh ' `) has no expected value to compare against — not executable as written. Replace with a check that must produce no output.
- **[40]** T6.1 places the new README section between `/spec:ralph-implement`'s own documentation and its "Running the unattended loop" continuation. Move it after that section instead.
- **[35]** The Fixtures table still lists `specs/0037-show-me/` "(before this list lands)" as the FR-3 tasks.md-absent case — now stale since tasks.md exists. Replace with `specs/0005-defer-message-on-error/` (already used correctly by T1.5).
- **[32]** The Scope-creep check's claim that every task's `Traces to:` line cites an ADR is false for T6.4 and T6.6 (both end-to-end calibration tasks with no ADR reference). Add the references or soften the claim.

## Verified correct (recorded for the fix pass)

0002-sqs-cleanup, 0031-test_naming_conventions, 0033-pg-advisory-lock-sha256, 0003, 0023, 0005, 0027's byte/task counts, the five `0037*`/four `0057*` ADRs, `release_notes.md`'s absence of a spec-0002 section, `PROMPT.md`/`PROMPT-*.md` tracking status, PR #4282's rollup (28 entries, 26 CheckRun + 2 StatusContext, 5 SUCCESS/22 SKIPPED/1 FAILURE, `license/cla` as SUCCESS StatusContext), PR #4282's comment structure (six finding-free comments, the 29-second two-part design pass, `### Smaller notes`'s three bullets, `### 10. Smaller items`'s five bullets, all eight severity markers, the body-prose trap) all check out exactly as the task list states. Every FR-1…FR-20/NFR-1…NFR-8 and all 54 AC ids are cited somewhere in `tasks.md`.
