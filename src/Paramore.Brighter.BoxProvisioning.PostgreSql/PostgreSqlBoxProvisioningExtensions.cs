using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.BoxProvisioning.PostgreSql;

/// <summary>
/// Extension methods for registering PostgreSQL box provisioners with <see cref="BoxProvisioningOptions"/>.
/// </summary>
public static class PostgreSqlBoxProvisioningExtensions
{
    /// <summary>
    /// Register PostgreSQL outbox provisioning with an explicit configuration.
    /// </summary>
    public static BoxProvisioningOptions AddPostgreSqlOutbox(
        this BoxProvisioningOptions options,
        IAmARelationalDatabaseConfiguration configuration)
    {
        options.Add(services =>
        {
            RegisterSharedRoleImpls(services);
            services.TryAddSingleton<PostgreSqlOutboxMigrationCatalog>();
            services.AddSingleton<IAmABoxProvisioner>(sp =>
            {
                var catalog = sp.GetRequiredService<PostgreSqlOutboxMigrationCatalog>();
                var runner = new PostgreSqlBoxMigrationRunner(catalog, configuration, options.MigrationLockTimeout,
                    tracer: sp.GetService<IAmABrighterTracer>(), scope: options.MigrationHistoryScope);
                return new PostgreSqlOutboxProvisioner(
                    sp.GetRequiredService<PostgreSqlBoxDetectionHelper>(),
                    catalog,
                    sp.GetRequiredService<PostgreSqlPayloadModeValidator>(),
                    configuration,
                    runner);
            });
        });
        return options;
    }

    /// <summary>
    /// Register PostgreSQL outbox provisioning with a connection name resolved from IConfiguration at runtime.
    /// </summary>
    public static BoxProvisioningOptions AddPostgreSqlOutbox(
        this BoxProvisioningOptions options,
        string connectionName,
        string? outboxTableName = null,
        string? schemaName = null,
        bool binaryMessagePayload = false)
    {
        options.Add(services =>
        {
            RegisterSharedRoleImpls(services);
            services.TryAddSingleton<PostgreSqlOutboxMigrationCatalog>();
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
                var catalog = sp.GetRequiredService<PostgreSqlOutboxMigrationCatalog>();
                var runner = new PostgreSqlBoxMigrationRunner(catalog, dbConfig, options.MigrationLockTimeout,
                    tracer: sp.GetService<IAmABrighterTracer>(), scope: options.MigrationHistoryScope);
                return new PostgreSqlOutboxProvisioner(
                    sp.GetRequiredService<PostgreSqlBoxDetectionHelper>(),
                    catalog,
                    sp.GetRequiredService<PostgreSqlPayloadModeValidator>(),
                    dbConfig,
                    runner);
            });
        });
        return options;
    }

    /// <summary>
    /// Register PostgreSQL inbox provisioning with an explicit configuration.
    /// </summary>
    public static BoxProvisioningOptions AddPostgreSqlInbox(
        this BoxProvisioningOptions options,
        IAmARelationalDatabaseConfiguration configuration)
    {
        options.Add(services =>
        {
            RegisterSharedRoleImpls(services);
            services.TryAddSingleton<PostgreSqlInboxMigrationCatalog>();
            services.AddSingleton<IAmABoxProvisioner>(sp =>
            {
                var catalog = sp.GetRequiredService<PostgreSqlInboxMigrationCatalog>();
                var runner = new PostgreSqlBoxMigrationRunner(catalog, configuration, options.MigrationLockTimeout,
                    tracer: sp.GetService<IAmABrighterTracer>(), scope: options.MigrationHistoryScope);
                return new PostgreSqlInboxProvisioner(
                    sp.GetRequiredService<PostgreSqlBoxDetectionHelper>(),
                    catalog,
                    sp.GetRequiredService<PostgreSqlPayloadModeValidator>(),
                    configuration,
                    runner);
            });
        });
        return options;
    }

    /// <summary>
    /// Register PostgreSQL inbox provisioning with a connection name resolved from IConfiguration at runtime.
    /// </summary>
    public static BoxProvisioningOptions AddPostgreSqlInbox(
        this BoxProvisioningOptions options,
        string connectionName,
        string? inboxTableName = null,
        string? schemaName = null,
        bool binaryMessagePayload = false)
    {
        options.Add(services =>
        {
            RegisterSharedRoleImpls(services);
            services.TryAddSingleton<PostgreSqlInboxMigrationCatalog>();
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
                var catalog = sp.GetRequiredService<PostgreSqlInboxMigrationCatalog>();
                var runner = new PostgreSqlBoxMigrationRunner(catalog, dbConfig, options.MigrationLockTimeout,
                    tracer: sp.GetService<IAmABrighterTracer>(), scope: options.MigrationHistoryScope);
                return new PostgreSqlInboxProvisioner(
                    sp.GetRequiredService<PostgreSqlBoxDetectionHelper>(),
                    catalog,
                    sp.GetRequiredService<PostgreSqlPayloadModeValidator>(),
                    dbConfig,
                    runner);
            });
        });
        return options;
    }

    /// <summary>
    /// Register provisioning of the PostgreSQL tripped topics table that distributed circuit-breaker
    /// instances share. Pass the same table name to <c>PostgreSqlTrippedTopicStore</c>.
    /// </summary>
    /// <param name="options">The box provisioning options.</param>
    /// <param name="configuration">Supplies the connection string, and the schema when <paramref name="schemaName"/> is null.</param>
    /// <param name="tableName">The name of the trip table, at most 40 characters so that the index name <c>idx_{tableName}_ended_at_trip_time</c> fits PostgreSQL's 63-character limit.</param>
    /// <param name="schemaName">The schema holding the trip table, or null to use the configuration's schema.</param>
    public static BoxProvisioningOptions AddPostgreSqlTrippedTopics(
        this BoxProvisioningOptions options,
        IAmARelationalDatabaseConfiguration configuration,
        string tableName = "BrighterTrippedTopics",
        string? schemaName = null)
    {
        options.Add(services =>
        {
            RegisterSharedRoleImpls(services);
            services.AddSingleton<IAmABoxProvisioner>(sp =>
                TrippedTopicsProvisioner.Create(tableName, schemaName, configuration, effectiveSchema =>
                    new PostgreSqlBoxMigrationRunner(
                        new PostgreSqlTrippedTopicsMigrationCatalog(tableName, effectiveSchema),
                        configuration, options.MigrationLockTimeout,
                        tracer: sp.GetService<IAmABrighterTracer>(), scope: options.MigrationHistoryScope)));
        });
        return options;
    }

    // Shared by the outbox and inbox registrations; TryAdd makes calling both safe.
    private static void RegisterSharedRoleImpls(IServiceCollection services)
    {
        services.TryAddSingleton<PostgreSqlBoxDetectionHelper>();
        services.TryAddSingleton<PostgreSqlPayloadModeValidator>();
    }
}
