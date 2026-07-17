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

namespace Paramore.Brighter.InMemory.Tests.Confirmation;

public class AsyncConfirmationPumpTests
{
    [Test]
    public async Task When_async_confirmation_is_on_should_not_write_bus_before_returning()
    {
        // Arrange
        const string topic = "test_topic_deferred";
        var messageId = Id.Random();
        var message = new Message(
            new MessageHeader(messageId, new RoutingKey(topic), MessageType.MT_DOCUMENT),
            new MessageBody("test_content"));
        var bus = new InternalBus();
        var producer = new InMemoryMessageProducer(bus, instrumentationOptions: InstrumentationOptions.All)
        {
            UseAsyncPublishConfirmation = true
        };

        var confirmed = new TaskCompletionSource<PublishConfirmationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        producer.OnMessagePublished += r => confirmed.TrySetResult(r);

        // Act
        producer.Send(message);

        // Assert — bus write is deferred: bus is empty immediately after Send returns
        await Assert.That(bus.Stream(new RoutingKey(topic))).IsEmpty();

        // Wait for the pump to drain and raise the confirmation (max 5s)
        var result = await confirmed.Task.WaitAsync(System.TimeSpan.FromSeconds(5));

        // Assert — confirmation eventually arrives and the message is now on the bus
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.MessageId).IsEqualTo(messageId);
        await Assert.That(bus.Stream(new RoutingKey(topic))).HasSingleItem();
    }

    [Test]
    public async Task When_async_confirmation_is_on_should_drain_in_fifo_enqueue_order()
    {
        // Arrange — 5 messages enqueued in a known order
        const string topic = "test_topic_fifo";
        const int count = 5;
        var bus = new InternalBus();
        var producer = new InMemoryMessageProducer(bus, instrumentationOptions: InstrumentationOptions.All)
        {
            UseAsyncPublishConfirmation = true
        };

        var messages = Enumerable.Range(0, count)
            .Select(_ => new Message(
                new MessageHeader(Id.Random(), new RoutingKey(topic), MessageType.MT_DOCUMENT),
                new MessageBody("body")))
            .ToArray();

        var remaining = count;
        var allConfirmed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        producer.OnMessagePublished += _ =>
        {
            if (Interlocked.Decrement(ref remaining) == 0)
                allConfirmed.TrySetResult();
        };

        // Act — enqueue all messages in order
        foreach (var msg in messages)
            producer.Send(msg);

        // Wait for all confirmations (max 5s)
        await allConfirmed.Task.WaitAsync(System.TimeSpan.FromSeconds(5));

        // Assert — bus contains all messages in FIFO enqueue order
        var busMessages = bus.Stream(new RoutingKey(topic)).ToList();
        await Assert.That(busMessages.Count).IsEqualTo(count);
        for (var i = 0; i < count; i++)
            await Assert.That(busMessages[i].Id).IsEqualTo(messages[i].Id);
    }
}