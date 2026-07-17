#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Paramore.Brighter.Observability;

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
                var catalog = sp.GetRequiredService<SqliteOutboxMigrationCatalog>();
                var runner = new SqliteBoxMigrationRunner(
                    catalog, configuration, options.MigrationLockTimeout, enableWalMode,
                    tracer: sp.GetService<IAmABrighterTracer>(), scope: options.MigrationHistoryScope);
                return new SqliteOutboxProvisioner(
                    sp.GetRequiredService<SqliteBoxDetectionHelper>(),
                    catalog,
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
                var catalog = sp.GetRequiredService<SqliteOutboxMigrationCatalog>();
                var runner = new SqliteBoxMigrationRunner(
                    catalog, dbConfig, options.MigrationLockTimeout, enableWalMode,
                    tracer: sp.GetService<IAmABrighterTracer>(), scope: options.MigrationHistoryScope);
                return new SqliteOutboxProvisioner(
                    sp.GetRequiredService<SqliteBoxDetectionHelper>(),
                    catalog,
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
                var catalog = sp.GetRequiredService<SqliteInboxMigrationCatalog>();
                var runner = new SqliteBoxMigrationRunner(
                    catalog, configuration, options.MigrationLockTimeout, enableWalMode,
                    tracer: sp.GetService<IAmABrighterTracer>(), scope: options.MigrationHistoryScope);
                return new SqliteInboxProvisioner(
                    sp.GetRequiredService<SqliteBoxDetectionHelper>(),
                    catalog,
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
                var catalog = sp.GetRequiredService<SqliteInboxMigrationCatalog>();
                var runner = new SqliteBoxMigrationRunner(
                    catalog, dbConfig, options.MigrationLockTimeout, enableWalMode,
                    tracer: sp.GetService<IAmABrighterTracer>(), scope: options.MigrationHistoryScope);
                return new SqliteInboxProvisioner(
                    sp.GetRequiredService<SqliteBoxDetectionHelper>(),
                    catalog,
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
