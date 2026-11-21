#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.Core.Tests.BoxProvisioning.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.BoxProvisioning;

public class BoxProvisioningHostedServiceStartsTests
{
    [Fact]
    public async Task When_the_hosted_service_starts_should_provision_outbox_then_tripped_topics_then_inbox()
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
    public void When_a_box_type_is_added_should_keep_the_numbers_of_the_existing_box_types()
    {
        //Assert
        Assert.Equal(0, (int)BoxType.Inbox);
        Assert.Equal(1, (int)BoxType.Outbox);
    }
}
