# Bugfix: SQLite/MSSQL/MySQL inbox V1 baseline wrongly includes ContextKey

**Status**: Verified

## Symptom

The SQLite, MSSQL and MySQL inbox migration catalogs serve a `V1HistoricalDdl` as `V1.UpScript` that
already contains `ContextKey`. Their class remarks say these inboxes were "born past V1" (first
shipped with `ContextKey`), citing 2019 rename commits as the first shipped shape (`695522367` for
SQLite, `b7f96957b` for MSSQL/MySQL). Three drift tests pin that `V1.UpScript` contains `ContextKey`,
and ADR 0057 §3 repeats the claim.

Expected: all three inboxes shipped years before `ContextKey`, which `787c31c52` (2018-10-09,
"once-per-context") added. The owner confirms: "There was no ContextKey on V1." Each `V1.UpScript`
should be the pre-ContextKey DDL, and V2 ("Add ContextKey column", `SourceReference: "787c31c52"`) a
real catch-up migration.

History, from `git show <sha>:<path>`:

| Inbox | First builder | First DDL | Catalog cites |
|---|---|---|---|
| SQLite | `2af33f3e7` (2016-08-05), `SqlLiteCommandStoreBuilder.cs` | `CommandId uniqueidentifier PK, CommandType nvarchar(256), CommandBody ntext, Timestamp dateTime` | `695522367` (2019 rename) |
| MSSQL | `3cf37bbe9` (2015-06-04), `SqlCommandStoreBuilder.cs` | same four columns, PK on `CommandId`; `[Id] BIGINT IDENTITY` PK added later in `f49d8c560` (2017-06-30) | `b7f96957b` (2019 rename) |
| MySQL | `133e5a4aa` (2017-09-07), `MySqlCommandStoreBuilder.cs` | `CommandId CHAR(36)`, `CommandType VARCHAR(256)`, `CommandBody TEXT`, `Timestamp TIMESTAMP(4)`, PK `CommandId` | `b7f96957b` (2019 rename) |
| Postgres (control) | `79100f509` (2021-02-24) | shipped with `ContextKey` | correct |

`787c31c52` adds exactly one line, the `ContextKey` column, to each of the three builders. The shape at
`787c31c52^` is, per backend, the current `V1HistoricalDdl` minus its `ContextKey` line (MSSQL: the
post-`f49d8c560` shape with `Id`).

Reproduction: `new SqliteInboxMigrationCatalog().All(config)[0].UpScript` (or the MSSQL/MySQL
equivalent) contains `ContextKey`; the tests below assert it.

**Runtime impact: none on a production path.** No production code executes or compares against a
V1.UpScript:
- Fresh path runs `FreshInstallDdl` (`src/Paramore.Brighter.BoxProvisioning/SqlBoxMigrationRunner.cs:219-227`,
  `src/Paramore.Brighter.BoxProvisioning.Sqlite/SqliteBoxMigrationRunner.cs:200`).
- Bootstrap stamps the detected version (≥ 1; 0 and -1 throw) and applies only `Version > detected`
  (`SqliteBoxMigrationRunner.cs:234-240`, `MsSqlBoxMigrationRunner.cs:323-333`, `MySqlBoxMigrationRunner.cs:188-196`).
- Normal path is taken only when history exists, so `maxVersion ≥ 1` and V1 is skipped
  (`SqlBoxMigrationRunner.cs:229-241`; `SqliteBoxMigrationRunner.cs:252-257`,
  `MsSqlBoxMigrationRunner.cs:345-349`, `MySqlBoxMigrationRunner.cs:207-212`).
- Detection uses `migrations[0].LogicalColumns` (superset check), not UpScript
  (`MsSqlBoxDetectionHelper.cs:182-188`).

The defect is in the recorded history, docs, and the tests that pin it.

## Suspected Location

- `src/Paramore.Brighter.BoxProvisioning.Sqlite/SqliteInboxMigrationCatalog.cs` — remarks `:34-36`
  (cites `695522367`), "Born-past-V1 asymmetry" `:44-52`, `V1HistoricalDdl` `:64-73` (`:72` ContextKey),
  V1 migration `:97-101`.
- `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlInboxMigrationCatalog.cs` — `:35-36`, `:41-48`,
  `V1HistoricalDdl` `:67-81` (`:78` ContextKey), V1 migration `:116-120`.
- `src/Paramore.Brighter.BoxProvisioning.MySql/MySqlInboxMigrationCatalog.cs` — `:34-35`, `:40-48`,
  `:55-58` (also wrongly says V1.UpScript is "used by the bootstrap and normal-update branches"),
  `V1HistoricalDdl` `:78-91` (`:88` ContextKey), V1 migration `:130-134`.
