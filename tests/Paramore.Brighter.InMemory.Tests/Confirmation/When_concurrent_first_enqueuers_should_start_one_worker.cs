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

public class ConcurrentStartGuardTests
{
    [Fact]
    public async Task When_concurrent_first_enqueuers_should_start_one_worker()
    {
        // Arrange — many threads race to be the first enqueuer while _worker is still null
        const string topic = "test_topic_concurrent_start";
        const int threadCount = 20;
        var bus = new InternalBus();
        var producer = new InMemoryMessageProducer(bus, instrumentationOptions: InstrumentationOptions.All)
        {
            UseAsyncPublishConfirmation = true
        };

        var messages = Enumerable.Range(0, threadCount)
            .Select(_ => new Message(
                new MessageHeader(Id.Random(), new RoutingKey(topic), MessageType.MT_DOCUMENT),
                new MessageBody("body")))
            .ToArray();

        var remaining = threadCount;
        var allConfirmed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        producer.OnMessagePublished += _ =>
        {
            if (Interlocked.Decrement(ref remaining) == 0)
                allConfirmed.TrySetResult();
        };

        // A Barrier forces all threads to call Send at the same instant, maximising the
        // race on _worker == null. Without a single-start guard, multiple drain workers
        // can start; with SingleReader = true, a second concurrent ReadAsync overwrites
        // the first's waiter slot, potentially starving it and preventing its confirmations
        // from ever firing.
        var gate = new Barrier(threadCount);
        var tasks = messages
            .Select(msg => Task.Run(() =>
            {
                gate.SignalAndWait(); // all threads cross together — race on worker start
                producer.Send(msg);
            }))
            .ToArray();

        // Act — release all threads simultaneously
        await Task.WhenAll(tasks);

        // Wait for every confirmation to arrive (5 s timeout surfaces any starvation)
        await allConfirmed.Task.WaitAsync(System.TimeSpan.FromSeconds(5));

        // Assert — exactly N bus writes and N confirmations; no duplicates, no drops
        var busMessages = bus.Stream(new RoutingKey(topic)).ToList();
        Assert.Equal(threadCount, busMessages.Count);
        Assert.Equal(threadCount, busMessages.Select(m => m.Id).Distinct().Count());
    }
}
