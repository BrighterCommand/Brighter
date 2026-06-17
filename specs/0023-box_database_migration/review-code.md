# Review: code — 0023-box_database_migration

**Date**: 2026-04-21
**Branch**: database_migration
**Base**: master (88ad8729)
**HEAD**: bae956c3d
**Threshold**: 60
**Verdict**: NEEDS WORK (at review date)

7 findings at or above threshold 60. Address these before opening PR.

> **Resolution status (2026-05-04)**: All blocking findings (≥60) are now closed. R1 was rerouted to spec 0027 (the version-per-schema-change migration chain). R2, R4, R5 are closed by spec 0027 Phases 1–4 / 5 / 6 respectively — see the inline resolution notes on each finding below. R3 closed by `297ca030f`; R6 closed by `0088abe54`; R7 (branch hygiene) resolved.

## Findings

### 1. Bootstrap path silently breaks for pre-DataRef/SpecVersion installations across ALL 10 provisioners (Score: 85)

Every `DetectCurrentVersionAsync` uses the pattern `if (actualColumns.IsSupersetOf(V1Columns)) return 1; return 0;` where `V1Columns` is the *current* full column list (including `DataRef` and `SpecVersion` on outboxes). For an installation that predates the addition of these columns — exactly the upgrade case the bootstrap path is designed to handle — `IsSupersetOf(V1Columns)` is `false` and the method returns `0`.

The runner then receives `BoxTableState(TableExists: true, HistoryExists: false, CurrentVersion: 0)`. Bootstrap loops in MSSQL (`MsSqlBoxMigrationRunner.InsertSyntheticHistoryAsync` lines 143-157) and all other backends use the guard `if (migration.Version > currentVersion) break;` — version 1 > 0, so bootstrap exits immediately, inserting no history row. The main migration loop then tries to apply V1, which is the full `CREATE TABLE` DDL from `SqlOutboxBuilder.GetDDL()` — this DDL does NOT use `IF NOT EXISTS` for MSSQL (`src/Paramore.Brighter.Outbox.MsSql/SqlOutboxBuilder.cs:34-94`), so it fails with "Cannot create table ... already exists". The provisioner then throws, `BoxProvisioningHostedService` re-throws as `ConfigurationException`, and the host fails to start.

This contradicts ADR 0053 §7 which states: "The safe fallback is version 1, which assumes the table was created by the original static builder" and NFR-1 (backward compatibility). It also contradicts AC-2 (migrate existing tables without data loss) for users upgrading from Brighter versions prior to when `DataRef`/`SpecVersion` were added.

**Evidence**:
- `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlOutboxProvisioner.cs:125-132`
- `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlOutboxProvisioner.cs:122-129`
- `src/Paramore.Brighter.BoxProvisioning.MySql/MySqlOutboxProvisioner.cs:116-123`
- `src/Paramore.Brighter.BoxProvisioning.Sqlite/SqliteOutboxProvisioner.cs:107-114`
- `src/Paramore.Brighter.BoxProvisioning.Spanner/SpannerOutboxProvisioner.cs:107-114`
- Plus the 5 inbox equivalents
- No integration test exercises this scenario — the bootstrap tests all seed via the current builder which already includes the v2+ columns

**Recommendation**: Either (a) compute `V1Columns` as the minimal stable column set (MessageId, Topic, Timestamp, etc.) and treat DataRef/SpecVersion as V2 columns — which matches the ADR's stated extensibility model in PROMPT.md lines 60-64 — then return 1 on subset match and 2 when DataRef/SpecVersion are also present; or (b) fall back to returning 1 when the table exists but lacks modern columns (per the ADR's explicit "safe fallback" guidance), relying on the future V2 migration to add them idempotently. Add an explicit integration test that creates a pre-DataRef table and verifies the provisioner bootstraps at V1 (and, once V2 exists, runs the ALTER TABLE).

---

### 2. Bootstrap path has a TOCTOU race across all backends — concurrent instances will crash on PK violation (Score: 78) — **CLOSED by spec 0027**

