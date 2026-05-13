# Spec 0028 — Functional Requirement Traceability

**Captured:** 2026-05-11 (Phase 12 sign-off)
**HEAD at capture:** `346ae25e7` (`docs: spec 0028 Phase 11 — release notes (AC7), PR description (AC11), ADR 0058 surface verification (AC5)`)
**Branch:** `database_migration`

This file cross-walks each functional requirement F1..F13 from `requirements.md` to the file(s) / class(es) / ADR section(s) that discharge it. F1..F9 belong to the parent spec (captured at Phase 12 sign-off above); F10..F13 belong to sub-phase A (post-acceptance, 2026-05-12 — see "## Sub-phase A" section at the bottom).

## F1 — One ADR with §A and §B sections

**Artefact:** `docs/adr/0058-box-provisioning-rdd-role-interfaces.md`

- Status: **Accepted** (line 5).
- §A "Role-based instance interfaces" — line 55. Contains §A.1 (detection helpers), §A.2 (migration catalogues), §A.3 (payload-mode validators), §A.4 (documentation deliverable).
- §B "Unit-of-work role and template-method runner" — line 230. Contains §B.1 (UoW interface), §B.2 (runner base shape), §B.3 (harmonised lifecycle/cancellation/disposal contract), §B.4 (open-closed sweep).

**`.design-approved`** marker file present in spec directory (per ADR review workflow).

## F2 — `IAmABoxMigrationDetectionHelper<TConnection, TTransaction>` + 5 backend impls

**Interface:**
- `src/Paramore.Brighter.BoxProvisioning/IAmABoxMigrationDetectionHelper.cs` — base interface (5 methods: `DoesTableExistAsync`, `DoesHistoryExistAsync`, `GetMaxVersionAsync`, `GetTableColumnsAsync`, `DiscriminatorFor`).
- `src/Paramore.Brighter.BoxProvisioning/IAmAVersionDetectingMigrationHelper.cs` — extends the base, adds `DetectCurrentVersionAsync` for relational backends.

**Implementations (5):**
| Backend  | Class | Implements |
|---|---|---|
| MSSQL | `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlBoxDetectionHelper.cs` | `IAmAVersionDetectingMigrationHelper<SqlConnection, SqlTransaction>` |
| Postgres | `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlBoxDetectionHelper.cs` | `IAmAVersionDetectingMigrationHelper<NpgsqlConnection, NpgsqlTransaction>` |
| MySQL | `src/Paramore.Brighter.BoxProvisioning.MySql/MySqlBoxDetectionHelper.cs` | `IAmAVersionDetectingMigrationHelper<MySqlConnection, MySqlTransaction>` |
| SQLite | `src/Paramore.Brighter.BoxProvisioning.Sqlite/SqliteBoxDetectionHelper.cs` | `IAmAVersionDetectingMigrationHelper<SqliteConnection, SqliteTransaction>` |
| Spanner | `src/Paramore.Brighter.BoxProvisioning.Spanner/SpannerBoxDetectionHelper.cs` | `IAmABoxMigrationDetectionHelper<SpannerConnection, SpannerTransaction>` (base only — degenerate per ADR 0057 §6) |

ADR section: §A.1 (line 57). Spanner exemption documented at §A.1 backend table line 121.

## F3 — `IAmABoxMigrationCatalog` + 8 backend impls (Spanner exempt)

**Interface:** `src/Paramore.Brighter.BoxProvisioning/IAmABoxMigrationCatalog.cs` — single method `All(IAmARelationalDatabaseConfiguration)`.

