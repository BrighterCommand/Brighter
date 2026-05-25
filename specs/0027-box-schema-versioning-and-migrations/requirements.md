# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details are documented in an Architecture Decision Record (ADR) — see `docs/adr/0057-box-schema-versioning-and-migrations.md` (to be created in the design phase). The supporting archaeology (git history, commits, column tables) is captured in this spec's `README.md`.

**Linked Issue**: None — originates from spec 0023 code review finding R1.
**Supersedes**: Bootstrap behaviour defined in spec 0023 / ADR 0053 §7.

## Problem Statement

**As a Brighter operator** upgrading an existing installation from any historical release to the current version, **I would like** the box provisioning system to correctly detect my outbox/inbox table's schema state and evolve it to the latest version **without data loss or manual intervention**, **so that** I can adopt new Brighter releases without custom ALTER-TABLE scripts and without risk of the host failing to start on a pre-existing table.

The current bootstrap behaviour (spec 0023) treats every pre-existing table as if it were at "version 1" where V1 is defined as the *current full DDL*. For any installation that predates the latest schema (i.e. lacks `DataRef`/`SpecVersion`, or `WorkflowId`/`JobId`, or the CloudEvents columns, or any earlier additions), detection returns "unknown" and the provisioner attempts a `CREATE TABLE` on a table that already exists — which throws on MSSQL and breaks host startup. This directly contradicts ADR 0053 §7 ("safe fallback is version 1") and NFR-1 ("backward compatibility") from spec 0023.

## Proposed Solution

Replace the single-version detection model with a **version-per-schema-change migration chain** derived from the actual git history of the builder files (see README.md for the archaeology). Each numbered version corresponds to a real column-set change that shipped in a specific Brighter release. The provisioner:

- **On fresh install** (table absent): runs current builder DDL once, stamps latest version as applied. No migration chain overhead.
- **On bootstrap** (table exists, no migration history): introspects actual column names, identifies the highest matching version `V`, stamps `V` as applied, then runs migrations `V+1..VN` as idempotent ALTERs.
- **On normal migration** (table + history present): reads `MAX(MigrationVersion)` from history, runs any newer migrations.

The version numbering is uniform across the 4 relational backends (MSSQL, PostgreSQL, MySQL, SQLite) — a logical `V4` means "table has PartitionKey column" regardless of when each backend's commit landed. Spanner is degenerate: fresh-install only, no migration chain (no known production users).

## Requirements

### Functional Requirements

**Detection**
- **FR-1**: `DetectCurrentVersionAsync` walks V_latest..V1 and returns the highest version `V` whose logical column set is a subset of the actual columns present in the table. Detection is by column *name* only (types and nullability are not compared).
- **FR-2**: Logical column sets exclude backend-specific housekeeping columns (MSSQL/Postgres `Id`, MySQL `Created`/`CreatedID`) — those live inside each backend's V1 DDL and are not part of the version-comparison set.

**Provisioning paths**
- **FR-3**: Fresh install (`TableExists=false`) runs the current builder `GetDDL()` directly and inserts **one** synthetic history row with `MigrationVersion = V_latest`. No `V1..V_latest` chain execution.
- **FR-4**: Bootstrap (`TableExists=true, HistoryExists=false`) inserts one synthetic history row at the detected `V`, then runs real migrations `V+1..V_latest` under the advisory lock.
- **FR-5**: Normal migration (`TableExists=true, HistoryExists=true`) reads `MAX(MigrationVersion)` and runs newer migrations only.
- **FR-6**: All three paths share the existing `IAmABoxMigrationRunner` interface from spec 0023 — API-compatible extension.

**Migration implementation**
- **FR-7**: Outbox supports **7 logical versions** across MSSQL, Postgres, MySQL, SQLite. Per-version logical column additions:
  - V1 = baseline (`MessageId, Topic, MessageType, Timestamp, HeaderBag, Body`)
  - V2 = + `Dispatched`
  - V3 = + `CorrelationId, ReplyTo, ContentType`
  - V4 = + `PartitionKey` (and NTEXT→NVARCHAR(MAX) widening where applicable)
  - V5 = + `Source, Type, DataSchema, Subject, TraceParent, TraceState, Baggage`
  - V6 = + `WorkflowId, JobId`
  - V7 = + `DataRef, SpecVersion`
- **FR-8**: Inbox supports **2 logical versions** across MSSQL, Postgres, MySQL, SQLite:
  - V1 = baseline (`CommandId, CommandType, CommandBody, Timestamp`)
  - V2 = + `ContextKey` (Postgres composite-PK change is part of the V2 migration for Postgres only)
- **FR-9**: Each migration is idempotent — re-running must be a no-op. Backend-specific idempotency mechanisms:
  - MSSQL: `IF COL_LENGTH('{table}', 'ColName') IS NULL ALTER TABLE ... ADD ColName ...`
  - PostgreSQL: `ALTER TABLE ... ADD COLUMN IF NOT EXISTS ColName ...`
  - MySQL: `information_schema.columns` existence check (portable; avoids the 8.0.29+ `IF NOT EXISTS` restriction)
  - SQLite: `pragma_table_info(...)` existence check
