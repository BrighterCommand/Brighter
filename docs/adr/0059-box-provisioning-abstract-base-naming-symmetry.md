# 59. Naming symmetry for BoxProvisioning abstract bases — rename `RelationalBoxMigrationRunnerBase` → `SqlBoxMigrationRunner`

Date: 2026-05-13

## Status

Proposed

## Context

**Parent Requirement**: [specs/0028-box-provisioning-rdd-role-interfaces/requirements.md](../../specs/0028-box-provisioning-rdd-role-interfaces/requirements.md) — specifically NF8 (Naming policy for the §B.5 abstract base).

**Parent ADR**: [docs/adr/0058-box-provisioning-rdd-role-interfaces.md](0058-box-provisioning-rdd-role-interfaces.md) — specifically §B.5 (Naming subsection) and the Consequences → Risks and Mitigations entry titled "Naming asymmetry, time-bounded".

**Scope**: This ADR addresses a single architectural concern — the naming convention applied to the pair of abstract bases in the shared `Paramore.Brighter.BoxProvisioning` assembly. It does not introduce, remove, or alter any role, responsibility, contract, or behaviour. It is a Tidy First rename in the Beck sense: structural change with zero behavioural delta, separated from any feature work.

### The asymmetric pair as it stands today

Sub-phase A of spec 0028 (post-acceptance amendment, 2026-05-12 → 2026-05-13) introduced a new abstract base in the shared assembly:

- `SqlBoxProvisioner<TConnection, TTransaction>` (§B.5 of ADR 0058) — hosts the eight relational provisioner template method (`ProvisionAsync` sealed orchestration; `CreateConnection` / `PayloadColumnName` abstract hooks; `EffectiveSchemaName` virtual hook).

The parent spec (2028 main body, §B.2 of ADR 0058) had earlier introduced its sibling base:

- `RelationalBoxMigrationRunnerBase` (§B.2 of ADR 0058) — hosts the four relational migration-runner template method (sealed `MigrateAsync`, abstract `CreateConnection`, etc.).

Both bases share an identical role-shape: a shared-assembly abstract host for the relational-but-not-Spanner backend family. Yet their names diverge on three axes:

| Axis | §B.2 (today) | §B.5 (today) |
|---|---|---|
| Prefix | `Relational*` | `Sql*` |
| Suffix | `*Base` | (none) |
| Contract precision | Broad (Spanner is relational/SQL too) | Precise (names the `where TConnection : DbConnection` constraint) |

ADR 0058 §B.5 / NF8 chose `Sql*` (no `*Base`) for the new base on grounds documented at length there:

1. **Contract precision** — the base requires `where TConnection : DbConnection`, i.e. the ADO.NET `DbConnection` lineage with its `IDbCommand`/`DbCommand`-shaped query surface. "Relational" would name a broader semantic category that *includes* the exempt Spanner backend (Spanner IS relational/SQL per ADR 0057 §6, yet is excluded from this base by design). "Sql" names the lineage cleanly.
2. **Style symmetry with §A's role-interface family** — the §A interfaces follow the `IAmA*` style (`IAmABoxMigrationCatalog`, `IAmABoxPayloadModeValidator`, etc.) without a `*Base` suffix. Dropping `*Base` from §B.5 mirrors this.

The §B.5 decision was correct on its own terms. But it left the §B.2 sibling carrying both the imprecise prefix (`Relational*` includes Spanner-semantics it must exclude) and the now-deprecated `*Base` suffix.

### Why this asymmetry has to close

ADR 0058 §B.5 explicitly recorded the asymmetry as a transitional defect, with a mitigation that committed to a successor ADR:

> **Risk: Naming asymmetry between §B.2 (`RelationalBoxMigrationRunnerBase`) and §B.5 (`SqlBoxProvisioner`)** — a contributor pattern-matching from one base to find the sibling will not find them adjacent in the namespace.
> **Mitigation**: time-bounded by PR #4039. The PR description carries a "Post-merge follow-up" bullet committing to a successor ADR that renames `RelationalBoxMigrationRunnerBase` to a `Sql*`-prefixed equivalent for symmetry, authored before any third-party adopter ships against either base.

The original plan was to ship the rename in a separate post-merge ADR. **This ADR is that successor, brought into the scope of PR #4039 itself** rather than landing post-merge. The rationale for in-PR scope (decided 2026-05-13):

