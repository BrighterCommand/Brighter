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

public class RmqMessageProducerDisposedSendTests
{
    [Fact]
    public void When_sending_after_dispose_should_throw_object_disposed_exception()
    {
        // Arrange
        var messageProducer = CreateMessageProducer();
        messageProducer.Dispose();

        // Act
        var exception = Record.Exception(() => messageProducer.Send(CreateMessage()));

        // Assert
        Assert.IsType<ObjectDisposedException>(exception);
    }

    [Fact]
    public async Task When_sending_async_after_dispose_should_throw_object_disposed_exception()
    {
        // Arrange
        var messageProducer = CreateMessageProducer();
        await messageProducer.DisposeAsync();

        // Act
        var exception = await Record.ExceptionAsync(async () => await messageProducer.SendAsync(CreateMessage()));

        // Assert
        Assert.IsType<ObjectDisposedException>(exception);
    }

    private static RmqMessageProducer CreateMessageProducer()
    {
        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };

        return new RmqMessageProducer(rmqConnection);
    }

    private static Message CreateMessage()
    {
        var routingKey = new RoutingKey(Guid.NewGuid().ToString());

        return new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND),
            new MessageBody("test content"));
    }
}
