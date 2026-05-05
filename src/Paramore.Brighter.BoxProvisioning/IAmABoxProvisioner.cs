using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.BoxProvisioning;

/// <summary>
/// Knows how to provision (create and migrate) a box (inbox or outbox) table.
/// </summary>
public interface IAmABoxProvisioner
{
    /// <summary>The type of box being provisioned.</summary>
    BoxType BoxType { get; }

    /// <summary>
    /// The unqualified name of the box table this provisioner targets. Surfaced by
    /// <see cref="BoxProvisioningHostedService"/> in startup log entries so an operator
    /// reading the log can disambiguate concurrent provisioning steps when multiple
    /// boxes of the same <see cref="BoxType"/> are registered.
    /// </summary>
    string BoxTableName { get; }

    /// <summary>
    /// Provision the box table: create if it doesn't exist, then apply any
    /// outstanding migrations. Idempotent — safe to call on every startup.
    /// </summary>
    Task ProvisionAsync(CancellationToken cancellationToken = default);
}
