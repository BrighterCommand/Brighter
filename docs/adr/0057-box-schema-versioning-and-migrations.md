# 57. Box Schema Versioning and Migrations

Date: 2026-04-22

## Status

Accepted

## Context

**Parent Requirement**: [specs/0027-box-schema-versioning-and-migrations/requirements.md](../../specs/0027-box-schema-versioning-and-migrations/requirements.md)
**Extends**: [ADR 0053 — Box Database Migration](0053-box-database-migration.md)
**Supersedes**: ADR 0053 §7 "Detection and Bootstrap" behaviour

**Scope**: This ADR defines the versioning model and migration-chain architecture for evolving outbox and inbox tables across all releases of Brighter's relational backends. It covers the version numbering scheme, detection algorithm, three-path provisioning runner, per-backend conditional-ALTER patterns, and the Spanner special case. The `__BrighterMigrationHistory` table schema from ADR 0053 is preserved. The `IAmABoxMigration` interface is **extended with new required members** — an accepted source-breaking change (see "Consequences → Negative") inherited from the same netstandard2.0 constraint that forced the `SchemaName` decision in spec 0023.

### The problem

ADR 0053 shipped a single-version provisioning model: each backend has exactly one migration, "V1", defined as the *current* full builder DDL. On bootstrap (existing table, no history), `DetectCurrentVersionAsync` attempts to match V1's column set against the table's actual columns; on match it stamps synthetic history at V1, on mismatch it returns `0` and the runner tries to apply V1 — which is a full `CREATE TABLE`, not idempotent on MSSQL, and throws.