> **Resolution (2026-05-04)**: Closed by spec 0027 — see ADR 0057 §3 "TOCTOU re-check under lock" and the per-backend runner rewrites in [`specs/0027-box-schema-versioning-and-migrations/tasks.md`](../0027-box-schema-versioning-and-migrations/tasks.md):
> - **MSSQL** Tasks 1.4 + 1.5 (commit `705e8946a`) — runner re-reads `tableExistsNow`/`historyExistsNow` inside the `sp_getapplock @LockOwner='Transaction'` transaction; three-path branching (fresh / bootstrap / normal); concurrent-bootstrap test in Task 1.8 (`2376998a8`) covers outbox + inbox.
> - **PostgreSQL** Tasks 2.4 + 2.5 (commit `888897981`) — single `NpgsqlTransaction` wraps the TOCTOU re-check + DDL chain under `pg_try_advisory_lock`; concurrent-bootstrap test in Task 2.7 (`dd836c6bd`).
> - **MySQL** Tasks 3.4 + 3.5 (commit `76f9bd6a9`) — TOCTOU re-check under `GET_LOCK` session-scoped lock; per-DDL implicit commit + history-table PK enforces the single-synthetic-row invariant; concurrent-bootstrap test in Task 3.7 (`6bc55a8c4`).
> - **SQLite** Task 4.4 (commit `97e6b400b`) — runner sets `PRAGMA busy_timeout = 0` and serialises via explicit `BEGIN IMMEDIATE` with bounded SQLITE_BUSY retry; concurrent-bootstrap test in Task 4.8 (`08a2f9176`).
>
> Each runner now invokes `IsMigrationAppliedAsync` (or its idempotency-check equivalent on SQLite) before inserting any history row, removing the unguarded synthetic-row INSERT that the original race exploited. The history-table PK on `(SchemaName, BoxTableName, MigrationVersion)` is the final backstop.



In every provisioner, `DetectTableStateAsync` opens its own connection and runs outside any advisory lock (e.g. `MsSqlOutboxProvisioner.cs:55-75`, `MySqlOutboxProvisioner.cs:46-67`, `PostgreSqlOutboxProvisioner.cs:54-74`, etc.). Only afterward does the runner acquire the lock (MSSQL `sp_getapplock`, PostgreSQL `pg_try_advisory_lock`, MySQL `GET_LOCK`).

The bootstrap path in `InsertSyntheticHistoryAsync` / `BootstrapExistingTableAsync` unconditionally calls `InsertHistoryRowAsync` — no `IsMigrationAppliedAsync` guard (unlike the regular migration loop). The primary key on `__BrighterMigrationHistory` is `(SchemaName, BoxTableName, MigrationVersion)`.

Race scenario:
1. Instance A detects `(true, false, 1)` (a legacy table existed and was just bootstrapped by another process).
2. Instance B detects `(true, false, 1)` concurrently.
3. Instance A acquires lock, inserts synthetic V1 row, releases.
4. Instance B acquires lock, calls `InsertSyntheticHistoryAsync` with its stale `(true, false, 1)` state, attempts to insert V1 row → primary key violation.

The existing concurrency tests (`When_multiple_mssql_provisioners_run_concurrently...`, MySQL and PostgreSQL equivalents) only test the *fresh install* race path where `DetectTableStateAsync` returns `(false, false, 0)` — the regular migration loop's `IsMigrationAppliedAsync` check catches that case (MsSqlBoxMigrationRunner.cs:64-67). No test exists for concurrent bootstrap.

**Evidence**:
- `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlBoxMigrationRunner.cs:143-157` — `InsertSyntheticHistoryAsync` has no applied-check
- `src/Paramore.Brighter.BoxProvisioning.MySql/MySqlBoxMigrationRunner.cs:46-60`
- `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlBoxMigrationRunner.cs:139-152`
- `src/Paramore.Brighter.BoxProvisioning.Sqlite/SqliteBoxMigrationRunner.cs:43-57`
- `src/Paramore.Brighter.BoxProvisioning.Spanner/SpannerBoxMigrationRunner.cs:39-53`

**Recommendation**: Re-read the state inside the lock (after acquiring, re-query history existence and skip the synthetic insert if it now exists), OR call `IsMigrationAppliedAsync` within the bootstrap loop before each insert, OR use `INSERT ... WHERE NOT EXISTS` / `INSERT OR IGNORE` / `ON CONFLICT DO NOTHING` per backend. Add a concurrency test specifically for the bootstrap path (seed a table directly via builder, then race two provisioners).

---

### 3. `SchemaName` added to `IAmARelationalDatabaseConfiguration` as a plain abstract member — not as a default interface member — breaks external implementors (Score: 72) — **CLOSED by `297ca030f`**

