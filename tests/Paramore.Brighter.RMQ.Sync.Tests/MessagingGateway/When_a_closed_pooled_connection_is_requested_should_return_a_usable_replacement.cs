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

using System;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using RabbitMQ.Client;
using Xunit;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway;

[Trait("Category", "RMQ")]
[Collection("RMQ connection pool")]
public class RmqClosedPooledConnectionReplacementTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void When_a_closed_pooled_connection_is_requested_should_return_a_usable_replacement(bool automaticRecoveryEnabled)
    {
        //Arrange
        var factory = new ConnectionFactory
        {
            Uri = new Uri("amqp://guest:guest@localhost:5672/%2f"),
            AutomaticRecoveryEnabled = automaticRecoveryEnabled
        };
        var pool = new RmqMessageGatewayConnectionPool($"pool-replacement-{Guid.NewGuid()}", 20);

        try
        {
            var original = Assert.IsAssignableFrom<IConnection>(pool.GetConnection(factory));
            Assert.Same(original, pool.GetConnection(factory));
            original.Close();

            //Act
            var replacement = Assert.IsAssignableFrom<IConnection>(pool.GetConnection(factory));

            //Assert
            Assert.NotSame(original, replacement);
            original.Dispose();
            Assert.True(replacement.IsOpen);
            Assert.Same(replacement, pool.GetConnection(factory));
            using var channel = replacement.CreateModel();
            var queue = channel.QueueDeclare();
            Assert.False(string.IsNullOrEmpty(queue.QueueName));
        }
        finally
        {
            pool.RemoveConnection(factory);
        }
    }
}
