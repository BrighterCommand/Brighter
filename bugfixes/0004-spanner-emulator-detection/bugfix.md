# Bugfix: Spanner Outbox/Inbox tests fail against the emulator (missing EmulatorDetection)

**Linked Issue**: #4162
**Status**: Verified

## Symptom
When the Spanner Outbox/Inbox tests in `tests/Paramore.Brighter.Gcp.Tests` are run against the Spanner emulator (`SPANNER_EMULATOR_HOST=localhost:9010`, `GOOGLE_CLOUD_PROJECT=brighter-tests`), 60 tests fail at fixture setup with:

```
System.InvalidOperationException : Your default credentials were not found.
```

Breakdown (per issue): Outbox.SpannerBinary.{Sync,Async} = 24, Outbox.SpannerText.{Sync,Async} = 24, Spanner.Inbox.SpannerInbox{,Async}Test = 12 = 60 total. The 33 `Spanner/BoxProvisioning/` tests pass. The exception is thrown because the Spanner gRPC client tries to load Application Default Credentials instead of routing to `SPANNER_EMULATOR_HOST`, i.e. `EmulatorDetection` is never opted into.

Repro:
```bash
docker compose -f docker-compose-spanner.yaml up -d
bash ./setup-spanner-emulator.sh
SPANNER_EMULATOR_HOST=localhost:9010 GOOGLE_CLOUD_PROJECT=brighter-tests \
  dotnet test tests/Paramore.Brighter.Gcp.Tests/Paramore.Brighter.Gcp.Tests.csproj -f net9.0 \
  --filter "FullyQualifiedName~Spanner"
```

## Suspected Location
Two distinct classes of construction site are implicated — the connection *string* the tests build, and the raw `SpannerConnection` objects created from it.

Connection string source (no `EmulatorDetection`, shared by all failing tests):
- `tests/Paramore.Brighter.Gcp.Tests/Spanner/Const.cs:7` — `ConnectionString` = `"Data Source=projects/{...}/instances/brighter-spanner/databases/brightertests"` (used by both Outbox providers and Inbox tests)
- `tests/Paramore.Brighter.Gcp.Tests/Spanner/SpannerTestHelper.cs:82` — `SpannerSqlSettings.TestsBrighterConnectionString`, same shape, also no `EmulatorDetection`

Test-controlled DDL/setup connections built directly from that string (fail at fixture init):
- `tests/Paramore.Brighter.Gcp.Tests/Spanner/Inbox/SpannerInboxAsyncTest.cs:23` and `:32`
- `tests/Paramore.Brighter.Gcp.Tests/Spanner/Inbox/SpannerInboxTest.cs:22` and `:31`
- `tests/Paramore.Brighter.Gcp.Tests/Outbox/SpannerBinary/SpannerBinaryOutboxProvider.cs:34, :43, :57, :66`
- `tests/Paramore.Brighter.Gcp.Tests/Outbox/SpannerText/SpannerTextOutboxProvider.cs:34, :43, :57, :66`
- `tests/Paramore.Brighter.Gcp.Tests/Spanner/SpannerTestHelper.cs:47, :59, :71`

Internal (production `src/`) connections built from `configuration.ConnectionString`, which the tests do NOT touch directly:
- `src/Paramore.Brighter.Spanner/SpannerConnectionProvider.cs:22` and `:30` — `new SpannerConnection(_connectionString)` where `_connectionString = configuration.ConnectionString` (line 17)
- `src/Paramore.Brighter.Spanner/SpannerUnitOfWork.cs:28` and `:41` — same pattern (`_connectionString` at line 23)
- These are reached via `SpannerOutbox` (`src/Paramore.Brighter.Outbox.Spanner/SpannerOutbox.cs:45`) and `SpannerInboxAsync` (`src/Paramore.Brighter.Inbox.Spanner/SpannerInboxAsync.cs:37`), both of which do `new SpannerConnectionProvider(configuration)`.

Passing reference pattern (opts into emulator detection):
- Test side: `tests/Paramore.Brighter.Gcp.Tests/Spanner/BoxProvisioning/When_spanner_fresh_install_runs_it_should_create_table_and_stamp_v_latest_and_skip_duplicate_history_insert.cs:125-129` (and the same 2-line `SpannerConnectionStringBuilder(...) { EmulatorDetection = EmulatorDetection.EmulatorOrProduction }` / `new SpannerConnection(builder)` block repeated across the other `BoxProvisioning/*.cs` files, e.g. `:225-229`)
- Production side: `src/Paramore.Brighter.BoxProvisioning.Spanner/SpannerConnectionHelper.cs:35-42` — `CreateConnection` wraps the string in `SpannerConnectionStringBuilder` with `EmulatorDetection = EmulatorDetection.EmulatorOrProduction`. This is why BoxProvisioning passes on both its test-controlled AND internal connections.

