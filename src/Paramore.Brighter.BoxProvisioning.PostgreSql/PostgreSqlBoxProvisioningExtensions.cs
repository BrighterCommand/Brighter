using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.BoxProvisioning.PostgreSql;

/// <summary>
/// Extension methods for registering PostgreSQL box provisioners.
/// </summary>
public static class PostgreSqlBoxProvisioningExtensions
{
    public static BoxProvisioningOptions AddPostgreSqlOutbox(
        this BoxProvisioningOptions options,
        IAmARelationalDatabaseConfiguration configuration)
    {
        var lockTimeout = options.MigrationLockTimeout;
        options.Add(services =>
        {
            var runner = new PostgreSqlBoxMigrationRunner(configuration, lockTimeout);
            services.AddSingleton<IAmABoxProvisioner>(
                new PostgreSqlOutboxProvisioner(configuration, runner));
        });
        return options;
    }

    public static BoxProvisioningOptions AddPostgreSqlOutbox(
        this BoxProvisioningOptions options,
        string connectionName,
        string? outboxTableName = null,
        string? schemaName = null,
        bool binaryMessagePayload = false)
    {
        var lockTimeout = options.MigrationLockTimeout;
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
                var runner = new PostgreSqlBoxMigrationRunner(dbConfig, lockTimeout);
                return new PostgreSqlOutboxProvisioner(dbConfig, runner);
            });
        });
        return options;
    }

    public static BoxProvisioningOptions AddPostgreSqlInbox(
        this BoxProvisioningOptions options,
        IAmARelationalDatabaseConfiguration configuration)
    {
        var lockTimeout = options.MigrationLockTimeout;
        options.Add(services =>
        {
            var runner = new PostgreSqlBoxMigrationRunner(configuration, lockTimeout);
            services.AddSingleton<IAmABoxProvisioner>(
                new PostgreSqlInboxProvisioner(configuration, runner));
        });
        return options;
    }

    public static BoxProvisioningOptions AddPostgreSqlInbox(
        this BoxProvisioningOptions options,
        string connectionName,
        string? inboxTableName = null,
        string? schemaName = null,
        bool binaryMessagePayload = false)
    {
        var lockTimeout = options.MigrationLockTimeout;
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
                var runner = new PostgreSqlBoxMigrationRunner(dbConfig, lockTimeout);
                return new PostgreSqlInboxProvisioner(dbConfig, runner);
            });
        });
        return options;
    }
}
