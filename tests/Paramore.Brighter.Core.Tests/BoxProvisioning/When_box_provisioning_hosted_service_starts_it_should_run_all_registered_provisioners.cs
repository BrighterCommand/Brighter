using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.Core.Tests.BoxProvisioning.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.BoxProvisioning;

public class When_box_provisioning_hosted_service_starts_it_should_run_all_registered_provisioners
{
    [Fact]
    public async Task Should_run_all_registered_provisioners()
    {
        //Arrange
        var outboxProvisioner = new SpyBoxProvisioner(BoxType.Outbox);
        var inboxProvisioner = new SpyBoxProvisioner(BoxType.Inbox);
        var provisioners = new List<IAmABoxProvisioner> { outboxProvisioner, inboxProvisioner };

        var hostedService = new BoxProvisioningHostedService(
            provisioners,
            NullLogger<BoxProvisioningHostedService>.Instance
        );

        //Act
        await hostedService.StartAsync(CancellationToken.None);

        //Assert
        Assert.True(outboxProvisioner.WasProvisioned, "Outbox provisioner should have been called");
        Assert.True(inboxProvisioner.WasProvisioned, "Inbox provisioner should have been called");
    }
}
