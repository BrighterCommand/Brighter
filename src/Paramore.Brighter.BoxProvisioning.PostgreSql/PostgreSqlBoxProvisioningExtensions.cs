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
        // MigrationLockTimeout is read inside the registration lambda so it stays late-bound:
        // the configure delegate may set the timeout AFTER calling Add*Outbox/Add*Inbox and
        // the change still takes effect (registrations only run once the configure delegate
        // has fully completed inside UseBoxProvisioning).
        options.Add(services =>
        {
            RegisterSharedRoleImpls(services);
            services.TryAddSingleton<PostgreSqlOutboxMigrationCatalog>();
            services.AddSingleton<IAmABoxProvisioner>(sp =>
            {
                var runner = new PostgreSqlBoxMigrationRunner(configuration, options.MigrationLockTimeout,
                    tracer: sp.GetService<IAmABrighterTracer>());
                return new PostgreSqlOutboxProvisioner(
                    sp.GetRequiredService<PostgreSqlBoxDetectionHelper>(),
                    sp.GetRequiredService<PostgreSqlOutboxMigrationCatalog>(),
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
                var runner = new PostgreSqlBoxMigrationRunner(dbConfig, options.MigrationLockTimeout,
                    tracer: sp.GetService<IAmABrighterTracer>());
                return new PostgreSqlOutboxProvisioner(
                    sp.GetRequiredService<PostgreSqlBoxDetectionHelper>(),
                    sp.GetRequiredService<PostgreSqlOutboxMigrationCatalog>(),
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
                var runner = new PostgreSqlBoxMigrationRunner(configuration, options.MigrationLockTimeout,
                    tracer: sp.GetService<IAmABrighterTracer>());
                return new PostgreSqlInboxProvisioner(
                    sp.GetRequiredService<PostgreSqlBoxDetectionHelper>(),
                    sp.GetRequiredService<PostgreSqlInboxMigrationCatalog>(),
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
                var runner = new PostgreSqlBoxMigrationRunner(dbConfig, options.MigrationLockTimeout,
                    tracer: sp.GetService<IAmABrighterTracer>());
                return new PostgreSqlInboxProvisioner(
                    sp.GetRequiredService<PostgreSqlBoxDetectionHelper>(),
                    sp.GetRequiredService<PostgreSqlInboxMigrationCatalog>(),
                    sp.GetRequiredService<PostgreSqlPayloadModeValidator>(),
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
        services.TryAddSingleton<PostgreSqlBoxDetectionHelper>();
        services.TryAddSingleton<PostgreSqlPayloadModeValidator>();
    }
}
