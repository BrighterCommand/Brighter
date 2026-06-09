# Resume State — Spec 0031 test_naming_conventions

**Last updated:** 2026-06-09
**Branch:** `test_naming`
**Spec dir:** `specs/0031-test_naming_conventions/`  ·  `specs/.current-spec` = `0031-test_naming_conventions`
**Issue:** #4157 (Database Migration Has Bad Test Naming — Not Convention)
**ADR:** none — **Design marked N/A** (this is a correction to an already-documented convention, not a design decision)

## What this is

Rename the BoxProvisioning tests to follow Brighter's authoritative naming convention in
`.agent_instructions/testing.md`:
- **Class**: `[Behavior]Tests` (PascalCase, ends `Tests`; never `When_`)
- **Method**: `When_[condition]_should_[expected_behavior]` (snake_case)
- **File**: named after the happy-path method, `When_..._should_....cs`

Pure rename/refactor — **no new tests, no behaviour/assertion/logic changes, no test-count changes.**
`/test-first` does NOT apply (nothing is written test-first). Verification = build + count parity
+ green/red parity (run suite where the backend container is available; note explicitly where not).

## Where we are in the workflow

Issue → **Requirements ✅** → **Design N/A** → **Tasks ✅ approved** → **Implement ✅ DONE** → **Verify ✅ DONE** → Review (next)

> **STATUS 2026-06-09: IMPLEMENTATION COMPLETE.** All 7 tasks (T1 baseline + T2–T6 renames + T7 sign-off)
> done & committed on branch `test_naming`. 118 non-conforming files renamed to convention (0 WRONG +
> 0 MIXED remain). Commits: T2 `a3b2bc33d` · T3 `53520bb84` · T4 `d9947c90b` · T5 `e9358acb3` ·
> T6 `24c4f168d`. Verification record: `specs/0031-test_naming_conventions/.scratch/T7-signoff.md`.
> All AC-1..10 PASS; build clean on all 5 projects; count + green/red parity vs baseline (MSSQL keeps its
> 13 pre-existing failures, 1 now under a renamed FQN). **Remaining: `/spec:review code` then PR / merge.**

| Phase | Status |
|---|---|
| Requirements (`requirements.md`) | ✅ written + approved (`.requirements-approved`); FR-1..6, NFR-1..6, AC-1..10. Issue commented. |
| Design (ADR) | ⏭️ N/A by decision (`.design-approved` marker records why) |
| Tasks (`tasks.md`) | ✅ drafted + **approved** (`.tasks-approved`) 2026-06-09 — 8 tasks (T1 baseline · T2–T6 rename-per-project · T7 final). Full FR/NFR/AC coverage map, no gaps/creep. |
| `/spec:implement` | 🔄 **NEXT — start at T1 (baseline)** |

## Scope (survey baseline — T1 will re-confirm exact counts)

5 in-scope folders, 252 test files; **118 need correction** (111 WRONG + 7 MIXED). 77 CORRECT files left untouched.

| Unit (task) | Folder | Non-conforming |
|---|---|---|
| T2 BoxProvisioning.Tests | `tests/Paramore.Brighter.BoxProvisioning.Tests/` | 2 |
| T3 Sqlite | `tests/Paramore.Brighter.Sqlite.Tests/BoxProvisioning/` | 22 (18 WRONG + 4 MIXED) |
| T4 MSSQL | `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/` | 33 WRONG |
| T5 MySQL | `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning/` | 27 (24 WRONG + 3 MIXED) |
| T6 PostgreSQL | `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning/` | 34 WRONG |

**Categories:** CORRECT = good class + good method (leave untouched). MIXED = good class, `Should_` method
(rename method only). WRONG = `When_` class + `Should_` method (rename class + method + file-if-needed).

## Container needs for verification

- **No container**: BoxProvisioning.Tests (T2), Sqlite (T3) — suite MUST be run.
- **Docker required**: MSSQL (T4), PostgreSQL (T5), MySQL (T6) — run if available, else NOTE "suite not run — infra unavailable" (never silent-skip).

## Key implementation notes

- Class-name derivation: concise PascalCase describing behaviour-under-test, strip `When_`/`Should_`, end in `Tests`.
  Worked example: class `When_sqlite_outbox_provisioner_finds_existing_table_without_history_it_should_bootstrap`
  → `OutboxProvisionerBootstrapTests`; method `Should_bootstrap_with_synthetic_history()`
  → `When_sqlite_outbox_provisioner_finds_existing_table_without_history_it_should_bootstrap()`; file already conforms → keep name.
- MIXED files: rename `Should_` method(s) ONLY; class already conforms.
- File renames via `git mv` (preserve history); leave already-conforming file names as-is.
- Update all references to renamed identifiers: `[Collection]`, `nameof`, base/partial classes, namespace usages.
- Commit per project (T2–T6).
- ⚠️ `.gitignore` `*.sqlite` quirk: new files under `*.Sqlite` dirs need `git add -f` (see CLAUDE.md). Test dirs end in `.Tests` so NOT affected — but watch the Sqlite rename commit.

## Resume steps

1. `git branch --show-current` → expect `test_naming`; `cat specs/.current-spec` → `0031-test_naming_conventions`.
2. If tasks not yet reviewed: run `/spec:review tasks`, address findings, then `/spec:approve tasks`.
3. `/spec:implement` → start at **T1 (baseline)**: record per-project discovery count + pass/fail/skip + container availability to a scratch note.
4. Then T2–T6 (any order/parallel; each: rename → build → count parity → suite-where-available → commit).
5. Finally T7 whole-spec sign-off (0 WRONG + 0 MIXED, CORRECT untouched, scope containment AC-10).

## Test run command (per project, e.g.)

```bash
dotnet test tests/Paramore.Brighter.BoxProvisioning.Tests/ --framework net9.0 -q
```
