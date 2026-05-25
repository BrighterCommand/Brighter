# Review: tasks — 0027-box-schema-versioning-and-migrations

**Date**: 2026-05-06
**Threshold**: 60
**Verdict**: PASS

> This phase is already approved. Findings are informational — consider whether any warrant re-opening the phase.

No findings at or above threshold 60. Consider addressing lower-scored items.

The three prior findings ≥60 (Item O signature break, Item P bootstrap RED gap, Item Q Spanner coverage + runtime-rejection) are all closed. Spot-check verifications:

- Phase 9 preamble replaces the prior "no source-breaking changes" sentence with explicit "spec 0027 has not yet shipped" framing and references the Phase 8 `release_notes.md` source-break entries as the ship boundary. Discoverable for a contributor reading Phase 9 in isolation.
- Numbering note defines `#46-3 #N` with a GitHub comment reference.
- Item P split into P-fresh and P-bootstrap, each with a dedicated concurrent RED test. P-bootstrap's `SpannerOutboxBuilder.GetDDL` setup is sound — `SpannerOutboxBuilder` is `public class` in `src/Paramore.Brighter.Outbox.Spanner/SpannerOutboxBuilder.cs`. Visibility-semantics paragraph (commit timestamp at `SpannerBoxMigrationRunner.cs:216`) is in Item P preamble.
- Item Q explicitly extends call sites to include Spanner runner's `MigrateAsync` and bumps to 6 RED facts (4 relational + 1 Spanner + 1 helper). Glob confirmed only 4 relational `*Migrations.cs` files exist.
- Item R sub-bullet commits to updating `IMySqlAdvisoryLock.cs:62-63` XML doc and adding ADR §5b material — both file:lines verified.
- Item S replaces `Stopwatch` with `TimeProvider` + `FakeTimeProvider`, references `Directory.Packages.props:72` and `:92` (both verified). 9 test csproj files already reference `Microsoft.Extensions.TimeProvider.Testing`; `Paramore.Brighter.PostgresSQL.Tests.csproj` does not (verified — Item S correctly notes this and adds the package reference).

## Findings

### 1. Item S's "Fix uses Microsoft.Bcl.TimeProvider" wording could mislead about production-side package addition (Score: 50)

Item S says: *"Fix uses `Microsoft.Bcl.TimeProvider` (already in `Directory.Packages.props:72`):"* followed by three production-code bullets and a test-project package addition. `Microsoft.Bcl.TimeProvider` is the .NET 8 *polyfill* for older TFMs; the abstract `TimeProvider` type itself lives in `System.Threading` and is native to .NET 8+. The production project `Paramore.Brighter.BoxProvisioning.PostgreSql.csproj` uses `BrighterCoreTargetFrameworks` = `net8.0;net9.0;net10.0` (`src/Directory.Build.props:45`), so it can use `TimeProvider` without referencing the polyfill package.

Item S does not state "no production-side package reference needed because TFMs are net8+". An implementer reading the lead sentence could reasonably add `<PackageReference Include="Microsoft.Bcl.TimeProvider" />` to `Paramore.Brighter.BoxProvisioning.PostgreSql.csproj` — unnecessary clutter, and inconsistent with how the spec only adds the *testing* package to `Paramore.Brighter.PostgresSQL.Tests.csproj`.

**Evidence**: tasks.md Item S ("Fix uses `Microsoft.Bcl.TimeProvider` (already in `Directory.Packages.props:72`)"); `src/Directory.Build.props:45` (`BrighterCoreTargetFrameworks = net8.0;net9.0;net10.0`); production `.csproj` does not currently reference `Microsoft.Bcl.TimeProvider` (consistent with native availability on net8+).

**Recommendation**: Replace "Fix uses `Microsoft.Bcl.TimeProvider` (already in `Directory.Packages.props:72`)" with "Fix uses `System.TimeProvider` (native on net8+, which covers all `BrighterCoreTargetFrameworks` targets — no production-side package reference needed). The polyfill `Microsoft.Bcl.TimeProvider` is centrally pinned at `Directory.Packages.props:72` if a future netstandard target requires it."

---

### 2. Item R's "ADR §5b amendment row" is structurally imprecise (Score: 45)

Item R sub-bullet says: *"Add an ADR 0057 §5b amendment row (mirror Item N's MSSQL per-return-code mapping table) showing MySQL's two distinguishable error codes."*

ADR `0057-box-schema-versioning-and-migrations.md` §5b's "Interface shape per backend" is a 3-row table (Postgres, MySQL, MSSQL). Item N's MSSQL per-return-code mapping is enumerated **inside the MSSQL row's "Diagnostic value" column** (cell content `-1 TimeoutException, -2 OperationCanceledException, -3 MigrationLockDeadlockException (new), -999 ArgumentException`) — not as a separate row. So "add an amendment row" is ambiguous: should the implementer (a) add a new row to §5b's table — which would create a duplicate MySQL row — or (b) update the existing MySQL row's "Diagnostic value" column to enumerate the two codes, mirroring how MSSQL is enumerated? Only option (b) matches the existing pattern.

A future contributor implementing Item R could read this either way. Item N's commit (`2f727772d`) would resolve the ambiguity, but the spec text alone does not.

**Evidence**: tasks.md Item R sub-bullet ("Add an ADR 0057 §5b amendment row"); `docs/adr/0057-box-schema-versioning-and-migrations.md` §5b (3-row table with MSSQL per-code mapping enumerated inside one row's cell, not as a separate row).

**Recommendation**: Replace "Add an ADR 0057 §5b amendment row" with "Update the §5b table's MySQL row 'Diagnostic value' cell to enumerate the two distinguishable codes (mirror the MSSQL row's per-code shape): `0` (lock not acquired within timeout) → `TimeoutException`; `NULL` (server-side error) → `MySqlAdvisoryLockException` (new)."

---

### 3. Item Q's relational runner-entry AssertSafe is GREEN-only (Score: 35)

Item Q lists three call-site categories: (1) each relational backend's `*OutboxMigrations.All(...)` and `*InboxMigrations.All(...)` factory entry, (2) each relational runner's `MigrateAsync` entry, (3) Spanner runner's `MigrateAsync` entry. Six RED facts target: 4 factory-entry tests (one per relational backend, name `When_{backend}_migrations_are_built_with_an_unsafe_table_name_they_should_throw`), 1 Spanner runner-entry test, 1 helper-direct test.

The 4 relational runner-entry call sites have no direct RED facts. The factory-entry tests cover them transitively only because the factory throws first when `migrations` are constructed via `*Migrations.All(...)`. A caller who bypasses the factory (constructs `IAmABoxMigration` instances manually with a valid table name, then calls `MigrateAsync` with a different unsafe `tableName`) exercises only the runner-entry guard — which has no test. This is defense-in-depth and the factory path is the documented one, so the gap is small.

**Evidence**: tasks.md Item Q (3 call-site categories vs 6 RED facts; 4 relational fact names target "migrations are built with an unsafe table name" = factory entry).

**Recommendation**: Either (a) add one sentence to Item Q stating "the relational runner-entry `AssertSafe` is defense-in-depth for callers bypassing `*Migrations.All(...)`; the factory-entry RED facts cover the documented path. A 7th fact for the runner-entry is optional," OR (b) add a 5th relational fact targeting the runner-entry directly. Option (a) keeps the count at 6 and is consistent with the "defence in depth" framing already used in Item Q.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 1 |
| 0-49 (Low) | 2 |

**Total findings**: 3
**Findings at or above threshold (60)**: 0