> **Resolution (2026-05-04)**: Closed by commit `297ca030f`. ADR 0053 §10 and `tasks.md` were updated to document that the default-interface-member approach is not viable on `netstandard2.0` (which Brighter still targets) and the source-breaking consequence of `SchemaName` being a plain abstract member is accepted; `release_notes.md` carries the breaking-change entry. The recommendation's alternative path (a separate optional `IAmASchemaQualifiedConfiguration` interface checked for at runtime) was considered and rejected as accidental complexity for the small number of expected external implementors.

Task 0.1 and ADR 0053 §10 both explicitly require `SchemaName` to be added "as a **default interface member**: `string? SchemaName => null;`" to avoid breaking external implementors. The actual implementation at `src/Paramore.Brighter/IAmARelationalDatabaseConfiguration.cs:45-48` declares it as a plain abstract member:

```csharp
/// <summary>
/// Gets the name of the schema containing the tables.
/// </summary>
/// <value>The schema name, or <c>null</c> for the backend's default schema.</value>
string? SchemaName { get; }
```

The dismissal in PROMPT.md (B1, lines 85-86) correctly notes that `Paramore.Brighter` targets `netstandard2.0` (confirmed in `src/Directory.Build.props:43`), which cannot support default interface members — so the ADR decision was literally unimplementable. But the *actual* outcome is a breaking source-level change: any external consumer who has implemented `IAmARelationalDatabaseConfiguration` (e.g. a custom configuration wrapper for test or multi-tenant scenarios) will fail to compile without adding the new member. This contradicts NFR-1 (backward compatibility).

Neither the ADR, the requirements, nor `tasks.md` have been updated to reflect this — they still say "default interface member". Downstream release notes will need a breaking-change note that the spec doesn't currently anticipate.

**Evidence**:
- `src/Paramore.Brighter/IAmARelationalDatabaseConfiguration.cs:45-48` — abstract, not default
- `specs/0023-box_database_migration/tasks.md:15` — "Add `string? SchemaName => null;` as a **default interface member**"
- `docs/adr/0053-box-database-migration.md:704` — "this will be added as a **default interface member**"
- `src/Directory.Build.props:43` — `netstandard2.0;net8.0;net9.0;net10.0` (no default-interface-member support on netstandard2.0)
- `PROMPT.md:86` — dismissed but not followed through to ADR/task updates

**Recommendation**: Update ADR 0053 §10 and `tasks.md` task 0.1 to document that the default-interface-member approach is not viable on `netstandard2.0` and the breaking-change consequence is accepted, and add a release-notes entry. Alternatively, if backward compatibility is critical, move `SchemaName` onto a separate optional interface (e.g. `IAmASchemaQualifiedConfiguration`) that the provisioner code checks for at runtime.

---

### 4. Spanner runner has no concurrency protection for the history INSERT (Score: 70) — **CLOSED by spec 0027**

> **Resolution (2026-05-04)**: Closed by spec 0027 Task 5.1 (commit `06d35740d`). The Spanner runner now gates the history INSERT through `IsMigrationAppliedAsync(vLatest)` on the fresh-install path, then through the same gate on the existing-table-without-history bootstrap path (Task 5.2, commit `e61cde3c9`). Two concurrent instances racing on a fresh install therefore land at most one synthetic V_latest history row regardless of which sequence wins; the loser's `IsMigrationAppliedAsync` returns true and the INSERT is skipped. See ADR 0057 §6 (Spanner degenerate runner contract) and [`specs/0027-box-schema-versioning-and-migrations/tasks.md`](../0027-box-schema-versioning-and-migrations/tasks.md) Tasks 5.1–5.3. Spanner BoxProvisioning suite is 14/14 GREEN against the cloud-spanner emulator on net9.0 + net10.0.



`SpannerBoxMigrationRunner` (src/Paramore.Brighter.BoxProvisioning.Spanner/SpannerBoxMigrationRunner.cs:24-37) applies no lock and no catch for already-inserted history rows. The comment at line 13 claims "Spanner handles DDL concurrency internally — no advisory lock is needed", but while that covers DDL serialization for `CreateDdlCommand`, it does NOT protect the read-write INSERT into `BrighterMigrationHistory`. `ExecuteDdlSafeAsync` catches `AlreadyExists`/`FailedPrecondition` only for DDL; the history INSERT at line 124-134 will fail with a primary-key violation (`BoxTableName, MigrationVersion` is the PK per EnsureHistoryTableAsync line 98) if two instances race.

