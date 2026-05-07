# Review: tasks (round 2) — 0028-box-provisioning-rdd-role-interfaces

**Date**: 2026-05-07
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. Consider addressing lower-scored items.

## Round-1 findings status

| # | Title (round 1) | Score (r1) | Status (r2) |
|---|-----------------|------------|-------------|
| 1 | SpannerBoxMigrationRunner ctor rewire missing — Phase 8.6 grep gate unsatisfiable | 90 | **Fixed** (Phase 7.5 added; ctor takes `IAmABoxMigrationDetectionHelper<SpannerConnection, SpannerTransaction>`; Phase 9.5 wires it through DI) |
| 2 | Phase 0 baseline / phase gates not auditable | 75 | **Fixed** (Phase 0 writes tracked `baseline.md`; preamble mandates phase-gate commit messages quote count delta vs baseline) |
| 3 | F9 / AC4 (open-closed sweep) has no traceable task | 70 | **Fixed** (Phase 12 cross-walks F1..F9; F9 discharge via `sweep-result.md`; Appendix adds AC4 reactive obligation) |
| 4 | Phase 7 backend runner refactor tasks are monolithic | 65 | **Fixed** (Phase 7.1 broken into 7.1a/7.1b/7.1c; same a/b/c structure for 7.2/7.3/7.4; "one commit per hook" inside 7.1b/etc.) |
| 5 | Phase 12 lacks F1..F9 cross-walk to tasks | 60 | **Fixed** (Phase 12 contains explicit F1..F9 enumeration with verifying artefacts; per-AC checklist replaces bulk tick) |

Below-threshold round-1 findings (#6 Phase 8 "Same shape" stubs, #7 bridging-shim behavioural-change framing, #8 Phase 11 fragile ADR line numbers, #9 UoW accepted-and-ignored, #10 baseline drift) — all addressed by the new "Unreleased-branch licence" preamble, the fleshed-out Phase 8 sub-tasks, the Phase 11 grep-by-section-title, and the Phase 0 baseline.md workflow.

## Findings (round 2)

### 1. Phase 9.2/9.3/9.4 DI extension tasks remain one-line stubs (Score: 50)

Phase 9.2 (Postgres), 9.3 (MySQL), 9.4 (SQLite) are each a single bullet — `**TIDY FIRST: AddPostgreSqlOutbox / AddPostgreSqlInbox DI updates**`, with no body, no file path, no validation step. Phase 9.1 (MSSQL) is fully spelled out (file path, four registrations enumerated, connection-name overload note, validation). Phase 9.5 (Spanner) is partially spelled out. Same risk shape as round-1 finding #6 about Phase 8 — uneven task quality across sibling backends. Below threshold so does not block, but worth replicating the 9.1 template for the other three.

**Recommendation**: For 9.2/9.3/9.4 either (a) replicate the 9.1 task body (file path + 4 registrations + connection-name overload note + validation) or (b) compress all four relational backends into a single task with a per-backend checklist.

---

### 2. Phase 7.{2,3,4}b "one commit per hook" sub-task is itself a 7-commit checklist disguised as one bullet (Score: 50)

Each of 7.2b, 7.3b, 7.4b says "Replace each legacy-delegate hook with the cleaned override (one commit per hook)". The MSSQL equivalent 7.1b enumerates the seven hooks explicitly. The Postgres/MySQL/SQLite equivalents do not, so a developer working 7.2b has to back-reference 7.1b to know "one commit per hook" means seven sub-commits. The dependency graph also shows Phase 7.1–7.4 in parallel; if four developers worked these concurrently, three of them would have less guidance than the first.

**Recommendation**: Either (a) replicate the seven-hook enumeration in 7.2b/7.3b/7.4b, or (b) add a one-line back-reference like "see 7.1b for the seven-hook list — same shape applies here".

---

### 3. Phase 11 ADR-section verification still uses brittle naming-grep substitute for diff (Score: 45)

The round-1 finding #8 (Phase 11 manual eyeball without diff record) is partially fixed — the section is referenced by title now, and there is class-name grep verification. However, the verification only catches drift in *class names*; it does not catch drift in *method signatures*, *ctor parameter shapes*, or *interface inheritance edges*. If `RelationalBoxMigrationRunnerBase` ends up taking five ctor params instead of four, the class-name grep still passes while the ADR text drifts silently.

**Recommendation**: Add a second grep step: "grep ADR ctor signatures (`protected RelationalBoxMigrationRunnerBase(...)`) against the shipped `.cs` file and confirm they match parameter-for-parameter".

---

### 4. UoW "accepted-and-ignored" parameter contracts still have no test coverage (Score: 45)

Round-1 finding #9 noted that `SqliteProvisioningUnitOfWork`'s `BeginAsync(lockResource, lockTimeout, ...)` takes parameters it must ignore (no separate lock primitive in SQLite); same shape applies to MySQL UoW's `lockTimeout` slot. Phase 5.4 tests still verify only "BEGIN IMMEDIATE issued, Transaction non-null". No test pins the ignore-contract for `lockResource` and `lockTimeout`.

**Recommendation**: Add one `/test-first` task per UoW where parameters are ignored, asserting parameter values cannot leak into the lifecycle.

---

### 5. Phase 7.1a "Both base orchestration AND legacy delegates coexist for one commit" creates a transient state with two MigrateAsync codepaths (Score: 40)

The 7.1a task says "Both the new base orchestration AND the legacy delegates coexist for one commit". The base ctor in 7.1a wires `(detectionHelper, configuration, lockTimeout ?? default, logger)` — calling `MigrateAsync` on the new instance now goes through the BASE's algorithm, not the legacy one. The "legacy" `MigrateLegacyAsync` is private and never invoked. So the validation "existing 54/54 MSSQL tests stay green" actually exercises the NEW base algorithm via the base-supplied `MigrateAsync`, not the legacy code; the legacy private method is dead code from the moment 7.1a commits. The framing is misleading — it's not "both coexist", it's "base wins, legacy is private dead code transitionally".

**Recommendation**: Rephrase as "Base orchestration becomes the single live path; legacy delegates remain as transitional internal helpers each override calls into for one commit; legacy `MigrateLegacyAsync` is private dead code until 7.1c removes it."

---

### 6. Phase 0 baseline workflow does not commit `baseline.md` separately from the requirements NF2 update (Score: 35)

Phase 0 instructs the developer to update `requirements.md` NF2 in the same commit as `baseline.md` if counts have drifted. The commit message is fixed regardless of whether `requirements.md` was touched. A future reader running `git log --oneline | grep "Phase 0"` cannot tell whether the requirements update happened just from the message.

**Recommendation**: Make the commit message conditional: append "and update NF2 enumeration" only when requirements.md was modified.

---

### 7. Appendix "AC4 reactive obligation" introduces process scope expansion that isn't reflected in the task numbering (Score: 35)

The Appendix says "if implementation surfaces an open-closed sweep candidate that ADR §B.4 missed, the spec scope EXPANDS to fold it in (or document the deferral with reason). Do NOT silently absorb such a candidate as scope creep". This is correct but conflicts with the strict CLAUDE.md change-scope rule ("Do NOT change defaults or make changes beyond what was explicitly requested"). The Appendix gives an explicit escape hatch but a developer might still hesitate.

**Recommendation**: Add a note in the Appendix saying "this is a documented exception to CLAUDE.md's strict change-scope rule, sanctioned by F9/AC4".

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 2 |
| 0-49 (Low) | 5 |

**Total findings**: 7
**Findings at or above threshold (60)**: 0
