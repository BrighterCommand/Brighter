using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.Core.Tests.BoxProvisioning.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.BoxProvisioning;

public class When_box_provisioning_hosted_service_starts_it_should_provision_tripped_topics_after_outbox_before_inbox
{
    [Fact]
    public async Task Should_provision_outbox_then_tripped_topics_then_inbox()
    {
        //Arrange
        var callOrder = new List<BoxType>();
        var inboxProvisioner = new OrderTrackingBoxProvisioner(BoxType.Inbox, callOrder);
        var trippedTopicsProvisioner = new OrderTrackingBoxProvisioner(BoxType.TrippedTopics, callOrder);
        var outboxProvisioner = new OrderTrackingBoxProvisioner(BoxType.Outbox, callOrder);

        // Deliberately register in the reverse of the expected order
        var provisioners = new List<IAmABoxProvisioner> { inboxProvisioner, trippedTopicsProvisioner, outboxProvisioner };

        var hostedService = new BoxProvisioningHostedService(
            provisioners,
            NullLogger<BoxProvisioningHostedService>.Instance
        );

        //Act
        await hostedService.StartAsync(CancellationToken.None);

        //Assert
        Assert.Equal(3, callOrder.Count);
        Assert.Equal(BoxType.Outbox, callOrder[0]);
        Assert.Equal(BoxType.TrippedTopics, callOrder[1]);
        Assert.Equal(BoxType.Inbox, callOrder[2]);
    }

    [Fact]
    public void Should_keep_the_numbers_of_the_existing_box_types()
    {
        //Assert
        Assert.Equal(0, (int)BoxType.Inbox);
        Assert.Equal(1, (int)BoxType.Outbox);
    }
}
