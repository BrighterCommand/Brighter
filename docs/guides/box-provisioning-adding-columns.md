# Adding a Column to the Outbox or Inbox

This guide walks a contributor through adding a new column to Brighter's Outbox or Inbox schema. The same workflow applies to either box; differences are called out where they exist.

> **Quick reference for AI agents**: [`.agent_instructions/box_provisioning.md`](../../.agent_instructions/box_provisioning.md). This guide is the long-form companion.

## What the migration system does for you

Brighter's box provisioning system maintains a **versioned migration chain** per (backend × box-type). Each relational backend (MSSQL, PostgreSQL, MySQL, SQLite) keeps an ordered list of `BoxMigration` records starting at V1 (the live builder DDL — fresh-install fast path) and walking forward via idempotent `ALTER TABLE ADD COLUMN` UpScripts. At application startup, a `BoxProvisioningHostedService` runs each registered provisioner; the runner detects table state, takes a backend-appropriate advisory lock, applies any pending migrations, and records each applied version in the `__BrighterMigrationHistory` table.

Spanner is **degenerate** (fresh-install only — see [ADR 0057 §6](../adr/0057-box-schema-versioning-and-migrations.md)). It has no V_k chain and no migration catalog; Spanner installations receive new columns purely through builder updates.

## The mandatory rule

> Every column added to a `*OutboxBuilder` or `*InboxBuilder` MUST ship with a new `V(N+1)` `BoxMigration` entry in the corresponding `*MigrationCatalog` class for the same backend.

A column added to the builder DDL without a matching migration is schema drift. Existing deployments that already passed the previous fresh-install path will never run the new ALTER, so their tables fall behind silently and runtime SQL fails with `invalid column name`. The per-backend drift test catches this at CI time.

## Files you will touch

For an Outbox column landing on all backends:

| Concern | Files (per backend) |
|---------|---------------------|
| Migration catalog (4 relational backends) | `src/Paramore.Brighter.BoxProvisioning.{Backend}/{Backend}OutboxMigrationCatalog.cs` |
| Builder DDL (all 5 backends — Spanner too) | `src/Paramore.Brighter.Outbox.{Backend}/{Backend}OutboxBuilder.cs` |
| Runtime read/write code (all 5 backends if exercised) | `src/Paramore.Brighter.Outbox.{Backend}/{Backend}Outbox.cs` (or similar) |
| Tests | `tests/Paramore.Brighter.{Backend}.Tests/BoxProvisioning/...` |

Files you should **NOT** touch when adding a column:

- `{Backend}BoxDetectionHelper.cs` — detection is data-driven from `LogicalColumns`.
- `{Backend}BoxMigrationRunner.cs`, `{Backend}OutboxProvisioner.cs`, `{Backend}InboxProvisioner.cs` — orchestration is owned by the abstract bases `SqlBoxMigrationRunner` and `SqlBoxProvisioner` and is column-agnostic.
- `Identifiers.cs` — the safe-identifier chokepoint is fixed.

For an Inbox column, swap `Outbox`→`Inbox` throughout. **Note**: the Postgres inbox is V1-only by design (see ADR 0057 §1). Do not add a Postgres inbox migration unless you are deliberately rolling out a new inbox schema across all backends — coordinate with maintainers first.

## Step 1 — Append the new migration to each relational catalog

Each backend's catalog implements `IAmABoxMigrationCatalog` and exposes `IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration)`. The shape is identical across backends; only the `UpScript` grammar differs.

### Required fields on the new `BoxMigration`

- **`Version`** — strictly `previous_version + 1`. No gaps. Same V-number across all four relational backends for outbox columns that land everywhere.
- **`Description`** — short imperative phrase (e.g. `"Add CustomField column"`). Written to `__BrighterMigrationHistory.Description`.
- **`UpScript`** — provider-appropriate **idempotent** ALTER (see grammar table below). UpScripts execute under transactional lock and MUST be safe to re-execute against any prior state (post-bootstrap, post-failed-partial-migration).
- **`LogicalColumns`** — the **cumulative** column set after this migration applies (`Cumulative(N+1)` if you follow the existing pattern). Used by `DetectCurrentVersionAsync` for legacy-table version inference, and by the drift test to compare against the builder DDL.
- **`SourceReference`** — commit SHA (and PR number where available) that introduced the column, e.g. `"d67dac947 / #3790"`. Required from V2 onwards; V1 stays `null`.
- **`IdempotencyCheckSql`** — **SQLite only**. SQLite's grammar lacks `ALTER TABLE ADD COLUMN IF NOT EXISTS`, so the runner needs an explicit existence probe (`SELECT COUNT(*) FROM pragma_table_info(...) WHERE name = ...`). The runner skips the `UpScript` if the result is `> 0`. The other three backends leave this `null`.

