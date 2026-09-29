#region Licence

/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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

#nullable enable

using System;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using Paramore.Brighter.RMQ.Sync.Tests.TestDoubles;
using RabbitMQ.Client;
using Xunit;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway;

[Trait("Category", "RMQ")]
[Collection("RMQ")]
public class RmqConsumerConnectionDisposalRaceTests
{
    [Fact]
    public async Task When_a_pending_connection_resumes_after_disposal_should_not_acquire_a_reference()
    {
        // Arrange
        var connection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@127.0.0.1:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };
        var routingKey = new RoutingKey(Guid.NewGuid().ToString());
        var factory = new ConnectionFactory { Uri = connection.AmpqUri.Uri };
        var pool = new RmqMessageGatewayConnectionPool(connection.Name!, connection.Heartbeat, loggerFactory: Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        using var peer = new RmqMessageProducer(connection, loggerFactory: Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        peer.Send(new Message(
            new MessageHeader(Id.Random(), routingKey, MessageType.MT_COMMAND), new MessageBody("peer")));
        var sharedConnection = pool.GetConnection(factory);
        Assert.NotNull(sharedConnection);
        var connecting = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var consumer = new PausedConnectionRmqConsumer(
            connection, new ChannelName(Guid.NewGuid().ToString()), routingKey, connecting, resume);
        var operation = Task.Run(consumer.Purge);

        try
        {
            await connecting.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // Act
            consumer.Dispose();
            resume.TrySetResult(true);
            var error = await Record.ExceptionAsync(() => operation.WaitAsync(TimeSpan.FromSeconds(5)));

            // Assert
            Assert.NotNull(error);
            Assert.IsType<ObjectDisposedException>(error.GetBaseException());
            Assert.True(sharedConnection.IsOpen);
            peer.Dispose();
            Assert.False(sharedConnection.IsOpen);
        }
        finally
        {
            resume.TrySetResult(true);
            await Record.ExceptionAsync(() => operation.WaitAsync(TimeSpan.FromSeconds(5)));
            pool.RemoveConnection(factory);
        }
    }
}
