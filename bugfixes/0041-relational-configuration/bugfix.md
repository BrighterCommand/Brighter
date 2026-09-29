# Bugfix: Reuse relational outbox configuration for provider activation

**Linked Issue**: #4279
**Status**: Verified locally — approved eager-overload scope

## Symptom

A relational outbox holds valid database configuration, but independently activated connection and transaction providers cannot resolve that configuration from DI.
Registration succeeds; provider activation later fails with a missing `IAmARelationalDatabaseConfiguration` dependency.

## Suspected Location

- `src/Paramore.Brighter/RelationDatabaseOutbox.cs:27` retains configuration in a protected property.
- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:299` registers the eager producer configuration, provider types, and outbox separately.
- The deferred overload at line 435 activates the transaction provider at line 476.
- Its outbox factory resolves the transaction provider before returning the outbox, at line 483.
- `src/Paramore.Brighter.Sqlite/SqliteTransactionProvider.cs:45` and `SqliteConnectionProvider.cs:45` require the configuration interface.

## Root-Cause Hypothesis

Original hypothesis, now confirmed by code trace:
`AddProducers` does not register the configuration retained by the relational outbox for its DI-activated providers.
Option A would reuse that configuration when no explicit interface registration exists.
The MsSql, MySql, PostgreSql, Sqlite, and Spanner outboxes share the relational base class.

## Confirmed Root Cause

`AddProducers` registers provider types without the configuration interface that their constructors require.
The supplied relational outbox retains the configuration, but DI cannot access its protected property.
Outbox construction and provider activation therefore use disconnected configuration paths.

## Evidence

- `RelationDatabaseOutbox.cs:27` retains the constructor configuration.
- `ServiceCollectionExtensions.cs:341` and line 342 register transaction-provider types without their configuration.
- Lines 348 and 367 retain and register the supplied outbox independently.
- Lines 888 and 893 register the connection-provider interfaces without their configuration.
- Line 700 resolves the transaction provider during command-processor construction, exposing the missing dependency.
- `SqliteConnectionProvider.cs:45` and `SqliteTransactionProvider.cs:45` require the configuration interface, but do not open a connection in their constructors.

The five relational outbox implementations inherit the same base class, including Spanner.
The regression run reproduced the missing registration without database I/O: five cases failed on each framework before implementation.
The failures were missing-registration assertions, not build or infrastructure errors.

## Scope Notes

Approved scope:

- Implement Option A for `AddProducers(Action<ProducersConfiguration>, ...)`.
- Expose the existing base configuration as a documented public read-only property.
- Register that exact instance with `TryAddSingleton<IAmARelationalDatabaseConfiguration>` only for relational outboxes.
- Preserve existing explicit interface registrations, including scoped descriptors and factory registrations.
- Later ordinary `AddSingleton` or `AddScoped` registrations remain authoritative for single-service resolution.
- Do not promise that later `TryAdd` replaces the fallback, or that enumeration contains only one configuration after a later registration.
- Do not modify non-relational outboxes, provider constructor selection, or provider lifetimes.
- Leave the deferred `Func<IServiceProvider, ProducersConfiguration>` overload unchanged and document its explicit-registration requirement.

The deferred overload is not equivalent to the eager overload.
At `ServiceCollectionExtensions.cs:483`, resolving the outbox first requires the transaction provider, so deriving configuration through that outbox creates a cycle.
Deriving configuration through the producer factory can also recurse if its callback requests configuration from DI before constructing the outbox.
The deferred overload's existing absence of connection-provider registration is a separate concern, not part of this fix.

## Regression Test

The six files in `tests/Paramore.Brighter.Extensions.Tests/RelationalConfiguration/` contain 11 cases per target framework:

- `When_configuring_relational_producers_should_reuse_outbox_configuration.cs`: three cases cover the original configuration instance, command-processor and provider activation, and transient/scoped/singleton provider lifetimes.
- `When_registering_explicit_configuration_first_should_preserve_it.cs`: two cases preserve an existing singleton instance or lazy singleton factory.
- `When_registering_configuration_after_producers_should_override_the_fallback.cs`: two cases require the automatic registration, then verify a later singleton or scoped registration wins single-service resolution.
- `When_registering_scoped_configuration_first_should_preserve_scope_identity.cs`: one case checks distinct configuration instances across scopes and successful scoped provider activation.
- `When_configuring_non_relational_producers_should_not_register_database_configuration.cs`: two cases cover the default and explicitly supplied in-memory outbox.
- `When_deferring_producer_configuration_should_preserve_optional_database_lookup.cs`: one case preserves deferred execution and an optional configuration lookup inside the callback.

The tests use real Sqlite outboxes and providers without opening connections or creating tables.
They introduce no test doubles, request types, closed handler/mapper generic types, or package dependencies.
The first and third files failed before the fix because the automatic configuration registration was absent.
The other six cases characterized existing behavior and initially passed.

Approved named mutations, each observed to fail for the intended assertions on both frameworks:

1. `OverwriteExplicitConfiguration`: temporarily replace the new relational `TryAddSingleton` with `AddSingleton`. Existing instance/factory and scoped-configuration cases must fail.
2. `InventNonRelationalConfiguration`: temporarily add a relational-configuration descriptor for a non-relational outbox. Both non-relational cases must fail their absence assertion.
3. `AddDeferredConfigurationFallback`: temporarily register a configuration factory in the deferred overload that reads the producer configuration's relational outbox. The deferred case must reject that descriptor before executing the potentially recursive callback.

All mutations were removed. The unchanged tests pass against the final implementation.
The test-authoring build succeeded for .NET 9 and .NET 10 with 366 warnings and zero errors.

## Fix

- `RelationDatabaseOutbox.DatabaseConfiguration` is now a documented public read-only getter for the existing configuration instance.
- The eager `AddProducers` overload conditionally registers that instance through `TryAddSingleton<IAmARelationalDatabaseConfiguration>` after validating outbox compatibility.
- XML documentation and `release_notes.md` describe registration precedence and the unchanged deferred-overload requirement.
- No new packages, backend-specific dependencies, provider lifetime changes, or CI changes were needed. The main CI job already runs the Extensions suite.

## Verification

All runs used Release configuration on .NET 9 and .NET 10.

| Check | .NET 9 | .NET 10 |
| --- | --- | --- |
| Before fix | 5 failed, 6 passed | 5 failed, 6 passed |
| After fix | 11 passed | 11 passed |
| Overwrite explicit configuration mutation | 3 expected failures | 3 expected failures |
| Invent non-relational configuration mutation | 2 expected failures | 2 expected failures |
| Add deferred fallback mutation | 1 expected failure | 1 expected failure |
| Full Extensions suite, mutations removed | 645 passed, no skips | 642 passed, no skips |
| Full Core suite | 1,494 passed, 7 existing skips | 1,494 passed, 7 existing skips |

The full suites ran without filters and reported no failures after mutation removal.
DI and all five relational outbox projects built successfully across their configured target frameworks, including their Core dependency.
All six final project build commands reported zero warnings and zero errors.
Tracked and new-file whitespace checks passed.

The regression tests exercise DI registration and real Sqlite provider activation without opening database connections.
Live relational backend integration suites were not run. No remote CI result is claimed.

Reproduce the focused tests:

```bash
dotnet test tests/Paramore.Brighter.Extensions.Tests/Paramore.Brighter.Extensions.Tests.csproj -c Release --filter 'FullyQualifiedName~Paramore.Brighter.Extensions.Tests.RelationalConfiguration.'
```

Reproduce the broader checks:

```bash
dotnet test tests/Paramore.Brighter.Extensions.Tests/Paramore.Brighter.Extensions.Tests.csproj -c Release
dotnet test tests/Paramore.Brighter.Core.Tests/Paramore.Brighter.Core.Tests.csproj -c Release
```