## Root-Cause Hypothesis
The failing tests build a Spanner connection string (`Const.ConnectionString` at `Const.cs:7` / `SpannerSqlSettings.TestsBrighterConnectionString` at `SpannerTestHelper.cs:82`) that contains only `Data Source=...` and never sets `EmulatorDetection`. Every `new SpannerConnection(...)` derived from that string — both the test's own DDL-setup connections and, crucially, the outbox/inbox's INTERNAL connections in `SpannerConnectionProvider` and `SpannerUnitOfWork` — therefore defaults to loading ADC and throws "Your default credentials were not found" against the emulator. The BoxProvisioning tests pass precisely because their production helper (`SpannerConnectionHelper.CreateConnection`) sets `EmulatorDetection.EmulatorOrProduction`, whereas the generic `SpannerConnectionProvider`/`SpannerUnitOfWork` used by outbox/inbox do not.

**UNVERIFIED — to be proven or refuted in /bugfix:confirm**: The issue proposes applying `EmulatorDetection.EmulatorOrProduction` to the Outbox/Inbox test setup helpers (test-only change). A commenter refines this: fixing only the test-controlled DDL connections (the `new SpannerConnection(configuration.ConnectionString)` call sites in the providers/inbox tests) would be insufficient, because the outbox/inbox's own internal connections (`SpannerConnectionProvider.cs:22/30`, `SpannerUnitOfWork.cs:28/41`) rebuild from `configuration.ConnectionString` and would still fail. Therefore `EmulatorDetection` must be embedded into the connection STRING itself (via `SpannerConnectionStringBuilder.EmulatorDetection`, which serialises into `.ConnectionString`) at `Const.cs:7` / `SpannerTestHelper.cs:82` so it round-trips through `RelationalDatabaseConfiguration` into the internal connections. Open questions for confirm: (a) does `SpannerConnectionStringBuilder.EmulatorDetection` actually serialise into `.ConnectionString` on this driver version; (b) is this genuinely test-only, or should the production `SpannerConnectionProvider`/`SpannerUnitOfWork` gain emulator support like `SpannerConnectionHelper`; (c) confirm the exact count (60) maps to these construction sites.

## Confirmed Root Cause
**CONFIRMED.** `Const.ConnectionString` (`tests/Paramore.Brighter.Gcp.Tests/Spanner/Const.cs:7`) produces a bare `Data Source=projects/.../databases/brightertests` with **no `EmulatorDetection` keyword**. That single string feeds both the Inbox tests (via `DefaultConnectingString` → `RelationalDatabaseConfiguration.ConnectionString`) and the Outbox providers (`_configuration`). Because `EmulatorDetection` is opt-in and absent, the gRPC client ignores `SPANNER_EMULATOR_HOST` and falls back to ADC → `InvalidOperationException: Your default credentials were not found`. The 33 BoxProvisioning tests pass precisely because they DO set it (production `SpannerConnectionHelper` + their own test helpers).

**A connection-string-level fix is REQUIRED** (not a test-object-only fix): the failing tests exercise the outbox/inbox's *internal* connections (`SpannerConnectionProvider`/`SpannerUnitOfWork`), which rebuild `new SpannerConnection(configuration.ConnectionString)`. Wrapping only the tests' own DDL `new SpannerConnection(...)` calls would move the failure from fixture-init into the test body but still throw. `EmulatorDetection` must be embedded into the STRING so it round-trips through `RelationalDatabaseConfiguration` into those internal connections.

## Evidence
- [x] **Code-trace** (executable repro needs live emulator infra, so trace stands as proof):

**(a) `EmulatorDetection` serialises into `.ConnectionString` — PROVEN** (Google.Cloud.Spanner.Data 5.12.0, `Directory.Packages.props`). Decompiled `SpannerConnectionStringBuilder.set_EmulatorDetection`: after `CheckEnumValue`, executes `this["EmulatorDetection"] = value.ToString()` via `DbConnectionStringBuilder::set_Item` (IL_000c–IL_001f); base `DbConnectionStringBuilder` serialises the keyword dictionary into `.ConnectionString`. `get_EmulatorDetection` reads it back via `TryGetValue("EmulatorDetection", ...)`. Round-trip confirmed from the shipped assembly (not a risk). `RelationalDatabaseConfiguration.ConnectionString` is a passthrough auto-property (`src/Paramore.Brighter/RelationalDatabaseConfiguration.cs:35,56`), so the keyword survives into the internal providers.

