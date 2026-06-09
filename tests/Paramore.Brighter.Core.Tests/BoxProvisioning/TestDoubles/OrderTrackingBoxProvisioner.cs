using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.BoxProvisioning;

namespace Paramore.Brighter.Core.Tests.BoxProvisioning.TestDoubles;

internal class OrderTrackingBoxProvisioner : IAmABoxProvisioner
{
    private readonly List<BoxType> _callOrder;

    public BoxType BoxType { get; }
    public BoxTableName BoxTableName { get; } = "stub_box";

    public OrderTrackingBoxProvisioner(BoxType boxType, List<BoxType> callOrder)
    {
        BoxType = boxType;
        _callOrder = callOrder;
    }

    public Task ProvisionAsync(CancellationToken cancellationToken = default)
    {
        _callOrder.Add(BoxType);
        return Task.CompletedTask;
    }
}
