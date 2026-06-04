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
    /// <para>
    /// In a rolling deploy with N replicas, contended migrations serialize behind the lock — worst
    /// case <c>StartAsync</c> blocks for up to <c>MigrationLockTimeout × (N − 1)</c> per replica.
    /// In Kubernetes this can collide with readiness probes: if
    /// <c>initialDelaySeconds + (failureThreshold × periodSeconds)</c> is shorter than that
    /// window, the pod is killed before migrations complete and the deployment churns. Either size
    /// the probe's tolerance to cover the worst-case window, or shorten this timeout (at the cost
    /// of more retries on contention).
    /// </para>
    /// <para>
    /// Per-replica, <see cref="BoxProvisioningHostedService"/> provisions each backend phase
    /// sequentially (one outbox/inbox table at a time per registered backend). For a deployment
    /// with T provisioned tables on a single replica, the cumulative wall-clock window in the
    /// contended worst case is <c>MigrationLockTimeout × T</c>. Multi-tenant deployments with
    /// many tables should size their readiness-probe tolerance accordingly. Parallelising the
    /// per-phase loop is tracked at <see href="https://github.com/BrighterCommand/Brighter/issues/4140"/>.
    /// </para>
    /// <para>
    /// MySQL/SQLite-specific caveat: MySQL's <c>GET_LOCK</c> takes a timeout argument as an
    /// INTEGER number of seconds, and the SQLite runner uses the same whole-second granularity.
    /// <see cref="TimeSpan"/> values smaller than 1 second are rounded UP to 1 second on both
    /// backends before being passed to the underlying lock primitive; sub-second precision is
    /// therefore unavailable on MySQL and SQLite — the lock will block for at least 1 second
    /// on contention even if a shorter timeout is configured, and <see cref="TimeSpan.Zero"/>
    /// is NOT fail-fast. This is necessary divergence from MSSQL (<c>sp_getapplock</c> accepts
    /// milliseconds and supports fail-fast 0-ms) and PostgreSQL (<c>pg_try_advisory_lock</c>
    /// is non-blocking and the runner loops with a monotonic deadline). Per PR #4039 reviewer
    /// items M2-6 and F2-6.
    /// </para>
    /// </remarks>
    public TimeSpan MigrationLockTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Controls where the box migration-history table is physically placed. Default:
    /// <see cref="MigrationHistoryScope.Global"/> — identical to behaviour prior to this feature.
    /// </summary>
    /// <remarks>
    /// <see cref="MigrationHistoryScope.PerSchema"/> places history in the configured
    /// <see cref="IAmARelationalDatabaseConfiguration.SchemaName"/> on MSSQL and PostgreSQL; it is
    /// a no-op on MySQL, SQLite and Spanner (no distinct schema concept), where history stays in
    /// the default location. Selecting <see cref="MigrationHistoryScope.PerSchema"/> with a null
    /// <see cref="IAmARelationalDatabaseConfiguration.SchemaName"/> on a placement backend throws
    /// <see cref="ConfigurationException"/>. See <see cref="MigrationHistoryScope"/> for full
    /// flip semantics (auto-seed on a <see cref="MigrationHistoryScope.Global"/>→
    /// <see cref="MigrationHistoryScope.PerSchema"/> upgrade, read-access requirement on the
    /// legacy default-schema history table, reverse flip and legacy-row cleanup out of scope).
    /// </remarks>
    public MigrationHistoryScope MigrationHistoryScope { get; set; } = MigrationHistoryScope.Global;

    /// <summary>
    /// Add a registration action that will be applied to the service collection.
    /// </summary>
    /// <param name="registration">The registration action.</param>
    public void Add(Action<IServiceCollection> registration) => _registrations.Add(registration);
}
