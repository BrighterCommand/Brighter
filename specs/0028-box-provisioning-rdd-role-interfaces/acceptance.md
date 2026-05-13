# Spec 0028 Acceptance Criteria — AC1..AC12 Sign-off

**Captured:** 2026-05-11 (Phase 12 sign-off for AC1..AC11); 2026-05-13 (Phase 13.C.3 sign-off for AC12 — sub-phase A delivered).
**HEAD at capture:** `346ae25e7` (parent spec); `bdefd3ea9` → AC12-tick commit at end of this section (sub-phase A).
**Branch:** `database_migration`
**Phase 0 baseline sha:** `cb3a5ad56` (`docs: spec 0028 Box Provisioning RDD — tasks approved (round 2 PASS)`)
**Sub-phase A range:** `246ea6f13` (`docs: ADR 0058 sub-phase A — §B.5 SqlBoxProvisioner pull-up`) .. `31d84d18d` (`feat: spec 0028 sub-phase A 13.B`).

Each AC records (a) the verifying artefact and (b) the tick.

## AC1 — ADR 0058 authored, adversarially reviewed, approved

- [x] Verifying artefacts:
  - `docs/adr/0058-box-provisioning-rdd-role-interfaces.md` — title "0058. Box Provisioning RDD Role Interfaces and Template-Method Runner", **Status: Accepted** (line 5), Date: 2026-05-07.
  - `specs/0028-box-provisioning-rdd-role-interfaces/.requirements-approved`, `.design-approved`, `.tasks-approved` markers all present.
  - Adversarial review records: `specs/0028-box-provisioning-rdd-role-interfaces/review-design.md` (round 2 PASS), `review-tasks.md` (round 2 PASS).

## AC2 — Feedback items 1, 3, 5 each have an instance role-based interface with XML-doc + impls per backend

- [x] Verifying artefacts:
  - **Feedback item 1 (detection helpers)** — `IAmABoxMigrationDetectionHelper<TConnection, TTransaction>` (base) + `IAmAVersionDetectingMigrationHelper<TConnection, TTransaction>` (extension) at `src/Paramore.Brighter.BoxProvisioning/IAmABoxMigrationDetectionHelper.cs` and `IAmAVersionDetectingMigrationHelper.cs`. XML-doc on interface and each method. 5 backend impls (see `traceability.md` F2 table). Per-method `schemaName` null-handling contract documented in XML-doc.
  - **Feedback item 3 (migration catalogues)** — `IAmABoxMigrationCatalog` at `src/Paramore.Brighter.BoxProvisioning/IAmABoxMigrationCatalog.cs`. XML-doc on interface and method. 8 backend impls (Spanner exempt — see `traceability.md` F3 table).
  - **Feedback item 5 (payload-mode validators)** — `IAmABoxPayloadModeValidator<TConnection>` at `src/Paramore.Brighter.BoxProvisioning/IAmABoxPayloadModeValidator.cs`. XML-doc on interface and method. 5 backend impls (see `traceability.md` F4 table).

## AC3 — Feedback item 7 has abstract base `RelationalBoxMigrationRunnerBase` AND `IAmAProvisioningUnitOfWork` interface with 4 relational impls; Spanner exemption documented

- [x] Verifying artefacts:
  - Abstract base: `src/Paramore.Brighter.BoxProvisioning/RelationalBoxMigrationRunnerBase.cs` (`public abstract class RelationalBoxMigrationRunnerBase<TConnection, TTransaction> : IAmABoxMigrationRunner`).
  - 4 relational runners derive from base — see `traceability.md` F6 table.
  - UoW interface: `src/Paramore.Brighter.BoxProvisioning/IAmAProvisioningUnitOfWork.cs` (`public interface IAmAProvisioningUnitOfWork<TTransaction> : IAsyncDisposable`).
  - 4 relational UoW impls — see `traceability.md` F5 table.
  - Spanner exemption documented at ADR 0058 §A.2 (catalogue), §B.1 (UoW), §B.2 (runner base) — referencing ADR 0057 §6 fresh-install-only model. `SpannerBoxMigrationRunner` implements `IAmABoxMigrationRunner` directly without deriving from the base.

