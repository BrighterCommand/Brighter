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
        var ordered = _provisioners.OrderBy(p => p.BoxType == BoxType.Outbox ? 0 : 1);

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
}
