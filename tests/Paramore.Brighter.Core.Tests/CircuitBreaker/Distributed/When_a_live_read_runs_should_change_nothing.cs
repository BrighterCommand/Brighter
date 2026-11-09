using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class InMemoryTrippedTopicStoreLiveReadTests
{
    private InMemoryTrippedTopicStore Store { get; } = new();

    private static readonly DateTimeOffset s_now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_tripLength = TimeSpan.FromSeconds(50);

    [Fact]
    public async Task When_a_live_read_runs_should_change_nothing()
    {
        // Arrange
        var expiredTopic = new RoutingKey($"expired-{Uuid.NewAsString()}");
        var liveTopic = new RoutingKey($"live-{Uuid.NewAsString()}");
        var liveStamp = s_now - s_tripLength + TimeSpan.FromSeconds(1);
        await Store.WriteAsync([new TopicTrip(expiredTopic, s_now - s_tripLength), new TopicTrip(liveTopic, liveStamp)]);

        // Act
        var firstRead = await Store.ReadLiveAsync(s_now, s_tripLength);
        var secondRead = await Store.ReadLiveAsync(s_now, s_tripLength);
        var ended = await Store.EndExpiredAsync(s_now, s_tripLength);

        // Assert
        Assert.Equal([new TopicTrip(liveTopic, liveStamp)], firstRead);
        Assert.Equal(firstRead, secondRead);
        Assert.Equal(expiredTopic, Assert.Single(ended).Topic);
    }
}
