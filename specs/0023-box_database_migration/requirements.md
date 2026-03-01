# Requirements: Box Database Migration

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/0053-box-database-migration.md`.

## Problem Statement

As a developer using Brighter, I would like a library that helps me create and migrate Outbox and Inbox database tables, so that I don't have to write boilerplate DDL setup code for each database backend and can evolve my schemas over time.

Today, Brighter provides static builder classes (e.g. `SqlInboxBuilder`, `MsSqlOutboxBuilder`) that return DDL strings for creating inbox/outbox tables. However:

- **No migration support**: The builders only support initial table creation via "CREATE IF NOT EXISTS". There is no mechanism to evolve schemas when Brighter adds new columns or changes types between versions.
- **No unified API**: Each builder has a different static method signature (e.g. `GetExistsQuery` takes different parameters across providers; Spanner lacks it entirely). There is no common interface or abstraction.
- **Sample-only orchestration**: The only orchestration code lives in `samples/WebAPI/WebAPI_Common/DbMaker/SchemaCreation.cs`, which is a sample — not a reusable library. It mixes concerns (local app DB creation, inbox, outbox) and is tightly coupled to the sample's configuration patterns.
- **No .NET Aspire integration**: The codebase has no Aspire `AppHost`, `ServiceDefaults`, or service discovery integration. Developers using Aspire must wire up connections manually.

## Proposed Solution

Provide a set of NuGet libraries that allow developers to create and migrate Inbox and Outbox tables with a simple, unified API. The solution should:

1. **Offer a common abstraction** for box (inbox/outbox) database provisioning and migration, so that switching between database backends requires changing only registration — not application code.
2. **Support schema migrations** for relational databases, so that upgrading Brighter versions automatically applies any required schema changes to existing inbox/outbox tables.
3. **Be modular**: each database backend is a separate NuGet package with a shared core, making it easy to add new backends without modifying existing code.
4. **Integrate with .NET Aspire** for connection management and service discovery, allowing developers to use Aspire's resource model to provision and connect to their inbox/outbox databases.

## Requirements

### Functional Requirements

#### FR-1: Unified Box Provisioning API
- Provide a common interface/abstraction for creating inbox and outbox tables across all supported relational database backends.
- Support the five existing relational backends: **MSSQL, MySQL, PostgreSQL, SQLite, Google Cloud Spanner**.
- The API should be callable from `IHost` startup (e.g. as an extension method) or from a DI-configured service.

#### FR-2: Schema Migration Support
- For relational databases that support schemas (MSSQL, MySQL, PostgreSQL), provide a migration mechanism that can evolve inbox/outbox table schemas between Brighter versions.
- Migrations should be idempotent — running them multiple times should be safe.
- Track which migrations have been applied (migration version tracking).
- Support both text and binary message payload variants where applicable.

#### FR-3: Modular Package Structure
- A core/abstractions package that defines the common interfaces and migration infrastructure.
- Per-database-backend packages (e.g. `Paramore.Brighter.Extensions.Hosting.MsSql`) that implement the abstractions for each backend.
- Adding a new database backend should only require implementing the backend-specific package — no changes to the core.

#### FR-4: .NET Aspire Integration
- Provide Aspire hosting integration packages that allow developers to wire up inbox/outbox database provisioning through Aspire's `IDistributedApplicationBuilder`.
- Support Aspire's connection string resolution and service discovery for database connections.
- Follow Aspire's conventions for hosting extensions (e.g. `AddBrighterOutbox()` on the app model).

#### FR-5: Startup/Hosting Extensions
- Provide `IHostBuilder` / `IServiceCollection` extension methods for non-Aspire scenarios.
- Allow configuration of which box type (inbox, outbox, or both) to provision.
- Allow configuration of table names, schemas, and payload format (text/binary/JSON).

### Non-functional Requirements

#### NFR-1: Backward Compatibility
- Existing applications using the current static builder classes must continue to work without changes.
- The new library is additive — it does not replace or remove the existing builders.

#### NFR-2: Startup Performance
- Box provisioning and migration should complete quickly at application startup.
- Migration version checks should be lightweight (single query to check current version).

#### NFR-3: Safety
- Migrations must never drop data or columns without explicit opt-in.
- Failed migrations should leave the database in a consistent state (use transactions where supported).

#### NFR-4: Testability
- The abstractions should be mockable/substitutable for unit testing.
- Integration tests should be possible using the same test infrastructure as existing Brighter tests (Docker containers via test fixtures).

### Constraints and Assumptions

- **Relational focus for migrations**: Schema migrations only apply to relational backends (MSSQL, MySQL, PostgreSQL, SQLite, Spanner). NoSQL backends (DynamoDB, MongoDB, Firestore) handle schema evolution differently and are out of scope for migration support.
- **Brighter-managed schemas only**: The migration system manages only inbox and outbox tables — not the application's own domain tables. Application database migration remains the developer's responsibility.
- **.NET 9+**: The Aspire integration targets .NET 9 or later, consistent with Aspire's requirements.
- **Existing builder DDL**: The initial "create" migration for each backend will reuse the DDL from the existing builder classes rather than rewriting it.

### Out of Scope

- **Application database migration**: Migration of the developer's own domain/business tables.
- **NoSQL migration**: Schema evolution for DynamoDB, MongoDB, or Firestore backends.
- **Data migration**: Moving data between database backends (e.g. migrating from MSSQL to PostgreSQL).
- **Aspire resource provisioning**: Actually provisioning database server instances (e.g. spinning up a PostgreSQL container). Aspire integration covers connection wiring, not infrastructure provisioning.
- **Queue/transport table provisioning**: The `MsSqlQueueBuilder` and similar transport-level tables are not covered by this feature.
- **Removing the existing static builders**: The current `*InboxBuilder` / `*OutboxBuilder` classes remain as-is.

## Acceptance Criteria

### AC-1: Create Tables
- Given a new application with no existing inbox/outbox tables, when the application starts with box provisioning configured, then the correct inbox and/or outbox tables are created for the configured database backend.

### AC-2: Migrate Tables
- Given an existing application with inbox/outbox tables from a previous Brighter version, when the application starts with box provisioning configured and a new migration is available, then the schema is updated to the latest version without data loss.

### AC-3: Idempotent Migrations
- Given an application whose inbox/outbox tables are already at the latest schema version, when the application starts with box provisioning configured, then no schema changes are made and startup completes normally.

### AC-4: Multiple Backends
- Given the same application code using the unified API, when switching from one database backend to another (e.g. MSSQL to PostgreSQL) by changing only DI registration, then the correct backend-specific DDL is used.

### AC-5: Aspire Integration
- Given an Aspire AppHost that includes a Brighter service with outbox configuration, when the application starts, then the outbox database connection is resolved through Aspire's service discovery and the outbox table is provisioned.

### AC-6: Backward Compatibility
- Given an existing application that uses the static builder classes directly, when upgrading to the Brighter version that includes this feature, then the existing code continues to compile and work without changes.

## Additional Context

### Current Builder Inconsistencies to Address
- `GetExistsQuery` parameter signatures differ across providers (schema name vs. catalog vs. none).
- Spanner builders lack `GetExistsQuery` entirely.
- PostgreSQL outbox binary variant omits `IF NOT EXISTS` (unlike the text variant).
- Spanner outbox builder is missing `DataRef` and `SpecVersion` columns compared to other backends.

### Existing Code References
- Inbox builders: `src/Paramore.Brighter.Inbox.{MsSql,MySql,Postgres,Sqlite,Spanner}/`
- Outbox builders: `src/Paramore.Brighter.Outbox.{MsSql,MySql,PostgreSql,Sqlite,Spanner}/`
- Sample DbMaker: `samples/WebAPI/WebAPI_Common/DbMaker/SchemaCreation.cs`
- DynamoDB table factory: `src/Paramore.Brighter.DynamoDb/DynamoDbTableFactory.cs`
