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
using System.Linq;
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
    /// <see cref="BoxProvisioningOptions.MigrationLockTimeout"/> inside the delegate — the
    /// timeout is read late, when the registrations actually run, so the order of statements
    /// inside the delegate does not matter.
    /// <para>
    /// <b>Single-call contract:</b> invoke this method exactly once per builder. The
    /// hosted-service registration is guarded by <see cref="ServiceCollectionDescriptorExtensions.TryAddEnumerable(IServiceCollection, ServiceDescriptor)"/>,
    /// but per-backend <c>Add{Backend}{Box}</c> registrations append plain
    /// <see cref="ServiceCollectionServiceExtensions.AddSingleton{TService}(IServiceCollection, Func{IServiceProvider, TService})"/>
    /// descriptors, so a second invocation would double-register every provisioner and
    /// <see cref="BoxProvisioningHostedService"/> would run each migration twice. Configure
    /// all outboxes and inboxes inside a single delegate; a second call throws
    /// <see cref="ConfigurationException"/>.
    /// </para>
    /// </summary>
    /// <param name="builder">The Brighter builder.</param>
    /// <param name="configure">A delegate to configure box provisioning options.</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="ConfigurationException">Thrown when <c>UseBoxProvisioning</c> has
    /// already been invoked on <paramref name="builder"/>.</exception>
    public static IBrighterBuilder UseBoxProvisioning(
        this IBrighterBuilder builder,
        Action<BoxProvisioningOptions> configure)
    {
        if (builder.Services.Any(d => d.ServiceType == typeof(BoxProvisioningMarker)))
        {
            throw new ConfigurationException(
                "UseBoxProvisioning has already been called on this builder. Configure all "
                + "outboxes and inboxes inside a single UseBoxProvisioning(o => …) delegate; "
                + "a second invocation would double-register every provisioner and the "
                + "hosted service would run each migration twice.");
        }

        // Run configure(options) BEFORE registering the marker. A throwing configure must
        // leave the service collection in the same state as the never-invoked case so the
        // operator's natural retry succeeds; registering the marker first would leak it on a
        // failed configure and trip the duplicate-call guard above with a misleading message.
        var options = new BoxProvisioningOptions();
        configure(options);

        builder.Services.AddSingleton<BoxProvisioningMarker>();

        foreach (var registration in options.Registrations)
        {
            registration(builder.Services);
        }

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, BoxProvisioningHostedService>());
        return builder;
    }

    // Marker singleton used to detect a second UseBoxProvisioning call on the same builder.
    // Internal because callers should never resolve or depend on it directly — its only role
    // is to flip the "already invoked" check on subsequent calls.
    internal sealed class BoxProvisioningMarker { }
}