**Implementations (8):**
| Backend | Outbox | Inbox |
|---|---|---|
| MSSQL | `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlOutboxMigrationCatalog.cs` | `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlInboxMigrationCatalog.cs` |
| Postgres | `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlOutboxMigrationCatalog.cs` | `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlInboxMigrationCatalog.cs` |
| MySQL | `src/Paramore.Brighter.BoxProvisioning.MySql/MySqlOutboxMigrationCatalog.cs` | `src/Paramore.Brighter.BoxProvisioning.MySql/MySqlInboxMigrationCatalog.cs` |
| SQLite | `src/Paramore.Brighter.BoxProvisioning.Sqlite/SqliteOutboxMigrationCatalog.cs` | `src/Paramore.Brighter.BoxProvisioning.Sqlite/SqliteInboxMigrationCatalog.cs` |
| Spanner | (exempt per ADR 0057 §6 — no V_k chain) | (exempt) |

ADR section: §A.2 (line 146). Spanner exemption explicit at §A.2 line 174.

## F4 — `IAmABoxPayloadModeValidator<TConnection>` + 5 backend impls

**Interface:** `src/Paramore.Brighter.BoxProvisioning/IAmABoxPayloadModeValidator.cs` — single method `ValidateAsync` (single-generic, no `TTransaction`).

**Implementations (5):**
| Backend | Class | Implements |
|---|---|---|
| MSSQL | `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlPayloadModeValidator.cs` | `IAmABoxPayloadModeValidator<SqlConnection>` |
| Postgres | `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlPayloadModeValidator.cs` | `IAmABoxPayloadModeValidator<NpgsqlConnection>` |
| MySQL | `src/Paramore.Brighter.BoxProvisioning.MySql/MySqlPayloadModeValidator.cs` | `IAmABoxPayloadModeValidator<MySqlConnection>` |
| SQLite | `src/Paramore.Brighter.BoxProvisioning.Sqlite/SqlitePayloadModeValidator.cs` | `IAmABoxPayloadModeValidator<SqliteConnection>` |
| Spanner | `src/Paramore.Brighter.BoxProvisioning.Spanner/SpannerPayloadModeValidator.cs` | `IAmABoxPayloadModeValidator<SpannerConnection>` |

Spanner is **not** exempt (ADR §A.3 line 213): although Spanner's payload column is fixed binary, the existing validator already had the same shape (`STARTS_WITH("BYTES" / "STRING")`).

ADR section: §A.3 (line 178).

## F5 — `IAmAProvisioningUnitOfWork<TTransaction>` + 4 relational backend impls

**Interface:** `src/Paramore.Brighter.BoxProvisioning/IAmAProvisioningUnitOfWork.cs` — extends `IAsyncDisposable`. Methods: `Transaction` (property), `BeginAsync`, `CommitAsync`, `RollbackAsync`.

**Implementations (4):**
| Backend | Class | BeginAsync order | Lock release |
|---|---|---|---|
| MSSQL | `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlProvisioningUnitOfWork.cs` | `BeginTransaction → AcquireLock(@LockOwner='Transaction')` | Implicit on commit/rollback |
| Postgres | `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlProvisioningUnitOfWork.cs` | `AcquireLock(pg_advisory_lock) → BeginTransaction` | Explicit `pg_advisory_unlock` then commit/rollback |
| MySQL | `src/Paramore.Brighter.BoxProvisioning.MySql/MySqlProvisioningUnitOfWork.cs` | `AcquireLock(GET_LOCK)` only — no tx (DDL auto-commits) | Explicit `RELEASE_LOCK` |
| SQLite | `src/Paramore.Brighter.BoxProvisioning.Sqlite/SqliteProvisioningUnitOfWork.cs` | `BEGIN IMMEDIATE` (writer-slot reservation IS the lock) | Commit/rollback releases writer slot |

Spanner is exempt (no UoW lifecycle to model — degenerate fresh-install-only per ADR 0057 §6 / §A.2 / §B.1).

ADR section: §B.1 (line 232). Per-backend ordering table at line 281.

## F6 — `SqlBoxMigrationRunner<TConnection, TTransaction>` + 4 derived runners

**Abstract base:** `src/Paramore.Brighter.BoxProvisioning/SqlBoxMigrationRunner.cs` — implements `IAmABoxMigrationRunner`. Owns `MigrateAsync` algorithm; exposes `protected DetectionHelper`, `protected Configuration`, `protected Logger`.