- `docs/adr/0057-box-schema-versioning-and-migrations.md:167` ("all four inboxes are 'born past V1'")
  and `:219` (lists MSSQL/MySQL/SQLite inboxes as born-past-V1). The ADR's own §1 table (`:78-81`)
  is already right, so §3 contradicts §1.
- Tests pinning the wrong claim:
  - `tests/Paramore.Brighter.Sqlite.Tests/BoxProvisioning/When_sqlite_outbox_and_inbox_builders_are_compared_to_latest_migration_columns_they_should_have_identical_expected_column_sets.cs:172-193`
  - `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/When_mssql_inbox_builder_is_compared_to_v3_migration_columns_it_should_have_identical_expected_column_set.cs:73-95`
  - `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning/When_mysql_outbox_and_inbox_builders_are_compared_to_latest_migration_columns_they_should_have_identical_expected_column_sets.cs:167-187`
- Legacy seeders already seed a pre-ContextKey V1 and need no change:
  `tests/…Sqlite.Tests/BoxProvisioning/Legacy/SqliteInboxLegacySeeder.cs:44-51`,
  `tests/…MSSQL.Tests/BoxProvisioning/Legacy/MsSqlInboxLegacySeeder.cs:41-50`,
  `tests/…MySQL.Tests/BoxProvisioning/Legacy/MySqlInboxLegacySeeder.cs:43-51`.

## Root-Cause Hypothesis

When V1.UpScript was split from `FreshInstallDdl`, each SQLite/MSSQL/MySQL "historical" V1 literal was
taken from the builder as it stood at the 2019 CommandStore→Inbox rename commits — after `787c31c52`
had added `ContextKey`. The renames hide the earlier history from a plain `git log`, so the rename
commit was taken as "first shipped", and the "born past V1" story was then written into the catalog
remarks, ADR 0057 §3 and three tripwire tests.

Falsifiable: if `git show 787c31c52^:<builder>` for each backend has no `ContextKey` and otherwise
matches `V1HistoricalDdl`, the V1 literals are historically wrong. (Pre-checked in triage: it does, for
all three.)

Everything else is already consistent with a true pre-ContextKey V1: `V1.LogicalColumns` has no
`ContextKey` (SQLite `:57-58`, MSSQL `:60-61`, MySQL `:71-72`), and each V2 is idempotent on
`ContextKey` (SQLite `pragma_table_info` `:109`, MSSQL `IF COL_LENGTH(...) IS NULL` `:150-152`, MySQL
`information_schema` guard `:161-169`).

**Proposed fix — UNVERIFIED, to be proven or refuted in /bugfix:confirm:**
1. Remove `ContextKey` from the three `V1HistoricalDdl` literals, using the `787c31c52^` shape.
   Decision needed for MSSQL: the 2015 shape (no `Id`, PK on `CommandId`, `3cf37bbe9`) or the
   2017 shape (`Id` identity PK, `f49d8c560`). Both are pre-ContextKey; `Id` is outside
   LogicalColumns, so detection is unaffected either way.
2. Correct the commit citations and remarks in all three catalogs, including MySQL `:55-58`.
3. Flip the three tripwire tests to assert `ContextKey` is absent from V1.UpScript.
4. ADR 0057 `:167` and `:219`: only the Postgres outbox and Postgres inbox are born past V1.

Not in scope: the Postgres catalog cites `1cdc04b60` (Nov 2023) where ADR §1 cites `79100f509`
(Feb 2021) — a separate citation discrepancy.

## Confirmed Root Cause

**Verdict: CONFIRMED.** Commit `be1ae9dcb3` (2026-05-19, "V1 historical baseline DDLs in 8 catalogs")
took the MSSQL/MySQL inbox V1 literals from `b7f96957b` and the SQLite inbox V1 literal from
`695522367` (both March 2019). Both are pure rename commits (`git log --follow` stops there; builder
diff is a 100% rename), so they were mistaken for first ship. `787c31c52` (2018-10-09) had already added
exactly one line — `ContextKey` — to each builder, so `ContextKey` ended up in V1, and the "born past
V1" story was written into the catalog remarks, ADR 0057 §3 and three tripwire tests.

The same mistake — a rename commit taken as first ship — also produced a wrong **Postgres outbox** V1
(see Scope Notes).

## Evidence

