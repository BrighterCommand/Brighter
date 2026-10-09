using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Post;

/// <summary>
/// When a check of the outstanding message count finishes, the next post that is due queues the next check. A post that
/// comes while the check is in flight does not move the time of the last check.
/// </summary>
public class OutstandingCountNextCheckTests
{
    private const int LIMIT = 100;
    private static readonly TimeSpan s_interval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan s_deadline = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task When_a_check_finishes_should_queue_the_next_due_check()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        var outbox = new CountingOutbox(timeProvider);
        var commandProcessor = CountingOutboxCommandProcessorBuilder.Build(outbox, LIMIT, s_interval, timeProvider);

        //the clock only moves when the test moves it
        timeProvider.Advance(TimeSpan.FromSeconds(2));

        outbox.HoldCounts();
        try
        {
            //the first check is queued at 2s and held open
            commandProcessor.Post(new MyCommand { Value = "Queue the first check" });
            Assert.True(await outbox.WaitForCountToStartAsync(s_deadline), "The first check never started");

            //at 4s a post is due, but the first check is still in flight
            timeProvider.Advance(TimeSpan.FromSeconds(2));
            commandProcessor.Post(new MyCommand { Value = "Due while the first check is in flight" });
        }
        finally
        {
            outbox.ReleaseCounts();
        }

        //Act
        //still at 4s, two seconds after the first check was queued
        var nextCheckRan = await PostUntilTheSecondCheckRunsAsync(commandProcessor, outbox);

        //Assert
        Assert.True(nextCheckRan, "No check ran after the first one finished");
    }

    //Post more than once, so a post that comes while the first check is still finishing does not decide the result
    private static async Task<bool> PostUntilTheSecondCheckRunsAsync(CommandProcessor commandProcessor, CountingOutbox outbox)
    {
        var stopwatch = Stopwatch.StartNew();
        while (outbox.OutstandingCountCalls < 2 && stopwatch.Elapsed < s_deadline)
        {
            commandProcessor.Post(new MyCommand { Value = "Is a check due?" });
            await Task.Delay(TimeSpan.FromMilliseconds(20));
        }

        return outbox.OutstandingCountCalls >= 2;
    }
}