No concurrency test exists for Spanner — the Spanner test collection `SpannerBoxProvisioningCollection` explicitly disables parallelization, hiding this issue from the test suite.

**Evidence**:
- `src/Paramore.Brighter.BoxProvisioning.Spanner/SpannerBoxMigrationRunner.cs:120-135` — bare INSERT with no conflict handling
- `tests/Paramore.Brighter.Gcp.Tests/Spanner/BoxProvisioning/SpannerBoxProvisioningCollection.cs:9` — `DisableParallelization = true`

**Recommendation**: Either wrap the history INSERT in a uniqueness-tolerant pattern (`INSERT ... WHERE NOT EXISTS`, or call `IsMigrationAppliedAsync` immediately before insert and skip if applied), or catch Spanner's `AlreadyExists` on the INSERT and treat it as success. Document why the Spanner test collection disables parallelization — noting that the current implementation doesn't tolerate concurrent provisioning, which operators deploying multiple instances need to know.

---

### 5. Missing payload-mode-mismatch integration tests for four of five backends (Score: 68) — **CLOSED by spec 0027**

> **Resolution (2026-05-04)**: Closed by spec 0027 Phase 6 — one payload-mode-mismatch integration test per remaining backend, mirroring the MSSQL pattern (`When_mssql_outbox_provisioner_detects_payload_mode_mismatch_it_should_throw.cs`):
> - **PostgreSQL** Task 6.1 — commit `26aea6aab`
> - **MySQL** Task 6.2 — commit `a063c3111`
> - **SQLite** Task 6.3 — commit `a4dbe725c`
> - **Spanner** Task 6.4 — commit `1307ecbda`
>
> Each test seeds a table at one payload mode (text or binary), constructs a provisioner configured for the opposite mode, and asserts `ConfigurationException` propagates from the per-backend `*PayloadModeValidator` through `ProvisionAsync()`. No production changes were required — the validators were already in place from spec 0023 and the provisioners already invoke `ValidatePayloadModeAsync` whenever `tableState.TableExists` is true. See [`specs/0027-box-schema-versioning-and-migrations/tasks.md`](../0027-box-schema-versioning-and-migrations/tasks.md) Phase 6.



Task 2.5 (MSSQL outbox) and 2.6 (MSSQL inbox) are the only payload-mode validation tests. The ADR §6 mandates payload mode validation as a separate step for all relational backends, and the code has `PostgreSqlPayloadModeValidator`, `MySqlPayloadModeValidator`, `SqlitePayloadModeValidator`, `SpannerPayloadModeValidator` — but no integration test exercises any of them.

The validators have non-trivial dialect-specific logic (e.g. `MySqlPayloadModeValidator` accepts both `longtext`/`text` and `longblob`/`blob`; `SqlitePayloadModeValidator` also accepts `NTEXT`; `SpannerPayloadModeValidator` uses `StartsWith("BYTES"/"STRING")`). A regression in any of these would not be caught.

**Evidence**:
- `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlPayloadModeValidator.cs` — exists, no test
- `src/Paramore.Brighter.BoxProvisioning.MySql/MySqlPayloadModeValidator.cs` — exists, no test
- `src/Paramore.Brighter.BoxProvisioning.Sqlite/SqlitePayloadModeValidator.cs` — exists, no test
- `src/Paramore.Brighter.BoxProvisioning.Spanner/SpannerPayloadModeValidator.cs` — exists, no test
- `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning/`, `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning/`, `tests/Paramore.Brighter.Sqlite.Tests/BoxProvisioning/`, `tests/Paramore.Brighter.Gcp.Tests/Spanner/BoxProvisioning/` — no `*payload_mode*` test files

**Recommendation**: Add one payload-mode-mismatch integration test per backend (outbox is sufficient — inbox reuses the same validator). Follow the MSSQL test pattern (`When_mssql_outbox_provisioner_detects_payload_mode_mismatch_it_should_throw.cs`): seed a text-mode table via the existing builder, configure the provisioner with `binaryMessagePayload: true`, assert `ConfigurationException`.

---

### 6. `MsSqlBoxMigrationRunner.AcquireLockAsync` builds dynamic SQL with `sp_getapplock` parameter-name collisions (Score: 62)

`src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlBoxMigrationRunner.cs:100-109`:

