# Current State — Specs 0023 (Box Database Migration) + 0027 (Box Schema Versioning)

Branch `database_migration`, 7 commits ahead of `origin/database_migration`. Two specs in flight on the same branch — spec 0027 exists specifically to address spec 0023's R1 finding (the biggest blocker) and ships alongside it.

## Work this session

### Spec 0023 code-review findings (from `specs/0023-box_database_migration/review-code.md`)

Priority order was R1..R6 plus R7-resolved. **Delivered: R3 and R6. Rerouted: R1 → spec 0027. Remaining: R2, R4, R5.**

| # | Score | Status | Commit / Disposition |
|---|-------|--------|---------------------|
| R1 | 85 | **Rerouted to spec 0027** — "V1Columns bootstrap breaks pre-DataRef/SpecVersion". Fixing this properly requires version-per-schema-change migration chain, not a one-line patch. Archaeology + design lives in spec 0027. |
| R2 | 78 | **Open** — TOCTOU race in bootstrap path. To be addressed inline or folded into spec 0027 implementation. ADR 0057 §3 mandates TOCTOU re-check inside the advisory lock. |
| R3 | 72 | **Done** — docs-only commit `297ca030f`. ADR 0053 §10 + tasks.md 0.1 + ADR Negative bullet updated to acknowledge netstandard2.0 blocks DIM; `SchemaName` as plain abstract is a source-breaking change, accepted. |
| R4 | 70 | **Open** — Spanner history INSERT unprotected. Covered by ADR 0057 §6: `SpannerBoxMigrationRunner` adds `IsMigrationAppliedAsync` gate before history INSERT. To be implemented alongside 0027. |
| R5 | 68 | **Open** — Payload-mode-mismatch tests only on MSSQL. Four per-backend test additions (PG, MySQL, SQLite, Spanner). Standalone work, not dependent on 0027. |
| R6 | 62 | **Done** — commit `0088abe54`. Renamed `sp_getapplock` placeholders to `@lockResourceName`/`@lockTimeoutMs` to disambiguate. |
| R7 | 60 | Resolved (review branch hygiene). |

Below-threshold findings (55/50/45): still deferred.

### Spec 0027 created: box-schema-versioning-and-migrations

- **Branch**: `database_migration` (shared with 0023 per user direction)
- **Driver**: spec 0023 R1 (bootstrap path silently breaks for pre-DataRef/SpecVersion tables — i.e. every existing Brighter install)
- **Scope**: proper version-per-schema-change migration chain derived from git archaeology. Outbox: 7 logical versions (V1..V7) across 4 relational backends. Inbox: 2 versions on MSSQL/MySQL/SQLite, 1 version on Postgres (born with composite PK). Spanner: fresh-only, no chain.

#### Phase 0 — Archaeology (done)

