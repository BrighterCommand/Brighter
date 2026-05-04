using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.BoxProvisioning;

/// <summary>
/// Extension methods for registering box provisioning with <see cref="IBrighterBuilder"/>.
/// </summary>
public static class BrighterBuilderBoxProvisioningExtensions
{
    /// <summary>
    /// Register box provisioning. The configure delegate should call backend-specific
    /// extension methods (e.g. <c>AddMsSqlOutbox</c>) to register provisioners. To override the
    /// default migration lock timeout (30 seconds), assign
    /// <see cref="BoxProvisioningOptions.MigrationLockTimeout"/> inside the delegate BEFORE
    /// calling any backend <c>AddXxxOutbox</c>/<c>AddXxxInbox</c> method — backend extensions
    /// capture the timeout at registration time.
    /// </summary>
    /// <param name="builder">The Brighter builder.</param>
    /// <param name="configure">A delegate to configure box provisioning options.</param>
    /// <returns>The builder for chaining.</returns>
    public static IBrighterBuilder UseBoxProvisioning(
        this IBrighterBuilder builder,
        Action<BoxProvisioningOptions> configure)
    {
        var options = new BoxProvisioningOptions();
        configure(options);

        foreach (var registration in options.Registrations)
        {
            registration(builder.Services);
        }

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, BoxProvisioningHostedService>());
        return builder;
    }
}
