using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.BoxProvisioning;

namespace Paramore.Brighter.Core.Tests.BoxProvisioning.TestDoubles;

internal class SpyBoxProvisioner : IAmABoxProvisioner
{
    public BoxType BoxType { get; }
    public string BoxTableName { get; } = "stub_box";
    public bool WasProvisioned { get; private set; }

    public SpyBoxProvisioner(BoxType boxType)
    {
        BoxType = boxType;
    }

    public Task ProvisionAsync(CancellationToken cancellationToken = default)
    {
        WasProvisioned = true;
        return Task.CompletedTask;
    }
}
