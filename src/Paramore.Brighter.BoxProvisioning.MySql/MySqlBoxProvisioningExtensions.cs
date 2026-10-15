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
        options.Add(services =>
        {
            RegisterSharedRoleImpls(services);
            services.TryAddSingleton<MySqlOutboxMigrationCatalog>();
            services.AddSingleton<IAmABoxProvisioner>(sp =>
            {
                var catalog = sp.GetRequiredService<MySqlOutboxMigrationCatalog>();
                var runner = new MySqlBoxMigrationRunner(catalog, configuration, options.MigrationLockTimeout,
                    tracer: sp.GetService<IAmABrighterTracer>(), scope: options.MigrationHistoryScope);
                return new MySqlOutboxProvisioner(
                    sp.GetRequiredService<MySqlBoxDetectionHelper>(),
                    catalog,
                    sp.GetRequiredService<MySqlPayloadModeValidator>(),
                    configuration,
                    runner);
            });
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
            RegisterSharedRoleImpls(services);
            services.TryAddSingleton<MySqlOutboxMigrationCatalog>();
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
                var catalog = sp.GetRequiredService<MySqlOutboxMigrationCatalog>();
                var runner = new MySqlBoxMigrationRunner(catalog, dbConfig, options.MigrationLockTimeout,
                    tracer: sp.GetService<IAmABrighterTracer>(), scope: options.MigrationHistoryScope);
                return new MySqlOutboxProvisioner(
                    sp.GetRequiredService<MySqlBoxDetectionHelper>(),
                    catalog,
                    sp.GetRequiredService<MySqlPayloadModeValidator>(),
                    dbConfig,
                    runner);
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
            RegisterSharedRoleImpls(services);
            services.TryAddSingleton<MySqlInboxMigrationCatalog>();
            services.AddSingleton<IAmABoxProvisioner>(sp =>
            {
                var catalog = sp.GetRequiredService<MySqlInboxMigrationCatalog>();
                var runner = new MySqlBoxMigrationRunner(catalog, configuration, options.MigrationLockTimeout,
                    tracer: sp.GetService<IAmABrighterTracer>(), scope: options.MigrationHistoryScope);
                return new MySqlInboxProvisioner(
                    sp.GetRequiredService<MySqlBoxDetectionHelper>(),
                    catalog,
                    sp.GetRequiredService<MySqlPayloadModeValidator>(),
                    configuration,
                    runner);
            });
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
            RegisterSharedRoleImpls(services);
            services.TryAddSingleton<MySqlInboxMigrationCatalog>();
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
                var catalog = sp.GetRequiredService<MySqlInboxMigrationCatalog>();
                var runner = new MySqlBoxMigrationRunner(catalog, dbConfig, options.MigrationLockTimeout,
                    tracer: sp.GetService<IAmABrighterTracer>(), scope: options.MigrationHistoryScope);
                return new MySqlInboxProvisioner(
                    sp.GetRequiredService<MySqlBoxDetectionHelper>(),
                    catalog,
                    sp.GetRequiredService<MySqlPayloadModeValidator>(),
                    dbConfig,
                    runner);
            });
        });
        return options;
    }

    // Shared by the outbox and inbox registrations; TryAdd makes calling both safe.
    private static void RegisterSharedRoleImpls(IServiceCollection services)
    {
        services.TryAddSingleton<MySqlBoxDetectionHelper>();
        services.TryAddSingleton<MySqlPayloadModeValidator>();
    }
}