- **Single source-break window**. The rename is a public-surface source break (renamed public abstract class affects every downstream `: RelationalBoxMigrationRunnerBase` derivation and any explicit references in test doubles). Shipping it with the rest of the BoxProvisioning RDD refactor means downstream adopters absorb one rename in one release. Shipping it as a successor PR means two rename rounds across two releases — strictly worse for adopters.
- **The new §B.5 base only just landed on origin**. PR #4039 has not merged yet. No adopter has shipped against `SqlBoxProvisioner` or against the existing `RelationalBoxMigrationRunnerBase` in its current name. Closing the asymmetry now costs the project nothing in adopter-migration noise.
- **Coherent feature commit-set**. The whole BoxProvisioning RDD refactor lands as a single conceptual unit. The naming symmetry is part of the design's coherence, not an afterthought to be patched later.

### Constraints

- **No behaviour change**. The §B.2 abstract method set, sealed `MigrateAsync` orchestration, hook signatures, and DI cascade must be byte-for-byte equivalent before and after the rename.
- **TFM matrix unchanged** per parent C6 / C7 / NF11 — the rename is purely a symbol rename, so the TFM matrix is necessarily preserved.
- **Spanner runner is out of scope**. `SpannerBoxMigrationRunner` is free-standing per ADR 0057 §6; it implements `IAmABoxMigrationRunner` directly and does not derive from the §B.2 base. The Spanner runner's name and source are unaffected.
- **Test surface preservation**. Every test that exercises a relational migration runner (per-backend tests + the Phase 6 sibling-base contract tests) must remain green. Per the user's instruction, tests run **once** at the end of the rename, not at each derivation site — the rename is a name-tracking refactor where the compiler is the verification surface during the change.

## Decision

Rename the §B.2 sibling abstract base to align with the §B.5 naming convention:

- **Class rename**: `RelationalBoxMigrationRunnerBase` → `SqlBoxMigrationRunner`.
- **File rename**: `src/Paramore.Brighter.BoxProvisioning/RelationalBoxMigrationRunnerBase.cs` → `src/Paramore.Brighter.BoxProvisioning/SqlBoxMigrationRunner.cs`.

The new name applies the exact policy ADR 0058 §B.5 / NF8 established for `SqlBoxProvisioner`:

- `Sql*` prefix names the `where TConnection : DbConnection` constraint precisely (the same constraint applies to `RelationalBoxMigrationRunnerBase` today; the rename surfaces that fact in the name).
- No `*Base` suffix — mirrors §A's `IAmA*` interface family and §B.5's new abstract base.

The end-state pair becomes symmetric:

| Role | New name | Lineage constraint |
|---|---|---|
| Provisioner template host | `SqlBoxProvisioner<TConnection, TTransaction>` | `where TConnection : DbConnection` |
| Migration-runner template host | `SqlBoxMigrationRunner` | `where TConnection : DbConnection` (unchanged, surfaced in name) |

### Architecture overview

The role allocation is unchanged. Only labels move:

```
Paramore.Brighter.BoxProvisioning (shared assembly)
│
├── §A roles (instance interfaces — unchanged)
│   ├── IAmABoxMigrationCatalog
│   ├── IAmABoxMigrationDetectionHelper
│   ├── IAmABoxPayloadModeValidator
│   ├── IAmAProvisioningUnitOfWork<TTransaction>
│   └── IAmAVersionDetectingMigrationHelper
│
├── §B.2 abstract base — RENAMED HERE
│   ├── BEFORE: RelationalBoxMigrationRunnerBase   ← old
│   └── AFTER:  SqlBoxMigrationRunner              ← new
│
└── §B.5 abstract base (introduced sub-phase A — unchanged)
    └── SqlBoxProvisioner<TConnection, TTransaction>
```

The four relational derivations rebind their `: base` declaration:

```
Paramore.Brighter.BoxProvisioning.{MsSql,PostgreSql,MySql,Sqlite}
│
└── {Mssql,PostgreSql,MySql,Sqlite}BoxMigrationRunner
    BEFORE: : RelationalBoxMigrationRunnerBase
    AFTER:  : SqlBoxMigrationRunner
```

Spanner is exempt and unchanged:

```
Paramore.Brighter.BoxProvisioning.Spanner
│
└── SpannerBoxMigrationRunner : IAmABoxMigrationRunner   ← free-standing per ADR 0057 §6, NO rename
```

### Key components affected

