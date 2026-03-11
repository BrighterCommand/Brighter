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
    /// Provision the box table: create if it doesn't exist, then apply any
    /// outstanding migrations. Idempotent — safe to call on every startup.
    /// </summary>
    Task ProvisionAsync(CancellationToken cancellationToken = default);
}