Done in conversation; codified in `specs/0027-box-schema-versioning-and-migrations/README.md`. Outbox column-evolution table covers 2015→present, 7 distinct additive changes. Inbox is much simpler — 2 changes. Key decisions locked in during archaeology:
1. Fold V5 (UNIQUEIDENTIFIER→NVARCHAR type change, #3042) into V4 — name-only detection, type-change edge case documented (A-1)
2. Fresh-install fast path: run current builder DDL, stamp V_latest, no ALTER chain
3. Spanner degenerate: fresh-only, assume V_latest (no known production users — A-2)
4. Uniform outbox version numbering across 4 backends (they evolved in lockstep)
5. Backend-specific housekeeping columns (MSSQL/Postgres Id PK, MySQL Created/CreatedID, SQLite COLLATE NOCASE) live inside each V1 DDL, not in logical version set

#### Phase 1 — Requirements (done, approved)

- `specs/0027.../requirements.md` — drafted, `/spec:approve requirements` 2026-04-22
- 12 functional, 6 non-functional, 5 constraints, 4 assumptions, 7 out-of-scope items, 19 acceptance criteria
- Key non-obvious AC: **AC-6** — spec-0023-era installations (history rows marked V1 where V1 meant "current DDL") must transition cleanly under spec 0027's numbering (V1 = baseline). Must be explicitly tested.

#### Phase 2 — Design (done, approved)

- **ADR**: `docs/adr/0057-box-schema-versioning-and-migrations.md` — single comprehensive narrative, ~650 lines. Status: **Accepted** (2026-04-22).
- **Review history**: first draft → 9 findings, 6 ≥ 60 → revision 1 (all 9 resolved, 8 new findings) → revision 2 (all 8 resolved, 2 cosmetic items fixed in same pass) → **PASS**.
- **Review artifact**: `specs/0027.../review-design.md` — reflects final PASS state.

Key architectural decisions from ADR 0057:

1. **Per-backend migration lists with logical-column-set versions**: outbox V1..V7 uniform, inbox per-backend (Postgres = V1 only).
2. **Detection**: three-way return — `-1` (no discriminator column — `HeaderBag`/`CommandBody`), `0` (Brighter-shaped but unknown version), `V ≥ 1` (matched). `DetectCurrentVersionAsync` walks top-down.
3. **Three-path runner** inside existing `MigrateAsync(tableName, schemaName, migrations, tableState, ct)`: fresh (current DDL + stamp V_latest) / bootstrap (detect V, stamp V, run V+1..V_latest) / normal (read MAX(V), run above). No new interface methods.
4. **Advisory lock** wraps the *entire* path per backend — fresh-path race fixed. SQLite uses `BEGIN IMMEDIATE TRANSACTION` with `SQLITE_BUSY` retry.
5. **`IAmABoxMigration` extended** — new required `LogicalColumns` + nullable `SourceReference` + nullable `IdempotencyCheckSql`. Source-breaking on netstandard2.0 — accepted (same pattern as spec 0023 R3). `IdempotencyCheckSql` used exclusively by SQLite (others embed check in `UpScript`).
6. **Per-backend conditional-ALTER pattern**: MSSQL `IF COL_LENGTH` / Postgres `IF NOT EXISTS` / MySQL `information_schema`+prepared-statement / SQLite `pragma_table_info` via `IdempotencyCheckSql`. Atomicity from the serialization primitive (lock), not the SQL.
7. **Spanner degenerate runner** with discriminator gate — fresh install runs current DDL + stamp; existing-table-without-history requires discriminator column presence or throws.
8. **Error handling (§5a)**: per-backend transactional model — MSSQL/Postgres/SQLite whole-chain transactional (atomic rollback on failure); MySQL per-DDL implicit commit (each migration individually idempotent; resumes from `MAX(V)`).
9. **Drift-detection test**: new test infra shipping in step 1 of implementation. Per-backend `GetExpectedColumns(table, binaryPayload)` test helper regex-parses DDL; asserts `latest_migration.LogicalColumns ∪ housekeeping == expected`. Catches "forgot to add V8" at CI.

## Uncommitted artifacts (to commit before resuming)

```
docs/adr/0057-box-schema-versioning-and-migrations.md     (new, Accepted)
specs/0027-box-schema-versioning-and-migrations/.adr-list (new)
specs/0027-box-schema-versioning-and-migrations/.design-approved (new)
specs/0027-box-schema-versioning-and-migrations/review-design.md (new, PASS)
specs/0027-box-schema-versioning-and-migrations/README.md        (status updated)
```

Suggested commit message: `docs: accept ADR 0057 box-schema-versioning-and-migrations (spec 0027)`.

Spec 0027 is currently 1 commit (`394bf4911` — opening commit with requirements.md) — this second commit would land the approved design. Per convention, the first ADR commit is the first on the feature branch; 0057 is the second ADR on this branch (ADR 0053 from spec 0023 was already there).

## Spec status snapshot

```
Spec dir:     specs/0027-box-schema-versioning-and-migrations/
Requirements: Approved (2026-04-22)
Design:       Approved — ADR 0057 Accepted (2026-04-22)  [review PASS]
Tasks:        Not started  ←  resume point: /spec:tasks
Implementation: Not started
```

## How to resume

1. **Commit the approved design**: stage `docs/adr/0057-box-schema-versioning-and-migrations.md` and the four spec-dir files listed above; commit with the suggested message.
2. **Run `/spec:tasks`** to draft the implementation task list. Expected scope:
   - Shared groundwork (extend `IAmABoxMigration` + `BoxMigration` + `IAmABoxMigrationRunner` implementations for three-path branching; add drift test)
   - Per-backend expansion: MSSQL first (reference implementation), then Postgres, MySQL, SQLite. Each 1 day.
   - Spanner degenerate runner rework (adds discriminator check + `IsMigrationAppliedAsync` gate — also closes R4)
   - Test sweep: bootstrap-at-V_k per backend × box type; idempotency; concurrent bootstrap (also closes R2); AC-6 spec-0023-era transition.
   - Payload-mode-mismatch tests for PG/MySQL/SQLite/Spanner outbox (closes spec 0023 R5)
   - Docs: `.agent_instructions/box_provisioning.md` rule for new columns; release notes for `IAmARelationalDatabaseConfiguration.SchemaName` + `IAmABoxMigration` source-breaking additions.
3. **Run `/spec:review tasks`** then `/spec:approve tasks`.
4. **Run `/spec:implement` (TDD loop)** or `/spec:ralph-tasks` if unattended.

Spec 0023 R2, R4, R5 close out naturally as part of spec 0027 implementation — they're already captured in ADR 0057 / the expected task breakdown. Once 0027 ships, all spec 0023 blocking findings are resolved.

## Reference — archaeology tables

### Outbox logical versions

| V | Columns added | Commit | PR |
|---|--------------|--------|-----|
| V1 | MessageId, Topic, MessageType, Timestamp, HeaderBag, Body | — | pre-2019 |
| V2 | + Dispatched | 3c30343fa | 2019-07 |
| V3 | + CorrelationId, ReplyTo, ContentType | 79100f509 | #1401 |
| V4 | + PartitionKey; NTEXT→NVARCHAR(MAX) widening; binary variant introduced; #3042 type change folded here | 1cdc04b60 / cff67fd5e (PG) | #2560 / #3464 |
| V5 | + Source, Type, DataSchema, Subject, TraceParent, TraceState, Baggage | b740a68ed | #3633 |
| V6 | + WorkflowId, JobId | 0e79332f1 | #3693 |
| V7 | + DataRef, SpecVersion | d67dac947 | #3790 |

### Inbox logical versions

| Backend | Versions | Notes |
|---------|----------|-------|
| MSSQL/MySQL/SQLite | V1 (baseline), V2 (+ ContextKey) | V2 in `787c31c52` (Oct 2018) |
| Postgres | V1 only | Born with ContextKey + composite PK (Feb 2021, `79100f509`) |
| Spanner | fresh-only | No chain; stamps V_latest |

### Spec 0023 earlier context (preserved from prior PROMPT.md)

#### Latest commit summaries
- `394bf4911` — docs: open spec 0027 for box schema versioning and migrations (requirements.md + README.md)
- `0088abe54` — refactor: disambiguate sp_getapplock placeholder names (R6 fix)
- `297ca030f` — docs: acknowledge SchemaName as source-breaking abstract member (R3 fix)
- `4f806df9d` — chore: add code-review phase to /spec:review skill and capture 0023 findings
- `bae956c3d` — feat: migrate EFCore SalutationAnalytics sample to UseBoxProvisioning
- `43de453c3` — fix: restore DetectCurrentVersionAsync with column introspection (spec 0023 last pass)
- `6ac3093b4` — fix: address PR review findings for box provisioning

#### Spec 0023 Phase 7 (still complete)
WebAPI EFCore sample migrated to `UseBoxProvisioning`. All 9 WebAPI sample projects build cleanly. Dynamo samples intentionally unchanged (BoxProvisioning is relational-only).

## Key conventions (unchanged)

- Primary constructors as default for new classes
- Test file names: GWT convention (`When_X_should_Y.cs`)
- Test class names: general form (`[Behavior]Tests`)
- Reduce nesting by extracting well-named helpers
- Prefer project-owned files (`.agent_instructions/`, `CLAUDE.md`, `PROMPT.md`, specs/) over ephemeral Claude memory
- `netstandard2.0` constraint on `Paramore.Brighter` — no default interface members; source-breaking changes accepted when DIMs would otherwise be used
