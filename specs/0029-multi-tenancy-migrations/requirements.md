# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Linked Issue**: [#4144 — Box provisioning: support per-tenant isolation of `__BrighterMigrationHistory`](https://github.com/BrighterCommand/Brighter/issues/4144)

## Problem Statement

As an **operator running Brighter in a multi-tenant deployment with a separate database schema per tenant**, I would like **the box-provisioning migration-history table to be isolatable to the tenant's configured schema**, so that **each tenant's migration history sits inside that tenant's isolation, backup-restore, and retention boundary rather than in a single shared table in the backend's default schema**.

Today the migration-history table — `__BrighterMigrationHistory` on the relational backends, `BrighterMigrationHistory` on Spanner — is always created in the backend's **default** schema regardless of the configured `IAmARelationalDatabaseConfiguration.SchemaName`:

- MSSQL → `[dbo].[__BrighterMigrationHistory]` (`MsSqlBoxMigrationRunner.cs:140`)
- PostgreSQL → `"public"."__BrighterMigrationHistory"` (`PostgreSqlBoxMigrationRunner.cs:130`)
- MySQL → unqualified, lives in the connection-bound `DATABASE()` (`MySqlBoxMigrationRunner.cs:137`)
- SQLite / Spanner → no schema concept, so out of scope for placement (see below)

Multi-tenant rows are not *logically* ambiguous — the relational backends carry a composite primary key `(SchemaName, BoxTableName, MigrationVersion)` (`MsSqlBoxMigrationRunner.cs:146`, `PostgreSqlBoxMigrationRunner.cs:136`, `MySqlBoxMigrationRunner.cs:143`), detection queries already filter on the `SchemaName` **column value** (`MsSqlBoxDetectionHelper.cs:94-96`), and history-row inserts already stamp the resolved schema into that column (`MySqlBoxMigrationRunner.cs:245-248`) — but **physically one history table is shared across all tenants** in a deployment. There is no way to keep one tenant's migration history out of the schema shared with other tenants.

Because rows are already `SchemaName`-stamped and keyed, the only thing that changes about the *data* when history moves into a per-tenant schema is the **physical table location** — the logical partitioning already exists. (Detection is a separate matter: today the detection/insert SQL hardcodes the default-schema table — e.g. `SELECT COUNT(1) FROM [dbo].[__BrighterMigrationHistory]` at `MsSqlBoxDetectionHelper.cs:93` — so per-schema placement also requires the detection query to target the new location and the existing rows to reach it. See FR5.) This still materially narrows what any migration of existing deployments must cover.

The decision to pin history to the default schema is currently deliberate and documented in code comments (e.g. `MsSqlBoxMigrationRunner.cs:48-49`, `PostgreSqlBoxMigrationRunner.cs:48-52`). This feature revisits that decision for operators who need physical per-tenant isolation.

## Proposed Solution

Expose an **explicit, opt-in** choice of where the migration-history table physically lives, so that in a per-schema-per-tenant deployment the history table can be co-located with the tenant's box tables in the configured `SchemaName`. The chosen direction is a migration-history *scope* with two values:

- **`Global`** (the default) — history resolves to today's location (`dbo` / `public` / connection `DATABASE()`) for **every** operator, *including* those who already set a non-null `SchemaName`. Upgrading Brighter without changing configuration moves nothing and re-runs nothing.
- **`PerSchema`** — on backends with a distinct schema concept (**MSSQL and PostgreSQL**), history is created in the configured `SchemaName`, co-located with that tenant's box tables. `PerSchema` requires a non-null `SchemaName`. **MySQL is excluded** (see below and Out of Scope): a MySQL "schema" *is* a database, so co-locating history would mean writing to a different database than the connection binds — out of scope here; MySQL keeps history in the connection `DATABASE()` regardless of scope.

