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

    public BoxProvisioningHostedService(
        IEnumerable<IAmABoxProvisioner> provisioners,
        ILogger<BoxProvisioningHostedService> logger)
    {
        _provisioners = provisioners;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var ordered = _provisioners.OrderBy(p => OrderingOrdinal(p.BoxType));

        foreach (var provisioner in ordered)
        {
            _logger.LogInformation(
                "Provisioning {BoxType} '{BoxTableName}'...",
                provisioner.BoxType, provisioner.BoxTableName);
            try
            {
                await provisioner.ProvisionAsync(cancellationToken);
                _logger.LogInformation(
                    "Provisioned {BoxType} '{BoxTableName}' successfully",
                    provisioner.BoxType, provisioner.BoxTableName);
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