- **FR-10**: Spanner provisioner is fresh-install-only: on `TableExists=false` it runs current DDL and stamps `MigrationVersion = V_latest`. On `TableExists=true` without history, it stamps `V_latest` as applied without attempting detection (no production users expected to exist in an intermediate state).

**History table**
- **FR-11**: Use the existing `__BrighterMigrationHistory` table (MSSQL/Postgres/MySQL/SQLite) and `BrighterMigrationHistory` (Spanner) from spec 0023 — no schema change.
- **FR-12**: History rows for synthetic-bootstrap and fresh-install paths are distinguishable from real-migration rows by the `Description` field (e.g. `"bootstrap: legacy table detected at V3"` vs `"V4: add PartitionKey"`).

### Non-functional Requirements

- **NFR-1 (backward compatibility)**: Every previously released Brighter outbox/inbox schema — V1 onward for both box types — is recognised by the detector and upgraded to the latest version without data loss.
- **NFR-2 (fresh-install performance)**: Fresh install executes a single `CREATE TABLE` + single history insert. No overhead from running `V1..V_latest` as an ALTER chain on a clean database.
- **NFR-3 (migration lock timeout)**: A full bootstrap from V1 → V_latest on a single outbox table completes within the default `MigrationLockTimeout` (30s). On typical RDBMS instances the expected time is well under 1s since each migration is a single DDL.
- **NFR-4 (data preservation)**: All existing row data in an outbox/inbox table survives any migration applied by this system. Migrations only add columns (nullable or defaulted) or widen types — never drop or reshape existing data.
- **NFR-5 (audit traceability)**: Each migration object references the commit SHA and PR number (where applicable) that introduced the schema change it mirrors. Auditors can cross-reference `V4` in the code against `#2560` or `#3464` (Postgres) in git history.
- **NFR-6 (concurrency)**: Concurrent provisioning by multiple instances of the same host must not duplicate history rows or corrupt state. The existing advisory-lock pattern from spec 0023 still applies; bootstrap path inherits the same protections, plus idempotency-tolerant INSERTs per backend (the concern raised by spec 0023 R2/R4 — addressed in the design).

### Constraints and Assumptions

