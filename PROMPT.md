# Current State — Spec 0023: Box Database Migration

## Branch
`database_migration` (2 commits ahead of `origin/database_migration`)

## Completed Phases
- **Phase 1**: Core Abstractions (`Paramore.Brighter.BoxProvisioning`)
- **Phase 2**: MSSQL Backend
- **Phase 3**: PostgreSQL Backend
- **Phase 4**: MySQL Backend
- **Phase 5**: SQLite Backend
- **Phase 6**: Spanner Backend
- **Phase 7**: WebAPI sample migration to `UseBoxProvisioning()`

## Latest commit — `43de453c3` (this session)

**fix: restore DetectCurrentVersionAsync with column introspection**

- **Restored `DetectCurrentVersionAsync`** across all 10 provisioners. Previous simplification returned `Task.FromResult(1)`, losing the introspection that distinguishes Brighter tables from unrelated tables in the bootstrap path. Each outbox provisioner now has a `V1Columns` HashSet + `GetTableColumnsAsync` helper; inbox provisioners reuse the outbox helper with their own V1 set. Detection is by **column name only** (types/nullability not checked — acceptable since 22+ specific column names are extremely unlikely to collide).
- **Added 6 dedicated bootstrap integration tests** (one per backend × box type where missing): SQLite outbox, MSSQL inbox, PostgreSQL inbox, MySQL outbox, Spanner outbox, Spanner inbox. Each creates the box table directly via `*Builder.GetDDL()` (no migration history), invokes the provisioner, and asserts a synthetic V1 history row is inserted. `DetectCurrentVersionAsync` stays internal.
- **Spanner history table reverted to `BrighterMigrationHistory`** (no underscores). The new Spanner tests caught that Spanner GoogleSQL rejects identifiers starting with `_`. Other backends keep `__BrighterMigrationHistory`. `.agent_instructions/box_provisioning.md` documents the Spanner exception.

### Verification (this session)
- All 5 BoxProvisioning packages build with 0 warnings, 0 errors (net462 / net8 / net9 / net10)
- SQLite BoxProvisioning tests (3 existing + 1 new bootstrap): pass
- Core BoxProvisioning tests (5): pass
- MSSQL / PostgreSQL / MySQL bootstrap tests: pass against Docker containers
- Spanner (2 existing fresh + 2 new bootstrap): pass against emulator

## Next up — adversarial code review findings

**Source**: `specs/0023-box_database_migration/review-code.md` (produced by `/spec:review code` on 2026-04-21 against `master` at `88ad8729`). Verdict: **NEEDS WORK**, 7 findings ≥ threshold 60.

Priority order (fix top-down; verify the finding is real *before* changing code — findings are adversarial claims, not established bugs):

1. **R1 [85] — V1Columns bootstrap breaks pre-DataRef/SpecVersion upgrades**. Every `DetectCurrentVersionAsync` across the 10 provisioners uses `actualColumns.IsSupersetOf(V1Columns)` where `V1Columns` is the *current* full column list. For tables created before `DataRef`/`SpecVersion` were added, this returns 0 → runner tries to apply V1 via `CREATE TABLE` (MSSQL has no `IF NOT EXISTS`, so it throws). This contradicts ADR 0053 §7 "safe fallback is version 1" and NFR-1. **Verify first** — read the actual `V1Columns` sets vs. the builder DDL across backends; PROMPT.md lines 47-52 document the column sets but not when DataRef/SpecVersion were added historically. If confirmed, fix by treating DataRef/SpecVersion as V2 columns (minimal V1 set as the bootstrap match target). Add a test that seeds a pre-DataRef table and verifies bootstrap at V1.

2. **R2 [78] — TOCTOU race in bootstrap path (all backends)**. `DetectTableStateAsync` runs outside the advisory lock; `InsertSyntheticHistoryAsync` / `BootstrapExistingTableAsync` has no `IsMigrationAppliedAsync` guard (unlike the regular migration loop). Two concurrent instances with stale `(true, false, 1)` state will PK-violate on `__BrighterMigrationHistory`. Existing concurrency tests only cover fresh-install race. Fix: re-check applied inside the lock before synthetic insert, or use `INSERT OR IGNORE` / `ON CONFLICT DO NOTHING` / `WHERE NOT EXISTS` per backend. Add a concurrent-bootstrap test (seed via builder, race two provisioners).

