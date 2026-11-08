using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.Base.Test.CircuitBreaker;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.MSSQL.Tests.TestDoubles;
using Paramore.Brighter.Outbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.CircuitBreaker;

public class MsSqlTrippedTopicStoreWriteDeadlockTests : MsSqlTrippedTopicStoreTestBase
{
    private static readonly DateTimeOffset s_firstStamp = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task When_the_mssql_trip_store_write_is_chosen_as_a_deadlock_victim_should_retry_once_and_succeed()
    {
        // Arrange
        var first = new RoutingKey("a");
        var last = new RoutingKey("z");
        var laterStamp = s_firstStamp.AddSeconds(10);
        await Store.WriteAsync([new TopicTrip(first, s_firstStamp), new TopicTrip(last, s_firstStamp)]);

        var recordingProvider = new RecordingConnectionProvider(ConnectionProvider);
        var store = new MsSqlTrippedTopicStore(recordingProvider, TableName, commandTimeout: TimeSpan.FromSeconds(30));
        await using var holder = await DeadlockPriorityRowHolder.HoldAsync(ConnectionProvider, TableName, last.Value);

        // Act
        var write = store.WriteAsync([new TopicTrip(first, laterStamp), new TopicTrip(last, laterStamp)]);
        await holder.WaitUntilBlockedAsync();
        var closeTheCycle = holder.TouchAsync(first.Value);
        await closeTheCycle;
        await holder.CommitAsync();
        var results = await write;
        var read = await Store.ReadAsync(laterStamp.AddSeconds(1), TimeSpan.FromHours(1));

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal(2, recordingProvider.ExecutedCommands.Count);
        Assert.Equal(laterStamp, Assert.Single(read.Live, trip => trip.Topic == first).TripTime);
        Assert.Equal(laterStamp, Assert.Single(read.Live, trip => trip.Topic == last).TripTime);
    }
}