**Derived classes (4 relational; Spanner free-standing):**
| Backend | Class | Base parameterisation |
|---|---|---|
| MSSQL | `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlBoxMigrationRunner.cs` | `SqlBoxMigrationRunner<SqlConnection, SqlTransaction>` |
| Postgres | `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlBoxMigrationRunner.cs` | `SqlBoxMigrationRunner<NpgsqlConnection, NpgsqlTransaction>` |
| MySQL | `src/Paramore.Brighter.BoxProvisioning.MySql/MySqlBoxMigrationRunner.cs` | `SqlBoxMigrationRunner<MySqlConnection, MySqlTransaction>` |
| SQLite | `src/Paramore.Brighter.BoxProvisioning.Sqlite/SqliteBoxMigrationRunner.cs` | `SqlBoxMigrationRunner<SqliteConnection, SqliteTransaction>` |
| Spanner | `src/Paramore.Brighter.BoxProvisioning.Spanner/SpannerBoxMigrationRunner.cs` | Implements `IAmABoxMigrationRunner` directly — exempt per ADR 0057 §6 |

ADR section: §B.2 (line 296). Hooks enumerated at lines 386-425.

## F7 — Harmonised UoW lifecycle / cancellation / disposal contract per relational backend

**Contract:** ADR §B.3 (line 442) — table of seven concerns: order of operations (success/failure), `BeginAsync` throws, `CommitAsync` throws, cancellation (3-window), rollback contract, disposal contract, logger plumbing, logging diagnostics.

**Tests pinning the contract per relational backend (Phase 6 + Phase 10):**
| Concern | MSSQL | Postgres | MySQL | SQLite |
|---|---|---|---|---|
| Mid-flight cancellation → `RollbackAsync(CancellationToken.None)` | `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/When_mssql_migration_is_cancelled_mid_flight_it_should_rollback_with_cancellation_token_none.cs` (Phase 10.1, sha `683e42b4d`) | `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning/When_postgres_migration_is_cancelled_mid_flight_it_should_rollback_and_release_session_lock.cs` (Phase 10.1, sha `f6eca9869`) | `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning/When_mysql_migration_is_cancelled_mid_flight_it_should_release_get_lock.cs` (Phase 10.1, sha `d3219195c`) | `tests/Paramore.Brighter.Sqlite.Tests/BoxProvisioning/When_sqlite_migration_is_cancelled_mid_flight_it_should_rollback_releasing_writer_slot.cs` (Phase 10.1, sha `b125f2f11`) |
| `BeginAsync` throws → no Commit, no Rollback, yes Dispose | `tests/Paramore.Brighter.BoxProvisioning.Tests/When_relational_box_migration_runner_base_begin_async_throws_it_should_skip_commit_and_rollback_and_still_dispose.cs` (Phase 6 cross-backend, base test) + `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/When_mssql_advisory_lock_acquire_throws_during_begin_async_runner_should_not_call_commit_or_rollback.cs` (Phase 10.2, sha `76705d2b0`) | (Phase 6 cross-backend test covers this contract via the base; per-backend pin not required) | (Phase 6 cross-backend) | (Phase 6 cross-backend) |
| `CommitAsync` throws → best-effort `RollbackAsync` against zombied tx (no throw; Warning logged; original commit exception propagates) | `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/When_mssql_commit_throws_rollback_should_be_best_effort_without_throwing.cs` (Phase 10.3, sha `09d600300`) | (covered by §B.3 contract; MSSQL is the canonical zombied-tx case) | (no transaction; not applicable) | (transaction owned by writer slot — best-effort behaviour intrinsic) |

Phase 6 base test pins the cross-backend contract; Phase 10 tests pin per-backend specifics for the cancellation/lock-release diagnostics that vary by lock primitive.

## F8 — "Adding a new BoxProvisioning backend" section in ADR 0058