```csharp
command.CommandText =
    "DECLARE @result INT; " +
    "EXEC @result = sp_getapplock " +
    "@Resource = @Resource, " +
    "@LockMode = 'Exclusive', " +
    "@LockTimeout = @LockTimeout, " +
    "@LockOwner = 'Transaction'; " +
    "SELECT @result;";
command.Parameters.AddWithValue("@Resource", lockResource);
command.Parameters.AddWithValue("@LockTimeout", (int)_lockTimeout.TotalMilliseconds);
```

`@Resource` is both the `sp_getapplock` named parameter (left of `=`) and the SqlCommand placeholder (right of `=`). Likewise `@LockTimeout`. This works in practice because SQL Server resolves the LHS to the SP signature and the RHS to the batch variable, but it is fragile — a driver change or a future ADO.NET behavior could reject it, and it reads as a typo. Additionally, `@result` is declared as a batch variable while `@Resource`/`@LockTimeout` are bound parameters — mixing the two is unusual style.

**Evidence**: `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlBoxMigrationRunner.cs:100-109`.

**Recommendation**: Rename the SqlCommand parameters to unambiguous placeholders (e.g. `@lockResourceName`, `@lockTimeoutMs`) so the EXEC reads `@Resource = @lockResourceName, @LockTimeout = @lockTimeoutMs`. This removes the ambiguity and improves readability.

---

### 7. Uncommitted changes on the review branch (Score: 60)

`git status` at HEAD shows an uncommitted modification to `.claude/commands/spec/review.md` (adding a "code" review phase — scope creep vs. spec 0023) and an untracked `PROMPT.md`. Per review rubric, non-empty `git status` at time of review is a Medium finding. The `review.md` change is orthogonal to spec 0023 (it is the very review skill being used), and `PROMPT.md` is a per-session scratch file that should either be added to `.gitignore` or committed.

**Evidence**:
- `git status` output at the start of this review: ` M .claude/commands/spec/review.md` and `?? PROMPT.md`.
- `git diff master -- .claude/commands/spec/review.md` shows ~40 lines of additions that describe `/spec:review code` — unrelated to BoxProvisioning.

**Recommendation**: Commit the `review.md` change on a separate branch/PR (it is a tooling change, not part of spec 0023). Add `PROMPT.md` to `.gitignore` (it is clearly per-session working state per line 1: "Current State — Spec 0023"), or commit it alongside spec artefacts if it should be shared. Re-run the review against a clean working tree before merging.

---

### 8. Cross-backend test class-name inconsistency (Score: 55)

The MySQL concurrency test file `When_multiple_mysql_provisioners_run_concurrently_they_should_not_corrupt_state.cs` contains a class named `ConcurrentProvisionerTests` (`tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning/...:9`). The other backends name their classes to match the file: `When_multiple_mssql_provisioners_run_concurrently_they_should_not_corrupt_state` (MSSQL) and `When_multiple_postgresql_provisioners_run_concurrently_they_should_not_corrupt_state` (PostgreSQL). Per `.agent_instructions/testing.md` the class name convention is `[Behavior]Tests` **OR** match the file — but within one feature the project should pick one. The inconsistency is a grep-hazard and breaks `When_...` search navigation.

**Evidence**:
- `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning/When_multiple_mysql_provisioners_run_concurrently_they_should_not_corrupt_state.cs:9` — `public class ConcurrentProvisionerTests`
- `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/When_multiple_mssql_provisioners_run_concurrently_they_should_not_corrupt_state.cs:10` — matching class name
- `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning/When_multiple_postgresql_provisioners_run_concurrently_they_should_not_corrupt_state.cs:9` — matching class name

**Recommendation**: Rename the MySQL class to match the file (or rename all three to `[Behavior]Tests` form). Apply consistently across the feature.

---

### 9. `MsSqlBoxMigrationRunner.EnsureHistoryTableAsync` checks `sys.tables` without schema filter (Score: 50)

`src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlBoxMigrationRunner.cs:127-139`:

```csharp
command.CommandText = $@"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = '{MIGRATION_HISTORY_TABLE}')
BEGIN
    CREATE TABLE [{MIGRATION_HISTORY_TABLE}] (...);
END";
```

The `IF NOT EXISTS` check filters only by table name, not by schema. If any schema in the current database has a table named `__BrighterMigrationHistory`, the check returns true and the CREATE is skipped — but the subsequent INSERT targets `[{MIGRATION_HISTORY_TABLE}]` (resolved to the default `dbo.__BrighterMigrationHistory`), so the row goes into `dbo` while the existence check saw a different schema. This is a corner case (a user would have to have manually created `other.__BrighterMigrationHistory`), but it contradicts the dismissal of B3/B4 in PROMPT.md which says "The history table is always created in the default schema (dbo/public) by the migration runner. Checking there is correct."

