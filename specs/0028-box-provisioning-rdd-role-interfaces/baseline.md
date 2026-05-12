# Spec 0028 Phase 0 Baseline

**Captured:** 2026-05-07
**HEAD at capture:** `cb3a5ad56` (`docs: spec 0028 Box Provisioning RDD — tasks approved (round 2 PASS)`)
**Branch:** `database_migration`

This file is the spec 0028 NF2 floor. Every subsequent phase gate compares against the per-backend / per-TFM counts recorded here.

## Per-backend test counts (BoxProvisioning filter)

Command form: `dotnet test <project> --filter "FullyQualifiedName~BoxProvisioning" -c Release --no-build`

| Backend | Test project | net9.0 | net10.0 |
|---|---|---|---|
| Core (BoxProvisioning library) | `tests/Paramore.Brighter.BoxProvisioning.Tests` | 23/23 | 23/23 |
| Core (BoxProvisioning sub-filter in Brighter.Core.Tests) | `tests/Paramore.Brighter.Core.Tests` | 5/5 | 5/5 |
| MSSQL | `tests/Paramore.Brighter.MSSQL.Tests` | 54/54 | 54/54 |
| Postgres | `tests/Paramore.Brighter.PostgresSQL.Tests` | 46/46 | 46/46 |
| MySQL (net9.0-only TFM) | `tests/Paramore.Brighter.MySQL.Tests` | 50/50 | n/a |
| SQLite | `tests/Paramore.Brighter.Sqlite.Tests` | 40/40 | 40/40 |
| Spanner | `tests/Paramore.Brighter.Gcp.Tests` (BoxProvisioning sub-filter) | 26/26 | 26/26 |

These counts equal the NF2 anchor in `requirements.md` at HEAD `edfa9fc99`. No additive drift on `database_migration` between `edfa9fc99` and `cb3a5ad56`. NF2 enumeration is unchanged — no co-update committed alongside this baseline.

## Phase-gate comparison rule

After each phase, re-run the same per-backend filter and confirm counts equal or exceed the rows above. Counts **must not decrease**. New tests landing as part of spec 0028 (e.g. UoW lifecycle tests under Phase 5, runner-base contract tests under Phase 6) raise the floor for subsequent phases — the phase commit message must quote the new count.

## TFM-matrix build outcome

`dotnet build` succeeds clean (0 warnings, 0 errors) on:

- `src/Paramore.Brighter.BoxProvisioning/Paramore.Brighter.BoxProvisioning.csproj` against `BrighterTargetFrameworks` = `netstandard2.0;net8.0;net9.0;net10.0`.
- `src/Paramore.Brighter.BoxProvisioning.MsSql/Paramore.Brighter.BoxProvisioning.MsSql.csproj` against `BrighterFrameworkAndCoreTargetFrameworks` = `net462;net8.0;net9.0;net10.0`.

This confirms the matrix that Phase 1's role-interface declarations must respect (per C7).

## Test-runner notes (for future phase gates)

- **MSSQL parallel-TFM contention**: running net9.0 + net10.0 against the same `brighter-sqlserver-1` container in one `dotnet test` invocation reproducibly fails 1 test per TFM (deadlock victim on `When_mssql_inbox_provisioner_detects_payload_mode_mismatch_it_should_throw`; missing `dbo.__BrighterMigrationHistory` on `When_mssql_outbox_table_is_bootstrapped_at_vk_it_should_upgrade_to_v7 (k:7)`). This is a runner-orchestration artifact, not a test-quality issue. Each TFM passes 54/54 cleanly when run in isolation (`-f net9.0` then `-f net10.0`). **Phase gates must invoke the MSSQL filter once per TFM rather than letting the multi-target runner overlap them.**
- **Spanner emulator env vars required**: `SPANNER_EMULATOR_HOST=localhost:9010` + `GOOGLE_CLOUD_PROJECT=brighter-tests` must be set in the shell that invokes `dotnet test` for the Gcp BoxProvisioning filter. Without them, `GatewayFactory.GetProjectId()` returns empty and 20/26 tests fail with `'projects//instances/...' is not a valid value for $DataSource`. Run `bash setup-spanner-emulator.sh` (or set the two env vars manually) before each Spanner phase gate.
- **Container readiness**: Postgres, MySQL, Spanner emulator were already running for >30h at capture time. MSSQL (`brighter-sqlserver-1` / `mcr.microsoft.com/azure-sql-edge`) was started fresh during Phase 0 via `docker-compose -f docker-compose-mssql.yaml up -d`. SQLite + the two Core suites are in-process and need no infrastructure.

## Sub-phase A preliminaries

**Captured:** 2026-05-12 (post-Phase-12 acceptance, before Phase 13.A implementation).

### F12 disposition (disposal pattern for `SqlBoxProvisioner`)

Requirement F12 originally required verifying `IAsyncDisposable` support on the four backend `DbConnection` subtypes across the C6 TFM matrix before standardising the base on `await using`. **No independent probe was run** — the round-2 adversarial review of ADR 0058 §B.5 surfaced that the verification's outcome is already known from the sibling abstract base in the same shared assembly.

**Disposition** (cited under requirement F12 and ADR §B.5 line 647 rationale bullet "Sync `using` for the connection — mirroring the §B.2 precedent"):

