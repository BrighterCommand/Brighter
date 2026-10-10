using System;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Post;

/// <summary>
/// With the default <c>AddProducers</c> settings (a limit of -1 and a check interval of <see cref="TimeSpan.Zero"/>)
/// nothing compares the outstanding count with a limit, so the mediator should not ask the outbox for it after a post.
/// </summary>
public class OutstandingCountWithoutLimitTests
{
    private const int POST_COUNT = 20;

    private readonly CountingOutbox _outbox;
    private readonly CommandProcessor _commandProcessor;
    private readonly CountingOutbox _controlOutbox;
    private readonly CommandProcessor _controlCommandProcessor;

    public OutstandingCountWithoutLimitTests()
    {
        _outbox = new CountingOutbox(TimeProvider.System);
        _commandProcessor = CountingOutboxCommandProcessorBuilder.Build(
            _outbox,
            maxOutStandingMessages: -1,
            maxOutStandingCheckInterval: TimeSpan.Zero);

        //The control has a limit, so it does count; it shows that the background work had the chance to run
        _controlOutbox = new CountingOutbox(TimeProvider.System);
        _controlCommandProcessor = CountingOutboxCommandProcessorBuilder.Build(
            _controlOutbox,
            maxOutStandingMessages: 100,
            maxOutStandingCheckInterval: TimeSpan.Zero);
    }

    [Fact]
    public async Task When_no_outstanding_limit_is_set_should_not_count_outstanding_messages()
    {
        //Arrange

        //Act
        for (var i = 0; i < POST_COUNT; i++)
        {
            _commandProcessor.Post(new MyCommand { Value = $"Hello World: {i}" });
        }

        await WaitForBackgroundCountsToHaveRun();

        //Assert
        Assert.Equal(0, _outbox.OutstandingCountCalls);
    }

    [Fact]
    public async Task When_no_outstanding_limit_is_set_should_not_count_outstanding_messages_async()
    {
        //Arrange

        //Act
        for (var i = 0; i < POST_COUNT; i++)
        {
            await _commandProcessor.PostAsync(new MyCommand { Value = $"Hello World: {i}" });
        }

        await WaitForBackgroundCountsToHaveRun();

        //Assert
        Assert.Equal(0, _outbox.OutstandingCountCalls);
    }

    /// <summary>
    /// The count runs on a background thread, so "no calls yet" proves nothing on its own. A post to a mediator that
    /// does have a limit queues its count after ours, and we wait for it and a short settle time, so a count queued
    /// by the posts above has had time to run by the time we assert. This guards against a false pass because we
    /// asserted before the background work started.
    /// </summary>
    private async Task WaitForBackgroundCountsToHaveRun()
    {
        _controlCommandProcessor.Post(new MyCommand { Value = "Control" });

        Assert.True(
            await _controlOutbox.WaitForOutstandingCountCallsAsync(1, TimeSpan.FromSeconds(5)),
            "The control mediator never counted, so the background work is not running");

        await Task.Delay(250);
    }
}