- [x] Code-trace:
  1. Earliest builders, all without `ContextKey`: MSSQL `3cf37bbe9` (2015-06-04, released v5.0.323),
     SQLite `2af33f3e7` (2016-08-05, v7.0.137), MySQL `133e5a4aa` (2017-09-07, v7.2.10).
     `787c31c52` (first in v8.0.37) adds the `ContextKey` line to each. No other SQL inbox shipped with
     `ContextKey` at first ship except Postgres (`79100f509`, 2021) — correctly born with it.
  2. `787c31c52^` builder DDL equals today's `V1HistoricalDdl` apart from the `ContextKey` line and
     table-name quoting: SQLite `SqliteInboxMigrationCatalog.cs:66-73` (`:72`), MSSQL
     `MsSqlInboxMigrationCatalog.cs:69-81` (`:78`, incl. `Id BIGINT IDENTITY` PK), MySQL
     `MySqlInboxMigrationCatalog.cs:80-91` (`:88`).
  3. No production code reads V1.UpScript: fresh path uses `FreshInstallDdl`
     (`SqlBoxMigrationRunner.cs:223`); bootstrap skips `Version <= detected` (≥ 1;
     `MsSqlBoxMigrationRunner.cs:323-326`, `SqliteBoxMigrationRunner.cs:234-238`,
     `MySqlBoxMigrationRunner.cs:188-191`); normal path skips `Version <= maxVersion` with
     `maxVersion ≥ 1` (`SqlBoxMigrationRunner.cs:434`); detection reads only
     `migrations[0].LogicalColumns`; Spanner uses builders directly. No test executes an UpScript;
     legacy seeders carry their own DDL.
- [x] Red repro (pure logic): `Assert.DoesNotContain("ContextKey", new XInboxMigrationCatalog().All(config)[0].UpScript, StringComparison.OrdinalIgnoreCase)`
  fails today for SQLite, MSSQL and MySQL. The current tripwires assert the opposite:
  `…Sqlite.Tests/BoxProvisioning/When_sqlite_outbox_and_inbox_builders_…cs:189`,
  `…MSSQL.Tests/BoxProvisioning/When_mssql_inbox_builder_…v3…cs:93`,
  `…MySQL.Tests/BoxProvisioning/When_mysql_outbox_and_inbox_builders_…cs:184`.

**Suggested-fix assessment: CONFIRMED, refined.**
- MSSQL V1 shape: use `787c31c52^` (with the `Id` identity PK, `f49d8c560`, shipped from v7.1.0; the
  MSSQL DDL script had `Id` from `5e09d04792`, 2016-05). A pre-ContextKey upgrader most plausibly has
  `Id`; the 2015 no-`Id` shape is worth one line in the remarks. `Id` affects nothing (detection is a
  LogicalColumns superset check; seeders are independent).
- `SourceReference` data needs no change: V1 has none (tests pin it null), V2 is already `"787c31c52"`.
- Flip the three tripwire asserts and correct their comments; fix MySQL remark `:55-58` (V1.UpScript is
  not used by bootstrap or normal paths — ADR 0057 `:165` already says so).

## Scope Notes