3. **R3 [72] — `SchemaName` shipped as plain abstract member, not default interface member**. ADR 0053 §10 and tasks.md 0.1 both require the default-interface-member form, but `Paramore.Brighter` targets `netstandard2.0` which can't support DIMs — making the ADR literally unimplementable. The actual member at `src/Paramore.Brighter/IAmARelationalDatabaseConfiguration.cs:45-48` is plain abstract, so external implementors break at recompile. Fix options: (a) update ADR 0053 §10 and tasks.md 0.1 to document the DIM approach is not viable and accept the breaking change, add release notes; or (b) move `SchemaName` to a separate optional interface (e.g. `IAmASchemaQualifiedConfiguration`) the provisioners detect at runtime.

4. **R4 [70] — Spanner history INSERT unprotected from concurrent PK violation**. `SpannerBoxMigrationRunner` has no lock and no `AlreadyExists` catch on the history INSERT. `SpannerBoxProvisioningCollection` disables parallelization, hiding this in tests. Fix: wrap INSERT in `WHERE NOT EXISTS`, or call `IsMigrationAppliedAsync` immediately before insert, or catch Spanner `AlreadyExists` on the INSERT.

5. **R5 [68] — Payload-mode-mismatch tests only exist for MSSQL**. `PostgreSqlPayloadModeValidator`, `MySqlPayloadModeValidator`, `SqlitePayloadModeValidator`, `SpannerPayloadModeValidator` all have non-trivial dialect-specific logic but no tests. Add one mismatch test per backend following the MSSQL pattern (`When_mssql_outbox_provisioner_detects_payload_mode_mismatch_it_should_throw.cs`).

6. **R6 [62] — MSSQL `sp_getapplock` has parameter-name collisions**. `MsSqlBoxMigrationRunner.cs:100-109` uses `@Resource = @Resource` and `@LockTimeout = @LockTimeout` where the same name is both the SP parameter (LHS) and the SqlCommand placeholder (RHS). Works today but fragile. Rename SqlCommand placeholders to `@lockResourceName` / `@lockTimeoutMs`.

7. ~~**R7 [60] — Uncommitted changes at review time**~~ (resolved by the commit that includes this PROMPT.md update; the review-skill change is intentionally staying on this branch).

### Below-threshold findings (defer)

- **[55]** MySQL concurrency test class named `ConcurrentProvisionerTests` while MSSQL/PostgreSQL equivalents match the filename. Pick one convention and apply across the feature.
- **[50]** `MsSqlBoxMigrationRunner.EnsureHistoryTableAsync` checks `sys.tables` by name only (no `schema_id` filter) — corner-case issue for users who have manually created `other_schema.__BrighterMigrationHistory`.
- **[45]** Each provisioner opens three connections per run (detect, validate, migrate); ADR §5 implies a single connection. Correctness-neutral; document or consolidate.

### Older cleanup items (lower priority than R1–R6)

1. **Q1** — Extract shared schema inspection helpers per backend (inbox provisioners currently call `internal static` methods on outbox provisioners; should be standalone helpers — the `GetTableColumnsAsync` added in the latest commit is the main candidate)
2. **Q2** — Standardize constructor style (MSSQL/PostgreSQL use traditional, others use primary constructors)
3. **Q3** — Deduplicate `StubBrighterBuilder` (exists in both Core.Tests and MSSQL.Tests)

## Phase 7 — completed this session

- `samples/WebAPI/WebAPI_EFCore/SalutationAnalytics/Program.cs` — removed stale `host.CreateInbox`/`host.CreateOutbox` calls (extension methods had already been deleted in a6ed373e2 but master-merge restored the call sites), added `using Paramore.Brighter.BoxProvisioning;`, chained `.UseBoxProvisioning(options => { BoxProvisioningFactory.AddOutbox(...); BoxProvisioningFactory.AddInbox(...); })` onto Brighter configuration, removed now-unused `HasBinaryMessagePayload()` local function.
- All 9 WebAPI sample projects build cleanly (0 errors). `DbMaker.csproj` already transitively exposes the `Paramore.Brighter.BoxProvisioning.{MsSql,MySql,PostgreSql,Sqlite}` packages so no csproj change was needed.
- `SchemaCreation.cs` retains only app-database creation (Greetings/Salutations) and `MigrateDatabase`/`CheckDbIsUp`.
- Dynamo samples intentionally unchanged — BoxProvisioning is relational-only; DynamoDb inbox/outbox creation stays in `InboxFactory.CreateInbox<T>()` / `OutboxFactory.MakeDynamoOutbox`.

