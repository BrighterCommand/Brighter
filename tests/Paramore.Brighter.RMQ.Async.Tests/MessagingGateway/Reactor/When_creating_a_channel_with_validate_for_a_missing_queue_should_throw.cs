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
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Paramore.Brighter.RMQ.Async.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.Reactor;

/// <summary>
/// A channel for a subscription with <see cref="OnMissingChannel.Validate"/> must not be handed out when its
/// queue does not exist: the channel factory reports a <see cref="ChannelFailureException"/> instead
/// (bugfix 0049, #4519).
/// </summary>
[Trait("Category", "RMQ")]
[Collection("RMQ")]
public class RmqChannelFactorySyncChannelValidateTests
{
    [Fact]
    public void When_creating_a_channel_with_validate_for_a_missing_queue_should_throw()
    {
        // Arrange
        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };

        // A fresh queue name, so the queue cannot exist on the broker
        var subscription = new RmqSubscription(
            subscriptionName: new SubscriptionName("rmq-channel-factory-validate-test"),
            channelName: new ChannelName(Guid.NewGuid().ToString()),
            routingKey: new RoutingKey(Guid.NewGuid().ToString()),
            requestType: typeof(MyCommand),
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Validate);

        var channelFactory = new ChannelFactory(new RmqMessageConsumerFactory(rmqConnection));

        // Act
        var exception = Record.Exception(() => channelFactory.CreateSyncChannel(subscription));

        // Assert
        Assert.IsType<ChannelFailureException>(exception);
    }
}
