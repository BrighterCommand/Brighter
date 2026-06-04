using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.Core.Tests.BoxProvisioning.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.BoxProvisioning;

public class When_box_provisioning_hosted_service_starts_it_should_provision_outbox_before_inbox
{
    [Fact]
    public async Task Should_provision_outbox_before_inbox()
    {
        //Arrange
        var callOrder = new List<BoxType>();
        var inboxProvisioner = new OrderTrackingBoxProvisioner(BoxType.Inbox, callOrder);
        var outboxProvisioner = new OrderTrackingBoxProvisioner(BoxType.Outbox, callOrder);

        // Deliberately register inbox first to verify ordering
        var provisioners = new List<IAmABoxProvisioner> { inboxProvisioner, outboxProvisioner };

        var hostedService = new BoxProvisioningHostedService(
            provisioners,
            NullLogger<BoxProvisioningHostedService>.Instance
        );

        //Act
        await hostedService.StartAsync(CancellationToken.None);

        //Assert
        Assert.Equal(2, callOrder.Count);
        Assert.Equal(BoxType.Outbox, callOrder[0]);
        Assert.Equal(BoxType.Inbox, callOrder[1]);
    }
}