**Evidence**: `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlBoxMigrationRunner.cs:127-128`.

**Recommendation**: Add `AND schema_id = SCHEMA_ID('dbo')` to the existence check, mirroring the filter in `DoesTableExistAsync` (MsSqlOutboxProvisioner.cs:94-97).

---

### 10. Provisioners open three separate connections per run (Score: 45)

Each provisioner (e.g. `MsSqlOutboxProvisioner.ProvisionAsync`) opens one connection for `DetectTableStateAsync`, a second for `ValidatePayloadModeAsync`, and the runner opens a third for `MigrateAsync`. The ADR §5 "Connection Lifecycle" says "The migration runner opens a **single `DbConnection`** ... at the start of `MigrateAsync`." The spirit of that decision is efficiency + lock consistency, but the implementation triples the connection count per provision.

This is not a correctness bug (detection doesn't need to hold a lock), but it is a small efficiency regression and a divergence from the stated design.

**Evidence**:
- `MsSqlOutboxProvisioner.cs:59` (detect), `:81` (validate), `MsSqlBoxMigrationRunner.cs:38` (migrate)
- `PostgreSqlOutboxProvisioner.cs:58, 80`, `PostgreSqlBoxMigrationRunner.cs:39`
- Same pattern in MySQL, SQLite, Spanner

**Recommendation**: Either consolidate detection + validation onto the migration runner's single connection (passed as a parameter), or document in the ADR that the 3-connection approach is accepted.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 4 |
| 50-69 (Medium) | 5 |
| 0-49 (Low) | 1 |

**Total findings**: 10
**Findings at or above threshold (60)**: 7

## Files examined

Core package:
- `src/Paramore.Brighter/IAmARelationalDatabaseConfiguration.cs`
- `src/Paramore.Brighter.BoxProvisioning/{BoxProvisioningHostedService,BoxProvisioningOptions,BrighterBuilderBoxProvisioningExtensions,BoxMigration,BoxTableState,BoxType,IAmABoxMigration,IAmABoxMigrationRunner,IAmABoxProvisioner}.cs`

MSSQL: `MsSql{OutboxProvisioner,InboxProvisioner,BoxMigrationRunner,PayloadModeValidator,BoxProvisioningExtensions,OutboxMigrations,InboxMigrations}.cs`
PostgreSQL: `PostgreSql{OutboxProvisioner,InboxProvisioner,BoxMigrationRunner,PayloadModeValidator,BoxProvisioningExtensions}.cs`
MySQL: `MySql{OutboxProvisioner,InboxProvisioner,BoxMigrationRunner,PayloadModeValidator,BoxProvisioningExtensions}.cs`
SQLite: `Sqlite{OutboxProvisioner,BoxMigrationRunner,PayloadModeValidator,BoxProvisioningExtensions}.cs`
Spanner: `Spanner{OutboxProvisioner,InboxProvisioner,BoxMigrationRunner,PayloadModeValidator,OutboxMigrations,InboxMigrations,BoxProvisioningExtensions,ConnectionHelper}.cs`
Builders consulted: `SpannerOutboxBuilder.cs`, `SqlOutboxBuilder.cs`

Tests (sampled):
- `tests/Paramore.Brighter.Core.Tests/BoxProvisioning/When_using_box_provisioning_extension_it_should_register_hosted_service_and_provisioners.cs`
- MSSQL bootstrap, concurrency, payload-mode, connection-name tests
- PostgreSQL concurrency + bootstrap tests
- MySQL concurrency test
- Spanner outbox fresh-database test and `SpannerBoxProvisioningCollection`
- `Paramore.Brighter.Extensions.Tests/TestDifferentSetups.cs` + `StubSqlDbConfiguration.cs`

Specs + config:
- `specs/0023-box_database_migration/{requirements,tasks}.md`
- `docs/adr/0053-box-database-migration.md`
- `.agent_instructions/{code_style,testing}.md`
- `PROMPT.md`
- `src/Directory.Build.props`, `src/Paramore.Brighter/Paramore.Brighter.csproj`

Samples:
- `samples/WebAPI/WebAPI_EFCore/SalutationAnalytics/Program.cs`
- `samples/WebAPI/WebAPI_Common/DbMaker/{BoxProvisioningFactory,Rdbms}.cs`
