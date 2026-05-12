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

---

# Sub-phase A — `SqlBoxProvisioner` pull-up (post-acceptance reactive)

> **Status**: requirements drafted 2026-05-12. AC1..AC11 of the parent spec are signed off; sub-phase A is appended under the **F9 / AC4 reactive obligation** (parent requirements §F9 + acceptance §AC4 + ADR 0058 §B.4 closing paragraph: *"If implementation surfaces new candidates, the spec 0028 tasks list folds them in or defers them with documented reason"*).

**Surfaced:** 2026-05-12, on `database_migration` post-Phase-12 acceptance (HEAD `efdced78e`), before PR #4039 merge. Discovered during a code-review pass of the ten shipped `*BoxProvisioner.cs` files after the Phase 8 ctor cascade made the duplication structurally visible.

**Trigger record:** [sweep-result.md Amendment §Candidate 5 (2026-05-12)](sweep-result.md). The original 2026-05-11 sweep returned empty; this candidate was missed by both ADR 0058 §B.4 and the post-implementation re-walk because the §B.4 candidate list was framed around the original review feedback items 1–8 — which treated provisioners only at the role-interface level (item 4, "already met by `IAmABoxProvisioner`") and never at the implementation-side body-duplication level.

## Sub-phase A Problem Statement

> As a Brighter maintainer reviewing the eight relational `*BoxProvisioner.cs` files after the Phase 8 ctor cascade, I would like the ~80 lines of substantially identical body per provisioner — three near-duplicate methods (`ProvisionAsync`, `DetectTableStateAsync`, `ValidatePayloadModeAsync`) varying only on connection factory, payload column casing, and SQLite's schema handling — to live in one shared algorithm with backend-specific hooks, so that future cross-backend changes to the orchestration are made once rather than eight times, and so that the orchestration itself becomes the documented contract for "what provisioning a box means" in Brighter.

> As a Brighter contributor adding a new BoxProvisioning backend, I would like the provisioner role to be supported by a template-method base class for the relational case (matching the precedent already established for the migration runner by `RelationalBoxMigrationRunnerBase`), so that I implement only the irreducibly-backend-specific hooks (connection factory, payload column name) and inherit the orchestration without re-implementing it from a sibling backend.

Concretely: after Phase 8 cleaned the ten provisioners onto the same canonical ctor shape and replaced static helper calls with instance dispatch, the relational eight became structurally homogeneous — `ProvisionAsync` is byte-identical bar three deltas (negative-version clamp, payload column casing, schema-name argument). This homogeneity was invisible during the spec 0028 design phase because the static helper calls obscured the common body shape. Post-Phase-8 it is plain: ~640 lines of duplication collapsible into one ~120-line base class. The parallel `RelationalBoxMigrationRunnerBase<TConnection, TTransaction>` precedent (ADR 0058 §B.2) demonstrates the shape is appropriate and known-good across the existing TFM matrix.

## Sub-phase A Proposed Solution

Author **ADR 0058 §B.5** (extending the existing single-ADR-with-two-sections shape from C3) describing a new abstract base class `SqlBoxProvisioner<TConnection, TTransaction>` in `src/Paramore.Brighter.BoxProvisioning/`. The base owns the three duplicated method bodies; the eight relational provisioners derive and supply hooks for the irreducibly-backend-specific pieces. Spanner stays a direct `IAmABoxProvisioner` implementation — same exemption pattern as `RelationalBoxMigrationRunnerBase` (per ADR 0057 §6).

After this sub-phase lands, the "How to add a new BoxProvisioning backend" section (parent F8 / AC5 / ADR 0058 §A.4) is amended to point relational backends at `SqlBoxProvisioner` as the recommended base — paralleling the existing recommendation to derive from `RelationalBoxMigrationRunnerBase`.

The MySQL pre-lock negative-version-clamp inconsistency (an additional finding surfaced by the same survey) is reconciled in a **separate behavioural slice** following the parent NF3 / AC10 TDD discipline — `/test-first` for the unified behaviour, then remove a transitional override in the same commit as the test goes GREEN. Phase 13.A is structural (TIDY FIRST per Beck, behaviour bit-for-bit preserved); Phase 13.B is behavioural (TEST + IMPLEMENT). The split matches the parent Phase 7 / Phase 10 precedent.

## Sub-phase A Requirements

### Functional Requirements