### Provider-appropriate idempotent ALTER grammar

| Backend    | UpScript pattern |
|------------|------------------|
| MSSQL      | `IF COL_LENGTH(N'[{schema}].[{table}]', N'{col}') IS NULL ALTER TABLE [{schema}].[{table}] ADD [{col}] {type} NULL;` |
| PostgreSQL | `ALTER TABLE "{schema}"."{table}" ADD COLUMN IF NOT EXISTS "{col}" {type} NULL;` |
| MySQL      | `information_schema.columns` existence check + prepared `ALTER TABLE` (DDL cannot be parameterised in MySQL — see existing `MySqlOutboxMigrationCatalog` for the prepared-statement form) |
| SQLite     | Plain `ALTER TABLE [{table}] ADD COLUMN [{col}] {type} NULL;` — runner skips when `IdempotencyCheckSql` returns `> 0` |

### Worked example — adding `CustomField NVARCHAR(255) NULL` at V8 on MSSQL

Open `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlOutboxMigrationCatalog.cs`.

Append the column array near the existing `s_v*AddedColumns`:

```csharp
private static readonly string[] s_v8AddedColumns = ["CustomField"];
```

Extend the `Cumulative` helper:

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

The existing `AddColumns(schema, table, params (string Column, string Type)[] columns)` helper produces the `IF COL_LENGTH(...) IS NULL ALTER TABLE …` boilerplate.

### Repeat for PostgreSQL, MySQL, SQLite

Apply the same pattern to:

- `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlOutboxMigrationCatalog.cs`
- `src/Paramore.Brighter.BoxProvisioning.MySql/MySqlOutboxMigrationCatalog.cs`
- `src/Paramore.Brighter.BoxProvisioning.Sqlite/SqliteOutboxMigrationCatalog.cs`

The `Version`, `LogicalColumns` membership of the new column, `Description`, and `SourceReference` MUST match across the four backends. The `UpScript` differs by grammar. SQLite additionally sets `IdempotencyCheckSql`.

## Step 2 — No detection-helper change

`DetectCurrentVersionAsync` (one per relational backend, on `IAmAVersionDetectingMigrationHelper<TConn, TTx>`) walks the migration list `V_latest..V1` and returns the first version whose `LogicalColumns` is a subset of the table's actual columns. As long as step 1 populates `LogicalColumns` correctly, legacy-table detection at V8 is automatic.

**Do not edit `{Backend}BoxDetectionHelper.cs`** when adding a column. If you find yourself touching detection logic, you are looking at the wrong file.

## Step 3 — Update the live builder DDL (all 5 backends including Spanner)

Update the CREATE TABLE DDL in each builder so green-field installations land on V8 in one shot. V1's `UpScript` IS the builder DDL (the fresh-install fast path — ADR 0057 §3), so the builder change automatically updates V1's script too.

| Backend    | File |
|------------|------|
| MSSQL      | `src/Paramore.Brighter.Outbox.MsSql/SqlOutboxBuilder.cs` |
| PostgreSQL | `src/Paramore.Brighter.Outbox.PostgreSql/PostgreSqlOutboxBuilder.cs` |
| MySQL      | `src/Paramore.Brighter.Outbox.MySql/MySqlOutboxBuilder.cs` |
| SQLite     | `src/Paramore.Brighter.Outbox.Sqlite/SqliteOutboxBuilder.cs` |
| Spanner    | `src/Paramore.Brighter.Outbox.Spanner/SpannerOutboxBuilder.cs` |

**The Spanner builder must be updated** even though Spanner has no migration catalog — it is the only path by which Spanner installations receive the new column.

Update both text and binary payload variants if the builder has separate ones.

## Step 4 — Update runtime read/write code

If the new column is populated or read by Brighter at runtime (most are), update the Outbox / Inbox implementation classes:

- INSERT statements + parameter binding in `Add` / `AddAsync` methods.
- SELECT statements + result mapping in `Get` / `GetAsync` methods.
- The `Message` / `MessageHeader` model class properties if the column maps to a new field.
- Any payload-mode-specific handling (text vs binary vs JSON/JSONB).