**Section:** `docs/adr/0058-box-provisioning-rdd-role-interfaces.md` line 475 ("## Adding a new BoxProvisioning backend"). Seven numbered steps:

1. Detection helper (F2) — `{Backend}BoxDetectionHelper : IAmAVersionDetectingMigrationHelper<,>` (or base interface for degenerate backends).
2. Migration catalogues (F3) — `{Backend}OutboxMigrationCatalog`, `{Backend}InboxMigrationCatalog : IAmABoxMigrationCatalog`.
3. Payload-mode validator (F4) — `{Backend}PayloadModeValidator : IAmABoxPayloadModeValidator<TConnection>`.
4. Advisory lock primitive (where applicable) — `I{Backend}AdvisoryLock` per ADR 0057 §5b.
5. Provisioning UoW (F5) — `{Backend}ProvisioningUnitOfWork : IAmAProvisioningUnitOfWork<TTransaction>`.
6. Provisioners (existing role from ADR 0053) — ctor cascade absorbs three new typed parameters (detection helper, catalogue, payload validator).
7. Migration runner (F6) — `{Backend}BoxMigrationRunner : SqlBoxMigrationRunner<TConnection, TTransaction>` (or implements `IAmABoxMigrationRunner` directly for degenerate backends — Spanner pattern).

Phase 11.3 verified all 27 referenced class names match shipped surface (no drift).

## F9 — Open-closed sweep

**ADR section:** §B.4 (line 460). Four candidates surveyed; all four "No" decisions hold post-implementation. No new candidates surfaced during implementation.

**Sweep result:** see `sweep-result.md` in this directory.

---

## Sub-phase A (post-acceptance, 2026-05-12) — F10..F13

**Captured:** 2026-05-13 (Phase 13.C.2)
**Sub-phase A range:** `246ea6f13` (`docs: ADR 0058 sub-phase A — §B.5 SqlBoxProvisioner pull-up`) .. `31d84d18d` (`feat: spec 0028 sub-phase A 13.B`).

ADR amendment: `docs/adr/0058-box-provisioning-rdd-role-interfaces.md` §B.5 ("SqlBoxProvisioner pull-up", appended in `246ea6f13`); §B.4 amended same commit with Candidate 5 row + forward link to §B.5 (F13).

## F10 — `SqlBoxProvisioner<TConnection, TTransaction>` + 8 relational derivations

**Abstract base:** `src/Paramore.Brighter.BoxProvisioning/SqlBoxProvisioner.cs` — `public abstract class SqlBoxProvisioner<TConnection, TTransaction> : IAmABoxProvisioner` with `where TConnection : DbConnection where TTransaction : DbTransaction`. Owns `ProvisionAsync` (sealed orchestration), private `DetectTableStateAsync` (with inlined negative-version clamp post-13.B), private `ValidatePayloadModeAsync`. Three hooks: `CreateConnection` (abstract), `PayloadColumnName` (abstract), `EffectiveSchemaName` (virtual; default = `_configuration.SchemaName`).

**Derived classes (8 relational; Spanner free-standing):**
| Backend | Outbox | Inbox |
|---|---|---|
| MSSQL | `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlOutboxProvisioner.cs` | `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlInboxProvisioner.cs` |
| Postgres | `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlOutboxProvisioner.cs` | `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlInboxProvisioner.cs` |
| MySQL | `src/Paramore.Brighter.BoxProvisioning.MySql/MySqlOutboxProvisioner.cs` | `src/Paramore.Brighter.BoxProvisioning.MySql/MySqlInboxProvisioner.cs` |
| SQLite | `src/Paramore.Brighter.BoxProvisioning.Sqlite/SqliteOutboxProvisioner.cs` | `src/Paramore.Brighter.BoxProvisioning.Sqlite/SqliteInboxProvisioner.cs` |
| Spanner | `src/Paramore.Brighter.BoxProvisioning.Spanner/SpannerOutboxProvisioner.cs` (free-standing — `IAmABoxProvisioner` direct) | `src/Paramore.Brighter.BoxProvisioning.Spanner/SpannerInboxProvisioner.cs` (free-standing) |

