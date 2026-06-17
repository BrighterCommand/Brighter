# 60. Multi-Tenancy: Per-Schema Placement of the Box Migration-History Table

Date: 2026-05-27

## Status

Accepted

## Context

The box-provisioning migration runner records applied migrations in a history table — `__BrighterMigrationHistory` on the relational backends. Today that table is **always** created in the backend's default schema, regardless of the configured `IAmARelationalDatabaseConfiguration.SchemaName`:

- MSSQL → `[dbo].[__BrighterMigrationHistory]` (`HISTORY_TABLE_SCHEMA = "dbo"`, `MsSqlBoxMigrationRunner.cs:50`)
- PostgreSQL → `"public"."__BrighterMigrationHistory"` (`HISTORY_TABLE_SCHEMA = "public"`, `PostgreSqlBoxMigrationRunner.cs:53`)
- MySQL → unqualified, lives in the connection-bound `DATABASE()` (`MySqlBoxMigrationRunner.cs:137`)

Operators running a schema-per-tenant deployment want the history table inside the tenant's isolation, backup-restore, and retention boundary, not in a schema shared with other tenants. Spec 0029 (requirements approved 2026-05-27) fixes the *behavioural contract*; this ADR fixes the *architecture* that delivers it.

**Parent Requirement**: [specs/0029-multi-tenancy-migrations/requirements.md](../../specs/0029-multi-tenancy-migrations/requirements.md)

**Scope**: This ADR is the single architectural decision for spec 0029 — *how* to make migration-history placement an opt-in, schema-aware capability across the role/template-method box-provisioning architecture established by [ADR 0057](0057-box-schema-versioning-and-migrations.md), [ADR 0058](0058-box-provisioning-rdd-role-interfaces.md), and [ADR 0059](0059-box-provisioning-abstract-base-naming-symmetry.md). It covers six connected sub-decisions (the option, schema resolution, the misconfiguration guard, end-to-end schema-awareness, the existing-deployment seed, and observability). They are presented together because they share one responsibility — *deciding and acting on where history lives* — and splitting them would scatter that responsibility.

### Forces

- **Backward compatibility is non-negotiable (FR4/NF1)**: operators who do not opt in — *including those who already set a non-null `SchemaName`* — must see byte-for-byte the same placement and no migration re-run.
- **Three call sites of one rule**: the *write* side (runner: CREATE TABLE + INSERT history row), the *authoritative read* side (runner, under the advisory lock, via the detection helper), and a *pre-lock non-authoritative read* (`SqlBoxProvisioner.DetectTableStateAsync`, `SqlBoxProvisioner.cs:141-169`, which calls `DoesHistoryExistAsync`/`DetectCurrentVersionAsync`/`GetMaxVersionAsync` before the runner runs). The detection helper is a stateless DI singleton (`MsSqlBoxDetectionHelper.cs:38`) whose history queries currently hardcode the default schema (`MsSqlBoxDetectionHelper.cs:93`). If these sites resolve the schema independently they can diverge — duplicated knowledge (a design-principle violation) and a latent correctness bug. The provisioner's pre-lock read is explicitly a discarded hint (`SqlBoxProvisioner.cs:155-157`: "the runner re-detects under the lock") — the runner's under-lock detection is the single authoritative read — but the provisioner is still a compile-time caller of the detection methods and is in scope for the structural signature change.
- **Existing deployments (FR5)**: a deployment with a populated `dbo`/`public` history table that flips to `PerSchema` must not re-run already-applied migrations. Detection reads from the *resolved* (per-schema) table, where the prior rows do not yet live — so a flip needs the prior facts to reach the new location.
- **Backend asymmetry**: MSSQL/PG have a true schema distinct from the database; MySQL's "schema" *is* a database (`MySqlOutboxMigrationCatalog.cs:232` treats a configured schema as a separate database); SQLite/Spanner have no schema concept. Per-schema placement is therefore defined only for MSSQL and PostgreSQL (requirements Out of Scope).
- **Constraints**: must reuse the `SqlBoxMigrationRunner<TConnection,TTransaction>` template-method base (`SqlBoxMigrationRunner.cs:54`), the existing role interfaces, `Paramore.Brighter.ConfigurationException` for config guards, and the existing `ILogger`/identifier-safety (`Identifiers.AssertSafe`, `PgIdentifier`) machinery. No new defaults; no behaviour change off the opt-in.

