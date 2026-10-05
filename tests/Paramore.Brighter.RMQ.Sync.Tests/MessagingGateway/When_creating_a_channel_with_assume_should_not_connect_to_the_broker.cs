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

using System;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using Paramore.Brighter.RMQ.Sync.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway;

/// <summary>
/// With <see cref="OnMissingChannel.Assume"/> the channel factory does no broker I/O, so a channel can be
/// created even when no broker is reachable (bugfix 0049, #4519).
/// </summary>
public class RmqSyncChannelFactoryAssumeTests
{
    [Fact]
    public void When_creating_a_channel_with_assume_should_not_connect_to_the_broker()
    {
        // Arrange - nothing listens on port 1, so any connection attempt fails
        var unreachableBroker = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:1/%2f"), connectionRetryCount: 0),
            Exchange = new Exchange("paramore.brighter.exchange")
        };

        var subscription = new RmqSubscription(
            subscriptionName: new SubscriptionName("rmq-sync-channel-factory-assume-test"),
            channelName: new ChannelName(Guid.NewGuid().ToString()),
            routingKey: new RoutingKey(Guid.NewGuid().ToString()),
            requestType: typeof(MyCommand),
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Assume);

        var channelFactory = new ChannelFactory(new RmqMessageConsumerFactory(unreachableBroker));

        // Act
        var exception = Record.Exception(() => channelFactory.CreateSyncChannel(subscription).Dispose());

        // Assert
        Assert.Null(exception);
    }
}
