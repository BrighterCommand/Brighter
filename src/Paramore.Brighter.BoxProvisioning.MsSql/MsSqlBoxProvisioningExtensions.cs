using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
            var runner = new MsSqlBoxMigrationRunner(configuration, options.MigrationLockTimeout);
            services.AddSingleton<IAmABoxProvisioner>(
                new MsSqlOutboxProvisioner(configuration, runner));
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
                var runner = new MsSqlBoxMigrationRunner(dbConfig, options.MigrationLockTimeout);
                return new MsSqlOutboxProvisioner(dbConfig, runner);
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
            var runner = new MsSqlBoxMigrationRunner(configuration, options.MigrationLockTimeout);
            services.AddSingleton<IAmABoxProvisioner>(
                new MsSqlInboxProvisioner(configuration, runner));
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
                var runner = new MsSqlBoxMigrationRunner(dbConfig, options.MigrationLockTimeout);
                return new MsSqlInboxProvisioner(dbConfig, runner);
            });
        });
        return options;
    }
}
