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
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Paramore.Brighter.RMQ.Async.Tests.TestDoubles;
using RabbitMQ.Client.Exceptions;
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.Proactor;

[Trait("Category", "RMQ")]
public class RmqSendFailsOnReplacedChannelAsyncTests : IAsyncDisposable
{
    private readonly RoutingKey _routingKey = new(Guid.NewGuid().ToString());
    private readonly ParkedPublishRmqProducer _messageProducer;
    private readonly ConcurrentQueue<PublishConfirmationResult> _confirmed = new();

    public RmqSendFailsOnReplacedChannelAsyncTests()
    {
        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };

        _messageProducer = new ParkedPublishRmqProducer(rmqConnection);
        _messageProducer.OnMessagePublished += result => _confirmed.Enqueue(result);

        //we need a queue to avoid a discard
        new QueueFactory(rmqConnection, new ChannelName(Guid.NewGuid().ToString()), new RoutingKeys(_routingKey))
            .CreateAsync()
            .GetAwaiter()
            .GetResult();
    }

    [Fact]
    public async Task When_a_send_fails_on_a_replaced_channel_should_not_remove_the_new_channels_confirmation()
    {
        //Arrange — a send is in flight on the first channel, which then closes under it
        var onOldChannel = NewMessage();
        var onNewChannel = NewMessage();
        var oldChannelSend = _messageProducer.SendAsync(onOldChannel);
        var oldChannel = await _messageProducer.OpenedChannel(0);
        await oldChannel.PublishParked;
        oldChannel.MarkClosed();

        //Arrange — a second send opens a new channel, whose delivery tags restart, and is in flight on it
        var newChannelSend = _messageProducer.SendAsync(onNewChannel);
        var newChannel = await _messageProducer.OpenedChannel(1);
        await newChannel.PublishParked;

        //Act — the send on the old channel fails, then the send on the new channel publishes
        oldChannel.Release();
        await Assert.ThrowsAsync<AlreadyClosedException>(() => oldChannelSend);
        newChannel.Release();
        await newChannelSend;

        //Assert — the message sent on the new channel is confirmed
        Assert.Contains(_confirmed, result => result.MessageId == onNewChannel.Id && result.Success);
    }

    private Message NewMessage() => new(
        new MessageHeader(Id.Random(), _routingKey, MessageType.MT_COMMAND),
        new MessageBody("confirm me"));

    public async ValueTask DisposeAsync() => await _messageProducer.DisposeAsync();
}