Spanner exemption per ADR 0057 §6 — same shape as `SqlBoxMigrationRunner` (F6). Verified locally: `grep -l 'SqlBoxProvisioner' src/Paramore.Brighter.BoxProvisioning.Spanner/ -r` returns zero matches.

**Commits introducing the base and the eight derivations:**
- `70f92df44` — slice 1 (base + `ProvisionAsync` orchestration; hardcoded schema; no clamp hook).
- `1cc5e009b` — slice 2 (`EffectiveSchemaName` virtual hook).
- `4e271d861` — slice 3 (transitional `ClampDetectedVersion` virtual hook; removed in 13.B per F11).
- `f0de8b62b` / `de5516765` — 13.A.2 MsSql{Outbox,Inbox}Provisioner derive.
- `edffcf8bf` / `6ce460174` — 13.A.3 PostgreSql{Outbox,Inbox}Provisioner derive.
- `f76ef8c39` / `971a8fa38` — 13.A.4 MySql{Outbox,Inbox}Provisioner derive (transitional clamp override; removed in 13.B).
- `be70aa7ff` / `7965eae4d` — 13.A.5 Sqlite{Outbox,Inbox}Provisioner derive (permanent `EffectiveSchemaName => null` override).
- `42a35ce3c` — 13.A.7 Phase 13.A gate.

ADR section: §B.5 (added in `246ea6f13`). NF10 source-break neutrality preserved — each derivation keeps both ctors (5-arg canonical + 2-arg back-compat), both delegate to `base(...)`.

## F10.1 — Hook surface (five variance deltas)

**ADR section:** §B.5 hook table (added in `246ea6f13`; transitional `ClampDetectedVersion` row crossed out post-13.B in `31d84d18d`).

| Delta (req F10.1) | Hook on base | Per-backend overrides post-13.B |
|---|---|---|
| a. Connection factory | `protected abstract TConnection CreateConnection();` | All 8 derivations override (no default — `new {Backend}Connection(connectionString)`). |
| b. Payload column name | `protected abstract string PayloadColumnName { get; }` | All 8 derivations override (one per (backend, box-type) — `"Body"` / `"body"` / `"CommandBody"` / `"commandbody"`). |
| c. Schema-name to detection helper + payload validator | `protected virtual string? EffectiveSchemaName => _configuration.SchemaName;` | SQLite Outbox + Inbox override to `=> null` (per ADR 0057 §6 — SQLite has no schema concept). MSSQL/PG/MySQL inherit default. |
| d. Negative-version clamp | **Inlined post-13.B** at `DetectTableStateAsync` bootstrap branch (`CurrentVersion: detectedVersion < 0 ? 0 : detectedVersion`). The transitional `protected virtual int ClampDetectedVersion(int)` hook (added in slice 3 `4e271d861`) was removed in `31d84d18d` along with the MySQL identity overrides. | None — uniform behaviour. |
| e. Disposal pattern | Sync `using` for the connection per §B.2 precedent (`SqlBoxMigrationRunner.cs:112-116`) — no hook required (uniform across all four derivations; `DbConnection` lacks `IAsyncDisposable` on netstandard2.0). | N/A — F12 disposition. |

**Base-contract tests (`tests/Paramore.Brighter.BoxProvisioning.Tests/`):**
- `When_sql_box_provisioner_provision_async_runs_successfully_it_should_invoke_hooks_in_documented_order.cs` — 3 `[Fact]`s (slice 1 `70f92df44`); pins delta (a) via `RecordingFakeDbConnection`.
- `When_sql_box_provisioner_effective_schema_name_is_overridden_it_should_propagate_to_detection_and_payload_calls_only.cs` — 2 `[Fact]`s (slice 2 `1cc5e009b`); pins delta (c).
- `When_sql_box_provisioner_detect_table_state_inlines_negative_version_clamp.cs` — 2 `[Fact]`s (renamed + trimmed in `31d84d18d` from the slice 3 file; pre-13.B was 3 `[Fact]`s incl. override-identity); pins delta (d) by data-flow through `DetectTableStateAsync` bootstrap branch.

