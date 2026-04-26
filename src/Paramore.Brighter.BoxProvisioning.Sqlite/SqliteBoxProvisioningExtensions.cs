using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.BoxProvisioning.Sqlite;

/// <summary>
/// Extension methods for registering SQLite box provisioners with <see cref="BoxProvisioningOptions"/>.
/// </summary>
public static class SqliteBoxProvisioningExtensions
{
    /// <summary>
    /// Register SQLite outbox provisioning with an explicit configuration.
    /// </summary>
    public static BoxProvisioningOptions AddSqliteOutbox(
        this BoxProvisioningOptions options,
        IAmARelationalDatabaseConfiguration configuration)
    {
        options.Add(services =>
        {
            var runner = new SqliteBoxMigrationRunner(configuration);
            services.AddSingleton<IAmABoxProvisioner>(
                new SqliteOutboxProvisioner(configuration, runner));
        });
        return options;
    }

    /// <summary>
    /// Register SQLite outbox provisioning with a connection name resolved from IConfiguration at runtime.
    /// </summary>
    public static BoxProvisioningOptions AddSqliteOutbox(
        this BoxProvisioningOptions options,
        string connectionName,
        string? outboxTableName = null,
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
                    binaryMessagePayload: binaryMessagePayload);
                var runner = new SqliteBoxMigrationRunner(dbConfig);
                return new SqliteOutboxProvisioner(dbConfig, runner);
            });
        });
        return options;
    }

    /// <summary>
    /// Register SQLite inbox provisioning with an explicit configuration.
    /// </summary>
    public static BoxProvisioningOptions AddSqliteInbox(
        this BoxProvisioningOptions options,
        IAmARelationalDatabaseConfiguration configuration)
    {
        options.Add(services =>
        {
            var runner = new SqliteBoxMigrationRunner(configuration);
            services.AddSingleton<IAmABoxProvisioner>(
                new SqliteInboxProvisioner(configuration, runner));
        });
        return options;
    }

    /// <summary>
    /// Register SQLite inbox provisioning with a connection name resolved from IConfiguration at runtime.
    /// </summary>
    public static BoxProvisioningOptions AddSqliteInbox(
        this BoxProvisioningOptions options,
        string connectionName,
        string? inboxTableName = null,
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
                    binaryMessagePayload: binaryMessagePayload);
                var runner = new SqliteBoxMigrationRunner(dbConfig);
                return new SqliteInboxProvisioner(dbConfig, runner);
            });
        });
        return options;
    }
}
