# Tasks: 0031 — Test Naming Conventions (BoxProvisioning)

## Preamble

This is a **rename-only refactor**. We are bringing existing BoxProvisioning tests
into line with the project's authoritative naming convention documented in
`.agent_instructions/testing.md`. There are **no new behaviors and no new tests**.

Because nothing is written test-first, the `/test-first` TDD template **does not
apply** and is deliberately not used here. This is a structural ("tidy-first")
change: capture a baseline, rename per project, then verify parity against that
baseline. Test bodies, arrange/act/assert logic, assertions, attributes (beyond the
identifier updates required to keep things compiling), and discovered test counts are
all **preserved exactly**.

There is **no ADR** for this work — design was deliberately marked N/A; `requirements.md`
is the sole spec input. Do not reference an ADR.

### The Convention (target state) — from `.agent_instructions/testing.md`
- **Class name**: `[Behavior]Tests` — PascalCase, ends in `Tests`. The `When_` prefix
  is for method and file names ONLY, never class names.
- **Method name**: `When_[condition]_should_[expected_behavior]` (snake_case).
- **File name**: named after the (happy-path) test method, i.e.
  `When_[condition]_should_[expected_behavior].cs`.
- **One test case per file** preferred; for multi-case files the class describes the
  shared behavior and the file is named after the happy-path method.

### File categories (survey baseline, from requirements.md)
- **CORRECT** (77 files total): class `[Behavior]Tests` AND methods `When_..._should_...`.
  **Leave byte-for-byte untouched.**
- **MIXED** (7 files total): class already `[Behavior]Tests` BUT methods are `Should_...`.
  Rename methods only; do NOT rename the class.
- **WRONG** (111 files total): class `When_..._should_...` AND methods `Should_...`.
  Rename class, methods, and (where the file name does not already conform) the file.

### Rename units (5 in-scope folders → 6 tasks: 1 baseline + 5 renames + 1 final)
| Unit | Folder | Non-conforming |
|------|--------|----------------|
| BoxProvisioning.Tests | `tests/Paramore.Brighter.BoxProvisioning.Tests/` | 2 (WRONG/MIXED) |
| Sqlite | `tests/Paramore.Brighter.Sqlite.Tests/BoxProvisioning/` | 22 (18 WRONG + 4 MIXED) |
| MSSQL | `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/` | 33 WRONG |
| MySQL | `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning/` | 27 (24 WRONG + 3 MIXED) |
| PostgreSQL | `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning/` | 34 WRONG |

> The survey figures are the working baseline. If a rename task discovers an
> additional non-conforming file inside its in-scope folder, treat it as WRONG/MIXED
> per the Definitions and bring it to the CORRECT state (Constraints/Assumptions).

### How to derive `[Behavior]Tests` class names (NFR-6, FR-1)
Choose a **concise PascalCase** name that describes the behavior under test (not the
condition, not the expectation). Strip the `When_`/`Should_` framing and summarise the
unit of behavior. End the name in `Tests`.

**Worked example (from requirements.md):**
- WRONG before: file `When_sqlite_outbox_provisioner_finds_existing_table_without_history_it_should_bootstrap.cs`,
  class `When_sqlite_outbox_provisioner_finds_existing_table_without_history_it_should_bootstrap`,
  method `Should_bootstrap_with_synthetic_history()`.
- After: class `OutboxProvisionerBootstrapTests`;
  method `When_sqlite_outbox_provisioner_finds_existing_table_without_history_it_should_bootstrap()`;
  **file name unchanged** (it already conforms).

For multi-test-case files (FR-4): class = shared behavior (e.g.
`CommandProcessorPostBoxBulkClearAsyncTests`), file = happy-path method
(e.g. `When_bulk_clearing_the_outbox_it_should_dispatch_all_messages.cs`), every
method in the file follows `When_..._should_...`.

### Dependencies
- **T1 (baseline)** runs first and **blocks all renames**.
- **T2–T6 (renames)** each depend only on T1; they are **independent of each other**
  and may run in any order or in parallel.
- **T7 (final verification)** depends on **all** of T2–T6.

### Verification approach
- Every rename task: `dotnet build` clean (AC-6) + test-count parity vs the T1 baseline
  (`dotnet test --list-tests` / discovery count, AC-8).
- Run the full suite to green/red parity vs baseline (AC-7) **when that backend's
  Docker container is available**. `BoxProvisioning.Tests` and `Sqlite` need **no**
  external container. `MSSQL` / `PostgreSQL` / `MySQL` need **Docker**; if the container
  is not available, **explicitly NOTE "suite not run — infra (Docker) unavailable"** in
  the task record — never silently skip.

---

## Tasks