Per-schema placement is engaged **only** by explicitly selecting it; it is never inferred from the presence of a non-null `SchemaName`. This keeps the upgrade-safe default explicit and avoids silently changing behaviour for operators who set `SchemaName` today. On the placement backends (MSSQL and PostgreSQL), selecting `PerSchema` with a null `SchemaName` is a misconfiguration — there is no schema to target — and is rejected at provisioning entry with a `ConfigurationException` rather than silently falling back. On MySQL (and the no-schema backends) the scope performs no placement, so `PerSchema` is simply a no-op there regardless of `SchemaName`.

When `PerSchema` is selected, the migration runner and detection helper must be schema-aware **end to end** — creation DDL, existence/version detection queries, and history-row read/write must all target the chosen schema consistently so that detection and provisioning agree on where history lives.

Existing deployments already have a populated history table in the backend default schema, and detection currently reads from that hardcoded default-schema table. So a flip from `Global` to `PerSchema` requires both the detection query to target the new per-schema table and the existing `SchemaName`-keyed rows to be reachable there — the solution must ensure no already-applied migration is re-run as a result.

> The behavioural contract above (explicit opt-in; `Global` default unchanged for all operators) is fixed at the requirements level. The exact API surface — the chosen name `BoxProvisioningOptions.MigrationHistoryScope`, the enum value labels `Global` / `PerSchema`, and the type's home — is the **chosen direction** but is finalised in the ADR.

## Requirements

### Functional Requirements

- **FR1 — Explicit opt-in scope.** Operators must be able to select migration-history placement through an explicit scope option with two values: `Global` (default) and `PerSchema`. Per-schema placement is engaged **only** when `PerSchema` is selected; it must **never** be inferred from the presence of a non-null `SchemaName`. The chosen direction is `BoxProvisioningOptions.MigrationHistoryScope` (name/labels finalised in the ADR).
- **FR1a — PerSchema requires a schema (on placement backends).** On the backends where `PerSchema` performs placement (**MSSQL and PostgreSQL**), selecting `PerSchema` with a null `SchemaName` must be rejected at provisioning entry with a `ConfigurationException` (there is no schema to target). It must **not** silently fall back to `Global`. On backends where the scope has no placement effect (MySQL, SQLite, Spanner), `PerSchema` is a no-op and this guard does not apply (see FR4 / Out of Scope).
- **FR2 — Schema-aware history placement.** When `PerSchema` is selected with a non-null `SchemaName`, the migration-history table must be created in the configured `SchemaName` rather than the backend default schema, on the backends that have a distinct schema concept: **MSSQL and PostgreSQL**. (MySQL is excluded — see Out of Scope.)
- **FR3 — End-to-end consistency.** When the history table is placed in a configured schema, every operation that touches it must target that same schema: create-table DDL, table/version detection queries, and migration-history row inserts/reads. Detection and provisioning must never disagree about which schema holds history.
- **FR4 — Backward-compatible default for all operators.** With the default scope `Global`, history resolves to today's location (`dbo` / `public` / connection `DATABASE()`) for **every** operator, *including those who already set a non-null `SchemaName`*. Upgrading Brighter without explicitly selecting `PerSchema` does not move the history table or re-run migrations, regardless of whether the operator's box tables live in a non-default schema.
- **FR5 — Existing-row visibility on opt-in.** When an operator flips from `Global` to `PerSchema` on a deployment that already has a populated default-schema history table, the existing `SchemaName`-keyed rows must remain visible to detection in the per-schema location, so that no migration whose `(SchemaName, BoxTableName, MigrationVersion)` key already exists is re-applied. (The rows are already schema-stamped, but detection currently reads a hardcoded default-schema table; closing this requires both the detection query to target the per-schema table and the existing rows to reach it. Whether the rows are physically moved, copied, or read across both tables is a design decision — the required outcome is no re-run.)
- **FR6 — Multi-tenant correctness preserved.** Whatever placement is chosen, the existing guarantee that an already-applied migration is not re-applied must hold per tenant. The primary-key / uniqueness semantics that make multi-tenant rows unambiguous today must remain correct under per-schema placement.

### Non-functional Requirements

