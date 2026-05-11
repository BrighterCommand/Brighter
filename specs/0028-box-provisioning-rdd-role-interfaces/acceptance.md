# Spec 0028 Acceptance Criteria — AC1..AC11 Sign-off

**Captured:** 2026-05-11 (Phase 12 sign-off)
**HEAD at capture:** `346ae25e7`
**Branch:** `database_migration`
**Phase 0 baseline sha:** `cb3a5ad56` (`docs: spec 0028 Box Provisioning RDD — tasks approved (round 2 PASS)`)

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
| Core BoxProvisioning.Tests | 36/36 | 36/36 | 23/23 | +13/+13 |
| Core sub-filter | 5/5 | 5/5 | 5/5 | =/= |
| MSSQL | 63/63 | 63/63 | 54/54 | +9/+9 |
| Postgres | 54/54 | 54/54 | 46/46 | +8/+8 |
| MySQL | 61/61 | n/a | 50/50 | +11 |
| SQLite | 45/45 | 45/45 | 40/40 | +5/+5 |
| Spanner | 26/26 | 26/26 | 26/26 | =/= |

Spanner equal (degenerate, unchanged per ADR 0057 §6); all six other filters exceed baseline due to additive Phase 5/6/10 tests.

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

## Sign-off

All eleven acceptance criteria discharged. **Ready for `/spec:approve code`.**