**(b) Internal providers ARE exercised by the test body → string fix REQUIRED.** Outbox: generated `outbox.Add(...)` → `SpannerOutbox(_configuration)` → `new SpannerConnectionProvider(configuration)` (`src/Paramore.Brighter.Outbox.Spanner/SpannerOutbox.cs:45`) → `GetConnection()` → `new SpannerConnection(_connectionString)` + `.Open()` (`src/Paramore.Brighter.Spanner/SpannerConnectionProvider.cs:22-23,30-31`); transaction path → `SpannerUnitOfWork.cs:28,41`. Inbox: `SpannerInboxAsync(configuration)` → `new SpannerConnectionProvider(configuration)` (`src/Paramore.Brighter.Inbox.Spanner/SpannerInboxAsync.cs:37`). `_connectionString = configuration.ConnectionString` (`SpannerConnectionProvider.cs:17`, `SpannerUnitOfWork.cs:23`).

**(d) Throws at fixture init, before any body runs — CONFIRMED.** Inbox: `InboxTests` ctor → `BeforeEachTest()` → `CreateStore()` → `CreateInboxTable` → `new SpannerConnection(configuration.ConnectionString).Open()` (`SpannerInboxTest.cs:22-23`; `SpannerInboxAsyncTest.cs:23`). Outbox: generated test ctor → `_outboxProvider.CreateStore()` → `new SpannerConnection(_configuration.ConnectionString).Open()` (`SpannerBinaryOutboxProvider.cs:34-35`; `SpannerTextOutboxProvider.cs:34,43`). First `Open()` throws → ctor throws → test fails at construction.

**(e) Count = 60 matches exactly.** SpannerBinary Sync 12 + Async 12 = 24; SpannerText Sync 12 + Async 12 = 24; Inbox 6 sync + 6 async = 12. Total 60.

Red-repro note: the failure is not an `Assert.*` but the fixture-construction `InvalidOperationException: Your default credentials were not found` from `SpannerConnection.Open()`.

## Scope Notes
- **Single canonical fix site: `tests/Paramore.Brighter.Gcp.Tests/Spanner/Const.cs:7`.** Both `SpannerInboxTest`/`SpannerInboxAsyncTest.DefaultConnectingString` and `SpannerBinary/SpannerTextOutboxProvider._configuration` derive from `Const.ConnectionString`, so fixing that one property fixes all 60. Per-call-site edits would be redundant AND insufficient (miss the internal providers). Prefer the string-source change.
- **Triage over-listed `SpannerTestHelper.cs` — it is DEAD CODE** on the failing path (referenced only by itself; no failing test consumes it). Its cited sites (`:47,:59,:71,:82`) do not need changing for the 60 failures.
- **Production parity gap — IN SCOPE (user-approved widening at Confirm gate).** `src/Paramore.Brighter.Spanner/SpannerConnectionProvider.cs:22,30` and `SpannerUnitOfWork.cs:28,41` build `SpannerConnection` without `EmulatorDetection`, diverging from `SpannerConnectionHelper.cs:35-42`. Fix will add `EmulatorDetection` code-level parity to these providers (matching the BoxProvisioning helper) in addition to the test-side connection-string fix. The regression test(s) should cover both the outbox/inbox internal-connection path (proving the production providers now honour the emulator) and fixture-init.
- **Cross-backend (informational):** Pub/Sub gateway test providers already set `EmulatorDetection` (e.g. `GcpPullMessageGatewayProvider.cs:58,62`). Only the Spanner outbox/inbox test path missed it — isolated omission, not systemic.

## Regression Test
**Production-parity coverage (new test):** `tests/Paramore.Brighter.Gcp.Tests/Spanner/Connection/When_a_spanner_connection_provider_opens_a_connection_it_should_honour_the_spanner_emulator.cs`. Builds a **bare** connection string (no `EmulatorDetection`, built inline — not via `Const` — so it stays scoped to the provider even after `Const.cs` is patched), constructs the production `SpannerConnectionProvider`, calls `GetConnectionAsync()`, and asserts the connection opens and `SELECT 1` returns `1`.

