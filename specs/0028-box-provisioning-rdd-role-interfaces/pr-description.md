We had some useful feedback that the two biggest usability issues were the complexity of configuration (covered by a separate ADR) and the management of a box (inbox/outbox).

This PR addresses the management of a box. It derives from lessons from the WebAPI sample and leans into Aspire, because that is the expectation for developers.

## Scope

This PR delivers **both** specifications together, so the bootstrap path is production-ready when the feature first ships:

- **Spec 0023 — Box Database Migration** ([ADR 0053](docs/adr/0053-box-database-migration.md))
  Core provisioning infrastructure: `IAmABoxProvisioner` / `IAmABoxMigrationRunner`, `BoxTableState`, `__BrighterMigrationHistory`, advisory locking per backend, fail-fast hosted service, Aspire connection-name overloads.

- **Spec 0027 — Box Schema Versioning and Migrations** ([ADR 0057](docs/adr/0057-box-schema-versioning-and-migrations.md))
  Proper version chain (V1..V_latest) for outbox (7 versions) and inbox (2 versions) across MSSQL/Postgres/MySQL/SQLite, plus subset-based version detection so existing pre-`DataRef`/`SpecVersion` tables bootstrap correctly. Spanner is fresh-install-only.

- **Spec 0028 — Box Provisioning RDD Role Interfaces** ([ADR 0058](docs/adr/0058-box-provisioning-rdd-role-interfaces.md), [spec dir](specs/0028-box-provisioning-rdd-role-interfaces/))
  A fourth-pass review response on this same PR (per Spec 0028 requirement C1). Restructures the BoxProvisioning surface introduced by Specs 0023 + 0027 around Responsibility-Driven-Design role interfaces (`IAmABoxMigrationDetectionHelper<TConnection, TTransaction>` and its version-detecting extension, `IAmABoxMigrationCatalog`, `IAmABoxPayloadModeValidator<TConnection>`, `IAmAProvisioningUnitOfWork<TTransaction>`) and a template-method `SqlBoxMigrationRunner<TConnection, TTransaction>` that owns the migration algorithm with derived runners supplying only backend-specific hooks. Static helper classes (`{Backend}BoxDetectionHelpers`, `{Backend}{Box}Migrations`, `{Backend}PayloadModeValidator`) become public instance classes; runner ctors gain typed parameters; provisioner ctors gain typed parameters. Behaviour unchanged. Source-breaks licensed by Spec 0028 NF1 (Spec 0027 surface had not shipped). Full source-break enumeration in `release_notes.md` and ADR 0058 §A.1/§A.2/§A.3/§B.

  - **Sub-phase A (post-acceptance review response, 2026-05-12)** — `SqlBoxProvisioner<TConnection, TTransaction>` pull-up consolidating ~640 lines duplicated body across the eight relational provisioners into one ~120-line base. MySQL pre-lock negative-version clamp unified with MsSql/Postgres/Sqlite per F11. ADR 0058 §B.5 + `specs/0028-box-provisioning-rdd-role-interfaces/` sub-phase A appendix.

  - **Sub-phase B (pre-merge rename, post-acceptance, 2026-05-13)** — per [ADR 0059](docs/adr/0059-box-provisioning-abstract-base-naming-symmetry.md) (Accepted 2026-05-13), renamed `RelationalBoxMigrationRunnerBase<TConnection, TTransaction>` → `SqlBoxMigrationRunner<TConnection, TTransaction>` for naming symmetry with sub-phase A's `SqlBoxProvisioner<TConnection, TTransaction>`. Pure Tidy First — zero behavioural delta; build clean on `netstandard2.0;net8.0;net9.0;net10.0`; all BoxProvisioning filters green at the post-13.D floor (MSSQL 64/64, PG 55/55, MySQL 67/67 net9.0-only, SQLite 46/46, Core 43/43, Spanner 26/26 per TFM). Pre-merge scope — see ADR 0059 §Context. Closes the "Naming asymmetry, time-bounded" risk in ADR 0058.

Bundling these resolves review finding **R1** — without Spec 0027, the Spec 0023 bootstrap path treats any pre-V_latest table as unrecognised and the runner attempts a `CREATE TABLE` that fails. Spec 0027 also addresses **R2** (TOCTOU race on bootstrap insert), **R4** (Spanner concurrency), and **R5** (payload-mode validator test coverage on non-MSSQL backends).

## Breaking Changes

- **`IAmARelationalDatabaseConfiguration.SchemaName`** — a new `string? SchemaName { get; }` member is added to this public interface. The core `Paramore.Brighter` package targets `netstandard2.0`, which does not support default interface members, so this is a **source-breaking** change for any external code that implements `IAmARelationalDatabaseConfiguration`. All in-tree implementors are updated; external implementors must add the property (returning `null` reproduces the previous behaviour).

  ADR 0057 §10 documents the rationale; commit `297ca030f` records the explicit decision to accept the abstract member rather than split target frameworks for one property.

- **Spec 0028 source-breaks** — restructuring the BoxProvisioning surface to RDD role interfaces is purely structural but source-breaks any external implementor of the affected types (static helpers, migration catalogues, payload validators, runner/provisioner ctors). The shipped Brighter call-sites and DI extensions absorb the cascade; `UseBoxProvisioning(opts => opts.Add{Backend}{Box}(config))` users require no change. Full enumeration in `release_notes.md` under "Box Provisioning RDD role-interface refactor (spec 0028)".

## Post-merge follow-up

**(Resolved in-PR by sub-phase B — see Scope above.)** Originally this section committed to a post-merge successor ADR for the §B.2 / §B.5 naming asymmetry. The successor ADR ([0059](docs/adr/0059-box-provisioning-abstract-base-naming-symmetry.md)) and the rename it documents both ship inside PR #4039 itself, eliminating the post-merge migration cost. No outstanding post-merge follow-ups remain for spec 0028.

