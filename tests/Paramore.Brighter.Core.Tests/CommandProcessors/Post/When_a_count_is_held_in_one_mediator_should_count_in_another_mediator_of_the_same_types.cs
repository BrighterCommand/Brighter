using System;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Post;

/// <summary>
/// Each mediator counts its own outstanding messages. A count that does not return in one mediator should not stop
/// another mediator with the same message and transaction types, and its own outbox, from counting.
/// </summary>
public class OutstandingCountAcrossMediatorsTests
{
    private const int LIMIT = 100;
    private static readonly TimeSpan s_deadline = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task When_a_count_is_held_in_one_mediator_should_count_in_another_mediator_of_the_same_types()
    {
        //Arrange
        var heldOutbox = new CountingOutbox(TimeProvider.System);
        var heldCommandProcessor = CountingOutboxCommandProcessorBuilder.Build(heldOutbox, LIMIT, TimeSpan.Zero);

        var outbox = new CountingOutbox(TimeProvider.System);
        var commandProcessor = CountingOutboxCommandProcessorBuilder.Build(outbox, LIMIT, TimeSpan.Zero);

        heldOutbox.HoldCounts();
        try
        {
            heldCommandProcessor.Post(new MyCommand { Value = "Hold this count" });
            Assert.True(await heldOutbox.WaitForCountToStartAsync(s_deadline), "The held count never started");

            //Act
            commandProcessor.Post(new MyCommand { Value = "Hello World" });

            //Assert
            Assert.True(await outbox.WaitForOutstandingCountCallsAsync(1, s_deadline),
                "The other mediator never counted while the first count was held");
        }
        finally
        {
            heldOutbox.ReleaseCounts();
        }
    }
}
