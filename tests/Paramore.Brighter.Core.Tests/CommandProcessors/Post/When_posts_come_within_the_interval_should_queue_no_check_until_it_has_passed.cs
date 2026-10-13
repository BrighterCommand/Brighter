using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Post;

/// <summary>
/// After a check of the outstanding message count, posts that come before the check interval has passed queue no other
/// check. A post after the interval has passed queues the next one.
/// </summary>
public class OutstandingCountIntervalTests
{
    private const int LIMIT = 100;
    private static readonly TimeSpan s_interval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan s_deadline = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task When_posts_come_within_the_interval_should_queue_no_check_until_it_has_passed()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        var outbox = new CountingOutbox(timeProvider);
        var commandProcessor = CountingOutboxCommandProcessorBuilder.Build(outbox, LIMIT, s_interval, timeProvider);

        timeProvider.Advance(TimeSpan.FromSeconds(2));
        commandProcessor.Post(new MyCommand { Value = "Queue the first check" });
        Assert.True(await outbox.WaitForOutstandingCountCallsAsync(1, s_deadline), "The first check never ran");

        //Act
        timeProvider.Advance(TimeSpan.FromMilliseconds(900));
        await PostForAsync(commandProcessor, TimeSpan.FromMilliseconds(500));
        var checksWithinTheInterval = outbox.OutstandingCountCalls;

        timeProvider.Advance(TimeSpan.FromMilliseconds(200));
        var secondCheckRan = await PostUntilTheSecondCheckRunsAsync(commandProcessor, outbox);

        //Assert
        Assert.Equal(1, checksWithinTheInterval);
        Assert.True(secondCheckRan, "No second check ran once the interval had passed");
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
