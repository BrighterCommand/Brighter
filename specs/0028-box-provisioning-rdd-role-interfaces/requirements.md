# Requirements — Spec 0028 Box Provisioning RDD Role Interfaces

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details belong in [ADR 0058 Box Provisioning RDD Role Interfaces](../../docs/adr/0058-box-provisioning-rdd-role-interfaces.md) (to be authored).

**Linked Issue**: none — surfaced by fourth-pass review feedback on PR #4039 (2026-05-07).

**Linked Spec**: builds on [Spec 0027 Box Schema Versioning and Migrations](../0027-box-schema-versioning-and-migrations/) — retrofits role-based interfaces and a template-method runner over the per-backend classes that spec 0027 introduced.

## Problem Statement

> As a Brighter contributor (or a third-party adopter) adding a new BoxProvisioning backend, I would like a single canonical list of role-based contracts I have to implement, so that I can confidently extend Brighter without re-deriving the per-backend conventions from a sprawl of `static class` files and trial-and-error compilation against the existing four backends.

> As a Brighter maintainer reviewing a future BoxProvisioning change, I would like the responsibilities of each per-backend class to be made explicit through Brighter's existing `IAmA*` role-interface convention, so that the design speaks RDD aloud (per `.agent_instructions/design_principles.md`) instead of leaving the role implicit.

> As a Brighter maintainer evolving the migration runner, I would like the substantially-similar try/catch/finally + lock + transaction lifecycle code across MSSQL/Postgres/MySQL/SQLite to live in one harmonised algorithm with backend-specific hooks, so that future cross-backend changes (rollback contract, resource disposal, logging shape) are made once instead of N times — and so that the algorithm itself becomes the documented contract for "what running a migration means" in Brighter.

Concretely: spec 0027 ships five families of per-backend classes that fulfill the same role across backends but are not unified by a role-based interface — detection helpers, DI extensions, migration factories, provisioners, and payload-mode validators. Additionally, the four relational `*BoxMigrationRunner` classes share enough structure that the algorithm should hoist into an abstract base, leaving each backend with only its irreducibly-backend-specific hooks. Today's design does not violate any single principle visibly, but it falls short of the RDD discipline the project explicitly aims for, and a contributor adding a sixth backend would have to deduce the role contracts by side-by-side comparison rather than implement a documented interface.

## Proposed Solution

Author **one ADR** (0058) with two sections covering the unified role-based design:

- **§A — Role-based interfaces** for the in-scope surface families. Each interface names a role per the existing `IAmA*` Brighter convention (or the closest analogue if the existing `IAmA*` convention does not fit a stereotype — to be reconciled in the ADR). Each interface is implemented by every applicable backend; backends that are *intentionally exempt* (e.g. Spanner from the migration-factory family, per ADR 0057 §6's degenerate-runner decision) are documented as exempt with the reason inline. **Feedback items 2 (DI extensions) and 4 (provisioners) are out of scope** — item 2 is dropped to simplify; item 4 is already met by the existing `IAmABoxProvisioner` interface (ADR 0053) and is not re-litigated here.
- **§B — Template-method runner abstract base class** for the four relational `*BoxMigrationRunner` classes. The base class owns the shared algorithm — try/catch/finally + advisory-lock acquire/release + transaction begin/commit/rollback + history-table ensure + path selection (fresh / bootstrap / normal) + logging shape. Each derived class supplies only the irreducibly-backend-specific hooks (DDL emission, advisory-lock primitive, transaction type). Spanner's runner stays free-standing as documented in ADR 0057 §6.

After this spec lands, "how to add a new BoxProvisioning backend" becomes a documented checklist of role-interface implementations + (for relational backends) a derivation from the abstract runner base.

## Requirements

### Functional Requirements

**F1.** Author one ADR (0058 Box Provisioning RDD Role Interfaces) with two sections (§A role interfaces, §B template-method runner abstract base).

**F2.** Introduce an **instance** role-based interface for the **detection-helper** role across the relational backends (MSSQL, Postgres, MySQL, SQLite). The interface is generic on **both** the connection and transaction types (`IAmABoxMigrationDetectionHelper<TConnection, TTransaction>`). Spanner's degenerate detection helper (per ADR 0057 §6 — fresh-install only, lacks `DetectCurrentVersionAsync`) must either implement a subset of the role contract or be documented as exempt; the ADR decides.

**F3.** Introduce an **instance** role-based interface for the **migration-factory** role across the four relational backends. Spanner is exempt by design (ADR 0057 §6 — no V_k chain).

**F4.** Introduce an **instance** role-based interface for the **payload-mode validator** role across the four relational backends. Spanner is exempt by design (fixed binary payload — no text/binary mode to validate).

**F5.** Introduce an **instance** role-based interface for the **provisioning unit-of-work** role — encapsulating the per-backend pairing of advisory lock and transaction (where present) for a single migration run. The four relational backends each ship an implementing class that owns its specific lock+tx ordering and lifecycle. This addresses the ordering contradiction between MSSQL (lock acquired AFTER `BeginTransaction` because the lock is transaction-scoped) and PG/MySQL (lock acquired BEFORE the optional transaction). Spanner stays free-standing per ADR 0057 §6.

**F6.** Introduce an abstract base class for the four relational `*BoxMigrationRunner` classes (`MsSqlBoxMigrationRunner`, `PostgreSqlBoxMigrationRunner`, `MySqlBoxMigrationRunner`, `SqliteBoxMigrationRunner`). The base class owns the shared algorithm — open connection, create UoW (F5), begin UoW, ensure history table, re-detect under lock (TOCTOU per ADR 0057 §3), dispatch to fresh / bootstrap / normal path, commit / rollback, dispose. Each derived class supplies only the irreducibly-backend-specific hooks (UoW factory, history-table DDL, path implementations). `SpannerBoxMigrationRunner` stays free-standing per ADR 0057 §6.

**F7.** Harmonise UoW lifecycle, cancellation, rollback, resource disposal, and logging contracts across the four relational runners as part of F5/F6. The contract is at the UoW lifecycle level (BeginAsync → CommitAsync → DisposeAsync, with RollbackAsync on failure), not at the lock-return-type level — preserving each backend's diagnostic richness (e.g. MySQL's `RELEASE_LOCK` tri-state distinction). Cancellation is specified at every step boundary, not just one. Rollback and disposal MUST NOT throw; cancellation tokens are passed `CancellationToken.None` to rollback to ensure unwind completes even when the caller's token is signalled.

