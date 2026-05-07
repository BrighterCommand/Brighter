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
