#region Licence
/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Confirmation;

public class AsyncConfirmationBatchFanOutTests
{
    [Fact]
    public async Task When_async_confirmation_is_on_should_fan_out_a_batch()
    {
        // Arrange
        const string topic = "test_topic_batch_fanout";
        const int batchSize = 3;
        var bus = new InternalBus();
        var producer = new InMemoryMessageProducer(bus, instrumentationOptions: InstrumentationOptions.All)
        {
            UseAsyncPublishConfirmation = true
        };

        var messages = Enumerable.Range(0, batchSize)
            .Select(_ => new Message(
                new MessageHeader(Id.Random(), new RoutingKey(topic), MessageType.MT_DOCUMENT),
                new MessageBody("body")))
            .ToArray();

        var batch = new MessageBatch(messages);

        var remaining = batchSize;
        var allConfirmed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        producer.OnMessagePublished += _ =>
        {
            if (Interlocked.Decrement(ref remaining) == 0)
                allConfirmed.TrySetResult();
        };

        // Act
        await producer.SendAsync(batch, CancellationToken.None);

        // Assert — bus write is deferred: bus is empty immediately after SendAsync returns
        Assert.Empty(bus.Stream(new RoutingKey(topic)));

        // Wait for all N confirmations (max 5s)
        await allConfirmed.Task.WaitAsync(System.TimeSpan.FromSeconds(5));

        // Assert — all N messages written and N confirmations raised, in FIFO batch order
        var busMessages = bus.Stream(new RoutingKey(topic)).ToList();
        Assert.Equal(batchSize, busMessages.Count);
        for (var i = 0; i < batchSize; i++)
            Assert.Equal(messages[i].Id, busMessages[i].Id);
    }
}
