using System;
using System.Threading.Tasks;
using Paramore.Brighter.Base.Test.CircuitBreaker;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Outbox.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.CircuitBreaker;

public class MySqlTrippedTopicStoreLiveReadTests : MySqlTrippedTopicStoreTestBase
{
    private static readonly DateTimeOffset s_now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_tripLength = TimeSpan.FromSeconds(50);

    [Fact]
    public async Task When_a_live_read_runs_should_change_nothing_and_not_return_an_expired_row()
    {
        // Arrange
        var recordingProvider = new RecordingConnectionProvider(ConnectionProvider);
        var store = new MySqlTrippedTopicStore(recordingProvider, TableName, commandTimeout: TimeSpan.FromSeconds(3));
        var expiredTopic = new RoutingKey($"expired-{Uuid.NewAsString()}");
        var liveTopic = new RoutingKey($"live-{Uuid.NewAsString()}");
        var liveStamp = s_now - s_tripLength + TimeSpan.FromSeconds(1);
        await Store.WriteAsync([new TopicTrip(expiredTopic, s_now - s_tripLength), new TopicTrip(liveTopic, liveStamp)]);

        // Act
        var firstRead = await store.ReadLiveAsync(s_now, s_tripLength);
        var secondRead = await store.ReadLiveAsync(s_now, s_tripLength);
        var ended = await store.EndExpiredAsync(s_now, s_tripLength);

        // Assert
        Assert.Equal([new TopicTrip(liveTopic, liveStamp)], firstRead);
        Assert.Equal(firstRead, secondRead);
        Assert.Equal(expiredTopic, Assert.Single(ended).Topic);
        Assert.NotEmpty(recordingProvider.ExecutedCommands);
        Assert.All(recordingProvider.ExecutedCommands, command => Assert.Equal(3, command.CommandTimeout));
    }
}
