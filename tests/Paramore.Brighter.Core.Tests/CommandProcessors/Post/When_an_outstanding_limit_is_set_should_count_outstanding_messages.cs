using System;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Post;

/// <summary>
/// The positive control for the "no limit" tests: when there is a limit (including 0, which
/// means "no outstanding messages allowed") the mediator must still ask the outbox for the outstanding count.
/// </summary>
public class OutstandingCountWithLimitTests
{
    private static readonly TimeSpan s_deadline = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task When_an_outstanding_limit_is_set_should_count_outstanding_messages()
    {
        //Arrange
        var outbox = new CountingOutbox(TimeProvider.System);
        var commandProcessor = CountingOutboxCommandProcessorBuilder.Build(
            outbox,
            maxOutStandingMessages: 100,
            maxOutStandingCheckInterval: TimeSpan.Zero);

        //Act
        commandProcessor.Post(new MyCommand { Value = "Hello World" });

        //Assert
        //the count runs on a background thread, so wait for it
        Assert.True(
            await outbox.WaitForOutstandingCountCallsAsync(1, s_deadline),
            "The outbox was never asked for the outstanding message count");
    }

    [Fact]
    public async Task When_the_outstanding_limit_is_zero_should_count_outstanding_messages()
    {
        //Arrange
        //0 is a real limit (no outstanding messages), not "no limit", so we must still count
        var outbox = new CountingOutbox(TimeProvider.System);
        var commandProcessor = CountingOutboxCommandProcessorBuilder.Build(
            outbox,
            maxOutStandingMessages: 0,
            maxOutStandingCheckInterval: TimeSpan.Zero);

        //Act
        commandProcessor.Post(new MyCommand { Value = "Hello World" });

        //Assert
        //the count runs on a background thread, so wait for it
        Assert.True(
            await outbox.WaitForOutstandingCountCallsAsync(1, s_deadline),
            "The outbox was never asked for the outstanding message count");
    }
}
