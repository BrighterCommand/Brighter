using System;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Post;

/// <summary>
/// A mediator keeps at most one check of the outstanding message count in flight. While one is running, further posts
/// do not queue another, even though with an interval of zero every post is due for one.
/// </summary>
public class OutstandingCountInFlightTests
{
    private const int LIMIT = 100;
    private const int POST_COUNT = 50;
    private static readonly TimeSpan s_deadline = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task When_a_check_is_in_flight_should_queue_no_further_check()
    {
        //Arrange
        //With an interval of zero every post is due for a check, so only the check in flight can stop another being queued
        var outbox = new CountingOutbox(TimeProvider.System);
        var commandProcessor = CountingOutboxCommandProcessorBuilder.Build(outbox, LIMIT, TimeSpan.Zero);

        outbox.HoldCounts();
        try
        {
            commandProcessor.Post(new MyCommand { Value = "Start the first check" });
            Assert.True(await outbox.WaitForCountToStartAsync(s_deadline), "The first check never started");

            //Act
            for (var i = 0; i < POST_COUNT; i++)
            {
                commandProcessor.Post(new MyCommand { Value = $"Hello World: {i}" });
            }
        }
        finally
        {
            outbox.ReleaseCounts();
        }

        //Assert
        //give any check queued behind the first one time to run before we count; until then it would still be waiting on
        //the semaphore the counts share
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        Assert.Equal(1, outbox.OutstandingCountCalls);
    }
}