- [x] **T1 — BASELINE: capture pre-change state for all 5 in-scope projects** (blocks: T2–T7) — done 2026-06-09; baseline in `.scratch/baseline.md`. Counts (suite Total): boxprov 111 / sqlite 127 / mssql 198 (13 pre-existing fails, 1 in-scope) / mysql 160 / postgres 191. All containers up.
  - No code changes. Read-only measurement only.
  - For each of the 5 in-scope projects/folders, record to a scratch note:
    - Test-**discovery COUNT** (`dotnet test --list-tests` count, or discovery count).
    - Current **pass / fail / skip** status (the existing baseline — including any
      pre-existing failures/skips; we are not fixing or breaking outcomes).
  - Record which backends' **containers are available now** (BoxProvisioning.Tests and
    Sqlite need none; MSSQL/PostgreSQL/MySQL need Docker). For any backend whose
    container is unavailable, record that its full-suite baseline status is "not run —
    infra unavailable" so the same caveat applies symmetrically before/after.
  - This note is the reference for AC-7 (green/red parity) and AC-8 (count parity).

- [x] **T2 — RENAME: BoxProvisioning.Tests** (depends on: T1 baseline) — done `a3b2bc33d`. 2 WRONG files renamed (class+method; file names already conformed). Build clean; suite 111/111 (parity).
  - Scope: `tests/Paramore.Brighter.BoxProvisioning.Tests/` — 2 non-conforming files
    (WRONG/MIXED); leave all already-CORRECT files untouched (FR-5/AC-5).
  - For each WRONG file: rename class `When_...` → a concise `[Behavior]Tests` PascalCase
    name (FR-1); rename `Should_...` method(s) → `When_..._should_...` (FR-2).
  - For each MIXED file: rename `Should_...` method(s) → `When_..._should_...` only; the
    class already conforms — do not rename it.
  - Rename the file via `git mv` to match its happy-path method where the file name does
    not already conform (FR-3/NFR-4); leave conforming file names as-is.
  - For multi-test-case files: class = shared behavior, file = happy-path method, every
    method `When_..._should_...` (FR-4).
  - Update any references to renamed identifiers — `[Collection]`, `nameof`, base classes,
    partial classes, namespace usages (FR-6).
  - **VERIFY**: `dotnet build` clean (AC-6); test-count parity vs baseline (AC-8); run the
    full suite to green/red parity vs baseline (AC-7) — **no container required** for this
    project, so the suite MUST be run.
  - Commit per project.

- [x] **T3 — RENAME: Sqlite / BoxProvisioning** (depends on: T1 baseline) — done `53520bb84`. 18 WRONG + 4 MIXED renamed (class+ctor+method / method-only). 1 doc-comment cross-ref updated; file-name comment left intact. File names already conformed. Build clean; suite 127/127 (parity).
  - Scope: `tests/Paramore.Brighter.Sqlite.Tests/BoxProvisioning/` — 22 non-conforming
    files (18 WRONG, 4 MIXED); leave already-CORRECT files untouched (FR-5/AC-5).
  - For each WRONG file: rename class `When_...` → concise `[Behavior]Tests` (FR-1);
    rename `Should_...` method(s) → `When_..._should_...` (FR-2).
  - For each MIXED file: rename `Should_...` method(s) → `When_..._should_...` only; class
    already conforms — do not rename it.
  - Rename the file via `git mv` to match its happy-path method where the file name does
    not already conform (FR-3/NFR-4); leave conforming file names as-is. (Note: the
    worked-example file already conforms and keeps its name.)
  - Multi-test-case files: class = shared behavior, file = happy-path method, every method
    `When_..._should_...` (FR-4).
  - Update references to renamed identifiers — `[Collection]`, `nameof`, base classes,
    partial classes, namespace usages (FR-6).
  - **VERIFY**: `dotnet build` clean (AC-6); test-count parity vs baseline (AC-8); run the
    full suite to green/red parity vs baseline (AC-7) — **no container required** for
    Sqlite, so the suite MUST be run.
  - Commit per project.

- [x] **T4 — RENAME: MSSQL / BoxProvisioning** (depends on: T1 baseline) — done `d9947c90b`. 33 WRONG renamed (class→`MsSql[Behavior]Tests` folder-consistent prefix + ctor + method). 1 doc cross-ref updated. File names conformed. Docker available; build clean; suite 185P/13F/198T = exact parity (13 pre-existing fails preserved, 1 in-scope under renamed FQN).
  - Scope: `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/` — 33 WRONG files (0 MIXED);
    leave already-CORRECT files untouched (FR-5/AC-5).
  - For each WRONG file: rename class `When_...` → concise `[Behavior]Tests` (FR-1);
    rename `Should_...` method(s) → `When_..._should_...` (FR-2).
  - Rename the file via `git mv` to match its happy-path method where the file name does
    not already conform (FR-3/NFR-4); leave conforming file names as-is.
  - Multi-test-case files: class = shared behavior, file = happy-path method, every method
    `When_..._should_...` (FR-4).
  - Update references to renamed identifiers — `[Collection]`, `nameof`, base classes,
    partial classes, namespace usages (FR-6).
  - **VERIFY**: `dotnet build` clean (AC-6); test-count parity vs baseline (AC-8); run the
    full suite to green/red parity vs baseline (AC-7) **IF the MSSQL Docker container is
    available**, else explicitly **NOTE "suite not run — MSSQL Docker unavailable"**.
  - Commit per project.

