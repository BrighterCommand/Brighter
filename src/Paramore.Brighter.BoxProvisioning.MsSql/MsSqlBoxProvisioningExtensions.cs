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
        options.Add(services =>
        {
            RegisterSharedRoleImpls(services);
            services.TryAddSingleton<MsSqlOutboxMigrationCatalog>();
            services.AddSingleton<IAmABoxProvisioner>(sp =>
            {
                var catalog = sp.GetRequiredService<MsSqlOutboxMigrationCatalog>();
                var runner = new MsSqlBoxMigrationRunner(catalog, configuration, options.MigrationLockTimeout,
                    tracer: sp.GetService<IAmABrighterTracer>(), scope: options.MigrationHistoryScope);
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
                    tracer: sp.GetService<IAmABrighterTracer>(), scope: options.MigrationHistoryScope);
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
                    tracer: sp.GetService<IAmABrighterTracer>(), scope: options.MigrationHistoryScope);
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
                    tracer: sp.GetService<IAmABrighterTracer>(), scope: options.MigrationHistoryScope);
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

    // Shared by the outbox and inbox registrations; TryAdd makes calling both safe.
    private static void RegisterSharedRoleImpls(IServiceCollection services)
    {
        services.TryAddSingleton<MsSqlBoxDetectionHelper>();
        services.TryAddSingleton<MsSqlPayloadModeValidator>();
    }
}
