# Review: tasks — replay-matching-outbox-events-when-inbox-has-already-seen

**Date**: 2026-06-22
**Threshold**: 60
**Verdict**: NEEDS WORK

2 findings at or above threshold 60. Address these before approving.

*Note: this phase is already approved — confirmatory review of the 2026-06-22 backward-compatibility tasks (24–27). Finding #1 (Spanner old-schema gap) is the one that warrants re-opening Task 24, because as written the Spanner coverage is not achievable with existing test infrastructure.*

## Findings

### 1. Task 24 mandates Spanner old-schema coverage but no Spanner legacy seeder exists, and the task doesn't say to build one (Score: 78)
Task 24 lists Spanner in its backend matrix and requires provisioning a table with pre-feature DDL (no `CausationId` column) — not the current builder. Pre-feature provisioning is achievable for MsSql/MySql/Postgres/Sqlite via existing `*LegacySeeder` helpers (8 files under `tests/.../BoxProvisioning/Legacy/`). **No equivalent Spanner legacy seeder exists**, and the live `Spanner{Inbox,Outbox}Builder`/provisioner now always emit the column. So a Spanner "new code, old schema" test requires hand-rolled legacy DDL that does not yet exist, and Task 24 neither acknowledges this nor instructs the author to add a `Spanner{Inbox,Outbox}LegacySeeder`.
**Evidence**: `find tests -iname "*LegacySeeder*"` → 8 files for MsSql/MySql/Postgres/Sqlite only; none for Spanner. Task 24 lists Spanner with no carve-out or seeder instruction.
**Recommendation**: Either (a) add an explicit sub-bullet to build `Spanner{Inbox,Outbox}LegacySeeder` helpers, or (b) carve Spanner out of the old-schema matrix. Also fold in the known Spanner emulator / per-store-run-isolation constraint.

### 2. Task 24 bundles 5 backends × 2 store types (10 surfaces) plus an end-to-end validation assertion under a single approval gate (Score: 64)
Task 24 has two `/test-first` lines (outbox + inbox) but each must cover all five backends against real containers, plus assert `SupportsCausationTracking()==false` and that `ValidatePipelines` rejects a Replay pipeline. Tasks 19/21 were deliberately split per-backend (19b–19f, 21c–21g). Task 24 reverses that into two monolithic gates spanning 10 surfaces. Mitigating: the production fix is a single shared change in the two base classes — but the *tests* are per-backend container tests (different seeders, different probe SQL).
**Evidence**: tasks.md TEST bullets vs. the split rationale ("the behavioral interface work is genuinely per-backend and each backend gets its own `/test-first` cycle").
**Recommendation**: Keep the single shared IMPLEMENT bullet, but split the TEST bullet per backend (matching 19/21). The first backend drives the shared fix test-first; the rest are characterization once the base-class change lands.

### 3. Task 24 does not call out the first-deposit-extra-round-trip / memoization-concurrency traps the ADR raises (Score: 52)
The ADR Write-path gate flags the memoization edge case and one-shot-lazy semantics. Task 24's IMPLEMENT bullet says to memoize "per store instance (nullable bool field)" and share the cache — good — but does not require a test for probe-happens-once or thread-safety of the memo under concurrent first deposits. The race is benign (double-probe, same result).
**Evidence**: ADR edge-case paragraph; Task 24 has no probe-count/concurrency assertion.
**Recommendation**: Note that the memo's "probe at most once under normal flow" is acceptable-to-race (worst case a duplicate probe, same result); optionally a test asserting the probe is not issued on every deposit.

### 4. Task 24 does not explicitly assert the migrated-schema (column-present) path still writes CausationId (Score: 48)
Task 24's RED tests only exercise the old-schema (absent) path. The opposite-direction risk: a memo that wrongly returns "absent" on a migrated table would silently stop writing CausationId. The 19c–f/21c–g suites would catch a hard failure but maybe not a silent "wrote NULL".
**Evidence**: Task 24 covers only the absent-column direction; no positive-direction regression assertion.
**Recommendation**: Add a "column present → still writes CausationId" regression assertion (or note 19c–f/21c–g cover it).

### 5. Minor: dependency list omits the test-infrastructure dependency on legacy seeders (Score: 30)
Task 24 `Depends On: 19b–19f, 21c–21g, 14`. The test approach requires the `*LegacySeeder` helpers (and a new Spanner one). Otherwise deps are correct: 25→22, 26→20/22, 27→24/25/26; AC10↔24, AC11↔25 trace cleanly; Task 25 covers both DynamoDB and DynamoDB.V4; Task 26 is correctly characterization. No scope creep found.
**Evidence**: tasks.md Task 24 deps; implicit reliance on Legacy seeders.
**Recommendation**: Note "uses existing `*LegacySeeder` test helpers" so the infra dependency is explicit.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 1 |
| 50-69 (Medium) | 2 |
| 0-49 (Low) | 2 |

**Total findings**: 5
**Findings at or above threshold (60)**: 2

**Verified premises (all confirmed against code):** outbox `Add` sync/async gate on `CausationQueries is not null` (RelationDatabaseOutbox.cs:82,172) and inbox likewise (RelationalDatabaseInbox.cs:82,181); `InitAddDbCommand` uses `CausationQueries?.AddCausationCommand ?? queries.AddCommand` (:1279); `SupportsCausationTracking()` is un-memoized (:983); BulkAdd uses `queries.BulkAddCommand` with no causation (bulk scope unchanged). Both DynamoDB outboxes hardcode `=> true` (DynamoDB:551, V4:564); replay queries `IndexName = _configuration.CausationIndexName`; `IAmazonDynamoDB _client` in scope so `DescribeTableAsync` is feasible. Live builders always emit `CausationId` with no suppression overload — confirming pre-feature provisioning must use the `*LegacySeeder` helpers.
