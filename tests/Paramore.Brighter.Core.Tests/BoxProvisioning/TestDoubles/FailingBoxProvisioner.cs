using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.BoxProvisioning;

namespace Paramore.Brighter.Core.Tests.BoxProvisioning.TestDoubles;

internal class FailingBoxProvisioner : IAmABoxProvisioner
{
    private readonly Exception _exception;

    public BoxType BoxType { get; }
    public BoxTableName BoxTableName { get; } = "stub_box";

    public FailingBoxProvisioner(BoxType boxType, Exception exception)
    {
        BoxType = boxType;
        _exception = exception;
    }

    public Task ProvisionAsync(CancellationToken cancellationToken = default)
    {
        throw _exception;
    }
}
