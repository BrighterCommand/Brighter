using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.BoxProvisioning.MsSql;

/// <summary>
/// Extension methods for registering MSSQL box provisioners with <see cref="BoxProvisioningOptions"/>.
/// </summary>
public static class MsSqlBoxProvisioningExtensions
{
    /// <summary>
    /// Register MSSQL outbox provisioning with an explicit configuration.
    /// </summary>
    public static BoxProvisioningOptions AddMsSqlOutbox(
        this BoxProvisioningOptions options,
        IAmARelationalDatabaseConfiguration configuration)
    {
        // MigrationLockTimeout is read inside the registration lambda so it stays late-bound:
        // the configure delegate may set the timeout AFTER calling Add*Outbox/Add*Inbox and
        // the change still takes effect (registrations only run once the configure delegate
        // has fully completed inside UseBoxProvisioning).
        options.Add(services =>
        {
            RegisterSharedRoleImpls(services);
            services.TryAddSingleton<MsSqlOutboxMigrationCatalog>();
            services.AddSingleton<IAmABoxProvisioner>(sp =>
            {
                var catalog = sp.GetRequiredService<MsSqlOutboxMigrationCatalog>();
                var runner = new MsSqlBoxMigrationRunner(catalog, configuration, options.MigrationLockTimeout,
                    tracer: sp.GetService<IAmABrighterTracer>());
                return new MsSqlOutboxProvisioner(
                    sp.GetRequiredService<MsSqlBoxDetectionHelper>(),
                    catalog,
                    sp.GetRequiredService<MsSqlPayloadModeValidator>(),
                    configuration,
                    runner);
            });
        });
        return options;
    }

    /// <summary>
    /// Register MSSQL outbox provisioning with a connection name resolved from IConfiguration at runtime.
    /// </summary>
    public static BoxProvisioningOptions AddMsSqlOutbox(
        this BoxProvisioningOptions options,
        string connectionName,
        string? outboxTableName = null,
        string? schemaName = null,
        bool binaryMessagePayload = false)
    {
        options.Add(services =>
        {
            RegisterSharedRoleImpls(services);
            services.TryAddSingleton<MsSqlOutboxMigrationCatalog>();
            services.AddSingleton<IAmABoxProvisioner>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var connectionString = config.GetConnectionString(connectionName)
                    ?? throw new InvalidOperationException(
                        $"Connection string '{connectionName}' not found in configuration.");
                var dbConfig = new RelationalDatabaseConfiguration(
                    connectionString,
                    outBoxTableName: outboxTableName ?? "Outbox",
                    schemaName: schemaName,
                    binaryMessagePayload: binaryMessagePayload);
                var catalog = sp.GetRequiredService<MsSqlOutboxMigrationCatalog>();
                var runner = new MsSqlBoxMigrationRunner(catalog, dbConfig, options.MigrationLockTimeout,
                    tracer: sp.GetService<IAmABrighterTracer>());
                return new MsSqlOutboxProvisioner(
                    sp.GetRequiredService<MsSqlBoxDetectionHelper>(),
                    catalog,
                    sp.GetRequiredService<MsSqlPayloadModeValidator>(),
                    dbConfig,
                    runner);
            });
        });
        return options;
    }

    /// <summary>
    /// Register MSSQL inbox provisioning with an explicit configuration.
    /// </summary>
    public static BoxProvisioningOptions AddMsSqlInbox(
        this BoxProvisioningOptions options,
        IAmARelationalDatabaseConfiguration configuration)
    {
        options.Add(services =>
        {
            RegisterSharedRoleImpls(services);
            services.TryAddSingleton<MsSqlInboxMigrationCatalog>();
            services.AddSingleton<IAmABoxProvisioner>(sp =>
            {
                var catalog = sp.GetRequiredService<MsSqlInboxMigrationCatalog>();
                var runner = new MsSqlBoxMigrationRunner(catalog, configuration, options.MigrationLockTimeout,
                    tracer: sp.GetService<IAmABrighterTracer>());
                return new MsSqlInboxProvisioner(
                    sp.GetRequiredService<MsSqlBoxDetectionHelper>(),
                    catalog,
                    sp.GetRequiredService<MsSqlPayloadModeValidator>(),
                    configuration,
                    runner);
            });
        });
        return options;
    }

    /// <summary>
    /// Register MSSQL inbox provisioning with a connection name resolved from IConfiguration at runtime.
    /// </summary>
    public static BoxProvisioningOptions AddMsSqlInbox(
        this BoxProvisioningOptions options,
        string connectionName,
        string? inboxTableName = null,
        string? schemaName = null,
        bool binaryMessagePayload = false)
    {
        options.Add(services =>
        {
            RegisterSharedRoleImpls(services);
            services.TryAddSingleton<MsSqlInboxMigrationCatalog>();
            services.AddSingleton<IAmABoxProvisioner>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var connectionString = config.GetConnectionString(connectionName)
                    ?? throw new InvalidOperationException(
                        $"Connection string '{connectionName}' not found in configuration.");
                var dbConfig = new RelationalDatabaseConfiguration(
                    connectionString,
                    inboxTableName: inboxTableName ?? "Inbox",
                    schemaName: schemaName,
                    binaryMessagePayload: binaryMessagePayload);
                var catalog = sp.GetRequiredService<MsSqlInboxMigrationCatalog>();
                var runner = new MsSqlBoxMigrationRunner(catalog, dbConfig, options.MigrationLockTimeout,
                    tracer: sp.GetService<IAmABrighterTracer>());
                return new MsSqlInboxProvisioner(
                    sp.GetRequiredService<MsSqlBoxDetectionHelper>(),
                    catalog,
                    sp.GetRequiredService<MsSqlPayloadModeValidator>(),
                    dbConfig,
                    runner);
            });
        });
        return options;
    }

    // Detection helper + payload validator are shared across Outbox and Inbox extensions;
    // TryAddSingleton makes the registration idempotent so calling both Add*Outbox and
    // Add*Inbox does not produce duplicate singletons. Catalogue registration is per
    // box-type and lives in the call-site (Outbox catalogue from Add*Outbox; Inbox
    // catalogue from Add*Inbox) per ADR 0058 §A.4 Alternatives.
    private static void RegisterSharedRoleImpls(IServiceCollection services)
    {
        services.TryAddSingleton<MsSqlBoxDetectionHelper>();
        services.TryAddSingleton<MsSqlPayloadModeValidator>();
    }
}
