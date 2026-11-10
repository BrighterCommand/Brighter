using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.Base.Test.CircuitBreaker;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.MySQL.Tests.TestDoubles;
using Paramore.Brighter.Outbox.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.CircuitBreaker;

public class MySqlTrippedTopicStoreWriteDeadlockTests : MySqlTrippedTopicStoreTestBase
{
    private static readonly DateTimeOffset s_firstStamp = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task When_the_mysql_trip_store_write_is_chosen_as_a_deadlock_victim_should_retry_once_and_succeed()
    {
        // Arrange
        var first = new RoutingKey("a");
        var last = new RoutingKey("z");
        var laterStamp = s_firstStamp.AddSeconds(10);
        await Store.WriteAsync([new TopicTrip(first, s_firstStamp), new TopicTrip(last, s_firstStamp)]);

        var recordingProvider = new RecordingConnectionProvider(ConnectionProvider);
        var store = new MySqlTrippedTopicStore(recordingProvider, TableName, commandTimeout: TimeSpan.FromSeconds(30));
        await using var holder = await MySqlDeadlockRowHolder.HoldAsync(ConnectionProvider, TableName, last.Value);

        // Act
        var write = store.WriteAsync([new TopicTrip(first, laterStamp), new TopicTrip(last, laterStamp)]);
        await holder.WaitUntilBlockedAsync();
        await holder.TouchAsync(first.Value);
        await holder.CommitAsync();
        var results = await write;
        var live = await Store.ReadLiveAsync(laterStamp.AddSeconds(1), TimeSpan.FromHours(1));

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal(2, recordingProvider.ExecutedCommands.Count(command => command.CommandText.Contains("ON DUPLICATE KEY UPDATE")));
        Assert.Equal(laterStamp, Assert.Single(live, trip => trip.Topic == first).TripTime);
        Assert.Equal(laterStamp, Assert.Single(live, trip => trip.Topic == last).TripTime);
    }
}
