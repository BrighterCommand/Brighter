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
/// A channel handed out for a subscription with <see cref="OnMissingChannel.Create"/> must already have its
/// queue declared and bound, so a message published before the channel's first receive is not lost
/// (bugfix 0049, #4519).
/// </summary>
[Trait("Category", "RMQ")]
[Collection("RMQ")]
public class RmqChannelFactorySyncChannelProvisioningTests : IDisposable
{
    private readonly RmqMessagingGatewayConnection _rmqConnection;
    private readonly IAmAMessageProducerSync _messageProducer;
    private readonly RmqSubscription _subscription;
    private readonly Message _message;
    private IAmAChannelSync? _channel;

    public RmqChannelFactorySyncChannelProvisioningTests()
    {
        _rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };

        // A fresh queue name, so the queue cannot already exist on the broker
        var routingKey = new RoutingKey(Guid.NewGuid().ToString());
        var queueName = new ChannelName(Guid.NewGuid().ToString());

        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND),
            new MessageBody("published before the first receive"));

        _messageProducer = new RmqMessageProducer(_rmqConnection);

        _subscription = new RmqSubscription(
            subscriptionName: new SubscriptionName("rmq-channel-factory-provisioning-test"),
            channelName: queueName,
            routingKey: routingKey,
            requestType: typeof(MyCommand),
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Create);
    }

    [Fact]
    public void When_creating_a_sync_channel_with_create_should_deliver_a_message_published_before_the_first_receive()
    {
        // Arrange
        _channel = new ChannelFactory(new RmqMessageConsumerFactory(_rmqConnection))
            .CreateSyncChannel(_subscription);

        // Act - publish before the channel has ever received
        _messageProducer.Send(_message);
        var received = _channel.Receive(TimeSpan.FromMilliseconds(5000));

        // Assert
        Assert.Equal(_message.Id, received.Id);
        Assert.Equal(_message.Body.Value, received.Body.Value);
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _messageProducer.Dispose();
    }
}
