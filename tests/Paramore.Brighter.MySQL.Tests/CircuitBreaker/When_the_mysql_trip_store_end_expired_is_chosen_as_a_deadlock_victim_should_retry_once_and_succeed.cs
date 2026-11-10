using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.Base.Test.CircuitBreaker;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.MySQL.Tests.TestDoubles;
using Paramore.Brighter.Outbox.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.CircuitBreaker;

public class MySqlTrippedTopicStoreEndExpiredDeadlockTests : MySqlTrippedTopicStoreTestBase
{
    private static readonly DateTimeOffset s_firstStamp = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task When_the_mysql_trip_store_end_expired_is_chosen_as_a_deadlock_victim_should_retry_once_and_succeed()
    {
        // Arrange
        var first = new RoutingKey("a");
        var last = new RoutingKey("z");
        await Store.WriteAsync([new TopicTrip(first, s_firstStamp)]);
        await Store.WriteAsync([new TopicTrip(last, s_firstStamp.AddSeconds(1))]);

        var recordingProvider = new RecordingConnectionProvider(ConnectionProvider);
        var store = new MySqlTrippedTopicStore(recordingProvider, TableName, commandTimeout: TimeSpan.FromSeconds(30));
        await using var holder = await MySqlDeadlockRowHolder.HoldAsync(ConnectionProvider, TableName, last.Value);

        // Act
        var endExpired = store.EndExpiredAsync(s_firstStamp.AddHours(1), TimeSpan.FromMinutes(1));
        await holder.WaitUntilBlockedAsync();
        await holder.TouchAsync(first.Value);
        await holder.CommitAsync();
        var ended = await endExpired;

        // Assert
        Assert.Equal(2, recordingProvider.ExecutedCommands.Count(command => command.CommandText.EndsWith("FOR UPDATE")));
        Assert.Equal([first, last], ended.Select(trip => trip.Topic).OrderBy(topic => topic.Value));
    }
}
