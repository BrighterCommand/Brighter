# Review: design (revision 2) — 0027-box-schema-versioning-and-migrations

**Date**: 2026-04-22
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. All 8 prior findings resolved. Two trivial cosmetic items (< 30) addressed during this revision pass.

## Prior findings status

| Prior # | Title | Status |
|---------|-------|--------|
| 1 | SQLite multi-statement UpScript | **Resolved** — §4 adds `IdempotencyCheckSql` (nullable) as a new interface member; §5's SQLite row is now a clean two-column protocol (`IdempotencyCheckSql` = `SELECT COUNT(*) FROM pragma_table_info(...)`; `UpScript` = plain `ALTER TABLE`). §4 explicitly states MSSQL/Postgres/MySQL embed the check in UpScript and leave `IdempotencyCheckSql` null. `UpScript` remains a plain single-statement string across all backends. |
| 2 | SQLite concurrency (file-lock) | **Resolved** — §3 Fresh path and §5 "Concurrency attribution" table both specify `BEGIN IMMEDIATE TRANSACTION` at runner entry with `SQLITE_BUSY` retry. The dedicated paragraph after the table explicitly contrasts `BEGIN IMMEDIATE` vs `BEGIN DEFERRED` and names the trade-off (long chains block readers; acceptable for dev/test). Whole-chain guarantee column = "Yes". |
| 3 | Spanner missing discriminator | **Resolved** — §6 second bullet now says: "apply the same discriminator gate as §2 — query information_schema.columns for HeaderBag (outbox) or CommandBody (inbox). If absent: throw ConfigurationException…". Brings Spanner to parity with the other backends. |
| 4 | Inbox housekeeping undocumented | **Resolved** — §1 now has a distinct "Inbox V1 housekeeping" subtable with entries per backend (MSSQL `Id BIGINT IDENTITY(1,1) + PRIMARY KEY (Id)`; Postgres `PRIMARY KEY (CommandId, ContextKey)`; MySQL `PRIMARY KEY (CommandId)`; SQLite `PRIMARY KEY (CommandId) via CONSTRAINT PK_MessageId`). Verified against `SqlInboxBuilder.cs:36-43`, `MySqlInboxBuilder.cs:43`, `SqliteInboxBuilder.cs:35`, `PostgreSqlInboxBuilder.cs:42` — all accurate. |
| 5 | Drift test vague | **Resolved** — "Technology choices" now contains a concrete sketch: per-backend `GetExpectedColumns(tableName, binaryPayload)` helper in the test project, regex-parses DDL template, asserts `migrations.Last().LogicalColumns ∪ housekeeping(box, backend) == expected` with a named `housekeeping` lookup sourced from §1's tables. Variant handling (Text default; Binary/JSON separate) is called out. Comparer per-backend is specified. Implementable. |
| 6 | Drift test pre-existing tense | **Resolved** — "Implementation approach" step 1 now says "**add** the builder-vs-latest-migration-list drift test (new test infrastructure per 'Technology choices')"; "Technology choices" says "This is new test infrastructure, shipped in step 1 of the implementation sequence — not an update to a pre-existing test." Tense reconciled. |
| 7 | Error handling mid-chain | **Resolved** — new §5a "Error handling and mid-chain failures" added with per-backend transactional-wrapping table, explicit MySQL implicit-commit-per-DDL callout, history-row timing, recovery invariant (`MAX(V)` resume), and exception taxonomy. Covers AC-17 concern. |
| 8 | Discriminator-gate dual 0 returns | **Resolved** — §2 now returns three distinct values: `-1` (no discriminator), `0` (discriminator present, no version match), `V >= 1` (matched). §3 Bootstrap path throws two different `ConfigurationException` messages for the `-1` vs `0` cases. |

All 8 prior findings fully resolved.

## New findings

None at or above threshold. The revision's additions are internally consistent:

- The new `IdempotencyCheckSql` does not contradict §4's earlier "idempotency baked into UpScript" stance — §4 explicitly states MSSQL/Postgres/MySQL embed the check in UpScript *and* leave `IdempotencyCheckSql` null; SQLite uses the new member.
- §5a's MySQL claim ("MySQL issues an implicit commit on every DDL statement") is accurate per MySQL docs and matches the mid-chain-resumability recovery invariant.
- §5a's SQLite claim ("SQLite supports transactional DDL. Rollback is atomic.") is correct for DDL wrapped in `BEGIN IMMEDIATE`/`COMMIT`.
- §6 Spanner discriminator gate mirrors §2 cleanly — no divergent semantics introduced.
- The drift-test helper placement (test project, regex parse) avoids polluting production builders and is implementable against the verified DDL shapes.

### Cosmetic items (addressed in-line; documented for traceability)

Two low-score drift items were surfaced by review and fixed in the same editing pass:

- **N1 (25)**: "Key components" bullet omitted `IdempotencyCheckSql` from the `IAmABoxMigration` extension list. Fixed: bullet now names all three new members.
- **N2 (22)**: §3 ASCII diagram's BOOTSTRAP lane didn't show the `-1`/`0`/`>=1` branches introduced in §2. Fixed: lane updated to show the three-outcome branching.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 0 |
| 0-49 (Low) | 0 (2 surfaced and fixed during revision) |

**Total findings**: 0 outstanding
**Findings at or above threshold (60)**: 0
