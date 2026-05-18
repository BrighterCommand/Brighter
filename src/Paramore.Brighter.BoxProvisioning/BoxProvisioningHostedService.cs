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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Paramore.Brighter.BoxProvisioning;

/// <summary>
/// Hosted service that provisions all registered box tables at application startup.
/// Outbox tables are provisioned before inbox tables.
/// </summary>
public class BoxProvisioningHostedService : IHostedService
{
    private readonly IEnumerable<IAmABoxProvisioner> _provisioners;
    private readonly ILogger<BoxProvisioningHostedService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="BoxProvisioningHostedService"/>.
    /// </summary>
    /// <param name="provisioners">The collection of <see cref="IAmABoxProvisioner"/>
    /// registrations resolved from the container. Each provisioner targets a single box table;
    /// applications typically register one per outbox or inbox in use.</param>
    /// <param name="logger">The <see cref="ILogger{TCategoryName}"/> used to report
    /// per-box provisioning progress and configuration failures at startup.</param>
    public BoxProvisioningHostedService(
        IEnumerable<IAmABoxProvisioner> provisioners,
        ILogger<BoxProvisioningHostedService> logger)
    {
        _provisioners = provisioners;
        _logger = logger;
    }

    /// <summary>
    /// Provisions every registered box table in turn — outboxes before inboxes — so that the
    /// host does not begin accepting traffic until all box schemas are at the expected version.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> signalled when the host
    /// is shutting down or the startup deadline elapses; propagated as-is so Kubernetes readiness
    /// probes see a cancellation rather than a configuration error.</param>
    /// <returns>A <see cref="Task"/> that completes once every provisioner has finished
    /// successfully.</returns>
    /// <exception cref="ConfigurationException">Thrown when any provisioner fails for a
    /// non-cancellation reason (for example an unreachable database or invalid migration).
    /// The original failure is preserved as the inner exception.</exception>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // OrderBy is stable, but the upstream IEnumerable<IAmABoxProvisioner> resolution order
        // is not guaranteed across DI containers — Microsoft.Extensions.DependencyInjection
        // preserves registration order today, but third-party containers (Autofac with
        // resolve-by-tag, Lamar, etc.) may not. Within an outbox/inbox group, ordering across
        // multiple provisioners (e.g. one tenant per provisioner) is intentionally
        // implementation-defined: the per-table startup log ("Provisioning Outbox '{BoxTableName}'…")
        // disambiguates which provisioner is running, so a non-deterministic intra-group order
        // is operationally tolerable and a deterministic secondary sort by BoxTableName would
        // entrench an ordering callers cannot influence today.
        var ordered = _provisioners.OrderBy(p => OrderingOrdinal(p.BoxType));

        foreach (var provisioner in ordered)
        {
            _logger.LogInformation("Provisioning {BoxType} '{BoxTableName}'...", provisioner.BoxType, provisioner.BoxTableName);
            try
            {
                await provisioner.ProvisionAsync(cancellationToken);
                _logger.LogInformation("Provisioned {BoxType} '{BoxTableName}' successfully", provisioner.BoxType, provisioner.BoxTableName);
            }
            catch (OperationCanceledException)
            {
                // Propagate cancellation as-is. Wrapping it in ConfigurationException would
                // misreport host-shutdown / startup-deadline timeouts as configuration errors —
                // particularly confusing for k8s readiness-probe diagnostics.
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to provision {BoxType} '{BoxTableName}'. The application cannot start " +
                    "without a valid box table. Check the database connection " +
                    "string and ensure the database is reachable.",
                    provisioner.BoxType, provisioner.BoxTableName);
                throw new ConfigurationException(
                    $"Box provisioning failed for {provisioner.BoxType} '{provisioner.BoxTableName}'. " +
                    $"See inner exception for details.", ex);
            }
        }
    }

    /// <summary>
    /// No-op shutdown hook. Box provisioning has no resources that need releasing on host stop —
    /// migrations either completed during <see cref="StartAsync"/> or the host failed fast.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> signalled when the host
    /// shutdown deadline elapses. Unused.</param>
    /// <returns>A <see cref="Task"/> that has already run to completion.</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // Single source of truth for provisioner ordering. The default arm converts a silent
    // mis-ordering (intermittent, DI-registration-order-dependent) into a loud startup
    // failure that names the file and method to update — so the next contributor adding
    // a BoxType value (Lockbox / DeadLetterBox / etc.) sees exactly where to add the arm.
    private static int OrderingOrdinal(BoxType type) => type switch
    {
        BoxType.Outbox => 0,
        BoxType.Inbox => 1,
        _ => throw new ArgumentOutOfRangeException(
            nameof(type), type,
            $"BoxProvisioningHostedService does not know how to order BoxType.{type} — add it to the switch in OrderingOrdinal")
    };
}