Git archaeology (see spec 0027 `README.md`) shows the outbox schema has evolved through **7 distinct additive column changes** since 2015. Every post-2019 production outbox table is missing at least some of the V7 column set — so the spec 0023 superset-match returns false and the bootstrap path breaks silently. The inbox has evolved through **2** column changes on MSSQL/MySQL/SQLite; the Postgres inbox was introduced later (Feb 2021, PR #1401) and shipped with its final column set from day one, so it has **1** version.

ADR 0053 §7 named the intended safe behaviour — "fallback to version 1" — without specifying how "version 1" relates to historical schema states. This ADR answers that: **version 1 is the *oldest* schema state for each backend**, and migrations V2..V_latest fill the gap.

### Forces

1. **Correctness first**: the bootstrap path exists specifically to handle upgrade scenarios. It must work for every previously-released schema state, or the feature is broken for its primary use case.
2. **No data loss**: schemas evolve by adding columns (nullable or defaulted). Existing rows keep their data; new columns are NULL. No rewrite.
3. **Fresh-install performance**: a clean install should not pay the cost of running N historical ALTERs against a table that doesn't exist yet.
4. **Per-backend dialects**: MSSQL, PostgreSQL, MySQL, SQLite, and Spanner each have different `ALTER TABLE ADD COLUMN` syntaxes and different column-existence-check primitives. The abstraction must accommodate these without leaking them into cross-cutting code.
5. **Spanner pragmatics**: Spanner was added recently (Oct 2025) with no known production deployments. A full migration chain benefits no one today; can be added later if adoption materialises.
6. **Audit traceability**: operators and reviewers need to see *which* release introduced *which* migration, so schema drift and upgrade risk are grounded in release history rather than arbitrary version numbers.
7. **Spec 0023 has not shipped**: the `IAmABoxMigration` interface and `BoxMigration` record are free to change. Downstream users of spec 0023 will see breaking API changes at upgrade time — acceptable because the known user population on netstandard2.0 is small and already has to accept source-breaking changes from spec 0023's `SchemaName` addition.

### Constraints

- Target `netstandard2.0` alongside `net8.0/9.0/10.0` (same as spec 0023). No default interface members.
- Preserve the `__BrighterMigrationHistory` table schema from ADR 0053 unchanged.
- Binary and JSON payload variants are fresh-install choices only — cross-mode data conversion is not attempted.

## Decision

### 1. Versioning model: logical column sets, per-backend migration lists

A **version** is defined by a *logical column set* — the collection of column names that a table must contain to be considered at that version. Version numbers are **per-backend** per box type: each backend maintains its own ordered migration list. For outbox the 4 relational backends happen to align at V1..V7 (they evolved in lockstep from 2015 onward); for inbox they diverge.

#### Outbox — 7 versions per backend (uniform alignment)

| V | Logical columns added | Commit | PR / Release |
|---|----------------------|--------|--------------|
| V1 | `MessageId, Topic, MessageType, Timestamp, HeaderBag, Body` (baseline) | — | pre-2019 |
| V2 | + `Dispatched` | `3c30343fa` | 2019-07 |
| V3 | + `CorrelationId, ReplyTo, ContentType` | `79100f509` | #1401 |
| V4 | + `PartitionKey` (also: widen NTEXT→NVARCHAR(MAX); introduce binary variant; absorbs the #3042 MessageId/CorrelationId type change) | `1cdc04b60` / `cff67fd5e` | #2560 / #3464 (Postgres) |
| V5 | + `Source, Type, DataSchema, Subject, TraceParent, TraceState, Baggage` | `b740a68ed` | #3633 |
| V6 | + `WorkflowId, JobId` | `0e79332f1` | #3693 |
| V7 | + `DataRef, SpecVersion` | `d67dac947` | #3790 |

#### Inbox — versions diverge by backend

| Backend | Versions | Notes |
|---------|----------|-------|
| MSSQL | V1 (baseline), V2 (+ `ContextKey`) | V2 landed in `787c31c52` (Oct 2018) |
| MySQL | V1, V2 (+ `ContextKey`) | Same V2 commit |
| SQLite | V1, V2 (+ `ContextKey`) | Same V2 commit |
| **Postgres** | **V1 only** (baseline already includes `ContextKey` + composite PK) | Postgres inbox was introduced in Feb 2021 (`79100f509`, PR #1401) and shipped with `ContextKey` and `PRIMARY KEY (CommandId, ContextKey)` from the first commit. No Postgres-only V2 migration exists because no pre-ContextKey Postgres inbox ever shipped. |
| Spanner | fresh-only | Degenerate (no prod users) |

This per-backend arrangement eliminates the need for a Postgres composite-PK rebuild migration (the failure mode flagged in spec 0027's design review as an over-engineered migration for a schema state that never existed).

#### Folded changes (not separate migrations)

- **MessageId/CorrelationId UNIQUEIDENTIFIER→NVARCHAR type change** (outbox, `fd71cc1bc` PR #3042, Mar 2024): folded into V4. Any table with `PartitionKey` (V4 logical column) is detected as V4 regardless of MessageId type. Pre-#3042 tables keep UNIQUEIDENTIFIER; application code accommodates both. Documented edge case for users with pre-#3042 string IDs.
- **CommandBody type widening** (inbox, NTEXT→NVARCHAR(MAX) and similar): fresh-install only; no migration.
- **CommandId type change on inbox** (#3042 MSSQL/MySQL/Postgres only, *not* SQLite): fresh-install only; pre-#3042 tables keep UNIQUEIDENTIFIER. SQLite's type affinity makes this a no-op regardless.
- **Payload-mode variants** (Text/Binary/JSON/JSONB): fresh-install choices. Incompatible data encoding makes cross-mode migration invalid. Handled by existing `*PayloadModeValidator` classes.

#### Backend-specific housekeeping columns

Backend-specific PK/housekeeping columns live *inside* each backend's V1 DDL and do not participate in logical-version comparison:

**Outbox V1 housekeeping**:

| Backend | V1 housekeeping |
|---------|-----------------|
| MSSQL | `Id BIGINT NOT NULL IDENTITY` + `PRIMARY KEY (Id)` |
| Postgres | `Id BIGSERIAL PRIMARY KEY` + `MessageId UNIQUE` |
| MySQL | `Created TIMESTAMP NOT NULL DEFAULT NOW(3)` + `CreatedID INT AUTO_INCREMENT UNIQUE` + `PRIMARY KEY (MessageId)` |
| SQLite | `MessageId COLLATE NOCASE` (no explicit PK — uses implicit rowid) |

**Inbox V1 housekeeping**:

| Backend | V1 housekeeping |
|---------|-----------------|
| MSSQL | `Id BIGINT IDENTITY(1,1) NOT NULL` + `PRIMARY KEY (Id)` |
| Postgres | `PRIMARY KEY (CommandId, ContextKey)` (composite — `ContextKey` is part of V1 baseline for Postgres, per §1 Inbox table) |
| MySQL | `PRIMARY KEY (CommandId)` |
| SQLite | `PRIMARY KEY (CommandId)` via `CONSTRAINT PK_MessageId` |

### 2. Detection algorithm

`DetectCurrentVersionAsync` queries `information_schema.columns` (or equivalent) for the actual column names present. Before walking versions, it **gates on a discriminator column** — a column specific enough to Brighter that false-positive matches on legacy non-Brighter tables are implausible:

- **Outbox discriminator**: `HeaderBag` — present since V1 and uncommon as a column name outside Brighter's message-bag pattern
- **Inbox discriminator**: `CommandBody` — similarly Brighter-specific

Detection distinguishes three outcomes via return value:
- `-1` — discriminator absent: "table exists but is not a Brighter box" (operator pointed at the wrong table)
- `0` — discriminator present but no version matched: "corrupt or unknown Brighter schema" (table is Brighter-shaped but at a pre-V1 or otherwise unexpected state)
- `V ≥ 1` — highest version whose logical column set is a subset of actual columns

```csharp
// Lives in *BoxDetectionHelpers as a static method (per Phase 1.5 / 2.5 / 3.5 / 4.5).
// Backend-specific connection / transaction types vary; signature shown here is illustrative.
public static async Task<int> DetectCurrentVersionAsync(
    DbConnection connection,
    DbTransaction? txn,
    string tableName,
    string? schemaName,
    IReadOnlyList<IAmABoxMigration> migrations,
    string discriminatorColumn,
    CancellationToken ct)
{
    var actualColumns = await GetTableColumnsAsync(connection, txn, tableName, schemaName, ct);  // HashSet<string> with backend-specific comparer
    if (!actualColumns.Contains(discriminatorColumn))
        return -1;  // not a Brighter table — bail out
    for (var version = migrations.Count; version >= 1; version--)
    {
        if (actualColumns.IsSupersetOf(migrations[version - 1].LogicalColumns))
            return version;
    }
    return 0;  // has discriminator but no version matched — unknown / corrupt
}
```

The bootstrap-path error message branches on this: `-1` → "Table *{name}* exists but is not a Brighter outbox/inbox (missing discriminator column *{column}*); check your configured table name"; `0` → "Table *{name}* appears to be a Brighter outbox/inbox but does not match any known schema version; manual inspection required".

**Correctness properties**:
- **Monotonic additivity**: every logical version's column set is a superset of its predecessor's. An actual-columns set matching V_k also matches V_1..V_{k-1}. Walking top-down returns the highest matching version in O(N) comparisons where N = migrations.Count.
- **Name-only comparison**: types and nullability are ignored. This is what enables folding the UNIQUEIDENTIFIER→NVARCHAR change (same names, different types). The discriminator gate bounds the false-positive risk.
- **Per-backend case sensitivity** (inherited from spec 0023): MSSQL/MySQL/SQLite use `StringComparer.OrdinalIgnoreCase`; Postgres uses `StringComparer.Ordinal` with lowercase literals (information_schema folds names); Spanner uses `StringComparer.Ordinal` matching the builder DDL casing.

### 3. Three-path provisioner (under the advisory lock)

`ProvisionAsync` branches into three paths based on `BoxTableState`. The branching lives inside each runner's existing `MigrateAsync(tableName, schemaName, migrations, tableState, ct)` signature — **no new interface methods are added**. The advisory lock wraps the entire path (not just the history insert) to close the concurrent-CREATE-TABLE race that bare `CREATE TABLE` would otherwise cause.

```
┌─────────────────────────────────────────────────────────────┐
│                    ProvisionAsync                           │
│                                                              │
│  DetectTableStateAsync → BoxTableState(exists, hasHistory, currentVersion)
│  runner.MigrateAsync(tableName, schemaName, migrations, tableState, ct)
│  → branches on tableState inside the runner                 │
└─────────────┬─────────────┬─────────────┬───────────────────┘
              │             │             │
              ▼             ▼             ▼
         ┌─────────┐   ┌─────────┐   ┌─────────┐
         │  FRESH  │   │BOOTSTRAP│   │ NORMAL  │
         │ !exists │   │ exists, │   │ exists, │
         │         │   │!history │   │ history │
         └────┬────┘   └────┬────┘   └────┬────┘
              │             │             │
              ▼             ▼             ▼
         Acquire lock   Acquire lock  Acquire lock
         Run V1 UpScript Detect V →    Read MAX(V)
         Insert history    -1: throw   Run migs above MAX
         at V_latest       0: throw    (each idempotent)
         Release lock     ≥1: stamp    Release lock
                         V; run
                         V+1..V_latest
                         Release lock
```

**Fresh path** (`!TableExists`):
- Acquire serialization primitive (MSSQL `sp_getapplock` + `BeginTransaction`; Postgres `pg_try_advisory_lock` + `BEGIN`; MySQL `GET_LOCK`; SQLite `BEGIN IMMEDIATE TRANSACTION` with `SQLITE_BUSY` retry; Spanner uses degenerate runner — see §6)
- Re-check table existence under the lock (closes the detect→create race; if the table now exists, fall through to bootstrap)
- Execute V1 UpScript (current builder DDL) — safe because we've verified under lock that the table doesn't exist
- Insert one history row: `(SchemaName, BoxTableName, V_latest, "fresh install at V_latest")`
- Release lock

**Bootstrap path** (`TableExists && !HistoryExists`):
- Acquire advisory lock
- Re-check history existence under the lock (addresses spec 0023 R2 TOCTOU)
- Re-invoke detection under the lock by calling the per-backend `*BoxDetectionHelpers.DetectCurrentVersionAsync(connection, txn, tableName, schemaName, migrations, discriminator, ct)` static helper. Spec 0027 introduces this method as part of Phase 1.5 / 2.5 / 3.5 / 4.5 — moving detection out of `*OutboxProvisioner` / `*InboxProvisioner` into the existing `*BoxDetectionHelpers` static classes (which currently host only the schema-introspection primitives `DoesTableExistAsync` / `DoesHistoryExistAsync` / `GetMaxVersionAsync` / `GetTableColumnsAsync` from commit `4db713c8`). After this move, both the provisioner (pre-lock, populating `BoxTableState.CurrentVersion`) and the runner (post-lock, TOCTOU re-detection) invoke the same helper — single source of detection truth. The runner does not trust `state.CurrentVersion` from the pre-lock pass; it re-reads
- If re-detected `V == -1`: throw `ConfigurationException("Table exists but is not a Brighter box; check configured table name")` — operator pointed at the wrong table
- If re-detected `V == 0`: throw `ConfigurationException("Table appears to be a Brighter box but does not match any known schema version; manual inspection required")` — Brighter-shaped but corrupt
- If re-detected `V >= 1`: insert synthetic history `(SchemaName, BoxTableName, V, "bootstrap: detected at V{V}")`
- Run migrations `V+1..V_latest` (each UpScript is idempotent per §5)
- Release lock

**Normal path** (`TableExists && HistoryExists`):
- Acquire advisory lock
- Read `MAX(MigrationVersion)` for `(SchemaName, BoxTableName)`
- Run migrations above MAX (each UpScript idempotent; `IsMigrationAppliedAsync` check before each as defence-in-depth)
- Release lock

**Why V1 does not need to be idempotent**: V1 is the full `CREATE TABLE` from the current builder, and the three-path logic guarantees it runs only when `!TableExists` *and* no concurrent instance has since created it (re-checked under lock). V2+ UpScripts MUST be idempotent per §5 because they may run on any intermediate schema state — including post-bootstrap and post-failed-partial-migration.

### 4. Migration object — extend, don't replace

`IAmABoxMigration` gains one new required member and two optional members:

```csharp
public interface IAmABoxMigration
{
    int Version { get; }
    string Description { get; }
    string UpScript { get; }                       // unchanged — a single SQL statement (or batch for backends that support it)
    ISet<string> LogicalColumns { get; }           // NEW (required) — ISet (not IReadOnlySet) because IReadOnlySet<T> is not available on netstandard2.0; contract is read-only-by-convention (implementations populate once and never mutate)
    string? SourceReference { get; }               // NEW (nullable) — e.g. "3c30343fa / #1401"
    string? IdempotencyCheckSql { get; }           // NEW (nullable) — scalar-returning SQL; >0 means skip UpScript
}

public record BoxMigration(
    int Version,
    string Description,
    string UpScript,
    ISet<string> LogicalColumns,
    string? SourceReference = null,
    string? IdempotencyCheckSql = null
) : IAmABoxMigration;
```

**Rationale**:
- `UpScript` stays a plain single-statement string (or backend-native batch form where supported — e.g. MSSQL's `IF COL_LENGTH … ALTER TABLE` batch). Parameterisation happens at factory-call time via `*Migrations.All(config)`.
- `LogicalColumns` is the detection-comparison set. Required so every migration carries its own version signature.
- `SourceReference` is a free-text audit pointer (commit SHA + PR or release tag). Nullable — V1 has no single commit.
- `IdempotencyCheckSql` is the "should I skip `UpScript`?" primitive for backends whose dialect doesn't support self-idempotent DDL. Runner executes it as a scalar; if the returned count is > 0, the migration is considered already applied and `UpScript` is skipped. **Used exclusively by SQLite** (V2+ migrations). MSSQL (`IF COL_LENGTH`), Postgres (`IF NOT EXISTS`), and MySQL (`information_schema` prepared-statement) embed the check in `UpScript` itself and leave `IdempotencyCheckSql` null.
- Payload-mode branching is not encoded in the migration object. V1 DDL comes from `*OutboxBuilder.GetDDL(table, binary)` which forks internally. V2+ ALTERs are pure column additions, independent of payload mode.

**Source-breaking consequence**: Adding `LogicalColumns` as required makes every existing `IAmABoxMigration` implementation fail to compile. This is accepted (see Negative consequences). The current internal `BoxMigration` record is updated with the two new parameters; test stubs and any external implementors must add them on recompile.

### 5. Per-backend conditional-ALTER pattern

V2+ UpScripts for each backend use the cheapest portable pattern that is **idempotent under the advisory lock**. Atomicity between the existence check and the DDL is guaranteed by the lock, not by SQL-level atomicity — the lock serializes Brighter provisioner concurrency. The pattern below therefore reads as "check-then-ALTER" and is safe provided the runner holds the lock.

| Backend | Pattern | Example (V4 adds PartitionKey) |
|---------|---------|--------------------------------|
| MSSQL | `IF COL_LENGTH(...) IS NULL ALTER TABLE ADD` | `IF COL_LENGTH(N'[{schema}].[{table}]', N'PartitionKey') IS NULL ALTER TABLE [{schema}].[{table}] ADD [PartitionKey] NVARCHAR(255) NULL;` |
| PostgreSQL | `ADD COLUMN IF NOT EXISTS` (native 9.6+) | `ALTER TABLE {schema}.{table} ADD COLUMN IF NOT EXISTS PartitionKey varchar(128) NULL;` |
| MySQL | `information_schema` + prepared statement | `SET @q = IF((SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = '{table}' AND column_name = 'PartitionKey') = 0, 'ALTER TABLE `{table}` ADD COLUMN `PartitionKey` VARCHAR(128) NULL', 'SELECT 1'); PREPARE stmt FROM @q; EXECUTE stmt; DEALLOCATE PREPARE stmt;` |
| SQLite | `IdempotencyCheckSql` + single-statement `UpScript` | `IdempotencyCheckSql`: `SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name='PartitionKey'`. `UpScript`: `ALTER TABLE [{table}] ADD COLUMN [PartitionKey] TEXT NULL`. Runner executes the check first; if the scalar result is 0, runs `UpScript`; otherwise skips. |
| Spanner | `ADD COLUMN IF NOT EXISTS` (GoogleSQL) | Not used — Spanner has no migration chain (see §6) |

**Concurrency attribution**: Atomicity of "check-then-ALTER" comes from a backend-appropriate serialization primitive held by the runner for the duration of the chain:

| Backend | Serialization primitive | Whole-chain guarantee? |
|---------|------------------------|-----------------------|
| MSSQL | `sp_getapplock @LockOwner='Transaction'` inside `BeginTransaction` | Yes — lock released on transaction commit/rollback |
| Postgres | `pg_try_advisory_lock(hash)` inside `BEGIN` | Yes — lock held until `pg_advisory_unlock` or end of session |
| MySQL | `GET_LOCK(name, timeout)` (session-scoped) | Yes — lock held until explicit `RELEASE_LOCK` or session end |
| SQLite | `BEGIN IMMEDIATE TRANSACTION` at runner entry | Yes — writer-lock acquired at `BEGIN IMMEDIATE`, held until COMMIT/ROLLBACK. `SQLITE_BUSY` is an expected transient error; runner retries with backoff |
| Spanner | none — see §6 degenerate runner |

SQLite's `BEGIN IMMEDIATE` is the key choice for the SQLite runner: unlike `BEGIN DEFERRED` (the default), it acquires the write-lock *at statement time* rather than at first-write, guaranteeing that two concurrent Brighter processes serialize cleanly. The trade-off is that long migration chains on SQLite block readers. Acceptable because SQLite is primarily a dev/test backend and migrations run once per schema upgrade.

**It does not serialize with external DBA activity** — if someone manually runs ALTER TABLE outside Brighter's control, the interleaving is undefined. Operators running Brighter alongside manual schema changes should coordinate externally.

**Table-name trust boundary**: Table names are supplied via `IAmARelationalDatabaseConfiguration.OutBoxTableName` / `InBoxTableName` / `SchemaName`. These are **application-configured code values**, not user input. They are nevertheless interpolated into DDL strings via `string.Format`. The existing ADR 0053 treats this as a configured-trust-source boundary; spec 0027 inherits the same assumption. Operators who inject attacker-controlled values into configuration have bigger problems than schema migrations.

**Rationale for MySQL's portable path**: `ADD COLUMN IF NOT EXISTS` arrived in MySQL 8.0.29 (2022). Brighter supports older deployments via Aurora and forks. The `information_schema` + prepared-statement pattern works back to MySQL 5.7 and is dialect-neutral enough to reuse across the 2 migrations needed.

**Rationale for SQLite's `IdempotencyCheckSql` approach**: SQLite's `ALTER TABLE ADD COLUMN` has no `IF NOT EXISTS` form and no equivalent single-statement primitive. Embedding a `SELECT COUNT(*) ... ; conditional ALTER` in `UpScript` would require either runner-side SQL parsing or a multi-statement execution convention that other backends don't need. A nullable `IdempotencyCheckSql` interface member is the cleanest accommodation: SQLite migrations set it, everyone else leaves it null, runner branches on presence.

### 5a. Error handling and mid-chain failures

The runner's transactional model is per-backend:

| Backend | Transactional wrapping | Mid-chain failure behaviour |
|---------|----------------------|----------------------------|
| MSSQL | Whole chain in a single `SqlTransaction` with `sp_getapplock @LockOwner='Transaction'` | Failure rolls back **all** migrations in the batch + all history rows written in the batch. Next invocation re-runs from `MAX(V)` in history, which reflects the last committed state. |
| Postgres | Whole chain in a single `NpgsqlTransaction`; lock via `pg_try_advisory_lock` | Same as MSSQL — Postgres supports transactional DDL. Rollback is atomic. |
| MySQL | **Per-migration** explicit commit (MySQL issues an implicit commit on every DDL statement). Lock via session-scoped `GET_LOCK` held across commits. | A mid-chain failure leaves the table at an intermediate state *V_k*, with history rows for V1..V_k already committed. Next invocation reads `MAX(V) = k` and resumes from V_{k+1}. Each migration must therefore be **individually** idempotent (which the `information_schema` pattern guarantees). |
| SQLite | Whole chain in `BEGIN IMMEDIATE` + `COMMIT` | SQLite supports transactional DDL. Rollback is atomic. Failure leaves no partial state. |
| Spanner | Not applicable (degenerate runner; each operation is independent) | Spanner's own DDL error handling (the R4 `AlreadyExists` catch) governs. |

**History-row timing**:
- MSSQL/Postgres/SQLite: history row inserted immediately after each migration's DDL, inside the same transaction. Either both are visible on commit or neither is (on rollback).
- MySQL: history row inserted immediately after each migration's DDL. Because each DDL is an implicit commit, the history row is too — state is durable per-migration.

**Recovery invariant**: after any failure, `MAX(MigrationVersion)` in history reflects the highest *successfully applied* migration. The runner's next invocation consults `MAX(V)` and resumes from the next version. Combined with the idempotency guarantee of each V2+ UpScript (per §5), interrupted chains are safely re-runnable.

**Exception types**:
- `ConfigurationException` — bootstrap detection returned `-1` (not a Brighter box) or `0` (unknown schema); or a backend/version mismatch (Spanner)
- Backend-native exceptions — rethrown. The provisioner's `BoxProvisioningHostedService` re-raises as `ConfigurationException` to fail host startup (per spec 0023)

### 6. Spanner: degenerate fresh-only runner

Spanner has zero known production deployments (confirmed in spec 0023 review; reaffirmed in spec 0027 requirements A-2). Writing a migration chain for Spanner is work that benefits no one today.

`SpannerBoxMigrationRunner` implements a minimal strategy:
- **Fresh install** (`!TableExists`): execute current builder DDL via `CreateDdlCommand` (with gRPC status-code error handling from spec 0023's R4 fix); insert history row at `V_latest` (outbox: V7, inbox: V2)
- **Existing table without history** (would be bootstrap elsewhere): **apply the same discriminator gate as §2** — query `information_schema.columns` for `HeaderBag` (outbox) or `CommandBody` (inbox). If absent: throw `ConfigurationException("Spanner table exists but is not a Brighter box; check configured table name")`. If present: insert history row at `V_latest` with description `"bootstrap: spanner-assumed-current (no known legacy installations, A-2)"`. Trusts A-2 assumption for version identification but not for table identity
- **Existing table with history**: no-op if `MAX(V) == V_latest`; throw `ConfigurationException("Spanner migration list out of sync with installed version")` if the installed version exceeds `V_latest` (indicates a build-time mismatch)

**Spanner concurrency for the history INSERT** (addressing spec 0023 R4): the runner calls `IsMigrationAppliedAsync` immediately before the INSERT and skips if already applied. This is the same pattern as the other backends' normal-path defence-in-depth. For Spanner the check is the primary protection since there is no advisory lock.

No `IAmABoxMigration` list is exposed by Spanner. The provisioner holds its own "current version" constant.

### Architecture overview

```
┌───────────────────────────────────────────────────────────────┐
│                    Per-backend Provisioner                    │
│  (MsSqlOutboxProvisioner, PostgreSqlInboxProvisioner, etc.)   │
│                                                                │
│  holds: migrations: IReadOnlyList<IAmABoxMigration>           │
│         (V1..V_latest, generated via *Migrations.All(config)) │
│  holds: runner: IAmABoxMigrationRunner                        │
│                                                                │
│  ProvisionAsync:                                              │
│   1. DetectTableStateAsync — calls *BoxDetectionHelpers       │
│      .DetectCurrentVersionAsync (pre-lock pass, populates     │
│      BoxTableState.CurrentVersion)                            │
│   2. runner.MigrateAsync(table, schema, migrations, state, ct)│
│      — runner branches on state.TableExists/HistoryExists    │
└───────────────────┬───────────────────────────────────────────┘
                    │
                    ▼
┌───────────────────────────────────────────────────────────────┐
│              IAmABoxMigrationRunner.MigrateAsync              │
│  (same signature as spec 0023; branching logic internal)      │
│                                                                │
│  Acquire advisory lock (backend-specific);                    │
│  TOCTOU re-check table / history existence under the lock;    │
│                                                                │
│  if !tableExistsNow:                                          │
│      execute V1.UpScript (full CREATE TABLE from builder)     │
│      insert history row at V_latest                           │
│                                                                │
│  elif !historyExistsNow:                                      │
│      // re-detect under the lock — TOCTOU defence:            │
│      v = *BoxDetectionHelpers.DetectCurrentVersionAsync(      │
│             conn, txn, table, schema, migrations, discrim)    │
│      if v == -1 or 0: throw ConfigurationException            │
│      insert synthetic history at v                            │
│      foreach M in migrations where M.Version > v:             │
│          apply M.UpScript (idempotent per §5)                 │
│          insert history row at M.Version                      │
│                                                                │
│  else:                                                         │
│      let max = MAX(MigrationVersion) from history             │
│      foreach M in migrations where M.Version > max:           │
│          IsMigrationAppliedAsync check                        │
│          apply M.UpScript                                     │
│          insert history row at M.Version                      │
│                                                                │
│  Release lock                                                 │
└───────────────────────────────────────────────────────────────┘
```

**Detection helper ownership**: per-backend `*BoxDetectionHelpers` static classes (`MsSqlBoxDetectionHelpers`, `PostgreSqlBoxDetectionHelpers`, `MySqlBoxDetectionHelpers`, `SqliteBoxDetectionHelpers`, `SpannerBoxDetectionHelpers`) are the single source of detection logic. Today they host only schema-introspection primitives (`DoesTableExistAsync`, `DoesHistoryExistAsync`, `GetMaxVersionAsync`, `GetTableColumnsAsync` — extracted in commit `4db713c8`). Spec 0027 Tasks 1.5 / 2.5 / 3.5 / 4.5 add `DetectCurrentVersionAsync` to each, **moving** the existing private detection methods from `*OutboxProvisioner` / `*InboxProvisioner` into the helpers. After the move, the provisioner calls the helper pre-lock to populate `BoxTableState`; the runner calls the same helper post-lock for TOCTOU-safe re-detection in the bootstrap path. No detection logic is duplicated between layers.

### Key components

- **`IAmABoxMigration`** (existing, **extended**): gains `LogicalColumns` (required), `SourceReference` (nullable), and `IdempotencyCheckSql` (nullable — used by SQLite only). Source-breaking.
- **`BoxMigration`** (existing, **extended**): positional record adds three parameters matching the interface additions.
- **`IAmABoxMigrationRunner`** (existing, unchanged signature): implementations gain three-path branching inside `MigrateAsync`; no new methods.
- **`*OutboxMigrations` / `*InboxMigrations`** (existing, **expanded**): per-backend static classes return `V1..V_latest` lists via `All(config)` factory.
- **`*OutboxProvisioner` / `*InboxProvisioner`** (existing, **simplified**): `DetectTableStateAsync` now delegates to `*BoxDetectionHelpers.DetectCurrentVersionAsync` (added in Phase 1.5/2.5/3.5/4.5); the previously-private detection methods and `V1Columns` static sets are deleted from the provisioners. Detection logic no longer lives in the provisioner.
- **`*BoxDetectionHelpers`** (existing, **extended**): per-backend static classes (`MsSqlBoxDetectionHelpers`, `PostgreSqlBoxDetectionHelpers`, `MySqlBoxDetectionHelpers`, `SqliteBoxDetectionHelpers`, `SpannerBoxDetectionHelpers`) currently host only schema-introspection primitives (`DoesTableExistAsync`, `DoesHistoryExistAsync`, `GetMaxVersionAsync`, `GetTableColumnsAsync`). Spec 0027 adds a new `DetectCurrentVersionAsync(connection, txn, tableName, schemaName, migrations, discriminatorColumn, ct)` static method to each, walking the migration list top-down with a discriminator gate per §2. This is the **single source of detection truth** — invoked by both the provisioner (pre-lock) and the runner (post-lock TOCTOU re-check).
- **`SpannerBoxMigrationRunner`** (existing, **reworked**): fresh-only strategy; no migration list; adds `IsMigrationAppliedAsync` gate on history INSERT (addresses spec 0023 R4).

### Technology choices

- **No new NuGet dependencies**: the migration chain uses the same `Microsoft.Data.SqlClient`, `Npgsql`, `MySqlConnector`, `Microsoft.Data.Sqlite`, and `Google.Cloud.Spanner.Data` packages already referenced by spec 0023's provisioners.
- **No schema change to `__BrighterMigrationHistory`**: the existing `(SchemaName, BoxTableName, MigrationVersion)` PK and `Description, AppliedAt` columns are sufficient.
- **Migration list is code, not data**: `MsSqlOutboxMigrations.All(config)` is a compile-time-ordered list; versions are not discovered by reflection or external config.
- **Drift-detection test (new, part of this spec)**: prevents the "forgot to add V8" class of bug where a column is added to a builder without a corresponding new migration. Concrete design:
  - Each `*OutboxBuilder` / `*InboxBuilder` exposes a test-only helper `GetExpectedColumns(string tableName, bool binaryPayload)` that parses its own DDL template(s) and returns the column-name set. The helper lives in the test project, not the production builder — a simple regex over the `CREATE TABLE` body is sufficient because the builders are developer-authored text with known quoting per backend.
  - Per-backend test (one each: `*OutboxMigrationsDriftTests`, `*InboxMigrationsDriftTests`) asserts:
    ```
    var expected = Builder.GetExpectedColumns(tableName, payloadMode);
    var covered = migrations.Last().LogicalColumns.Union(housekeeping(box, backend));
    Assert.Equal(expected, covered, CaseSensitiveOrCaseInsensitiveComparer(backend));
    ```
  - `housekeeping(box, backend)` is a static lookup populated from the two housekeeping tables in §1.
  - Test runs against the Text DDL variant by default; Binary and JSON variants are asserted separately (they share column names with Text — only `Body`/`CommandBody` type differs).
  - Runs in every backend's unit-test project (no Docker required — operates on DDL strings only).

This is new test infrastructure, shipped in step 1 of the implementation sequence — not an update to a pre-existing test.

### Implementation approach

1. **Shared groundwork (one commit)**: extend `IAmABoxMigration` + `BoxMigration` with `LogicalColumns`, `SourceReference`, `IdempotencyCheckSql`; **add** the builder-vs-latest-migration-list drift test (new test infrastructure per "Technology choices") for each backend; update existing internal `*Migrations.All(config)` call sites (one per backend × box type — currently each returns a single V1 entry per spec 0023) to pass the new parameters (V1 only at this point; full V2..V_latest expansion happens in per-backend steps).
2. **MSSQL reference implementation (one commit)**: flesh out `MsSqlOutboxMigrations` to V1..V7 and `MsSqlInboxMigrations` to V1..V2; update `MsSqlBoxMigrationRunner.MigrateAsync` for three-path branching (including fresh-path lock wrapping); update `MsSqlOutboxProvisioner`/`MsSqlInboxProvisioner` detection with discriminator gate; add tests.
3. **Port to Postgres, MySQL, SQLite (one commit per backend)**: same pattern. Postgres inbox gets V1 only (no V2 migration — baseline is composite-PK). Each backend commit includes its tests.
4. **Rework Spanner runner (one commit)**: degenerate fresh-only; add `IsMigrationAppliedAsync` gate.
5. **Test sweep**: bootstrap-at-V_k tests per backend × box type; idempotency (run twice); concurrent bootstrap; spec-0023-era transition (AC-6 from requirements).
6. **Documentation**: update `.agent_instructions/box_provisioning.md` with "every new column → new V(N+1) migration + idempotency check + test" rule; release notes entry for the `IAmABoxMigration` interface break.

Each backend migration commit is self-contained with its tests. Per TDD convention in this repo, each commit leads with its test suite.

## Consequences

### Positive

- **Correct bootstrap for every historical schema**: any Brighter table from 2015 onward is recognised and upgraded without error
- **Fresh install stays fast**: one `CREATE TABLE` + one history insert under one lock acquisition, same order of magnitude as before
- **Forward-extensible**: every future column addition becomes a routine V(N+1) migration. The drift-detection test ensures builder and migration list stay in sync
- **Audit trail**: every migration references its introducing commit/PR via `SourceReference`
- **Postgres inbox simplification**: no composite-PK rebuild migration needed (Postgres inbox was born with the V2-equivalent shape)
- **False-positive protection**: discriminator check (`HeaderBag` / `CommandBody`) prevents detection on tables that happen to share one or two column names with Brighter's schema
- **Spanner stays simple**: no speculative migration chain; R4 concurrency fix inline in the degenerate runner

### Negative

- **Source-breaking change to `IAmABoxMigration`**: adding required members `LogicalColumns` and `SourceReference` breaks any external implementor on recompile. Accepted because:
  - Spec 0023 has not shipped; the public API is still in flux
  - The netstandard2.0 constraint precludes default-interface-member softening (same issue as spec 0023's `SchemaName` decision — see ADR 0053 "Negative" consequences)
  - Known downstream user population is small; those users already need to adapt to spec 0023's breaking additions
  - Release notes must call this out alongside spec 0023's `SchemaName` change
- **Migration volume**: per backend, outbox has 7 versions and inbox has 2 (Postgres: 1). Most are 5–15 lines of DDL. Boilerplate-heavy but mechanical — probably ~1 day per backend once MSSQL is settled
- **Conditional-ALTER patterns vary by backend**: four dialect-specific patterns to maintain (MSSQL's `COL_LENGTH`, Postgres's `IF NOT EXISTS`, MySQL's `information_schema`-prepared-statement, SQLite's `pragma_table_info`). Mitigated by encapsulating each in its backend's migration list — cross-cutting code never touches the SQL
- **Pre-V1 schema not supported**: if any installation has an outbox table from before the 2015 baseline, this system won't detect it. No known such installations
- **Optional `IdempotencyCheckSql` member used only by SQLite**: adds a nullable interface member that is always null on MSSQL/Postgres/MySQL migrations. Cleaner than embedding a mini-language in `UpScript`, but the runner must conditionally branch on its presence. Minor interface bloat for backend-dialect accommodation

### Risks and Mitigations

- **Risk**: a column-name collision with a non-Brighter table in the same schema causes false-positive detection → provisioner attempts migrations on a user's unrelated table
  - **Mitigation**: discriminator gate (`HeaderBag` for outbox, `CommandBody` for inbox). These columns are specific to Brighter's semantic model and unlikely to appear in unrelated tables. Additionally, the operator explicitly pointed the provisioner at this table name via `IAmARelationalDatabaseConfiguration.OutBoxTableName` — misconfiguration is on the operator, not an autonomous failure
- **Risk**: concurrent bootstrap or concurrent fresh install duplicates history rows or collides on CREATE TABLE
  - **Mitigation**: advisory lock wraps the whole runner path for MSSQL/Postgres/MySQL; SQLite's file lock provides serialisation; Spanner uses `IsMigrationAppliedAsync` gate. TOCTOU re-check of table/history existence inside the lock for all backends
- **Risk**: a migration silently corrupts existing data on a production table
  - **Mitigation**: all migrations are `ADD COLUMN` (nullable) — no backfill, no rewrite, no drop. Tested per backend per version
- **Risk**: new Brighter releases add columns to a builder without updating the migration list → fresh install works but bootstrap lags
  - **Mitigation**: drift-detection test per backend asserts `latest_migration.LogicalColumns ∪ housekeeping == builder.CurrentColumns`. Failing this test at CI blocks the release
- **Risk**: MSSQL table name with embedded `]` character breaks the `COL_LENGTH` interpolation
  - **Mitigation**: documented as configured-trust-source. Operators controlling `OutBoxTableName` have the same trust level as application code. This is the inherited spec 0023 assumption

## Alternatives Considered

### A. Do nothing — ship spec 0023 as-is, document the limitation

Spec 0023 R1 was scored 85/100 (blocking). "Known limitation" is not acceptable for the *primary use case* of the provisioning feature (bootstrapping existing installations). Rejected.

### B. Fall back to V1 on unknown schema; make V1 idempotent

Modify the existing V1 to use `CREATE TABLE IF NOT EXISTS` (MSSQL: `IF OBJECT_ID('{0}') IS NULL CREATE TABLE ...`). On bootstrap, stamp V1 unconditionally. Rely on future V2+ migrations to add missing columns.

**Rejected** because:
- It lies about the version — a pre-V4 table stamped as "V1 applied" will then skip V2/V3 migrations under the normal-path logic, never acquiring those columns
- Future column additions would have to be written as "ALTER if missing" but with no understanding of which version they correspond to — the auditability property is lost
- The migration-history table becomes decorative rather than a true record

### C. Column-name+type detection

Introspect column types as well as names; distinguish a V4-outbox (MessageId as UNIQUEIDENTIFIER) from a V5-outbox (MessageId as NVARCHAR). This would let us model the #3042 type change as a real migration.

**Rejected** because:
- Pre-#3042 installations are an acknowledged edge case (spec 0027 A-1). Building machinery to handle them well when the population is probably empty is wasted effort
- Type introspection multiplies per-backend dialect complexity
- Name-only detection is sufficient for every real upgrade case

### D. Preserve `IAmABoxMigration` unchanged; add `LogicalColumns` via a separate interface

Keep `IAmABoxMigration` stable; add `IAmAColumnAwareBoxMigration : IAmABoxMigration` with `LogicalColumns`. Runners check for the additional interface at runtime.

**Rejected** because:
- The spec has not shipped; breaking it cleanly is cheaper than carrying a two-interface inheritance complication forever
- Every migration we ship *will* carry `LogicalColumns` (detection requires it), so the optional interface would be universal anyway — making the split artificial
- Runtime interface-probing is harder to reason about than a compile-time required member
- The netstandard2.0 + spec-0023 context already forces a source-breaking change for `SchemaName`; adding `LogicalColumns` in the same release is a single "accept breaking additions" event rather than two

### E. Postgres composite-PK rebuild migration (design review finding #1)

Earlier draft of this ADR specified a 5-step composite-PK rebuild for Postgres inbox V2 (drop old PK, add column, backfill sentinel, drop, add composite PK). **Rejected** after design review established that Postgres inbox was born with composite PK — no such migration has anything to migrate.

### F. Single-ADR versioning per spec-0023 + spec-0027 vs multiple focused ADRs

Considered splitting into three ADRs (numbering, runner, idempotency). Rejected because the three aspects are a single architectural decision — changes to one typically require changes to the others. One narrative is easier to review than three cross-referencing documents.

## References

- Spec: [specs/0027-box-schema-versioning-and-migrations/requirements.md](../../specs/0027-box-schema-versioning-and-migrations/requirements.md)
- Spec README (archaeology evidence): [specs/0027-box-schema-versioning-and-migrations/README.md](../../specs/0027-box-schema-versioning-and-migrations/README.md)
- Design review: [specs/0027-box-schema-versioning-and-migrations/review-design.md](../../specs/0027-box-schema-versioning-and-migrations/review-design.md)
- Parent ADR: [0053-box-database-migration.md](0053-box-database-migration.md)
- Spec 0023 code review: [specs/0023-box_database_migration/review-code.md](../../specs/0023-box_database_migration/review-code.md) — finding R1 motivates this spec; R2 and R4 concurrency fixes are coordinated here
- PRs introducing schema changes: #1401, #2560, #3042, #3464, #3633, #3693, #3790; inbox ContextKey in `787c31c52`
- Design principles: [.agent_instructions/design_principles.md](../../.agent_instructions/design_principles.md)