These files live under `src/Paramore.Brighter.Outbox.{Backend}/` and `src/Paramore.Brighter.Inbox.{Backend}/`. They are **separate** from `BoxProvisioning.{Backend}/`.

## Step 5 — Tests

### Drift test (automatic, mandatory)

The per-backend drift test under `tests/Paramore.Brighter.{Backend}.Tests/BoxProvisioning/Drift/` asserts:

```csharp
var expected = DdlColumnExtractor.GetExpectedColumns(builder.GetDDL(...), QuoteStyleFor(backend));
var covered = migrations.Last().LogicalColumns.Union(housekeeping(box, backend));
Assert.Equal(expected, covered, comparerFor(backend));
```

It goes **RED** the moment you update the builder in step 3 without a matching catalog entry, and **GREEN** automatically once both land. The RED-then-GREEN cycle proves the test guards the rule — verify it for yourself by reverting one of your two changes and re-running the test.

The MSSQL/PG/MySQL/SQLite housekeeping definitions live alongside the drift tests (e.g. `MsSqlOutboxHousekeeping.cs` = `["Id"]` for the identity PK). If your new column would change housekeeping membership, that is a structural change and warrants a maintainer review before merge.

### Behaviour tests you should write

- **Migration test** — provision an existing pre-V8 table, verify the V8 ALTER applies, history advances to V8, and the new column is present.
- **Idempotency test** — re-provision a V8-stamped table; nothing changes (no duplicate ALTER, no extra history row, no error).
- **Bootstrap test** — create a table via the V_(N-1) builder (or simulate a pre-V8 install some other way), provision it, verify it bootstraps to synthetic V_(N-1) history and walks forward to V8.
- **Round-trip test** — if the column is exercised at runtime, write a `Message` containing the new field through `Add`, read it back through `Get`, assert equality.

The existing `BoxProvisioning/` test directories under each backend's test project have prior examples for V1..V7 outbox and V1..V2 inbox — use them as templates.

## Checklist

- [ ] New `BoxMigration` entry appended to all 4 relational `*OutboxMigrationCatalog.cs` files with matching `Version`, `Description`, `LogicalColumns`, `SourceReference`.
- [ ] `s_v(N+1)AddedColumns` array + `Cumulative(N+1)` branch added in each of the 4 relational catalog files.
- [ ] Provider-appropriate idempotent `UpScript` written for each backend (use existing `AddColumn`/`AddColumns` helpers).
- [ ] SQLite migration sets `IdempotencyCheckSql`; the other three leave it `null`.
- [ ] Live `*OutboxBuilder` / `*InboxBuilder` DDL updated for **all 5 backends** including Spanner — both text and binary variants where applicable.
- [ ] Read/write code (INSERT / SELECT / model classes) updated for all 5 backends if the column is exercised at runtime.
- [ ] Drift test verified RED then GREEN.
- [ ] Migration / idempotency / bootstrap / round-trip tests added per backend.
- [ ] Postgres inbox **NOT** extended unless deliberately rolling out a new inbox schema across all backends (V1-only — ADR 0057 §1).
- [ ] You did **not** edit `*BoxDetectionHelper.cs`, `*BoxMigrationRunner.cs`, or any `*Provisioner.cs` — the migration mechanism is data-driven.

## Architecture reference

- [ADR 0057 — Box schema versioning and migrations](../adr/0057-box-schema-versioning-and-migrations.md) — versioning model, three-path runner (fresh / bootstrap / normal), advisory-lock abstractions, Spanner exemption, drift-test design.
- [ADR 0058 — Box provisioning RDD role interfaces](../adr/0058-box-provisioning-rdd-role-interfaces.md) — role-based interfaces and template-method bases (background, not directly relevant to column additions).
- [ADR 0059 — Box provisioning abstract base naming symmetry](../adr/0059-box-provisioning-abstract-base-naming-symmetry.md).
- [Spec 0027 README](../../specs/0027-box-schema-versioning-and-migrations/README.md) — archaeological evidence base for V1..V7 outbox / V1..V2 inbox.
- Core abstractions: `src/Paramore.Brighter.BoxProvisioning/` (`IAmABoxMigration`, `BoxMigration` record, `IAmABoxMigrationCatalog`, `SqlBoxMigrationRunner`).
