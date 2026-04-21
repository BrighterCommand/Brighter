using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.BoxProvisioning.Spanner;

/// <summary>
/// Extension methods for registering Spanner box provisioners with <see cref="BoxProvisioningOptions"/>.
/// </summary>
public static class SpannerBoxProvisioningExtensions
{
    /// <summary>
    /// Register Spanner outbox provisioning with an explicit configuration.
    /// </summary>
    public static BoxProvisioningOptions AddSpannerOutbox(
        this BoxProvisioningOptions options,
        IAmARelationalDatabaseConfiguration configuration)
    {
        options.Add(services =>
        {
            var runner = new SpannerBoxMigrationRunner(configuration);
            services.AddSingleton<IAmABoxProvisioner>(
                new SpannerOutboxProvisioner(configuration, runner));
        });
        return options;
    }

    /// <summary>
    /// Register Spanner outbox provisioning with a connection name resolved from IConfiguration at runtime.
    /// </summary>
    public static BoxProvisioningOptions AddSpannerOutbox(
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
                var runner = new SpannerBoxMigrationRunner(dbConfig);
                return new SpannerOutboxProvisioner(dbConfig, runner);
            });
        });
        return options;
    }

    /// <summary>
    /// Register Spanner inbox provisioning with an explicit configuration.
    /// </summary>
    public static BoxProvisioningOptions AddSpannerInbox(
        this BoxProvisioningOptions options,
        IAmARelationalDatabaseConfiguration configuration)
    {
        options.Add(services =>
        {
            var runner = new SpannerBoxMigrationRunner(configuration);
            services.AddSingleton<IAmABoxProvisioner>(
                new SpannerInboxProvisioner(configuration, runner));
        });
        return options;
    }

    /// <summary>
    /// Register Spanner inbox provisioning with a connection name resolved from IConfiguration at runtime.
    /// </summary>
    public static BoxProvisioningOptions AddSpannerInbox(
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
                var runner = new SpannerBoxMigrationRunner(dbConfig);
                return new SpannerInboxProvisioner(dbConfig, runner);
            });
        });
        return options;
    }
}