- [x] **T5 — RENAME: MySQL / BoxProvisioning** (depends on: T1 baseline) — done `e9358acb3`. 24 WRONG + 3 MIXED renamed (class→`MySql[Behavior]Tests` + ctor + method / method-only). 1 comment cross-ref updated. File names conformed. Docker available; build clean; suite 160/160 (parity).
  - Scope: `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning/` — 27 non-conforming files
    (24 WRONG, 3 MIXED); leave already-CORRECT files untouched (FR-5/AC-5).
  - For each WRONG file: rename class `When_...` → concise `[Behavior]Tests` (FR-1);
    rename `Should_...` method(s) → `When_..._should_...` (FR-2).
  - For each MIXED file: rename `Should_...` method(s) → `When_..._should_...` only; class
    already conforms — do not rename it.
  - Rename the file via `git mv` to match its happy-path method where the file name does
    not already conform (FR-3/NFR-4); leave conforming file names as-is.
  - Multi-test-case files: class = shared behavior, file = happy-path method, every method
    `When_..._should_...` (FR-4).
  - Update references to renamed identifiers — `[Collection]`, `nameof`, base classes,
    partial classes, namespace usages (FR-6).
  - **VERIFY**: `dotnet build` clean (AC-6); test-count parity vs baseline (AC-8); run the
    full suite to green/red parity vs baseline (AC-7) **IF the MySQL Docker container is
    available**, else explicitly **NOTE "suite not run — MySQL Docker unavailable"**.
  - Commit per project.

- [x] **T6 — RENAME: PostgreSQL / BoxProvisioning** (depends on: T1 baseline) — done `24c4f168d`. 34 WRONG renamed (class→`PostgreSql[Behavior]Tests` + ctor + method). 2 comment cross-refs updated. File names conformed. Docker available; build clean; suite 191/191 (parity).
  - Scope: `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning/` — 34 WRONG files
    (0 MIXED); leave already-CORRECT files untouched (FR-5/AC-5).
  - For each WRONG file: rename class `When_...` → concise `[Behavior]Tests` (FR-1);
    rename `Should_...` method(s) → `When_..._should_...` (FR-2).
  - Rename the file via `git mv` to match its happy-path method where the file name does
    not already conform (FR-3/NFR-4); leave conforming file names as-is.
  - Multi-test-case files: class = shared behavior, file = happy-path method, every method
    `When_..._should_...` (FR-4).
  - Update references to renamed identifiers — `[Collection]`, `nameof`, base classes,
    partial classes, namespace usages (FR-6).
  - **VERIFY**: `dotnet build` clean (AC-6); test-count parity vs baseline (AC-8); run the
    full suite to green/red parity vs baseline (AC-7) **IF the PostgreSQL Docker container
    is available**, else explicitly **NOTE "suite not run — PostgreSQL Docker unavailable"**.
  - Commit per project.

- [x] **T7 — FINAL VERIFICATION: whole-spec sign-off** (depends on: T2, T3, T4, T5, T6) — done 2026-06-09; record in `.scratch/T7-signoff.md`. AC-1..10 all PASS: 0 WRONG + 0 MIXED across 5 folders; 118 files changed (=non-conforming set), 0 renames (all file names already conformed), 0 outside-scope/src/analyzer/CI/agent-instruction changes, 0 `Assert.` lines touched; all 5 build clean; count + green/red parity vs T1 (MSSQL keeps its 13 pre-existing fails, 1 under renamed FQN). All containers available — no infra gaps.
  - **Zero non-conforming remain (AC-9)**: inspect all 5 in-scope folders and confirm
    **0 WRONG + 0 MIXED** files remain — every class is `[Behavior]Tests`, every test
    method is `When_..._should_...`, and every file is named after a `When_..._should_...`
    method. (No test method beginning with `Should_` remains anywhere in scope — AC-2.)
  - **CORRECT files untouched (AC-5/FR-5/NFR-1)**: confirm via `git diff` against the
    pre-work commit that every CORRECT file (e.g.
    `When_box_table_name_is_null_or_empty_is_called_it_should_report_emptiness.cs`) shows
    no rename and no content change.
  - **History preserved (AC-3/NFR-4)**: spot-check `git log --follow` on renamed files to
    confirm continuous history through the `git mv`.
  - **Scope containment (AC-10 / Out of Scope)**: confirm the full diff shows
    (a) no file **outside** the 5 in-scope folders modified; (b) no test body, assertion,
    arrange/act logic, or covered behavior changed (only identifier/file renames and the
    FR-6 reference updates); (c) no analyzer / Roslyn analyzer, no CI guard or lint step,
    no pre-commit hook, and no change to `.agent_instructions/testing.md` or any other
    agent-instruction file has been added or modified; (d) no production (non-test) code
    changed.
  - **Build & parity recap (AC-6/AC-7/AC-8/NFR-2/NFR-3/NFR-5)**: confirm every in-scope
    project still builds clean, discovery counts match T1 baseline for all 5 projects, and
    green/red status matches baseline for every project whose container was available
    (carry forward and restate any "suite not run — infra unavailable" notes from
    T4/T5/T6 so the gap is explicit, not silent).
  - This is the whole-spec sign-off; no code changes here beyond any reference fixes a
    discrepancy forces (which would loop back to the relevant rename task).

