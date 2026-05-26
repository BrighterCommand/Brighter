# Adding New Columns to the Outbox (or Inbox)

This document describes the process a Brighter maintainer must follow to add a new column to the Outbox or Inbox schema. The same process applies to both box types.

> **Authoritative quick reference**: [`.agent_instructions/box_provisioning.md`](../../.agent_instructions/box_provisioning.md) — start there. This document is the long-form playbook with worked examples and the full file-by-file checklist.

## Overview

Brighter's box provisioning system uses a **versioned migration chain** introduced in spec 0027. Each relational backend (MSSQL, PostgreSQL, MySQL, SQLite) maintains an ordered list of `BoxMigration` records starting at V1 (the live builder DDL — fresh-install fast path) and walking forward via idempotent `ALTER TABLE ADD COLUMN` UpScripts. Spanner is degenerate (fresh-install only — see ADR 0057 §6) and does NOT participate in the column-addition workflow.

When a new column is needed:

1. A new `V(N+1)` migration entry is appended to **each** of the 4 relational backends' migration lists.
2. The new column name is appended to the per-backend `s_v(N+1)AddedColumns` array consumed by the `Cumulative(int)` helper that builds each migration's `LogicalColumns` set.
3. The live builder DDL (`*OutboxBuilder` / `*InboxBuilder`) is updated so green-field installs land on the new schema in one shot.
4. **No provisioner detection logic needs to change** — the runner re-detects under lock by walking the migration list top-down via `*BoxDetectionHelpers.DetectCurrentVersionAsync`, which is data-driven from `LogicalColumns`. Adding a new migration with the right `LogicalColumns` is sufficient.
5. CI's per-backend drift test (`When_*_outbox_builder_is_compared_to_*_outbox_migration_columns_*`) fails until the new migration's cumulative columns match the updated builder DDL, catching the "added a column to the builder but forgot the migration" class of bug.

