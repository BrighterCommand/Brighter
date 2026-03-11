using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.BoxProvisioning;

/// <summary>
/// Options for configuring box provisioning. Collects backend-specific registrations
/// that will be applied to the service collection.
/// </summary>
public class BoxProvisioningOptions
{
    private readonly List<Action<IServiceCollection>> _registrations = new();

    internal IReadOnlyList<Action<IServiceCollection>> Registrations => _registrations;

    /// <summary>
    /// Timeout for acquiring a database-level migration lock. Default: 30 seconds.
    /// </summary>
    public TimeSpan MigrationLockTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Add a registration action that will be applied to the service collection.
    /// </summary>
    /// <param name="registration">The registration action.</param>
    public void Add(Action<IServiceCollection> registration)
        => _registrations.Add(registration);
}