| Component | File | Change |
|---|---|---|
| §B.2 abstract base — class | `src/Paramore.Brighter.BoxProvisioning/RelationalBoxMigrationRunnerBase.cs` | Class renamed + file renamed to `SqlBoxMigrationRunner.cs` |
| MSSQL runner — base chain | `src/Paramore.Brighter.BoxProvisioning.MsSql/MsSqlBoxMigrationRunner.cs` | `: RelationalBoxMigrationRunnerBase` → `: SqlBoxMigrationRunner` |
| PostgreSQL runner — base chain | `src/Paramore.Brighter.BoxProvisioning.PostgreSql/PostgreSqlBoxMigrationRunner.cs` | same |
| MySQL runner — base chain | `src/Paramore.Brighter.BoxProvisioning.MySql/MySqlBoxMigrationRunner.cs` | same |
| SQLite runner — base chain | `src/Paramore.Brighter.BoxProvisioning.Sqlite/SqliteBoxMigrationRunner.cs` | same |
| Tests — Phase 6 sibling-base contract tests | `tests/Paramore.Brighter.BoxProvisioning.Tests/` (six files referencing the §B.2 base name) | symbol references rebind |
| Tests — per-backend tests referencing the base by name | every `tests/Paramore.Brighter.{Backend}.Tests/BoxProvisioning/*.cs` that mentions `RelationalBoxMigrationRunnerBase` | symbol references rebind |
| xmldoc cross-references | any `/// <see cref="RelationalBoxMigrationRunnerBase"/>` | rebind to new name |
| ADR 0058 prose | references throughout 0058 (especially §B.2 sub-section heading + §B.5 Risks-and-Mitigations) | update the §B.2 name in prose; mark the "Naming asymmetry, time-bounded" risk as resolved by ADR 0059 |
| Spec 0028 prose | requirements.md NF8 + acceptance.md + traceability.md + baseline.md + release_notes.md | update the §B.2 name; record the rename's source-break in release_notes.md |
| Spanner runner | `src/Paramore.Brighter.BoxProvisioning.Spanner/SpannerBoxMigrationRunner.cs` | **NO CHANGE** (free-standing per ADR 0057 §6) |

### Technology choices

None — this is a symbol rename in the existing C# codebase. No new libraries, no new patterns, no new TFMs.

### Implementation approach

Pure Tidy First. The rename is mechanical and is best executed in this order (per `/spec:tasks` itemization to follow):

1. **Class + file rename** — git-aware rename of the source file, then class declaration rename. The C# compiler's name-binding immediately flags every consumer.
2. **Rebind derivations** — update the four relational runner derivations' `: base` declarations and any constructor `base(...)` calls (the constructor parameter list is unchanged, so `base(...)` argument lists are unaffected; only the class symbol changes).
3. **Rebind tests** — update Phase 6 sibling-base contract tests (six files) and any per-backend tests that mention the base by name. Test bodies are unchanged; only the type reference rebinds.
4. **Rebind xmldoc / prose** — `<see cref>` references, ADR 0058 prose, spec 0028 artefacts.
5. **Release notes** — add a source-break entry to `release_notes.md` documenting the rename. Reuse the existing spec-0028 release-notes section; this is a Sub-phase B amendment to that section.
6. **Run tests at the end** — the user instructed that tests run **once** at the end, not at each step. The compiler enforces correctness during the rename; tests verify the end-state behaviour is preserved.

The work fits in a small number of commits (likely 2-3): one Tidy First rename commit + one prose/release-notes commit + a final gate commit if needed.

## Consequences

### Positive

- **Symmetric pair of abstract bases**. `SqlBoxProvisioner` ↔ `SqlBoxMigrationRunner` — both prefixed `Sql`, both without `*Base` suffix, both named for the precise `DbConnection` lineage. A contributor pattern-matching from one to the other finds them adjacent in the namespace and stylistically matched.
- **Contract precision restored on §B.2**. The §B.2 name no longer suggests it admits Spanner-shaped derivations (it never did; the old name was misleading).
- **One source-break window**. Downstream adopters see exactly one rename, in the release that ships the new §B.5 base. No staircase of breaking changes across successive releases.
- **Resolves the documented "Naming asymmetry, time-bounded" risk in ADR 0058** — the risk's mitigation language ("authored before any third-party adopter ships against either base") is honoured by shipping the rename inside the same PR as the new base.
- **Naming convention enforced uniformly across the shared assembly**. Future BoxProvisioning abstract bases (if any) inherit the precedent.

### Negative

- **Public-surface source break for the §B.2 base name**. Any third-party adopter that derives from `RelationalBoxMigrationRunnerBase` will see a compile-time break and must rebind their derivation. Mitigated by:
  - PR #4039 has not merged; no adopter has shipped against either base yet.
  - The rename is announced in `release_notes.md` alongside the rest of the spec-0028 source breaks.
  - The rebind is mechanical (`: RelationalBoxMigrationRunnerBase` → `: SqlBoxMigrationRunner` and equivalents) — the kind of change an IDE rename refactor performs in seconds.
