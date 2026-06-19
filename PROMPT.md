# Session Resume — Replay Outbox on Inbox Duplicate

## Branch
`replay_on_seen`

## Issue
#2541 — Force Replay of Outbox on seeing duplicate Processed Inbox Message

## Spec
`specs/0027-replay-matching-outbox-events-when-inbox-has-already-seen/`

## Status
- [x] Requirements — approved (2026-04-16), updated 2026-04-17 (ConversationId → CausationId rename, AC-8 updated to all persistent stores)
- [x] Design — ADR 0057 accepted, 5 rounds of review (all findings addressed)
- [x] Tasks — approved (2026-04-17), 5 rounds of review; amended 2026-06-17 (BoxProvisioning in-scope + relational store split). 34 tasks (was 23; Task 19→19a–19f, Task 21→21a–21g).
- [~] Implementation — IN PROGRESS. **Tasks 1–18 + 19a + 19b DONE & committed** (+ STEP 0 tidy). Next: **Task 19c** (MySql inbox implements `IAmACausationTrackingInbox`, test-first against `CausationTrackingInboxBaseTests`). Then 19d–19f (Postgres/Sqlite/Spanner), 20 (NoSQL inbox), 21a (outbox schema, atomic), 21b (Liquid gen), 21c–21g, 22, 23.