## Reference

### V1 Column Sets (used by `DetectCurrentVersionAsync`)
- **Outbox MSSQL (23)** / **PostgreSQL (23, lowercase)**: Id, MessageId, Topic, MessageType, Timestamp, CorrelationId, ReplyTo, ContentType, PartitionKey, WorkflowId, JobId, Dispatched, HeaderBag, Body, Source, Type, DataSchema, Subject, TraceParent, TraceState, Baggage, DataRef, SpecVersion
- **Outbox MySQL (24)**: same as MSSQL minus Id, plus Created, CreatedID
- **Outbox SQLite (22)** / **Spanner (22)**: same as MSSQL minus Id
- **Inbox MSSQL (6)**: Id, CommandId, CommandType, CommandBody, Timestamp, ContextKey
- **Inbox PostgreSQL/MySQL/SQLite/Spanner (5)**: above minus Id

### Case sensitivity for V1Columns
- MSSQL / MySQL / SQLite: `StringComparer.OrdinalIgnoreCase`
- PostgreSQL: `StringComparer.Ordinal` (V1 set in lowercase — information_schema folds names)
- Spanner: `StringComparer.Ordinal` (V1 set matches builder DDL casing)

### Future extensibility
When V2 migrations are added:
```csharp
// if (actualColumns.IsSupersetOf(V2Columns)) return 2;
if (actualColumns.IsSupersetOf(V1Columns)) return 1;
return 0;
```

### Spanner-specific notes
- History table is `BrighterMigrationHistory` (no underscores — Spanner rejects identifiers starting with `_`)
- DDL via `CreateDdlCommand()` with gRPC status code error handling for crash safety
- `SpannerConnectionHelper` uses `EmulatorDetection.EmulatorOrProduction` for emulator support
- xUnit `[Collection("SpannerBoxProvisioning")]` serializes tests to avoid concurrent DDL errors
- Emulator setup: `docker compose -f docker-compose-spanner.yaml up -d` then `./setup-spanner-emulator.sh`
- Env vars needed: `SPANNER_EMULATOR_HOST=localhost:9010 GOOGLE_CLOUD_PROJECT=brighter-tests`

### Docker compose files for integration tests
- MSSQL: `docker-compose-mssql.yaml` (port 11433, `brighter-sqlserver-1`)
- PostgreSQL: `docker-compose-postgres.yaml` (`brighter-postgres-1`)
- MySQL: `docker-compose-mysql.yaml` (`brighter-mysql-1`)
- Spanner: `docker-compose-spanner.yaml` + `./setup-spanner-emulator.sh`

## Earlier commits (for context)
- `6ac3093b4` — fix: address PR review findings for box provisioning (payload mode validation on non-MSSQL backends, Spanner DI extensions, Spanner added to solution, MsSqlBoxMigrationRunner async/rollback fixes, Spanner gRPC status-code error handling)
- `e347222e6` — docs: `specs/0023-box_database_migration/adding-outbox-columns.md` and `.agent_instructions/box_provisioning.md`

### Findings previously dismissed as invalid (PR #4039)
- **B1 (SchemaName default interface member)**: Paramore.Brighter targets netstandard2.0, which doesn't support default interface members. Both internal implementors already have `SchemaName`.
- **B3/B4 (hardcoded schema for history table check)**: The history table is always created in the default schema (dbo/public) by the migration runner. Checking there is correct. Row-level queries already use the box table's schema.

## Key Conventions
- Use primary constructors as default for new classes
- Test file names: GWT convention (`When_X_should_Y.cs`)
- Test class names: general form (`[Behavior]Tests`)
- Reduce nesting in methods by extracting well-named helpers
- Prefer project-owned files (`.agent_instructions/`, `CLAUDE.md`, `PROMPT.md`) over ephemeral Claude memory
