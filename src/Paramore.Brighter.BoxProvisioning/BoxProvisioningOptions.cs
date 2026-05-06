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
    /// <remarks>
    /// In a rolling deploy with N replicas, contended migrations serialize behind the lock — worst
    /// case <c>StartAsync</c> blocks for up to <c>MigrationLockTimeout × (N − 1)</c> per replica.
    /// In Kubernetes this can collide with readiness probes: if
    /// <c>initialDelaySeconds + (failureThreshold × periodSeconds)</c> is shorter than that
    /// window, the pod is killed before migrations complete and the deployment churns. Either size
    /// the probe's tolerance to cover the worst-case window, or shorten this timeout (at the cost
    /// of more retries on contention).
    /// </remarks>
    public TimeSpan MigrationLockTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Add a registration action that will be applied to the service collection.
    /// </summary>
    /// <param name="registration">The registration action.</param>
    public void Add(Action<IServiceCollection> registration)
        => _registrations.Add(registration);
}
