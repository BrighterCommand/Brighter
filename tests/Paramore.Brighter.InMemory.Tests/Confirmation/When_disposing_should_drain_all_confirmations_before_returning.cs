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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Confirmation;

public class GracefulDrainOnDisposeTests
{
    [Fact]
    public async Task When_disposing_should_drain_all_confirmations_before_returning()
    {
        // Arrange — a gate keeps every confirmation callback outstanding after the pump
        // has submitted it, so DisposeAsync cannot return early before callbacks finish.
        const int messageCount = 10;
        var bus = new InternalBus();

        var gate = new SemaphoreSlim(0, messageCount);
        var confirmationCount = 0;

        var producer = new InMemoryMessageProducer(bus, instrumentationOptions: InstrumentationOptions.All)
        {
            UseAsyncPublishConfirmation = true
        };
        producer.OnMessagePublished += _ =>
        {
            gate.Wait(); // park until released — keeps callbacks in-flight during DisposeAsync
            Interlocked.Increment(ref confirmationCount);
        };

        for (var i = 0; i < messageCount; i++)
        {
            producer.Send(new Message(
                new MessageHeader(Id.Random(), new RoutingKey("drain.topic"), MessageType.MT_DOCUMENT),
                new MessageBody($"msg-{i}")));
        }

        // Release all gates from a background thread AFTER DisposeAsync has started blocking,
        // so the two-stage drain can finish.  With a no-op DisposeAsync the releaser never
        // gets a chance to fire before the assertion fires.
        var releaser = Task.Run(async () =>
        {
            await Task.Delay(50); // ensure DisposeAsync is already waiting before we open the gate
            gate.Release(messageCount);
        });

        // Act — DisposeAsync must: (1) complete the channel writer, (2) await the drain worker,
        // (3) await every confirmation raise Task before returning.
        await producer.DisposeAsync();

        // Assert — immediately after DisposeAsync returns (before the releaser even has a chance
        // to fire on its own), every confirmation must already be counted.
        // With a no-op DisposeAsync the count is 0 here → test fails RED.
        // With the two-stage drain, DisposeAsync blocked until the releaser fired and all
        // callbacks incremented the counter → count == messageCount → GREEN.
        var snapshot = Volatile.Read(ref confirmationCount);
        Assert.Equal(messageCount, snapshot);

        await releaser; // cleanup — ensure the background task has exited
    }
}