RED confirmed against the emulator (gRPC `localhost:9110`, REST `9020`; instance `brighter-spanner`, db `brightertests`, project `brighter-tests`):
```
System.InvalidOperationException : Your default credentials were not found.
  at Google.Apis.Auth.OAuth2.DefaultCredentialProvider.CreateDefaultCredentialAsync()
  ...
  at Paramore.Brighter.Spanner.SpannerConnectionProvider.GetConnectionAsync(...) in SpannerConnectionProvider.cs:line 31
```
Fails at the exact production line confirm identified (the `new SpannerConnection(_connectionString)` with `EmulatorDetection=None` falling back to ADC).

**Test-side coverage (existing suite):** the 60 pre-existing Spanner Outbox/Inbox tests remain the regression coverage for the `Const.cs` connection-string fix — they are red today and go green once `Const.ConnectionString` embeds `EmulatorDetection`. No new duplicate test is written for that (asserting a test-helper constant string would be brittle and lower-value than the real integration suite).

Run command:
```
SPANNER_EMULATOR_HOST=localhost:9110 GOOGLE_CLOUD_PROJECT=brighter-tests \
  dotnet test tests/Paramore.Brighter.Gcp.Tests/Paramore.Brighter.Gcp.Tests.csproj -f net9.0 \
  --filter "FullyQualifiedName~SpannerConnectionProviderEmulatorTests"
```
Note: local env remaps gRPC to `9110` (a VPN on `utun4` holds `9010`); standalone compose file `docker-compose-spanner.local.yaml`. On CI/default ports use `SPANNER_EMULATOR_HOST=localhost:9010`.

## Fix
Minimal change scoped to the confirmed cause (EmulatorDetection missing) — embed `EmulatorDetection.EmulatorOrProduction` into the connection string so it round-trips into every derived `SpannerConnection` (matching the existing production `SpannerConnectionHelper` pattern; safe in real deployments since it only routes to the emulator when `SPANNER_EMULATOR_HOST` is set):

**Production (parity — makes the new regression test green):**
- `src/Paramore.Brighter.Spanner/SpannerConnectionProvider.cs` — `_connectionString` field now built via `new SpannerConnectionStringBuilder(configuration.ConnectionString) { EmulatorDetection = EmulatorDetection.EmulatorOrProduction }.ConnectionString` (added `using Google.Api.Gax;`).
- `src/Paramore.Brighter.Spanner/SpannerUnitOfWork.cs` — same change to its `_connectionString` field (added `using Google.Api.Gax;`).

**Test-side (makes the 60 pre-existing ADC failures reach the DB):**
- `tests/Paramore.Brighter.Gcp.Tests/Spanner/Const.cs` — `ConnectionString` now embeds `EmulatorDetection` via `SpannerConnectionStringBuilder` (added `using Google.Api.Gax;`, `using Google.Cloud.Spanner.Data;`).

**Result (emulator, `SPANNER_EMULATOR_HOST=localhost:9110`):**
- New regression test `SpannerConnectionProviderEmulatorTests`: **GREEN**.
- Full `~Spanner` filter: **66 passed / 28 failed** (was 33 passed / 60 failed). All ADC "credentials were not found" failures eliminated — the confirmed root cause of #4162 is fixed.
- A single Spanner Outbox test in isolation: **passes**.

**Newly-surfaced SEPARATE issue (OUT OF SCOPE for this confirmed cause — not the ADC/EmulatorDetection bug):** the remaining 28 failures are all `FailedPrecondition: Schema change operation rejected because a concurrent schema change operation ... is already in progress`. The Spanner **emulator serialises DDL**, but the Outbox/Inbox test classes run in parallel (unlike `BoxProvisioning`, which serialises via `[Collection("SpannerBoxProvisioning")]`) and their fixture `CREATE/DROP TABLE` calls collide. These tests were previously masked behind the ADC failure at fixture-init and only became visible once EmulatorDetection let them reach the DDL stage. This has a different root cause (test parallelism vs emulator DDL) and is tracked separately as **#4224** — needs its own triage/fix — candidate remedy: place the Spanner Outbox/Inbox test classes in a shared xUnit collection (mirroring BoxProvisioning) or disable parallelisation for them.

**Verification:** with collection parallelisation disabled (`-- xUnit.ParallelizeTestCollections=false`) the full `~Spanner` filter is **94 passed / 0 failed** (93 pre-existing + the new regression test) — proving the fix resolves all 60 previously-ADC-failing tests, keeps the 33 BoxProvisioning tests green (no regression), and that the 28 parallel-run failures are solely the separate DDL-concurrency issue. src change is Spanner-only and a no-op in real production (EmulatorDetection only routes to the emulator when `SPANNER_EMULATOR_HOST` is set).