**F10.** Introduce an abstract base class `SqlBoxProvisioner<TConnection, TTransaction>` in `src/Paramore.Brighter.BoxProvisioning/` that implements `IAmABoxProvisioner` and owns the orchestration body shared by the eight relational provisioners (`ProvisionAsync`, `DetectTableStateAsync`, `ValidatePayloadModeAsync`). The four relational backends' eight provisioners (MSSQL/Postgres/MySQL/SQLite × Outbox/Inbox) derive from this base. Spanner's pair (`SpannerOutboxProvisioner`, `SpannerInboxProvisioner`) stays free-standing per ADR 0057 §6 — same exemption shape as `RelationalBoxMigrationRunnerBase` (F6).

**F10.1** — *Hook surface*. The base exposes backend-specific hooks for the five variance deltas surveyed 2026-05-12. The ADR enumerates each hook (signature, default behaviour where virtual, override expectation per backend). Deltas in scope:

| # | Delta observed in the eight relational provisioners | Resolution shape |
|---|---|---|
| a | Connection factory (`new {Backend}Connection(connectionString)`) | abstract hook, one override per backend |
| b | Payload column casing (`"Body"` / `"body"` / `"CommandBody"` / `"commandbody"`) | abstract property, one override per (backend, box-type) |
| c | Schema-name argument to detection helper and payload validator (relational pass `_configuration.SchemaName`; SQLite passes `null`) | virtual property, default = `_configuration.SchemaName`; SQLite overrides to `null` |
| d | Negative-version clamp (MSSQL/PG/SQLite clamp `detectedVersion < 0 ? 0 : v`; MySQL does NOT) | virtual hook, default = clamp; MySQL overrides to identity during Phase 13.A; override removed in Phase 13.B per F11 |
| e | Disposal pattern (`await using` vs `using`) | base uses sync `using` for the connection per §B.2 precedent (`RelationalBoxMigrationRunnerBase.cs:112-116`) — `DbConnection` does not implement `IAsyncDisposable` on netstandard2.0 (shared-assembly TFM), so a base-class `await using` over `TConnection : DbConnection` would not compile. No override hook required — sync shape is uniform across all four derivations. F12 disposition recorded in `baseline.md` per §B.2 precedent (no independent probe required) |

**F11.** Unify MySQL's pre-lock negative-version-clamp behaviour to match MSSQL/Postgres/SQLite. Today MySQL's `MySqlOutboxProvisioner`/`MySqlInboxProvisioner` return the raw `detectedVersion` from `DetectCurrentVersionAsync` (including `-1` as the "discriminator missing" sentinel from spec 0027); the other three relational backends clamp `-1` to `0` with the comment *"Pre-lock detection is a hint for the caller; the runner re-detects under the lock. Negative or zero return values are not gated here — the runner is the single source of truth"*. F11's unification picks the three-of-four majority behaviour (clamp) as the principled fix: the pre-lock value is a hint, the runner is authoritative, and the clamp removes a backend-specific surprise without changing what the runner observes under the lock. Delivered as a separate `/test-first` slice (Phase 13.B) per NF3.

**F12.** **Resolved 2026-05-12 by §B.2 precedent — no probe required.** Earlier framing required verifying `IAsyncDisposable` support on all four backend `DbConnection` subtypes (`SqlConnection`, `NpgsqlConnection`, `MySqlConnection`, `SqliteConnection`) across the full TFM matrix per C6 before standardising the base's disposal pattern on `await using`. Round-2 review of ADR 0058 §B.5 surfaced that the verification's outcome is already known from the sibling base: `RelationalBoxMigrationRunnerBase` (the §B.2 sibling abstract base in the same shared `Paramore.Brighter.BoxProvisioning` assembly) already encodes the disposition at `RelationalBoxMigrationRunnerBase.cs:112-116` — *"DbConnection does not implement IAsyncDisposable on netstandard2.0, so `await using` would not compile across the shared-assembly TFM matrix."* The limiting factor is the **base type `DbConnection`** on netstandard2.0, not the driver subtypes. `SqlBoxProvisioner` therefore inherits the same sync-`using` decision rather than running an independent probe. `baseline.md` records the disposition (sync `using` per §B.2 precedent) under the "Sub-phase A preliminaries" heading; no probe project is built. If a future TFM bump drops netstandard2.0 from the shared assembly, the disposition is revisited in a follow-up spec.

