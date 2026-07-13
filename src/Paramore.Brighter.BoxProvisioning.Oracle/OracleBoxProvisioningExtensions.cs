// The MIT License (MIT)
// Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
//  The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.BoxProvisioning.Oracle;

/// <summary>
/// Extension methods for registering Oracle box provisioners with <see cref="BoxProvisioningOptions"/>.
/// </summary>
public static class OracleBoxProvisioningExtensions
{
    /// <summary>
    /// Register Oracle outbox provisioning with an explicit configuration.
    /// </summary>
    public static BoxProvisioningOptions AddOracleOutbox(
        this BoxProvisioningOptions options,
        IAmARelationalDatabaseConfiguration configuration)
    {
        options.Add(services =>
        {
            RegisterSharedRoleImpls(services);
            services.TryAddSingleton<OracleOutboxMigrationCatalog>();
            services.AddSingleton<IAmABoxProvisioner>(sp =>
            {
                var catalog = sp.GetRequiredService<OracleOutboxMigrationCatalog>();
                var runner = new OracleBoxMigrationRunner(catalog, configuration, options.MigrationLockTimeout,
                    tracer: sp.GetService<IAmABrighterTracer>(), scope: options.MigrationHistoryScope);
                return new OracleOutboxProvisioner(
                    sp.GetRequiredService<OracleBoxDetectionHelper>(),
                    catalog,
                    sp.GetRequiredService<OraclePayloadModeValidator>(),
                    configuration,
                    runner);
            });
        });
        return options;
    }

    /// <summary>
    /// Register Oracle outbox provisioning with a connection name resolved from IConfiguration at runtime.
    /// </summary>
    public static BoxProvisioningOptions AddOracleOutbox(
        this BoxProvisioningOptions options,
        string connectionName,
        string? outboxTableName = null,
        string? schemaName = null,
        bool binaryMessagePayload = false)
    {
        options.Add(services =>
        {
            RegisterSharedRoleImpls(services);
            services.TryAddSingleton<OracleOutboxMigrationCatalog>();
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
                var catalog = sp.GetRequiredService<OracleOutboxMigrationCatalog>();
                var runner = new OracleBoxMigrationRunner(catalog, dbConfig, options.MigrationLockTimeout,
                    tracer: sp.GetService<IAmABrighterTracer>(), scope: options.MigrationHistoryScope);
                return new OracleOutboxProvisioner(
                    sp.GetRequiredService<OracleBoxDetectionHelper>(),
                    catalog,
                    sp.GetRequiredService<OraclePayloadModeValidator>(),
                    dbConfig,
                    runner);
            });
        });
        return options;
    }

    /// <summary>
    /// Register Oracle inbox provisioning with an explicit configuration.
    /// </summary>
    public static BoxProvisioningOptions AddOracleInbox(
        this BoxProvisioningOptions options,
        IAmARelationalDatabaseConfiguration configuration)
    {
        options.Add(services =>
        {
            RegisterSharedRoleImpls(services);
            services.TryAddSingleton<OracleInboxMigrationCatalog>();
            services.AddSingleton<IAmABoxProvisioner>(sp =>
            {
                var catalog = sp.GetRequiredService<OracleInboxMigrationCatalog>();
                var runner = new OracleBoxMigrationRunner(catalog, configuration, options.MigrationLockTimeout,
                    tracer: sp.GetService<IAmABrighterTracer>(), scope: options.MigrationHistoryScope);
                return new OracleInboxProvisioner(
                    sp.GetRequiredService<OracleBoxDetectionHelper>(),
                    catalog,
                    sp.GetRequiredService<OraclePayloadModeValidator>(),
                    configuration,
                    runner);
            });
        });
        return options;
    }

    /// <summary>
    /// Register Oracle inbox provisioning with a connection name resolved from IConfiguration at runtime.
    /// </summary>
    public static BoxProvisioningOptions AddOracleInbox(
        this BoxProvisioningOptions options,
        string connectionName,
        string? inboxTableName = null,
        string? schemaName = null,
        bool binaryMessagePayload = false)
    {
        options.Add(services =>
        {
            RegisterSharedRoleImpls(services);
            services.TryAddSingleton<OracleInboxMigrationCatalog>();
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
                var catalog = sp.GetRequiredService<OracleInboxMigrationCatalog>();
                var runner = new OracleBoxMigrationRunner(catalog, dbConfig, options.MigrationLockTimeout,
                    tracer: sp.GetService<IAmABrighterTracer>(), scope: options.MigrationHistoryScope);
                return new OracleInboxProvisioner(
                    sp.GetRequiredService<OracleBoxDetectionHelper>(),
                    catalog,
                    sp.GetRequiredService<OraclePayloadModeValidator>(),
                    dbConfig,
                    runner);
            });
        });
        return options;
    }

    private static void RegisterSharedRoleImpls(IServiceCollection services)
    {
        services.TryAddSingleton<OracleBoxDetectionHelper>();
        services.TryAddSingleton<OraclePayloadModeValidator>();
    }
}
