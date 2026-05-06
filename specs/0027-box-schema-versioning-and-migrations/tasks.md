# Tasks: Box Schema Versioning and Migrations (Spec 0027)

> **ADR**: [0057-box-schema-versioning-and-migrations.md](../../docs/adr/0057-box-schema-versioning-and-migrations.md)
> **Requirements**: [requirements.md](requirements.md)
> **Archaeology**: [README.md](README.md)

## Conventions

- TDD is mandatory. Every TEST task must use `/test-first <behavior>` and wait for approval before implementation.
- Per-backend docker images: tests live under `tests/Paramore.Brighter.{Backend}.Tests/BoxProvisioning/` and run against the docker-compose databases started by `dockercompose.*.yml`.
- File/test names follow the project GWT convention: `When_{condition}_should_{behavior}.cs`.
- Each TEST + IMPLEMENT task is a single combined unit — do **not** split into separate test/implement tasks.
- **Drift-test plumbing** (Tasks 1.1 / 2.1 / 3.1 / 4.1): the shared `DdlColumnExtractor` + `QuoteStyle` types live in the dedicated `tests/Paramore.Brighter.BoxProvisioning.Tests/` project (Task 0.4). Before writing any per-backend drift test, add `<ProjectReference Include="..\Paramore.Brighter.BoxProvisioning.Tests\Paramore.Brighter.BoxProvisioning.Tests.csproj" />` to the per-backend test project's csproj (one-time setup per backend) and `using Paramore.Brighter.BoxProvisioning.Tests.Drift;` to the test file.

## Risk-mitigating task ordering

This ordering de-risks the spec by proving the model on MSSQL first (reference implementation), surfacing the drift-detection test early so columns-vs-migrations divergence fails at CI from the start, and deferring Spanner rework until the shared abstractions are stable.

1. **Phase 0** — shared groundwork: interface extension (0.1/0.2), existing V1 call-site updates across 10 files **including Spanner bridge** (0.3), retarget existing hard-coded `MigrationVersion = 1` tests to `V_latest` (0.3a), drift-detection infrastructure (0.4)
2. **Phase 1** — MSSQL reference (outbox V1..V7 + inbox V1..V2 + three-path runner + discriminator gate + concurrent-bootstrap for outbox AND inbox + whole-chain rollback + NFR-3 timing)
3. **Phase 2** — Postgres (outbox V1..V7 + inbox V1-only + three-path runner + discriminator gate + concurrent-bootstrap + whole-chain rollback)
4. **Phase 3** — MySQL (outbox V1..V7 + inbox V1..V2 + three-path runner with per-migration commit + discriminator gate + concurrent-bootstrap; mid-chain-failure recovery already covered by 3.4)
5. **Phase 4** — SQLite (outbox V1..V7 + inbox V1..V2 + `BEGIN IMMEDIATE` + `IdempotencyCheckSql` path + discriminator gate + concurrent-bootstrap + whole-chain rollback + AC-6 split into its own task)
6. **Phase 5** — Spanner degenerate runner rework: deletes Spanner migration files (closes compile-bridge from 0.3); runner holds `V_latest` as constant; closes spec 0023 R4
7. **Phase 6** — Cross-backend payload-mode-mismatch tests (closes spec 0023 R5; all four validators already exist)
8. **Phase 7** — Docs + release notes (`.agent_instructions/box_provisioning.md`; `release_notes.md` at repo root)

Spec 0023 findings closed out as side-effects:
- **R2 (TOCTOU race)** — closed by TOCTOU re-check inside advisory lock added in the three-path runner of every backend (Phase 1–4).
- **R4 (Spanner history INSERT race)** — closed in Phase 5.
- **R5 (payload-mode tests MSSQL-only)** — closed in Phase 6.

---

## Phase 0: Shared groundwork

> **⚠ Single-commit constraint (Phase 0)**: Tasks **0.1, 0.2, 0.3, and 0.3a MUST land as a single commit**. The interface extension in 0.1 is source-breaking, so committing 0.1 alone leaves the build red across every consuming project; committing 0.3 without 0.3a leaves the test suite red. Task **0.4 may follow as a separate commit** — its drift-detection infrastructure (`DdlColumnExtractor` + per-backend test) compiles and runs independently of 0.1/0.2/0.3/0.3a. Per ADR 0057 line 367 ("Shared groundwork (one commit)").

### Task 0.1: Extend `IAmABoxMigration` with logical-column, source-reference, and SQLite idempotency members

- [x] **IMPLEMENT: Add `LogicalColumns`, `SourceReference`, `IdempotencyCheckSql` to `IAmABoxMigration`**
  - File: `src/Paramore.Brighter.BoxProvisioning/IAmABoxMigration.cs`
  - Add `ISet<string> LogicalColumns { get; }` as required member (source-breaking — acknowledged in ADR 0057 "Consequences → Negative"). **Use `ISet<string>` not `IReadOnlySet<string>`**: `IReadOnlySet<T>` is .NET 5+ and unavailable on `netstandard2.0` (per requirements C-5); `ISet<string>` provides the `Contains`/`IsSupersetOf` semantics detection needs, with read-only-by-convention enforcement (implementations populate once and never mutate)
  - Add `string? SourceReference { get; }` as required nullable member
  - Add `string? IdempotencyCheckSql { get; }` as required nullable member (null on MSSQL/Postgres/MySQL; non-null on SQLite V2+)
  - XML-doc each member describing its role per ADR 0057 §4
  - Expect every existing `BoxMigration` instantiation in the repo to break — they are fixed in Task 0.2 and Task 0.3
  - No tests — this is a pure interface extension; behaviour tests live with the consumers in later phases

### Task 0.2: Extend `BoxMigration` record with the three new parameters

- [x] **IMPLEMENT: Add `LogicalColumns`, `SourceReference`, `IdempotencyCheckSql` to `BoxMigration` record**
  - File: `src/Paramore.Brighter.BoxProvisioning/BoxMigration.cs`
  - Append three new positional parameters to the record, matching the interface order from Task 0.1
  - `SourceReference` and `IdempotencyCheckSql` default to `null`; `LogicalColumns` has no default (required)
  - Update XML doc-comments for the new parameters
  - No tests — verified by consumers compiling in Task 0.3 and Phases 1–4

### Task 0.3: Update existing V1 call sites to supply new parameters