**F13.** Amend ADR 0058 §B.4 to record the post-implementation discovery of Candidate 5 (overturning the original "no further candidates" verdict) and reference §B.5 for the design. The §B.4 amendment is a single-row table addition with a forward link; it does not re-litigate the four original candidate verdicts (1–4) which remain valid.

### Non-functional Requirements

**NF8.** **Naming**: the base class is `SqlBoxProvisioner` (not `BoxProvisionerBase` or `RelationalBoxProvisionerBase`). The `Sql` prefix names the implementation contract precisely — the base requires `where TConnection : DbConnection`, i.e. the ADO.NET `DbConnection` lineage with its `IDbCommand`/`DbCommand`-shaped query surface. "Relational" would name a broader semantic category that includes the exempt Spanner backend (Spanner IS relational/SQL per ADR 0057 §6 yet is excluded from this base), muddling the scope; "Sql" names the lineage cleanly. The `*Base` suffix is dropped to mirror §A's role-interface style (`IAmA*` — no `*Base` suffix). The choice creates a transitional naming asymmetry with the §B.2 sibling base `RelationalBoxMigrationRunnerBase`; the asymmetry is **time-bounded by PR #4039** via a "Post-merge follow-up" bullet on the PR description committing to a successor ADR that renames §B.2's class for symmetry (see ADR 0058 §B.5 Naming subsection + Risks and Mitigations entry "Naming asymmetry, time-bounded"). The ADR satisfies parent C4 / AC9 (naming-convention reconciliation).

**NF9.** **Behavioural neutrality of Phase 13.A**. The structural pull-up commit must keep every backend's BoxProvisioning test filter at the post-Phase-10 counts captured in `acceptance.md` AC6 (Core 43/43 + 5/5, MSSQL 63/63, PG 54/54, MySQL 67/67 net9.0-only, SQLite 46/46, Spanner 26/26 — per the AC6 table; the Core 36 → 44 raise reflects Phase 13.A.1's base-contract test cases per the Phase 6 precedent, subsequently trimmed to 43 in 13.B; the MySQL 61 → 67 and SQLite 45 → 46 raises reflect 13.B reconciliation of pre-existing drift from post-Phase-10.4 fix commits — see AC6 footnote for the commit list). MySQL's clamp behaviour is preserved bit-for-bit during Phase 13.A via a `ClampDetectedVersion` override on `MySqlOutboxProvisioner` / `MySqlInboxProvisioner` returning identity; the override is removed in Phase 13.B as part of the F11 behavioural slice. SQLite's `null` schema-name behaviour is similarly preserved via an `EffectiveSchemaName` override returning `null`. No new tests are added at the **per-backend** level during Phase 13.A (the per-backend ports 13.A.2–13.A.5 are strictly behaviour-preserving Tidy First commits). Phase 13.A.1 introduces base-contract `[Fact]` methods against `SqlBoxProvisioner` at `tests/Paramore.Brighter.BoxProvisioning.Tests/` per the Phase 6 precedent (`RelationalBoxMigrationRunnerBase` introduced six base-contract test files alongside the abstract base); the post-13.A.1 Core BoxProvisioning.Tests floor was 44/44 per TFM, reflecting 8 added `[Fact]` methods across three files (3 + 2 + 3); Phase 13.B then trimmed Core to 43/43 by deleting the transitional override-identity `[Fact]` when the `ClampDetectedVersion` hook was removed — see baseline.md "Sub-phase A preliminaries" → NF9 floor.

**NF10.** **Source-break neutrality**. Sub-phase A does not introduce new source-breaks beyond those already enumerated in `release_notes.md` for the parent spec. Each derived provisioner preserves both ctors (5-arg canonical + 2-arg back-compat) — both delegate to `base(...)`. No call-site change required. The DI extensions registered in Phase 9 continue to construct the canonical 5-arg ctor. The Phase 8 ctor cascade is the only source-break for the provisioner family; sub-phase A is additive on top (one new public abstract type — `SqlBoxProvisioner<TConnection, TTransaction>`).

**NF11.** **TFM matrix unchanged** per parent C6 / C7. The new abstract base uses plain generic class declaration with `where TConnection : DbConnection where TTransaction : DbTransaction` constraints — the same shape as `RelationalBoxMigrationRunnerBase`, known-good on the parent TFM matrix.

### Constraints and Assumptions

**C8.** Sub-phase A requires the parent spec 0028 Phase 8 (ctor cascade) and Phase 9 (DI wiring) complete. The eight relational provisioners' canonical 5-arg ctor shape is the contract `SqlBoxProvisioner` derives from; without Phase 8 the homogeneity to pull up would not exist.