**F8.** Document — inside ADR 0058 — a "How to add a new BoxProvisioning backend" section listing every role-based interface a new package must implement, plus the optional choice of inheriting the abstract runner base. This section satisfies feedback item 6 (the documentation benefit).

**F9.** Survey the rest of the BoxProvisioning packages for **other open-closed sweep candidates** (feedback item 8). The survey output may be empty (no further candidates earn their keep), in which case the ADR records the survey scope and the explicit decision not to act. If candidates are found, they are added to the spec scope or deferred with a documented reason.

**Removed (per user direction 2026-05-07)**: feedback item 2 (DI extensions role-based interface) and feedback item 4 (finer-grained provisioner sub-role) are out of spec 0028 scope. Item 2 is dropped to simplify; item 4 is already met by the existing `IAmABoxProvisioner` interface and the ADR does not re-litigate it.

### Non-functional Requirements

**NF1.** **Backwards compatibility within PR #4039**: spec 0027's public surface has not yet shipped (the entire BoxProvisioning family is still on `database_migration`). The ADR therefore has license to source-break any spec 0027 surface introduced on this branch. The ADR must explicitly enumerate every source-break and document it under the existing `release_notes.md` "Breaking Changes" section that PR #4039 already maintains.

**NF2.** **No behavioural regression** in the spec 0027 backend test suites. Every existing `BoxProvisioning`-namespace test must still pass after the refactor. Counts before refactor (per spec 0027 PROMPT.md state at HEAD `edfa9fc99`):
- MSSQL BoxProvisioning **54/54** per TFM
- Postgres BoxProvisioning **46/46** per TFM
- MySQL BoxProvisioning **50/50** net9.0-only
- SQLite BoxProvisioning **40/40** per TFM
- Spanner BoxProvisioning **26/26** per TFM
- Core BoxProvisioning.Tests **23/23** per TFM
- Core BoxProvisioning **5/5**

Post-refactor counts must equal or exceed each of these per backend per TFM.

**NF3.** **TDD discipline** per CLAUDE.md: every implementation task uses `/test-first <behavior>` with a mandatory STOP for approval before GREEN. Refactors that introduce no new behaviour (interface extraction with identical method signatures forwarded to the existing impl) follow Beck's Tidy First — structural-only commit, validated by running the existing tests before and after.

**NF4.** **RDD discipline** per `.agent_instructions/design_principles.md`: role names should describe the responsibility, not the mechanism; interfaces use the `IAmA*` convention or the ADR justifies a deviation; no "type-named" interfaces (e.g. avoid an interface that simply parrots a class name).

**NF5.** **No `InternalsVisibleTo` testing affordances introduced**. Testing affordances (test doubles, capturing loggers, fakes) live in the per-test-project `TestDoubles/` namespace per the convention established by spec 0027 Items D / M / N / K.