- **NF1 — No behavioural change unless `PerSchema` is selected.** Operators who take a new Brighter version without explicitly selecting `PerSchema` see identical provisioning behaviour and identical history-table location — this holds even if they already set a non-null `SchemaName`.
- **NF2 — Idempotency.** Provisioning remains idempotent under the new placement: running provisioning repeatedly is safe and does not duplicate history rows or re-apply migrations.
- **NF3 — Identifier safety.** Schema and table identifiers used in the new placement must continue to pass the existing identifier-safety validation (`Identifiers.AssertSafe()` / backend identifier quoting such as `PgIdentifier`), with no new SQL-injection surface.
- **NF4 — Consistency across in-scope backends.** The `PerSchema` capability behaves consistently across the in-scope backends (MSSQL and PostgreSQL), so operators get one mental model; the documented per-backend default-schema differences (`dbo` vs `public`) are the only variation. MySQL and the no-schema backends retain today's behaviour under any scope.
- **NF5 — Observability.** On each provisioning run the runner emits a log entry recording the resolved history-table schema (and the active scope, `Global` or `PerSchema`), at a level consistent with the existing migration-runner logging contract, so operators can confirm where history was written. (Exact level and message text finalised in the ADR; the requirement is that the resolved schema is observable in logs.)

### Constraints and Assumptions

- **C1** — `SchemaName` lives on `IAmARelationalDatabaseConfiguration.SchemaName` (`src/Paramore.Brighter/IAmARelationalDatabaseConfiguration.cs:61`) as a nullable string: `null` = backend default, non-null = operator-configured schema.
- **C2** — `BoxProvisioningOptions` today exposes only `MigrationLockTimeout`; it has no placement/scope option. The chosen `MigrationHistoryScope` option extends this type, defaulting to `Global` so existing construction sites compile and behave unchanged.
- **C3** — There is **no** existing mechanism to move history rows between tables, nor versioning of the history-table schema itself — this work would introduce the first such concern if a data-move is in scope.
- **C4** — This builds directly on the fresh-install schema-routing fixes already landed in PR #4039 (commits `df0e5a847`, `2c719254e`, `c9ee00311` — F1a/F1b/F1c), which route fresh-install box-table DDL to the configured `SchemaName`. History placement is the remaining gap (reviewer item F2-5).
- **A1** — Operators who want per-tenant isolation already run one schema per tenant and set `SchemaName` accordingly; this feature does not introduce the per-schema deployment model, only history-table placement within it.

### Out of Scope

- **MySQL `PerSchema` placement** — a MySQL "schema" *is* a database, so co-locating history in the configured `SchemaName` would mean writing to a different database than the connection binds (`MySqlOutboxMigrationCatalog.cs:232` treats a configured schema as a separate database), with distinct connection/permission semantics. Out of scope for this feature: under any scope, MySQL keeps history in the connection `DATABASE()`. The scope option has no placement effect on MySQL.
- **SQLite** — has no schema concept; the `SchemaName` parameter is already accepted-and-ignored (`SqliteBoxDetectionHelper.cs:42-44`). No placement change applies; scope has no effect.
- **Spanner** — has no schema concept (`SpannerBoxDetectionHelper.cs:42-45`); its history table `BrighterMigrationHistory` and `(BoxTableName, MigrationVersion)` PK are unchanged; scope has no effect.
- **Reverting `PerSchema` → `Global`** after history has already been placed per-schema — the safe-revert path is not defined or supported by this feature; the supported direction is the forward `Global` → `PerSchema` opt-in (FR5).
- Versioning / schema-evolution of the history table's own shape (a separate concern noted in C3).
- Cross-tenant reporting, aggregation, or a "list all tenants' history" capability.
- Changing the box (outbox/inbox) table placement itself — already handled by the F1a/F1b/F1c fixes.
- Automatic discovery or enumeration of tenant schemas.

## Acceptance Criteria

How we'll know this is working correctly:

- **AC1** — On MSSQL and PostgreSQL, with scope `PerSchema` and a non-null `SchemaName`, the history table is created in that `SchemaName`, and detection + history-row writes target the same schema (verified by integration test against real containers per the project's integration-test convention; the consistent cross-backend behaviour here also exercises NF4).
- **AC1a** — On MSSQL and PostgreSQL, with scope `PerSchema` and a **null** `SchemaName`, provisioning is rejected at entry with a `ConfigurationException`; no history table is created and no fall-back to `Global` occurs (exercises FR1a).
- **AC1b** — On MySQL, SQLite, and Spanner (the non-placement backends), selecting `PerSchema` has no placement effect *regardless of `SchemaName` (including null)*: history remains where it does under `Global` (connection `DATABASE()` for MySQL; the single database/file for SQLite/Spanner), and **no** `ConfigurationException` is thrown (exercises the out-of-scope decision; complements FR1a, which scopes the null-`SchemaName` guard to the placement backends).
- **AC2** — With the default scope `Global`, the history table resolves to today's location on every backend; an existing default-schema deployment upgrading Brighter sees no table move and no migration re-run (regression-guarded by test).
- **AC2a** — With scope `Global` *and a non-null `SchemaName` set* (box tables in a non-default schema), history still resolves to the default location (`dbo` / `public` / `DATABASE()`) — per-schema history is **not** inferred from `SchemaName` alone (verified by test exercising the FR1/FR4 interaction).
- **AC3** — Provisioning is idempotent under `PerSchema`: a second provisioning run applies no migrations and inserts no duplicate history rows.
- **AC4** — On MSSQL and PostgreSQL, two tenants with distinct `SchemaName` values, both under `PerSchema`, each get an independent, correct history table; applying a migration for one tenant does not affect the other's history.
- **AC5** — A deployment with a populated default-schema history table that flips from `Global` to `PerSchema` does not re-apply any migration whose `(SchemaName, BoxTableName, MigrationVersion)` key already exists (exercises FR5).
- **AC6** — Identifier-safety validation still rejects unsafe schema/table identifiers; no new injection surface (covered by existing assertions + a negative test).
- **AC7** — Each provisioning run logs the resolved history-table schema and active scope (exercises NF5), assertable in a test.
- **Definition of done** — All ACs covered by tests following the project TDD workflow (`/test-first` per behaviour, approval gate before GREEN); ADR documenting the chosen mechanism and migration story is approved; XML docs and any operator-facing documentation updated; behaviour for non-opted-in operators is provably unchanged.

## Additional Context

The issue suggested two candidate mechanisms. They have been weighed and **mechanism (2) is the chosen direction**:

1. *(rejected)* **Honour `SchemaName` unconditionally** — the history table would follow the configured `SchemaName` automatically. Rejected because it silently changes behaviour for every operator who already sets `SchemaName`, breaking the upgrade-safe guarantee (FR4) without an explicit opt-in.
2. *(chosen)* **Explicit opt-in scope** — `BoxProvisioningOptions.MigrationHistoryScope` enum, `Global` (default, today's behaviour) | `PerSchema`. Keeps the default behaviour explicit and upgrade-safe; per-schema placement is engaged only when selected.

Naming rationale (final labels confirmed in the ADR): `Scope` over `Schema` because the property selects the *breadth* of history sharing, not a schema name; `PerSchema` over `Tenant` because Brighter routes by `SchemaName` and does not otherwise model a tenant concept (see A1).

Existing deployments already have a populated default-schema `__BrighterMigrationHistory` whose rows are `SchemaName`-stamped; the opt-in path must keep those rows visible to detection so already-applied migrations are not re-run (FR5).

**Source provenance**: raised in code review of PR #4039 (reviewer item F2-5). Related ADR: [0057 Box Schema Versioning and Migrations](../../docs/adr/0057-box-schema-versioning-and-migrations.md) and the box-provisioning RDD ADRs 0058/0059. Issue labels: `feature request`, `V10.X`, `Agent Friendly`.