**Constraints**
- **C-1**: Must preserve the `IAmABoxMigrationRunner` and `IAmABoxProvisioner` interfaces from spec 0023.
- **C-2**: Must preserve the `__BrighterMigrationHistory` table schema from spec 0023 (`SchemaName, BoxTableName, MigrationVersion, Description, AppliedAt`). No columns added or renamed.
- **C-3**: Binary and JSON payload variants are fresh-install choices only. A text-mode existing table cannot be migrated to binary or JSON (the column *type* is incompatible). Payload-mode mismatch is caught by the existing `*PayloadModeValidator` classes — not by migration.
- **C-4**: Type-only changes (MessageId/CorrelationId UNIQUEIDENTIFIER→NVARCHAR in outbox #3042; CommandBody widening in inbox #2560; CommandId type change in inbox #3042) are *not* modeled as separate migration versions (the "folded V5" decision from Phase 0). Detection by column name alone treats these as version-equivalent.
- **C-5**: Must target `netstandard2.0` alongside `net8.0/net9.0/net10.0` — same as spec 0023 (no default-interface-member use; no C# 11+ features unavailable on netstandard).

**Assumptions**
- **A-1**: Installations with pre-#3042 tables (MessageId/CorrelationId as UNIQUEIDENTIFIER) are rare. Documented edge case: application code using non-GUID message IDs may throw `InvalidCastException` on INSERT against such a table. No migration is provided; users wanting type normalisation must ALTER manually.
- **A-2**: There are **no production Spanner users** (confirmed in spec 0023 B1 dismissal). Spanner's degenerate "fresh-install-only" provisioner is safe. If Spanner adoption arises, a migration chain can be added in a later spec.
- **A-3**: Column-name comparison is sufficient for detection — no backend reuses outbox/inbox column names for unrelated tables in the bootstrap path. The 22 columns at V7 outbox are collectively specific enough that false-positive detection is not a practical concern.
- **A-4**: Existing `__BrighterMigrationHistory` rows (if any — i.e. installations that adopted spec 0023 before this spec) are at `MigrationVersion = 1`. This spec's runner must treat those as "V1 applied" and continue with V2+ migrations. The semantic shift (spec 0023 V1 was "current DDL"; spec 0027 V1 is "baseline 6-column DDL") means these installations will appear to "skip" V2..V7 migrations, but in practice their tables already have the V7 columns — detection will confirm and stamp accordingly. **This transition case must be tested explicitly.**

### Out of Scope

- **Data migrations**: column renames, drops, value backfills, or reshaping of existing row data. Only additive schema changes (ADD COLUMN, ALTER COLUMN to wider type) are modelled.
- **Type-change migrations**: e.g. UNIQUEIDENTIFIER→NVARCHAR conversions. Folded per Phase 0 decision; documented as edge case.
- **Rollback migrations**: no "V7 → V6" path. Migrations are forward-only.
- **Spanner migration chain**: fresh-install-only provisioner ships; migration chain deferred to a future spec if needed.
- **Cross-payload-mode conversion**: a text-mode table cannot be transformed into binary or JSON via migration. Users must drop and recreate.
- **NoSQL / DynamoDB / MongoDB / CosmosDB provisioning**: spec 0023 (and therefore this spec) is relational-only.
- **Multi-tenant schema support**: each table lives in one schema; cross-schema migrations are not modelled. Backend-specific `SchemaName` support from spec 0023 is preserved but not extended.
- **Spec 0023 code-review findings R2, R4, R5, R6**: those are tracked separately and fixed in spec 0023's close-out. This spec assumes R2/R4 concurrency fixes are landed or coordinated.

## Acceptance Criteria

**Functional correctness**
- **AC-1 (outbox upgrade)**: A pre-V7 outbox table at any prior version V ∈ {1..6} boots without throwing and ends at V7 after provisioning. Existing row data is preserved; new columns are NULL on existing rows.
- **AC-2 (inbox upgrade)**: A pre-V2 inbox table (V1 — no `ContextKey`) boots without throwing and ends at V2 after provisioning.
- **AC-3 (fresh install)**: Absent-table provisioning produces a V7 outbox / V2 inbox table and a single synthetic history row marking latest version as applied. No ALTER statements executed.
- **AC-4 (no-op re-run)**: Running the provisioner twice in sequence on the same table produces exactly one set of history rows — the second run is a no-op.
- **AC-5 (concurrent bootstrap)**: Two provisioner instances racing on a bootstrap case (legacy table, no history) produce exactly one synthetic history row and one set of applied migrations. No PK violations.
- **AC-6 (spec 0023 transition)**: An outbox table with existing `__BrighterMigrationHistory` row at V1 (spec 0023 era, where V1 meant "current DDL") transitions cleanly — detection confirms the table has V7 columns, history advances without running redundant ALTERs.
- **AC-7 (Spanner fresh-only)**: Spanner provisioner on absent table runs current DDL + stamps V7 (outbox) / V2 (inbox). Spanner provisioner on existing table without history stamps latest version without attempting detection.
- **AC-8 (Spanner with history)**: Spanner provisioner on existing table with history is a no-op — `MAX(MigrationVersion)` already equals latest.

**Backend coverage**
- **AC-9**: All 4 relational backends implement the 7-version outbox chain: MSSQL, PostgreSQL, MySQL, SQLite.
- **AC-10**: All 4 relational backends implement the 2-version inbox chain.
- **AC-11**: Backend-specific housekeeping preserved: MSSQL/Postgres `Id` BIGINT/BIGSERIAL PK, MySQL `Created`/`CreatedID`, SQLite `CommandId` PK with `COLLATE NOCASE` on outbox, Postgres composite `(CommandId, ContextKey)` PK on inbox.

**Documentation / traceability**
- **AC-12**: Each migration object in code references the commit SHA and/or PR number from the archaeology (README.md tables).
- **AC-13**: ADR 0057 (to be created in design phase) captures the versioning model and references this spec's archaeology.
- **AC-14**: `.agent_instructions/box_provisioning.md` updated with the rule: "every column addition to a builder MUST ship with a new V(N+1) migration + idempotency check + test."

**Testing**
- **AC-15**: Per-backend fresh-install test asserts correct V7 / V2 column set and history row.
- **AC-16**: Per-backend bootstrap tests at V1, V3, V5 (sampled), V7 for outbox; V1, V2 for inbox. Assert correct upgrade to latest.
- **AC-17**: Per-backend idempotency test — run provisioner twice.
- **AC-18**: Per-backend concurrent-bootstrap test — race two instances.
- **AC-19**: Spec-0023-era transition test (AC-6 above).

## Additional Context

### Evidence base

The archaeology and version tables are captured in `README.md` for this spec — it tables every outbox builder commit from 2015 to present and maps them to logical versions V1..V7, plus the inbox's V1..V2.

### Relationship to spec 0023

Spec 0023 ships the `BoxProvisioning` infrastructure: interfaces, hosted service, history table, `IAmABoxMigrationRunner`, per-backend provisioners, and a **single** V1 migration per box. Spec 0027 extends the migration list to V1..V7 (outbox) / V1..V2 (inbox) with the runner modifications needed to handle the chain.

The two specs share the same branch (`database_migration`) because they're closely coupled — spec 0023's R1 finding is the reason spec 0027 exists. Spec 0023 lands first (with R1 explicitly deferred to 0027); spec 0027 lands second on the same branch.

### Impact on existing spec 0023 installations

If any downstream consumer has merged spec 0023 to production before spec 0027 lands, they will have `__BrighterMigrationHistory` rows marked `MigrationVersion = 1`. Those rows are still valid under spec 0027's numbering (V1 is the baseline, which every post-#3790 table has), but they won't trigger V2..V7 migrations because the detected version already matches the column set. AC-6 covers this transition; a release note entry is required.
