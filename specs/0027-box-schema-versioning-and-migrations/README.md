# Box Schema Versioning and Migrations

**Spec ID:** 0027
**Created:** 2026-04-22
**Branch:** `database_migration` (shared with spec 0023)
**Related:** [Spec 0023 Box Database Migration](../0023-box_database_migration/) — deferred finding R1 motivates this spec
**Related ADR:** [0053 Box Database Migration](../../docs/adr/0053-box-database-migration.md) — this spec extends it

## Summary

Replace the single-version `DetectCurrentVersionAsync` model introduced in spec 0023 with a proper version-per-schema-change migration chain. Spec 0023 treats every existing outbox/inbox table as if it were at version 1 of the current DDL, which silently breaks for any installation predating DataRef/SpecVersion (or WorkflowId/JobId, or the CloudEvents columns, or…). This spec introduces a `V1..V7` outbox migration chain and a `V1..V2` inbox migration chain derived from git archaeology of the builder files, plus a fresh-install fast path that bypasses the chain entirely.

## Scope

- **Outbox**: 7 logical versions × 4 relational backends (MSSQL, Postgres, MySQL, SQLite) = 28 migration objects
- **Inbox**: 2 logical versions × 4 relational backends = 8 migration objects
- **Spanner**: fresh-install only (no known production users; stamps current version directly)
- **Detection**: rewrite `DetectCurrentVersionAsync` to walk `V_latest..V1` returning first column-name-superset match
- **Runner**: extend to three paths — fresh / bootstrap / normal — with fresh-install bypassing the ALTER chain

## Decisions already agreed (inherited from Phase 0 discussion)

1. **V5 outbox (type change UNIQUEIDENTIFIER→NVARCHAR) folded into V4** — type changes that don't add columns are not migrated; pre-V5 tables stay with UNIQUEIDENTIFIER IDs and application code handles both (documented edge case).
2. **Fresh-install fast path** — `ProvisionAsync` runs current builder DDL directly and inserts a single synthetic history row marking latest version as applied. No V1..VN chain on fresh install.
3. **Spanner is degenerate** — no migration chain; fresh install only; always stamped at latest version. If Spanner adoption grows, a chain can be added retroactively.
4. **Cross-backend version numbering is uniform** — logical column additions are the same across all 4 relational backends even when backend-specific commits differed (e.g. Postgres got V4 14 months after MSSQL).
5. **Backend-specific housekeeping columns** (MSSQL `Id` PK, Postgres `Id` BIGSERIAL, MySQL `Created`/`CreatedID`) live inside each backend's V1 DDL — they don't participate in the logical version numbering.

## Status

- [x] Requirements (`requirements.md`) — drafted + approved 2026-04-22
- [x] Design (ADR 0057) — drafted, reviewed (NEEDS WORK → PASS after 2 revisions), approved 2026-04-22
- [x] Tasks (`tasks.md`) — drafted 2026-04-22, awaiting `/spec:review tasks` + `/spec:approve tasks`
- [ ] Implementation
- [ ] Review

## Archaeology evidence base (for requirements.md)

### Outbox — 7 logical versions

| V | Columns added | Commit | Date | PR |
|---|--------------|--------|------|-----|
| V1 | MessageId, Topic, MessageType, Timestamp, HeaderBag, Body (+ backend PK) | — | pre-2019 | — |
| V2 | + Dispatched | `3c30343fa` | 2019-07-10 | — |
| V3 | + CorrelationId, ReplyTo, ContentType | `79100f509` | 2021-02-24 | #1401 |
| V4 | + PartitionKey; widen NTEXT→NVARCHAR(MAX); binary variant introduced | `1cdc04b60` (MsSql/MySql/SQLite) / `cff67fd5e` (Postgres) | 2023-11 / 2025-01 | #2560 / #3464 |
| V5 | + Source, Type, DataSchema, Subject, TraceParent, TraceState, Baggage | `b740a68ed` | 2025-06-25 | #3633 |
| V6 | + WorkflowId, JobId | `0e79332f1` | 2025-08-10 | #3693 |
| V7 | + DataRef, SpecVersion | `d67dac947` | 2025-09-25 | #3790 |

Folded into other versions (not separate migrations):
- Type change MessageId/CorrelationId UNIQUEIDENTIFIER→NVARCHAR(255) (`fd71cc1bc` #3042 Mar 2024) — type-only; pre-change tables keep UNIQUEIDENTIFIER
- Binary payload variant (part of V4) — fresh-install choice only, not a migration target

### Inbox — 2 logical versions

| V | Columns added | Commit | Date |
|---|--------------|--------|------|
| V1 | CommandId, CommandType, CommandBody, Timestamp (+ backend PK) | — | 2015–2018 |
| V2 | + ContextKey | `787c31c52` | 2018-10-09 |

Folded (not migrations):
- CommandBody type widening (NTEXT→NVARCHAR(MAX), etc.) — fresh-install only
- CommandId UNIQUEIDENTIFIER→NVARCHAR(256) (#3042) — did not touch SQLite inbox; SQLite inbox keeps UNIQUEIDENTIFIER
- Payload-mode variants (Text/Binary/JSON/JSONB) — fresh-install choice only

### Spanner

Added post-V6 outbox / at V2 inbox (`22fe24fbc`, 2025-10-02). DataRef/SpecVersion added to Spanner outbox in this-session commit `50d6aee15`. No known production users — degenerate provisioner: fresh install only, stamped at latest version.