## F11 — Unified MySQL pre-lock negative-version clamp

**ADR section:** §B.5 line 646 (mandate to remove the transitional hook + MySQL overrides in 13.B per "no half-finished implementations").

**Behavioural test (single commit per CLAUDE.md):**
- `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning/When_mysql_pre_lock_detects_negative_version_it_should_clamp_to_zero.cs` — 2 `[Fact]`s (one per `MySqlOutboxProvisioner` / `MySqlInboxProvisioner`). Uses stub `IAmAVersionDetectingMigrationHelper<MySqlConnection, MySqlTransaction>` returning `-1` + spy `IAmABoxMigrationRunner` capturing `BoxTableState`; asserts captured `CurrentVersion == 0`.

**Commit:** `31d84d18d` (`feat: spec 0028 sub-phase A 13.B — unify MySQL pre-lock clamp behaviour with MsSql/Postgres/Sqlite (remove transitional hook + overrides; inline clamp; rename + trim base-contract clamp test; reconcile pre-existing MySQL+SQLite floor drift)`). The MySQL test pinned the unified behaviour green; the base-contract `When_sql_box_provisioner_detect_table_state_inlines_negative_version_clamp.cs` file pins the inlined clamp at the documented call-site by data-flow. Two-level coverage: base unit + backend integration.

**Sibling pins (pre-existing, unchanged):** MSSQL / PG / SQLite `When_*_pre_lock_detects_negative_version_*` files in each backend's `BoxProvisioning/` test suite already pinned the clamp before sub-phase A — F11 brings MySQL into line.

## F12 — Disposal pattern (sync `using` per §B.2 precedent)

**Disposition:** sync `using` for the connection on `SqlBoxProvisioner` — **no independent probe required**. Discharged by §B.2 precedent: `SqlBoxMigrationRunner.cs:112-116` already encodes the same decision (`DbConnection` does not implement `IAsyncDisposable` on netstandard2.0, so a base-class `await using` over `TConnection : DbConnection` would not compile across the shared-assembly TFM matrix `netstandard2.0;net8.0;net9.0;net10.0`).

**Where recorded:** `specs/0028-box-provisioning-rdd-role-interfaces/baseline.md` → "Sub-phase A preliminaries" → F12 disposition table. ADR 0058 §B.5 inherits the same decision (no §B.5 disposal sub-section beyond a cross-reference to §B.2).

**Limiting factor:** the **base type `DbConnection`** on netstandard2.0, not the four driver subtypes (`SqlConnection`, `NpgsqlConnection`, `MySqlConnection`, `SqliteConnection`). If a future TFM bump drops netstandard2.0 from the shared assembly, the disposition is revisited in a follow-up spec.

**Precedent-discharged per round-2 review** of ADR 0058 §B.5 (see `review-tasks.md` round-2 entry).

## F13 — ADR §B.4 amendment + forward link to §B.5

**ADR amendment:** `docs/adr/0058-box-provisioning-rdd-role-interfaces.md` §B.4 — single-row table addition for Candidate 5 (`SqlBoxProvisioner` pull-up) with a forward link to §B.5. Added in commit `246ea6f13` (`docs: ADR 0058 sub-phase A — §B.5 SqlBoxProvisioner pull-up`) — same commit that authored §B.5. The four original candidate verdicts (1–4) remain unaltered.

**§B.4 amendment scope:** the original §B.4 closed with "no further candidates"; the post-implementation discovery of the 8-provisioner duplication (~640 lines of body across the four relational backends × Outbox/Inbox) re-opened that verdict. The §B.4 row is a single-line addition; the design lives at §B.5.

**Forward link:** §B.4 Candidate 5 row → "See §B.5 — SqlBoxProvisioner pull-up".
