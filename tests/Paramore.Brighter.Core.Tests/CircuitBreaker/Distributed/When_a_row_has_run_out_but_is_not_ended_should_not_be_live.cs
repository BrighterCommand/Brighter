using System;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class InMemoryTrippedTopicStoreRunOutRowTests
{
    private InMemoryTrippedTopicStore Store { get; } = new();

    private static readonly DateTimeOffset s_now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_tripLength = TimeSpan.FromSeconds(50);

    [Fact]
    public async Task When_a_row_has_run_out_but_is_not_ended_should_not_be_live()
    {
        // Arrange
        var topic = new RoutingKey($"expired-{Uuid.NewAsString()}");
        await Store.WriteAsync([new TopicTrip(topic, s_now - s_tripLength - TimeSpan.FromSeconds(10))]);

        // Act
        var live = await Store.ReadLiveAsync(s_now, s_tripLength);
        var ended = await Store.EndExpiredAsync(s_now, s_tripLength);
        var endedAgain = await Store.EndExpiredAsync(s_now, s_tripLength);

        // Assert
        Assert.Empty(live);
        Assert.Equal(topic, Assert.Single(ended).Topic);
        Assert.Empty(endedAgain);
    }
}