For the architectural rationale (three-path runner, advisory locks, the cross-backend uniformity of version numbers, V1's fresh-install fast path), see:

- [ADR 0057 — Box schema versioning and migrations](../../docs/adr/0057-box-schema-versioning-and-migrations.md)
- [Spec 0027 README](../0027-box-schema-versioning-and-migrations/README.md)

## ⚠ Mandatory Rule

> Every column added to a `*OutboxBuilder` or `*InboxBuilder` MUST ship with a new `V(N+1)` `BoxMigration` entry in the corresponding `*Migrations` class for the same backend, with matching `LogicalColumns` set including the new column.

A column added to the builder DDL without a matching migration is schema drift. Existing deployments that already passed the previous fresh-install path will never run the new ALTER, so their tables fall behind silently and runtime SQL fails with `invalid column name` the next time the new column is read or written. The drift test catches this at CI time.

## Step-by-step process

### 1. Add the new migration to each relational backend

Each relational backend has a `*OutboxMigrations.cs` (or `*InboxMigrations.cs`) static class with an `All(IAmARelationalDatabaseConfiguration)` factory returning an ordered `IReadOnlyList<IAmABoxMigration>`. Append a new `BoxMigration` with the next version number, populate `LogicalColumns`, `SourceReference`, and (SQLite only) `IdempotencyCheckSql`, and write the idempotent ALTER `UpScript` for that backend's grammar.

**Files (Outbox):**

| Backend    | File                                                                              |
|------------|-----------------------------------------------------------------------------------|
| MSSQL      | `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlOutboxMigrations.cs`            |
| PostgreSQL | `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlOutboxMigrations.cs`  |
| MySQL      | `src/Paramore.Brighter.BoxProvisioning.MySql/MySqlOutboxMigrations.cs`            |
| SQLite     | `src/Paramore.Brighter.BoxProvisioning.Sqlite/SqliteOutboxMigrations.cs`          |

(Inbox: same five paths with `Inbox` instead of `Outbox` — except Postgres inbox, which is V1-only by design per ADR 0057 §1; do not add a Postgres inbox migration unless you are deliberately rolling out a new inbox schema across all backends.)

**Spanner is exempt.** `SpannerOutboxMigrations.cs` and `SpannerInboxMigrations.cs` were deleted in spec 0027 Phase 5 — Spanner has a degenerate runner that stamps `V_latest` from a runner constant. Spanner installations get the new column purely via the builder update in step 3.

**Required fields on each new migration:**

- **`Version`** — strictly `previous_version + 1`. No gaps. Same V-number across all four relational backends for outbox columns that land on every backend.
- **`Description`** — short imperative phrase (e.g. `"Add CustomField column"`) used in the `__BrighterMigrationHistory.Description` row.
- **`UpScript`** — provider-appropriate idempotent ALTER (see grammar table below). The runner runs UpScripts under transactional lock; each UpScript MUST be safe to re-execute against any prior state (post-bootstrap, post-failed-partial-migration).
- **`LogicalColumns`** — the **cumulative** column set after this migration applies (`Cumulative(N+1)` if you follow the existing pattern). Used by `*BoxDetectionHelpers.DetectCurrentVersionAsync` for legacy-table version inference, and by the drift test to compare against the builder DDL.
- **`SourceReference`** — commit SHA (and PR number where available) that introduced the column, e.g. `"d67dac947 / #3790"`. Required from V2 onwards; V1 stays `null`.
- **`IdempotencyCheckSql`** — **SQLite only**. SQLite's grammar lacks `ALTER TABLE ADD COLUMN IF NOT EXISTS`, so the runner needs an explicit existence probe to skip the ALTER when the column already exists. MSSQL / PostgreSQL / MySQL bake the existence check into `UpScript` itself and leave `IdempotencyCheckSql` `null`.

**Provider-appropriate idempotent ALTER grammar:**

| Backend    | UpScript pattern                                                                                                     |
|------------|----------------------------------------------------------------------------------------------------------------------|
| MSSQL      | `IF COL_LENGTH(N'[{schema}].[{table}]', N'{col}') IS NULL{newline}    ALTER TABLE [{schema}].[{table}] ADD [{col}] {type} NULL;` |
| PostgreSQL | `ALTER TABLE "{schema}"."{table}" ADD COLUMN IF NOT EXISTS "{col}" {type} NULL;`                                     |
| MySQL      | `information_schema.columns` existence check + prepared statement (`ALTER TABLE` cannot be parameterised in MySQL)   |
| SQLite     | Plain `ALTER TABLE [{table}] ADD COLUMN [{col}] {type} NULL;` — runner skips the ALTER when `IdempotencyCheckSql` returns `> 0` |

**Worked example — MSSQL adding a `CustomField NVARCHAR(255) NULL` column at V8:**

In `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlOutboxMigrations.cs`, append to the `s_v…AddedColumns` declarations:

```csharp
private static readonly string[] s_v8AddedColumns = ["CustomField"];
```

Extend the `Cumulative` helper (the runner uses it to build each migration's `LogicalColumns` from the union of V1..V_n):

```csharp
private static IReadOnlyCollection<string> Cumulative(int upToVersion)
{
    var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    if (upToVersion >= 1) { set.UnionWith(s_v1Columns); }
    // ... existing V2..V7 unions ...
    if (upToVersion >= 8) { set.UnionWith(s_v8AddedColumns); }
    return set;
}
```

Append the new migration to the `All` factory's returned list (after V7):

```csharp
new BoxMigration(
    Version: 8,
    Description: "Add CustomField column",
    UpScript: AddColumns(schema, table, ("CustomField", "NVARCHAR(255)")),
    LogicalColumns: Cumulative(8),
    SourceReference: "<commit-sha> / #<PR>")
```

The existing `AddColumns(schema, table, params (string Column, string Type)[] columns)` helper already produces the `IF COL_LENGTH(...) IS NULL ALTER TABLE …` boilerplate per column.

Repeat the **same pattern** in `PostgreSqlOutboxMigrations.cs`, `MySqlOutboxMigrations.cs`, and `SqliteOutboxMigrations.cs`. The version number, the new column name in `LogicalColumns`, the `Description`, and the `SourceReference` MUST match across all four backends. The `UpScript` differs by grammar; SQLite additionally sets `IdempotencyCheckSql`.

**General rules:**

- Version numbers must match across all four relational backends for any outbox column that lands on every backend.
- New columns must be nullable or have a `DEFAULT` value — the runner cannot apply `NOT NULL` adds against existing rows.
- Update both text and binary payload variants if the builder distinguishes them (check the existing `*Builder.GetDDL(...)` signature).
- Keep V1's `UpScript` pointed at the live builder DDL — V1 IS the fresh-install fast path (per ADR §3) and stays in sync with the builder; only V1's `LogicalColumns` is the historical 6-column baseline used for detection of pre-spec-0027 tables.

### 2. (No detection-helper change required)

`*BoxDetectionHelpers.DetectCurrentVersionAsync` (one per relational backend, plus Spanner) walks the migration list `V_latest..V1` and returns the first version whose `LogicalColumns` is a subset of the table's actual columns. As long as step 1 populates the new migration's `LogicalColumns` correctly, detection of legacy tables at the new version is automatic. **Do not edit `*BoxDetectionHelpers.cs`** — its job is data-driven from the migration list.

The previously-private `DetectCurrentVersionAsync` methods on `*OutboxProvisioner` / `*InboxProvisioner` were moved into `*BoxDetectionHelpers` in spec 0027 Phases 1.5 / 2.5 / 3.5 / 4.5; the provisioner now just calls the helper. If you find yourself editing detection logic on a provisioner, you are looking at the wrong file.

### 3. Update the live builder DDL

Update the existing `*Builder` classes so green-field installations get the complete schema in a single CREATE TABLE statement (V1's UpScript IS this DDL — see ADR §3 fresh-install fast path).

**Files (Outbox):**

| Backend    | File                                                                  |
|------------|-----------------------------------------------------------------------|
| MSSQL      | `src/Paramore.Brighter.Outbox.MsSql/SqlOutboxBuilder.cs`              |
| PostgreSQL | `src/Paramore.Brighter.Outbox.PostgreSql/PostgreSqlOutboxBuilder.cs`  |
| MySQL      | `src/Paramore.Brighter.Outbox.MySql/MySqlOutboxBuilder.cs`            |
| SQLite     | `src/Paramore.Brighter.Outbox.Sqlite/SqliteOutboxBuilder.cs`          |
| Spanner    | `src/Paramore.Brighter.Outbox.Spanner/SpannerOutboxBuilder.cs`        |

(Inbox: same paths with `Inbox`. **Spanner inbox builder must also be updated** — it is the only path by which Spanner installations receive the new column.)

Update both text and binary DDL string templates if the builder has separate variants (it does on backends with binary payload support).

### 4. Update the read/write SQL if the column is exercised at runtime

If the new column is populated or read by Brighter at runtime (most are), update the corresponding outbox / inbox implementation classes:

- The `Add` / `AddAsync` methods (INSERT statements + parameter binding)
- The `Get` / `GetAsync` methods (SELECT statements + result mapping into `Message` / `MessageHeader` / etc.)
- The `Message` (or related) model classes if the column maps to a new property
- Any payload-mode specifics if the column's type or storage differs by mode (text vs binary vs JSON/JSONB)

These files live under `src/Paramore.Brighter.Outbox.{Backend}/` and `src/Paramore.Brighter.Inbox.{Backend}/` — they are separate from the `BoxProvisioning.{Backend}` projects.

### 5. Tests

**Drift test (mandatory, automatic):** The per-backend drift test under `tests/Paramore.Brighter.{Backend}.Tests/BoxProvisioning/` (e.g. `When_mssql_outbox_builder_is_compared_to_mssql_outbox_migration_columns_they_should_match`) asserts:

```csharp
var expected = DdlColumnExtractor.GetExpectedColumns(builder.GetDDL(...), QuoteStyleFor(backend));
var covered = migrations.Last().LogicalColumns.Union(housekeeping(box, backend));
Assert.Equal(expected, covered, comparerFor(backend));
```

This test goes RED the moment you update the builder in step 3 without adding the matching migration in step 1 — that's the safety net. It goes GREEN automatically once both lands.

**Behaviour tests you should write:**

- **Migration test** — provision an existing pre-V8 table and verify the V8 ALTER applies cleanly, history advances to V8, and the new column is present.
- **Idempotency test** — re-provision a V8-stamped table and verify nothing changes (no duplicate ALTER, no extra history row, no error).
- **Bootstrap test** — create a table directly via the V_(N-1) builder (or simulate a pre-V8 install in some other way), provision it, and verify it bootstraps to the right synthetic V_(N-1) history and then walks forward to V8.
- **Round-trip test** — if the column is exercised at runtime, write a `Message` containing the new field through `Add`, read it back through `Get`, and assert equality.

The existing `BoxProvisioning/` test directories under each backend's test project have prior examples for V1..V7 outbox and V1..V2 inbox — use them as templates.

## Checklist

- [ ] New `BoxMigration` entry appended to all 4 relational `*OutboxMigrations.cs` (or `*InboxMigrations.cs`) files with matching `Version`, `Description`, `LogicalColumns`, `SourceReference`
- [ ] `s_v(N+1)AddedColumns` array + `Cumulative(N+1)` branch added in each of the 4 relational migration files
- [ ] Provider-appropriate idempotent `UpScript` written for each backend (using the existing `AddColumn`/`AddColumns` helpers where they exist)
- [ ] SQLite migration sets `IdempotencyCheckSql` (the other three backends leave it `null`)
- [ ] Live `*OutboxBuilder` / `*InboxBuilder` DDL updated for all 5 backends (including Spanner) — both text and binary variants where applicable
- [ ] Read/write code (INSERT / SELECT / model classes) updated for all 5 backends if the column is exercised at runtime
- [ ] Drift test verified RED then GREEN (the cycle proves the test guards the rule)
- [ ] Migration / idempotency / bootstrap / round-trip tests added per backend
- [ ] Postgres inbox NOT extended unless deliberately rolling out a new inbox schema across all backends (Postgres inbox is V1-only — ADR 0057 §1)
- [ ] **Do NOT edit** `*BoxDetectionHelpers.cs`, `*BoxMigrationRunner.cs`, or any `*OutboxProvisioner.cs` / `*InboxProvisioner.cs` — the migration mechanism is data-driven from `LogicalColumns`

## Architecture reference

For full details on the migration system architecture, see:

- **ADR 0057** — versioning model, three-path runner (fresh / bootstrap / normal), per-backend advisory-lock abstractions (`IPostgreSqlAdvisoryLock` / `IMySqlAdvisoryLock` / `IMsSqlAdvisoryLock`), Spanner degenerate runner, drift-test design.
- **Spec 0027** — `requirements.md` (acceptance criteria), `tasks.md` (the 7 implementation phases plus 15 Boy Scout follow-ups), `README.md` (archaeological evidence base for the V1..V7 outbox / V1..V2 inbox chain).
- **Core abstractions** — `src/Paramore.Brighter.BoxProvisioning/` (`IAmABoxMigration`, `BoxMigration` record, `IAmABoxMigrationRunner`, `BoxProvisioningHostedService`).
- **Migration history table** — `__BrighterMigrationHistory` on relational backends; `BrighterMigrationHistory` (no leading underscores) on Spanner. Composite primary key `(SchemaName, BoxTableName, MigrationVersion)`. Tracks applied versions per box table.
- **Concurrency** — MSSQL `sp_getapplock` (transaction-scoped), Postgres `pg_try_advisory_lock` (session-scoped), MySQL `GET_LOCK` (session-scoped), SQLite `BEGIN IMMEDIATE` writer slot, Spanner optimistic single-stmt INSERT with `IsMigrationAppliedAsync` gate.
- **Startup orchestration** — `BoxProvisioningHostedService` runs all provisioners at application startup (Outbox before Inbox); each lifecycle log line names the box type and table name (Item K).
