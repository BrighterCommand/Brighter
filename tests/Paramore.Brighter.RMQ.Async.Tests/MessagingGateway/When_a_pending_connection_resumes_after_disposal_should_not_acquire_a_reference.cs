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
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Paramore.Brighter.RMQ.Async.Tests.TestDoubles;
using RabbitMQ.Client;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway;

[Category("RMQ")]
[NotInParallel]
public class RmqConsumerConnectionDisposalRaceTests
{
    [Test]
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
        var pool = new RmqMessageGatewayConnectionPool(connection.Name, connection.Heartbeat);
        using var peer = new RmqMessageProducer(connection);
        await peer.SendAsync(new Message(
            new MessageHeader(Id.Random(), routingKey, MessageType.MT_COMMAND), new MessageBody("peer")));
        var sharedConnection = await pool.GetConnectionAsync(factory);
        await Assert.That(sharedConnection).IsNotNull();
        var connecting = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var consumer = new PausedConnectionRmqConsumer(
            connection, new ChannelName(Guid.NewGuid().ToString()), routingKey, connecting, resume);
        var operation = consumer.PurgeAsync();

        try
        {
            await connecting.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // Act
            await consumer.DisposeAsync();
            resume.TrySetResult(true);
            var error = await TestExceptionRecorder.CaptureAsync(() => operation.WaitAsync(TimeSpan.FromSeconds(5)));

            // Assert
            await Assert.That(error).IsNotNull();
            await Assert.That(error.GetBaseException()).IsTypeOf<ObjectDisposedException>();
            await Assert.That(sharedConnection.IsOpen).IsTrue();
            await peer.DisposeAsync();
            await Assert.That(sharedConnection.IsOpen).IsFalse();
        }
        finally
        {
            resume.TrySetResult(true);
            await TestExceptionRecorder.CaptureAsync(() => operation.WaitAsync(TimeSpan.FromSeconds(5)));
            await pool.RemoveConnectionAsync(factory);
        }
    }
}
