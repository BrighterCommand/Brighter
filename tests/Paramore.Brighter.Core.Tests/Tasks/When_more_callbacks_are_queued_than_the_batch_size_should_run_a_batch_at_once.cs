using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Tasks;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Tasks;

public class BatchedCallbackQueueBatchingTests
{
    private const int BatchSize = 32; // the queue's default
    private const int QueuedCallbacks = 40; // more than one batch
    private static readonly TimeSpan s_wait = TimeSpan.FromSeconds(5);

    [Fact]
    public void When_more_callbacks_are_queued_than_the_batch_size_should_run_a_batch_at_once()
    {
        //Arrange
        var queue = new BatchedCallbackQueue();
        var inFlight = 0;
        var mostAtOnce = 0;
        var roundTrip = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var allDone = new CountdownEvent(QueuedCallbacks);

        // Park the drain thread, so every callback below is queued before it takes its next batch
        using var drainParked = new ManualResetEventSlim(false);
        using var releaseDrain = new ManualResetEventSlim(false);
        queue.Enqueue(() =>
        {
            drainParked.Set();
            releaseDrain.Wait();
            return Task.CompletedTask;
        });
        Assert.True(drainParked.Wait(s_wait), "The drain thread did not start");

        try
        {
            //Act
            for (var i = 0; i < QueuedCallbacks; i++)
            {
                queue.Enqueue(async () =>
                {
                    InterlockedMax(ref mostAtOnce, Interlocked.Increment(ref inFlight));
                    await roundTrip.Task; // a confirmation awaiting its outbox round trip
                    Interlocked.Decrement(ref inFlight);
                    allDone.Signal();
                });
            }
            releaseDrain.Set();

            var batchRunning = SpinWait.SpinUntil(() => Volatile.Read(ref inFlight) == BatchSize, s_wait);
            Thread.Sleep(100); // give the drain the chance to start more than one batch at once
            var atOnceBeforeRelease = Volatile.Read(ref mostAtOnce);
            roundTrip.SetResult();
            var allRan = allDone.Wait(s_wait);

            //Assert
            Assert.True(batchRunning, $"Expected {BatchSize} callbacks in progress at once, but at most {Volatile.Read(ref mostAtOnce)} were");
            Assert.Equal(BatchSize, atOnceBeforeRelease);
            Assert.True(allRan, "Not every queued callback ran");
            Assert.Equal(BatchSize, mostAtOnce);
        }
        finally
        {
            releaseDrain.Set();
            roundTrip.TrySetResult();
            queue.Complete();
        }
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while ((current = Volatile.Read(ref target)) < value
               && Interlocked.CompareExchange(ref target, value, current) != current) { }
    }
}
