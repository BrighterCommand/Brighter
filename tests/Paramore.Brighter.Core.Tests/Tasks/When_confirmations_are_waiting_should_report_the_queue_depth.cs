using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.Tasks.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.Core.Tests.Tasks;

public class BatchedCallbackQueueDepthTests
{
    private const int WaitingConfirmations = 4;

    [Test]
    public async Task When_confirmations_are_waiting_should_report_the_queue_depth()
    {
        //Arrange
        using var observed = new ObservedQueueDepths();
        _ = new PublishConfirmationMeter(observed.MeterFactory);

        var topic = new RoutingKey(Guid.NewGuid().ToString()); // the registry is process-wide, so find our own queue
        var queue = new BatchedCallbackQueue(MessagingSystem.Kafka, topic);
        using var drainParked = new ManualResetEventSlim(false);
        using var releaseDrain = new ManualResetEventSlim(false);

        try
        {
            //Act
            // One confirmation holds the drain thread; the rest wait behind it
            queue.Enqueue(() =>
            {
                drainParked.Set();
                releaseDrain.Wait();
                return Task.CompletedTask;
            });
            await Assert.That(drainParked.Wait(TimeSpan.FromSeconds(5))).IsTrue().Because("The drain thread did not start");
            for (var i = 1; i < WaitingConfirmations; i++)
                queue.Enqueue(() => Task.CompletedTask);

            var depths = observed.Observe().Where(d => d.Destination == topic.Value).ToList();

            //Assert
            var depth = await Assert.That(depths).HasSingleItem();
            await Assert.That(depth.Value).IsEqualTo(WaitingConfirmations);
            await Assert.That(depth.MessagingSystem).IsEqualTo("kafka");
        }
        finally
        {
            releaseDrain.Set();
            queue.Complete();
        }
    }
}