**C9.** Sub-phase A does **not** require any AC1..AC11 to be un-ticked. The parent acceptance criteria stand; AC12 is the only new acceptance criterion. The `.code-approved` marker is re-stamped after AC12 ticks (a re-approval pass on the previously-approved spec).

**C10.** Sub-phase A ships on the same PR #4039 as the parent spec, treated as continuing review-response work per parent C1. PR description and `release_notes.md` are updated to enumerate sub-phase A's surface; no new PR is opened.

### Out of Scope

**OoS9.** **Collapsing Outbox + Inbox into one parameterised class per backend.** The Outbox/Inbox pair within a backend differ in `BoxType`, table name (`OutBoxTableName` vs `InBoxTableName`), payload column casing, and the injected `IAmABoxMigrationCatalog`. Collapsing them into one class parameterised on these values is technically feasible but deferred — the Phase 8 ctor surface is freshly stable, and the DI registration (Phase 9) treats them as separate types. Re-litigation of the per-pair shape is a separate spec if pursued.

**OoS10.** **Changes to `IAmARelationalDatabaseConfiguration` to fold `BoxType`-derived `BoxTableName` selection into the configuration object.** Sub-phase A computes `BoxTableName` inside `SqlBoxProvisioner` from `BoxType` and the existing `OutBoxTableName` / `InBoxTableName` properties. Adding a `TableNameFor(BoxType)` method to the configuration interface is excluded under parent OoS3.

**OoS11.** **Changes to the Spanner pair.** `SpannerOutboxProvisioner` and `SpannerInboxProvisioner` remain free-standing `IAmABoxProvisioner` implementations. Sub-phase A verifies their tests stay green (Spanner BoxProvisioning at 26/26 per TFM unchanged per ADR 0057 §6) but introduces no Spanner code change.

**OoS12.** **Other open-closed sweep candidates surfaced during sub-phase A implementation.** If the Phase 13 implementation pass surfaces yet another candidate, the F9 / AC4 reactive obligation applies recursively — record in `sweep-result.md` and ask the user before expanding sub-phase A scope. Do NOT silently absorb.

## Sub-phase A Acceptance Criteria

**AC12.** Sub-phase A delivered with:
- **F10**: `src/Paramore.Brighter.BoxProvisioning/SqlBoxProvisioner.cs` declares `public abstract class SqlBoxProvisioner<TConnection, TTransaction> : IAmABoxProvisioner` with `where TConnection : DbConnection where TTransaction : DbTransaction`. XML-doc on class + every protected hook. The eight relational provisioners derive (MSSQL/PG/MySQL/SQLite × Outbox/Inbox). Each preserves both ctors (5-arg canonical + 2-arg back-compat) delegating to `base(...)`. The Spanner pair does **not** derive — `grep -l "SqlBoxProvisioner" src/Paramore.Brighter.BoxProvisioning.Spanner/` returns zero.
- **F10.1**: each hook from the F10.1 table present on the base with the documented signature and default; each per-backend override present where the table requires one (MySQL `ClampDetectedVersion` during Phase 13.A only; SQLite `EffectiveSchemaName` always; everyone else inherits defaults).
- **F11**: MySQL's `ClampDetectedVersion` override removed in Phase 13.B. New `/test-first` test pinning the unified clamp behaviour present at `tests/Paramore.Brighter.MySQL.Tests/BoxProvisioning/When_mysql_pre_lock_detects_negative_version_it_should_clamp_to_zero.cs` (or equivalent name). Test counts move from the AC6 floor by the count of added test cases; new floor recorded in `baseline.md` and `requirements.md` NF2 in the same commit.
- **F12**: disposition recorded in `baseline.md` under a "Sub-phase A preliminaries" heading. Cites the §B.2 precedent (`RelationalBoxMigrationRunnerBase.cs:112-116`) as the operative reason for sync `using` on the connection. No independent probe required — `DbConnection` lacks `IAsyncDisposable` on netstandard2.0 across all four ADO.NET drivers regardless of subtype; the limiting factor is the base type on the shared-assembly TFM matrix. ADR §B.5 inherits the same decision.
- **F13**: ADR 0058 §B.4 amended (Candidate 5 row added with forward link to §B.5); ADR 0058 §B.5 authored as a new sub-section parallel in shape to §B.2.
- **NF8** naming compliance: deviation from `*Base` suffix documented in ADR §B.5 with precision-of-contract justification (`Sql` names the `DbConnection` lineage; `Relational` is a broader category that includes the exempt Spanner backend) AND a time-bounded follow-up commitment recorded in the PR #4039 description and ADR Risks and Mitigations.
- **NF9** behavioural neutrality: AC6 floor preserved through Phase 13.A commits; only Phase 13.B commits move the floor.
- **NF10** source-break neutrality: `release_notes.md` updates additive section only; no new entries under Breaking Changes attributable to sub-phase A.
- **NF11** TFM matrix unchanged: `dotnet build src/Paramore.Brighter.BoxProvisioning` clean on `netstandard2.0;net8.0;net9.0;net10.0`.
- No new `InternalsVisibleTo` (parent AC8 preserved).
- No new test-only public surface (parent AC8 preserved — `SqlBoxProvisioner` is a runtime production type, not testability-motivated).
- Traceability row F10 + F11 + F12 + F13 added to `traceability.md`.
- PR #4039 description amended to enumerate sub-phase A (parent AC11 preserved with a "Sub-phase A (post-acceptance)" bullet).

