using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Post;

/// <summary>
/// The mediator should not queue another count of outstanding messages until the check interval has passed since it queued
/// the last one. The count runs on a background thread, so a burst of posts that arrives within the interval, before that
/// thread has started, should queue only one.
/// </summary>
public class OutstandingCountBurstTests
{
    private const int LIMIT = 100;
    private const int POST_COUNT = 50;
    private static readonly TimeSpan s_interval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan s_deadline = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task When_a_burst_of_posts_arrives_before_the_first_check_starts_should_queue_one_outstanding_count()
    {
        //Arrange
        //Every mediator with the same message and transaction types shares one static semaphore for its counts. While a
        //second mediator of those types holds its count open, the first check of the mediator under test cannot start, so
        //the whole burst arrives before it does.
        var holdingTimeProvider = new FakeTimeProvider();
        var holdingOutbox = new CountingOutbox(holdingTimeProvider);
        var holdingCommandProcessor = CountingOutboxCommandProcessorBuilder.Build(
            holdingOutbox, LIMIT, s_interval, holdingTimeProvider);

        var timeProvider = new FakeTimeProvider();
        var outbox = new CountingOutbox(timeProvider);
        var commandProcessor = CountingOutboxCommandProcessorBuilder.Build(
            outbox, LIMIT, s_interval, timeProvider);

        //a check is due on both, and the clocks are frozen from here on, so no further check can become due
        holdingTimeProvider.Advance(TimeSpan.FromSeconds(2));
        timeProvider.Advance(TimeSpan.FromSeconds(2));

        holdingOutbox.HoldCounts();
        try
        {
            holdingCommandProcessor.Post(new MyCommand { Value = "Hold the semaphore" });
            Assert.True(await holdingOutbox.WaitForCountToStartAsync(s_deadline),
                "The holding mediator never started its count");

            //Act
            for (var i = 0; i < POST_COUNT; i++)
            {
                commandProcessor.Post(new MyCommand { Value = $"Hello World: {i}" });
            }

            Assert.Equal(0, outbox.OutstandingCountCalls); //the burst arrived before the first check started
        }
        finally
        {
            holdingOutbox.ReleaseCounts();
        }

        //Assert
        Assert.True(await outbox.WaitForOutstandingCountCallsAsync(1, s_deadline),
            "The mediator under test never counted");
        //give any check queued behind the first one time to run before we count
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        Assert.Equal(1, outbox.OutstandingCountCalls);
    }
}