- [x] **IMPLEMENT: Update each `*OutboxMigrations.All` / `*InboxMigrations.All` V1 entry with the new parameters**
  - Files affected (four relational backends × two box types + Spanner × two box types = 10 files):
    - `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlOutboxMigrations.cs`
    - `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlInboxMigrations.cs`
    - `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlOutboxMigrations.cs`
    - `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlInboxMigrations.cs`
    - `src/Paramore.Brighter.BoxProvisioning.MySql/MySqlOutboxMigrations.cs`
    - `src/Paramore.Brighter.BoxProvisioning.MySql/MySqlInboxMigrations.cs`
    - `src/Paramore.Brighter.BoxProvisioning.Sqlite/SqliteOutboxMigrations.cs`
    - `src/Paramore.Brighter.BoxProvisioning.Sqlite/SqliteInboxMigrations.cs`
    - `src/Paramore.Brighter.BoxProvisioning.Spanner/SpannerOutboxMigrations.cs` — Phase 5 deletes this file; during Phase 0 it is kept compilable with a bridge value
    - `src/Paramore.Brighter.BoxProvisioning.Spanner/SpannerInboxMigrations.cs` — same bridge treatment; Phase 5 deletes
  - Each V1 entry passes `LogicalColumns` = the baseline column set:
    - Outbox: `{ MessageId, Topic, MessageType, Timestamp, HeaderBag, Body }`
    - Inbox: `{ CommandId, CommandType, CommandBody, Timestamp }` — **except Postgres inbox**, which also includes `ContextKey` in its V1 baseline per ADR §1 (Postgres inbox was born with it)
    - **Spanner bridge**: `LogicalColumns = new HashSet<string>()` (empty) with a `// TODO(spec-0027 Phase 5): file deleted — runner no longer uses migration list` comment. Empty-set bridge exists solely to keep the build green between Phase 0 and Phase 5; Phase 5.3 deletes both Spanner migration files entirely.
  - **V1.UpScript stays the current full `*Builder.GetDDL(...)` permanently** (per ADR §3 "Why V1 does not need to be idempotent" — V1 is the fresh-install fast path, executed only when the table doesn't exist). Only `V1.LogicalColumns` is the 6-column baseline (used for detection in Phase 1–4). V1.UpScript is **never** rewritten to a 2015-baseline DDL.
  - `SourceReference` = null (V1 has no single source commit); `IdempotencyCheckSql` = null
  - No tests in this task — behaviour tests for V1 already exist and are updated in Task 0.3a

### Task 0.3a: Update existing hard-coded `MigrationVersion = 1` assertions to `V_latest`

- [x] **IMPLEMENT: Retarget existing fresh-install / bootstrap / idempotent / concurrent tests to `V_latest`**
  - Existing tests (written against the single-V1 model of spec 0023) hard-code SQL predicates like `[MigrationVersion] = 1` and C# assertions of the same shape. Once Phase 1–5 ships, fresh install stamps `V_latest` (outbox = 7, inbox = 2 for relational; same for Spanner) — these assertions will fail without update.
  - **Selection criterion**: every test file containing a hard-coded `MigrationVersion = 1` assertion (SQL predicate, parameterised SQL, or C# `Assert`) — confirmed at task-execution time by `grep -rn 'MigrationVersion.*=.*1\|MigrationVersion.*1' tests/Paramore.Brighter.*Tests/BoxProvisioning/`. The 23 files enumerated below are the current matches; if grep surfaces additional files at task-execution time, add them to the retarget set.
  - Files to update (23 files confirmed by grep — do all in one commit to keep Phase 0 green-build):
    - **MSSQL** (6): `When_mssql_outbox_provisioner_runs_on_fresh_database_it_should_create_outbox_table.cs`, `When_mssql_outbox_provisioner_finds_existing_table_without_history_it_should_bootstrap.cs`, `When_mssql_outbox_provisioner_runs_on_already_provisioned_database_it_should_be_idempotent.cs`, `When_multiple_mssql_provisioners_run_concurrently_they_should_not_corrupt_state.cs`, `When_mssql_inbox_provisioner_runs_on_fresh_database_it_should_create_inbox_table.cs`, `When_mssql_inbox_provisioner_finds_existing_table_without_history_it_should_bootstrap.cs`
    - **PostgreSQL** (6): `When_postgresql_outbox_provisioner_runs_on_fresh_database_it_should_create_outbox_table.cs`, `When_postgresql_outbox_provisioner_finds_existing_table_without_history_it_should_bootstrap.cs`, `When_postgresql_outbox_provisioner_runs_on_already_provisioned_database_it_should_be_idempotent.cs`, `When_multiple_postgresql_provisioners_run_concurrently_they_should_not_corrupt_state.cs`, `When_postgresql_inbox_provisioner_runs_on_fresh_database_it_should_create_inbox_table.cs`, `When_postgresql_inbox_provisioner_finds_existing_table_without_history_it_should_bootstrap.cs`
    - **MySQL** (4): `When_mysql_outbox_provisioner_runs_on_fresh_database_*`, `When_mysql_outbox_provisioner_finds_existing_table_without_history_*`, `When_multiple_mysql_provisioners_run_concurrently_*`, `When_mysql_inbox_provisioner_runs_it_should_create_table_or_bootstrap_existing.cs`
    - **SQLite** (3): `When_sqlite_outbox_provisioner_runs_on_fresh_database_it_should_create_outbox_table.cs`, `When_sqlite_outbox_provisioner_finds_existing_table_without_history_it_should_bootstrap.cs`, `When_sqlite_inbox_provisioner_runs_it_should_create_table_or_bootstrap_existing.cs`
    - **Spanner** (4): `When_spanner_outbox_provisioner_runs_on_fresh_database_it_should_create_outbox_table.cs`, `When_spanner_outbox_provisioner_finds_existing_table_without_history_it_should_bootstrap.cs`, `When_spanner_inbox_provisioner_runs_on_fresh_database_it_should_create_inbox_table.cs`, `When_spanner_inbox_provisioner_finds_existing_table_without_history_it_should_bootstrap.cs`
  - Replace `[MigrationVersion] = 1` SQL predicates with `[MigrationVersion] = @ExpectedVersion` and parameterise on a constant `V_LATEST_OUTBOX = 7` / `V_LATEST_INBOX = 2` imported from a shared `ExpectedMigrationVersions.cs` helper in each test project's `BoxProvisioning/` directory.
  - For bootstrap tests that also happen to seed a V7-shaped table: change the assertion to expect a synthetic row at `V_LATEST` + no additional rows (fresh-install path — detection returns `V_LATEST` and runner stamps directly).
  - For concurrent tests: assert `COUNT(*) = 1` for the history row at `V_LATEST` instead of at `V=1`.
  - No new tests — this task retargets existing tests; Phase 1–5 bootstrap-at-V_k and fresh-install tests add the new behavioural coverage.
  - **Dependency**: subject to the Phase-0 single-commit constraint at the top of this phase — Tasks 0.1 / 0.2 / 0.3 / 0.3a all land in the same commit. Splitting 0.3 from 0.3a leaves the test suite red.

### Task 0.4: Add drift-detection test infrastructure

- [x] **TEST + IMPLEMENT: Builder DDL column-set extraction helper for drift detection**
  - **USE COMMAND**: `/test-first when builder ddl is parsed by GetExpectedColumns it should return the actual column names`
  - **Project**: new `tests/Paramore.Brighter.BoxProvisioning.Tests/` project (separate from `Paramore.Brighter.Core.Tests` so Core stays free of backend-specific DDL grammar knowledge; per-backend drift test projects in Phases 1–4 add a `ProjectReference` to it). Registered in `Brighter.slnx` between `Base.Test` and `Core.Tests`.
  - Test location: `tests/Paramore.Brighter.BoxProvisioning.Tests/Drift/`
  - Test file: `When_builder_ddl_is_parsed_by_get_expected_columns_it_should_return_actual_column_names.cs`
  - Test class: `DdlColumnExtractorTests` (per testing.md the `When_…` convention is for method names and file names only — class names use `[Behavior]Tests`)
  - Test should verify:
    - Given a known inline `CREATE TABLE` string with 6 columns including quoted identifiers (MSSQL `[col]`, Postgres lowercase, MySQL backtick, SQLite bracket)
    - **SQLite inline `COLLATE NOCASE` after the type specifier is handled** — e.g. `[MessageId] TEXT NOT NULL COLLATE NOCASE,` — the extractor returns `MessageId`, not `MessageId COLLATE NOCASE` or any truncation
    - Constraint clauses are ignored: a `CONSTRAINT PK_X PRIMARY KEY (Col1, Col2)` line does not contribute spurious column names
    - Table-level `PRIMARY KEY (Col1, Col2)` line similarly ignored
    - When `DdlColumnExtractor.GetExpectedColumns(ddl, quoteStyle)` is called
    - Then the returned `HashSet<string>` contains exactly those 6 column names
    - Verify for each backend's quote style
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add test-project helper `DdlColumnExtractor` in `tests/Paramore.Brighter.BoxProvisioning.Tests/Drift/DdlColumnExtractor.cs`
    - Define a co-located test-project enum `public enum QuoteStyle { MsSql, Postgres, MySql, Sqlite }` (also in `tests/Paramore.Brighter.BoxProvisioning.Tests/Drift/QuoteStyle.cs`) — values are referenced by name from per-backend drift tests in 1.1 / 2.1 / 3.1 / 4.1
    - Regex-based extraction per backend quote style — parses the `(...)` body of `CREATE TABLE ...`, splits on top-level commas, extracts the first quoted identifier on each column-declaration line, filters out lines beginning with `CONSTRAINT` / `PRIMARY KEY` / `FOREIGN KEY` / `UNIQUE` / `INDEX`
    - Inline `COLLATE <name>` clauses are harmless because the extractor only reads the first quoted identifier on the line — but document this explicitly in a code comment
    - Returns `HashSet<string>` with the backend-appropriate comparer (Ordinal for Postgres, OrdinalIgnoreCase otherwise)
    - This helper is consumed by all per-backend drift tests in phases 1–4 — those test projects must add `<ProjectReference Include="..\Paramore.Brighter.BoxProvisioning.Tests\Paramore.Brighter.BoxProvisioning.Tests.csproj" />` so they can use `DdlColumnExtractor` and `QuoteStyle`

---

## Phase 1: MSSQL reference implementation

### Task 1.1: Drift test — MSSQL outbox builder matches V_latest migration list

- [x] **TEST + IMPLEMENT: MSSQL outbox builder column set equals V_latest migration LogicalColumns union housekeeping**
  - **USE COMMAND**: `/test-first when mssql outbox builder is compared to v7 migration columns it should have identical expected column set`
  - **Project setup (first MSSQL drift task only)**: add `<ProjectReference Include="..\Paramore.Brighter.BoxProvisioning.Tests\Paramore.Brighter.BoxProvisioning.Tests.csproj" />` to `tests/Paramore.Brighter.MSSQL.Tests/Paramore.Brighter.MSSQL.Tests.csproj` to gain access to `DdlColumnExtractor` + `QuoteStyle`.
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/`
  - Test file: `When_mssql_outbox_builder_is_compared_to_v7_migration_columns_it_should_have_identical_expected_column_set.cs`
  - Test should verify:
    - `DdlColumnExtractor.GetExpectedColumns(SqlOutboxBuilder.GetDDL("outbox_test", hasBinaryMessagePayload: false), QuoteStyle.MsSql)` produces the complete column set (use the actual parameter name — `SqlOutboxBuilder.GetDDL` signature is `GetDDL(string outboxTableName, bool hasBinaryMessagePayload = false)`)
    - `MsSqlOutboxMigrations.All(config).Last().LogicalColumns.Union(MsSqlOutboxHousekeeping.V1)` equals the builder's column set
    - Housekeeping set includes: `Id` (MSSQL outbox housekeeping per ADR §1)
    - Test runs twice: `hasBinaryMessagePayload: false` and `hasBinaryMessagePayload: true` (same column names, different Body types)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add static `MsSqlOutboxHousekeeping.V1` set in test project (`tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/Drift/MsSqlOutboxHousekeeping.cs`)
    - Test will fail against the current single-V1 list — that is intentional; it drives the V1..V7 expansion in Task 1.2
    - Do NOT expand `MsSqlOutboxMigrations.All()` in this task — Task 1.2 does that

### Task 1.2: MSSQL outbox migrations V1..V7

- [x] **TEST + IMPLEMENT: MSSQL outbox V1..V7 migrations each carry LogicalColumns matching the archaeology**
  - **USE COMMAND**: `/test-first when mssql outbox migrations are listed it should return v1 through v7 with correct logical columns`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/`
  - Test file: `When_mssql_outbox_migrations_are_listed_it_should_return_v1_through_v7_with_correct_logical_columns.cs`
  - Test should verify:
    - `MsSqlOutboxMigrations.All(config)` returns exactly 7 entries, Version 1..7 in order
    - V1 LogicalColumns = `{ MessageId, Topic, MessageType, Timestamp, HeaderBag, Body }` (baseline)
    - V2 LogicalColumns = V1 ∪ `{ Dispatched }`
    - V3 LogicalColumns = V2 ∪ `{ CorrelationId, ReplyTo, ContentType }`
    - V4 LogicalColumns = V3 ∪ `{ PartitionKey }`
    - V5 LogicalColumns = V4 ∪ `{ Source, Type, DataSchema, Subject, TraceParent, TraceState, Baggage }`
    - V6 LogicalColumns = V5 ∪ `{ WorkflowId, JobId }`
    - V7 LogicalColumns = V6 ∪ `{ DataRef, SpecVersion }`
    - Each V2..V7 has non-null `SourceReference` referencing the commit/PR from `README.md` archaeology
    - Drift test from Task 1.1 now passes (builder == V7 ∪ housekeeping)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Expand `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlOutboxMigrations.cs` to return 7 `BoxMigration` entries
    - V1 UpScript stays the current builder DDL (fresh-install fast path uses V1's UpScript per ADR §3)
    - V2..V7 UpScript each use the `IF COL_LENGTH(N'[{schema}].[{table}]', N'ColName') IS NULL ALTER TABLE ... ADD ...` pattern per ADR §5
    - V4 adds `PartitionKey NVARCHAR(255) NULL`
    - V5 column types per current builder DDL
    - V7 adds `DataRef NVARCHAR(MAX) NULL, SpecVersion NVARCHAR(10) NULL`
    - Each `SourceReference` = archaeology-derived, e.g. V4 = `"1cdc04b60 / #2560"`, V7 = `"d67dac947 / #3790"`
    - `IdempotencyCheckSql` = null for all (MSSQL embeds check in UpScript)

### Task 1.3: MSSQL inbox migrations V1..V2

- [x] **TEST + IMPLEMENT: MSSQL inbox V1..V2 migrations with ContextKey as V2**
  - **USE COMMAND**: `/test-first when mssql inbox migrations are listed it should return v1 and v2 with contextkey added in v2`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/`
  - Test file: `When_mssql_inbox_migrations_are_listed_it_should_return_v1_and_v2_with_contextkey_added_in_v2.cs`
  - Test should verify:
    - `MsSqlInboxMigrations.All(config)` returns exactly 2 entries
    - V1 LogicalColumns = `{ CommandId, CommandType, CommandBody, Timestamp }`
    - V2 LogicalColumns = V1 ∪ `{ ContextKey }`
    - V2 `SourceReference` = `"787c31c52"` (Oct 2018)
    - V2 UpScript follows `IF COL_LENGTH` pattern
    - Inbox drift test (analogous to Task 1.1) passes: builder columns == V2 LogicalColumns ∪ `{ Id }` housekeeping
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Extend `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlInboxMigrations.cs` to return V1 + V2
    - V1 stays the current `SqlInboxBuilder.GetDDL(...)` (fresh install only)
    - V2 UpScript: `IF COL_LENGTH(...'ContextKey') IS NULL ALTER TABLE ... ADD [ContextKey] NVARCHAR(128) NULL` (match current builder type/nullability)
    - Add inbox drift test file `When_mssql_inbox_builder_is_compared_to_v2_migration_columns_it_should_have_identical_expected_column_set.cs` alongside this test

### Task 1.4: MSSQL runner three-path branching with discriminator gate

- [x] **TEST + IMPLEMENT: MSSQL runner fresh path re-checks table existence under the advisory lock**
  - **USE COMMAND**: `/test-first when mssql runner fresh path acquires lock it should re-check table existence before creating`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/`
  - Test file: `When_mssql_runner_fresh_path_acquires_lock_it_should_re_check_table_existence_before_creating.cs`
  - Test should verify (TOCTOU symptom only — bootstrap end-state is verified by Tasks 1.5 / 1.6):
    - Given a `BoxTableState { TableExists=false }` passed to `MigrateAsync`, but seed a V_latest-shape outbox table (V7 columns present, no `__BrighterMigrationHistory` row) directly before calling `MigrateAsync` — simulates the race where `DetectTableStateAsync` ran before another instance created the table
    - When `MigrateAsync` runs
    - Then **no `CREATE TABLE` duplicate-object exception is thrown** — TOCTOU re-check sees `tableExistsNow=true && historyExistsNow=false` and falls through to bootstrap instead of attempting CREATE TABLE on an existing table
    - Then **at least one history row is inserted** (existence proves the bootstrap branch executed; the specific Version values and synthetic-row description are concerns of Tasks 1.5 / 1.6, not 1.4)
    - Then **the seeded table data is preserved** (no DROP/recreate happened — fresh path was correctly aborted)
    - Note: this test is deliberately permissive about the exact bootstrap output. It catches the bug "fresh path didn't TOCTOU re-check and tried to CREATE TABLE on an existing table"; it does NOT verify what bootstrap detection returns or which migrations apply — those are downstream concerns
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlBoxMigrationRunner.cs` `MigrateAsync`:
      - After `AcquireLockAsync`, re-run `DoesTableExistAsync` and `DoesHistoryExistAsync`
      - Branch on `(!tableExistsNow)` → fresh path: execute V1 UpScript, insert single history at V_latest
      - Branch on `(tableExistsNow && !historyExistsNow)` → bootstrap path: invoke `MsSqlBoxDetectionHelpers.DetectCurrentVersionAsync(...)` (the helper introduced by Task 1.5 — same method the provisioner uses pre-lock; see ADR §3 "Detection helper ownership"); apply discriminator gate from Task 1.5; stamp synthetic history at detected V; run migrations V+1..V_latest
      - Branch on `(tableExistsNow && historyExistsNow)` → normal path: read `MAX(V)` from history, run migrations above MAX
      - All three paths share the single transaction and `sp_getapplock` from current code
    - **Detection ownership**: after Task 1.5 moves detection into `MsSqlBoxDetectionHelpers`, the runner calls the helper for re-detection under the lock; it does not inline detection logic. The runner does not trust `tableState.CurrentVersion` from the pre-lock pass — the bootstrap path re-detects every time
    - The runner no longer trusts `tableState` blindly — it re-reads under the lock
  - **Test/impl scope note**: this task's test exercises only the fresh-path TOCTOU symptom (the most subtle race). The bootstrap-path branching is exercised by Task 1.5 (discriminator gate) and Tasks 1.6/1.7 (bootstrap-at-V_k); the normal-path branching is exercised by Task 1.9 (spec-0023-era transition). Implementation must satisfy all three paths but each is test-driven by a downstream task. This pattern repeats in 2.4 / 3.4 / 4.4
  - **Pairing constraint**: the runner's bootstrap branch invokes `MsSqlBoxDetectionHelpers.DetectCurrentVersionAsync`, which **does not exist until Task 1.5 implements it**. Therefore Tasks 1.4 and 1.5 land in the **same commit** — write 1.4's test, write 1.5's test, then implement the runner branching (1.4) + the helper method (1.5) together so both test suites compile and turn green simultaneously. The same pairing applies to 2.4↔2.5, 3.4↔3.5, 4.4↔4.5

### Task 1.5: MSSQL discriminator-gated detection returns `-1`, `0`, or `V>=1`

- [x] **TEST + IMPLEMENT: MSSQL outbox detection returns -1 when HeaderBag column is absent**
  - **USE COMMAND**: `/test-first when mssql outbox detects table missing headerbag discriminator it should return negative one`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/`
  - Test file: `When_mssql_outbox_detects_table_missing_headerbag_discriminator_it_should_return_negative_one.cs`
  - Test should verify:
    - Given a table that exists but has no `HeaderBag` column (e.g. a two-column `CommandId, Timestamp` foreign table)
    - When `MsSqlBoxDetectionHelpers.DetectCurrentVersionAsync` is called for outbox
    - Then it returns `-1`
    - When `ProvisionAsync` runs against such a table, it throws `ConfigurationException` with message containing "not a Brighter outbox" and the discriminator column name
    - Add analogous test for inbox discriminator (`CommandBody`) absent → `-1`
    - Add test: a `HeaderBag`-bearing table with no V1 columns → returns `0` (unknown schema) → throws `ConfigurationException` containing "does not match any known schema version"
    - Add test: a V3-shaped outbox table (has HeaderBag + CorrelationId + ReplyTo + ContentType + baseline but no PartitionKey) → returns `3`
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - **Move and rewrite** `DetectCurrentVersionAsync` from `MsSqlOutboxProvisioner` and `MsSqlInboxProvisioner` into `MsSqlBoxDetectionHelpers` as two new static methods (`DetectOutboxVersionAsync` and `DetectInboxVersionAsync`, or a single overloaded method taking the discriminator as a parameter). Detection becomes the single source of version-from-columns logic, callable by both the provisioner (pre-lock) and the runner (post-lock TOCTOU re-detection per Task 1.4 / ADR §3)
    - Method signature: `static Task<int> DetectCurrentVersionAsync(SqlConnection conn, SqlTransaction? txn, string tableName, string? schemaName, IReadOnlyList<IAmABoxMigration> migrations, string discriminatorColumn, CancellationToken ct)`
    - Algorithm per ADR §2:
      - `if (!actualColumns.Contains(discriminatorColumn)) return -1;`
      - Walk `migrations` top-down; return highest `V` where `actualColumns.IsSupersetOf(migration.LogicalColumns)`
      - Return `0` if discriminator present but no version matched
    - Update `MsSqlOutboxProvisioner.DetectTableStateAsync` and `MsSqlInboxProvisioner.DetectTableStateAsync` to call the helper instead of holding their own private detection method; pass `"HeaderBag"` (outbox) or `"CommandBody"` (inbox) as discriminator
    - Delete the now-private `MsSqlOutboxProvisioner.V1Columns` / equivalent static sets (no longer used)
    - In the runner's bootstrap path (Task 1.4), invoke `MsSqlBoxDetectionHelpers.DetectCurrentVersionAsync` directly under the lock; branch on return: `-1`/`0` → throw `ConfigurationException`; `>=1` → proceed

### Task 1.6: MSSQL bootstrap-at-V_k end-to-end per outbox version

- [x] **TEST + IMPLEMENT: MSSQL outbox bootstrap upgrades pre-V7 tables to V7**
  - **USE COMMAND**: `/test-first when mssql outbox table is bootstrapped at v_k it should upgrade to v7 with history advanced`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/`
  - Test file: `When_mssql_outbox_table_is_bootstrapped_at_vk_it_should_upgrade_to_v7_with_history_advanced.cs`
  - Test should verify (use Theory / parameterised test; one inline data row per version):
    - **Bootstrap-at-V_k** (this task only): for k ∈ {1, 3, 5, 7}, seed a table hand-rolled with the V_k column set (no history row). Run provisioner. Assert:
      - Table now has the full V7 column set
      - History has one synthetic row at V_k (description starts with `bootstrap: detected at V{k}`) + one row per applied migration V_{k+1}..V7
      - For k=7: only the synthetic bootstrap row at V7 (description `bootstrap: detected at V7`) — no ALTERs applied
      - Data in seeded rows survives
    - **Fresh-install case** is NOT covered here; it is verified by the existing fresh-install test (`When_mssql_outbox_provisioner_runs_on_fresh_database_it_should_create_outbox_table.cs`) after Task 0.3a retargets it to V_latest with description starting `fresh install at V_latest`. Two semantically distinct paths (bootstrap-with-seeded-data vs fresh-install-without-table) must not be bundled into the same Theory
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Seed helper: `MsSqlOutboxLegacySeeder.SeedAtV(k, connection)` — raw `CREATE TABLE` scripts for each historical version (k=1..7) in `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/Legacy/MsSqlOutboxLegacySeeder.cs`
    - No production-code changes beyond fixing bugs surfaced by the test
    - All prior phase-1 tasks must be passing before this task is meaningful

### Task 1.7: MSSQL inbox bootstrap test

- [x] **TEST + IMPLEMENT: MSSQL inbox bootstrap upgrades pre-V2 tables to V2**
  - **USE COMMAND**: `/test-first when mssql inbox table is bootstrapped at v1 it should upgrade to v2`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/`
  - Test file: `When_mssql_inbox_table_is_bootstrapped_at_v1_it_should_upgrade_to_v2.cs`
  - Test should verify:
    - Seed a V1 inbox (no ContextKey column, no history row)
    - Run provisioner
    - Assert V2 shape, synthetic history at V1 + applied row at V2
    - Seeded rows survive (NULL `ContextKey` on existing rows)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `MsSqlInboxLegacySeeder.SeedAtV1(connection)`
    - Fix any bugs surfaced

### Task 1.8: MSSQL concurrent-bootstrap race test (outbox + inbox)

- [x] **TEST + IMPLEMENT: Two MSSQL provisioners racing on a legacy table produce exactly one synthetic history row for both outbox and inbox**
  - **USE COMMAND**: `/test-first when two mssql provisioners race on legacy table they should produce exactly one synthetic history row for outbox and inbox`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/`
  - Test file: `When_two_mssql_provisioners_race_on_legacy_table_they_should_produce_exactly_one_synthetic_history_row.cs`
  - Test should verify:
    - **Outbox arm**: seed a V3 outbox with no history; run two `MsSqlOutboxProvisioner` instances (separate connections) in parallel via `Task.WhenAll`; neither throws; history has exactly one synthetic bootstrap row at V3 and exactly one row per V4..V7 (no duplicates); table ends at V7 with original data preserved.
    - **Inbox arm**: seed a V1 inbox with no history; run two `MsSqlInboxProvisioner` instances in parallel; exactly one synthetic row at V1 + one applied row at V2; inbox ends at V2. Separate `Fact` method in the same test file.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - No production-code change expected — the TOCTOU re-check from Task 1.4 + PK on history table already guarantees this
    - The runner is box-type-agnostic so outbox and inbox share the same concurrency mechanics; both arms verify the same invariant against different `BoxType`
    - If test fails, add defence-in-depth (e.g. `INSERT ... WHERE NOT EXISTS` for synthetic row) in the runner
  - Closes spec 0023 R2 for MSSQL

### Task 1.8a: MSSQL whole-chain rollback on mid-chain failure

- [x] **TEST + IMPLEMENT: MSSQL runner rolls back ALL migrations and history rows on mid-chain failure**
  - **USE COMMAND**: `/test-first when mssql runner fails mid chain it should roll back all migrations and history rows atomically`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/`
  - Test file: `When_mssql_runner_fails_mid_chain_it_should_roll_back_all_migrations_and_history_rows_atomically.cs`
  - Test should verify (mirrors Task 3.4 assertion shape but inverted for transactional DDL):
    - Seed legacy V3 table; substitute a broken V6 into the migration list (same `BrokenMigrationFactory` pattern as Task 3.4)
    - First invocation throws
    - Assert: history table has **no rows** for this box (synthetic V3 row and V4/V5 applied rows all rolled back); table still at V3 shape (V4/V5 column additions rolled back — MSSQL supports transactional DDL)
    - Second invocation with real V6: enters bootstrap path, stamps synthetic V3, applies V4..V7 clean
    - End state: exactly one synthetic V3 row + V4..V7 applied rows; table at V7
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - No production change expected — `MsSqlBoxMigrationRunner` already wraps the whole path in `BeginTransaction` (verified in `MigrateAsync`); `sp_getapplock @LockOwner='Transaction'` releases on rollback
    - If assertions fail, the runner is inserting history rows outside the transaction — fix to keep all writes inside the single transaction (ADR §5a)
  - Verifies ADR §5a "MSSQL/Postgres/SQLite: history row inserted immediately after each migration's DDL, inside the same transaction"

### Task 1.8b: MSSQL bootstrap V1→V7 completes within NFR-3 timing budget

- [x] **TEST + IMPLEMENT: MSSQL V1→V7 bootstrap completes within MigrationLockTimeout (NFR-3)**
  - **USE COMMAND**: `/test-first when mssql bootstraps legacy v1 outbox to v7 it should complete within migration lock timeout`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/`
  - Test file: `When_mssql_bootstraps_legacy_v1_outbox_to_v7_it_should_complete_within_migration_lock_timeout.cs`
  - Test should verify:
    - Seed a V1-shaped outbox against the docker-compose MSSQL instance
    - Run the provisioner with `MigrationLockTimeout = TimeSpan.FromSeconds(30)` (the NFR-3 default)
    - Measure `MigrateAsync` wall-clock duration via `Stopwatch`
    - Assert `duration < TimeSpan.FromSeconds(5)` — tight CI bound; NFR-3 expectation is "well under 1s on typical RDBMS instances", 5s gives headroom for CI noise without making the assertion vacuous
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - No production change expected
    - This is the canonical NFR-3 verification — other backends (Postgres/MySQL/SQLite) do not require separate timing tests; MSSQL serves as the reference since it has the heaviest DDL batch (7 `IF COL_LENGTH ... ALTER` statements)

### Task 1.9: MSSQL spec-0023-era transition test (AC-6)

- [x] **TEST + IMPLEMENT: MSSQL table with existing V1-marked history transitions cleanly to V7**
  - **USE COMMAND**: `/test-first when mssql table has spec 0023 era history at v1 it should transition cleanly to v7`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/`
  - Test file: `When_mssql_table_has_spec_0023_era_history_at_v1_it_should_transition_cleanly_to_v7.cs`
  - Test should verify:
    - Seed a full V7-shaped outbox (current builder DDL) AND a `__BrighterMigrationHistory` row at `MigrationVersion=1` with spec-0023-era description (simulates spec 0023 prod installation)
    - Run the new provisioner
    - Assert: normal path taken (not bootstrap), `MAX(V)` read as 1, migrations V2..V7 evaluated, each finds columns already present → idempotency check skips each, history rows V2..V7 inserted **without** re-running DDL
    - End state: `MAX(V)` = 7, no duplicate rows, table shape unchanged
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - No production-code change expected; validates that `IF COL_LENGTH` idempotency + `IsMigrationAppliedAsync` handle this gracefully
    - If the test reveals a gap (e.g. history INSERT collision), fix in runner

---

## Phase 2: PostgreSQL

### Task 2.1: Drift tests — Postgres outbox V7 and inbox V1

- [x] **TEST + IMPLEMENT: Postgres outbox + inbox builders match V_latest migration LogicalColumns**
  - **USE COMMAND**: `/test-first when postgres outbox and inbox builders are compared to latest migration columns they should have identical expected column sets`
  - **Project setup (first Postgres drift task only)**: add `<ProjectReference Include="..\Paramore.Brighter.BoxProvisioning.Tests\Paramore.Brighter.BoxProvisioning.Tests.csproj" />` to `tests/Paramore.Brighter.PostgresSQL.Tests/Paramore.Brighter.PostgresSQL.Tests.csproj` to gain access to `DdlColumnExtractor` + `QuoteStyle`.
  - Test location: `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning/`
  - Test file: `When_postgres_outbox_and_inbox_builders_are_compared_to_latest_migration_columns_they_should_have_identical_expected_column_sets.cs`
  - Test should verify:
    - Outbox: `DdlColumnExtractor.GetExpectedColumns(PostgreSqlOutboxBuilder.GetDDL("outbox_test", binaryMessagePayload: false), QuoteStyle.Postgres)` == V7 ∪ `{ id, messageid }` (Postgres Id BIGSERIAL + MessageId UNIQUE per ADR §1). Actual builder signature: `GetDDL(string outboxTableName, bool binaryMessagePayload = false)`.
    - Inbox: builder cols == V1 ∪ `{}` (Postgres inbox has no extra housekeeping beyond the V1 logical columns — composite PK uses `(CommandId, ContextKey)` and `ContextKey` is part of V1 per ADR §1)
    - Run against both payload variants: `binaryMessagePayload: false` (text) and `binaryMessagePayload: true` (binary). Postgres outbox has **two** payload variants only — no JSON/JSONB variant exists in the builder (verified by grep against `src/Paramore.Brighter.Outbox.PostgreSql/`). Column names are identical across both.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `PostgreSqlOutboxHousekeeping.V1` = `{ id, messageid }` (lowercase — Postgres folds)
    - Add `PostgreSqlInboxHousekeeping.V1` = `{}` (all V1 columns are in LogicalColumns)
    - Expected to fail before Task 2.2 / 2.3 — intentional

### Task 2.2: Postgres outbox migrations V1..V7

- [x] **TEST + IMPLEMENT: Postgres outbox V1..V7 migrations with IF NOT EXISTS ALTER pattern**
  - **USE COMMAND**: `/test-first when postgres outbox migrations are listed it should return v1 through v7 with lowercase logical columns and if not exists pattern`
  - Test location: `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning/`
  - Test file: `When_postgres_outbox_migrations_are_listed_it_should_return_v1_through_v7_with_lowercase_logical_columns_and_if_not_exists_pattern.cs`
  - Test should verify:
    - 7 entries, V1..V7, LogicalColumns per ADR §1 (all **lowercase** to match information_schema folding)
    - V2..V7 `UpScript` contains `ADD COLUMN IF NOT EXISTS`
    - `SourceReference` carried over from archaeology (V4 = `"cff67fd5e / #3464"` for Postgres)
    - Drift test from Task 2.1 now passes for outbox
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Expand `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlOutboxMigrations.cs` to 7 entries
    - Each V2..V7 `UpScript`: `ALTER TABLE {schema}.{table} ADD COLUMN IF NOT EXISTS colname type NULL;` (multiple columns per migration = multiple ALTERs, each IF NOT EXISTS)
    - Types match current Postgres builder
    - `IdempotencyCheckSql` = null

### Task 2.3: Postgres inbox migrations V1-only

- [x] **TEST + IMPLEMENT: Postgres inbox migrations list has only V1 (no V2 ContextKey migration)**
  - **USE COMMAND**: `/test-first when postgres inbox migrations are listed it should return only v1 with contextkey and composite pk as baseline`
  - Test location: `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning/`
  - Test file: `When_postgres_inbox_migrations_are_listed_it_should_return_only_v1_with_contextkey_and_composite_pk_as_baseline.cs`
  - Test should verify:
    - `PostgreSqlInboxMigrations.All(config)` returns exactly 1 entry
    - V1 LogicalColumns = `{ commandid, commandtype, commandbody, timestamp, contextkey }` (contextkey included because Postgres inbox was born with it per ADR §1 Inbox table — PR #1401)
    - V1 UpScript = current `PostgreSqlInboxBuilder.GetDDL(...)` with composite `PRIMARY KEY (CommandId, ContextKey)`
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlInboxMigrations.cs` returns 1 entry only
    - No V2 entry — the design review established no pre-ContextKey Postgres inbox ever shipped (ADR "Alternatives → E")
    - `PostgreSqlInboxProvisioner` detection must cope with a 1-entry migration list (covered by Task 2.5)

### Task 2.4: Postgres runner three-path branching

- [x] **TEST + IMPLEMENT: Postgres runner fresh path re-checks table existence under pg_try_advisory_lock**
  - **USE COMMAND**: `/test-first when postgres runner fresh path acquires advisory lock it should re-check table existence before creating`
  - Test location: `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning/`
  - Test file: `When_postgres_runner_fresh_path_acquires_advisory_lock_it_should_re_check_table_existence_before_creating.cs`
  - Test should verify:
    - Analogous to Task 1.4: `TableExists=false` at call time, table exists at SQL time → falls through to bootstrap without CREATE TABLE failure
    - History has one synthetic row at detected V, one row per applied migration
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Modify `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlBoxMigrationRunner.cs` `MigrateAsync` to three-path structure per ADR §3
    - Advisory lock wraps the whole path inside a single `NpgsqlTransaction`
    - Re-check `DoesTableExistAsync` / `DoesHistoryExistAsync` under the lock

### Task 2.5: Postgres discriminator-gated detection

- [x] **TEST + IMPLEMENT: Postgres outbox/inbox detection uses HeaderBag/CommandBody discriminator**
  - **USE COMMAND**: `/test-first when postgres outbox detects table missing headerbag it should return negative one and inbox should handle single version list`
  - Test location: `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning/`
  - Test file: `When_postgres_outbox_detects_table_missing_headerbag_it_should_return_negative_one_and_inbox_should_handle_single_version_list.cs`
  - Test should verify:
    - Outbox: foreign table without `headerbag` → -1; `headerbag`-bearing non-matching schema → 0; V3-shaped outbox → 3; V7-shaped → 7
    - Inbox: foreign table without `commandbody` → -1; valid V1 inbox → 1 (detection must work with 1-entry migration list)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - **Move and rewrite** detection from `PostgreSqlOutboxProvisioner` / `PostgreSqlInboxProvisioner` into `PostgreSqlBoxDetectionHelpers.DetectCurrentVersionAsync` per ADR §2 (lowercase discriminator: `"headerbag"` / `"commandbody"`); same single-source-of-truth pattern as Task 1.5
    - Walk migrations top-down; return highest match
    - Provisioner's `DetectTableStateAsync` calls the helper; runner re-invokes the helper under `pg_try_advisory_lock` per Task 2.4
    - Delete any static V1Columns field; accept migrations list as parameter

### Task 2.6: Postgres bootstrap-at-V_k test

- [x] **TEST + IMPLEMENT: Postgres outbox bootstrap upgrades pre-V7 tables to V7**
  - **USE COMMAND**: `/test-first when postgres outbox table is bootstrapped at v_k it should upgrade to v7`
  - Test location: `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning/`
  - Test file: `When_postgres_outbox_table_is_bootstrapped_at_vk_it_should_upgrade_to_v7.cs`
  - Test should verify: parameterised k ∈ {1, 3, 5, 7} — same assertions as Task 1.6 but against Postgres
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Seed helper `PostgreSqlOutboxLegacySeeder.SeedAtV(k, connection)` in `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning/Legacy/`
    - Historical DDL per version (from archaeology) — NB Postgres PR #1401 timing: V3 is the earliest Postgres shipped; include a V3-as-baseline case in the seeder

### Task 2.7: Postgres concurrent-bootstrap race test (outbox + inbox)

- [x] **TEST + IMPLEMENT: Two Postgres provisioners racing produce exactly one synthetic history row for outbox and inbox**
  - **USE COMMAND**: `/test-first when two postgres provisioners race on legacy table they should produce exactly one synthetic history row for outbox and inbox`
  - Test location: `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning/`
  - Test file: `When_two_postgres_provisioners_race_on_legacy_table_they_should_produce_exactly_one_synthetic_history_row.cs`
  - Test should verify:
    - **Outbox arm**: as Task 1.8 against Postgres (seed V3, race two provisioners, assert exactly one synthetic row at V3 + applied rows V4..V7)
    - **Inbox arm**: seed a V1-shape Postgres inbox (no history) and race two `PostgreSqlInboxProvisioner` instances. Assert exactly one synthetic row at V1 (Postgres inbox has no V2 — chain is V1-only per ADR §1). Separate `Fact` method.
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - No production-code change expected; `pg_try_advisory_lock` + TOCTOU re-check + PK on history table handles this
    - The inbox arm exercises the V1-only migration list path — verifies detection copes with a 1-entry list under concurrency

### Task 2.7a: Postgres whole-chain rollback on mid-chain failure

- [x] **TEST + IMPLEMENT: Postgres runner rolls back all migrations and history rows on mid-chain failure**
  - **USE COMMAND**: `/test-first when postgres runner fails mid chain it should roll back all migrations and history rows atomically`
  - Test location: `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning/`
  - Test file: `When_postgres_runner_fails_mid_chain_it_should_roll_back_all_migrations_and_history_rows_atomically.cs`
  - Test should verify: same as Task 1.8a but Postgres-specific; relies on Postgres's transactional DDL support (rollback atomic)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - No production change expected — `PostgreSqlBoxMigrationRunner` wraps in `NpgsqlTransaction`
    - If assertions fail, fix to keep history INSERTs inside the transaction

### Task 2.8: Postgres spec-0023-era transition test (AC-6)

- [x] **TEST + IMPLEMENT: Postgres table with existing V1-marked history transitions cleanly**
  - **USE COMMAND**: `/test-first when postgres table has spec 0023 era history at v1 it should transition cleanly to v7`
  - Test location: `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning/`
  - Test file: `When_postgres_table_has_spec_0023_era_history_at_v1_it_should_transition_cleanly_to_v7.cs`
  - Test should verify: same as Task 1.9 but Postgres-specific
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Validate `ADD COLUMN IF NOT EXISTS` is a no-op for already-present columns

---

## Phase 3: MySQL

### Task 3.1: Drift test — MySQL outbox V7 and inbox V2

- [x] **TEST + IMPLEMENT: MySQL outbox + inbox builders match V_latest migration LogicalColumns**
  - **USE COMMAND**: `/test-first when mysql outbox and inbox builders are compared to latest migration columns they should have identical expected column sets`
  - **Project setup (first MySQL drift task only)**: add `<ProjectReference Include="..\Paramore.Brighter.BoxProvisioning.Tests\Paramore.Brighter.BoxProvisioning.Tests.csproj" />` to `tests/Paramore.Brighter.MySQL.Tests/Paramore.Brighter.MySQL.Tests.csproj` to gain access to `DdlColumnExtractor` + `QuoteStyle`.
  - Test location: `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning/`
  - Test file: `When_mysql_outbox_and_inbox_builders_are_compared_to_latest_migration_columns_they_should_have_identical_expected_column_sets.cs`
  - Test should verify:
    - Outbox builder cols == V7 ∪ `{ Created, CreatedID }` housekeeping
    - Inbox builder cols == V2 (no housekeeping beyond `{ CommandId, CommandType, CommandBody, Timestamp, ContextKey }` — `PRIMARY KEY (CommandId)` uses existing LogicalColumn `CommandId`)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `MySqlOutboxHousekeeping.V1` = `{ Created, CreatedID }`
    - Add `MySqlInboxHousekeeping.V1` = `{}`

### Task 3.2: MySQL outbox migrations V1..V7

- [x] **TEST + IMPLEMENT: MySQL outbox V1..V7 with information_schema prepared-statement pattern**
  - **USE COMMAND**: `/test-first when mysql outbox migrations are listed it should return v1 through v7 with information schema prepared statement pattern`
  - Test location: `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning/`
  - Test file: `When_mysql_outbox_migrations_are_listed_it_should_return_v1_through_v7_with_information_schema_prepared_statement_pattern.cs`
  - Test should verify:
    - 7 entries, V1..V7
    - V2..V7 UpScript contains `information_schema.columns` + `PREPARE stmt FROM @q`
    - `SourceReference` present for V2..V7
    - Drift test (Task 3.1) passes for outbox
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Expand `src/Paramore.Brighter.BoxProvisioning.MySql/MySqlOutboxMigrations.cs` to 7 entries
    - Each V2..V7 UpScript follows the pattern in ADR §5 (works on MySQL 5.7+)
    - Types match current MySQL builder
    - `IdempotencyCheckSql` = null

### Task 3.3: MySQL inbox migrations V1..V2

- [x] **TEST + IMPLEMENT: MySQL inbox V1..V2 with ContextKey added in V2 via information_schema pattern**
  - **USE COMMAND**: `/test-first when mysql inbox migrations are listed it should return v1 and v2 with contextkey added in v2`
  - Test location: `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning/`
  - Test file: `When_mysql_inbox_migrations_are_listed_it_should_return_v1_and_v2_with_contextkey_added_in_v2.cs`
  - Test should verify: analogous to Task 1.3 for MySQL
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - `src/Paramore.Brighter.BoxProvisioning.MySql/MySqlInboxMigrations.cs` returns V1 + V2
    - V2 UpScript = information_schema + prepared-statement ContextKey ADD
    - Inbox drift test passes

### Task 3.4: MySQL runner three-path branching with per-migration commit

- [x] **TEST + IMPLEMENT: MySQL runner resumes from MAX(V) after mid-chain failure**
  - **USE COMMAND**: `/test-first when mysql runner fails mid chain it should resume from max applied version on next invocation`
  - Test location: `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning/`
  - Test file: `When_mysql_runner_fails_mid_chain_it_should_resume_from_max_applied_version_on_next_invocation.cs`
  - Test should verify:
    - Given a legacy V3 table, substitute a deliberately-broken V6 migration in the test's migration list (a `BoxMigration` whose `UpScript` is `SELECT 1 FROM non_existent_table_for_forced_failure;` — real SQL error, no production-code seam required)
    - First invocation fails during V6; history has V4 and V5 (committed per MySQL implicit-DDL-commit semantics), table has V3 + V4 + V5 columns
    - Second invocation uses the **real** V6 migration; reads `MAX(V)=5`, runs V6 + V7 successfully
    - End state: V7 table, history rows V4, V5, V6, V7
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Modify `src/Paramore.Brighter.BoxProvisioning.MySql/MySqlBoxMigrationRunner.cs` `MigrateAsync` to three-path structure
    - **Do not** wrap the chain in a single transaction (MySQL DDL auto-commits) — per-migration history INSERT goes in its own implicit commit
    - `GET_LOCK` held session-scoped across the entire path; `RELEASE_LOCK` in `finally`
    - TOCTOU re-check of table / history existence after lock acquire
    - Test uses a `BrokenMigrationFactory` helper in the test project that returns `MySqlOutboxMigrations.All(config)` with the entry at a given version swapped for a broken `BoxMigration` — avoids any test-only seam in production code

### Task 3.5: MySQL discriminator-gated detection

- [x] **TEST + IMPLEMENT: MySQL outbox/inbox detection uses HeaderBag/CommandBody discriminator**
  - **USE COMMAND**: `/test-first when mysql outbox or inbox detects missing discriminator column it should return negative one`
  - Test location: `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning/`
  - Test file: `When_mysql_outbox_or_inbox_detects_missing_discriminator_column_it_should_return_negative_one.cs`
  - Test should verify: same matrix as Task 1.5 (outbox + inbox; -1 / 0 / V≥1 cases) — MySQL-specific
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - **Move and rewrite** detection from `MySqlOutboxProvisioner` / `MySqlInboxProvisioner` into `MySqlBoxDetectionHelpers.DetectCurrentVersionAsync` (same pattern as Task 1.5 / 2.5)
    - Use case-insensitive comparison (MySQL default collation)
    - Provisioner's `DetectTableStateAsync` calls the helper; runner re-invokes under `GET_LOCK` per Task 3.4

### Task 3.6: MySQL bootstrap-at-V_k test

- [x] **TEST + IMPLEMENT: MySQL outbox bootstrap upgrades pre-V7 tables to V7**
  - **USE COMMAND**: `/test-first when mysql outbox table is bootstrapped at v_k it should upgrade to v7`
  - Test location: `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning/`
  - Test file: `When_mysql_outbox_table_is_bootstrapped_at_vk_it_should_upgrade_to_v7.cs`
  - Test should verify: parameterised k ∈ {1, 3, 5, 7} — same structure as Task 1.6 but MySQL
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - `MySqlOutboxLegacySeeder.SeedAtV(k, connection)` in `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning/Legacy/`

### Task 3.6a: MySQL inbox bootstrap V1 → V2

- [x] **TEST + IMPLEMENT: MySQL inbox bootstrap upgrades pre-V2 tables to V2**
  - **USE COMMAND**: `/test-first when mysql inbox table is bootstrapped at v1 it should upgrade to v2`
  - Test location: `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning/`
  - Test file: `When_mysql_inbox_table_is_bootstrapped_at_v1_it_should_upgrade_to_v2.cs`
  - Test should verify:
    - Seed a V1 MySQL inbox (no `ContextKey` column, no history row)
    - Run provisioner
    - Assert V2 shape, synthetic history at V1 + applied row at V2
    - Seeded rows survive (NULL `ContextKey` on existing rows)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `MySqlInboxLegacySeeder.SeedAtV1(connection)` in `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning/Legacy/`
    - V2 UpScript (information_schema + prepared-statement ContextKey ADD from Task 3.3) executes; history rows inserted via per-migration implicit-commit model (ADR §5a)
    - Mirrors Task 1.7 (MSSQL) and Task 4.7 (SQLite) — single-instance bootstrap, AC-2 case
  - Closes AC-2 for MySQL

### Task 3.7: MySQL concurrent-bootstrap race test (outbox + inbox)

- [x] **TEST + IMPLEMENT: Two MySQL provisioners racing produce exactly one synthetic history row for outbox and inbox**
  - **USE COMMAND**: `/test-first when two mysql provisioners race on legacy table they should produce exactly one synthetic history row for outbox and inbox`
  - Test location: `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning/`
  - Test file: `When_two_mysql_provisioners_race_on_legacy_table_they_should_produce_exactly_one_synthetic_history_row.cs`
  - Test should verify:
    - **Outbox arm**: as Task 1.8 but MySQL
    - **Inbox arm**: seed V1 MySQL inbox (no ContextKey, no history); race two inbox provisioners; exactly one synthetic V1 row + one applied V2 row
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should: no production change expected (`GET_LOCK` + TOCTOU re-check handles both box types)

### Task 3.8: MySQL spec-0023-era transition test (AC-6)

- [x] **TEST + IMPLEMENT: MySQL table with existing V1-marked history transitions cleanly**
  - **USE COMMAND**: `/test-first when mysql table has spec 0023 era history at v1 it should transition cleanly to v7`
  - Test location: `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning/`
  - Test file: `When_mysql_table_has_spec_0023_era_history_at_v1_it_should_transition_cleanly_to_v7.cs`
  - Test should verify: same as Task 1.9 but MySQL
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Validate `information_schema` check = no-op when column exists → V2..V7 just insert history rows

---

## Phase 4: SQLite

### Task 4.1: Drift test — SQLite outbox V7 and inbox V2

- [x] **TEST + IMPLEMENT: SQLite outbox + inbox builders match V_latest migration LogicalColumns**
  - **USE COMMAND**: `/test-first when sqlite outbox and inbox builders are compared to latest migration columns they should have identical expected column sets`
  - **Project setup (first SQLite drift task only)**: add `<ProjectReference Include="..\Paramore.Brighter.BoxProvisioning.Tests\Paramore.Brighter.BoxProvisioning.Tests.csproj" />` to `tests/Paramore.Brighter.Sqlite.Tests/Paramore.Brighter.Sqlite.Tests.csproj` to gain access to `DdlColumnExtractor` + `QuoteStyle`.
  - Test location: `tests/Paramore.Brighter.Sqlite.Tests/BoxProvisioning/`
  - Test file: `When_sqlite_outbox_and_inbox_builders_are_compared_to_latest_migration_columns_they_should_have_identical_expected_column_sets.cs`
  - Test should verify:
    - Outbox builder cols (COLLATE NOCASE noise stripped by extractor) == V7 (no extra housekeeping — SQLite uses implicit rowid)
    - Inbox builder cols == V2
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - `SqliteOutboxHousekeeping.V1` = `{}` (implicit rowid)
    - `SqliteInboxHousekeeping.V1` = `{}`
    - Ensure `DdlColumnExtractor` from Task 0.4 strips `COLLATE NOCASE` clauses

### Task 4.2: SQLite outbox migrations V1..V7 with IdempotencyCheckSql

- [x] **TEST + IMPLEMENT: SQLite outbox V1..V7 use IdempotencyCheckSql and plain ALTER UpScripts**
  - **USE COMMAND**: `/test-first when sqlite outbox migrations are listed it should return v1 through v7 with idempotency check sql and plain alter upscripts`
  - Test location: `tests/Paramore.Brighter.Sqlite.Tests/BoxProvisioning/`
  - Test file: `When_sqlite_outbox_migrations_are_listed_it_should_return_v1_through_v7_with_idempotency_check_sql_and_plain_alter_upscripts.cs`
  - Test should verify:
    - 7 entries, V1..V7
    - V1 `IdempotencyCheckSql` = null (V1 is the full CREATE TABLE)
    - V2..V7 each have `IdempotencyCheckSql` containing `SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name='...'`
    - V2..V7 `UpScript` is a plain `ALTER TABLE [{table}] ADD COLUMN [col] TYPE NULL;` (single statement, no embedded check)
    - Drift test from Task 4.1 passes
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Expand `src/Paramore.Brighter.BoxProvisioning.Sqlite/SqliteOutboxMigrations.cs` to 7 entries
    - `IdempotencyCheckSql` uses table-name substitution only (not schema — SQLite has no schema concept in this context)

### Task 4.3: SQLite inbox migrations V1..V2

- [x] **TEST + IMPLEMENT: SQLite inbox V1..V2 with IdempotencyCheckSql for ContextKey**
  - **USE COMMAND**: `/test-first when sqlite inbox migrations are listed it should return v1 and v2 with idempotency check sql for contextkey`
  - Test location: `tests/Paramore.Brighter.Sqlite.Tests/BoxProvisioning/`
  - Test file: `When_sqlite_inbox_migrations_are_listed_it_should_return_v1_and_v2_with_idempotency_check_sql_for_contextkey.cs`
  - Test should verify: analogous to Task 1.3 for SQLite, V2 uses `IdempotencyCheckSql`
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - `src/Paramore.Brighter.BoxProvisioning.Sqlite/SqliteInboxMigrations.cs` returns V1 + V2
    - V2 `IdempotencyCheckSql`: `SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name='ContextKey'`
    - V2 `UpScript`: `ALTER TABLE [{table}] ADD COLUMN [ContextKey] TEXT NULL;`

### Task 4.4: SQLite runner three-path branching with BEGIN IMMEDIATE and SQLITE_BUSY retry

- [x] **TEST + IMPLEMENT: SQLite runner uses BEGIN IMMEDIATE and retries SQLITE_BUSY with backoff**
  - **USE COMMAND**: `/test-first when sqlite runner contends with concurrent writer it should retry sqlite busy with backoff and succeed`
  - Test location: `tests/Paramore.Brighter.Sqlite.Tests/BoxProvisioning/`
  - Test file: `When_sqlite_runner_contends_with_concurrent_writer_it_should_retry_sqlite_busy_with_backoff_and_succeed.cs`
  - Test should verify:
    - Given: file-backed SQLite database with an active writer holding a write-lock transiently (open a conflicting BEGIN IMMEDIATE in another connection, release after 200ms)
    - When: `MigrateAsync` runs
    - Then: runner retries on `SQLITE_BUSY` (exponential backoff), eventually acquires the lock, applies migrations, commits
    - History rows correct, no duplicate state
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Modify `src/Paramore.Brighter.BoxProvisioning.Sqlite/SqliteBoxMigrationRunner.cs` `MigrateAsync`:
      - Replace `BEGIN DEFERRED` (or current default) with `BEGIN IMMEDIATE TRANSACTION`
      - Wrap acquire in a retry loop on `SQLITE_BUSY` — exponential backoff up to `_lockTimeout`
      - Three-path branching per ADR §3 (fresh / bootstrap / normal); call `SqliteBoxDetectionHelpers.DetectCurrentVersionAsync` for re-detection in the bootstrap path (per ADR §3 "Detection helper ownership")
      - TOCTOU re-check of table / history existence after lock acquired
      - For V2..V7: execute `IdempotencyCheckSql` first; if scalar > 0 skip `UpScript` (still insert history row)
  - **Test/impl scope note**: this task's test exercises only `SQLITE_BUSY` retry under contention — the most SQLite-specific concern. The other implementation behaviors are test-driven by downstream tasks: bootstrap-path branching by Tasks 4.5/4.6/4.7; TOCTOU re-check by Task 4.8 (concurrent bootstrap); `IdempotencyCheckSql` skip path by Task 4.9 (spec-0023-era transition where every column is already present); whole-chain rollback by Task 4.8a. Implementation must satisfy all of these but each is verified by a dedicated downstream test — same pattern as Task 1.4 / 2.4 / 3.4

### Task 4.5: SQLite discriminator-gated detection

- [x] **TEST + IMPLEMENT: SQLite outbox/inbox detection uses HeaderBag/CommandBody discriminator**
  - **USE COMMAND**: `/test-first when sqlite outbox or inbox detects missing discriminator column it should return negative one`
  - Test location: `tests/Paramore.Brighter.Sqlite.Tests/BoxProvisioning/`
  - Test file: `When_sqlite_outbox_or_inbox_detects_missing_discriminator_column_it_should_return_negative_one.cs`
  - Test should verify: same matrix as Task 1.5, SQLite-specific
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - **Move and rewrite** detection from `SqliteOutboxProvisioner` / `SqliteInboxProvisioner` into `SqliteBoxDetectionHelpers.DetectCurrentVersionAsync` (same pattern as Task 1.5 / 2.5 / 3.5)
    - Use `pragma_table_info(tableName)` to enumerate columns
    - Case-insensitive comparer (`StringComparer.OrdinalIgnoreCase`)
    - Provisioner's `DetectTableStateAsync` calls the helper; runner re-invokes under `BEGIN IMMEDIATE` per Task 4.4

### Task 4.6: SQLite outbox bootstrap-at-V_k

- [x] **TEST + IMPLEMENT: SQLite outbox bootstrap upgrades pre-V7 tables to V7**
  - **USE COMMAND**: `/test-first when sqlite outbox table is bootstrapped at v_k it should upgrade to v7`
  - Test location: `tests/Paramore.Brighter.Sqlite.Tests/BoxProvisioning/`
  - Test file: `When_sqlite_outbox_table_is_bootstrapped_at_vk_it_should_upgrade_to_v7.cs`
  - Test should verify: parameterised k ∈ {1, 3, 5, 7} — same shape as Task 1.6, SQLite-specific
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - `SqliteOutboxLegacySeeder.SeedAtV(k, connection)` in `tests/Paramore.Brighter.Sqlite.Tests/BoxProvisioning/Legacy/`
    - SQLite databases are file-backed in `Path.GetTempPath()` — no docker needed

### Task 4.7: SQLite inbox bootstrap V1 → V2

- [x] **TEST + IMPLEMENT: SQLite inbox bootstrap upgrades pre-V2 tables to V2**
  - **USE COMMAND**: `/test-first when sqlite inbox table is bootstrapped at v1 it should upgrade to v2`
  - Test location: `tests/Paramore.Brighter.Sqlite.Tests/BoxProvisioning/`
  - Test file: `When_sqlite_inbox_table_is_bootstrapped_at_v1_it_should_upgrade_to_v2.cs`
  - Test should verify: seed V1 inbox (no ContextKey), run provisioner, assert V2 shape + synthetic V1 history + applied V2 row + seeded rows survive with NULL ContextKey
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - `SqliteInboxLegacySeeder.SeedAtV1(connection)` in `tests/Paramore.Brighter.Sqlite.Tests/BoxProvisioning/Legacy/`
    - V2 `IdempotencyCheckSql` runs first — scalar = 0 on V1 table → `UpScript` executes

### Task 4.8: SQLite concurrent-bootstrap race test (outbox + inbox)

- [x] **TEST + IMPLEMENT: Two SQLite provisioners racing produce exactly one synthetic history row for outbox and inbox**
  - **USE COMMAND**: `/test-first when two sqlite provisioners race on legacy table they should produce exactly one synthetic history row for outbox and inbox`
  - Test location: `tests/Paramore.Brighter.Sqlite.Tests/BoxProvisioning/`
  - Test file: `When_two_sqlite_provisioners_race_on_legacy_table_they_should_produce_exactly_one_synthetic_history_row.cs`
  - Test should verify:
    - **Outbox arm**: seed V3 outbox in a shared file-backed database; launch two provisioners with separate connections via `Task.WhenAll`; neither throws; exactly one synthetic row at V3, one applied row per V4..V7; `SQLITE_BUSY` retry from Task 4.4 activates under the lock contention
    - **Inbox arm**: seed V1 SQLite inbox; race two inbox provisioners; exactly one synthetic V1 row + one applied V2 row
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - No production-code change expected — `BEGIN IMMEDIATE` + TOCTOU re-check handles both box types
    - If test reveals gap, add defence-in-depth in the runner

### Task 4.8a: SQLite whole-chain rollback on mid-chain failure

- [x] **TEST + IMPLEMENT: SQLite runner rolls back all migrations and history rows on mid-chain failure**
  - **USE COMMAND**: `/test-first when sqlite runner fails mid chain it should roll back all migrations and history rows atomically`
  - Test location: `tests/Paramore.Brighter.Sqlite.Tests/BoxProvisioning/`
  - Test file: `When_sqlite_runner_fails_mid_chain_it_should_roll_back_all_migrations_and_history_rows_atomically.cs`
  - Test should verify: same as Task 1.8a but SQLite; relies on `BEGIN IMMEDIATE` + `COMMIT` transactional DDL
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - No production change expected — `SqliteBoxMigrationRunner` wraps the whole chain in `BEGIN IMMEDIATE TRANSACTION` ... `COMMIT` (per Task 4.4)
    - If assertions fail, fix to keep history INSERTs inside the transaction

### Task 4.9: SQLite spec-0023-era transition test (AC-6)

- [x] **TEST + IMPLEMENT: SQLite table with existing V1-marked history transitions cleanly to V7**
  - **USE COMMAND**: `/test-first when sqlite table has spec 0023 era history at v1 it should transition cleanly to v7`
  - Test location: `tests/Paramore.Brighter.Sqlite.Tests/BoxProvisioning/`
  - Test file: `When_sqlite_table_has_spec_0023_era_history_at_v1_it_should_transition_cleanly_to_v7.cs`
  - Test should verify: same as Task 1.9 / 2.8 / 3.8 but SQLite-specific; crucially `IdempotencyCheckSql` scalar returns >0 for each V2..V7 column already present → each `UpScript` is skipped; history rows inserted for V2..V7 without any `ALTER TABLE` executing
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Validate the IdempotencyCheckSql path under the "all columns already present" condition
    - If the runner mis-handles this path, fix

---

## Phase 5: Spanner degenerate runner rework (closes spec 0023 R4)

### Task 5.1: Spanner fresh-install path stamps V_latest with IsMigrationAppliedAsync gate

- [x] **TEST + IMPLEMENT: Spanner fresh-install path creates table and stamps V_latest idempotently**
  - **USE COMMAND**: `/test-first when spanner fresh install runs it should create table and stamp v_latest and skip duplicate history insert`
  - Test location: `tests/Paramore.Brighter.Gcp.Tests/Spanner/BoxProvisioning/`
  - Test file: `When_spanner_fresh_install_runs_it_should_create_table_and_stamp_v_latest_and_skip_duplicate_history_insert.cs`
  - Test should verify:
    - Outbox: absent-table fresh install runs current builder DDL + inserts history row at V=7 with description starting with "fresh install"
    - Inbox: absent-table fresh install stamps V=2
    - Given an already-inserted history row at V_latest, a second `MigrateAsync` call is a no-op (no duplicate history row, no exception) — verifies `IsMigrationAppliedAsync` gate before INSERT
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Modify `src/Paramore.Brighter.BoxProvisioning.Spanner/SpannerBoxMigrationRunner.cs` per ADR §6:
      - Define `V_LATEST_OUTBOX = 7`, `V_LATEST_INBOX = 2` as constants; choose via `BoxType`
      - Fresh path: `CreateDdlCommand` for current DDL + `IsMigrationAppliedAsync` check before history INSERT
      - Keep gRPC status-code handling from spec 0023 R4 (AlreadyExists catch)
    - Closes spec 0023 R4 (history INSERT no longer unprotected)

### Task 5.2: Spanner existing-table-without-history path gated by discriminator

- [x] **TEST + IMPLEMENT: Spanner existing table without history throws when discriminator absent, stamps V_latest when present**
  - **USE COMMAND**: `/test-first when spanner existing table has no history it should throw if discriminator absent and stamp v_latest if present`
  - Test location: `tests/Paramore.Brighter.Gcp.Tests/Spanner/BoxProvisioning/`
  - Test file: `When_spanner_existing_table_has_no_history_it_should_throw_if_discriminator_absent_and_stamp_v_latest_if_present.cs`
  - Test should verify:
    - Given a Spanner table without `HeaderBag` and no history → throws `ConfigurationException` with "not a Brighter outbox" message
    - Given a Spanner table with `HeaderBag` and no history → stamps history at V_latest with description `"bootstrap: spanner-assumed-current (no known legacy installations, A-2)"`
    - Analogous for inbox with `CommandBody` discriminator
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `SpannerBoxMigrationRunner.MigrateAsync`: on `tableState { TableExists:true, HistoryExists:false }` query `information_schema.columns` for discriminator
    - Throw `ConfigurationException` on discriminator absence
    - Otherwise insert history at V_latest with the ADR §6 description
    - Discriminator query: case-sensitive Ordinal comparison (Spanner builder DDL uses PascalCase column names)

### Task 5.3: Spanner existing-table-with-history is a no-op or throws on version mismatch

- [x] **TEST + IMPLEMENT: Spanner existing table with history is no-op at V_latest and throws on out-of-sync version**
  - **USE COMMAND**: `/test-first when spanner existing table has history it should no-op at v_latest and throw on out of sync installed version`
  - Test location: `tests/Paramore.Brighter.Gcp.Tests/Spanner/BoxProvisioning/`
  - Test file: `When_spanner_existing_table_has_history_it_should_no_op_at_v_latest_and_throw_on_out_of_sync_installed_version.cs`
  - Test should verify:
    - `MAX(V) == V_latest` → no-op, returns cleanly
    - `MAX(V) > V_latest` (seed `V=99`) → throws `ConfigurationException` with "migration list out of sync"
    - `MAX(V) < V_latest` (e.g. 0 or 1 from prior buggy state) → should **not** happen per ADR §6; documented in test as "undefined path: manual recovery required"
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `SpannerBoxMigrationRunner.MigrateAsync`: on `tableState { TableExists:true, HistoryExists:true }` read `MAX(V)` and branch per ADR §6
    - No `IAmABoxMigration` list is needed — Spanner provisioner holds `V_latest` as a constant
    - **Delete** the Phase-0.3 compile bridges now that nothing references them:
      - `src/Paramore.Brighter.BoxProvisioning.Spanner/SpannerOutboxMigrations.cs`
      - `src/Paramore.Brighter.BoxProvisioning.Spanner/SpannerInboxMigrations.cs`
    - Remove any `SpannerOutboxMigrations.All(...)` / `SpannerInboxMigrations.All(...)` call sites in `SpannerOutboxProvisioner` / `SpannerInboxProvisioner` / `SpannerBoxMigrationRunner`
    - Build must be green after this task — if a caller still references the deleted migrations classes, surface and fix it here

---

## Phase 6: Cross-backend payload-mode-mismatch tests (closes spec 0023 R5)

### Task 6.1: Postgres inbox + outbox payload-mode-mismatch tests

- [x] **TEST + IMPLEMENT: Postgres provisioners detect payload-mode mismatch against existing table**
  - **USE COMMAND**: `/test-first when postgres provisioner runs against existing table with mismatched payload mode it should throw configuration exception`
  - Test location: `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning/`
  - Test file: `When_postgres_provisioner_runs_against_existing_table_with_mismatched_payload_mode_it_should_throw_configuration_exception.cs`
  - Test should verify:
    - Given a text-mode outbox table; provisioner configured for JSONB → throws `ConfigurationException` from `PostgreSqlPayloadModeValidator`
    - Given a bytea-mode outbox; provisioner configured for text → throws
    - Symmetric for inbox (CommandBody type mismatch — if Postgres inbox supports payload modes; otherwise scope to outbox only)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - No production changes expected — `PostgreSqlPayloadModeValidator` already exists per spec 0023
    - Test uses the existing validator through the provisioner path

### Task 6.2: MySQL outbox payload-mode-mismatch test

- [x] **TEST + IMPLEMENT: MySQL provisioner detects binary-vs-text Body mode mismatch**
  - **USE COMMAND**: `/test-first when mysql provisioner runs against existing outbox with mismatched payload mode it should throw configuration exception`
  - Test location: `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning/`
  - Test file: `When_mysql_provisioner_runs_against_existing_outbox_with_mismatched_payload_mode_it_should_throw_configuration_exception.cs`
  - Test should verify: text vs binary mismatch detected via existing `MySqlPayloadModeValidator`
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should: no production changes expected

### Task 6.3: SQLite outbox payload-mode-mismatch test

- [x] **TEST + IMPLEMENT: SQLite provisioner detects binary-vs-text Body mode mismatch via SqlitePayloadModeValidator**
  - **USE COMMAND**: `/test-first when sqlite provisioner runs against existing outbox with mismatched payload mode it should throw configuration exception`
  - Test location: `tests/Paramore.Brighter.Sqlite.Tests/BoxProvisioning/`
  - Test file: `When_sqlite_provisioner_runs_against_existing_outbox_with_mismatched_payload_mode_it_should_throw_configuration_exception.cs`
  - Test should verify: text-mode existing table + provisioner configured for binary → throws `ConfigurationException` via `SqlitePayloadModeValidator`; symmetric case for binary-mode existing vs text-configured
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - No production changes expected — `src/Paramore.Brighter.BoxProvisioning.Sqlite/SqlitePayloadModeValidator.cs` already exists from spec 0023; test exercises the existing validator through the provisioner path
    - Mirror the MSSQL test at `When_mssql_outbox_provisioner_detects_payload_mode_mismatch_it_should_throw.cs`

### Task 6.4: Spanner outbox payload-mode-mismatch test

- [x] **TEST + IMPLEMENT: Spanner provisioner detects payload-mode mismatch via SpannerPayloadModeValidator**
  - **USE COMMAND**: `/test-first when spanner provisioner runs against existing outbox with mismatched payload mode it should throw configuration exception`
  - Test location: `tests/Paramore.Brighter.Gcp.Tests/Spanner/BoxProvisioning/`
  - Test file: `When_spanner_provisioner_runs_against_existing_outbox_with_mismatched_payload_mode_it_should_throw_configuration_exception.cs`
  - Test should verify: binary-vs-text mismatch throws via `SpannerPayloadModeValidator`
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - No production changes expected — `src/Paramore.Brighter.BoxProvisioning.Spanner/SpannerPayloadModeValidator.cs` already exists from spec 0023
    - Mirror the MSSQL test pattern

---

## Phase 7: Documentation + release notes

### Task 7.1: Update `.agent_instructions/box_provisioning.md` with the "new column → new migration" rule

- [x] **IMPLEMENT: Add rule to box_provisioning.md enforcing migration + drift test for every new column** — closed by `c3f0c6e70`
  - File: `.agent_instructions/box_provisioning.md`
  - Add prominent section near the top:
    - "Every column added to a `*OutboxBuilder` or `*InboxBuilder` MUST ship with a new `V(N+1)` migration entry in the corresponding `*Migrations` class."
    - "The new migration MUST include `LogicalColumns`, `SourceReference` (commit SHA + PR), and `IdempotencyCheckSql` for SQLite (null for other backends)."
    - "The per-backend drift test asserts `latest_migration.LogicalColumns ∪ housekeeping == builder.ExpectedColumns`. CI will block if they drift."
  - Reference ADR 0057 and this spec's README.md archaeology
  - No tests — doc change

### Task 7.2: Release notes entry for `IAmABoxMigration` source-breaking additions

- [x] **IMPLEMENT: Release notes entry for IAmABoxMigration interface break** — closed by `5ced21cd4`
  - File: `release_notes.md` at repository root (confirmed by file listing — `CHANGELOG.md` and `docs/release-notes/` do not exist in this repo)
  - Entry must describe:
    - `IAmABoxMigration` now requires `LogicalColumns`, `SourceReference`, `IdempotencyCheckSql` members
    - External implementors must add these on recompile — same break model as spec 0023's `SchemaName` addition
    - Only SQLite uses `IdempotencyCheckSql`; MSSQL/Postgres/MySQL leave it null
    - Link to ADR 0057 "Consequences → Negative" for rationale
  - Also note: spec-0023-era `__BrighterMigrationHistory` rows at V=1 are still valid; the runner recognises them and advances to V7 without re-running DDL (AC-6)
  - No tests — doc change

### Task 7.3: Close the loop on spec 0023 findings

- [x] **IMPLEMENT: Update spec 0023's review-code.md to mark R2, R4, R5 as closed by spec 0027** — closed by `e42b8c5ef`
  - File: `specs/0023-box_database_migration/review-code.md`
  - Mark R2 (TOCTOU) as closed by Phase 1.4 / 2.4 / 3.4 / 4.4 TOCTOU re-check (and concurrent-bootstrap tests 1.8 / 2.7 / 3.7 / 4.6)
  - Mark R4 (Spanner history INSERT) as closed by Phase 5.1
  - Mark R5 (payload-mode tests MSSQL-only) as closed by Phase 6
  - Reference the ADR 0057 sections and this tasks.md for each
  - No tests — doc change

---

## Acceptance checklist (maps to requirements.md AC-*)

- [x] **AC-1** outbox upgrade (V1..V6 → V7): covered by 1.6, 2.6, 3.6, 4.6
- [x] **AC-2** inbox upgrade (V1 → V2): covered by 1.7 (MSSQL), 3.6a (MySQL), 4.7 (SQLite); Postgres inbox is V1-only (ADR §1) so no upgrade applies
- [x] **AC-3** fresh install produces V_latest + single history row: covered by **Task 0.3a** retargets of `When_*_runs_on_fresh_database_*` tests for MSSQL/PostgreSQL/MySQL/SQLite (relational backends) + **Task 5.1** for Spanner. Bootstrap-at-V_k tests (1.6/2.6/3.6/4.6) do **not** cover fresh install — they were narrowed in F10 to focus on legacy-table upgrade only
- [x] **AC-4** no-op re-run (idempotency): covered by existing `When_*_runs_on_already_provisioned_database_it_should_be_idempotent` tests after Task 0.3a retargets them to `V_latest`, plus SQLite idempotency verified implicitly by the AC-6 arm in Task 4.9
- [x] **AC-5 / AC-18** concurrent bootstrap (outbox + inbox): covered by 1.8, 2.7, 3.7, 4.8 — each now includes both outbox and inbox arms
- [x] **AC-6 / AC-19** spec-0023-era transition: covered by 1.9, 2.8, 3.8, 4.9
- [x] **AC-7** Spanner fresh-only: covered by 5.1, 5.2
- [x] **AC-8** Spanner with history: covered by 5.3
- [x] **AC-9** all 4 backends have V1..V7 outbox: verified by 1.2, 2.2, 3.2, 4.2
- [x] **AC-10** all 4 backends have V1..V2 inbox (Postgres V1 only): verified by 1.3, 2.3, 3.3, 4.3
- [x] **AC-11** backend-specific housekeeping preserved: verified by drift tests 1.1, 2.1, 3.1, 4.1
- [x] **AC-12** migration SourceReference populated: verified by 1.2, 1.3, 2.2, 2.3, 3.2, 3.3, 4.2, 4.3
- [x] **AC-13** ADR 0057 created and accepted: done (pre-tasks)
- [x] **AC-14** `.agent_instructions/box_provisioning.md` rule added: Task 7.1
- [x] **AC-15** per-backend fresh-install test asserts V_latest + history row: covered exclusively by **Task 0.3a** retargeting the existing `When_*_runs_on_fresh_database_*` tests for relational backends + **Task 5.1** for Spanner. Bootstrap-at-V_k tests (1.6/2.6/3.6/4.6) do not cover fresh install
- [x] **AC-16** per-backend bootstrap-at-V_k tests (k ∈ {1,3,5,7} outbox; k ∈ {1,2} inbox): 1.6/1.7, 2.6, 3.6/3.6a, 4.6/4.7
- [x] **AC-17** per-backend idempotency test: existing `When_*_runs_on_already_provisioned_database_it_should_be_idempotent` tests (retargeted in Task 0.3a) + 4.9 IdempotencyCheckSql path
- [x] **NFR-3** migration completes within `MigrationLockTimeout` (30s): verified by Task 1.8b (MSSQL reference, tight 5s bound)
- [x] **ADR §5a** whole-chain rollback on mid-chain failure: verified by Task 1.8a (MSSQL), Task 2.7a (Postgres), Task 4.8a (SQLite); MySQL's per-migration-commit recovery verified by Task 3.4

---

## Phase 8: Boy Scout follow-ups from PR #4039 reviews (post-Phase-7)

A re-read of all prior Claude code reviews on PR #4039 after Phase 7 closed surfaced **15 items closed** against `f7d6e7a55` (originally 12; Items M and N added 2026-05-05 when the design for Item D pivoted from a public static helper to a per-backend advisory-lock abstraction — see ADR 0057 §5b — pulling the MySQL and MSSQL diagnostics into the same shape; Item N-bis added during Item N's GREEN to relocate `ValidateLockTimeout` from the runner ctor into `MsSqlAdvisoryLock.AcquireAsync` per ADR §5b's prescription). Most older review findings already closed by Phases 2–6 work (R1/R2/R4/R5); these 15 are the Phase-8 set. **Decision: apply the Boy Scout rule** — fix all in this PR rather than deferring. Both specs (0023 and 0027) deliver the feature; we will not ship with technical debt around the lock primitives.

Each item: TDD `When_..._should_...` test first (RED), then implementation (GREEN), then commit. Source-breaking items (F, G) roll into the existing Breaking Change PR + `release_notes.md` alongside the spec 0027 source-break section. Items D, M, N introduce additive (non-breaking) public abstractions — documented in `release_notes.md` under the spec 0027 "Additive" subsection. Spanner runner is exempt from H/I/L/D/M/N because it ignores the migrations parameter and has no advisory-lock primitive per ADR 0057 §6 (degenerate runner). SQLite is exempt from D/M/N because it serializes via `BEGIN IMMEDIATE` writer slot, not an advisory lock (per ADR §5).

**Order of operations**: Tier 1 first (correctness bugs that hit production), then tier 2 (consolidate breaking-change story), then tiers 3–5.

### Tier 1 — correctness (✅ all closed)

- [x] **Item A** — MySQL `GET_LOCK` 64-char length guard. Extract `MySqlMigrationLockName.For(string tableName)` helper; ≤46-char names keep historical `BrighterMigration_<name>` form (preserves interlock with running deployments); >46-char names get SHA-256 hashed suffix → 64 chars exactly, collision-resistant. Both call sites in `MySqlBoxMigrationRunner` (Acquire + Release) delegate. Closes review #46 M1 / #45 M1. Commit `d71162ed0`.
- [x] **Item B** — MSSQL `EnsureHistoryTableAsync` `schema_id` filter. Filter `sys.tables WHERE name = '__BrighterMigrationHistory' AND schema_id = SCHEMA_ID('dbo')` and schema-qualify every history-table reference (CREATE / SELECT / INSERT) in `MsSqlBoxMigrationRunner` + `MsSqlBoxDetectionHelpers`. New `HISTORY_TABLE_SCHEMA = "dbo"` const. Closes review #46 M3 / #39 B4 / #42 #6. Commit `7c6b32fe8`.
- [x] **Item C** — Postgres history-table schema-qualification. Same shape as B for Postgres: every reference → `"public"."__BrighterMigrationHistory"` so `search_path` can't scatter rows across schemas. New `HISTORY_TABLE_SCHEMA = "public"` const. Closes review #46 N3. Commit `950a12b3f`.
- [x] **Item E** — `sp_getapplock` `(int)TotalMilliseconds` overflow + negative guard. New static `ValidateLockTimeout(TimeSpan)` called from a primary-ctor field initializer; rejects negative spans and spans whose `TotalMilliseconds` exceeds `int.MaxValue` (~24.85 days) with `ArgumentOutOfRangeException`. `AcquireLockAsync` reads `_lockTimeout`. Closes review #47 #2. Commit `be910cf16`.

### Tier 2 — public API source-break (✅ all closed)

- [x] **Item F** — `IAmABoxMigration.LogicalColumns` (and `BoxMigration.LogicalColumns`) `ISet<string>` → `IReadOnlyCollection<string>`. Public surface no longer exposes Add/Remove/Clear. All 7 backend `Cumulative(int)` helpers also switched return type — `ISet<string>` is NOT assignable to `IReadOnlyCollection<T>` (because `ICollection<T>` doesn't inherit from `IReadOnlyCollection<T>`); bodies unchanged because `HashSet<T>` IS `IReadOnlyCollection<T>`. Internal `HashSet` with backend-appropriate `StringComparer` (Ordinal vs OrdinalIgnoreCase per ADR 0057 §1) preserved. `release_notes.md` was already forward-written documenting `IReadOnlyCollection<string>`, so the implementation simply caught up. Closes review #46 M5. Commit `b8a629dc3`.
- [x] **Item G** — Consolidate dual migration-lock-timeout API on `BrighterBuilderBoxProvisioningExtensions.cs`. Removed the `TimeSpan? migrationLockTimeout = null` parameter from `UseBoxProvisioning`; the timeout is now set exclusively through `BoxProvisioningOptions.MigrationLockTimeout` inside the configure delegate, with the documented requirement that callers assign it BEFORE invoking any backend `AddXxxOutbox`/`AddXxxInbox` method (backend extensions capture the timeout at registration time — the ordering bug fixed by this change). New reflection-based `UseBoxProvisioningPublicApiTests` (3 facts) pins the consolidated signature: exactly one overload, parameters `(IBrighterBuilder, Action<BoxProvisioningOptions>)`, no parameter named `migrationLockTimeout`. All 6 in-tree call sites used the default and required no source change. `release_notes.md` extended with a "Source-breaking change: `UseBoxProvisioning` overload consolidation" subsection under spec 0027. Closes review #47 #6 / #37 #5.

### Tier 3 — defensive (✅ all closed)

- [x] **Item H** — Fresh path asserts `migrations[0].Version == 1` across 4 relational runners (Spanner exempt — degenerate, ignores migrations parameter per ADR §6). Each `RunFreshPathAsync` checks `migrations[0].Version != 1` after the empty-list guard and throws `ConfigurationException` with a "first migration must be V1, but … starts at V{n}" message before any DDL fires. Without the guard, a misordered or filtered list (e.g. `realMigrations.Skip(1)`) would silently execute V2's `ALTER TABLE … ADD COLUMN` against a non-existent table — the runner would surface the underlying provider's "object not found" exception (SQL 208 / 42P01 / 1146 / SQLITE_ERROR=1) instead of the actionable misconfiguration error. Closed by 4 new RED-then-GREEN integration tests (`When_{mssql,postgres,mysql,sqlite}_runner_fresh_path_is_called_with_migrations_not_starting_at_v1_it_should_throw`). Closes review #46 N1. Commit `dda187313`.
- [x] **Item I** — Validate migrations list is monotonic ascending with no gaps/duplicates (4 backends; Spanner exempt). New private static `ValidateMigrationsMonotonic(schemaName, tableName, migrations)` helper in each of MSSQL / Postgres / MySQL / SQLite runners; called as the FIRST action of `MigrateAsync` (before opening a connection) so the rule applies uniformly across fresh / bootstrap / normal paths. Throws `ConfigurationException` when the list is not contiguous and strictly ascending — catches duplicates, gaps, and out-of-order pairs uniformly with a "V{prev} followed by V{curr} (expected V{prev+1})" message. Item H's `migrations[0].Version == 1` guard is **complementary, not redundant** — Item H rejects non-V1-rooted lists; Item I rejects malformed pairwise sequences. 4 new RED-then-GREEN integration tests `When_{backend}_runner_is_called_with_non_monotonic_migrations_it_should_throw` each contain 3 facts (duplicate, gap, descending) and assert the box table was NOT created (proves guard fires before any DDL). Closes review #46 N2. Commit `929f5ca43`.

### Tier 4 — diagnostics + advisory-lock abstraction (✅ all closed)

Tier 4 grew on 2026-05-05 when the design for Item D was reviewed: rather than a one-off public static helper or `protected virtual` test seam (both rejected as implementation-detail coupling), we extract a per-backend `I*AdvisoryLock` abstraction so the runner's lock collaborator is genuinely substitutable for tests **and** for advanced integrators (custom connection-pool sharing, Vault-driven lock keys, etc.). The same shape pulls in the MySQL and MSSQL diagnostics that were previously implicit. ADR 0057 §5b documents the design.

- [x] **Item D** — Extract `IPostgreSqlAdvisoryLock` (public interface + default `PostgreSqlAdvisoryLock` impl) in `Paramore.Brighter.BoxProvisioning.PostgreSql`. `AcquireAsync(NpgsqlConnection, string lockKey, TimeSpan timeout, CT)` (throws `TimeoutException` on deadline) + `Task<bool> ReleaseAsync(NpgsqlConnection, string lockKey, CT)` returns the bool result of `pg_advisory_unlock`. `PostgreSqlBoxMigrationRunner` ctor adds two additive optional params: `IPostgreSqlAdvisoryLock? advisoryLock = null` and `ILogger? logger = null`. Runner's `finally` block: `var held = await _advisoryLock.ReleaseAsync(...); if (!held) _logger.LogWarning(...)`. Closes review #46 M2 / #45 M2 + addresses test-coupling concerns by removing the static `private` seam. Commit `81810bff6`.
- [x] **Item M** — Extract `IMySqlAdvisoryLock` (public interface + default `MySqlAdvisoryLock` impl) in `Paramore.Brighter.BoxProvisioning.MySql`. Same shape as Item D, but `Task<bool?> ReleaseAsync(...)` returns nullable bool because MySQL `RELEASE_LOCK` has three outcomes: `1` = released by this session (true); `0` = lock exists but held by another session (false); `NULL` = lock didn't exist (null). All three non-`true` results are diagnostic anomalies — runner logs warning at Warning level naming both result code and lock key. `MySqlMigrationLockName.For(tableName)` (Item A) stays as the lock-name derivation; abstraction owns only the `GET_LOCK` / `RELEASE_LOCK` SQL. `MySqlBoxMigrationRunner` ctor adds two additive optional params (lock + logger). Cross-backend symmetry: same release-diagnostic gap as Postgres. Commit `69657b151`.
- [x] **Item N** — Extract `IMsSqlAdvisoryLock` (public interface + default `MsSqlAdvisoryLock` impl) in `Paramore.Brighter.BoxProvisioning.MsSql`. **Different shape from D and M**: MSSQL's `sp_getapplock` is invoked with `@LockOwner = 'Transaction'` (per ADR 0057 §5a) — the lock is automatically released when the wrapping `SqlTransaction` commits or rolls back. There is no separate `sp_releaseapplock` call, so the abstraction is **acquire-only**: `AcquireAsync(SqlConnection, SqlTransaction, string lockResource, TimeSpan timeout, CT)`. Today's runner collapses every `result < 0` into a generic `TimeoutException` — losing the deadlock and parameter-error signals. New: `AcquireAsync` throws distinguishable exception types per `sp_getapplock` return code: `TimeoutException` for `-1`, `OperationCanceledException` for `-2`, a new public `MigrationLockDeadlockException` for `-3`, `ArgumentException` for `-999`. The 255-char `@Resource` length guard absorbs into the abstraction's acquire path. `MsSqlBoxMigrationRunner` ctor adds two additive optional params (lock + logger). SQLite exempt (writer slot, not advisory lock); Spanner exempt (degenerate). Commit `2f727772d`.
  - [x] **Item N-bis** — Move `ValidateLockTimeout` from `MsSqlBoxMigrationRunner` ctor into `MsSqlAdvisoryLock.AcquireAsync` (per ADR §5b's prescription that lock-timeout validation absorbs into the abstraction's acquire path — second-half deferral from Item N's GREEN). The negative-span guard + `TotalMilliseconds > int.MaxValue` overflow guard move into `AcquireAsync` as the FIRST guard, before the 255-char `@Resource` length check. The runner's static helper is deleted; field initializer becomes plain `_lockTimeout = lockTimeout;`. **Behaviour change**: pre-N-bis a bad timeout failed at runner construction; post-N-bis it fails on first `MigrateAsync` call. Test re-routing: the 3 Item E facts move from `MsSqlBoxMigrationRunnerLockTimeoutValidationTests` to a new `MsSqlAdvisoryLockTimeoutValidationTests` exercising the abstraction directly. Commit `9cb05aedc`.
- [x] **Item K** — Hosted service log includes box table name. New `string BoxTableName { get; }` on `IAmABoxProvisioner` (additive on a still-unreleased spec 0027 interface), implemented on all 10 production provisioners by delegating to `configuration.OutBoxTableName` / `.InBoxTableName`. `BoxProvisioningHostedService.StartAsync` lifecycle logs (start / success / error / `ConfigurationException` message) all now name the table. 3 RED-then-GREEN facts in `BoxProvisioningHostedServiceLoggingTests` (in `Paramore.Brighter.BoxProvisioning.Tests`) pin the contract via `CapturingLogger` + `StubBoxProvisioner` test doubles. Closes review #46 / #41 #6. Commit `eb4d9b450`.

### Tier 5 — cleanup (✅ all closed)

- [x] **Item J** — Remove `Console.WriteLine` in samples `SchemaCreation.cs:93,130,142` (added by PR commit `a6ed373e2`). Threads `ILogger<IHost>` resolved by `CheckDbIsUp` from the service provider down through `WaitToConnect` and the two `CreateXxxIfNotExists` helpers; replaces three `Console.WriteLine` calls with structured `LogInformation` (healthcheck retry) + `LogError` (database-creation failure). Also fixes a pre-existing copy-paste in `CreateSalutationsIfNotExists` ("creating Greetings tables" → "creating Salutations database"). Closes review #47 #10. Commit `1d6634fc5`.
- [x] **Item L** — Remove redundant `IsMigrationAppliedAsync` in normal path (4 backends; Spanner exempt). Each `Run{Backend}NormalPathAsync` reads `maxVersion` from `__BrighterMigrationHistory` once at the top, then iterates `migrations`, skipping any entry with `migration.Version <= maxVersion`. The subsequent `IsMigrationAppliedAsync` row-existence check inside the same transaction was redundant: with monotonically-ascending contiguous migrations (Items H + I) any V_n with `n <= maxVersion` is by definition already applied, and nothing greater than `maxVersion` can be in history because the transaction has held the advisory lock (or SQLite's writer slot) since the read. Deletes 4 call sites + 4 now-unused private helpers across MSSQL / Postgres / MySQL / SQLite. Spanner runner exempt (degenerate; ignores migrations parameter per ADR §6). **Test approach**: integration only — no abstraction extraction (D/M/N's lock abstraction does not help here; this is pure code deletion verified by re-running each backend's existing BoxProvisioning suite). All counts unchanged. Decision (2026-05-05): kept L in this PR's scope; did not extract a `IBoxMigrationHistoryStore` abstraction just to unit-test the deletion — that would have been real architectural scope creep. Closes review #47 #5. Commit `1c7cd16dc`.

## Phase 9: Boy Scout follow-ups from PR #4039 third review — Tier 1 correctness (post-Phase-8)

A third pass on PR #4039 by the human reviewer (GitHub comment 4386251619, 2026-05-06) surfaced **7 numbered findings** plus smaller observations against `e16fef083`. Phase 8 closed the prior reviewers' set; this third review found new gaps the prior passes missed (lock-key schema scoping across Postgres/MySQL, Spanner concurrent fresh-install TOCTOU, identifier injection vector in MySQL migration scripts, plus three smaller defensive items). **Decision: apply the Boy Scout rule again** — fix all in this PR rather than deferring. Phase 9 covers the three Tier 1 items (correctness + injection defence in depth); Phase 10 covers the three Tier 2 defensive items. The smaller observations (history-schema sentinel, `Random.Shared`, double-connection on Azure AD path, V_latest drift test, SchemaName divergence on SQLite/Spanner) remain deferred to follow-up issues — none are correctness-affecting, and the V_latest drift test is the only one worth promoting later.

Each item: TDD `When_..._should_...` test first (RED), then implementation (GREEN), then commit. Source-break analysis intentionally omitted — spec 0027 has not yet shipped, so all public surfaces introduced by Phase 8 (`MySqlMigrationLockName.For`, the `I*AdvisoryLock` interfaces, `IAmABoxProvisioner.BoxTableName`, etc.) remain unreleased and may be changed freely within this PR. The Phase 8 source-break notes in `release_notes.md` (already covering `IAmABoxMigration.LogicalColumns` and the `UseBoxProvisioning` overload consolidation) are the ship boundary; anything else is in-flight.

**Numbering**: `#46-3 #N` denotes finding `#N` from the third review pass by reviewer `#46` (GitHub comment 4386251619, 2026-05-06). Phase 8's bare `#NN` numbers were per-comment; Phase 9/10 disambiguates because the same reviewer has commented multiple times on the same PR.

**Order of operations**: Item O (lock-key schema scoping — affects parallel-provisioner correctness across Postgres + MySQL), then Item P (Spanner race), then Item Q (identifier validator — defence in depth, affects all backends).

### Tier 1 — correctness + injection defence in depth

- [x] **Item O** — Schema in advisory lock key (Postgres + MySQL). MSSQL already includes `effectiveSchema` in `lockResource = "BrighterMigration_{effectiveSchema}.{tableName}"` (`MsSqlBoxMigrationRunner.cs:90`). Postgres (`PostgreSqlBoxMigrationRunner.cs:100`) and MySQL (`MySqlBoxMigrationRunner.cs:100` via `MySqlMigrationLockName.For(...)`) omit the schema, so e.g. `public.Outbox` and `billing.Outbox` share an advisory lock and serialize unnecessarily. Postgres fix: change `lockKey = $"BrighterMigration_{tableName}"` to `lockKey = $"BrighterMigration_{effectiveSchema}.{tableName}"` matching the MSSQL shape. MySQL fix: change `MySqlMigrationLockName.For` signature to `For(string? schema, string tableName)`; the simple-form length check still applies after schema folding (budget shrinks by `schema.Length + 1` for the `.` separator), and the long-form hash input includes both so distinct (schema, table) pairs cannot collide. Both runner call sites pass `effectiveSchema`. Two new RED-then-GREEN concurrency integration tests `When_postgres_runner_runs_two_provisioners_in_distinct_schemas_they_should_not_block_each_other` + MySQL twin use a `Holding{PG,MySql}AdvisoryLock` decorator (real lock + park-on-gate) on provisioner A while provisioner B (different schema, 1s lockTimeout) attempts to provision in parallel — RED on the buggy lock-key shape (B times out after 1s on the shared key); GREEN on the schema-aware fix (B uses its own schema-prefixed key, completes in <500ms). Downstream tests that hard-coded the pre-fix lock-key shape updated: `When_postgres_advisory_unlock_returns_false_..._normally.cs:78` (now `BrighterMigration_public.<table>`), `When_mysql_advisory_release_lock_returns_non_true_..._normally.cs:114` (now `MySqlMigrationLockName.For("BrighterTests", _tableName)`). MySQL helper-tests `MySqlMigrationLockNameTests` updated: 3 existing facts re-parameterised with schema; 1 new `When_two_distinct_schemas_share_a_table_name_lock_names_should_differ` fact pins the schema-distinction behaviour; 1 new fact pins the null-schema fallback. ADR 0057 §5b updated: MySQL row in the per-backend table now mentions `For(schema, tableName)` (schema parameter added in Item O); the "What does not absorb" derivation summary now reflects schema folding for Postgres + MySQL + MSSQL. Postgres BoxProvisioning 36/36 PASS per TFM; MySQL BoxProvisioning + helper-tests 40/40 PASS. Closes review #46-3 #1. Commit `c4ed8ff86`.
- [x] **Item P** — Spanner concurrent history-row race (fresh-install + bootstrap). Two TOCTOU pairs in `SpannerBoxMigrationRunner` use the same `IsMigrationAppliedAsync` → `InsertHistoryRowAsync` shape and require the same fix; split into two sub-items so each branch has a dedicated RED test, but they share an implementation idea (a single helper that wraps `InsertHistoryRowAsync` in a benign-race filter; both call sites delegate). Spanner's history-row insert uses `SpannerParameter.CommitTimestamp` (`InsertHistoryRowAsync:216`) so the row is only visible to other sessions after the implicit transaction commits; this is the basis for the "loser-replica hits `AlreadyExists`" mental model — both replicas can pass `IsMigrationAppliedAsync` (which reads at the older commit timestamp) before either commits, then race on the insert and the second one fails with `AlreadyExists` on the PK `(BoxTableName, MigrationVersion)`.
  - [x] **Item P-fresh** — `FreshInstallAsync:111-115` (`SpannerBoxMigrationRunner.cs`). Two concurrent fresh-installing replicas both see no row at `vLatest`, both insert, the loser hits `AlreadyExists` which currently surfaces as an unrecoverable startup error. Fix: wrap `InsertHistoryRowAsync` in `try { ... } catch (SpannerException ex) when (ex.RpcException.StatusCode == StatusCode.AlreadyExists) { /* benign race — concurrent fresh-installer already stamped */ }` mirroring `ExecuteDdlSafeAsync`:166's shape. The existing serial-rerun test (`When_spanner_fresh_install_runs_..._skip_duplicate_history_insert`) only covers the second-call-takes-Normal-Path scenario. New RED-then-GREEN concurrent integration test `When_spanner_runner_runs_two_concurrent_fresh_installers_neither_should_throw` launches two `MigrateAsync` calls in parallel against an empty database and asserts both succeed with exactly one history row at `V_latest`. Initially implemented as an inline `try`/`catch` (per CLAUDE.md "don't introduce abstractions beyond what the task requires"); P-bootstrap then extracted both call sites to the shared `InsertHistoryRowToleratingDuplicateAsync` helper as planned. RED on the buggy code reproduced reliably on the Spanner emulator (~700ms per run, no artificial delay needed); GREEN after the wrap. Spanner BoxProvisioning 17/17 PASS per TFM. Closes review #46-3 #2 (fresh-install half). Commit `b90947f92`.
  - [x] **Item P-bootstrap** — `BootstrapExistingTableAsync:140-144` (`SpannerBoxMigrationRunner.cs`). Same TOCTOU shape: two replicas both see an existing-without-history table (e.g. spec-0023-era pre-existing user tables), both pass the `IsMigrationAppliedAsync(vLatest)` check, both attempt the bootstrap insert, the loser hits `AlreadyExists`. Fix extracts the wrap shape into a shared private static helper `InsertHistoryRowToleratingDuplicateAsync` and routes both `BootstrapExistingTableAsync` and `FreshInstallAsync` (P-fresh's previously-inline `try`/`catch`) through it — single implementation, two call sites. Discriminator-column validation at `:132-138` runs before the insert and is unaffected. New RED-then-GREEN concurrent integration test `When_spanner_runner_runs_two_concurrent_bootstrap_callers_against_an_existing_table_neither_should_throw` (in `tests/Paramore.Brighter.Gcp.Tests/Spanner/BoxProvisioning/`, class `SpannerConcurrentBootstrapTests`) pre-creates a Brighter-shaped outbox table via `SpannerOutboxBuilder.GetDDL(_tableName, _config.BinaryMessagePayload)` directly (no history rows — forces both provisioners onto the bootstrap path), launches two `SpannerOutboxProvisioner.ProvisionAsync()` calls via `Task.WhenAll`, asserts no exception thrown + exactly one history row at `V_latest` whose description starts with `"bootstrap: spanner-assumed-current"` (proves bootstrap path, not fresh-install). RED reproduced reliably on the Spanner emulator without artificial delay (loser hit `Grpc.Core.RpcException: Status(StatusCode="AlreadyExists", Detail="Table BrighterMigrationHistory: Row {String(...), Int64(7)} already exists.")` at `BootstrapExistingTableAsync:157`); GREEN after the helper extraction. Spanner BoxProvisioning 18/18 PASS per TFM (was 17/17 + the new concurrency test). Closes review #46-3 #2 (bootstrap half).
- [ ] **Item Q** — `AssertSafeIdentifier` defence-in-depth helper for migration up-scripts and runner DDL. `MySqlOutboxMigrations.cs:165-173` (and the inbox mirror) inline `tableName` into SQL strings — `AddColumn` builds an `information_schema` probe and a prepared `ALTER TABLE` from `'{table}'` / `` `{table}` ``. The column names are constants (safe), but `tableName` flows from user-supplied `OutBoxTableName`/`InBoxTableName` via `IAmARelationalDatabaseConfiguration`. A name containing a single quote breaks the `information_schema` probe and is an injection vector for hosts that build the table name from external input. The same identifier-interpolation pattern exists across all four relational backends and Spanner (DDL like `ALTER TABLE` and Spanner's `CREATE TABLE` cannot parameterize identifiers), so reviewer's argument applies broadly: fragile pattern future contributors will copy. New `BoxProvisioning.Identifiers.AssertSafe(string identifier, string parameterName)` static helper in shared `Paramore.Brighter.BoxProvisioning` project; allowed character set `[A-Za-z0-9_]+` with leading character `[A-Za-z_]` (matches ADR 0057 §1's identifier rules and is a strict subset of every backend's permissible identifier syntax — over-restrictive on quoted identifiers in MSSQL/Postgres/Spanner backticked but that's deliberate defence in depth, and spec 0027 has not yet shipped so the runtime rejection of unusual identifiers does not break any released configuration). Throws `ConfigurationException` naming the offending identifier and parameter. Item Q is broken into per-target sub-items, mirroring the P-fresh / P-bootstrap split so each backend's RED → GREEN cycle commits independently:
  - [x] **Item Q-helper** — Public static `Identifiers.AssertSafe(string identifier, string parameterName)` in `Paramore.Brighter.BoxProvisioning`. Regex `^[A-Za-z_][A-Za-z0-9_]*$`, compiled, culture-invariant. Throws `ConfigurationException` whose message names both the offending identifier (renders as `<null>` for null inputs) and the parameter so contributors see the call-site that rejected it. New `AssertSafeIdentifierTests` (`tests/Paramore.Brighter.BoxProvisioning.Tests/`) — 3 facts / 10 cases: unsafe-rejection theory (single-quote, semicolon, hyphen, leading-digit, empty), safe-acceptance theory (`Outbox`, `my_outbox_v2`, `_underscore_leading`, `Outbox123`), null-rejection. BoxProvisioning.Tests **22/22** PASS net9.0 + 22/22 net10.0 (was 12/12 + 10 new). Commit `17dba0259`.
  - [x] **Item Q-mssql** — `MsSqlOutboxMigrations.All` + `MsSqlInboxMigrations.All` validate `OutBoxTableName` / `InBoxTableName` and the resolved `SchemaName` (after the `?? "dbo"` default — `"dbo"` passes the regex; only user-supplied unsafe schemas reject) at the entry of each factory. New `MsSqlMigrationsUnsafeIdentifierTests` (`tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/`) — 3 facts / 7 cases: outbox theory + inbox theory + outbox unsafe-schema fact. MSSQL BoxProvisioning **53/53** PASS net9.0 + 53/53 net10.0 (was 46/46 + 7 new) against live MSSQL container started via `docker compose -f docker-compose-mssql.yaml up -d`. Commit `17dba0259`.
  - [x] **Item Q-postgres** — `PostgreSqlOutboxMigrations.All` + `PostgreSqlInboxMigrations.All` factory-entry validation. Same shape as Q-mssql (default schema `"public"` — passes the regex; only user-supplied unsafe schemas reject). PG inbox is V1-only with no schema interpolation in its DDL (ADR 0057), so the inbox factory validates only `InBoxTableName`; the outbox factory validates both `OutBoxTableName` and the resolved `SchemaName`. New `PostgresMigrationsUnsafeIdentifierTests` (`tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning/When_postgres_migrations_are_built_with_an_unsafe_table_name_they_should_throw.cs`) — 3 facts / 7 cases mirroring `MsSqlMigrationsUnsafeIdentifierTests`: outbox theory (`O'Brien` / `1Outbox` / `my-outbox`), inbox theory (`O'Brien` / `1Inbox` / `my-inbox`), outbox unsafe-schema fact (`bad-schema`). Connection string ignored (`"Host=ignored;Database=ignored;"`) — rejection happens before any DDL renders against a live server. RED 7/7 fail (`No exception thrown`), GREEN 7/7 pass net9.0 + net10.0; full Postgres BoxProvisioning **43/43** PASS net9.0 + 43/43 net10.0 (was 36/36 + 7 new) against live container `brighter-postgres-1`. Closes review #46-3 #4 (Postgres portion).
  - [x] **Item Q-mysql** — `MySqlOutboxMigrations.All` + `MySqlInboxMigrations.All` factory-entry validation. MySQL has no schema concept (V2+ ALTER scripts run against runtime `DATABASE()` not a configured schema), so only the table identifier is validated at the factory; the existing 64-char `GET_LOCK` length guard (Item A) sits at the runner and is independent. The injection vector called out in the reviewer comment sits at `MySqlOutboxMigrations.cs:165` where a single quote in `tableName` breaks the inlined `information_schema.columns` predicate (`'{table}'`); the helper guarantees no quote ever reaches that interpolation path. New `MySqlMigrationsUnsafeIdentifierTests` (`tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning/When_mysql_migrations_are_built_with_an_unsafe_table_name_they_should_throw.cs`) — 2 facts / 6 cases (no schema fact): outbox theory (`O'Brien` / `1Outbox` / `my-outbox`), inbox theory (`O'Brien` / `1Inbox` / `my-inbox`). Connection string ignored (`"Server=ignored;Database=ignored;"`). RED 6/6 fail (`No exception thrown`), GREEN 6/6 pass net9.0; full MySQL BoxProvisioning **46/46** PASS net9.0 (was 40/40 + 6 new) against live container `brighter-mysql-1`. MySQL is net9.0-only per `BrighterTestNineOnlyTargetFrameworks`. Closes review #46-3 #4 (MySQL portion).
  - [ ] **Item Q-sqlite** — `SqliteOutboxMigrations.All` + `SqliteInboxMigrations.All` factory-entry validation. SQLite has no schema (single attached database), so only the table identifier is validated. New `When_sqlite_migrations_are_built_with_an_unsafe_table_name_they_should_throw` in `tests/Paramore.Brighter.SQLite.Tests/BoxProvisioning/`.
  - [ ] **Item Q-spanner** — `SpannerBoxMigrationRunner.MigrateAsync` entry validates the table name (Spanner has no `*Migrations.All(...)` factory — degenerate runner per ADR §6 — so the runner entry is the only place to assert). `BuildBoxDdl(boxType, tableName)` at `:118-123` and `BootstrapExistingTableAsync` both interpolate `tableName` into Spanner DDL/queries. New `When_spanner_runner_is_called_with_an_unsafe_table_name_it_should_throw` in `tests/Paramore.Brighter.Gcp.Tests/Spanner/BoxProvisioning/`.

  The relational runner-entry `AssertSafe` is defense-in-depth for callers bypassing the `*Migrations.All(...)` factory (constructing `IAmABoxMigration` instances manually with a valid name then calling `MigrateAsync` with a different unsafe table name); the 4 factory-entry RED facts cover the documented path, and a relational-runner-entry sub-item is optional. Closes review #46-3 #4.

## Phase 10: Boy Scout follow-ups from PR #4039 third review — Tier 2 defensive (post-Phase-9)

Tier 2 captures the three smaller defensive findings from the same PR #4039 third review. Each is a 1–3 line fix backed by a focused test. Phase 8's stated principle ("will not ship with technical debt around the lock primitives") applies — items R and S are lock-primitive issues; item T is host-service ordering. Folded into this PR rather than deferred.

**Order of operations**: Item R (MySQL timeout floor — affects sub-second timeouts), then Item S (Postgres monotonic clock — affects long-running provisioner waits across NTP corrections), then Item T (hosted-service ordering — preventative only, no current symptom).

### Tier 2 — defensive

- [ ] **Item R** — `MySqlAdvisoryLock` timeout floor + NULL-vs-timeout disambiguation. `MySqlAdvisoryLock.cs:43` does `var timeoutSeconds = (int)timeout.TotalSeconds;` — any `TimeSpan` with `TotalSeconds < 1` truncates to `0`, which `GET_LOCK(name, 0)` interprets as **non-blocking** (return immediately). A caller setting `MigrationLockTimeout = TimeSpan.FromMilliseconds(500)` would get an effectively-no-wait acquire that fails on any contention. Fix: `var timeoutSeconds = (int)Math.Max(1, Math.Ceiling(timeout.TotalSeconds));` — guarantees at least 1s of server-side blocking even for sub-second `TimeSpan` inputs. Separately, `GET_LOCK` returns `NULL` on a server-side error (not just on timeout); current code at line 51-55 conflates both into `TimeoutException`. Mirror MSSQL's per-return-code mapping (per Item N): on `NULL` raise a new `MySqlAdvisoryLockException` with a "server-side error during GET_LOCK on '{lockKey}' — check server logs for OOM or memory-table errors" message; on `0` keep `TimeoutException`.
  - **Sub-bullet: contract symmetry with MSSQL's Item N.** Update `IMySqlAdvisoryLock.AcquireAsync` XML doc at `IMySqlAdvisoryLock.cs:62-63` to list both `TimeoutException` (on `0` return — could not acquire within timeout) and `MySqlAdvisoryLockException` (on `NULL` return — server-side error during `GET_LOCK`). Update the ADR 0057 §5b "Interface shape per backend" table's MySQL row "Diagnostic value" cell to enumerate the two distinguishable codes (mirror the MSSQL row's per-code shape — Item N established this pattern in commit `2f727772d`): `0` (lock not acquired within timeout) → `TimeoutException`; `NULL` (server-side error) → `MySqlAdvisoryLockException` (new). The new public exception type `Paramore.Brighter.BoxProvisioning.MySql.MySqlAdvisoryLockException` is additive on a still-unreleased package; no `release_notes.md` entry needed.
  
  RED test (3 facts; class name and test-file naming follow project convention — multi-fact `*Tests` class in a single `When_..._should_...` file is acceptable per Phase 8 Item K precedent): `When_acquire_is_called_with_a_subsecond_timeout_it_should_block_at_least_one_second`, `When_release_lock_returns_null_acquire_should_throw_advisory_lock_exception_not_timeout`, `When_acquire_blocks_until_lock_available_within_subsecond_timeout_it_should_succeed_after_floor_applied`. Closes review #46-3 #3.
- [ ] **Item S** — `PostgreSqlAdvisoryLock` deadline uses monotonic, injectable clock via `TimeProvider`. `PostgreSqlAdvisoryLock.cs:51` does `var deadline = DateTime.UtcNow.Add(timeout);` and `:64` checks `if (DateTime.UtcNow >= deadline)`. A wall-clock jump (NTP correction during long lock waits, leap-second smear, container clock skew on VM resume) can collapse the deadline (premature `TimeoutException`) or extend it (lock wait runs past the configured timeout). Fix uses `System.TimeProvider` (native on net8+, which covers all `BrighterCoreTargetFrameworks` targets at `src/Directory.Build.props:45` — `net8.0;net9.0;net10.0` — so no production-side package reference is required; the polyfill `Microsoft.Bcl.TimeProvider` is centrally pinned at `Directory.Packages.props:72` if a future netstandard target requires it):
  - Add an additive optional ctor parameter `TimeProvider? timeProvider = null` to `PostgreSqlAdvisoryLock`, defaulting to `TimeProvider.System` when null. Store as `_timeProvider`.
  - Replace the deadline pattern with `var startTimestamp = _timeProvider.GetTimestamp();` and `if (_timeProvider.GetElapsedTime(startTimestamp) >= timeout)` — `GetTimestamp` returns a monotonic high-resolution counter (backed by `Stopwatch.GetTimestamp()` in `TimeProvider.System`) and `GetElapsedTime` converts it to a `TimeSpan`. Wall-clock jumps cannot affect either.
  - The `await Task.Delay(delayMs, cancellationToken)` retry stays as-is (delay is bounded by the cancellation token, not by wall-clock).
  - RED test in `Paramore.Brighter.PostgresSQL.Tests` injects `Microsoft.Extensions.TimeProvider.Testing.FakeTimeProvider` (already in 8+ test projects per `Directory.Packages.props:92`) and uses `fakeTimeProvider.Advance(...)` to drive the deadline check deterministically without needing real wall time. Test name: `When_postgres_advisory_lock_deadline_is_evaluated_against_the_injected_time_provider_it_should_be_independent_of_wall_clock`. Asserts that advancing `FakeTimeProvider` by `timeout + 1s` while the lock is held by another session causes acquire to throw `TimeoutException` regardless of `DateTime.UtcNow`. The `Paramore.Brighter.PostgresSQL.Tests.csproj` needs `Microsoft.Extensions.TimeProvider.Testing` added to its `PackageReference` list (one-line addition; all sibling test projects already have it).
  
  Closes review #46-3 #6.
- [ ] **Item T** — `BoxProvisioningHostedService` ordering defends against new `BoxType` values. `BoxProvisioningHostedService.cs:30` uses `OrderBy(p => p.BoxType == BoxType.Outbox ? 0 : 1)` — works for the current `BoxType { Outbox, Inbox }` enum but silently degrades to "all non-Outbox provisioners in registration order" if a third type is added. A future `Lockbox` / `DeadLetterBox` type would intermittently provision before Outbox depending on DI registration order, and the failure mode would be silent (provisioning still completes but possibly in the wrong order). Fix: replace the lambda with an explicit `static int OrderingOrdinal(BoxType type) => type switch { BoxType.Outbox => 0, BoxType.Inbox => 1, _ => throw new ArgumentOutOfRangeException(nameof(type), type, $"BoxProvisioningHostedService does not know how to order BoxType.{type} — add it to the switch in OrderingOrdinal") };` and call `OrderBy(p => OrderingOrdinal(p.BoxType))`. The throw-on-unknown converts a silent ordering bug into a loud startup failure that points the contributor at the file to update. RED test in `BoxProvisioningHostedServiceLoggingTests`: `When_hosted_service_is_started_with_a_provisioner_for_an_unrecognised_box_type_it_should_throw_argument_out_of_range` — uses a `StubBoxProvisioner` returning a non-existent `(BoxType)999` cast value to drive the default arm. Closes review #46-3 #7.