| Backend / `DbConnection` subtype | TFM | `IAsyncDisposable` available? | Disposal pattern in `SqlBoxProvisioner` |
|---|---|---|---|
| All four (`SqlConnection` / `NpgsqlConnection` / `MySqlConnection` / `SqliteConnection`) | netstandard2.0 (shared assembly) | **No** — `DbConnection` base class does not implement `IAsyncDisposable` on netstandard2.0; subtype implementations are immaterial because `SqlBoxProvisioner` declares `using` over `TConnection : DbConnection` (the constrained type, not the runtime subtype) | sync `using` |
| All four | net8.0 / net9.0 / net10.0 | Yes (BCL `DbConnection` implements `IAsyncDisposable` from netstandard2.1+) | sync `using` (uniform with netstandard2.0 — the base cannot vary per TFM without per-TFM source which violates C6 / NF11) |

**Operative reason**: `DbConnection` is the constrained base type in `SqlBoxProvisioner<TConnection, TTransaction> where TConnection : DbConnection`. On netstandard2.0 (in the shared assembly's TFM list), `DbConnection` does not implement `IAsyncDisposable`. A `using` statement bound to the constrained type therefore compiles only as sync `using` across all four target frameworks. Per-derived-class `await using` is possible only outside the base — and only in derived assemblies that drop netstandard2.0 (e.g. `Paramore.Brighter.BoxProvisioning.PostgreSql` which targets `$(BrighterCoreTargetFrameworks)` = `net8.0;net9.0;net10.0`). The base does not have that latitude.

**Precedent reference**: `src/Paramore.Brighter.BoxProvisioning/RelationalBoxMigrationRunnerBase.cs:112-116` — the §B.2 sibling abstract base in the same shared assembly encodes the same disposition with the same reasoning. `SqlBoxProvisioner` mirrors the decision verbatim. The two abstract bases now share a uniform disposal-pattern policy across the shared `Paramore.Brighter.BoxProvisioning` assembly.

**Re-litigation trigger**: if a future TFM bump drops netstandard2.0 from `BrighterTargetFrameworks` (`src/Directory.Build.props:43`), the disposal pattern can be revisited in a follow-up spec — `DbConnection` implements `IAsyncDisposable` from netstandard2.1+ via the BCL.

### NF9 floor for Phase 13.A

The Phase 13.A structural pull-up commit must preserve every backend's BoxProvisioning test count at the **post-Phase-10 numbers in `acceptance.md` AC6**:

| Backend | Test count to preserve (per TFM) |
|---|---|
| Core (BoxProvisioning library) | 43/43 [^core-base-contract] |
| Core (BoxProvisioning sub-filter in Brighter.Core.Tests) | 5/5 [^core-sub-filter] |
| MSSQL | 63/63 |
| Postgres | 54/54 |
| MySQL (net9.0-only) | 67/67 |
| SQLite | 46/46 |
| Spanner | 26/26 |

[^core-base-contract]: Post-Phase-13.B floor (sub-phase A complete). The Phase 6 precedent legitimises adding base-contract tests alongside an abstract base (`RelationalBoxMigrationRunnerBase` shipped with six base-contract test files at Phase 6 — same precedent). Phase 13.A.1 added 8 `[Fact]` methods across three files (3 + 2 + 3) at `tests/Paramore.Brighter.BoxProvisioning.Tests/`: orchestration-order (3), `EffectiveSchemaName` propagation (2), and transitional `ClampDetectedVersion` (3). The 36 → 44 raise was authorised by the 13.A.0.5 NF9 amendment (carve-out wording added). Phase 13.B then dropped Core to 43/43 (-1 deleted override-identity `[Fact]` when the transitional hook was removed) and raised MySQL to 67/67 (+2 unification `[Fact]`s + 4 pre-existing drift from three post-Phase-10.4 fix commits — `ba8813e6f` lock-timeout harmonisation +1, `a8e99e1c4` negative-TimeSpan rejection +1, `03bdd7455` overflowing-TimeSpan rejection +2 net; same drift pattern as SQLite, reconciled here in lock-step). SQLite's recorded floor was reconciled at 13.B from 45/45 to 46/46 — pre-existing +1 drift from commit `b14d76592` (default lock-timeout pin) that landed between Phase 12 acceptance and sub-phase A entry.

[^core-sub-filter]: Unchanged at 5/5 — Phase 13.A.1's new test cases live in `tests/Paramore.Brighter.BoxProvisioning.Tests/`, NOT in `tests/Paramore.Brighter.Core.Tests/` (the sub-filter project). The Core sub-filter is the BoxProvisioning slice of the Core test project; the new base-contract `[Fact]` methods belong to the standalone BoxProvisioning test project per the Phase 6 sibling-base convention.

Phase 13.A.1 introduces 8 base-contract `[Fact]` methods at the abstract-base level per the Phase 6 precedent (see NF9 carve-out in requirements.md); per-backend ports 13.A.2–13.A.5 introduce zero new tests (per NF9 strict). Phase 13.B introduced the new F11 `/test-first` clamp-behaviour test in `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning/` (counts against MySQL net9.0 only) and rewrote the slice-3 base-contract clamp test (deleted the override-identity `[Fact]` since the transitional hook disappeared); the new floor is recorded in `requirements.md` NF9 (parenthetical), this section, and `acceptance.md` AC6 in the same commit. **NF2 is the Phase-0 baseline anchor and remains immutable**; only NF9 (and the AC6 / baseline.md tables it points to) move.
