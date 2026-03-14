using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.BoxProvisioning.MySql;

/// <summary>
/// Extension methods for registering MySQL box provisioners.
/// </summary>
public static class MySqlBoxProvisioningExtensions
{
    public static BoxProvisioningOptions AddMySqlOutbox(
        this BoxProvisioningOptions options,
        IAmARelationalDatabaseConfiguration configuration)
    {
        var lockTimeout = options.MigrationLockTimeout;
        options.Add(services =>
        {
            var runner = new MySqlBoxMigrationRunner(configuration, lockTimeout);
            services.AddSingleton<IAmABoxProvisioner>(
                new MySqlOutboxProvisioner(configuration, runner));
        });
        return options;
    }

    public static BoxProvisioningOptions AddMySqlOutbox(
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
                var runner = new MySqlBoxMigrationRunner(dbConfig, lockTimeout);
                return new MySqlOutboxProvisioner(dbConfig, runner);
            });
        });
        return options;
    }

    public static BoxProvisioningOptions AddMySqlInbox(
        this BoxProvisioningOptions options,
        IAmARelationalDatabaseConfiguration configuration)
    {
        var lockTimeout = options.MigrationLockTimeout;
        options.Add(services =>
        {
            var runner = new MySqlBoxMigrationRunner(configuration, lockTimeout);
            services.AddSingleton<IAmABoxProvisioner>(
                new MySqlInboxProvisioner(configuration, runner));
        });
        return options;
    }

    public static BoxProvisioningOptions AddMySqlInbox(
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
                var runner = new MySqlBoxMigrationRunner(dbConfig, lockTimeout);
                return new MySqlInboxProvisioner(dbConfig, runner);
            });
        });
        return options;
    }
}
