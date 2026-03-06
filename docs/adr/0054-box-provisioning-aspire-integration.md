# 54. Box Provisioning Aspire Integration

Date: 2026-03-06

## Status

Proposed

## Context

**Parent Requirement**: [specs/0023-box_database_migration/requirements.md](../../specs/0023-box_database_migration/requirements.md)

**Related ADR**: [ADR 0053 — Box Database Migration](0053-box-database-migration.md)

ADR 0053 defines the core box provisioning library: interfaces, migration runner, backend provisioners, and hosted service. That architecture is self-contained — it takes a connection string via `IAmARelationalDatabaseConfiguration` and provisions tables at startup.

This ADR addresses how box provisioning integrates with .NET Aspire and, more broadly, with `IConfiguration`-based connection string resolution. Aspire uses `IConfiguration` to inject connection strings via service discovery, so the two concerns are closely related.

Brighter's current configuration model is **code-first**: consumers construct typed configuration objects (e.g. `RelationalDatabaseConfiguration`) explicitly in their DI setup. There is no `IConfiguration` binding — connection strings are passed as constructor arguments, not read from `appsettings.json` or environment variables. This is deliberate: it keeps Brighter's configuration explicit and avoids hidden dependencies on configuration key conventions.

However, Aspire's model assumes that components resolve connection strings from `IConfiguration` via `builder.Configuration.GetConnectionString(name)`. Bridging these two models without compromising Brighter's code-first approach requires design decisions.

## Open Questions

The following questions must be resolved before this ADR can move to Accepted:

### 1. IConfiguration Integration Scope

Brighter does not currently read from `IConfiguration`. Should this ADR introduce a narrow integration point — specifically for connection string resolution — or should it remain Aspire-only?

**Option A — Aspire-only**: The Aspire service-side packages (e.g. `Paramore.Brighter.BoxProvisioning.Aspire.MsSql`) provide `connectionName`-based overloads that resolve connection strings from `IConfiguration` internally. Non-Aspire users are unaffected.

**Option B — General IConfiguration bridge**: Introduce a small integration point where `IConfiguration` can supply connection strings to Brighter's configuration objects. This would benefit Aspire users and also non-Aspire users who prefer `appsettings.json`-based configuration. The bridge would be limited to connection strings — it would not attempt to serialize/deserialize Brighter's typed configuration objects from configuration sections.

Key constraint for either option: `IConfiguration` integration supplies **connection strings only**, not serialized types. Brighter's configuration remains code-first. The integration simply provides an alternative source for the connection string value that gets passed into `RelationalDatabaseConfiguration`.

### 2. Aspire Package Structure

ADR 0053 proposed two package tiers:
- **`Paramore.Brighter.BoxProvisioning.Aspire.Hosting`** — AppHost-side (`WithBrighterOutbox()` on `IDistributedApplicationBuilder`)
- **`Paramore.Brighter.BoxProvisioning.Aspire.{Backend}`** — Service-side per-backend packages

Questions:
- Is a per-backend service-side package warranted, or can a single `Paramore.Brighter.BoxProvisioning.Aspire` package handle all backends via the connection name?
- Does the AppHost-side package provide enough value to justify its own package, or is documentation + environment variable conventions sufficient?

### 3. Aspire Testing Patterns

The Brighter codebase has no existing Aspire test infrastructure. Before implementing:
- What does Aspire's `DistributedApplicationTestingBuilder` require?
- Can Aspire integration tests run without Docker, or do they inherently require container orchestration?
- Should Aspire tests live in the existing test structure or in a separate test project?

A spike is likely needed to answer these questions empirically.

### 4. Aspire API Stability

Aspire reached GA but continues to evolve. The extension point APIs (`IDistributedApplicationBuilder`, resource annotations, environment variable injection) may change between versions.
- Which Aspire version should we target?
- How thin can the wrapper be to minimize exposure to API churn?
- Should the Aspire packages track Aspire's release cadence independently of Brighter's?

## Decision

To be determined once the open questions are resolved.

## Consequences

To be determined.

## References

- [ADR 0053 — Box Database Migration](0053-box-database-migration.md)
- [.NET Aspire custom component authoring](https://learn.microsoft.com/en-us/dotnet/aspire/extensibility/custom-component)
- [.NET Aspire service discovery](https://learn.microsoft.com/en-us/dotnet/aspire/service-discovery/overview)
