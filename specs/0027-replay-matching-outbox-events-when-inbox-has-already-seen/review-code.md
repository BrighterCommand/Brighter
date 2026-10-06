# Review: code — replay-matching-outbox-events-when-inbox-has-already-seen

**Date**: 2026-06-22
**Threshold**: 60
**Base**: master (merge-base `bbb0c8c`)
**Reviewed at**: HEAD `cbd5201be` (56 commits, 183 files, ~9,333 insertions)
**Verdict**: NEEDS WORK

1 finding at or above threshold 60. Address it before approving.

## Findings

### 1. `SupportsCausationTracking()` runs an unguarded live DB/network probe during host-startup validation (Score: 62)

The validation rule `ReplayRequiresCausationTracking` calls `SupportsCausationTracking()` directly (`HandlerPipelineValidationRules.cs:157,183`), and `BrighterValidationHostedService.StartAsync` (`BrighterValidationHostedService.cs:76`) calls `_validator.Validate()` with no try/catch. For relational stores `SupportsCausationTracking()` → `CausationColumnExists()` → `ReadFromStore(...)` opens a connection and runs SQL on first call (`RelationDatabaseOutbox.cs:992,999-1008`; `RelationalDatabaseInbox.cs:275,282-293`). For DynamoDB it is a sync-over-async network `DescribeTableAsync` (`DynamoDbOutbox.cs:557-561,572-574`).

Net effect: when a Replay pipeline is configured with a tracking inbox/outbox (the intended config), an unreachable DB / missing-permission at startup makes `Validate()` throw a raw `SqlException`/`AmazonDynamoDBException` and the host fails to start — surfaced as an opaque infra error, not a `PipelineValidationException`. This is a behavior introduced by this feature (previous validation rules did not probe live schema).

**Evidence**: `HandlerPipelineValidationRules.cs:157` / `:183` call `SupportsCausationTracking()` unwrapped; `BrighterValidationHostedService.cs:76` invokes `Validate()` with no guard; the relational probe is the first-call branch of the memoized field (returns only after a successful query), and the DynamoDB sync wrapper blocks on a network call.

**Recommendation**: Wrap the `SupportsCausationTracking()` calls inside the rule in a try/catch, treating a probe failure as "capability unknown" (degrade to a Warning finding) — or wrap the probe so a transient infra failure does not crash startup. Add a test where the tracking store's `SupportsCausationTracking()` throws and assert the validator degrades rather than propagates.

---

### 2. DynamoDB GSI probe has no null guard on `GlobalSecondaryIndexes`; sync wrapper blocks on a network call (Score: 48)

Both stores do `describeResponse.Table.GlobalSecondaryIndexes.Any(gsi => gsi.IndexName == ...)` with no null guard (`Outbox.DynamoDB/DynamoDbOutbox.cs:576`; `Outbox.DynamoDB.V4/DynamoDbOutbox.cs:589`). A table with zero GSIs could NRE. In practice this is mitigated: `AWSSDK.DynamoDBv2` is pinned `[3.7.500.5, 4)` (`Directory.Packages.props:18`), and SDK v3 defaults `AWSConfigs.InitializeCollections = true`, so the collection is an empty list, not null — and a real Brighter outbox always has Outstanding/Delivered GSIs. The probe test only strips the *Causation* GSI (`When_a_dynamodb_outbox_table_lacks_the_causation_index...cs:150`), leaving other GSIs, so the zero-GSI path is never exercised.

**Evidence**: `.GlobalSecondaryIndexes.Any(...)` unguarded at both file:lines above; test removes only `IndexName == "Causation"`.

**Recommendation**: Use `?.GlobalSecondaryIndexes?.Any(...) ?? false` for defensive safety against an app that sets `InitializeCollections=false`. Low priority given the SDK default.

---

### 3. Untracked SQLite WAL/SHM sidecar files with no `.gitignore` coverage (Score: 30)

`git status` shows untracked `samples/WebAPI/WebAPI_Dapper/GreetingsWeb/Greetings.db-shm` and `.db-wal` — transient SQLite WAL/SHM files from running the Dapper sample. `.gitignore` covers `*.db` (line 251) but not `*.db-shm`/`*.db-wal`, so these recur for anyone running the sample. They are untracked (not committed), so impact is low.

**Evidence**: `git status --porcelain` lists both; `.gitignore:251` has `*.db` only.

