---
id: 0057-box-schema-versioning-and-migrations
title: "Box Schema Versioning and Migrations"
status: Accepted
author:
  - "Brighter Team"
created: 2026-04-22
summary: "Replaces ADR 0053's single-version provisioning model with a versioned migration chain (7 outbox versions, up to 2 inbox versions per backend) and a three-path provisioner (fresh install / bootstrap of existing unversioned tables / normal incremental upgrade) for all relational backends. Introduces discriminator-gated version detection, per-backend idempotent ALTER patterns, extends `IAmABoxMigration` with `LogicalColumns` for detection, and adds `IAmABoxMigrationCatalog` to separate the historical V1 DDL from the current live-builder DDL."
tags:
  - "outbox"
  - "inbox"
  - "migration"
  - "provisioning"
---

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

`ProvisionAsync` branches into three paths based on `BoxTableState`. The branching lives inside each runner's existing `MigrateAsync(tableName, schemaName, tableState, ct)` signature — **no new interface methods are added** beyond the catalog already injected into the runner via constructor (per spec 0027 R1). The advisory lock wraps the entire path (not just the history insert) to close the concurrent-CREATE-TABLE race that bare `CREATE TABLE` would otherwise cause.

**Fresh-install DDL vs V1.UpScript — two distinct strings, sourced separately**: the fresh path needs the *current* live-builder DDL (so a fresh database matches the running Brighter version exactly); chain replay needs the *historical* V1 DDL (so a legacy table that was first-shipped at some earlier code revision sees the same starting shape it always saw). Spec 0027 R1 (Parts 1–4) splits these:

- `IAmABoxMigrationCatalog.FreshInstallDdl(configuration)` — returns the live builder's `CREATE TABLE` (`*Builder.GetDDL(table, binaryMessagePayload)`). This is what the fresh path executes.
- `migrations[0].UpScript` — returns the literal historical first-shipped DDL for that backend (the string extracted from the first commit that introduced the box, asymmetric per backend). This is **never re-executed** by the runner — bootstrap stamps V1 into history directly when detection says V≥1, and normal-path replay only applies V2..V_latest. V1.UpScript exists for archaeology, auditability, and as the canonical baseline that V2+ idempotency guards (`IF COL_LENGTH IS NULL`, `ADD COLUMN IF NOT EXISTS`, `information_schema` probe, `pragma_table_info`) reason against.

The asymmetry across backends is documented per-catalog: MSSQL/MySQL/SQLite outboxes are pre-Dispatched (true V1 baselines); PostgreSQL outbox and all four inboxes are "born past V1" (shipped with V2-equivalent columns from the first commit). See spec 0027 [README archaeology](../../specs/0027-box-schema-versioning-and-migrations/README.md) and each `*MigrationCatalog` class-level `<remarks>` for the per-backend literal DDL.

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
         Run            Detect V →    Read MAX(V)
         FreshInstallDdl  -1: throw   Run migs above MAX
         Insert history   0: throw    (each idempotent)
         at V_latest    ≥1: stamp     Release lock
         Release lock   V; run
                        V+1..V_latest
                        Release lock
