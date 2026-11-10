using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.Base.Test.CircuitBreaker;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.MSSQL.Tests.TestDoubles;
using Paramore.Brighter.Outbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.CircuitBreaker;

public class MsSqlTrippedTopicStoreEndExpiredDeadlockTests : MsSqlTrippedTopicStoreTestBase
{
    private static readonly DateTimeOffset s_firstStamp = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task When_the_mssql_trip_store_end_expired_is_chosen_as_a_deadlock_victim_should_retry_once_and_succeed()
    {
        // Arrange
        var first = new RoutingKey("a");
        var last = new RoutingKey("z");
        await Store.WriteAsync([new TopicTrip(first, s_firstStamp)]);
        await Store.WriteAsync([new TopicTrip(last, s_firstStamp.AddSeconds(1))]);

        var recordingProvider = new RecordingConnectionProvider(ConnectionProvider);
        var store = new MsSqlTrippedTopicStore(recordingProvider, TableName, commandTimeout: TimeSpan.FromSeconds(30));
        await using var holder = await MsSqlDeadlockRowHolder.HoldAsync(ConnectionProvider, TableName, last.Value);

        // Act
        var endExpired = store.EndExpiredAsync(s_firstStamp.AddHours(1), TimeSpan.FromMinutes(1));
        await holder.WaitUntilBlockedAsync();
        await holder.TouchAsync(first.Value);
        await holder.CommitAsync();
        var ended = await endExpired;

        // Assert
        Assert.Equal(2, recordingProvider.ExecutedCommands.Count);
        Assert.Equal([first, last], ended.Select(trip => trip.Topic).OrderBy(topic => topic.Value));
    }
}
