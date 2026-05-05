using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
            var runner = new PostgreSqlBoxMigrationRunner(configuration, options.MigrationLockTimeout);
            services.AddSingleton<IAmABoxProvisioner>(
                new PostgreSqlOutboxProvisioner(configuration, runner));
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
                var runner = new PostgreSqlBoxMigrationRunner(dbConfig, options.MigrationLockTimeout);
                return new PostgreSqlOutboxProvisioner(dbConfig, runner);
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
            var runner = new PostgreSqlBoxMigrationRunner(configuration, options.MigrationLockTimeout);
            services.AddSingleton<IAmABoxProvisioner>(
                new PostgreSqlInboxProvisioner(configuration, runner));
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
                var runner = new PostgreSqlBoxMigrationRunner(dbConfig, options.MigrationLockTimeout);
                return new PostgreSqlInboxProvisioner(dbConfig, runner);
            });
        });
        return options;
    }
}