```

**Fresh path** (`!TableExists`):
- Acquire serialization primitive (MSSQL `sp_getapplock` + `BeginTransaction`; Postgres `pg_try_advisory_lock` + `BEGIN`; MySQL `GET_LOCK`; SQLite `BEGIN IMMEDIATE TRANSACTION` with `SQLITE_BUSY` retry; Spanner uses degenerate runner — see §6)
- Re-check table existence under the lock (closes the detect→create race; if the table now exists, fall through to bootstrap)
- Execute `IAmABoxMigrationCatalog.FreshInstallDdl(configuration)` — the live builder's `CREATE TABLE` (per-backend, with payload-mode forking) — safe because we've verified under lock that the table doesn't exist. This is *distinct* from `migrations[0].UpScript`: the runner does not re-execute the historical V1 DDL on fresh install. See the "Fresh-install DDL vs V1.UpScript" note above
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
- Run migrations above MAX (each UpScript idempotent — see §5; the `migration.Version <= maxVersion` filter is the sole gate. With monotonically-ascending contiguous migrations enforced at runner construction, `maxVersion` was just read inside the lock-bearing transaction, and nothing greater than `maxVersion` can be in history, so a per-migration `IsMigrationAppliedAsync` check is redundant and not run)
- Release lock

**Why the fresh-install DDL does not need to be idempotent**: `FreshInstallDdl` is a plain `CREATE TABLE` (no `IF NOT EXISTS` wrapping) and the three-path logic guarantees it runs only when `!TableExists` *and* no concurrent instance has since created it (re-checked under lock). V2+ UpScripts MUST be idempotent per §5 because they may run on any intermediate schema state — including post-bootstrap and post-failed-partial-migration. V1.UpScript inherits the same correctness without being idempotent itself: the bootstrap path stamps detected V1 directly into history without re-executing V1.UpScript, so the historical DDL is never replayed against an existing table. On chain replay against a born-past-V1 backend (PostgreSQL outbox; MSSQL/MySQL/SQLite/PostgreSQL inboxes — see per-backend catalog `<remarks>`), V2's idempotency guard sees the column already present in V1 and skips the ADD COLUMN cleanly.

**Cross-box history-table race**: the advisory lock above scopes per-box (`BrighterMigration_{schema}.{table}`), but `__BrighterMigrationHistory` is shared across every box in the database. Concurrent provisioners targeting different boxes (e.g. outbox + inbox at host startup) hold *different* advisory locks and can both reach `EnsureHistoryTableAsync` simultaneously. The history-table DDL is intentionally outside any global lock; cross-backend resolution falls to the database's own duplicate-object handling:

| Backend | Strategy | Why no extra serialization |
|---|---|---|
| MSSQL | `IF NOT EXISTS` + `CREATE TABLE`; catch 2714 | Statement-terminating under default `XACT_ABORT OFF` — loser's tx stays usable |
| PostgreSQL | `CREATE TABLE IF NOT EXISTS`; catch 23505 / 42P07 / 42710 | pg_class check and pg_type insert are not atomic |
| MySQL | `CREATE TABLE IF NOT EXISTS` | `MDL_EXCLUSIVE` metadata lock makes the statement atomic |
| SQLite | `CREATE TABLE IF NOT EXISTS` | `BEGIN IMMEDIATE` writer lock already serialises concurrent runners |

A global "history-table lock" was considered and rejected: the race is rare (first-touch only), each backend's native semantics produce a clean recovery, and a global lock would serialize otherwise-independent per-box provisioning chains. Compensating catches live at `MsSqlBoxMigrationRunner.EnsureHistoryTableAsync` and `PostgreSqlBoxMigrationRunner.EnsureHistoryTableAsync`.

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

### 5b. Advisory-lock abstraction (per-backend)

The advisory-lock primitive that wraps the runner's chain (§3, §5a) is exposed to the runner through a per-backend `I*AdvisoryLock` interface, with a default implementation that ships in the same package. The abstraction is genuinely substitutable — adopted post-hoc to (a) close the diagnostic gaps surfaced in PR #4039 reviews ([review #46 M2 / #45 M2] for Postgres; symmetric gaps in MySQL and MSSQL once examined) and (b) make the lock collaborator testable without `InternalsVisibleTo`, `protected virtual` test seams, or global state swaps. Operators with custom connection-pool sharing or external lock-key derivation (Vault, KMS, etc.) can supply their own implementation.

**Interface shape per backend**:

| Backend | Interface | Acquire | Release | Diagnostic value |
|---------|-----------|---------|---------|------------------|
| PostgreSQL | `IPostgreSqlAdvisoryLock` | `Task AcquireAsync(NpgsqlConnection, string lockKey, TimeSpan timeout, CT)` — throws `TimeoutException` on deadline | `Task<bool> ReleaseAsync(NpgsqlConnection, string lockKey, CT)` — bool result of `pg_advisory_unlock` | `false` from release = calling session did not hold the lock; runner emits `ILogger.LogWarning` (does not throw — the chain has already committed) |
| MySQL | `IMySqlAdvisoryLock` | `Task AcquireAsync(MySqlConnection, string lockKey, TimeSpan timeout, CT)` — throws `TimeoutException` on `GET_LOCK` returning `0` (timeout) or `MySqlAdvisoryLockException` (new, Boy Scout Item R) on `GET_LOCK` returning `NULL` (server-side error: OOM, KILLed connection, memory-table fault). Sub-second `timeout` floored at 1 second so `GET_LOCK(name, 0)`'s non-blocking semantic cannot accidentally apply | `Task<bool?> ReleaseAsync(MySqlConnection, string lockKey, CT)` — `1` (true) released by us; `0` (false) held by another session; `NULL` did not exist | Per-return-code distinction is the diagnostic on acquire (`0` `TimeoutException`, `NULL` `MySqlAdvisoryLockException`); per-return-code logging on release (all non-`true` outcomes are anomalies — runner logs warning naming both result code and lock key). NULL is unreachable on current MySQL 8 in practice (NULL/invalid lock names raise a typed connector exception at parse time before `GET_LOCK` runs); kept defensively for parity with MSSQL's per-return-code mapping. Lock-name derivation lives in the existing `MySqlMigrationLockName.For(schema, tableName)` static helper (Boy Scout Item A; schema parameter added in Item O — the composite is folded through the same 64-char-safe transformation so distinct (schema, table) pairs cannot collide) — the abstraction owns only the SQL |
| MSSQL | `IMsSqlAdvisoryLock` | `Task AcquireAsync(SqlConnection, SqlTransaction, string lockResource, TimeSpan timeout, CT)` — throws specific exception types per `sp_getapplock` return code (see below) | (none — lock is `@LockOwner = 'Transaction'`, released by the wrapping transaction's commit/rollback) | Return-code distinction is the diagnostic: `-1` `TimeoutException`, `-2` `OperationCanceledException`, `-3` `MigrationLockDeadlockException` (new), `-999` `ArgumentException`. Today's runner collapses all `< 0` into `TimeoutException`, losing the deadlock and parameter-validation signals |

**Why each shape differs**:

- Postgres and MySQL use **session-scoped** locks (`pg_try_advisory_lock` / `GET_LOCK`) released by an explicit unlock call. The runner holds the lock outside the transaction (so the lock survives the transaction commit) and releases in `finally`. The abstraction therefore exposes both `AcquireAsync` and `ReleaseAsync`. The release call has a meaningful return value worth logging.
- MSSQL's `sp_getapplock` is invoked with `@LockOwner = 'Transaction'` so the lock is **transaction-scoped** — `transaction.Commit()` / `Rollback()` releases it implicitly, and there is no `sp_releaseapplock` call to make. The abstraction is acquire-only. The diagnostic value sits in distinguishing the `sp_getapplock` return codes, which today are all collapsed.
- SQLite is **exempt** from the abstraction — it has no advisory lock; serialization is provided by `BEGIN IMMEDIATE`'s writer slot (per §5 table). The runner's existing `BeginImmediateWithRetryAsync` handles `SQLITE_BUSY` retry directly; introducing an `ISqliteAdvisoryLock` would invent a primitive that does not exist in the database.
- Spanner is **exempt** — degenerate runner per §6, no concurrency primitive of its own.

**Constructor injection (additive)**: each runner ctor gains two optional parameters — `I*AdvisoryLock? advisoryLock = null` (default: `new *AdvisoryLock()`) and `ILogger? logger = null` (default: `ApplicationLogging.CreateLogger<*BoxMigrationRunner>()`). Both are non-breaking additions; existing call sites continue to compile and run with default behaviour. The DI extensions (`UseBoxProvisioning`) do not register the abstractions — operators wanting custom impls construct the runner explicitly. This matches the existing wiring approach for `IAmABoxMigrationRunner` itself.

**What does not absorb into the abstraction**:

- The lock-key / lock-resource derivation (e.g. MySQL's 64-char `MySqlMigrationLockName.For` from Item A, schema-extended in Item O) stays as a separate helper. Different backends derive keys differently (Postgres: `BrighterMigration_<schema>.<table>` then `hashtext(...)`; MySQL: 64-char-safe transformation over the `<schema>.<table>` composite; MSSQL: `BrighterMigration_<schema>.<table>` raw resource name within the 255-char limit); folding them into the lock interface would force a common naming abstraction that has no operational benefit.
- `BRIGHTER_LOCK_NAMESPACE = 74726` (Postgres-only constant for the two-arg `pg_try_advisory_lock(int4, int4)` form) moves into the default `PostgreSqlAdvisoryLock` impl.
- Item E's `ValidateLockTimeout` overflow guard (rejects timeouts whose `TotalMilliseconds > int.MaxValue`, ~24.85 days) absorbs into `MsSqlAdvisoryLock`'s acquire path — that is the only place the cast happens.
- Item R's MySQL sub-second-timeout floor (`Math.Max(1, Math.Ceiling(timeout.TotalSeconds))`) absorbs into `MySqlAdvisoryLock`'s acquire path — the floor exists because of the `GET_LOCK` whole-second-only protocol, not because of any caller-shaped contract; runners and operators continue to express timeouts as `TimeSpan` without rounding.
- Item S's `TimeProvider` substitution absorbs into `PostgreSqlAdvisoryLock`'s acquire path — the deadline math (`GetTimestamp` / `GetElapsedTime`) is local to the retry loop. The default `TimeProvider.System` reads `Stopwatch.GetTimestamp` (monotonic), so a wall-clock jump (NTP correction during a long lock wait, leap-second smear, container clock skew on VM resume) cannot collapse or extend the budget; tests inject `FakeTimeProvider` to drive the deadline check deterministically. Runners and operators continue to express timeouts as `TimeSpan`. Mirror substitutions for the other backends are not pursued in this PR — MSSQL's `sp_getapplock` performs the wait server-side (no client-side deadline math), MySQL's `GET_LOCK` likewise (the only client-side math is Item R's whole-second cast, not a deadline), and SQLite's `BEGIN IMMEDIATE` retry already uses the cancellation token rather than a deadline.

**Cross-backend symmetry not pursued**: SQLite has no native advisory lock; we do not invent one. The MSSQL acquire-only shape vs the PG/MySQL acquire-and-release shape is dictated by the underlying primitive's lifetime model — not a code-style choice we can normalise away.

**Design alternatives rejected** (during 2026-05-05 design review of Boy Scout Item D):

- *Public static helper* (mirroring `MySqlMigrationLockName.For` from Item A) — handles only the diagnostic formatting; does not solve the underlying "lock collaborator is not substitutable" testability gap, and creates a public surface whose only purpose is testability.
- *`protected virtual ExecuteUnlockAsync` test seam* — adds a public-API extension point whose primary motivation is testing, which is exactly the implementation-detail coupling we want to avoid.
- *`InternalsVisibleTo` to the test assembly* — repository policy prohibits coupling tests to internal APIs.

The abstraction approach is heavier than any of these but produces no test-only artifacts and unlocks a long-standing gap (the lock-acquisition retry/backoff behaviour becomes unit-testable for the first time — fakes can simulate N transient failures before success, exercising the deadline math).

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
│  holds: runner: IAmABoxMigrationRunner                        │
│         (runner is constructed with an injected               │
│          IAmABoxMigrationCatalog per spec 0027 R1)            │
│                                                                │
│  ProvisionAsync:                                              │
│   1. DetectTableStateAsync — calls *BoxDetectionHelpers       │
│      .DetectCurrentVersionAsync (pre-lock pass, populates     │
│      BoxTableState.CurrentVersion)                            │
│   2. runner.MigrateAsync(table, schema, state, ct)            │
│      — runner pulls migrations and FreshInstallDdl from       │
│        its injected catalog; branches on state.TableExists    │
│        / HistoryExists internally                             │
└───────────────────┬───────────────────────────────────────────┘
                    │
                    ▼
┌───────────────────────────────────────────────────────────────┐
│              IAmABoxMigrationRunner.MigrateAsync              │
│  (state-only signature post-R1; chain + fresh-install DDL     │
│   sourced from the injected IAmABoxMigrationCatalog)          │
│                                                                │
│  let migrations = catalog.All(configuration)                  │
│  let freshInstallDdl = catalog.FreshInstallDdl(configuration) │
│                                                                │
│  Acquire advisory lock (backend-specific);                    │
│  TOCTOU re-check table / history existence under the lock;    │
│                                                                │
│  if !tableExistsNow:                                          │
│      execute freshInstallDdl (live builder CREATE)            │
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
- **`IAmABoxMigrationRunner`** (existing, **reshaped post-R1**): `MigrateAsync` drops the `IReadOnlyList<IAmABoxMigration>` parameter; the runner is constructed with an injected `IAmABoxMigrationCatalog` and retrieves the migration chain *and* `FreshInstallDdl` from it. Implementations gain three-path branching inside `MigrateAsync`.
- **`IAmABoxMigrationCatalog`** (new in R1): per-backend interface exposing `All(configuration)` (the V1..V_latest chain) and `FreshInstallDdl(configuration)` (the live builder DDL used by the fresh path). Splits the historical baseline DDL (which lives on `migrations[0].UpScript`) from the current-builder DDL (which lives on `FreshInstallDdl`), addressing the spec 0027 R1 readability concern that V1.UpScript was previously asked to play both roles.
- **`*OutboxMigrationCatalog` / `*InboxMigrationCatalog`** (per-backend, formerly `*Migrations`, **expanded**): implement `IAmABoxMigrationCatalog` — return `V1..V_latest` lists via `All(config)` and the live builder DDL via `FreshInstallDdl(config)`. Each catalog also documents the per-backend V1 archaeology in its class-level `<remarks>`.
- **`*OutboxProvisioner` / `*InboxProvisioner`** (existing, **simplified**): `DetectTableStateAsync` now delegates to `*BoxDetectionHelpers.DetectCurrentVersionAsync` (added in Phase 1.5/2.5/3.5/4.5); the previously-private detection methods and `V1Columns` static sets are deleted from the provisioners. Detection logic no longer lives in the provisioner.
- **`*BoxDetectionHelpers`** (existing, **extended**): per-backend static classes (`MsSqlBoxDetectionHelpers`, `PostgreSqlBoxDetectionHelpers`, `MySqlBoxDetectionHelpers`, `SqliteBoxDetectionHelpers`, `SpannerBoxDetectionHelpers`) currently host only schema-introspection primitives (`DoesTableExistAsync`, `DoesHistoryExistAsync`, `GetMaxVersionAsync`, `GetTableColumnsAsync`). Spec 0027 adds a new `DetectCurrentVersionAsync(connection, txn, tableName, schemaName, migrations, discriminatorColumn, ct)` static method to each, walking the migration list top-down with a discriminator gate per §2. This is the **single source of detection truth** — invoked by both the provisioner (pre-lock) and the runner (post-lock TOCTOU re-check).
- **`SpannerBoxMigrationRunner`** (existing, **reworked**): fresh-only strategy; no migration list; adds `IsMigrationAppliedAsync` gate on history INSERT (addresses spec 0023 R4).

### Technology choices

- **No new NuGet dependencies**: the migration chain uses the same `Microsoft.Data.SqlClient`, `Npgsql`, `MySqlConnector`, `Microsoft.Data.Sqlite`, and `Google.Cloud.Spanner.Data` packages already referenced by spec 0023's provisioners.
- **No schema change to `__BrighterMigrationHistory`**: the existing `(SchemaName, BoxTableName, MigrationVersion)` PK and `Description, AppliedAt` columns are sufficient.
- **Migration list is code, not data**: `MsSqlOutboxMigrations.All(config)` is a compile-time-ordered list; versions are not discovered by reflection or external config.
- **Drift-detection test (new, part of this spec)**: prevents the "forgot to add V8" class of bug where a column is added to a builder without a corresponding new migration. Concrete design:
  - A test-only helper `DdlColumnExtractor.GetExpectedColumns(string ddl, QuoteStyle quoteStyle)` parses any backend's DDL string and returns the column-name set. The helper lives in a dedicated test project (`tests/Paramore.Brighter.BoxProvisioning.Tests/`, registered in `Brighter.slnx` between `Base.Test` and `Core.Tests`) — kept out of the production builder and out of `Paramore.Brighter.Core.Tests` so Core stays free of backend-specific DDL grammar knowledge. A simple regex over the `CREATE TABLE` body is sufficient because the builders are developer-authored text with known quoting per backend.
  - Per-backend test (one each: `*OutboxMigrationsDriftTests`, `*InboxMigrationsDriftTests`) lives in the per-backend integration-test project (e.g. `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/`, which adds a `<ProjectReference>` to `Paramore.Brighter.BoxProvisioning.Tests` for the helper). This per-backend test asserts:
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
