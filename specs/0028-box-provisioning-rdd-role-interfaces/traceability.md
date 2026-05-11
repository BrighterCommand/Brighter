# Spec 0028 — Functional Requirement Traceability

**Captured:** 2026-05-11 (Phase 12 sign-off)
**HEAD at capture:** `346ae25e7` (`docs: spec 0028 Phase 11 — release notes (AC7), PR description (AC11), ADR 0058 surface verification (AC5)`)
**Branch:** `database_migration`

This file cross-walks each functional requirement F1..F9 from `requirements.md` to the file(s) / class(es) / ADR section(s) that discharge it.

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

## F6 — `RelationalBoxMigrationRunnerBase<TConnection, TTransaction>` + 4 derived runners

**Abstract base:** `src/Paramore.Brighter.BoxProvisioning/RelationalBoxMigrationRunnerBase.cs` — implements `IAmABoxMigrationRunner`. Owns `MigrateAsync` algorithm; exposes `protected DetectionHelper`, `protected Configuration`, `protected Logger`.

**Derived classes (4 relational; Spanner free-standing):**
| Backend | Class | Base parameterisation |
|---|---|---|
| MSSQL | `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlBoxMigrationRunner.cs` | `RelationalBoxMigrationRunnerBase<SqlConnection, SqlTransaction>` |
| Postgres | `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlBoxMigrationRunner.cs` | `RelationalBoxMigrationRunnerBase<NpgsqlConnection, NpgsqlTransaction>` |
| MySQL | `src/Paramore.Brighter.BoxProvisioning.MySql/MySqlBoxMigrationRunner.cs` | `RelationalBoxMigrationRunnerBase<MySqlConnection, MySqlTransaction>` |
| SQLite | `src/Paramore.Brighter.BoxProvisioning.Sqlite/SqliteBoxMigrationRunner.cs` | `RelationalBoxMigrationRunnerBase<SqliteConnection, SqliteTransaction>` |
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
7. Migration runner (F6) — `{Backend}BoxMigrationRunner : RelationalBoxMigrationRunnerBase<TConnection, TTransaction>` (or implements `IAmABoxMigrationRunner` directly for degenerate backends — Spanner pattern).

Phase 11.3 verified all 27 referenced class names match shipped surface (no drift).

## F9 — Open-closed sweep

**ADR section:** §B.4 (line 460). Four candidates surveyed; all four "No" decisions hold post-implementation. No new candidates surfaced during implementation.

**Sweep result:** see `sweep-result.md` in this directory.
