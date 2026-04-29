using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Bus;

public class InternalBusConcurrentEnqueueTests
{
    [Fact]
    public void When_concurrently_enqueueing_to_a_fresh_routing_key_no_messages_are_lost()
    {
        // Reproduces issue #4098: TOCTOU race in InternalBus.Enqueue caused the
        // losing thread of a concurrent first-write to enqueue into a dangling
        // BlockingCollection that was never published to the dictionary.
        const int iterations = 200;
        const int producersPerIteration = 4;
        var lostIterations = new List<(string Topic, int Expected, int Actual)>();

        for (var i = 0; i < iterations; i++)
        {
            var internalBus = new InternalBus();
            var routingKey = new RoutingKey($"concurrent-fresh-{Guid.NewGuid():N}");
            using var startGate = new ManualResetEventSlim(false);

            var producers = Enumerable.Range(0, producersPerIteration)
                .Select(producerIndex => Task.Run(() =>
                {
                    startGate.Wait();
                    var message = new Message(
                        new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND),
                        new MessageBody($"body-{producerIndex}"));
                    internalBus.Enqueue(message);
                }))
                .ToArray();

            startGate.Set();
            Task.WaitAll(producers);

            var observed = internalBus.Stream(routingKey).Count();
            if (observed != producersPerIteration)
            {
                lostIterations.Add((routingKey.Value, producersPerIteration, observed));
            }
        }

        Assert.True(
            lostIterations.Count == 0,
            $"InternalBus dropped messages on {lostIterations.Count}/{iterations} iterations: " +
            string.Join("; ", lostIterations.Take(5).Select(l => $"{l.Topic} expected={l.Expected} actual={l.Actual}")));
    }
}
