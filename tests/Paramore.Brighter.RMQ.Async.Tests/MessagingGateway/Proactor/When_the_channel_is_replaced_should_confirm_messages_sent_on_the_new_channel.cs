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
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Paramore.Brighter.RMQ.Async.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.Proactor;

[Trait("Category", "RMQ")]
public class RmqChannelReplacedConfirmationAsyncTests : IAsyncDisposable
{
    private readonly RoutingKey _routingKey = new(Guid.NewGuid().ToString());
    private readonly ConfirmCountingRmqProducer _messageProducer;
    private readonly Message _messageOnNewChannel;
    private readonly TaskCompletionSource<PublishConfirmationResult> _newChannelConfirmation =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public RmqChannelReplacedConfirmationAsyncTests()
    {
        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };

        _messageOnNewChannel = NewMessage();

        _messageProducer = new ConfirmCountingRmqProducer(rmqConnection);
        _messageProducer.OnMessagePublished += result =>
        {
            if (result.MessageId == _messageOnNewChannel.Id) _newChannelConfirmation.TrySetResult(result);
        };

        //we need a queue to avoid a discard
        new QueueFactory(rmqConnection, new ChannelName(Guid.NewGuid().ToString()), new RoutingKeys(_routingKey))
            .CreateAsync()
            .GetAwaiter()
            .GetResult();
    }

    [Fact]
    public async Task When_the_channel_is_replaced_should_confirm_messages_sent_on_the_new_channel()
    {
        //Arrange — a send opens the first channel, then the channel closes under the producer,
        //as it does when the broker closes it or another gateway drops the shared connection
        await _messageProducer.SendAsync(NewMessage());
        await _messageProducer.Channels[0].CloseAsync();

        //Act — the next send opens a new channel
        await _messageProducer.SendAsync(_messageOnNewChannel);

        //Assert — the message sent on the new channel is confirmed
        var confirmed = await Task.WhenAny(_newChannelConfirmation.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.True(ReferenceEquals(confirmed, _newChannelConfirmation.Task),
            "Timed out waiting for the confirmation of the message sent on the new channel");
        Assert.True((await _newChannelConfirmation.Task).Success);

        //Assert — the producer now listens to the new channel only
        var oldChannel = _messageProducer.Channels[0];
        var newChannel = _messageProducer.Channels[1];

        Assert.Equal(0, oldChannel.AckSubscriberCount);
        Assert.Equal(0, oldChannel.NackSubscriberCount);

        Assert.Equal(1, newChannel.AckSubscriberCount);
        Assert.Equal(1, newChannel.NackSubscriberCount);
    }

    private Message NewMessage() => new(
        new MessageHeader(Id.Random(), _routingKey, MessageType.MT_COMMAND),
        new MessageBody("confirm me"));

    public async ValueTask DisposeAsync() => await _messageProducer.DisposeAsync();
}
