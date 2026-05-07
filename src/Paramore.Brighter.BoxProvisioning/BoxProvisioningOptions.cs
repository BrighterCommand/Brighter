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