## Sub-phase A Additional Context

**Empirical surface evidence (2026-05-12)**: the eight relational provisioners measured at ~80 lines per body × 8 = ~640 lines duplicated. The new abstract base measures at approximately one body shared (~120 lines including XML-doc). Net code reduction: ~520 lines plus the eight ctor-rewrite shrinkages (~25 lines per derivation × 8 = ~200 lines saved on the derived side). Aggregate reduction: ~700 lines collapsed to ~120 base + ~80 across eight derivations = ~720 → ~200 lines.

**Reference precedent**: `RelationalBoxMigrationRunnerBase<TConnection, TTransaction>` (ADR 0058 §B.2) — same shape, same generic constraints, same Spanner-exemption pattern. The §B.2 sibling's `Relational*Base` naming is provisional: the precision-of-contract argument that motivates `SqlBoxProvisioner` for §B.5 (see NF8) applies equally to §B.2, and the asymmetry is time-bounded by the PR #4039 "Post-merge follow-up" bullet committing to a successor ADR that renames `RelationalBoxMigrationRunnerBase` to a `Sql*`-prefixed equivalent before any third-party adopter takes a hard dependency on either base. Sub-phase A ships the new base under the corrected naming; §B.2 follows in the successor ADR.

**Source code references** (pre-sub-phase-A, as of HEAD `efdced78e`):
- `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlOutboxProvisioner.cs:83-143`
- `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlInboxProvisioner.cs:83-140`
- `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlOutboxProvisioner.cs:83-144` (uses `await using` — disposal-pattern outlier)
- `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlInboxProvisioner.cs:83-141`
- `src/Paramore.Brighter.BoxProvisioning.MySql/MySqlOutboxProvisioner.cs:82-139` (no clamp — F11 outlier)
- `src/Paramore.Brighter.BoxProvisioning.MySql/MySqlInboxProvisioner.cs:82-139` (no clamp — F11 outlier)
- `src/Paramore.Brighter.BoxProvisioning.Sqlite/SqliteOutboxProvisioner.cs:62-122` (passes `schemaName: null` — F10.1.c outlier)
- `src/Paramore.Brighter.BoxProvisioning.Sqlite/SqliteInboxProvisioner.cs:61-119`
- `src/Paramore.Brighter.BoxProvisioning.Spanner/SpannerOutboxProvisioner.cs:72-135` (exempt — different role-interface shape, no catalogue, custom version inference)
- `src/Paramore.Brighter.BoxProvisioning.Spanner/SpannerInboxProvisioner.cs:68-131` (exempt — same reasons)

**Why the original 2026-05-07 design phase missed this**: ADR 0058 §B.4's four-candidate sweep was framed around the original review feedback items 1–8 — item 4 ("provisioners") was assessed at the role-interface level only (already met by `IAmABoxProvisioner` per ADR 0053). The implementation-side duplication only became structurally visible after Phase 8 cleaned the eight relational provisioners onto the canonical 5-arg ctor with instance-dispatched dependencies — before Phase 8 they differed in their dependence on static helper classes which obscured the common shape. The 2026-05-11 post-implementation sweep (`sweep-result.md`) walked §B.4's candidate list but did not re-walk the post-Phase-8 surface end-to-end; the candidate was discovered by a separate ad-hoc code review on 2026-05-12.
