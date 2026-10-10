using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.Sweeping;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Tasks;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Tasks;

[Collection(ThreadPoolStarvationCollection.Name)]
public class BatchedCallbackQueueStarvationTests
{
    [Fact]
    public void When_the_thread_pool_is_starved_should_still_run_queued_callbacks()
    {
        //Arrange
        var queue = new BatchedCallbackQueue();
        using var confirmed = new ManualResetEventSlim(false);

        var starvedPool = new StarvedThreadPool();
        try
        {
            //Act
            queue.Enqueue(() =>
            {
                confirmed.Set(); // a publish confirmation reaching its subscriber
                return Task.CompletedTask;
            });
            var ran = confirmed.Wait(TimeSpan.FromSeconds(3));

            //Assert
            Assert.True(ran, "The queued callback did not run while every thread-pool worker was blocked");
        }
        finally
        {
            starvedPool.Dispose();
            queue.Complete();
        }
    }
}
