#region Licence

/* The MIT License (MIT)
Copyright © 2026 Tom Longhurst <30480171+thomhurst@users.noreply.github.com>

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
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.Proactor;

[Trait("Category", "RMQ")]
public class RmqMessageProducerDisposeConfirmationTests : IDisposable, IAsyncLifetime
{
    private readonly Message _message;
    private readonly RmqMessageProducer _messageProducer;
    private readonly RmqMessagingGatewayConnection _rmqConnection;
    private readonly ChannelName _channelName;
    private readonly RoutingKeys _routingKeys;
    private readonly TaskCompletionSource<bool> _published = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public RmqMessageProducerDisposeConfirmationTests()
    {
        var routingKey = new RoutingKey(Guid.NewGuid().ToString());

        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND),
            new MessageBody("test content"));

        _rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };
        _channelName = new ChannelName(Guid.NewGuid().ToString());
        _routingKeys = new RoutingKeys(routingKey);

        _messageProducer = new RmqMessageProducer(
            _rmqConnection,
            new RmqPublication
            {
                MakeChannels = OnMissingChannel.Create,
                WaitForConfirmsTimeOutInMilliseconds = 2000
            });

        _messageProducer.OnMessagePublished += (success, messageId) =>
        {
            if (messageId == _message.Id)
                _published.TrySetResult(success);
        };
    }

    [Fact]
    public async Task When_disposing_after_sending_should_publish_confirmation()
    {
        // Arrange handled by fixture setup.

        // Act
        _messageProducer.Send(_message);
        _messageProducer.Dispose();

        // Assert
        // Dispose waits for confirms; the timeout keeps failures bounded if that contract regresses.
        Assert.True(await _published.Task.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    public void Dispose()
    {
        // The test disposes explicitly; teardown keeps cleanup idempotent if the test fails first.
        _messageProducer.Dispose();
    }

    public async Task InitializeAsync()
    {
        await new QueueFactory(_rmqConnection, _channelName, _routingKeys).CreateAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;
}