## AC4 — Open-closed sweep recorded

- [x] Verifying artefact: `specs/0028-box-provisioning-rdd-role-interfaces/sweep-result.md` — re-walks ADR 0058 §B.4's four candidates against post-implementation surface; all four "No" decisions hold; new-candidate sweep returns empty. F9 fully discharged.

## AC5 — "Adding a new BoxProvisioning backend" section in ADR 0058 lists every role interface

- [x] Verifying artefact: `docs/adr/0058-box-provisioning-rdd-role-interfaces.md` line 475 ("## Adding a new BoxProvisioning backend"). Seven numbered steps enumerate detection helper (F2), catalogues (F3), payload validator (F4), advisory lock (ADR 0057 §5b), UoW (F5), provisioner (ADR 0053), runner (F6). Phase 11.3 commit `346ae25e7` verified all 27 referenced class names match shipped surface (no drift).

## AC6 — Backend test counts ≥ NF2 baseline per backend per TFM

- [x] Verifying artefact: Phase 10.4 gate commit `94c822369` (`docs: spec 0028 Phase 10.4 — Phase 10 gate ✓; tick task; seven filters at post-Phase-10 baseline`).

| Filter | net9.0 | net10.0 | Baseline | Δ |
|---|---|---|---|---|
| Core BoxProvisioning.Tests | 43/43 [^ac6-core-sub-phase-a] | 43/43 [^ac6-core-sub-phase-a] | 23/23 | +20/+20 |
| Core sub-filter | 5/5 | 5/5 | 5/5 | =/= |
| MSSQL | 64/64 [^ac6-mssql-drift] | 64/64 [^ac6-mssql-drift] | 54/54 | +10/+10 |
| Postgres | 55/55 [^ac6-pg-drift] | 55/55 [^ac6-pg-drift] | 46/46 | +9/+9 |
| MySQL | 67/67 | n/a | 50/50 | +17 |
| SQLite | 46/46 | 46/46 | 40/40 | +6/+6 |
| Spanner | 26/26 | 26/26 | 26/26 | =/= |

[^ac6-core-sub-phase-a]: Post-Phase-13.B (sub-phase A complete). The Phase 6 precedent legitimises adding base-contract tests alongside an abstract base; Phase 13.A.1 added 8 `[Fact]` methods across three test files (orchestration 3 + schema 2 + clamp 3) at `tests/Paramore.Brighter.BoxProvisioning.Tests/` — recomputed Δ at 13.A.1: 44 − 23 = +21 per TFM (was +13 pre-13.A.0.5 amendment). Phase 13.B then moved Core from 44/44 +21/+21 → 43/43 +20/+20 (-1 deleted override-identity `[Fact]` once the transitional `ClampDetectedVersion` hook was removed) and MySQL from 61/61 +11 → 67/67 +17 (+2 unification `[Fact]`s in `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning/` PLUS +4 pre-existing drift from three post-Phase-10.4 fix commits: `ba8813e6f` "harmonise MySql default lock timeout to 30 seconds" +1, `a8e99e1c4` "reject negative TimeSpan in MySqlAdvisoryLock.AcquireAsync" +1, `03bdd7455` "reject overflowing TimeSpan in MySqlAdvisoryLock.AcquireAsync" +2 net — same pattern as the SQLite +1 drift, reconciled here in lock-step). SQLite recorded a pre-existing +1 drift from commit `b14d76592` (default lock-timeout pin) — reconciled in lock-step at 13.B: 45/45 +5/+5 → 46/46 +6/+6.

