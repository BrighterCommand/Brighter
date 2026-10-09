using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Post;

/// <summary>
/// The time of the last check is recorded when the check is queued, so the next check is due one interval after that,
/// however long the queued check waited before it started.
/// </summary>
public class OutstandingCountIntervalTests
{
    private const int LIMIT = 100;
    private static readonly TimeSpan s_interval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan s_deadline = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task When_the_first_check_starts_late_should_measure_the_interval_from_when_it_was_queued()
    {
        //Arrange
        //Every mediator with the same message and transaction types shares one static semaphore for its counts. While a
        //second mediator of those types holds its count open, the first check of the mediator under test cannot start.
        var holdingTimeProvider = new FakeTimeProvider();
        var holdingOutbox = new CountingOutbox(holdingTimeProvider);
        var holdingCommandProcessor = CountingOutboxCommandProcessorBuilder.Build(
            holdingOutbox, LIMIT, s_interval, holdingTimeProvider);

        var timeProvider = new FakeTimeProvider();
        var outbox = new CountingOutbox(timeProvider);
        var commandProcessor = CountingOutboxCommandProcessorBuilder.Build(
            outbox, LIMIT, s_interval, timeProvider);

        //a check is due on both, and the clocks only move when the test moves them
        holdingTimeProvider.Advance(TimeSpan.FromSeconds(2));
        timeProvider.Advance(TimeSpan.FromSeconds(2));

        holdingOutbox.HoldCounts();
        try
        {
            holdingCommandProcessor.Post(new MyCommand { Value = "Hold the semaphore" });
            Assert.True(await holdingOutbox.WaitForCountToStartAsync(s_deadline),
                "The holding mediator never started its count");

            //the first check is queued at 2s
            commandProcessor.Post(new MyCommand { Value = "Queue the first check" });
            Assert.Equal(0, outbox.OutstandingCountCalls); //the first check has not started

            //and only starts at 2.5s
            timeProvider.Advance(TimeSpan.FromMilliseconds(500));
        }
        finally
        {
            holdingOutbox.ReleaseCounts();
        }

        Assert.True(await outbox.WaitForOutstandingCountCallsAsync(1, s_deadline),
            "The first check never ran");

        //Act
        //at 2.9s less than one interval has passed since the first check was queued
        timeProvider.Advance(TimeSpan.FromMilliseconds(400));
        await PostForAsync(commandProcessor, TimeSpan.FromMilliseconds(500));
        var checksBeforeOneInterval = outbox.OutstandingCountCalls;

        //at 3.2s one interval has passed since the first check was queued, though not since it started
        timeProvider.Advance(TimeSpan.FromMilliseconds(300));
        var secondCheckRan = await PostUntilTheSecondCheckRunsAsync(commandProcessor, outbox);

        //Assert
        Assert.Equal(1, checksBeforeOneInterval);
        Assert.True(secondCheckRan, "No second check ran one interval after the first was queued");
    }

    //These helpers post more than once, so a post that comes while the first check is still finishing does not decide
    //the result
    private static async Task PostForAsync(CommandProcessor commandProcessor, TimeSpan duration)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < duration)
        {
            commandProcessor.Post(new MyCommand { Value = "Is a check due?" });
            await Task.Delay(TimeSpan.FromMilliseconds(20));
        }
    }

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
