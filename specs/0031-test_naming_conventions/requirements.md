# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Linked Issue**: #4157

## Problem Statement

As a maintainer of the Brighter codebase, I want all BoxProvisioning tests to follow the project's authoritative test-naming convention (defined in `.agent_instructions/testing.md`), so that test classes, methods, and files are consistent, self-describing, and discoverable across all database backends, and so that the test suite reflects the conventions we ask all contributors (human and agent) to follow.

Currently, a large number of BoxProvisioning tests use a non-conforming ("bastardized") naming schema, presumably inherited from a generic training-set convention rather than the project convention. In the non-conforming schema the test class is named `When_[condition]_should_[expected_behavior]` and the individual test method is named `Should_[expectation]`. This is the inverse of the project convention, which reserves the `When_` prefix for method and file names and uses `[Behavior]Tests` for class names. The inconsistency makes the test suite harder to read and navigate, and contradicts the documented standard.

## Proposed Solution

Rename and, where necessary, restructure the non-conforming BoxProvisioning tests so that every BoxProvisioning test conforms to the canonical convention:

- Test classes are named `[Behavior]Tests` (PascalCase, ending in `Tests`).
- Test methods are named `When_[condition]_should_[expected_behavior]` (snake_case with underscores).
- Test files are named after the (happy-path) test method, i.e. `When_[condition]_should_[expected_behavior].cs`.

This is a pure rename/restructure of identifiers and file names. No test behavior, assertion, arrange/act logic, or test count changes. Files that already conform are left completely untouched. After the change, each affected backend's BoxProvisioning test suite still compiles and produces the same pass/fail results as before.

This work is rename-only. It deliberately does not introduce any mechanism to prevent recurrence (no analyzer, no CI guard, no agent-instruction changes); those are explicitly out of scope (see Out of Scope).

## Requirements

### Definitions

To remove ambiguity, the following terms are used throughout this document.

- **The Convention** (authoritative, from `.agent_instructions/testing.md`):
  - **Method name**: `When_[condition]_should_[expected_behavior]` (snake_case, underscore-separated).
  - **Class name**: `[Behavior]Tests` — PascalCase, ending in `Tests`. The `When_` prefix is for method and file names ONLY, never class names. Examples: `PipelineValidatorErrorAggregationTests`, `CommandProcessorPostBoxBulkClearAsyncTests`.
  - **File name**: named after the (happy-path) test method, i.e. `When_[condition]_should_[expected_behavior].cs`.
  - **One test case per file** is preferred. When multiple test cases share a file (e.g. shared complex setup), the class name describes the behavior shared across all tests and the file is named after the happy-path test method.
- **CORRECT file**: a file whose class is named `[Behavior]Tests` AND whose test method(s) are named `When_..._should_...`. (Survey: 77 files.)
- **MIXED file**: a file whose class IS correctly named `[Behavior]Tests` but whose test method(s) are named `Should_...` instead of `When_..._should_...`. (Survey: 7 files.)
- **WRONG file**: a file whose class is named `When_..._should_...` AND whose test method(s) are named `Should_...`. (Survey: 111 files.)
- **In-scope folders** (the only locations this work touches):
  1. `tests/Paramore.Brighter.BoxProvisioning.Tests/` (48 files)
  2. `tests/Paramore.Brighter.Sqlite.Tests/BoxProvisioning/` (36 files)
  3. `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/` (60 files)
  4. `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning/` (49 files)
  5. `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning/` (59 files)

  Total in-scope test files: 252. Total files requiring correction: 118 (111 WRONG + 7 MIXED).

### Functional Requirements

**FR-1: Rename non-conforming test classes to `[Behavior]Tests`.**
Every WRONG file's test class must be renamed from the `When_..._should_...` form to a concise PascalCase `[Behavior]Tests` name that describes the behavior under test. MIXED files (whose class is already correct) must not have their class renamed by this requirement.
- *Example*: In `When_sqlite_outbox_provisioner_finds_existing_table_without_history_it_should_bootstrap.cs`, the class `When_sqlite_outbox_provisioner_finds_existing_table_without_history_it_should_bootstrap` is renamed to a behavior-describing class such as `OutboxProvisionerBootstrapTests`.

**FR-2: Rename non-conforming test methods to `When_[condition]_should_[expected_behavior]`.**
Every test method named `Should_...` in any WRONG or MIXED file must be renamed to the `When_..._should_...` form, preserving the meaning of the original condition and expectation.
- *Example (WRONG file)*: method `Should_bootstrap_with_synthetic_history()` is renamed to `When_sqlite_outbox_provisioner_finds_existing_table_without_history_it_should_bootstrap()`.
- *Example (MIXED file)*: a file with class `BoxTableNameIsNullOrEmptyTests` containing a method `Should_return_true_for_null()` has that method renamed to e.g. `When_box_table_name_is_null_or_empty_is_called_with_null_it_should_return_true()`.

