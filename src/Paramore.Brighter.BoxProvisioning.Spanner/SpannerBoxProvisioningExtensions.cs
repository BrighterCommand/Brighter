using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Paramore.Brighter.Observability;

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
            RegisterSharedRoleImpls(services);
            services.AddSingleton<IAmABoxProvisioner>(sp =>
            {
                var detectionHelper = sp.GetRequiredService<SpannerBoxDetectionHelper>();
                var runner = new SpannerBoxMigrationRunner(detectionHelper, configuration,
                    tracer: sp.GetService<IAmABrighterTracer>());
                return new SpannerOutboxProvisioner(
                    detectionHelper,
                    sp.GetRequiredService<SpannerPayloadModeValidator>(),
                    configuration,
                    runner);
            });
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
            RegisterSharedRoleImpls(services);
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
                var detectionHelper = sp.GetRequiredService<SpannerBoxDetectionHelper>();
                var runner = new SpannerBoxMigrationRunner(detectionHelper, dbConfig,
                    tracer: sp.GetService<IAmABrighterTracer>());
                return new SpannerOutboxProvisioner(
                    detectionHelper,
                    sp.GetRequiredService<SpannerPayloadModeValidator>(),
                    dbConfig,
                    runner);
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
            RegisterSharedRoleImpls(services);
            services.AddSingleton<IAmABoxProvisioner>(sp =>
            {
                var detectionHelper = sp.GetRequiredService<SpannerBoxDetectionHelper>();
                var runner = new SpannerBoxMigrationRunner(detectionHelper, configuration,
                    tracer: sp.GetService<IAmABrighterTracer>());
                return new SpannerInboxProvisioner(
                    detectionHelper,
                    sp.GetRequiredService<SpannerPayloadModeValidator>(),
                    configuration,
                    runner);
            });
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
            RegisterSharedRoleImpls(services);
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
                var detectionHelper = sp.GetRequiredService<SpannerBoxDetectionHelper>();
                var runner = new SpannerBoxMigrationRunner(detectionHelper, dbConfig,
                    tracer: sp.GetService<IAmABrighterTracer>());
                return new SpannerInboxProvisioner(
                    detectionHelper,
                    sp.GetRequiredService<SpannerPayloadModeValidator>(),
                    dbConfig,
                    runner);
            });
        });
        return options;
    }

    // Detection helper + payload validator are shared across Outbox and Inbox extensions;
    // TryAddSingleton makes the registration idempotent so calling both AddSpannerOutbox and
    // AddSpannerInbox does not produce duplicate singletons. The detection helper is ALSO
    // consumed by SpannerBoxMigrationRunner (Phase 7.5) — the runner is constructed inside
    // the factory using the same singleton so both the runner and provisioner see one
    // shared instance per service-provider scope. NO catalogue registration per ADR 0057 §6
    // (Spanner is degenerate fresh-only).
    private static void RegisterSharedRoleImpls(IServiceCollection services)
    {
        services.TryAddSingleton<SpannerBoxDetectionHelper>();
        services.TryAddSingleton<SpannerPayloadModeValidator>();
    }
}