- **Postgres outbox — same defect (verified).** `PostgreSqlOutboxMigrationCatalog.cs` (remarks `:36-48`,
  literal `:77-90`) cites `3c30343fa` (2019-07-10) as first ship and says it was "born with Dispatched".
  `3c30343fa` renamed `PostgreSqlMessageStoreBuilder.cs` → `PostgreSqlOutboxBuilder.cs` *and* added
  `Dispatched timestamptz NULL`. The Postgres outbox first shipped in `b1f880367` (2017-08-24) without
  `Dispatched`. Also affected: tripwire
  `…PostgresSQL.Tests/BoxProvisioning/When_postgres_outbox_and_inbox_builders_…cs:84-101`
  (`Assert.Contains("Dispatched", v1.UpScript)`), and ADR 0057 `:167`/`:219` ("PostgreSQL outbox …
  born past V1"). `PostgreSqlOutboxLegacySeeder.cs:64-65` already treats V2 as adding `dispatched`.
- **Postgres inbox citation only:** `79100f509` (ADR §1 `:81`) is right; `1cdc04b60`
  (`PostgreSqlInboxMigrationCatalog.cs:37,43,59`, test comment `When_postgres_outbox_and_inbox_builders_…cs:181-182`)
  was a rename with unchanged DDL. The V1 literal itself is correct.
- **MSSQL/SQLite/MySQL outbox citations** (`695522367`) are imprecise but logically harmless: true
  first ships MSSQL `3cf37bbe9`, SQLite `2af33f3e7`, MySQL `3a80d74e20`; none had `Dispatched`, so
  "pre-Dispatched" holds. Citation fix only (e.g. `SqliteOutboxMigrationCatalog.cs:57`).
- Other places repeating the inbox claim: only ADR 0057 `:167`, `:219`, the three inbox catalogs and
  the three tripwire test comments. Specs 0027 already assume a pre-ContextKey V1.
- **Approved scope (owner, at the Confirm gate):** the three inboxes (SQLite, MSSQL, MySQL) **plus the
  Postgres outbox** V1 DDL, remarks, tripwire tests and ADR 0057 `:167`/`:219`; plus the Postgres inbox
  citation (`1cdc04b60` → `79100f509`) and the Postgres outbox citation. The MSSQL/SQLite/MySQL outbox
  first-ship citations are **out of scope** (recorded only).
- Cosmetic, out of scope: MSSQL inbox V1 literal uses unqualified `[{0}]` while V2/V3 use
  `[schema].[table]`; never executed.

## Regression Test

The four tripwire tests that pinned the wrong history are corrected in place (renamed, comment block
replaced by AAA markers, the column assertion flipped to `Assert.DoesNotContain`). All four were
observed RED with `Assert.DoesNotContain() Failure: Sub-string found`; they are pure logic over the
catalogs and need no containers.

- `tests/Paramore.Brighter.Sqlite.Tests/BoxProvisioning/When_sqlite_outbox_and_inbox_builders_are_compared_to_latest_migration_columns_they_should_have_identical_expected_column_sets.cs`
  — `SqliteInboxBuilderDriftTests.When_sqlite_inbox_v1_upscript_is_inspected_it_should_carry_the_pre_contextkey_historical_baseline`
- `tests/Paramore.Brighter.MSSQL.Tests/BoxProvisioning/When_mssql_inbox_builder_is_compared_to_v3_migration_columns_it_should_have_identical_expected_column_set.cs`
  — `MsSqlInboxBuilderDriftTests.When_mssql_inbox_v1_upscript_is_inspected_it_should_carry_the_pre_contextkey_historical_baseline` (both payload modes)
- `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning/When_mysql_outbox_and_inbox_builders_are_compared_to_latest_migration_columns_they_should_have_identical_expected_column_sets.cs`
  — `MySqlInboxBuilderDriftTests.When_mysql_inbox_v1_upscript_is_inspected_it_should_carry_the_pre_contextkey_historical_baseline`
- `tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning/When_postgres_outbox_and_inbox_builders_are_compared_to_latest_migration_columns_they_should_have_identical_expected_column_sets.cs`
  — `PostgreSqlOutboxBuilderDriftTests.When_postgres_outbox_v1_upscript_is_inspected_it_should_carry_the_pre_dispatched_historical_baseline` (both payload modes)

The Postgres inbox tripwire (`…born_past_v1…with_contextkey_and_composite_pk`) is correct and unchanged.

## Fix

Each V1 historical literal is now the builder DDL as it stood just before the column was added
(`787c31c52^` for the inboxes, `3c30343fa^` for the Postgres outbox), and the docs tell the true history.

- `src/Paramore.Brighter.BoxProvisioning.Sqlite/SqliteInboxMigrationCatalog.cs`,
  `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlInboxMigrationCatalog.cs`,
  `src/Paramore.Brighter.BoxProvisioning.MySql/MySqlInboxMigrationCatalog.cs` — `ContextKey` removed
  from `V1HistoricalDdl` (MSSQL keeps the `Id` identity key, `f49d8c560`); "born past V1" remarks
  replaced with the real first-ship commits (`2af33f3e7`, `3cf37bbe9`, `133e5a4aa`); MySQL's remark
  that V1.UpScript is used by the bootstrap and normal-update branches corrected.
- `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlOutboxMigrationCatalog.cs` — `Dispatched`
  removed from `V1HistoricalDdl`; remarks cite first ship `b1f880367` and `3c30343fa` as the commit
  that added `Dispatched`.
- `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlInboxMigrationCatalog.cs` and the Postgres
  inbox tripwire comment — first-ship citation `1cdc04b60` → `79100f509`.
- `src/Paramore.Brighter.BoxProvisioning.MySql/MySqlOutboxMigrationCatalog.cs`,
  `src/Paramore.Brighter.BoxProvisioning.Sqlite/SqliteOutboxMigrationCatalog.cs` — remarks no longer call
  the Postgres outbox the born-past-V1 exception.
- `docs/adr/0057-box-schema-versioning-and-migrations.md` §3 (`:167`, `:219`) — only the Postgres inbox
  is born past V1.

Follow-up (owner request, same PR): the MSSQL/MySQL/SQLite outbox catalogs and their drift-test comments
cited rename commits (`695522367`, `44a405b79`) as first ship. Their V1 literals equal the builders at
`3c30343fa^`, so they now cite that, plus the real first ships (MSSQL `3cf37bbe9`, `Id` added in
`e6ff9e673`; MySQL `3a80d74e2`; SQLite `2af33f3e7`). The MSSQL outbox remark calling it "the only one of
the four" outboxes with a pre-V2 first ship is removed. SQLite's IdempotencyCheckSql references
"ADR 0057 §6" → §5; the provisioners' "(per ADR 0057 §6)" for "no schema concept" is stripped (the ADR
does not cover it).

Regression tests: all 6 cases green (SQLite 1, MSSQL 2, MySQL 1, Postgres 2).