[^ac6-mssql-drift]: Post-sub-phase-A Docker-deferred floor pass on 2026-05-13. Pre-existing +1 drift from commit `acff5eb34` ("fix: harmonise MsSql default lock timeout to 30 seconds" — PR #4039 review item #1) which landed between Phase 10.4 gate (`94c822369`) and sub-phase A entry (`246ea6f13`). Reconciled here in lock-step: 63/63 +9/+9 → 64/64 +10/+10. Same four-backend lock-timeout harmonisation series as MySQL `ba8813e6f`, SQLite `b14d76592`, and Postgres `080e93c96` (next row's footnote); see `^ac6-core-sub-phase-a` above for the MySQL/SQLite drift reconciliation already recorded at 13.B.

[^ac6-pg-drift]: Post-sub-phase-A Docker-deferred floor pass on 2026-05-13. Pre-existing +1 drift from commit `080e93c96` ("fix: harmonise PostgreSql default lock timeout to 30 seconds" — PR #4039 review item #1 continuation) which landed between Phase 10.4 gate (`94c822369`) and sub-phase A entry (`246ea6f13`). Reconciled here in lock-step: 54/54 +8/+8 → 55/55 +9/+9. Same four-backend lock-timeout harmonisation series as MySQL `ba8813e6f`, SQLite `b14d76592`, and MSSQL `acff5eb34` (preceding row's footnote); see `^ac6-core-sub-phase-a` above for the MySQL/SQLite drift reconciliation already recorded at 13.B.

Spanner equal (degenerate, unchanged per ADR 0057 §6); all six other filters exceed baseline due to additive Phase 5/6/10 tests plus the sub-phase A base-contract additions on the Core row.

## AC7 — `release_notes.md` enumerates source-breaks and additive surface

- [x] Verifying artefact: commit `346ae25e7` adds new section "Box Provisioning RDD role-interface refactor (spec 0028)" to `release_notes.md` (between spec 0027 section and Release 10.0.0). Five source-break sub-headings (detection helpers, migration catalogues, payload validators, provisioner ctor cascade, runner ctor cascade + template-method base) + Additive surface enumeration (5 role interfaces + 1 abstract base + 4 UoW + 5 detection helpers + 8 catalogues + 5 payload validators).

## AC8 — No new `InternalsVisibleTo`; no test-only public surface

- [x] Verifying evidence:
  - `grep -r "InternalsVisibleTo" src/Paramore.Brighter.BoxProvisioning*` → returns zero matches (Phase 12 grep).
  - Public-type survey of `src/Paramore.Brighter.BoxProvisioning/*.cs` (Phase 12) shows every new public type is a runtime production type: 5 role interfaces + 1 abstract base class + per-backend implementations (4 UoW + 5 detection helpers + 8 catalogues + 5 payload validators). Zero types motivated by testability — test doubles live in per-test-project `TestDoubles/` per the spec 0027 convention (NF6).

## AC9 — Naming convention: every new role interface starts with `IAmA*`

- [x] Verifying evidence: `grep "^public interface" src/Paramore.Brighter.BoxProvisioning/*.cs` (Phase 12) returns 8 hits, all starting with `IAmA*`:
  - `IAmABoxMigration` (pre-existing)
  - `IAmABoxMigrationCatalog` ✨ spec 0028
  - `IAmABoxMigrationDetectionHelper` ✨ spec 0028
  - `IAmABoxMigrationRunner` (pre-existing)
  - `IAmABoxPayloadModeValidator` ✨ spec 0028
  - `IAmABoxProvisioner` (pre-existing)
  - `IAmAProvisioningUnitOfWork` ✨ spec 0028
  - `IAmAVersionDetectingMigrationHelper` ✨ spec 0028

  All 5 spec 0028 interfaces conform; no deviation requiring justification (ADR §A.1/§A.2/§A.3 rationale sub-sections justify the role names internally — `Catalog`, `DetectionHelper`, `PayloadModeValidator`, `ProvisioningUnitOfWork`, `VersionDetectingMigrationHelper`).

## AC10 — Every new-behaviour task used `/test-first`; structural moves are Tidy First commits

- [x] Verifying evidence: `git log cb3a5ad56..346ae25e7 --oneline` returns 126 commits since Phase 0 baseline. Commit-prefix tally:

  | Prefix | Count | Meaning |
  |---|---|---|
  | `refactor:` | 72 | Pure structural moves (Tidy First per Beck — interface extraction with identical method signatures forwarded to existing impls; existing tests green before AND after) |
  | `feat:` | 19 | New behaviour landed (paired with preceding `test:` per TDD) |
  | `test:` | 16 | Test-first commits (Phase 5/6/10 contract tests; STOPPED for IDE approval before implementation per CLAUDE.md TDD mandate) |
  | `docs:` | 16 | Phase gate / doc commits |
  | `fix:` | 2 | Bug fixes |
  | `chore:` | 1 | |

  Phase 5/6/10 test commits used the `/test-first` skill (verified inline by the consistent `test: spec 0028 Phase X.Y — ...` commit-message format). Phase 1–4/7–9 are dominated by `refactor:` commits (interface extraction + DI cascade) — pure Tidy First, no test-first required. Phase 10.2 and 10.3 produced `test:` commits with no paired `feat:` because the Phase 6 runner base already satisfied the contract being pinned (implementation-no-op, contract-pinning tests).

## AC11 — PR #4039 description updated

- [x] Verifying artefact: PR #4039 description includes "Spec 0028 — Box Provisioning RDD Role Interfaces" Scope bullet (linking ADR 0058 + `specs/0028-box-provisioning-rdd-role-interfaces/` directory) and a Breaking Changes bullet pointing to release notes. Confirmed via `gh pr view 4039 --json body --jq .body | grep -c "Spec 0028"` returning 3 (Scope header + Breaking Changes header + body reference). Pushed in this Phase 11 session.

---

## AC12 — Sub-phase A delivered

Each sub-bullet records (a) the verifying artefact and (b) the tick. Sub-phase A range: `246ea6f13` .. `31d84d18d` (sixteen commits) + Phase 13.C documentation commits (`3d12bc302`, `bdefd3ea9`, the AC12-tick commit itself, and the 13.C.4 pr-description splice commit). Validation deferred to the pre-`/spec:approve code` pass for the three Docker-requiring backends (MSSQL / Postgres / Spanner).

### F10 — `SqlBoxProvisioner<TConnection, TTransaction>` + 8 relational derivations

- [x] Verifying artefacts:
  - `src/Paramore.Brighter.BoxProvisioning/SqlBoxProvisioner.cs` — declares `public abstract class SqlBoxProvisioner<TConnection, TTransaction> : IAmABoxProvisioner` with `where TConnection : DbConnection where TTransaction : DbTransaction`. XML-doc on class + every protected hook (introduced in `70f92df44` slice 1; refined through slices 2–3 `1cc5e009b` / `4e271d861`; finalised in `31d84d18d`).
  - Eight relational derivations land in commits `f0de8b62b` (MsSqlOutbox), `de5516765` (MsSqlInbox), `edffcf8bf` (PostgreSqlOutbox), `6ce460174` (PostgreSqlInbox), `f76ef8c39` (MySqlOutbox), `971a8fa38` (MySqlInbox), `be70aa7ff` (SqliteOutbox), `7965eae4d` (SqliteInbox). Each preserves both ctors (5-arg canonical + 2-arg back-compat), both delegating to `base(...)`.
  - Spanner pair does NOT derive: `grep -l 'SqlBoxProvisioner' src/Paramore.Brighter.BoxProvisioning.Spanner/ -r` returns zero matches (locally verified end of session 2).
  - Phase 13.A gate commit: `42a35ce3c`.

### F10.1 — Hook surface (five variance deltas)

- [x] Verifying artefacts:
  - Post-13.B hook count on `SqlBoxProvisioner` is **three**: `CreateConnection` (abstract, delta a), `PayloadColumnName` (abstract, delta b), `EffectiveSchemaName` (virtual, delta c). The transitional fourth (`ClampDetectedVersion`, delta d) was introduced in slice 3 `4e271d861` and removed in `31d84d18d` — clamp is now inlined at the `DetectTableStateAsync` bootstrap branch. Delta e (disposal) requires no hook (uniform sync `using` per §B.2 precedent — see F12 below).
  - Per-backend override expectations from the F10.1 table:
    - SQLite Outbox + Inbox override `EffectiveSchemaName => null` (delta c) — permanent. Landed in `be70aa7ff` + `7965eae4d`.
    - MySQL Outbox + Inbox overrode `ClampDetectedVersion` to identity during Phase 13.A (delta d) — landed in `f76ef8c39` + `971a8fa38`; both overrides deleted in `31d84d18d` along with the hook itself.
    - MSSQL / Postgres derivations carry no overrides beyond the two abstracts.
  - Base-contract tests (`tests/Paramore.Brighter.BoxProvisioning.Tests/`) — 7 `[Fact]`s across three files pin the hook table:
    - `When_sql_box_provisioner_provision_async_runs_successfully_it_should_invoke_hooks_in_documented_order.cs` (3 `[Fact]`s) pins delta a.
    - `When_sql_box_provisioner_effective_schema_name_is_overridden_it_should_propagate_to_detection_and_payload_calls_only.cs` (2 `[Fact]`s) pins delta c.
    - `When_sql_box_provisioner_detect_table_state_inlines_negative_version_clamp.cs` (2 `[Fact]`s; renamed + trimmed in `31d84d18d` from the 3-`[Fact]` slice-3 file) pins delta d post-13.B via data-flow.

### F11 — Unified MySQL pre-lock negative-version clamp

- [x] Verifying artefacts:
  - Commit `31d84d18d` (`feat: spec 0028 sub-phase A 13.B — unify MySQL pre-lock clamp with MsSql/Postgres/Sqlite (remove transitional hook + overrides; inline clamp; rename + trim base-contract clamp test; reconcile pre-existing MySQL+SQLite floor drift)`).
  - Behavioural test: `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning/When_mysql_pre_lock_detects_negative_version_it_should_clamp_to_zero.cs` — 2 `[Fact]`s (one per `MySqlOutboxProvisioner` / `MySqlInboxProvisioner`). Verified locally GREEN end of session 2: captured `BoxTableState.CurrentVersion == 0` (was -1 pre-13.B).
  - MySQL filter count moved 65/65 → 67/67 net9.0-only across 13.B (+2 from the new test file; +4 pre-existing drift reconciled in lock-step — see AC6 footnote `^ac6-core-sub-phase-a`).
  - "No half-finished implementations" (CLAUDE.md): override-removal + hook-removal + inline land in **one** commit per ADR §B.5 line 646 mandate.

### F12 — Disposal pattern (sync `using` per §B.2 precedent — no probe)

- [x] Verifying artefacts:
  - `specs/0028-box-provisioning-rdd-role-interfaces/baseline.md` → "Sub-phase A preliminaries" → F12 disposition table. Cites the §B.2 precedent (`src/Paramore.Brighter.BoxProvisioning/RelationalBoxMigrationRunnerBase.cs:112-116`) as the operative reason for sync `using` on the connection.
  - **No independent probe project built**: the limiting factor is `DbConnection` (the base type) on netstandard2.0, not the four driver subtypes (`SqlConnection`, `NpgsqlConnection`, `MySqlConnection`, `SqliteConnection`). `DbConnection` does not implement `IAsyncDisposable` on netstandard2.0, so a base-class `await using` over `TConnection : DbConnection` would not compile across the shared-assembly TFM matrix (`netstandard2.0;net8.0;net9.0;net10.0`).
  - **Precedent-discharged per round-2 review** of ADR 0058 §B.5 (see `specs/0028-box-provisioning-rdd-role-interfaces/review-tasks.md` round-2 entry). ADR §B.5 inherits the §B.2 decision rather than re-litigating.

### F13 — ADR §B.4 amendment + forward link to §B.5

- [x] Verifying artefacts:
  - `docs/adr/0058-box-provisioning-rdd-role-interfaces.md` §B.4 — single-row table addition for Candidate 5 (`SqlBoxProvisioner` pull-up) with forward link to §B.5. Same commit (`246ea6f13`) that authored §B.5. The four original candidate verdicts (1–4) remain unaltered.
  - §B.5 authored as a new sub-section parallel in shape to §B.2 (hook table, lifecycle contract by inheritance, naming subsection, risks-and-mitigations).

### NF8 — Naming compliance (`SqlBoxProvisioner` + time-bounded asymmetry)

- [x] Verifying artefacts:
  - ADR 0058 §B.5 "Naming" subsection (line ~673) — precision-of-contract justification: `Sql` names the `DbConnection` lineage precisely (the base requires `where TConnection : DbConnection`); `Relational` would name a broader semantic category that includes the exempt Spanner backend (Spanner IS relational/SQL per ADR 0057 §6 yet is excluded from this base).
  - ADR 0058 §B.5 "Naming" subsection drops the `*Base` suffix to mirror §A's role-interface style (line ~681).
  - ADR 0058 §B.5 "Risks and Mitigations" → "Naming asymmetry, time-bounded" entry (line ~752) — records the §B.2 sibling base (`RelationalBoxMigrationRunnerBase`) as carrying the pre-existing name and commits to a successor ADR that renames it for symmetry.
  - PR #4039 description "Post-merge follow-up" bullet — time-bounds the asymmetry by committing the rename to a successor ADR before any third-party adopter takes a hard dependency on either base. Verified live by `gh pr view 4039 --json body --jq '.body' | grep -A3 'Post-merge follow-up'` (preserved through 13.C.4 splice; smoke-grep `'Post-merge follow-up'` == 1 verifies).
  - **The §B.2 rename is NOT a sub-phase A task** — it is recorded on the PR description, not implemented here.

### NF9 — Behavioural neutrality (Phase 13.A floor-preserving; Phase 13.B moves floor)

- [x] Verifying artefacts:
  - Floor trajectory (Core BoxProvisioning.Tests, per TFM):
    - Pre-sub-phase A: 36/36 (parent spec acceptance at HEAD `346ae25e7`).
    - Post-13.A.1 (`23c05a9fc` gate): **44/44** — +8 base-contract `[Fact]`s across three files (3 orchestration + 2 schema + 3 clamp). Legitimised by the 13.A.0.5 NF9 carve-out (commit `667b5246f`) per the Phase 6 precedent (`RelationalBoxMigrationRunnerBase` introduced six base-contract test files alongside the abstract base).
    - Post-13.B (`31d84d18d`): **43/43** — -1 deleted override-identity `[Fact]` when the transitional `ClampDetectedVersion` hook was removed (slice-3 clamp test file trimmed 3 → 2 `[Fact]`s).
  - Floor trajectory (MySQL BoxProvisioning sub-filter, net9.0-only): 61/61 → **67/67** post-13.B. +2 from `When_mysql_pre_lock_detects_negative_version_it_should_clamp_to_zero.cs`; +4 from pre-existing drift reconciliation (post-Phase-10.4 fix commits `ba8813e6f` lock-timeout harmonisation +1, `a8e99e1c4` negative-TimeSpan rejection +1, `03bdd7455` overflowing-TimeSpan rejection +2 net — same drift pattern as SQLite +1; reconciled in lock-step per the precedent).
  - Floor trajectory (SQLite BoxProvisioning sub-filter): 45/45 → **46/46** post-13.B. +1 from pre-existing drift (commit `b14d76592` default lock-timeout pin; flagged at 13.A.7 in commit `42a35ce3c`).
  - Per-backend ports 13.A.2–13.A.5 introduce **zero** new tests, satisfying NF9-strict at the backend level.
  - Three-artefact lock-step (NF9 / baseline / AC6) preserved across both amendments — 13.A.0.5 (`667b5246f`) and 13.B (`31d84d18d`). NF2 (Phase-0 baseline anchor) untouched.

### NF10 — Source-break neutrality (additive only)

- [x] Verifying artefacts:
  - `release_notes.md` — sub-phase A entry under the existing spec 0028 "Additive: new public types" section names `SqlBoxProvisioner<TConnection, TTransaction>` as a second "Abstract base" bullet (commit `3d12bc302` — 13.C.1).
  - **Zero entries under Breaking Changes** attributable to sub-phase A: each derived provisioner preserves both ctors (5-arg canonical + 2-arg back-compat) and both delegate to `base(...)`; no call-site change required. The Phase 8 ctor cascade (already shipped) is the only source-break for the provisioner family; sub-phase A is additive on top (one new public abstract type).

### NF11 — TFM matrix unchanged

- [x] Verifying artefacts:
  - `dotnet build src/Paramore.Brighter.BoxProvisioning -c Release --no-incremental` — clean on `netstandard2.0;net8.0;net9.0;net10.0`. 0 warnings, 0 errors. Verified locally end of session 2 (post-13.B HEAD `31d84d18d`).
  - The new abstract base uses plain generic class declaration with `where TConnection : DbConnection where TTransaction : DbTransaction` constraints — the same shape as `RelationalBoxMigrationRunnerBase` (F6), known-good on the parent TFM matrix.

### Parent AC8 preservation — no new `InternalsVisibleTo`, no new test-only public surface

- [x] Verifying evidence:
  - `grep -r 'InternalsVisibleTo' src/Paramore.Brighter.BoxProvisioning*` returns zero new matches (parent AC8 grep at Phase 12 returned zero; sub-phase A added no `InternalsVisibleTo` directives).
  - `SqlBoxProvisioner<TConnection, TTransaction>` is a runtime production type — the eight derived provisioners are the production surface; the base hosts the algorithm. Zero types motivated by testability (test doubles live in `tests/Paramore.Brighter.BoxProvisioning.Tests/TestDoubles/` per the spec 0027 / parent NF6 convention).

### Traceability — F10/F10.1/F11/F12/F13 rows added

- [x] Verifying artefact: `specs/0028-box-provisioning-rdd-role-interfaces/traceability.md` — "## Sub-phase A (post-acceptance, 2026-05-12) — F10..F13" section appended in commit `bdefd3ea9` (13.C.2). Each row cross-walks the requirement to (a) the file(s) / class(es), (b) the verifying test(s) where applicable, and (c) the commit sha(s).

### PR #4039 description amended (parent AC11 preservation + sub-phase A bullet)

- [x] Verifying artefacts (post-13.C.4 publish smoke greps run 2026-05-13):
  - `gh pr view 4039 --json body --jq '.body' | grep -c 'Sub-phase A (post-acceptance'` → `1` ✓ (new sub-bullet added by `gh pr edit --body-file specs/0028-box-provisioning-rdd-role-interfaces/pr-description.md`).
  - `gh pr view 4039 --json body --jq '.body' | grep -c 'Post-merge follow-up'` → `1` ✓ (existing §B.2 rename commitment section preserved unmodified).
  - `gh pr view 4039 --json body --jq '.body' | wc -l` → `34` ≥ pre-edit `31` ✓.
  - Local audit-trail commit: `b3ddbc3f9` (`docs: spec 0028 sub-phase A 13.C.4 — local pr-description.md captures spliced PR #4039 body with sub-phase A bullet`). Publish action `gh pr edit 4039 --body-file …` returned the PR URL with no error.
  - Note on AC11's historical `'Spec 0028'` grep count: AC11 (Phase 11) recorded the count as 3. Live count is now 4 — the +1 is from the existing "Post-merge follow-up" section (added 2026-05-12, before sub-phase A entry); the 13.C.4 splice itself adds zero new `Spec 0028` occurrences. AC11 remains discharged at its captured value.

---

## Sign-off

All twelve acceptance criteria discharged. **Ready for `/spec:approve code`** — Docker-requiring backend validations completed 2026-05-13: MSSQL 64/64 per TFM (+1 drift from `acff5eb34`, reconciled), Postgres 55/55 per TFM (+1 drift from `080e93c96`, reconciled), Spanner 26/26 per TFM (no drift, exempt per ADR 0057 §6). Floor amendment applied in three-artefact lock-step (AC6 + baseline.md NF9 + requirements.md NF9; NF2 untouched) per the pattern documented in `^ac6-core-sub-phase-a`. Core (43/43 per TFM), MySQL (67/67 net9.0-only), and SQLite (46/46 per TFM) verified locally end of session 2.
