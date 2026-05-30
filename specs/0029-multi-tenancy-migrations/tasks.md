# Tasks — Spec 0029 Multi-Tenancy Migration History Scope

Derived from approved **requirements.md** (FR1–FR6, NF1–NF5, AC1–AC7) and approved **ADR 0060** (D1–D6).

**Workflow gates (CLAUDE.md):**
- **TDD is MANDATORY.** Every behavioural task below is a single `TEST + IMPLEMENT` unit: run the named `/test-first` command, **STOP for approval in the IDE before writing implementation**. Do not write tests manually.
- **Tidy First.** Task S1 is the single **structural**, behaviour-preserving commit and lands **before** any behavioural slice. It is *not* a `/test-first` task — it is validated by the **existing** box-provisioning suite staying green before and after.
- **Integration tests hit REAL databases** on containers (never mock the DB). Container bring-up commands are in `PROMPT.md` → "Container infrastructure".
- **Change scope:** the `Global` default must remain byte-for-byte today's behaviour. Do not change defaults or add unrequested behaviour.

**Per-TFM / per-container gotchas (PROMPT.md):**
- MSSQL: run `-f net9.0` and `-f net10.0` **separately** (parallel-TFM deadlock).
- MySQL: net9.0 only; `dotnet build -f net9.0` before `dotnet test -f net9.0` when test files changed.
- New files in `Paramore.Brighter.BoxProvisioning.Sqlite` need `git add -f` (`.sqlite` ignore quirk).

---

## Reviewer feedback ([PR #4155 comment](https://github.com/BrighterCommand/Brighter/pull/4155#issuecomment-4554219174))

The seven non-blocking refinements raised at spec/ADR approval are surfaced here:

| # | Item | Where addressed in this file |
|---|------|------------------------------|
| 1 | D5 cross-schema read permission → pre-condition + hardened error + negative test | T9/T10 impl (rethrow `ConfigurationException` with explicit message); **T-PERM** negative test; DOC task |
| 2 | Specify the legacy-table existence probe query | T9 (MSSQL `sys.tables`/`sys.schemas`), T10 (PG `information_schema.tables`) impl bullets |
| 3 | D6 seed log structured fields + `Activity` attribute | T12 impl (`{RowCount}/{LegacySchema}/{TargetSchema}` + `brighter.box.migration.seed.rows`) |
| 4 | Broaden AC1b no-op to MySQL **+ SQLite + Spanner** | T5 (one file per backend, all three) |
| 5 | Structural commit must enumerate all implementors incl. test doubles | S1 (scope guard + enumerated call sites + `TestDoubles/`) |
| 6 | Note the defensive `SchemaName ... DEFAULT 'dbo'` column | T9/T10 impl bullets (defensive-column note) |
| 7 | Tag NF4 on AC1 | T3/T4 headings ("FR2, FR3, NF3, NF4, AC1") |

---

## Legend / dependencies

```
S1 (structural, tidy-first)  ──►  all behavioural tasks
   T1 (MSSQL D3 guard + MSSQL SupportsPerSchemaHistory override) ─►  prereq for T3
   T2 (PG D3 guard reuse + PG SupportsPerSchemaHistory override)  ─►  depends on T1; prereq for T4
   T3,T4 (D2+D4 core placement, AC1) ──►  T3 depends on T1, T4 depends on T2; prerequisite for T7,T8,T9,T10,T11,T12
   T5 (AC1b no-op)        ───────►  independent (no new impl beyond S1)
   T6 (AC2a Global regression) ──►  independent
   T7 (AC3 idempotency)   ───────►  depends on T3/T4
   T8 (AC4 two-tenant)    ───────►  depends on T3/T4
   T9,T10 (D5 seed, AC5)  ───────►  depends on T3/T4
   T-PERM (D5 read-permission failure) ─►  depends on T9/T10
   T11 (AC6 identifier safety) ──►  depends on T3/T4
   T12 (D6 logging, AC7)  ───────►  depends on T3 (+ T9 for seed log)
   DOC, VERIFY, REVIEW    ───────►  last
```

---