**NF6.** **No test-only public surface** introduced. If an abstraction's existence is testability-driven, the ADR must declare so explicitly and justify the public-surface cost.

**NF7.** **Instance interfaces only.** Role interfaces use plain instance methods — no static virtual / static abstract members — so they compile and run on the full TFM matrix per C7. Detection helpers, catalogues, payload validators, and the new provisioning UoW are instance classes (constructed once and injected, or instantiated per call where the lifecycle requires it — UoW is per-migration, the others are stateless singletons). This restores test substitutability that pure-static helpers would have foreclosed; tests that need a fake detection helper or a fake UoW can supply one.

### Constraints and Assumptions

**C1.** Spec 0028 ships on the `database_migration` branch — same PR #4039 as specs 0023 + 0027. This is review feedback on the same PR, not greenfield work.

**C2.** Spec 0027 is closed at 19/19 acceptance criteria with all 15 Boy Scout follow-ups complete. Spec 0028 builds on the spec 0027 surface as it stands at HEAD `edfa9fc99` (pre-spec-0028).

**C3.** The user has agreed: one ADR with two sections (not two separate ADRs). The role-interface design and the template-method runner design hang together as a unified RDD pass over the spec 0027 surface.

**C4.** **Naming convention**: the user's review feedback proposed `IAmABoxMigrationDetectionHelper` (item 1). Brighter's documented convention is `IAmA*` (per `.agent_instructions/design_principles.md`). The ADR must reconcile naming consistency — preferring `IAmA*` unless the proposed alternative captures a meaningfully different stereotype.

**C5.** **No production-runtime dependency changes**. The refactor introduces no new NuGet packages; it must work within the existing dependency graph (Microsoft.Data.SqlClient, Npgsql, MySqlConnector, Microsoft.Data.Sqlite, Google.Cloud.Spanner.Data).

**C6.** **No CLI/MSBuild target framework changes**. The refactor lands on the same TFM matrix as spec 0027. The shared `Paramore.Brighter.BoxProvisioning` assembly targets `netstandard2.0;net8.0;net9.0;net10.0` (`$(BrighterTargetFrameworks)`); the MSSQL package targets `net462;net8.0;net9.0;net10.0` (`$(BrighterFrameworkAndCoreTargetFrameworks)`); MySQL is net9.0-only per `BrighterTestNineOnlyTargetFrameworks`; everything else is net8.0+.

**C7.** **All new role interfaces must compile and run on the existing TFM matrix** — including netstandard2.0 and net462 where those TFMs are present. This rules out static virtual / static abstract interface members (a .NET 7+ feature) and `IReadOnlySet<T>` (.NET 5+) in the public role contracts of the shared assembly. Existing precedent: `IAmABoxMigration.LogicalColumns` is typed `IReadOnlyCollection<string>` rather than `IReadOnlySet<string>` for exactly this reason. New interfaces use plain instance methods with types available across all targeted TFMs.

### Out of Scope

**OoS1.** **No new backend introductions.** Spec 0028 unifies the existing five backends (MSSQL, Postgres, MySQL, SQLite, Spanner). Adding a sixth backend (e.g. Oracle) is explicitly out of scope.

**OoS2.** **No changes to spec 0027's logical migration chain or DDL.** V1..V7 outbox migrations and V1..V2 inbox migrations are frozen as of spec 0027.

**OoS3.** **No changes to existing public role interfaces.** `IAmABoxProvisioner`, `IAmABoxMigrationRunner`, `IAmABoxMigration`, `IAmARelationalDatabaseConfiguration`, `I*AdvisoryLock` (where introduced by spec 0027 Items D/M/N) — these are stable. Spec 0028 may add new role interfaces but does not change the shape of these existing ones.

**OoS4.** **No changes to spec 0027's Boy Scout follow-ups** — Items A through U-spanner-inbox stay as committed. Spec 0028 may *consolidate* their effects under the new role interfaces but does not retrace any of their behavioural fixes.

**OoS5.** **No changes to the migration-history table schema.** `__BrighterMigrationHistory` shape, columns, and indexes stay as defined in ADR 0057.

**OoS6.** **No introduction of new lock primitives or rollback strategies.** F7's harmonisation describes contracts uniformly without inventing new mechanisms — `pg_advisory_unlock`, MySQL `RELEASE_LOCK`, MSSQL transaction-scoped `sp_getapplock`, SQLite `BEGIN IMMEDIATE` writer slot all stay. The new `IAmAProvisioningUnitOfWork` interface (F5) is a *new abstraction* over the existing primitives, not a new primitive — each backend's UoW class invokes the same lock/transaction APIs the existing runner does today, just in a class that hides the per-backend ordering.

