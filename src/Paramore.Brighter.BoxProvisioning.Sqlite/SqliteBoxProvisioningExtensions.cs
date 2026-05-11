using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Paramore.Brighter.BoxProvisioning.Sqlite;

/// <summary>
/// Extension methods for registering SQLite box provisioners with <see cref="BoxProvisioningOptions"/>.
/// </summary>
public static class SqliteBoxProvisioningExtensions
{
    /// <summary>
    /// Register SQLite outbox provisioning with an explicit configuration.
    /// </summary>
    /// <param name="options">The provisioning options.</param>
    /// <param name="configuration">The relational database configuration.</param>
    /// <param name="enableWalMode">
    /// When <see langword="true"/> (default) the runner issues
    /// <c>PRAGMA journal_mode=WAL</c> on each migration call. Set to <see langword="false"/>
    /// if the host application manages SQLite journal mode itself — the pragma is
    /// database-file-wide and overrides any DELETE/TRUNCATE choice the host already made.
    /// </param>
    public static BoxProvisioningOptions AddSqliteOutbox(
        this BoxProvisioningOptions options,
        IAmARelationalDatabaseConfiguration configuration,
        bool enableWalMode = true)
    {
        options.Add(services =>
        {
            RegisterSharedRoleImpls(services);
            services.TryAddSingleton<SqliteOutboxMigrationCatalog>();
            services.AddSingleton<IAmABoxProvisioner>(sp =>
            {
                var runner = new SqliteBoxMigrationRunner(
                    configuration, options.MigrationLockTimeout, enableWalMode);
                return new SqliteOutboxProvisioner(
                    sp.GetRequiredService<SqliteBoxDetectionHelper>(),
                    sp.GetRequiredService<SqliteOutboxMigrationCatalog>(),
                    sp.GetRequiredService<SqlitePayloadModeValidator>(),
                    configuration,
                    runner);
            });
        });
        return options;
    }

    /// <summary>
    /// Register SQLite outbox provisioning with a connection name resolved from IConfiguration at runtime.
    /// </summary>
    /// <param name="enableWalMode">
    /// When <see langword="true"/> (default) the runner issues
    /// <c>PRAGMA journal_mode=WAL</c> on each migration call. Set to <see langword="false"/>
    /// if the host application manages SQLite journal mode itself.
    /// </param>
    public static BoxProvisioningOptions AddSqliteOutbox(
        this BoxProvisioningOptions options,
        string connectionName,
        string? outboxTableName = null,
        bool binaryMessagePayload = false,
        bool enableWalMode = true)
    {
        options.Add(services =>
        {
            RegisterSharedRoleImpls(services);
            services.TryAddSingleton<SqliteOutboxMigrationCatalog>();
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
                var runner = new SqliteBoxMigrationRunner(
                    dbConfig, options.MigrationLockTimeout, enableWalMode);
                return new SqliteOutboxProvisioner(
                    sp.GetRequiredService<SqliteBoxDetectionHelper>(),
                    sp.GetRequiredService<SqliteOutboxMigrationCatalog>(),
                    sp.GetRequiredService<SqlitePayloadModeValidator>(),
                    dbConfig,
                    runner);
            });
        });
        return options;
    }

    /// <summary>
    /// Register SQLite inbox provisioning with an explicit configuration.
    /// </summary>
    /// <param name="enableWalMode">
    /// When <see langword="true"/> (default) the runner issues
    /// <c>PRAGMA journal_mode=WAL</c> on each migration call. Set to <see langword="false"/>
    /// if the host application manages SQLite journal mode itself.
    /// </param>
    public static BoxProvisioningOptions AddSqliteInbox(
        this BoxProvisioningOptions options,
        IAmARelationalDatabaseConfiguration configuration,
        bool enableWalMode = true)
    {
        options.Add(services =>
        {
            RegisterSharedRoleImpls(services);
            services.TryAddSingleton<SqliteInboxMigrationCatalog>();
            services.AddSingleton<IAmABoxProvisioner>(sp =>
            {
                var runner = new SqliteBoxMigrationRunner(
                    configuration, options.MigrationLockTimeout, enableWalMode);
                return new SqliteInboxProvisioner(
                    sp.GetRequiredService<SqliteBoxDetectionHelper>(),
                    sp.GetRequiredService<SqliteInboxMigrationCatalog>(),
                    sp.GetRequiredService<SqlitePayloadModeValidator>(),
                    configuration,
                    runner);
            });
        });
        return options;
    }

    /// <summary>
    /// Register SQLite inbox provisioning with a connection name resolved from IConfiguration at runtime.
    /// </summary>
    /// <param name="enableWalMode">
    /// When <see langword="true"/> (default) the runner issues
    /// <c>PRAGMA journal_mode=WAL</c> on each migration call. Set to <see langword="false"/>
    /// if the host application manages SQLite journal mode itself.
    /// </param>
    public static BoxProvisioningOptions AddSqliteInbox(
        this BoxProvisioningOptions options,
        string connectionName,
        string? inboxTableName = null,
        bool binaryMessagePayload = false,
        bool enableWalMode = true)
    {
        options.Add(services =>
        {
            RegisterSharedRoleImpls(services);
            services.TryAddSingleton<SqliteInboxMigrationCatalog>();
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
                var runner = new SqliteBoxMigrationRunner(
                    dbConfig, options.MigrationLockTimeout, enableWalMode);
                return new SqliteInboxProvisioner(
                    sp.GetRequiredService<SqliteBoxDetectionHelper>(),
                    sp.GetRequiredService<SqliteInboxMigrationCatalog>(),
                    sp.GetRequiredService<SqlitePayloadModeValidator>(),
                    dbConfig,
                    runner);
            });
        });
        return options;
    }

    // Detection helper + payload validator are shared across Outbox and Inbox extensions;
    // TryAddSingleton makes the registration idempotent so calling both AddSqliteOutbox and
    // AddSqliteInbox does not produce duplicate singletons. Catalogue registration is per
    // box-type and lives in the call-site (Outbox catalogue from AddSqliteOutbox; Inbox
    // catalogue from AddSqliteInbox) per ADR 0058 §A.4 Alternatives.
    private static void RegisterSharedRoleImpls(IServiceCollection services)
    {
        services.TryAddSingleton<SqliteBoxDetectionHelper>();
        services.TryAddSingleton<SqlitePayloadModeValidator>();
    }
}