**FR-3: Rename test files that do not match the method-name convention.**
Where a WRONG or MIXED file's name does not already follow `When_[condition]_should_[expected_behavior].cs`, the file is renamed to match its (happy-path) test method. File renames use `git mv` so that git history is preserved. Where a file name already matches the convention (e.g. the WRONG example file `When_sqlite_outbox_provisioner_finds_existing_table_without_history_it_should_bootstrap.cs`), the file name itself is left unchanged even though its class/method are corrected.
- *Example*: a WRONG file misnamed `Should_bootstrap_with_synthetic_history.cs` is `git mv`-renamed to `When_sqlite_outbox_provisioner_finds_existing_table_without_history_it_should_bootstrap.cs`.

**FR-4: Handle multiple-test-case files per the convention.**
For any in-scope file that contains more than one test case sharing setup, the class is named after the shared behavior (`[Behavior]Tests`) and the file is named after the happy-path test method. Every method in such a file follows `When_..._should_...`.
- *Example*: a file containing several related bulk-clear tests sharing a constructor uses a class such as `CommandProcessorPostBoxBulkClearAsyncTests` and is filed under the happy-path method's name, e.g. `When_bulk_clearing_the_outbox_it_should_dispatch_all_messages.cs`.

**FR-5: Leave already-conforming files untouched.**
Every CORRECT file (77 files) must be byte-for-byte unchanged by this work — no class rename, no method rename, no file rename, no formatting churn.
- *Example*: `When_box_table_name_is_null_or_empty_is_called_it_should_report_emptiness.cs` (class `BoxTableNameIsNullOrEmptyTests`, method `When_box_table_name_is_null_or_empty_is_called_with_null_it_should_return_true()`) is committed without modification.

**FR-6: Preserve all references to renamed identifiers.**
Any reference to a renamed class or method (e.g. `[Collection]` attributes referencing class names, base-class references, partial-class declarations, cross-file references, namespace usages) must be updated so the suite still compiles. No dangling references to old names may remain.
- *Example*: if class `When_sqlite_..._bootstrap` is referenced by a test collection definition or a `nameof(...)`, that reference is updated to the new `OutboxProvisionerBootstrapTests` name.

### Non-functional Requirements

- **NFR-1 (Behavior preservation)**: The change is a pure refactor. No test body, assertion, arrange/act code, attribute (beyond identifier updates required by FR-6), or covered behavior changes.
- **NFR-2 (Compilation & test parity)**: After the change, each in-scope project compiles, and the BoxProvisioning test suite for each backend produces the same green/red status (same passing tests, same failing/skipped tests) as before the rename.
- **NFR-3 (Test-count preservation)**: The number of discovered/executed test cases per in-scope project is identical before and after the change. No tests are added or removed by renaming.
- **NFR-4 (Git history preservation)**: File renames are performed with `git mv` so that history follows the file where practical.
- **NFR-5 (Consistency)**: All 252 in-scope files end in the CORRECT state; the resulting names are consistent with the examples cited in `.agent_instructions/testing.md`.
- **NFR-6 (Readability of derived names)**: Newly chosen `[Behavior]Tests` class names are concise, PascalCase, and meaningfully describe the behavior under test.

### Constraints and Assumptions

- The convention defined in `.agent_instructions/testing.md` is authoritative and is the target for all renames.
- Work is bounded to exactly the five in-scope folders listed under Definitions; no files outside those folders are modified.
- Survey figures (252 in-scope files; 77 CORRECT, 7 MIXED, 111 WRONG; 118 to correct) are taken as the working baseline; if implementation discovers an additional non-conforming file within the in-scope folders, it is treated as WRONG or MIXED per the Definitions and brought into the CORRECT state.
- The existing pass/fail status of tests (including any pre-existing failures or skips) is the baseline; this work does not attempt to fix or break test outcomes.
- Test frameworks, target frameworks, project files, and build configuration are unchanged.

### Out of Scope

- **Recurrence prevention of any kind**: no naming-convention analyzer/Roslyn analyzer, no CI guard or lint step, no pre-commit hook, and no changes to `.agent_instructions/testing.md` or any other agent instructions.
- **Any test outside the BoxProvisioning scope**: tests elsewhere in the five listed projects but NOT under a `BoxProvisioning` folder, and tests in any other project, are out of scope. (Note: `tests/Paramore.Brighter.BoxProvisioning.Tests/` is entirely in scope; the four `{RDBMS}.Tests` projects are in scope ONLY under their `BoxProvisioning/` subfolders.)
- **Any behavior or logic change to tests**: no new assertions, no changed assertions, no added/removed test cases, no changes to setup/teardown logic beyond identifier renames required to compile.
- **Production (non-test) code changes** of any kind.

## Acceptance Criteria