## Decision

Introduce an explicit, opt-in **`MigrationHistoryScope`** and make history-table placement a first-class responsibility of the runner base, resolved **once** and handed to the detection helper, so write and read sides cannot diverge. Six connected decisions:

### D1 — The option (FR1)

Add an enum and a property on the existing options type (`BoxProvisioningOptions.cs`):

```csharp
namespace Paramore.Brighter.BoxProvisioning;

/// <summary>Controls where the box migration-history table is physically placed.</summary>
public enum MigrationHistoryScope
{
    /// <summary>History lives in the backend default schema (dbo / public / connection DATABASE()).
    /// The default — identical to behaviour prior to this feature.</summary>
    Global = 0,

    /// <summary>On MSSQL and PostgreSQL, history is created in the configured SchemaName,
    /// co-located with the tenant's box tables. No-op on backends without a schema concept.</summary>
    PerSchema = 1
}

// on BoxProvisioningOptions, alongside MigrationLockTimeout:
public MigrationHistoryScope MigrationHistoryScope { get; set; } = MigrationHistoryScope.Global;
```

`Global = 0` is the default so existing construction sites compile and behave unchanged (C2). The scope is threaded into each runner's constructor on the same call path as `MigrationLockTimeout` (`BoxProvisioningOptions → UseBoxProvisioning → runner ctor`).

> **Naming** (requirements Additional Context): `Scope` over `Schema` because it selects the *breadth* of history sharing, not a schema name; `PerSchema` over `Tenant` because Brighter routes by `SchemaName` and models no tenant concept. An enum (not a richer type) is appropriate for a closed two-value mode and reads at the call site.

### D2 — History-schema resolution: one responsibility, in the base (FR2, NF4, AC1b)

The "decide which schema holds history" responsibility lives **once**, in `SqlBoxMigrationRunner`, and the resolved value is passed to the detection helper. Two new protected members on the base let each backend declare its capability and default without duplicating the rule:

```csharp
// SqlBoxMigrationRunner<TConnection, TTransaction>
protected abstract string? DefaultHistorySchema { get; }      // "dbo" | "public" | null (MySQL/SQLite)
protected virtual  bool    SupportsPerSchemaHistory => false; // overridden => true on MSSQL & PG

protected string? ResolveHistorySchema() =>
    _scope == MigrationHistoryScope.PerSchema && SupportsPerSchemaHistory
        ? Configuration.SchemaName            // guaranteed non-null by D3
        : DefaultHistorySchema;               // Global, or any non-placement backend
```

- MSSQL overrides `DefaultHistorySchema => "dbo"`, `SupportsPerSchemaHistory => true`.
- PostgreSQL overrides `DefaultHistorySchema => "public"`, `SupportsPerSchemaHistory => true`.
- MySQL / SQLite: `SupportsPerSchemaHistory` stays `false` → `ResolveHistorySchema()` always returns the default → **`PerSchema` is a no-op there** (AC1b), with no exception (the D3 guard is gated on `SupportsPerSchemaHistory`). Spanner does not derive from this base (ADR 0057 §6) and is untouched.

The history-row `SchemaName` **column value** is unchanged — it remains the *box table's* schema (`schemaName`), as today. Only the physical *table* the rows live in moves. Under `PerSchema` the two coincide; under `Global` they may differ (box in `billing`, history in `dbo`) — which is exactly why a single hardcoded constant cannot serve both and a resolver is required.

### D3 — Misconfiguration guard (FR1a, AC1a)

At the top of `MigrateAsync` (base), on placement backends only:

```csharp
if (_scope == MigrationHistoryScope.PerSchema && SupportsPerSchemaHistory && Configuration.SchemaName is null)
    throw new ConfigurationException(
        "MigrationHistoryScope.PerSchema requires a non-null SchemaName; there is no schema to place history in.");
```

Gating on `SupportsPerSchemaHistory` is what makes FR1a (reject) and AC1b (MySQL no-op, no throw) consistent: the guard fires only where `PerSchema` actually performs placement. This reuses the established config-guard pattern (`SqlBoxMigrationRunner.cs:165`, `:410`).

### D4 — Schema-aware end-to-end (FR2, FR3, NF3)