## S1 — STRUCTURAL (Tidy First, one commit, NO behaviour change)

- [x] **STRUCTURAL: Introduce the scope option and the schema-resolution seam, behaviour-preserving**
  - **NOT a `/test-first` task.** Use `/tidy-first` framing. Run the **existing** box-provisioning suite green **before** starting and **after** finishing — zero behavioural diff. Commit separately from all behavioural work.
  - Sub-steps (all in this one structural commit):
    - **D1 option:** add `public enum MigrationHistoryScope { Global = 0, PerSchema = 1 }` in namespace `Paramore.Brighter.BoxProvisioning` (XML-doc both values). Add `public MigrationHistoryScope MigrationHistoryScope { get; set; } = MigrationHistoryScope.Global;` to `BoxProvisioningOptions` (alongside `MigrationLockTimeout`, `BoxProvisioningOptions.cs:74`). `Global = 0` so existing construction sites compile unchanged (C2).
    - **Thread scope into runner ctors** on the same path as `MigrationLockTimeout` (`BoxProvisioningOptions → UseBoxProvisioning → runner ctor`) for the **four relational runners** (MSSQL, PG, MySQL, SQLite). Spanner does **not** derive from the base and is left untouched (ADR 0057 §6).
    - **D2 base seam (returns today's constant — no behaviour change yet):** on `SqlBoxMigrationRunner<TConnection,TTransaction>` (`SqlBoxMigrationRunner.cs:54`) add `protected abstract string? DefaultHistorySchema { get; }`, `protected virtual bool SupportsPerSchemaHistory => false`, and `protected string? ResolveHistorySchema()`. Implement `DefaultHistorySchema` per backend = today's constant: MSSQL `"dbo"`, PG `"public"`, MySQL/SQLite their current default. `ResolveHistorySchema()` body per D2 (returns `DefaultHistorySchema` while every `SupportsPerSchemaHistory` is still `false`/scope unused, so the resolved value is identical to today's hardcoded `HISTORY_TABLE_SCHEMA`).
    - **Detection-helper interface signature change:** add a **nullable `string? historySchema`** parameter (semantics: `null` = "history lives in the backend default schema" = today's behaviour) to the **two history-reading** methods on `IAmABoxMigrationDetectionHelper<,>`: `DoesHistoryExistAsync` and `GetMaxVersionAsync`. **Do NOT** add it to `DetectCurrentVersionAsync` — that method reads the *box table's columns* (`GetTableColumnsAsHashSetAsync` → `INFORMATION_SCHEMA.COLUMNS`, `MsSqlBoxDetectionHelper.cs:162`), not the history table, so it is a box-table method like `DoesTableExistAsync`/`GetTableColumnsAsync` and takes no `historySchema`. Consequently **`IAmAVersionDetectingMigrationHelper<,>` does not change at all** for this feature. (Review finding #2; ADR 0060 D4 carries the same mislabel — see the D4 errata note added to the ADR.) Note the existing `schemaName` param stays — it is the box table's schema / row-filter value; `historySchema` is the *physical* schema of the history table. Keep the established parameter-ordering convention (`cancellationToken` before optional `transaction`); document the new param in XML-doc.
    - **Scope guard (reviewer #5):** *only* the two history-reading methods (`DoesHistoryExistAsync`, `GetMaxVersionAsync`) gain the param. `DoesTableExistAsync`, `GetTableColumnsAsync`, `DetectCurrentVersionAsync`, and `DiscriminatorFor` are **unchanged** (box-table / pure-function methods). Keeping this boundary explicit prevents the later GREEN diffs from absorbing a structural change.
    - **Update ALL FIVE detection-helper impls** (`MsSql`, `PostgreSql`, `MySql`, `Sqlite`, `Spanner` `*BoxDetectionHelper.cs`) so the two methods accept `historySchema` and, when `null`, reproduce today's exact query (backend default schema). No query should change shape under `null`.
    - **MSSQL existence-delegation gotcha (review finding #1):** `MsSqlBoxDetectionHelper.DoesHistoryExistAsync` decides whether the history table exists by delegating to `DoesTableExistAsync(connection, "__BrighterMigrationHistory", DefaultSchemaName, ...)` (`:85-86`) — a hardcoded `dbo` argument — *before* the COUNT at `:93`. The method `DoesTableExistAsync` keeps its signature, but in S1 this internal call must pass the new `historySchema` (still `null` here, so still `dbo` — behaviour-preserving) rather than the literal `DefaultSchemaName`, so that T3 can later flow the resolved per-schema value through it. If left as `DefaultSchemaName`, PerSchema detection short-circuits `false` and re-runs migrations. (PG has no equivalent issue — its existence check is inlined at `:122`.)
    - **Update EVERY call site / implementor — enumerate them so the structural commit is complete (reviewer #5):**
      - Runner under-lock authoritative reads → pass `ResolveHistorySchema()` (initially still returns the default constant).
      - `SqlBoxProvisioner.DetectTableStateAsync` (`SqlBoxProvisioner.cs:151/158/166`) → passes `historySchema: null` (its pre-lock read is an explicitly discarded hint, `:155-157`; the runner re-detects authoritatively under the lock).
      - **Test doubles / fakes** implementing the detection-helper interface in each test project's `TestDoubles/` directory that has one (MSSQL, PG, MySQL, SQLite `BoxProvisioning` tests, plus `BoxProvisioning.Tests/TestDoubles` and `Core.Tests/BoxProvisioning/TestDoubles`) → add the param with `null`/default so they still compile and behave identically. (Spanner has **no** `BoxProvisioning/TestDoubles` directory and its helper implements only the base interface — nothing to change there beyond the production `SpannerBoxDetectionHelper`.)
  - **Done when:** solution builds on all TFMs; the full existing box-provisioning suite (MSSQL net9.0 + net10.0 separately, PG, MySQL net9.0, SQLite, Spanner, `BoxProvisioning.Tests`, `Core.Tests/BoxProvisioning`) is **green with no behavioural change**.

---

## Behavioural slices (TDD — `/test-first` each, STOP for approval before GREEN)

### D3 — Misconfiguration guard (FR1a, AC1a)

- [x] **TEST + IMPLEMENT: MSSQL PerSchema with null SchemaName is rejected at provisioning entry**
  - **USE COMMAND**: `/test-first when mssql migration runner is invoked with PerSchema scope and a null SchemaName it should throw ConfigurationException and create no history table`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning`
  - Test file: `When_mssql_per_schema_scope_is_selected_with_null_schema_name_it_should_throw_configuration_exception.cs`
  - Test should verify:
    - Runner configured with `MigrationHistoryScope.PerSchema` and `SchemaName == null`
    - `MigrateAsync` throws `Paramore.Brighter.ConfigurationException`
    - No history table is created; no silent fall-back to `Global`
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - At the **top** of `SqlBoxMigrationRunner.MigrateAsync` add the D3 guard: `if (scope == PerSchema && SupportsPerSchemaHistory && Configuration.SchemaName is null) throw new ConfigurationException("MigrationHistoryScope.PerSchema requires a non-null SchemaName; there is no schema to place history in.");`
    - **Deliver MSSQL's `SupportsPerSchemaHistory => true` override here** — the guard is gated on `SupportsPerSchemaHistory`, so it cannot fire (and this test cannot go GREEN) without it. This is the first test that requires the override; T3 then *consumes* it. (`DefaultHistorySchema => "dbo"` already exists from S1.)
    - Reuse the existing config-guard pattern (`SqlBoxMigrationRunner.cs:165/410`). Guard placed on the base so it applies uniformly; gating on `SupportsPerSchemaHistory` keeps MySQL/SQLite out (T5).

- [x] **TEST + IMPLEMENT: PostgreSQL PerSchema with null SchemaName is rejected at provisioning entry**
  - **USE COMMAND**: `/test-first when postgres migration runner is invoked with PerSchema scope and a null SchemaName it should throw ConfigurationException and create no history table`
  - Test location: `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning`
  - Test file: `When_postgres_per_schema_scope_is_selected_with_null_schema_name_it_should_throw_configuration_exception.cs`
  - Test should verify:
    - Same as the MSSQL case, against the PG runner/container
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - The base D3 guard already exists (delivered in T1). **Deliver PG's `SupportsPerSchemaHistory => true` override here** — same reasoning as T1: the guard cannot fire on PG without it, so this test drives it into existence; T4 then *consumes* it. (`DefaultHistorySchema => "public"` already exists from S1.) No other PG-specific code.
  - **Depends on**: T1 (base D3 guard).

### D2 + D4 — Schema-aware placement, end-to-end (FR2, FR3, NF3, NF4, AC1)

- [x] **TEST + IMPLEMENT: MSSQL PerSchema places history in the configured schema, with detection and writes consistent**
  - **USE COMMAND**: `/test-first when mssql migration runner uses PerSchema scope with a non-null SchemaName it should create the history table in that schema and detect and write history there`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning`
  - Test file: `When_mssql_per_schema_scope_is_selected_it_should_create_history_table_in_configured_schema.cs`
  - Test should verify (real container):
    - With `PerSchema` + a created non-`dbo` schema, `__BrighterMigrationHistory` is created in **that** schema (assert via catalog query), **not** in `dbo`
    - Migration runs to latest; a history row exists in the per-schema table with the box's `SchemaName` column value
    - A second detection (existence + max version) reads the per-schema table and agrees (no migration re-run on re-detect)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - **Depends on**: T1 (delivers MSSQL `SupportsPerSchemaHistory => true` + base guard).
  - Implementation should:
    - Consume the MSSQL `SupportsPerSchemaHistory => true` override (delivered in T1) and `DefaultHistorySchema => "dbo"` (from S1) — no re-declaration here.
    - **Write side** (`MsSqlBoxMigrationRunner`): replace hardcoded `HISTORY_TABLE_SCHEMA`/`[dbo]` in `EnsureHistoryTableAsync` CREATE DDL (`:140-148`) and the history-row INSERT (`:281`) with `ResolveHistorySchema()`, quoted via `Identifiers.AssertSafe` + brackets.
    - **Read side** (`MsSqlBoxDetectionHelper`): use the passed `historySchema` (bracket-quoted, `AssertSafe`) in **both** the history-existence delegation (`:85-86` — pass `historySchema` to the inner `DoesTableExistAsync` instead of `DefaultSchemaName`; see S1 gotcha) **and** the COUNT query (`:93`), and likewise in `GetMaxVersionAsync` (`:119`). The runner passes `ResolveHistorySchema()` to `DoesHistoryExistAsync`/`GetMaxVersionAsync` on its under-lock reads. (`DetectCurrentVersionAsync` is untouched — box-table columns.)

- [x] **TEST + IMPLEMENT: PostgreSQL PerSchema places history in the configured schema, with detection and writes consistent (case-folded identically both sides)**
  - **USE COMMAND**: `/test-first when postgres migration runner uses PerSchema scope with a non-null SchemaName it should create detect and write history in that schema with identical identifier folding`
  - Test location: `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning`
  - Test file: `When_postgres_per_schema_scope_is_selected_it_should_create_history_table_in_configured_schema.cs`
  - Test should verify (real container):
    - As MSSQL case, against PG, history table created in configured schema (not `public`)
    - **Mixed-case `SchemaName`** (e.g. `"Billing"`) folds identically on write and read so detection finds the table it created (regression against the `Quote`/`Normalize` lower-casing)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - **Depends on**: T2 (delivers PG `SupportsPerSchemaHistory => true`).
  - Implementation should:
    - Consume the PG `SupportsPerSchemaHistory => true` override (delivered in T2) and `DefaultHistorySchema => "public"` (from S1) — no re-declaration here.
    - **Write side** (`PostgreSqlBoxMigrationRunner`): every `"public"`-qualified history reference (CREATE `:130-137`, INSERT `:295`) uses `PgIdentifier.Quote(ResolveHistorySchema())`.
    - **Read side** (`PostgreSqlBoxDetectionHelper`): COUNT qualifier (`:132`) and `GetMaxVersionAsync` qualifier use `PgIdentifier.Quote(historySchema)`; the `information_schema` existence-check param (`TABLE_SCHEMA = 'public'`, `:122`) uses `PgIdentifier.Normalize(historySchema)`. Both `Quote` and `Normalize` lower-case (`PgIdentifier.cs:57/77`) — fold identically on both sides. No new injection surface (NF3): the schema is the already-validated `SchemaName`.

- [x] **TEST + IMPLEMENT: PerSchema is a no-op on MySQL, SQLite, and Spanner (no placement, no throw)**
  - **USE COMMAND**: `/test-first when PerSchema scope is selected on mysql sqlite or spanner it should keep history in the default location and not throw`
  - Test location: one file per backend —
    - `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning/When_mysql_per_schema_scope_is_selected_it_should_keep_history_in_connection_database_and_not_throw.cs`
    - `tests/Paramore.Brighter.Sqlite.Tests/BoxProvisioning/When_sqlite_per_schema_scope_is_selected_it_should_be_a_no_op_and_not_throw.cs`
    - `tests/Paramore.Brighter.Gcp.Tests/Spanner/BoxProvisioning/When_spanner_per_schema_scope_is_selected_it_should_be_a_no_op_and_not_throw.cs`
  - Test should verify (each backend, including with a **non-null** `SchemaName` *and* with null):
    - Selecting `PerSchema` causes **no** placement change — history stays where `Global` puts it (connection `DATABASE()` for MySQL; the single database/file for SQLite/Spanner)
    - **No** `ConfigurationException` is thrown (the D3 guard does not fire — `SupportsPerSchemaHistory == false`)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - **No new production code beyond S1** — these backends keep `SupportsPerSchemaHistory => false` (inherited default), so `ResolveHistorySchema()` returns the backend default and the D3 guard is gated off. These are characterization tests guarding the no-op (AC1b). For new SQLite test files, remember `git add -f`.

### Global default — regression guard (FR4, NF1, AC2, AC2a)

- [x] **TEST + IMPLEMENT: Global scope with a non-default SchemaName still places history in the backend default schema**
  - **USE COMMAND**: `/test-first when global scope is used with a non-default SchemaName the history table should remain in the backend default schema while box tables go to the configured schema`
  - Test location: one file per backend —
    - `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/When_global_scope_is_used_with_a_non_default_schema_mssql_history_should_remain_in_dbo.cs`
    - `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning/When_global_scope_is_used_with_a_non_default_schema_postgres_history_should_remain_in_public.cs`
  - Test should verify:
    - Scope `Global` (default) **and** a non-null, non-default `SchemaName`: box table lands in the configured schema (F1 behaviour) but `__BrighterMigrationHistory` is in `dbo`/`public` — per-schema history is **not** inferred from `SchemaName` alone
    - No migration re-run, no table move (AC2)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - **No new production code expected** — S1 already makes `Global` return the default schema regardless of `SchemaName`. This locks the FR1/FR4 interaction. (Complements the existing `When_history_table_exists_in_a_non_dbo_schema_runner_should_still_create_it_in_dbo` regression tests, which must also remain green.)

### D5 — Existing-deployment seed on first per-schema creation (FR5, NF2, AC3, AC5)

- [x] **TEST + IMPLEMENT: Repeated PerSchema provisioning is idempotent**
  - **USE COMMAND**: `/test-first when per-schema provisioning runs a second time it should apply no migrations and insert no duplicate history rows`
  - Test location: one file per backend —
    - `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/When_mssql_per_schema_provisioning_runs_twice_it_should_be_idempotent.cs`
    - `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning/When_postgres_per_schema_provisioning_runs_twice_it_should_be_idempotent.cs`
  - Test should verify:
    - Provision twice under `PerSchema`; second run applies no migration and the per-schema history row count is unchanged (no duplicates)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Rely on the existing detection-driven short-circuit now targeting the per-schema table (from T3/T4). No re-insert. (Likely no new code beyond T3/T4; this characterizes NF2 under PerSchema.)

- [ ] **TEST + IMPLEMENT: MSSQL flip from Global to PerSchema seeds existing history and re-runs nothing**
  - **USE COMMAND**: `/test-first when an mssql deployment with a populated dbo history table flips to PerSchema it should seed the per-schema table from the legacy rows and re-run no applied migration`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning`
  - Test file: `When_mssql_deployment_flips_from_global_to_per_schema_it_should_not_re_run_applied_migrations.cs`
  - Test should verify (real container):
    - Arrange: provision under `Global` (history populated in `dbo`), with box tables in a non-default schema
    - Act: flip config to `PerSchema` (same `SchemaName`), provision again
    - Assert: per-schema `__BrighterMigrationHistory` now contains this tenant's prior rows (all columns incl. `Description`); **no** migration is re-applied (versions/keys already present); seed is filtered to this tenant's `SchemaName`/`BoxTableName`
    - Idempotency: a further provision run copies nothing more (NOT EXISTS guard)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In `EnsureHistoryTableAsync` add a **pre-create probe** of the per-schema history table (under the existing advisory lock + transaction). Probe query (reviewer #2): `EXISTS(SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = '__BrighterMigrationHistory' AND s.name = @perSchema)`; legacy-existence probe: same against `s.name = @defaultSchema` (`'dbo'`).
    - If the per-schema table was **absent** AND a legacy default-schema table **exists**, after creating the per-schema table run the D5 seed `INSERT ... SELECT` copying all **five** columns `(MigrationVersion, SchemaName, BoxTableName, Description, AppliedAt)` `WHERE src.SchemaName=@schemaName AND src.BoxTableName=@boxTableName AND NOT EXISTS (composite-PK match in target)`. `Description` is NOT NULL with no default — must be copied. Filtered to this tenant. Bracket-quote both `<schema>` and `<default>` via `Identifiers.AssertSafe`.
    - Defensive-column note (reviewer #6): the runner always stamps the explicit box `SchemaName`, so the `SchemaName VARCHAR(256) NOT NULL DEFAULT 'dbo'` column DEFAULT (`:142`) is never exercised — the SELECT copies explicit values, no NULL concern.
    - **Harden the seed read failure (reviewer #1 — must, not should):** wrap the legacy-table read; if it fails (e.g. tenant-isolated credentials lack SELECT on `dbo`) **rethrow a clear error** — `ConfigurationException` whose message states *"the first Global → PerSchema run requires read access to the legacy default-schema history table"* — rather than letting a silent empty seed break FR5 (no re-run). The throw rolls back the transaction. See the dedicated negative test (T-PERM) and DOC task.

- [ ] **TEST + IMPLEMENT: PostgreSQL flip from Global to PerSchema seeds existing history and re-runs nothing**
  - **USE COMMAND**: `/test-first when a postgres deployment with a populated public history table flips to PerSchema it should seed the per-schema table from the legacy rows and re-run no applied migration`
  - Test location: `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning`
  - Test file: `When_postgres_deployment_flips_from_global_to_per_schema_it_should_not_re_run_applied_migrations.cs`
  - Test should verify:
    - Same arrange/act/assert as the MSSQL flip, against PG (legacy table in `public`)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - PG `EnsureHistoryTableAsync` pre-create probe + D5 seed, all `public`/`<schema>` references `PgIdentifier.Quote`-d, existence param `Normalize`-d, identical folding both sides as in T4.
    - Probe query (reviewer #2): `EXISTS(SELECT 1 FROM information_schema.tables WHERE table_name = '__BrighterMigrationHistory' AND table_schema = @perSchema)` with `@perSchema = PgIdentifier.Normalize(SchemaName)`; legacy-existence probe: same against `table_schema = 'public'`.
    - Defensive-column note (reviewer #6): same as MSSQL — the runner always stamps `SchemaName`, the column DEFAULT is never exercised; no NULL concern in the SELECT.
    - **Harden the seed read failure (reviewer #1 — must):** wrap the legacy-table read; on failure rethrow a `ConfigurationException` stating *"the first Global → PerSchema run requires read access to the legacy default-schema history table"* rather than allowing a silent empty seed (FR5 break). Rolls back the transaction. Covered by T-PERM + DOC.

- [x] **TEST + IMPLEMENT: Global → PerSchema flip without read access to the legacy table fails loudly, not silently (reviewer #1)** — `389daf1c4`. SqlBoxProvisioner pre-lock hint slice (catch DbException on both DoesHistoryExistAsync + GetMaxVersionAsync; static ILogger via ApplicationLogging.CreateLogger). Tests symmetric MSSQL+PG; ConfigurationException + documented phrase + inner provider exception + legacy row intact + no per-schema stub. Regression-clean across base/MSSQL/PG/MySQL/SQLite.
  - **USE COMMAND**: `/test-first when a global to perschema flip cannot read the legacy default-schema history table it should throw a clear configuration error and not silently re-run migrations`
  - Test location: one file per backend —
    - `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/When_per_schema_flip_cannot_read_legacy_history_table_mssql_runner_should_throw_clear_error.cs`
    - `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning/When_per_schema_flip_cannot_read_legacy_history_table_postgres_runner_should_throw_clear_error.cs`
  - Test should verify (real container):
    - Arrange: populate a legacy default-schema history table under elevated creds; create a **restricted** login/role that can write its own schema but has **no SELECT** on `dbo`/`public`
    - Act: provision under `PerSchema` with the restricted credentials
    - Assert: a `ConfigurationException` is thrown whose message names the cause (*read access to the legacy default-schema history table*); the transaction rolls back; **no** migration is silently re-applied and the per-schema table is **not** left as an empty stub that would defeat FR5
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Delivered by the error-hardening in T9/T10 (wrap the legacy read, rethrow `ConfigurationException` with the explicit message). This task pins the behaviour with a test; if T9/T10 already implemented it, this is test-only.
    - Note the infra cost: requires creating a least-privilege login/role in the test (MSSQL `CREATE LOGIN/USER` + `DENY SELECT`; PG `CREATE ROLE` without grant on the legacy schema). Document the credential setup in the test for reproducibility.

### Multi-tenant isolation (FR6, AC4)

- [x] **TEST + IMPLEMENT: Two tenants under PerSchema get independent history tables** — `50b148111`. Two characterisation tests (MSSQL+PG) GREEN on first run; no prod code (T3/T4 `ResolveHistorySchema()` already keys off `SchemaName`). Two tenants share box-table name with distinct `tenant_a_<guid>`/`tenant_b_<guid>` `SchemaName`s under PerSchema; assert each schema's `__BrighterMigrationHistory` has filtered COUNT 1 stamped with own SchemaName; capture B's row count + AppliedAt + MigrationVersion; re-provision A; assert B unchanged. PG schemas folded; PG row-count helper probes existence in separate round-trip (T6 lesson). MSSQL 80/80 + 80/80 net9/net10; PG 73/73 + 73/73 net9/net10.
  - **USE COMMAND**: `/test-first when two tenants with distinct SchemaName values both use PerSchema each should get an independent history table and one tenant's migration should not affect the other`
  - Test location: one file per backend —
    - `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/When_two_mssql_tenants_use_per_schema_scope_each_should_get_independent_history.cs`
    - `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning/When_two_postgres_tenants_use_per_schema_scope_each_should_get_independent_history.cs`
  - Test should verify:
    - Two distinct `SchemaName`s, both `PerSchema`; each gets its own `__BrighterMigrationHistory` in its own schema; provisioning tenant A leaves tenant B's history untouched
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - No new code beyond T3/T4 expected (resolution already keys off `SchemaName`); this characterizes FR6 under per-schema placement. (Compare existing `When_<backend>_runner_runs_two_provisioners_in_distinct_schemas_they_should_not_block_each_other`.)

### NF3 — Identifier safety (AC6)

- [x] **TEST + IMPLEMENT: PerSchema with an unsafe schema identifier is rejected** (commit `c4a40f872`)
  - **USE COMMAND**: `/test-first when PerSchema scope is selected with an unsafe SchemaName the runner should throw and never emit unvalidated identifiers`
  - Test location: one file per backend —
    - `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/When_per_schema_scope_is_selected_with_an_unsafe_schema_name_mssql_runner_should_throw.cs`
    - `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning/When_per_schema_scope_is_selected_with_an_unsafe_schema_name_postgres_runner_should_throw.cs`
  - Test should verify:
    - A `SchemaName` containing an injection/unsafe token under `PerSchema` is rejected by identifier-safety validation; no DDL with the raw identifier is emitted
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - This is a **new** negative assertion on the *schema* identifier (the existing `When_<backend>_migrations_are_built_with_an_unsafe_table_name` analogs validate the *table* name — a different input). Ensure the resolved per-schema identifier flows through `Identifiers.AssertSafe` (MSSQL) / `PgIdentifier` (PG) on **every** new qualified reference added in T3/T4/T9/T10, and that an unsafe `SchemaName` is rejected before any DDL with the raw identifier is emitted. Add validation wherever a per-schema path lacks it; no new injection surface.

### D6 — Observability (NF5, AC7)

- [x] **TEST + IMPLEMENT: Each provisioning run logs the resolved history schema and active scope** (commit `a5bc62d28`)
  - **USE COMMAND**: `/test-first when the migration runner provisions it should log at information level the resolved history schema and the active scope`
  - Test location: `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning`
  - Test file: `When_provisioning_runs_mssql_runner_should_log_resolved_history_schema_and_scope.cs`
  - Test should verify:
    - On a provisioning run, an `Information` log records the resolved history schema (the actual schema, or `<backend default>`) and the scope (`Global`/`PerSchema`)
    - When the D5 seed runs (PerSchema flip), a distinct `Information` log records the copied **row count, legacy schema, and target schema** as separate structured fields (assert against the flip scenario)
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - In the base runner emit `Logger.LogInformation("Box migration history for {BoxTable} resolved to schema {HistorySchema} (scope {Scope})", tableName, ResolveHistorySchema() ?? "<backend default>", scope);` per run (D6).
    - In the D5 seed path emit a distinct `Information` log with **structured fields** (reviewer #3): `Logger.LogInformation("Seeded {RowCount} legacy history row(s) for {BoxTable} from {LegacySchema} to {TargetSchema}", count, tableName, DefaultHistorySchema, Configuration.SchemaName);` and add an OpenTelemetry `Activity` event on the migration span carrying the attribute `brighter.box.migration.seed.rows` (pattern: `MsSqlBoxMigrationRunner.cs:174-175`).

---

## Closeout

- [x] **DOC: XML docs + operator-facing documentation** (commit `991b4440b`)
  - XML-doc the `MigrationHistoryScope` enum + values, the `BoxProvisioningOptions.MigrationHistoryScope` property, and the new `historySchema` interface parameter (semantics: `null` = backend default).
  - Document the negative consequences from ADR 0060: `PerSchema` is a **no-op on MySQL/SQLite/Spanner** (operators should not expect placement; the D6 log shows the resolved default); the `Global → PerSchema` flip must run with **read access to the legacy default-schema history table** (D5 seed); reverse flip and legacy-row cleanup are **out of scope**.
  - Use `<c>Identifiers.AssertSafe</c>` (not a cross-project `cref`) for cross-assembly references; `<see cref="X{T1, T2}"/>` curly-brace generic syntax.

- [ ] **VERIFY: Full regression across all backends**
  - Run the complete box-provisioning suites with infra up (MSSQL net9.0 + net10.0 **separately**; PG; MySQL net9.0 with a prior `-f net9.0` build; SQLite; Spanner after `setup-spanner-emulator.sh`; `BoxProvisioning.Tests`; `Core.Tests/BoxProvisioning`). All green, including every pre-existing regression test (e.g. the non-`dbo`/non-`public` "still create in default" tests). Confirm `Global` paths are byte-for-byte unchanged.

- [ ] **REVIEW: `/spec:review code` → `/spec:approve code`**
  - Adversarial code review against requirements + ADR (every AC1–AC7 traces to a passing test; no default changed; tidy-first ordering honoured). Iterate to PASS, then approve.