---

## Coverage cross-reference

### Functional Requirements
| Req | Covered by |
|-----|-----------|
| FR-1 (rename WRONG classes → `[Behavior]Tests`; MIXED classes untouched) | T2, T3, T4, T5, T6; verified T7 (AC-9) |
| FR-2 (rename `Should_...` methods → `When_..._should_...`, WRONG + MIXED) | T2, T3, T4, T5, T6; verified T7 (AC-9, AC-2) |
| FR-3 (rename non-conforming file names via `git mv`; conforming names kept) | T2, T3, T4, T5, T6; verified T7 (AC-3) |
| FR-4 (multi-test-case files: class=behavior, file=happy-path, methods `When_...`) | T2, T3, T4, T5, T6; verified T7 (AC-9) |
| FR-5 (leave CORRECT files byte-for-byte untouched) | T2, T3, T4, T5, T6 (explicit "leave CORRECT untouched"); verified T7 (AC-5) |
| FR-6 (preserve/update references to renamed identifiers) | T2, T3, T4, T5, T6; verified T7 (AC-6) |

### Non-functional Requirements
| Req | Covered by |
|-----|-----------|
| NFR-1 (behavior preservation — pure refactor) | All rename tasks (rename-only scope); verified T7 (AC-10) |
| NFR-2 (compilation & green/red parity) | `dotnet build` + suite-run VERIFY in T2–T6; baseline in T1; recap T7 (AC-6, AC-7) |
| NFR-3 (test-count preservation) | count-parity VERIFY in T2–T6; baseline in T1; recap T7 (AC-8) |
| NFR-4 (git history via `git mv`) | T2–T6 (file rename step); verified T7 (AC-3, `git log --follow`) |
| NFR-5 (consistency — all 252 files CORRECT) | Cumulative T2–T6; verified T7 (AC-9) |
| NFR-6 (readability of derived `[Behavior]Tests` names) | "How to derive" note + class-rename step in T2–T6 |

### Acceptance Criteria
| AC | Covered by |
|----|-----------|
| AC-1 (WRONG class → `[Behavior]Tests`) | T2–T6 (FR-1 step); verified T7 |
| AC-2 (methods → `When_..._should_...`; no `Should_` method remains) | T2–T6 (FR-2 step); verified T7 |
| AC-3 (non-conforming files `git mv`-renamed, history follows; conforming names kept) | T2–T6 (FR-3 step); verified T7 |
| AC-4 (multi-test-case files structured per convention) | T2–T6 (FR-4 step); verified T7 (AC-9) |
| AC-5 (CORRECT files show no diff) | T2–T6 ("leave untouched"); verified T7 |
| AC-6 (projects compile, all references resolve) | `dotnet build` VERIFY in T2–T6; recap T7 |
| AC-7 (green/red status matches baseline) | T1 baseline + suite-run VERIFY in T2–T6 (with infra-skip notes); recap T7 |
| AC-8 (test count matches baseline) | T1 baseline + count-parity VERIFY in T2–T6; recap T7 |
| AC-9 (0 WRONG + 0 MIXED across all 5 folders) | T7 |
| AC-10 (no out-of-scope file/behavior/analyzer/CI/agent-instruction change) | T7 |

### Scope-creep check
No task introduces work that does not trace to a requirement. T1 (baseline) and T7
(final verification) exist solely to enable AC-7/AC-8 parity checks and to assert
AC-5/AC-9/AC-10 — they add no new behavior, no tests, no analyzer/CI/hook, and no
production code. **No scope creep detected.**

### Gap check
Every FR (FR-1..FR-6), NFR (NFR-1..NFR-6), and AC (AC-1..AC-10) maps to at least one
task. **No coverage gaps detected.**