- **Spec 0028 documentation churn**. NF8, AC9, and several prose references in ADR 0058 / acceptance.md / traceability.md / baseline.md / release_notes.md mention the §B.2 name. All need rebinding. Mitigated by `/spec:tasks` itemization and the Tidy First commit shape.
- **One more entry in the spec's `.adr-list`**. Spec 0028 now lists two ADRs (0058 + 0059). Acceptable — the §B.5 amendment was itself a sub-phase A post-acceptance edit to ADR 0058, and 0059 sits naturally alongside as the symmetry-closing decision.

### Risks and mitigations

- **Risk: missed reference in xmldoc or test prose**. A `<see cref="RelationalBoxMigrationRunnerBase"/>` left un-rebound is silent at build time (xmldoc references degrade to broken-link comments) and only surfaces at doc-build time. **Mitigation**: post-rename `grep -r "RelationalBoxMigrationRunnerBase" .` must return zero matches before the rename commit lands. The /spec:tasks itemization includes that verification step.
- **Risk: missed reference in test-double class hierarchies**. Test doubles may derive from the §B.2 base directly. **Mitigation**: same grep covers test doubles. The compiler then catches anything `grep` misses.
- **Risk: future drift if a third abstract base joins the family**. Without an enforced policy, the precedent could erode. **Mitigation**: NF8 already encodes the policy ("`Sql*` prefix, no `*Base` suffix, contract-precision rationale"). Sub-phase B re-affirms NF8's scope to cover both bases.

## Alternatives Considered

### Alternative 1: defer the rename to a post-merge ADR (the original plan in ADR 0058)

**Rejected.** The original plan in ADR 0058 was exactly this — open a successor ADR after PR #4039 merges. Bringing the rename into PR #4039 is strictly better because:

- It produces one source-break window instead of two.
- It produces one set of release notes covering the whole RDD refactor (including the symmetry-closing rename) instead of splitting the rationale across two release notes.
- It preserves the coherent feature commit-set on a single PR.
- The original plan's only motivation was scope discipline ("don't widen sub-phase A"). That motivation no longer applies once sub-phase A is closed and we are deliberately opening sub-phase B to absorb the rename.

### Alternative 2: keep the `*Base` suffix — rename to `SqlBoxMigrationRunnerBase`

**Rejected.** This would close the prefix asymmetry but leave the suffix asymmetric with §B.5's `SqlBoxProvisioner` (no `*Base`). NF8 already established the project's policy: drop `*Base` to mirror the §A `IAmA*` interface family. Sub-phase B re-affirms that policy uniformly across both bases.

### Alternative 3: rename §B.5 to `Relational*` instead, matching §B.2's existing name

**Rejected per ADR 0058 §B.5 / NF8.** "Relational" is a broader semantic category that *includes* the exempt Spanner backend (Spanner IS relational/SQL per ADR 0057 §6, yet is excluded from this base). The new §B.5 name `SqlBoxProvisioner` was deliberately chosen on contract-precision grounds; rolling back that choice to absorb the asymmetry into §B.2's imprecision would entrench the very problem NF8 was written to avoid.

### Alternative 4: tolerate the asymmetry indefinitely

**Rejected.** ADR 0058 explicitly recorded the asymmetry as a transitional defect carrying a time-bound mitigation ("authored before any third-party adopter ships against either base"). The project has paid the design cost of the asymmetry; it would be perverse to refuse to bank the resolution now that the cost of resolving is minimal and the cost of *not* resolving grows monotonically with every release that ships the asymmetric pair.

## References

- **Requirements**: [specs/0028-box-provisioning-rdd-role-interfaces/requirements.md](../../specs/0028-box-provisioning-rdd-role-interfaces/requirements.md) — NF8 (Naming policy).
- **Parent ADR**: [docs/adr/0058-box-provisioning-rdd-role-interfaces.md](0058-box-provisioning-rdd-role-interfaces.md) — §B.5 (Naming subsection) + Consequences → Risks and Mitigations entry "Naming asymmetry, time-bounded".
- **Spanner exemption rationale**: [docs/adr/0057-box-schema-versioning-and-migrations.md](0057-box-schema-versioning-and-migrations.md) §6 (fresh-install-only model).
- **PR**: [#4039](https://github.com/BrighterCommand/Brighter/pull/4039) — carries specs 0023 + 0027 + 0028 stacked; sub-phase B amendments land on the same PR.
- **External reference (Tidy First)**: Kent Beck, *Tidy First? A Personal Exercise in Empirical Software Design* (O'Reilly, 2023) — the rename pattern this ADR follows is the canonical Tidy First "rename for clarity" case.
