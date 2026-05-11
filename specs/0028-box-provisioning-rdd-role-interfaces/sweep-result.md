# Spec 0028 Open-Closed Sweep Result (F9 / AC4)

**Captured:** 2026-05-11 (Phase 12 sign-off)
**HEAD at capture:** `346ae25e7`
**Branch:** `database_migration`

This file records the post-implementation re-walk of ADR 0058 §B.4's open-closed sweep candidate list per requirement F9 / acceptance criterion AC4. The sweep verifies that every "No" decision recorded in the ADR still holds against the shipped surface, and surfaces any new candidate that emerged during implementation.

## Method

For each of the four candidates in ADR 0058 §B.4, examine the post-implementation surface and reconfirm whether the original "No" decision is still appropriate. New candidates: walk the BoxProvisioning surface that was *not* addressed by §A (detection helpers, catalogues, payload validators) or §B.1–§B.3 (UoW interface, runner base, lifecycle contract) and ask whether any other family deserves an open-closed abstraction.

## Re-walk of ADR §B.4 candidates

### Candidate 1: `I*AdvisoryLock` family (spec 0027 Items D / M / N) — could share a base?

**Original decision (ADR §B.4):** No. Per-backend lock primitives have fundamentally different return semantics (PG `bool`, MySQL `bool?`, MSSQL throws). The UoW abstraction (§B.1) already hides the variation behind a uniform lifecycle.

**Post-implementation re-walk:** Confirmed No. The four shipped UoW classes (`MsSql`/`PostgreSql`/`MySql`/`SqliteProvisioningUnitOfWork`) own their backend-specific lock+tx ordering and lifecycle internally. The runner base (`RelationalBoxMigrationRunnerBase`) sees only the uniform `BeginAsync`/`CommitAsync`/`RollbackAsync`/`DisposeAsync` lifecycle. A shared lock-base interface would force a lowest-common-denominator return type (collapsing MySQL's `bool?` tri-state); the UoW pattern preserves backend-specific diagnostics inside each UoW's `RollbackAsync`/`DisposeAsync` per ADR §B.3. **No change required.**

### Candidate 2: `*Outbox/InboxMigrations` row data — V_n × backend cross product?

**Original decision (ADR §B.4):** No. Each migration row is unique DDL. Rows have nothing to share.

**Post-implementation re-walk:** Confirmed No. The eight shipped catalogue classes (`{MsSql,PostgreSql,MySql,Sqlite}{Outbox,Inbox}MigrationCatalog`) each declare the per-backend, per-box DDL chain. Rows do not share structure across backends — each backend's V_n DDL uses backend-specific syntax (`IF NOT EXISTS` placement, type names, default-value clauses, etc.). The `IAmABoxMigrationCatalog` interface (F3) gives the polymorphic surface; the rows themselves remain unique. **No change required.**

### Candidate 3: `BoxProvisioningHostedService` — open-closed via abstract base?

**Original decision (ADR §B.4):** No. Single class with no per-backend variants. Open-closed is satisfied by the existing `IAmABoxProvisioner` collection injection.

**Post-implementation re-walk:** Confirmed No. `src/Paramore.Brighter.BoxProvisioning/BoxProvisioningHostedService.cs` remains a single public class composing an `IEnumerable<IAmABoxProvisioner>`. The collection injection IS the open-closed point — a new backend ships its `{Backend}{Box}Provisioner` and DI extension; the hosted service requires no change. No abstract base is warranted because there are no derived hosted services. **No change required.**

### Candidate 4: `Identifiers.AssertSafe` (spec 0027 Item Q) helper — interface?

**Original decision (ADR §B.4):** No. Already a single static method in the shared `Paramore.Brighter.BoxProvisioning` assembly. No polymorphic role.

**Post-implementation re-walk:** Confirmed No. `src/Paramore.Brighter.BoxProvisioning/Identifiers.cs` remains `public static class Identifiers`. Its sole role is input validation — there is no per-backend variation in what constitutes a safe identifier (the validation is regex-based and deliberately strict for SQL injection defence per ADR 0057). An interface would add a polymorphism layer with no implementations to vary. **No change required.**

## New-candidate sweep

Surface families *not* addressed by §A or §B that I considered for an open-closed abstraction during implementation:

| Family | Files | Decision | Reason |
|---|---|---|---|
| Per-backend `Add{Backend}Outbox`/`Add{Backend}Inbox` DI extensions | `BoxProvisioning.{Backend}/Add{Backend}{Box}Extensions.cs` (or equivalent named class) | **No** | Extension methods on `BoxProvisioningOptions`. Polymorphism via static method dispatch is not a real role; no caller would benefit from an interface. Per requirements §Out of Scope: feedback item 2 (DI-extension role interface) was explicitly dropped. |
| `BoxProvisioningOptions` — single class consumed by all backends | `src/Paramore.Brighter.BoxProvisioning/BoxProvisioningOptions.cs` | **No** | Configuration carrier; no per-backend variant. Open-closed via additive properties (existing pattern). |
| `BoxTableState` record | `src/Paramore.Brighter.BoxProvisioning/BoxTableState.cs` | **No** | Plain data carrier (`record bool TableExists, bool HistoryExists, int CurrentVersion`). No polymorphic role. |
| `BoxType` enum | `src/Paramore.Brighter.BoxProvisioning/BoxType.cs` | **No** | Enum (Outbox / Inbox). Already maximally constrained. |

**No new candidates surfaced during Phase 1–11 implementation.** Spec scope does not need to expand; no further `/spec:review code` round required.

## Conclusion

ADR 0058 §B.4's four "No" decisions all hold post-implementation. No new candidates emerged. F9 is fully discharged; AC4 is dischargeable.
