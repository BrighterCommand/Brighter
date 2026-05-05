using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.BoxProvisioning.MySql;

/// <summary>
/// Extension methods for registering MySQL box provisioners with <see cref="BoxProvisioningOptions"/>.
/// </summary>
public static class MySqlBoxProvisioningExtensions
{
    /// <summary>
    /// Register MySQL outbox provisioning with an explicit configuration.
    /// </summary>
    public static BoxProvisioningOptions AddMySqlOutbox(
        this BoxProvisioningOptions options,
        IAmARelationalDatabaseConfiguration configuration)
    {
        // MigrationLockTimeout is read inside the registration lambda so it stays late-bound:
        // the configure delegate may set the timeout AFTER calling Add*Outbox/Add*Inbox and
        // the change still takes effect (registrations only run once the configure delegate
        // has fully completed inside UseBoxProvisioning).
        options.Add(services =>
        {
            var runner = new MySqlBoxMigrationRunner(configuration, options.MigrationLockTimeout);
            services.AddSingleton<IAmABoxProvisioner>(
                new MySqlOutboxProvisioner(configuration, runner));
        });
        return options;
    }

    /// <summary>
    /// Register MySQL outbox provisioning with a connection name resolved from IConfiguration at runtime.
    /// </summary>
    public static BoxProvisioningOptions AddMySqlOutbox(
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
                var runner = new MySqlBoxMigrationRunner(dbConfig, options.MigrationLockTimeout);
                return new MySqlOutboxProvisioner(dbConfig, runner);
            });
        });
        return options;
    }

    /// <summary>
    /// Register MySQL inbox provisioning with an explicit configuration.
    /// </summary>
    public static BoxProvisioningOptions AddMySqlInbox(
        this BoxProvisioningOptions options,
        IAmARelationalDatabaseConfiguration configuration)
    {
        options.Add(services =>
        {
            var runner = new MySqlBoxMigrationRunner(configuration, options.MigrationLockTimeout);
            services.AddSingleton<IAmABoxProvisioner>(
                new MySqlInboxProvisioner(configuration, runner));
        });
        return options;
    }

    /// <summary>
    /// Register MySQL inbox provisioning with a connection name resolved from IConfiguration at runtime.
    /// </summary>
    public static BoxProvisioningOptions AddMySqlInbox(
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
                var runner = new MySqlBoxMigrationRunner(dbConfig, options.MigrationLockTimeout);
                return new MySqlInboxProvisioner(dbConfig, runner);
            });
        });
        return options;
    }
}
