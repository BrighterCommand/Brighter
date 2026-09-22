#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Diagnostics;
using System.Threading;
using Paramore.Brighter.MQTT.Tests.MessagingGateway.Helpers.Base;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.MQTT.Tests.MessagingGateway.Reactor
{
    /// <summary>
    /// A receive blocks until a message arrives. Once the buffer is empty - whether it was drained
    /// by a receive or discarded by a purge - the next receive has nothing to hand back, so it must
    /// wait for its timeout rather than return an empty result straight away.
    /// </summary>
    /// <remarks>
    /// The existing purge test asserts only the shape of the result, which an immediate empty return
    /// satisfies. These assert the wait, which is what the Reactor pump depends on: a receive that
    /// returns instantly turns the pump's blocking wait into a spin.
    /// </remarks>
    [Trait("Category", "MQTT")]
    [Collection("MQTT")]
    public class ArrivalSignalTests : MqttTestClassBase<ArrivalSignalTests>
    {
        private const string ClientId = "BrighterIntegrationTests-ArrivalSignal";
        private const string TopicPrefix = "BrighterIntegrationTests/ArrivalSignalTests";

        // A burst, so that any per-message signal left behind is left behind more than once.
        private const int BURST_SIZE = 5;

        // Long enough that "waited" and "returned immediately" cannot be confused for one another.
        private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromMilliseconds(500);

        // Scheduling slack: the wait need only be observably close to the timeout.
        private static readonly TimeSpan MinimumWait = TimeSpan.FromMilliseconds(400);

        public ArrivalSignalTests(ITestOutputHelper testOutputHelper)
            : base(ClientId, TopicPrefix, testOutputHelper)
        {
        }

        private IAmAMessageProducerSync MessageProducerSync => (MessageProducerAsync as IAmAMessageProducerSync)!;

        private IAmAMessageConsumerSync MessageConsumerSync => (MessageConsumerAsync as IAmAMessageConsumerSync)!;

        [Fact]
        public void When_a_purge_discards_buffered_messages_should_wait_for_the_next_arrival()
        {
            // Arrange - a burst arrives and is then thrown away, so the buffer is empty.
            SendBurst();
            Thread.Sleep(500);
            MessageConsumerSync.Purge();

            // Act
            var stopwatch = Stopwatch.StartNew();
            Message[] received = MessageConsumerSync.Receive(ReceiveTimeout);
            stopwatch.Stop();

            // Assert - nothing to hand back, so the receive waited for its timeout.
            Assert.Contains(_noopMessage, received);
            Assert.True(stopwatch.Elapsed >= MinimumWait,
                $"Receive returned after {stopwatch.ElapsedMilliseconds}ms with an empty buffer; "
                + $"it should have waited about {ReceiveTimeout.TotalMilliseconds}ms for an arrival.");
        }

        [Fact]
        public void When_a_receive_drains_a_burst_should_wait_for_the_next_arrival()
        {
            // Arrange - a burst arrives and is read out in full. How many messages a single receive
            // hands back is the batch size's business, so drain in a loop rather than assume one
            // call empties the buffer.
            SendBurst();
            Thread.Sleep(500);

            int drained = 0;
            while (drained < BURST_SIZE)
            {
                Message[] batch = MessageConsumerSync.Receive(ReceiveTimeout);
                Assert.DoesNotContain(_noopMessage, batch);
                drained += batch.Length;
            }

            // Act
            var stopwatch = Stopwatch.StartNew();
            Message[] received = MessageConsumerSync.Receive(ReceiveTimeout);
            stopwatch.Stop();

            // Assert - every message has been read out, so the next receive waited for its timeout.
            Assert.Contains(_noopMessage, received);
            Assert.True(stopwatch.Elapsed >= MinimumWait,
                $"Receive returned after {stopwatch.ElapsedMilliseconds}ms having already read out "
                + $"all {drained} message(s); it should have waited about "
                + $"{ReceiveTimeout.TotalMilliseconds}ms for a new arrival.");
        }

        private void SendBurst()
        {
            for (int i = 0; i < BURST_SIZE; i++)
            {
                Message message = new(
                    new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey(Guid.NewGuid().ToString()), MessageType.MT_COMMAND),
                    new MessageBody("test message")
                );

                MessageProducerSync.Send(message);
            }
        }
    }
}
