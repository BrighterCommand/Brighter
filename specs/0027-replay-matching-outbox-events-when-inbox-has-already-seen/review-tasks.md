# Review: tasks — 0027-replay-matching-outbox-events-when-inbox-has-already-seen

**Date**: 2026-06-17
**Threshold**: 60
**Verdict**: NEEDS WORK

This phase is already APPROVED; the following findings are INFORMATIONAL (BoxProvisioning re-review). Two grounded coverage gaps in the folded-in BoxProvisioning work would cause an incomplete/divergent implementation (drift-guard tests would go red). Both verified directly by the main agent.

## Findings

### 1. Spanner `VLatestOutbox`/`VLatestInbox` constants and their cross-backend drift guard are not in any task step (Score: 88)

Tasks 19/21 describe the Spanner path only as "add the column via the provisioner + builder; move Spanner's drift parity test." But `SpannerBoxMigrationRunner` hard-codes the chain length as `VLatestOutbox = 7` / `VLatestInbox = 2`, and a dedicated test (`When_spanner_v_latest_constants_are_compared_to_relational_catalogs_they_should_match_every_backend`) asserts these constants equal each relational catalog's `.Count`. When outbox catalogs bump V7→V8 (Task 21) and inbox catalogs bump (Task 19), these constants MUST become 8 and 3 — yet no task step mentions `SpannerBoxMigrationRunner`. This is a *different* test from the per-backend builder-vs-migration drift test the tasks cite.

**Evidence**: `SpannerBoxMigrationRunner.cs:136-137` `VLatestOutbox = 7; VLatestInbox = 2;` with an in-code comment instructing to bump them when a relational backend advances. Test: `tests/Paramore.Brighter.Gcp.Tests/Spanner/BoxProvisioning/When_spanner_v_latest_constants_are_compared_to_relational_catalogs_they_should_match_every_backend.cs`. **Confirmed by main agent.**

**Recommendation**: Add explicit Spanner sub-steps to Tasks 19/21: bump `VLatestInbox`/`VLatestOutbox` and keep the cross-backend constant test green (it lives in `Paramore.Brighter.Gcp.Tests`, separate from the builder-drift test).

---

### 2. Task 19 says inbox catalogs are "next after the current V2" — but the PostgreSQL inbox catalog is V1-only (Score: 85)

Task 19 instructs, for all four catalog inbox stores, "append a new migration version (next after the current V2)". This is **false for PostgreSQL**: its inbox catalog has a single V1 entry (whose V1 already includes `ContextKey`). For Postgres the new version is **V2**, not V3. The blanket "V2" premise does not match per-backend reality, and the Spanner cross-backend test has a deliberate Postgres carve-out (`PostgreSqlInboxMigrationCatalog().All().Count == 1`) that must be re-derived after the bump.

**Evidence**: `PostgreSqlInboxMigrationCatalog.cs` returns a single `Version: 1`. MsSql/MySql/Sqlite inbox are at V2. **Confirmed by main agent** (contradicts the design reviewer's "Postgres at V2" claim, which was wrong).

**Recommendation**: Reword Task 19 per-backend: MsSql/MySql/Sqlite inbox V2→V3; **Postgres inbox V1→V2**. After the change MsSql/MySql/Sqlite inbox count = 3, Postgres = 2 — so the Spanner carve-out math must be re-derived, not just bumped.

---

### 3. Drift-guard sequencing (version + builder + constants must land in one commit) is not stated (Score: 64)

The folded-in work introduces a hard ordering constraint left implicit: the catalog version bump, the live-builder DDL change, and (for Spanner) the `VLatest*` constant bump must be committed together, or the drift tests go red between commits. The ADR states "The new version and the builder change must move together" but this coupling never propagated into tasks.md, and it omits the Spanner constant.

**Evidence**: tasks.md Tasks 19/21 list catalog + builder + drift as parallel bullets with no "same commit" note; ADR §Schema Evolution point 3 states the coupling.

**Recommendation**: Add a note to Tasks 19/21: "Catalog version + live builder DDL + (Spanner) `VLatest*` constant must land in a single commit so the drift parity tests stay green."

---

### 4. Tasks 19 and 21 are over-broad for a single test-first session (Score: 58)

Task 19 now spans 4 catalog stores × (new version + `s_vN`/`Cumulative()` + live builder + builder-drift test) + a Spanner provisioner/builder/drift path + per-store tracking-interface code + base-test inheritance — plus the Spanner constants and Postgres-specific versioning. A single `/test-first` command cannot frame this as one behavior; much of it is structural/characterization work. DB integration tests here require a real database (Brighter rule).

**Evidence**: tasks.md Task 19 body enumerates 5 stores plus catalog/builder/drift/tracking concerns under one `/test-first` command.

**Recommendation**: Split Tasks 19/21 per backend, or split "schema evolution via BoxProvisioning" (structural) from "store implements tracking interface" (behavioral); note relational tests need a live DB container, not a mock.

---

### 5. ADR Decision Coverage row points only to Tasks 19/21, omitting the Spanner runner-constant and Postgres-version specifics (Score: 50)

The new coverage row "Schema evolution via BoxProvisioning → 19, 21" inherits the gaps in findings 1–2: it masks the `SpannerBoxMigrationRunner.VLatest*` constant dependency and treats all inbox stores uniformly despite Postgres being V1.

**Evidence**: tasks.md ADR coverage row; Summary rows otherwise match the bodies.

**Recommendation**: After fixing findings 1–2, update the row to reference the Spanner runner constant + per-backend inbox versioning.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 2 |
| 50-69 (Medium) | 3 |
| 0-49 (Low) | 0 |

**Total findings**: 5
**Findings at or above threshold (60)**: 3