Replace every hardcoded use of `HISTORY_TABLE_SCHEMA` with the resolved value, on both sides:

- **Write side (runner)**: `EnsureHistoryTableAsync` CREATE DDL (`MsSqlBoxMigrationRunner.cs:140`, `PostgreSqlBoxMigrationRunner.cs:130`) and the history-row INSERT (`:281` / `:295`) qualify the table with `ResolveHistorySchema()`.
- **Read side (detection helper)**: the history-reading methods — `DoesHistoryExistAsync` and `GetMaxVersionAsync` — gain a **nullable** `historySchema` parameter (a **structural** interface change to `IAmABoxMigrationDetectionHelper<TConnection,TTransaction>`), where `null` means "the backend default schema" — i.e. today's behaviour. **[Errata 2026-05-27 — see Errata section: `DetectCurrentVersionAsync` is NOT in this set (it reads box-table columns, not history), so `IAmAVersionDetectingMigrationHelper<,>` does not change; and the MSSQL `DoesHistoryExistAsync` existence-delegation argument must also carry `historySchema`.]** The helper builds the qualified name with the existing safe-quoting:
  - **MSSQL**: `Identifiers.AssertSafe` + bracket-quoting; CREATE/INSERT and the read existence/version queries all qualify with the resolved schema.
  - **PostgreSQL**: the resolved schema is folded **identically on both sides** — `PgIdentifier.Quote` for *every* `"public"`-qualified table reference, on the write side (CREATE/INSERT, today embedding the literal `"public"`) **and** the read side (`PostgreSqlBoxDetectionHelper`'s `DoesHistoryExistAsync` COUNT qualifier `:132` and `GetMaxVersionAsync`'s qualifier), plus `PgIdentifier.Normalize` for the `information_schema` existence-check parameter (today the literal `TABLE_SCHEMA = 'public'` at `:122`). Because `Quote` and `Normalize` both lower-case (`PgIdentifier.cs:57,77`), a mixed-case `SchemaName` (`"Billing"`) folds to `billing` on write and read alike, so detection finds the table it created.
  This adds **no** new injection surface (NF3) — the schema is the already-validated `SchemaName`.
- **Pre-lock read (`SqlBoxProvisioner`)**: the provisioner calls the same three read methods before the runner runs (`SqlBoxProvisioner.cs:141-169`) and holds the runner only via the narrow `IAmABoxMigrationRunner` interface, so it cannot call `ResolveHistorySchema()`. It passes `historySchema: null` — its pre-lock result is an explicitly discarded hint (`SqlBoxProvisioner.cs:155-157`); the runner's under-lock re-detection is authoritative, so FR3 ("detection and provisioning never disagree about which schema holds history") holds at the authoritative site. The provisioner is a **mandatory** structural-change call site (see Implementation Approach).

Because under `Global` `ResolveHistorySchema()` returns the same constant the code uses today, and `historySchema: null`/default reproduces today's queries, every existing code path is byte-for-byte unchanged (FR4/NF1).

### D5 — Existing-deployment seed: one-time copy on first per-schema creation (FR5, AC5, NF2)

When, under `PerSchema`, the per-schema history table **does not yet exist** at the start of a provisioning run and a legacy default-schema history table **does** exist, `EnsureHistoryTableAsync` — after creating the per-schema table, inside the same advisory-lock + transaction it already holds — copies the prior facts for this box into the new table:

```sql
-- All FIVE columns of __BrighterMigrationHistory, matching the CREATE DDL
-- (MsSqlBoxMigrationRunner.cs:141-148 / PostgreSqlBoxMigrationRunner.cs:130-137).
-- Description is NOT NULL with no default, so it MUST be copied — omitting it fails the insert.
INSERT INTO [<schema>].[__BrighterMigrationHistory]
    (MigrationVersion, SchemaName, BoxTableName, Description, AppliedAt)
SELECT src.MigrationVersion, src.SchemaName, src.BoxTableName, src.Description, src.AppliedAt
FROM   [<default>].[__BrighterMigrationHistory] src
WHERE  src.SchemaName = @schemaName            -- only this tenant's rows from the shared table
  AND  src.BoxTableName = @boxTableName
  AND  NOT EXISTS (                             -- idempotent: composite-PK match already present
        SELECT 1 FROM [<schema>].[__BrighterMigrationHistory] tgt
        WHERE tgt.SchemaName = src.SchemaName
          AND tgt.BoxTableName = src.BoxTableName
          AND tgt.MigrationVersion = src.MigrationVersion);
```