**Recommendation**: Add `*.db-shm` and `*.db-wal` to `.gitignore`. Do not commit the sidecar files.

---

### 4. Validation test gaps: After-step placement and throwing-probe never exercised (Score: 42)

The rule concatenates `BeforeSteps.Concat(AfterSteps)` (`HandlerPipelineValidationRules.cs:140`), but the test helper always builds `beforeSteps` with `afterSteps: []` (`When_replay_configured_without_causation_tracking_should_report_error.cs:166-185`), so the After-step branch is never tested. The test doubles' `SupportsCausationTracking()` returns a backing field and never throws (`:210,235`), so Finding 1's failure path is uncovered. `OnceOnlyAction.Warn` (non-Replay) is also untested (only `Throw`).

**Evidence**: helper `ReplayPipeline(...)` always passes `afterSteps: []`; `TrackingInbox`/`TrackingOutbox` doubles return a field.

**Recommendation**: Add an After-step Replay case and a throwing-probe case (supports Finding 1).

---

### 5. ADR observability table lists a `context_key` tag the code does not emit (Score: 22)

ADR 0057's observability table (lines 533-536) lists `context_key` on the inbox telemetry events, but the implementation emits only `request.id` (+ `causation_id` on Replay). The ADR's own implementation sketch (lines 552-557) omits `context_key`, so the ADR is internally inconsistent; the code is arguably the better choice.

**Recommendation**: Reconcile the ADR table with the shipped tags (delete `context_key` from the table or add the tag). Cosmetic.

---

### 6. Index-in-migration listing tests asymmetric across backends (Score: 20)

Only SQLite's migration-listing test asserts the V8 CREATE INDEX appears in the migration UpScript. MSSQL/MySQL/PG listing tests assert the V8 ADD COLUMN + columns but not the embedded CREATE INDEX. The index IS present and idempotent in all four catalog sources (verified directly) and the builder-side index is tested everywhere, so the upgrade-path index is real — just not pinned by a string assertion on three backends.

**Recommendation**: Add a V8-UpScript CREATE INDEX assertion to the three listing tests for parity. Low.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 1 |
| 0-49 (Low) | 5 |

**Total findings**: 6
**Findings at or above threshold (60)**: 1

## Notes on what was verified clean (no findings)

- **Write-path gate (AC10)**: memoized `bool?` probe correctly gates the write in both base classes; sync/async share one field with harmless idempotent races; `InitAddDbCommand`/`CreateAddCommand` branch on the same memoized value so parameter list and SQL columns stay consistent; absent column → plain `AddCommand`, byte-for-byte unchanged. The `AddAsync`→`async` conversion is necessary and improves span lifetime (no regression). All 5 relational backends have real-DB AC10 backward-compat tests via `*LegacySeeder` (pre-feature schema, no mocks, no `[Skip]`).
- **Probe SQL parity**: all 5 backends use `SELECT 1 ...` consumed via `dr.HasRows`; PG correctly uses lowercase `causationid` (folding) and DDL emits unquoted; all handle a missing TABLE gracefully (OBJECT_ID/to_regclass→null, information_schema/pragma→empty). A claimed PG `to_regclass` bug was investigated and refuted.
- **Core behaviour (Tasks 6-16)**: CausationId set before `base.Handle`; Replay early-returns without re-handling; null-outbox is a safe no-op via pattern-match guard; async path captures `Context.Span` into a local before awaits (no thread-affinity bug); telemetry gated on `Span != null` AND `InstrumentationOptions.Brighter`; event names exact.
- **Schema (19a/21a)**: all ADD COLUMN idempotent-guarded; CREATE INDEX present and idempotent in BOTH builder and V8 migration on every backend; Spanner `VLatestInbox 2→3`/`VLatestOutbox 7→8`; drift parity tests live (PG inbox carve-out `1→2`, index asserted separately).
- **DynamoDB GSI honesty (AC11)**: `=> true` replaced with memoized `DescribeTableAsync` GSI check on both V3 and V4; sync wrapper matches store-wide `.GetAwaiter().GetResult()` pattern; NoSQL inboxes + Mongo/Firestore correctly stay `=> true`.
- **TDD**: each behavioral commit bundles its `When_..._should_...cs` test; naming convention followed; no committed binaries.

The single at-threshold finding (#1) is the startup-fragility of the live capability probe in the validation path — worth addressing before merge, but narrowly scoped to the new Replay opt-in configuration.