## How I'm running this (process notes for resume)
- Using `/spec:implement`. Structural tasks (1–5, 19a, 21a, 21b) = no approval gate, verified by existing tests staying green, commit separately. TEST tasks (6+) = MANDATORY `/test-first` approval gate: write RED test, run it, STOP and ask user via AskUserQuestion before GREEN.
- Each task: tick its checkbox in `specs/0027.../tasks.md` and commit code+test+tasks.md together. Commit trailer: `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.
- Test result greps are noisy (CA1852/xUnit analyzer warnings) — filter with `grep -E "^(Passed!|Failed!)"` or `grep "<TestClass>\."`.
- Tests run on BOTH net9.0 + net10.0 (multi-target). Core/InMemory tests need no containers.

## Implementation progress
Structural prereqs (2026-06-17):
- ✅ **Task 1** `a7b743f85` — UseInboxHandler[Async] use pipeline `Context as RequestContext`; `protected InstrumentationOptions` on RequestHandler[Async]; removed duplicate `base.InitializeFromAttributeParams` in async.
- ✅ **Task 2** `46a5f32e1` — `PipelineStepDescription.Attribute` init-property; populated at both Describe() sites.
- ✅ **Task 3** `5df3caed9` — Describe() injects global inbox attrs via reflection-only `TryCreateGlobalInboxAttribute` (same guards as Build()); `BrighterPipelineValidationExtensions.ResolveInboxConfiguration` from `IAmConsumerOptions`.
- ✅ **Task 4** `a6d624fe2` — `IAmAnOutbox? Outbox` on `IAmAnOutboxProducerMediator` (impl: `(IAmAnOutbox?)_outBox ?? _asyncOutbox`).
- ✅ **Task 5** `6f4977511` — `OnceOnlyAction.Replay`; `RequestContextBagNames.CausationId="Brighter-CausationId"`; `BrighterSemanticConventions.CausationId="paramore.brighter.causation_id"`; `IAmACausationTrackingInbox` (Inbox/ folder, root ns); `IAmACausationTrackingOutbox` (root folder, root ns). Both root `Paramore.Brighter` namespace.

Core behaviour (test-first, 2026-06-17/18):
- ✅ **Task 6** `c532102bd` — InMemoryInbox implements IAmACausationTrackingInbox (InboxItem.CausationId; Add reads from Bag; GetCausationId[Async]; Supports=>true). Test: `InMemoryInboxCausationTrackingTests`.
- ✅ **Task 7** `63c3b1f61` — InMemoryOutbox implements IAmACausationTrackingOutbox (OutboxEntry.CausationId; ReplayCausation[Async] resets TimeFlushed=MinValue for matching causation). Test: `InMemoryOutboxCausationTrackingTests`.
- ✅ **Task 8** `9e91bd83b` — sync UseInboxHandler defaults Context.Bag[CausationId]=request.Id (preserving existing) before handling. Test: `UseInboxHandlerCausationTrackingTests` (drives Send w/ passed-in RequestContext to inspect Bag).
- ✅ **Task 9** `36c479605` — async UseInboxHandlerAsync same. Test: `UseInboxHandlerAsyncCausationTrackingTests`.
- ✅ **Task 10** `cd8a3d0d2` — sync Replay branch: UseInboxHandler gains optional `IAmACausationTrackingOutbox?` ctor param; on duplicate reads causation from tracking inbox, calls ReplayCausation, returns w/o re-handling. Log.CommandHasAlreadyBeenSeenReplayingOutbox. Test: `UseInboxHandlerReplayTests` + double `MyStoredCommandToReplayHandler`.
- ✅ **Task 11** `ec349bfca` — async Replay branch (GetCausationIdAsync + ReplayCausationAsync). Test: `UseInboxHandlerAsyncReplayTests` + double `MyStoredCommandToReplayHandlerAsync`.
- ✅ **Tasks 12 & 13** `c7e54b775` — characterization (green on first run): Replay w/ no outbox = graceful terminal step (no re-exec, no throw), sync+async. Tests: `UseInboxHandlerReplayWithNoOutboxTests`, `UseInboxHandlerAsyncReplayWithNoOutboxTests`.

- ✅ **Task 15** `45d77ef11` — Replay telemetry ActivityEvent `"UseInboxHandler Duplicate Replay"` on `Context.Span`, tags `RequestId`+`CausationId`, gated on non-null span **and** `Context.InstrumentationOptions.HasFlag(Brighter)`. Sync+async (`WriteReplayEvent` helper in each; async captures `span = Context?.Span` BEFORE the awaits because `RequestContext.Span` is thread-affine). **DESIGN CHANGE (user-steered, supersedes the ctor-param/Task-1B-protected-prop plan)**: `InstrumentationOptions` was always `All` on the handler itself (wrong source) — the configured value lives in `CommandProcessor._instrumentationOptions` and is what created the span. So added `InstrumentationOptions` as a first-class property (default `All`) on **`IRequestContext` + `RequestContext`** (peer to `Span`/`FeatureSwitches`/`ResiliencePipeline`), set in `CommandProcessor.InitRequestContext` from `_instrumentationOptions`, carried in `CreateCopy()`. Handlers now read `Context.InstrumentationOptions` like they read `Context.Span` — no ctor param. ⚠️ **Task 1B's `protected InstrumentationOptions` on `RequestHandler<T>` is now redundant for this purpose** (reflects the handler's own ctor value, not the configured one) — left in place; possible follow-up tidy. Test: `UseInboxHandlerReplayTelemetryTests` (4 facts: sync event, async event, Brighter-flag-off → no event, null-span → no event + replay still happens). Core.Tests 807/807 (7 skipped) net9.0; new test 4/4 net9+net10.

- ✅ **Task 14** `b9ca1adbe` — pipeline validation rejects Replay without causation tracking. `HandlerPipelineValidationRules.ReplayRequiresCausationTracking(IAmAnInbox?, IAmAnOutbox?)` (collapsed spec; private `OnceOnlyActionOf(RequestHandlerAttribute?)` switch over UseInbox(Async)Attribute; finds Replay across Before+AfterSteps then evaluates inbox then outbox; Error=role-not-impl, Warning=Supports()=false or no-outbox). Wired into `PipelineValidator` (new optional `IAmAnInbox? inbox`/`IAmAnOutbox? outbox` ctor params → specs array) and `BrighterPipelineValidationExtensions.ValidatePipelines` (inbox=`ResolveInboxConfiguration(sp)?.Inbox`, outbox=`sp.GetService<IAmAnOutboxProducerMediator>()?.Outbox`). Test: `ReplayCausationTrackingValidationTests` (7 facts, file-scoped Tracking/NonTracking In/Outbox doubles). Core Validation 86/86 both TFMs.

## Current position — resume at Task 19c (MySql inbox impl `IAmACausationTrackingInbox`, test-first)

### ✅ DONE 2026-06-19 — Task 19b `46532f5ab` (MsSql inbox `IAmACausationTrackingInbox`)
**Shared-base seam design (applies to all of 19c–19f).** The shared `RelationalDatabaseInbox` base now implements `IAmACausationTrackingInbox`, gated on an OPTIONAL companion interface `IRelationalDatabaseInboxCausationQueries` (new, in core `Paramore.Brighter`) that a backend's `*Queries` set may implement. Members: `AddCausationCommand` (INSERT incl. CausationId), `GetCausationIdCommand` (SELECT), `CausationColumnExistsCommand` (live column probe — backend-specific SQL). When `queries as IRelationalDatabaseInboxCausationQueries` is non-null: `Add`/`AddAsync` append `@CausationId` (read from `RequestContext.Bag[CausationId]` via private `ReadCausationId`) and `CreateAddCommand` switches to `AddCausationCommand`; `GetCausationId[Async]` runs the SELECT (private `MapCausationId`/`MapCausationIdAsync`, IsDBNull→null); `SupportsCausationTracking[Async]` runs the probe (MapBoolFunction→HasRows). When null: `SupportsCausationTracking`→false, `GetCausationId`→null, Add stores nothing extra (opt-in, non-breaking). **So merely making the base implement the interface does NOT turn 19c–19f green** — each backend's `*Queries` must implement the optional interface with its own SQL (esp. the column-existence probe dialect). `CreateAddParameters` virtual signature was NOT changed (param appended in `Add` instead) to avoid breaking external overrides.
- **For 19c (MySql)**: make `MySqlQueries` implement `IRelationalDatabaseInboxCausationQueries`. Probe SQL = `information_schema.columns` (MySql has no sys.columns). `AddCausationCommand` mirrors MySql's existing `AddCommand` + `CausationId`/`@CausationId`. `GetCausationIdCommand` = `SELECT CausationId FROM {0} WHERE CommandID=@CommandID AND ContextKey=@ContextKey LIMIT 1`. Test class `MySqlCausationTrackingInboxTest : CausationTrackingInboxBaseTests` (mirror `MsSqlCausationTrackingInboxTest`; MySql uses main docker-compose port 3306; MySql test proj is net9-only). Then Postgres (information_schema, lowercase `causationid`), Sqlite (`pragma_table_info('{0}')`), Spanner (`information_schema.columns` + SpannerSqlQueries — but Spanner inbox is async-only via SpannerInboxAsync, check the seam).
- **MsSql specifics committed**: `MsSqlQueries.CausationColumnExistsCommand = "SELECT 1 FROM sys.columns WHERE [object_id]=OBJECT_ID('{0}') AND [name]='CausationId'"`. 4/4 both TFMs; MSSQL Inbox suite 47/47.

### (prior) resume at Task 19b — superseded, see above

### ⚠️ DB CONTAINER GOTCHA (cost ~40 min this session — DO NOT repeat)
- **MSSQL tests connect to host port `11433`, NOT 1433.** Bring SQL Server up with `docker compose -f docker-compose-mssql.yaml up -d` (image = **azure-sql-edge**, arm64-native, maps `11433:1433`). The main `docker-compose.yaml` `sqlserver` service uses `mcr.microsoft.com/mssql/server` (=SQL Server 2025) on port **1433** — WRONG image AND wrong port → tests get "Connection refused / error 35" that masquerades as a server crash. If a fresh azure-sql-edge container exits(1) with `/.system could not be created Errno 13`, `docker volume rm brighter_sqlserver-data` then recreate.
- azure-sql-edge has **no `sqlcmd`** in the container — readiness probe via `docker exec sqlcmd` always fails; instead grep logs for `SQL Server is now ready for client connections` or python-probe host port 11433.
- **Spanner**: `docker compose -f docker-compose-spanner.yaml up -d` (emulator, ports 9010/9020), then `export SPANNER_EMULATOR_HOST=localhost:9010 GOOGLE_CLOUD_PROJECT=brighter-tests` and run `bash setup-spanner-emulator.sh` (creates instance `brighter-spanner` + db `brightertests`). Run Gcp tests with those two env vars exported. Spanner BoxProvisioning = 33 tests.
- MySQL (3306) + Postgres (5432) come from the main `docker-compose.yaml` and are correct there.

### ✅ DONE 2026-06-19 — Task 19a `f1d7ca6de` (relational inbox CausationId schema, forced-atomic)
Single structural commit. Builders (5) emit `CausationId`; catalogs MsSql/MySql/Sqlite inbox **V2→V3**, PostgreSql inbox **V1→V2** (added `AddColumn` helper + `DefaultSchema="public"`, idempotent `ADD COLUMN IF NOT EXISTS causationid`); Spanner `VLatestInbox` 2→3 + `SpannerInboxProvisioner.V1Columns` gains CausationId. Tests: 5 `ExpectedMigrationVersions` bumped (PG→2, rest→3); 4 migrations-listed + 3 bootstrap-upgrade tests renamed v2→v3 + assert new column; MsSql drift renamed v3, PG drift renamed v_latest; cross-backend Spanner carve-out literal **1→2** (PG stays exactly one version behind — re-derive carve-out, don't fold back); MsSql+PG legacy-race arms expect the extra applied migration (was the one non-obvious break — PG/MsSql race tests hard-coded the old count; MySQL/Sqlite race tests auto-adapt). **Column type per backend**: NVARCHAR(256) MsSql/Sqlite, VARCHAR(256) MySql, character varying(256)/lowercase `causationid` PG, STRING(256) Spanner. **VERIFIED real containers both TFMs**: Sqlite 55 (full suite 127), MySQL 78 (net9-only proj), Postgres 79, MSSQL 85, Spanner/Gcp 33. Drift auto-adapts (compares builder DDL vs `migrations[Count-1].LogicalColumns`) so only NAME-bearing tests needed manual renames.

### Prior position (Task 19a was "needs DB containers")

### DONE 2026-06-18
- ✅ **Task 18** `60198d607` — base test classes `CausationTrackingInboxBaseTests` (4 facts) + `CausationTrackingOutboxBaseTests<TTransaction>` (3 facts) in `tests/Paramore.Brighter.Base.Test/{Inbox,Outbox}/`. Abstract `Inbox`/`Outbox` property + virtual `CreateStore`/`DeleteStore` hooks (sync `IDisposable` lifecycle, matching existing `InboxTests`/`OutboxTest`). Validated green against InMemory first via concrete subclasses `InMemoryCausationTrackingInboxTests`/`InMemoryCausationTrackingOutboxTests` in `InMemory.Tests` (added a `Base.Test` ProjectReference — InMemory.Tests did NOT reference it before). **Green on first run** (characterization; InMemory already implements interfaces from Tasks 6/7). Outbox base assertions use `OutstandingMessages` ONLY (NOT `DispatchedMessages`) — `DispatchedMessages` compares `TimeFlushed <= now-age` which breaks under FakeTimeProvider (fake now=2000 vs real dispatch ts), so "still dispatched" is proved by "not in outstanding" instead. InMemory.Tests CausationTracking 7/7 net9+net10.
- ✅ **Task 17** `7ec5ecfb9` — DI registration of `IAmACausationTrackingOutbox`. Registered under the role interface (same singleton) in BOTH `AddProducers` paths in `ServiceCollectionExtensions`: eager path conditionally (`if (outbox is IAmACausationTrackingOutbox)`); deferred path via factory `sp => (sp.GetRequiredService<IAmAnOutbox>() as IAmACausationTrackingOutbox)!` (null for non-tracking outbox). Inbox needs no reg (handler pattern-matches). Test `CausationTrackingOutboxRegistrationTests` (3 facts) builds the DI via `ServiceCollectionBrighterBuilder` directly — NOT `services.AddBrighter()` (auto-scan hits a duplicate-mapper conflict between `MyDescribableCommand` doubles in the Core.Tests assembly). Negative case uses `SpyOutbox`+`SpyTransactionProvider` (SpyTransaction type). Core.Tests OnceOnly 38/38 both TFMs; Extensions.Tests 99/99 net9.

### DONE earlier 2026-06-18
- ✅ **STEP 0 tidy** `9e2ec8d2a` — removed redundant Task-1B `protected InstrumentationOptions` props from `RequestHandler<T>`/`RequestHandlerAsync<T>` (+ XML docs). Primary-ctor `instrumentationOptions` param STAYS (still used for `BrighterTracer.WriteHandlerEvent`). Behaviour-preserving. Core.Tests 807/807.
- ✅ **Task 16** `7365def9c` — UseInboxHandler[Async] add Throw/Warn/Add telemetry ActivityEvents (`"UseInboxHandler Duplicate Throw"`/`"...Duplicate Warn"`/`"UseInboxHandler Add"`). New `WriteInboxEvent(span, request, eventName)` helper in each handler, RequestId tag only, same `Context?.Span` + `Context.InstrumentationOptions.HasFlag(Brighter)` gate as `WriteReplayEvent`. Throw event written BEFORE the throw; async Add captures span before awaits (thread-affine). Test: `UseInboxHandlerTelemetryTests` (8 facts). Core.Tests 815/815 net9+net10 (net9 had one unrelated flaky failure on first multi-target run; clean on isolated re-run).

### NEXT — Task 19a (STRUCTURAL: relational inbox CausationId schema via BoxProvisioning — needs DB containers)
**Task 19a** is a single forced-atomic STRUCTURAL commit (no `/test-first` gate; verified by existing drift/cross-backend tests staying green). Add the `CausationId` column to every relational inbox schema: catalog bump per backend (MsSql/MySql/Sqlite inbox V2→**V3**; PostgreSql inbox V1→**V2**), update live `*InboxBuilder` DDL, move each backend's builder/migration drift parity test, add column via `SpannerInboxProvisioner`+`SpannerInboxBuilder`, and the atomic glue: bump `SpannerBoxMigrationRunner.VLatestInbox` 2→3 + update the PostgreSql carve-out literal in `SpannerVLatestDriftAgainstRelationalCatalogTests` (1→2). See tasks.md Task 19a for full detail. Then Tasks 19b–19f (per-backend `IAmACausationTrackingInbox` impls, test-first).

## Remaining (Task 15 onward)
- Task 15: UseInboxHandler Replay telemetry ActivityEvent on Context.Span (gated InstrumentationOptions.Brighter via protected prop from Task 1B); `BrighterSemanticConventions.CausationId` tag. Test name `When_replaying_duplicate_should_add_replay_telemetry_event_to_span`.
- Task 16: Throw/Warn/Add telemetry events (tidy; commit separately).
- Task 17: DI registration of IAmACausationTrackingOutbox in ServiceCollectionExtensions (`if (outbox is IAmACausationTrackingOutbox) services.AddSingleton(...)`). Note inbox does NOT need separate reg (handler pattern-matches `_inbox is IAmACausationTrackingInbox`).
- Task 18: base test classes `CausationTrackingInboxBaseTests`/`CausationTrackingOutboxBaseTests` in `tests/Paramore.Brighter.Base.Test`, validated vs InMemory first.
- Tasks 19a/21a: forced-atomic relational SCHEMA via BoxProvisioning (catalog bumps + builders + Spanner VLatest constants + drift tests). Need DB containers. See "IN scope" section below for exact versions.
- Tasks 19b–f / 21c–g: per-backend interface impls (need DB containers). 21b = Liquid generator.
- Tasks 20/22: NoSQL (DynamoDB/DynamoDB.V4/Firestore/MongoDb), schemaless, Supports()=>true.
- Task 23: build + full core test run.

### Task review history
- Round 1: 3 findings ≥60 (75: missing async terminal step, 72: monolithic persistent store tasks, 62: telemetry covers too many behaviors) — all addressed
- Round 2: 1 finding ≥60 (65: missing InstrumentationOptions.Brighter gating) — addressed
- Round 3: 1 finding ≥60 (75: InstrumentationOptions inaccessible from UseInboxHandler) — addressed by adding Part B to Task 1
- Round 4: 1 finding ≥60 (75: Spanner misclassified as NoSQL) — addressed by moving to relational tasks
- Round 5: PASS — 0 findings ≥60

## Task overview (34 tasks after 2026-06-17 split)

### Structural prerequisites (Tasks 1-5)
1. UseInboxHandler uses pipeline's `this.Context` + expose `InstrumentationOptions` as protected property on `RequestHandler<T>`
2. Enrich `PipelineStepDescription` with `Attribute` property
3. `Describe()` includes global inbox attributes + `InboxConfiguration` in validation path (depends on 2)
4. Expose `Outbox` from `IAmAnOutboxProducerMediator`
5. New types: `OnceOnlyAction.Replay`, `RequestContextBagNames.CausationId`, `BrighterSemanticConventions.CausationId`, `IAmACausationTrackingInbox`, `IAmACausationTrackingOutbox`

### Core behavior (Tasks 6-13, test-first)
6. InMemoryInbox stores and retrieves CausationId
7. InMemoryOutbox stores CausationId + `ReplayCausation`
8. Sync UseInboxHandler generates CausationId on first handling
9. Async UseInboxHandlerAsync generates CausationId on first handling
10. Sync UseInboxHandler replays outbox on duplicate (Replay action)
11. Async UseInboxHandlerAsync replays outbox on duplicate (Replay action)
12. Sync UseInboxHandler handles Replay with no outbox (terminal step)
13. Async UseInboxHandlerAsync handles Replay with no outbox (terminal step)

### Infrastructure (Tasks 14-18, test-first)
14. Pipeline validation: Replay requires causation-tracking support
15. UseInboxHandler Replay telemetry event on pipeline span
16. UseInboxHandler Throw/Warn/Add telemetry events (tidy improvement)
17. DI registration of `IAmACausationTrackingOutbox`
18. Base test classes for persistent store causation tracking

### Persistent stores (Tasks 19-22 — relational ones SPLIT 2026-06-17)
- **19a** STRUCTURAL (atomic, 1 commit): relational inbox schema via BoxProvisioning — all catalogs (MsSql/MySql/Sqlite V3, Postgres V2) + builders + Spanner provisioner + `VLatestInbox` 2→3 + carve-out literal 1→2 + drift tests.
- **19b–19f** test-first per backend: MsSql, MySql, Postgres, Sqlite, Spanner implement `IAmACausationTrackingInbox` (depends 19a+18+5).
- **20** NoSQL inbox (schemaless, no BoxProvisioning): DynamoDB, DynamoDB.V4, Firestore, MongoDb.
- **21a** STRUCTURAL (atomic, 1 commit): relational outbox schema via BoxProvisioning — all catalogs V8 + index + builders + Spanner provisioner + `VLatestOutbox` 7→8 + drift tests.
- **21b** TOOLING: extend Liquid generator for causation-tracking outbox tests (depends 18).
- **21c–21g** test-first per backend: MsSql, MySql, PostgreSql, Sqlite, Spanner implement `IAmACausationTrackingOutbox` (depends 21a+21b+5).
- **22** NoSQL outbox (schemaless): DynamoDB, DynamoDB.V4, Firestore, MongoDb.
- **Why split**: relational schema is forced-atomic (`SpannerVLatestDriftAgainstRelationalCatalogTests` asserts `VLatest*` == every relational catalog count), so all catalog bumps must land together → one structural task; behavioral interface work is genuinely per-backend → separate `/test-first` cycles.

### Verification (Task 23)
23. Build + run all core tests

## Key design decisions (ADR 0057)

### Core feature
- **Causation Id**: Links an inbox entry to the outbox messages produced during that handler invocation. Propagated via `RequestContext.Bag` (key: `Brighter-CausationId`). Distinct from CorrelationId (request-reply), JobId, WorkflowId.
- **OnceOnlyAction.Replay**: New enum value. When inbox detects duplicate and action is Replay, it clears dispatch state on matching outbox messages so the sweeper resends them.
- **Two new role interfaces** (opt-in, non-breaking):
  - `IAmACausationTrackingInbox` — knows the CausationId for an inbox entry
  - `IAmACausationTrackingOutbox` — replays a causation's outbox messages; knows if schema supports it (`SupportsCausationTracking()`)
- **UseInboxHandler** gains optional `IAmACausationTrackingOutbox?` constructor param (resolved via standard MSDI `ActivatorUtilities`)

### Observability
- `UseInboxHandler` currently has no telemetry — adding span events for all paths (Add, Throw, Warn, Replay)
- Events written to `Context.Span` (pipeline's Activity), gated on `InstrumentationOptions.Brighter`
- New `BrighterSemanticConventions.CausationId` constant (`"paramore.brighter.causation_id"`) — distinct from existing `ConversationId` which carries `CorrelationId`
- No new child spans; OutboxSweeper already creates its own trace on sweep

### Persistent store strategy
- All 18 Brighter-maintained stores (9 inbox, 9 outbox) get CausationId support
- Schema migration is opt-in — users only need to migrate if they use Replay
- Schema migration ships IN this spec via BoxProvisioning (was "separate PR" — superseded 2026-06-17; see "IN scope" section below)
- `SupportsCausationTracking()` is a permanent runtime schema check (not transitional)

### Structural prerequisites (tidy-first)
1. **RequestHandler<T>**: Expose `instrumentationOptions` as `protected InstrumentationOptions InstrumentationOptions => instrumentationOptions;` (same for async)
2. **UseInboxHandler**: Switch from private `InitRequestContext()` to pipeline's `this.Context` so Bag data is shared across pipeline
3. **PipelineStepDescription**: Add non-positional `RequestHandlerAttribute? Attribute { get; init; }` property
4. **Describe() global inbox**: Pass `InboxConfiguration` into `ValidatePipelines()` → `PipelineBuilder`. `Describe()` injects global inbox attributes using same `MethodInfo` guard checks as `Build()`
5. **IAmAnOutboxProducerMediator**: Add `IAmAnOutbox? Outbox` read-only property
6. **DI registration**: Register outbox as `IAmACausationTrackingOutbox` alongside primary interface when it implements it

### Pipeline validation
- `HandlerPipelineValidationRules.ReplayRequiresCausationTracking(IAmAnInbox? inbox, IAmAnOutbox? outbox)` — collapsed `Specification<HandlerPipelineDescription>`
- Both inbox and outbox captured via closure; checks: inbox implements tracking, inbox schema supports it, outbox present, outbox implements tracking, outbox schema supports it

### Test strategy
- In-memory stores first (base tests in `Paramore.Brighter.Base.Test`)
- Outbox persistent store tests generated via Liquid templates
- Inbox persistent store tests derived manually from base classes

### Out of scope
- Saga/workflow orchestration
- Immediate send replay (sweeper only)
- Data backfill for existing rows (columns nullable, existing rows have null CausationId)

### IN scope (decision 2026-06-17) — Schema evolution via BoxProvisioning
Schema migration for the new CausationId column is NOW IN THIS SPEC/PR (was "separate PR"). Delivered via BoxProvisioning:
- **Catalog-based relational (MsSql, MySql, PostgreSql, Sqlite), inbox+outbox**: append a new migration version to `<Backend>{Inbox,Outbox}MigrationCatalog` — idempotent `ALTER TABLE ADD CausationId ... NULL` + extend `s_vNAddedColumns`/`Cumulative()`; update live `*{Inbox,Outbox}Builder` DDL (+ NEW index on outbox CausationId); move the backend's builder/migration **drift parity test** (columns only — index NOT covered).
  - **Per-backend versions (verified — NOT uniform for inbox!)**: outbox **V8** (all four, V7→V8). Inbox: MsSql/MySql/Sqlite **V3** (V2→V3); **PostgreSql inbox is V1-only → V2**. Postgres inbox V1 already carries ContextKey.
- **Spanner (provisioner-based, no catalog), inbox+outbox**: add column via `Spanner{Inbox,Outbox}Provisioner` + live `Spanner{Inbox,Outbox}Builder`; move Spanner drift test. **CRITICAL: bump `SpannerBoxMigrationRunner.VLatestOutbox` 7→8 and `VLatestInbox` 2→3**, and keep `When_spanner_v_latest_constants_are_compared_to_relational_catalogs...` (in `Paramore.Brighter.Gcp.Tests`) green — re-derive its Postgres carve-out (Postgres inbox count → 2, others → 3). Spanner `SupportsCausationTracking()` = live column-existence probe.
- **NoSQL (DynamoDB, DynamoDB.V4, Firestore, MongoDb)**: schemaless, NOT BoxProvisioning — field just written/read; `SupportsCausationTracking()` returns true.
- **Sequencing**: catalog version + builder DDL + (Spanner) VLatest constant land in ONE commit so drift tests never go red between commits.
- Captured in requirements.md (Constraints + AC9 + Perf NFR), ADR 0057 §"Schema Evolution via BoxProvisioning", tasks.md Tasks 19a–19f / 21a–21g (split — see Task overview above).

### Re-review status (2026-06-17, BoxProvisioning change)
Ran `/spec:review` on all 3 phases (focused on BoxProvisioning edits) → `review-{requirements,design,tasks}.md`:
- **requirements: NEEDS WORK** (2≥60: AC9 index gap 72, AC9 named-test brittleness 64) — **FIXED** (AC9 reworded w/ index + outcome; Perf NFR index made definite).
- **design/ADR: PASS** (0≥60). Sub-threshold Spanner `SupportsCausationTracking()` semantics 52 — **FIXED**. NOTE: design reviewer wrongly claimed "Postgres inbox at V2"; tasks reviewer + main agent corrected to V1-only.
- **tasks: NEEDS WORK** (3≥60: Spanner VLatest constants 88, Postgres-V1 inbox 85, single-commit sequencing 64) — **ALL FIXED** in Tasks 19/21 + coverage row. Sub-threshold granularity 58 / coverage 50 also addressed.
- All fixes applied; consider a quick confirmatory re-review before `/spec:implement`, then `/spec:approve` is NOT needed (phases already approved — these were informational re-reviews).

## Design notes for implementation
- Brighter uses `DateTimeOffset` over `DateTime` in APIs
- `BrighterSemanticConventions.ConversationId` (`messaging.message.conversation_id`) already exists and carries `CorrelationId` — do NOT reuse for CausationId
- `UseInboxHandlerAsync.cs` has a duplicate `base.InitializeFromAttributeParams()` call — fix in Task 1 tidy-first pass
- Spanner is relational (implements `IRelationalDatabaseInboxQueries`), NOT NoSQL

## Files modified
- `docs/adr/0057-replay-outbox-on-inbox-duplicate.md` — the ADR
- `specs/0027-replay-matching-outbox-events-when-inbox-has-already-seen/` — requirements.md, README.md, tasks.md, review-requirements.md, review-design.md, review-tasks.md, .issue-number, .adr-list, .requirements-approved, .design-approved, .tasks-approved

## Last commit
- `45d77ef11` feat(spec-0027): UseInboxHandler adds Replay telemetry event to pipeline span (Task 15). Tasks 1–15 done & committed; nothing pushed. PROMPT.md is gitignored scratch (not committed).

## Next step
Fresh session: **STEP 0 first** — tidy out the redundant Task-1B protected `InstrumentationOptions` props on `RequestHandler<T>`/`RequestHandlerAsync<T>` (separate structural commit; see "Current position → STEP 0"). **Then STEP 1** — resume `/spec:implement` at **Task 16** (Throw/Warn/Add telemetry events). Design pivot to remember: telemetry gating reads `Context.InstrumentationOptions` (new first-class property on IRequestContext/RequestContext), NOT a handler ctor param / the Task-1B protected prop.