- **One-time**: `EnsureHistoryTableAsync` today uses `CREATE TABLE IF NOT EXISTS` and returns no created-vs-pre-existed signal, so this feature adds a **pre-create probe** of the per-schema history table inside `EnsureHistoryTableAsync` (under the lock): if it was absent and a legacy default-schema table exists, run the seed after creating it. After the first per-schema run the legacy default-schema table is never read again — the per-schema table is fully self-contained and the tenant is isolated going forward (the feature's intent). Even without a perfect table-level gate, the `NOT EXISTS` row guard below makes the seed idempotent.
- **Idempotent (NF2)**: the `NOT EXISTS` guard means a retried/partial run cannot duplicate rows; the composite PK `(SchemaName, BoxTableName, MigrationVersion)` is the backstop.
- **Filtered**: only rows whose `SchemaName` column matches this tenant are copied, because the shared default table holds every tenant's history.
- **Fresh deployments** that start on `PerSchema` with no legacy table simply skip the seed and take the normal fresh-install path.

This makes AC5 satisfiable by an automated integration test (set up `dbo` history → flip config → provision → assert no re-run), which a manual-script-only approach could not.

### D6 — Observability (NF5, AC7)

On each provisioning run the runner logs, at **Information** level (provisioning is a startup-time, low-volume operation; operators need to confirm placement), the active scope and resolved history schema:

```csharp
Logger.LogInformation(
    "Box migration history for {BoxTable} resolved to schema {HistorySchema} (scope {Scope})",
    tableName, ResolveHistorySchema() ?? "<backend default>", _scope);
```

When the D5 seed runs, a distinct Information log records the row count copied from the legacy table, and an OpenTelemetry `Activity` event is added to the migration span (consistent with the existing race-swallow event at `MsSqlBoxMigrationRunner.cs:174-175`).

### Architecture Overview

```
BoxProvisioningOptions.MigrationHistoryScope (Global default)
            │  (threaded via UseBoxProvisioning → runner ctor, same path as MigrationLockTimeout)
            ▼
SqlBoxMigrationRunner<TConn,TTx>  ── owns placement decision + history-table lifecycle
  │  D3 guard (MigrateAsync entry): PerSchema + placement-backend + null SchemaName → ConfigurationException
  │  D2 ResolveHistorySchema()  ◀── DefaultHistorySchema (abstract) + SupportsPerSchemaHistory (virtual)
  │        │ single source of truth
  │        ├─► D4 write side: EnsureHistoryTableAsync CREATE + history-row INSERT  (qualified by resolved schema)
  │        ├─► D5 seed (first per-schema creation only): copy legacy rows WHERE SchemaName=@tenant, NOT EXISTS
  │        ├─► D6 log: Information(scope, resolved schema)
  │        └─► passes resolved historySchema ▼
  │                                  IAmAVersionDetectingMigrationHelper (stateless singleton)
  │                                    D4 read side: existence / MAX(version) / detect — FROM <historySchema>.__BMH
  ▼
 MsSql / PostgreSql runners  → SupportsPerSchemaHistory = true,  DefaultHistorySchema = "dbo" / "public"
 MySql / Sqlite  runners     → SupportsPerSchemaHistory = false → PerSchema is a no-op (AC1b)
 Spanner                     → does not derive from base (ADR 0057 §6); untouched
```

### Key Components (Responsibility-Driven Design)

| Component | Stereotype | Responsibility | Change |
|---|---|---|---|
| `MigrationHistoryScope` (enum) | information holder | *knows* the operator's placement intent | **new** |
| `BoxProvisioningOptions` | information holder | *holds* the scope alongside `MigrationLockTimeout` | +1 property |
| `SqlBoxMigrationRunner<T,Tx>` | coordinator / controller | *decides* the history schema (D2), *guards* misconfig (D3), *acts* on placement + seed (D4/D5), *reports* (D6) | base gains `DefaultHistorySchema`, `SupportsPerSchemaHistory`, `ResolveHistorySchema()`, seed logic |
| `MsSql/PostgreSql` runners | service provider | declare placement capability + default schema; emit qualified DDL/INSERT | override two members |
| `IAmAVersionDetectingMigrationHelper` impls | information holder / service provider | *read* history from the schema they are *told* to | history methods gain `historySchema` param |

The placement *decision* and the history-table *lifecycle* are cohesive and stay together on the runner (which already owns both). The detection helper stays a pure read-side service that is *told where to look* — it gains no decision-making. No new role/type is introduced beyond the enum: the resolver is a base method, not a class, because a single owning collaborator consumes it (principle: do not add types without necessity).

### Technology Choices

- **`enum MigrationHistoryScope`** — closed two-value mode; expressive at call sites; `Global = 0` makes the safe default the zero-value.
- **`Paramore.Brighter.ConfigurationException`** — existing config-guard exception (`ConfigurationException.cs:32`).
- **`Identifiers.AssertSafe` / `PgIdentifier`** — existing identifier-safety; reused, no new injection surface.
- **`ILogger` Information + `Activity` event** — matches the runners' existing logging/telemetry conventions.

### Implementation Approach (Tidy First)

Structural changes precede behavioural ones, in separate commits (design principle / `/tidy-first`):

1. **Structural**: add the nullable `historySchema` parameter to the detection-helper role interfaces and **all five** impls, plus **every call site** — the runner's under-lock reads *and* `SqlBoxProvisioner.DetectTableStateAsync` (`SqlBoxProvisioner.cs:151,158,166`), which passes `null` — with `null`/default reproducing today's queries so behaviour is identical; add the `enum` and option defaulted to `Global`; add `DefaultHistorySchema`/`SupportsPerSchemaHistory`/`ResolveHistorySchema()` returning today's constant. Tests green, no behaviour change.
2. **Behavioural** (TDD, `/test-first` per slice, integration tests on real containers per project convention): D3 guard; D4 wiring of `ResolveHistorySchema()` into write+read sides; D5 seed; D6 logging.

## Consequences

### Positive

- Operators get true per-tenant history isolation on MSSQL/PG, opt-in and upgrade-safe (FR1–FR4).
- Write and read sides resolve placement from **one** method — detection and provisioning cannot disagree (FR3); knowledge is not duplicated.
- Existing deployments can flip without re-running migrations, exercised by an automated test (FR5/AC5).
- Off the opt-in, every path returns today's constant → provably unchanged behaviour (FR4/NF1).
- Fits the existing template-method base; the per-backend delta is two small overrides.

### Negative

- The detection-helper role interfaces gain a parameter — a breaking change to those interfaces (internal to box-provisioning, but public types). All five backends, the `SqlBoxProvisioner` pre-lock call site, and any external implementor must add it (mitigated: the parameter is nullable and `null` = today's behaviour).
- The runner base grows three members plus seed logic — more surface on an already-central class.
- `PerSchema` on MySQL/SQLite is silently a no-op. It is documented (XML docs + AC1b) but an operator could still expect placement; mitigated by the Information log (D6) showing the resolved default schema.

### Risks and Mitigations

- **Cross-schema read permission at flip time (D5)**: the one-time seed reads the legacy default-schema table; under tenant-isolated credentials that may be denied. *Mitigation*: provisioning typically runs with elevated (DDL-capable) credentials; document that the `Global → PerSchema` flip must run with read access to the legacy history table, and surface a clear error if the read fails. The reverse flip is out of scope (requirements).
- **Legacy rows left behind**: after the seed, the tenant's rows remain in the shared default table (harmless duplicates, ignored under `PerSchema`). *Mitigation*: documented; cleanup is explicitly out of scope (requirements).
- **Partial seed on crash**: *Mitigation*: seed runs inside the existing advisory-lock + transaction and is `NOT EXISTS`-guarded, so a retry completes idempotently (NF2).
- **Identifier safety regression**: *Mitigation*: reuse `Identifiers.AssertSafe`/`PgIdentifier`; add a negative test (AC6).

## Alternatives Considered

- **Honour `SchemaName` unconditionally (no opt-in)** — rejected in requirements: silently changes behaviour for every operator who already sets `SchemaName`, breaking FR4.
- **Dual-read / read-through (no data motion)** — detection reads the union of per-schema and legacy default tables forever. Rejected: it permanently couples the tenant to the shared default schema (ongoing read permission; the default table can never be dropped), defeating the isolation/backup-restore boundary that is the feature's purpose.
- **Move-with-delete** — copy then delete legacy rows. Rejected as default: destructive, and the shared default table may be co-owned by other tenants/processes; deletion risks and permission needs outweigh the benefit of not leaving harmless duplicates. The non-destructive seed (D5) is preferred.
- **Manual migration script only** — ship a documented SQL script, no automatic seed. Rejected: AC5 requires an automated test of the flip, which a manual script does not satisfy within provisioning; and it pushes correctness onto every operator.
- **A dedicated `HistorySchemaResolver` value object / role** — extract resolution into its own type consumed by both runner and helper. Deferred: the runner already owns history-table lifecycle and is the sole resolver; the detection helper is *told* the result. A base method keeps the responsibility cohesive without adding a type (revisit only if a third consumer appears).
- **Per-backend resolution (no base method)** — let each runner null-coalesce independently. Rejected: duplicates the scope rule across backends and across the write/read split — the exact divergence risk D2 removes.

## Errata

**2026-05-27 (raised in `/spec:review tasks`, findings #1 and #2; corrections grounded against `master`):**

1. **`DetectCurrentVersionAsync` is not a history-reading method.** D4 originally listed it (and "the version `SELECT`") among the methods gaining `historySchema`. In the actual code it reads the **box table's** column set (`MsSqlBoxDetectionHelper.cs:156-180` → `GetTableColumnsAsHashSetAsync` → `INFORMATION_SCHEMA.COLUMNS`), never `__BrighterMigrationHistory`. The correct change set is **only `DoesHistoryExistAsync` and `GetMaxVersionAsync`**, both on `IAmABoxMigrationDetectionHelper<,>`. Consequently **`IAmAVersionDetectingMigrationHelper<,>` does not change** for this feature (the earlier text naming it as a structurally-changed interface is withdrawn).

2. **MSSQL history-existence delegation must also carry the resolved schema.** `MsSqlBoxDetectionHelper.DoesHistoryExistAsync` decides existence by delegating to `DoesTableExistAsync(connection, "__BrighterMigrationHistory", DefaultSchemaName, ...)` (`:85-86`) *before* the COUNT at `:93`. The `DoesTableExistAsync` signature is unchanged, but this internal call must pass `historySchema` (not the hardcoded `DefaultSchemaName`); otherwise PerSchema detection short-circuits `false` and migrations re-run (breaking FR5). PostgreSQL is unaffected — its existence check is inlined with the `TABLE_SCHEMA` literal (`PostgreSqlBoxDetectionHelper.cs:122`), already covered by D4.

These corrections are reflected in `specs/0029-multi-tenancy-migrations/tasks.md` (S1, T3) and do not change the decision's substance (single resolver, opt-in scope, upgrade-safe default).

## References

- Requirements: [specs/0029-multi-tenancy-migrations/requirements.md](../../specs/0029-multi-tenancy-migrations/requirements.md)
- Related ADRs: [0057 Box Schema Versioning and Migrations](0057-box-schema-versioning-and-migrations.md) (history-table model; Spanner exemption §6), [0058 Box Provisioning RDD Role Interfaces](0058-box-provisioning-rdd-role-interfaces.md) (detection-helper roles), [0059 Box Provisioning Abstract-Base Naming Symmetry](0059-box-provisioning-abstract-base-naming-symmetry.md) (`SqlBoxMigrationRunner` base)
- Issue: [#4144](https://github.com/BrighterCommand/Brighter/issues/4144) (reviewer item F2-5 on PR #4039)
- Key code: `SqlBoxMigrationRunner.cs:54` (base), `:339` (`EnsureHistoryTableAsync` hook), `:88` (`Configuration`); `MsSqlBoxMigrationRunner.cs:50`/`PostgreSqlBoxMigrationRunner.cs:53` (`HISTORY_TABLE_SCHEMA`); `MsSqlBoxDetectionHelper.cs:93` (hardcoded read); `BoxProvisioningOptions.cs:74` (`MigrationLockTimeout`); `ConfigurationException.cs:45` (`(string)` ctor); `SqlBoxProvisioner.cs:141-169` (pre-lock detection call site)