**OoS7.** **No move of existing test-double types.** Test doubles already at `tests/.../BoxProvisioning/TestDoubles/` (per spec 0027 Item 89910dfad) stay where they are; spec 0028 may add new test doubles in the same location pattern but does not relocate any existing ones.

**OoS8.** **No new package, project, or assembly creation.** Role interfaces land in the existing assemblies — `Paramore.Brighter.BoxProvisioning` for shared types; per-backend `Paramore.Brighter.BoxProvisioning.{MsSql,PostgreSql,MySql,Sqlite,Spanner}` for backend-specific types.

## Acceptance Criteria

**AC1.** ADR 0058 authored, adversarially reviewed (multiple rounds per CLAUDE.md), approved by user.

**AC2.** Each of feedback items 1, 3, 5 has a named **instance** role-based interface in the `Paramore.Brighter.BoxProvisioning` assembly, implemented by every applicable backend class. Each interface is documented (XML-doc on the interface and on the role).

**AC3.** Feedback item 7 has (a) an abstract base class `RelationalBoxMigrationRunnerBase` (or as named by the ADR) covering MSSQL/PG/MySQL/SQLite, with `SpannerBoxMigrationRunner` documented as exempt per ADR 0057 §6, AND (b) an `IAmAProvisioningUnitOfWork` instance role interface (F5) implemented by one class per relational backend that encapsulates that backend's lock+transaction pairing.

**AC4.** Feedback item 8 (other open-closed sweep candidates) is addressed by either: (a) an explicit "no further candidates" decision recorded in the ADR after a documented survey, or (b) additional refactor scope folded into spec 0028 with each candidate justified.

**AC5.** Feedback item 6 (documentation benefit) is met by an ADR section "How to add a new BoxProvisioning backend" that lists every role-based interface a new package must implement.

**AC6.** Backend test counts per NF2 are equal or improved post-refactor. Each backend's `BoxProvisioning` test filter passes against live containers (or in-process for SQLite, emulator for Spanner).

**AC7.** `release_notes.md` enumerates every source-breaking and additive public-surface change introduced by spec 0028 under the existing PR #4039 "Breaking Changes" / "Additive" sections.

**AC8.** No new `InternalsVisibleTo` directives. No new test-only public surface (any new public type whose primary motivation is testability is documented as such with the trade-off recorded).

**AC9.** Naming convention reconciliation per C4: each new role interface either follows `IAmA*` or the ADR explicitly justifies the deviation.

**AC10.** Every implementation task uses `/test-first` per the TDD mandate in CLAUDE.md. Tidy First-shaped refactors (interface extraction with no behavioural change) are committed structurally, validated by the existing tests passing before and after.

**AC11.** PR #4039 description updated to enumerate the spec 0028 work as a fourth-pass review response, with links to ADR 0058 and `specs/0028-box-provisioning-rdd-role-interfaces/`.

**Note on dropped feedback items**: items 2 (DI extensions) and 4 (provisioner finer-grained sub-role) are explicitly out of scope per user direction on 2026-05-07. Item 2 is dropped to simplify; item 4 is already met by the existing `IAmABoxProvisioner` interface (ADR 0053). Neither has an acceptance criterion in this spec.

## Additional Context

**Surface inventory** — captured in the survey performed during Phase 0 of this spec (output recorded informally in the conversation transcript that produced this document; the ADR will re-state the shapes formally):

- 5 detection helpers (4 relational + 1 Spanner-degenerate)
- 5 DI extension classes (one per backend)
- 8 migration factories (4 backends × outbox/inbox; Spanner exempt)
- 10 provisioners (5 backends × outbox/inbox; all already implement `IAmABoxProvisioner`)
- 5 payload-mode validators (4 relational + 1 Spanner — Spanner's is degenerate/uniform with relational shape)
- 5 migration runners (4 relational + 1 Spanner-degenerate)

**Reference convention examples** for `IAmA*` naming in Brighter:
- `IAmAProducerRegistry` (cited in `design_principles.md`)
- `IAmABoxProvisioner` (already exists — spec 0027)
- `IAmABoxMigrationRunner` (already exists — spec 0027)
- `IAmABoxMigration` (already exists — spec 0027)
- `IAmARelationalDatabaseConfiguration` (already exists — pre-spec)

**Reference exemption pattern**: ADR 0057 §6 documents Spanner as a "degenerate runner" — fresh-install only, no V_k chain, no advisory lock, no transaction. Spec 0028 inherits this exemption pattern wherever a role does not apply to Spanner.

**Source feedback** (verbatim — fourth-pass PR #4039 review, 2026-05-07; preserved in the spec README for traceability): see [README.md](README.md).