**AC-1 (maps FR-1)**: Given a WRONG file with a class named `When_..._should_...`, When the rename work is applied, Then the class is named `[Behavior]Tests` (PascalCase, ends in `Tests`, no `When_`/`Should_` prefix). Example: `When_sqlite_outbox_provisioner_finds_existing_table_without_history_it_should_bootstrap` becomes a `[Behavior]Tests` name such as `OutboxProvisionerBootstrapTests`.

**AC-2 (maps FR-2)**: Given a WRONG or MIXED file with a method named `Should_bootstrap_with_synthetic_history()`, When the rename work is applied, Then the method is named in the `When_..._should_...` form, e.g. `When_sqlite_outbox_provisioner_finds_existing_table_without_history_it_should_bootstrap()`, and no test method beginning with `Should_` remains in any in-scope file.

**AC-3 (maps FR-3, NFR-4)**: Given a WRONG or MIXED file whose file name does not match `When_[condition]_should_[expected_behavior].cs`, When the rename work is applied, Then the file is renamed via `git mv` to match its happy-path method name and `git log --follow` shows continuous history; And given a file whose name already matches the convention, Then its file name is unchanged.

**AC-4 (maps FR-4)**: Given an in-scope file containing multiple test cases that share setup, When the rename work is applied, Then the class is named for the shared behavior (`[Behavior]Tests`), the file is named after the happy-path method, and every method in the file follows `When_..._should_...`.

**AC-5 (maps FR-5, NFR-1)**: Given a CORRECT file (e.g. `When_box_table_name_is_null_or_empty_is_called_it_should_report_emptiness.cs`), When the rename work is applied, Then `git diff` shows no change to that file (no rename, no content change).

**AC-6 (maps FR-6, NFR-2)**: Given the renames are applied, When each in-scope project is built, Then it compiles with no errors and no unresolved references to old class or method names (e.g. `[Collection]`, `nameof`, base-class references all resolve).

**AC-7 (maps NFR-2)**: Given the renames are applied, When the BoxProvisioning test suite for each of the five in-scope projects/folders is run, Then the set of passing and failing/skipped tests is identical to the pre-change baseline for that project.

**AC-8 (maps NFR-3)**: Given the renames are applied, When test discovery is run per in-scope project, Then the executed test count for each project equals the pre-change count for that project (no tests added or removed).

**AC-9 (maps NFR-5, FR-1, FR-2, FR-3)**: Given the work is complete, When all 252 in-scope files are inspected, Then every file is in the CORRECT state (class `[Behavior]Tests`, methods `When_..._should_...`, file named after a `When_..._should_...` method), i.e. 0 WRONG and 0 MIXED files remain.

**AC-10 (maps Out of Scope)**: Given the work is complete, When the diff is reviewed, Then no analyzer, CI guard, hook, or agent-instruction file has been added or modified; And no file outside the five in-scope folders has been modified; And no test body, assertion, or covered behavior has changed.

## Additional Context

- Authoritative convention source: `.agent_instructions/testing.md` (verified to state the method/class/file rules and to cite `PipelineValidatorErrorAggregationTests` and `CommandProcessorPostBoxBulkClearAsyncTests` as example class names).
- The file counts in the Definitions section were confirmed against the working tree on 2026-06-09: `Paramore.Brighter.BoxProvisioning.Tests` = 48, `Sqlite.Tests/BoxProvisioning` = 36, `MSSQL.Tests/BoxProvisioning` = 60, `MySQL.Tests/BoxProvisioning` = 49, `PostgresSQL.Tests/BoxProvisioning` = 59 (total 252). A spot check of `Sqlite.Tests/BoxProvisioning` found 18 classes named `When_...` (matching the WRONG count for that folder) and 21 files containing `Should_` methods (WRONG + MIXED), consistent with the survey.
- Worked correct/wrong example pair (for implementers deriving new names):
  - CORRECT model: file `When_box_table_name_is_null_or_empty_is_called_it_should_report_emptiness.cs`, class `BoxTableNameIsNullOrEmptyTests`, method `When_box_table_name_is_null_or_empty_is_called_with_null_it_should_return_true()`.
  - WRONG before: file `When_sqlite_outbox_provisioner_finds_existing_table_without_history_it_should_bootstrap.cs`, class `When_sqlite_outbox_provisioner_finds_existing_table_without_history_it_should_bootstrap`, method `Should_bootstrap_with_synthetic_history()`. After: class `OutboxProvisionerBootstrapTests`, method `When_sqlite_outbox_provisioner_finds_existing_table_without_history_it_should_bootstrap()`, file name unchanged (already conforms).
- Technical design decisions (e.g. the precise algorithm for deriving `[Behavior]Tests` names, batching/sequencing of renames per backend, verification tooling) belong in an ADR under `docs/adr/`, not in this requirements document.
