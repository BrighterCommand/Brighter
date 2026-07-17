# Box Provisioning RDD Role Interfaces

**Spec ID:** 0028
**Created:** 2026-05-07
**Branch:** `database_migration` (shared with specs 0023 + 0027 — review feedback on the same PR #4039)
**Related:** [Spec 0027 Box Schema Versioning and Migrations](../0027-box-schema-versioning-and-migrations/) — this spec retrofits role-based interfaces and a template-method runner over the per-backend classes spec 0027 introduced
**Related ADR:** [0057 Box Schema Versioning and Migrations](../../docs/adr/0057-box-schema-versioning-and-migrations.md) — this spec authors a follow-on ADR §A (role interfaces) + §B (template-method runner)

## Summary

Apply Responsibility-Driven Design (per `.agent_instructions/design_principles.md`) to spec 0027's per-backend BoxProvisioning classes. Today the BoxProvisioning packages contain five families of classes that fulfill the same role across backends but lack a role-based interface: detection helpers, DI extensions, migration factories, provisioners, and payload-mode validators. Additionally, the four relational `*BoxMigrationRunner` classes implement `IAmABoxMigrationRunner.MigrateAsync` with substantially similar try/catch/finally + lock + transaction lifecycle code that should hoist into an abstract base class to support open-closed and harmonise rollback / resource disposal / logging contracts.

## Scope

Five role-based interface extractions plus one (or two) abstract base classes:

| # | Family | Interface candidate | Backends |
|---|--------|---------------------|----------|
| 1 | Detection helpers (`*BoxDetectionHelpers`) | `IAmABoxMigrationDetectionHelper<TConnection>` (likely static-virtual; transaction-handling shape TBD in ADR) | MSSQL, PG, MySQL, SQLite, Spanner (Spanner degenerate — missing `DetectCurrentVersionAsync`) |
| 2 | DI extensions (`*BoxProvisioningExtensions`) | `ISupportProvisioning` (or similar; user-suggested name) | MSSQL, PG, MySQL, SQLite, Spanner |
| 3 | Migration factories (`*OutboxMigrations`, `*InboxMigrations`) | role interface — uniform `static IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration)` | MSSQL, PG, MySQL, SQLite (Spanner exempt — no V_k chain per ADR 0057 §6) |
| 4 | Provisioners (`*OutboxProvisioner`, `*InboxProvisioner`) | **already implements `IAmABoxProvisioner`** — finding to reconcile in requirements (likely already-met) | MSSQL, PG, MySQL, SQLite, Spanner |
| 5 | Payload-mode validators (`*PayloadModeValidator`) | role interface (likely static-virtual) | MSSQL, PG, MySQL, SQLite (Spanner exempt — fixed binary payload) |
| 7 | Migration runners (`*BoxMigrationRunner`) | abstract base class `SqlBoxMigrationRunner` (template-method); Spanner stays free-standing per ADR 0057 §6 | MSSQL, PG, MySQL, SQLite (Spanner exempt) |
| 8 | Other open-closed sweep candidates | TBD during design | TBD |

## Decisions already agreed (Phase 0 discussion 2026-05-07)

1. **New spec 0028** (not a Phase 12 on spec 0027). Spec 0027 closes at 19/19 acceptance criteria; this is a design refresh on top.
2. **One ADR with two sections** — §A role-based interfaces (items 1–6 from the review feedback); §B template-method runner abstract base class (items 7–8).
3. **Ship on this PR** (`database_migration` → PR #4039). Treated as review feedback on 0027, not greenfield work — re-opening the diff is acceptable cost. User: "Arguably, it would have been better caught earlier but."
4. **Static virtual interface members** are the suggested mechanism for items 1, 3, 5 (and possibly 2). Decision will live in the ADR; the surveyed surfaces are all `static class` today, so the shape fits — at the cost of test-substitutability (acceptable; integration-only test convention is already established).
5. **Item 4 likely already-met** by existing `IAmABoxProvisioner`. Default treatment in requirements: declare met, document the existing role; reconsider only if a finer-grained sub-role earns its keep.
6. **Spanner stays free-standing** for the runner abstract base. Per ADR 0057 §6, Spanner's degenerate fresh-install-only model is intentional and shares ~30% structure with the relational four — not enough to share a base.

## Open design questions (for the ADR phase)

- **Transaction-divergence in item 1**: detection-helper signatures use a `SqlTransaction? = null` (MSSQL/PG/Sqlite), have **no** transaction parameter (MySQL — per-statement commit per ADR 0057 §5a), or are absent altogether (Spanner — single-statement DDL with no transaction concept). Three candidate interface shapes:
  - **(a)** Two interfaces — `IAmABoxMigrationDetectionHelper<TConnection>` (no transaction) and `IAmATransactionalBoxMigrationDetectionHelper<TConnection, TTransaction>` (the latter inheriting / extending the former).
  - **(b)** Single interface with `TConnection` and `TTransaction` type parameters where `TTransaction` defaults to `DbTransaction?`. Static abstract methods don't compose well with default type parameters; needs feasibility check.
  - **(c)** Single interface; transaction is absent from the contract; backend impls expose transaction-bearing overloads as non-interface methods. Loses the "find the contract" benefit for half the call sites.
- **Item 2 (DI extensions) and static-virtual fit**: extension methods on `BoxProvisioningOptions` don't fit cleanly into a static-virtual interface (extension method semantics ≠ static method semantics). Either accept the role is documented-only, or move the role onto a non-extension-method surface (e.g. `BackendProvisioningRegistry` / similar) — the ADR must call this.
- **Harmonise rollback / resource disposal / logging contract** for the relational runner base. Today: PG logs Warning when `pg_advisory_unlock` returns `false` (Item D); MySQL logs Warning when `RELEASE_LOCK` returns non-`true` (Item M); MSSQL is acquire-only (transaction-scoped, no release log); SQLite has no advisory lock. The base class must specify a uniform contract — ADR §B prescribes.
- **Naming for items 1–5 role interfaces**: user suggested `IAmABoxMigrationDetectionHelper` (item 1) and `ISupportProvisioning` (item 2). Note the second uses `ISupport*` not `IAmA*` — unusual for Brighter (`IAmA*` is the documented convention). ADR must reconcile naming consistency.

## Status

- [ ] Requirements (`requirements.md`) — to be drafted
- [ ] Design (ADR 005X) — to be drafted; one ADR with two sections (§A role interfaces, §B template-method runner)
- [ ] Tasks (`tasks.md`) — to be drafted post-ADR-approval
- [ ] Implementation — TDD per task; STOP for approval after each `/test-first` test before the GREEN
- [ ] Review — closes the fourth-pass PR #4039 review feedback from 2026-05-07

## Sub-phase A — `SqlBoxProvisioner` pull-up (post-acceptance reactive)

**Surfaced:** 2026-05-12, post Phase 12 acceptance, before PR #4039 merge. Reactive to F9 / AC4 obligation — see `sweep-result.md` Amendment (Candidate 5).

**Scope summary**: introduce `SqlBoxProvisioner<TConnection, TTransaction>` abstract base; port the eight relational provisioners to derive; Spanner pair exempt (parallel to the `SqlBoxMigrationRunner` exemption in §B.2 / ADR 0057 §6). Unify the MySQL pre-lock negative-version-clamp inconsistency as a separate `/test-first` slice after the structural pull-up lands.

**Requirements, design, tasks**: live in `requirements.md` (F10 + NF additions), ADR 0058 §B.5 (to be authored via `/spec:design`), and a Phase 13 block to be added to `tasks.md` (via `/spec:tasks`) after design approval. **Do not edit those documents directly** — follow the `/spec:*` workflow.

---

## Source feedback (verbatim — fourth-pass PR #4039 review, 2026-05-07)

> **DB Migrator**
>
> 1. We have a number of classes that implement static methods to determine whether we already have migration history or need to infer it. For example: `src/Paramore.Brighter.BoxProvisioning.MySql/MySqlBoxDetectionHelpers.cs` and `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlBoxDetectionHelpers.cs`. Despite them fulfilling the same role, we have not tried to factor these into a common interface. We prefer to do this because we explicitly aim to use RDD (see `.agent_instructions/design_principles.md`). Our naming convention would be something like `IAmABoxMigrationDetectionHelper`. Although these are static classes, .NET supports static interface members: https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/interface-implementation/static-virtual-interface-members. The interface would need to be generic. Some interfaces use a transaction, while others do not; this may be supportable.
> 2. Similarly, there is considerable overlap between `MsSqlBoxProvisioningExtensions`, `MySqlBoxProvisioningExtensions`, etc. Again, our preference would be to call out the role with an interface, such as `ISupportProvisioning`.
> 3. Again, `MSSqlInboxMigrations`, `MySqlInboxMigrations`, etc., probably share a common role-based interface that could be extracted.
> 4. As does `PostgreSqlOutboxProvisioner`, `MySqlOutboxProvisioner`.
> 5. As does `MySqlPayloadModeValidator`, `PostgreSqlPayloadModeValidator`.
> 6. By moving all these common patterns to role-based interfaces, it becomes easier for us to document how to create a new BoxProvisioning project i.e. implement all these interfaces.
> 7. Most of the implementations of `IAmABoxMigrationRunner.Task MigrateAsync` seems to follow a similar pattern. It suggests two things: (1) We could possibly use an abstract base class for the core try…catch..finally loop with abstract methods for `RunFreshPathAsync`, etc., that each derived class completes. We would need to harmonize the catch…finally model, particularly around rollback, resource disposal, and logging.
> 8. Again it's possible that we have other places where we could use an abstract base class to support the open-closed principle and move the algorithm into an abstract base class, which help future implementers.
