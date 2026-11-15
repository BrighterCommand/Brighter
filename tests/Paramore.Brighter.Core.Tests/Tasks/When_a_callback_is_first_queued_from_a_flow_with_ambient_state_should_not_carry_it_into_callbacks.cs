using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.Core.Tests.Tasks;

public class BatchedCallbackQueueAmbientStateTests
{
    private const int Callbacks = 2;
    private static readonly AsyncLocal<string?> s_ambient = new();

    [Test]
    public async Task When_a_callback_is_first_queued_from_a_flow_with_ambient_state_should_not_carry_it_into_callbacks()
    {
        //Arrange
        var queue = new BatchedCallbackQueue();
        var seen = new ConcurrentQueue<string?>();
        using var allRan = new CountdownEvent(Callbacks);
        Func<Task> recordAmbient = () =>
        {
            seen.Enqueue(s_ambient.Value);
            allRan.Signal();
            return Task.CompletedTask;
        };

        try
        {
            //Act
            await Task.Run(() =>
            {
                s_ambient.Value = "the first request's state"; // e.g. a logging scope or baggage
                queue.Enqueue(recordAmbient); // starts the drain thread
            });
            queue.Enqueue(recordAmbient); // a later confirmation, from a flow with no ambient state

            //Assert
            await Assert.That(allRan.Wait(TimeSpan.FromSeconds(3))).IsTrue().Because("The callbacks did not run");
            foreach (var item in seen) { await Assert.That(item).IsNull(); }
        }
        finally
        {
            queue.Complete();
        }
    }
}
