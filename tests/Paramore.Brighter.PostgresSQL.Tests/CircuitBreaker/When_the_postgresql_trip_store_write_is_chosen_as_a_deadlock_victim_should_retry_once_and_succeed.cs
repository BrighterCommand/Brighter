#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.Base.Test.CircuitBreaker;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Outbox.PostgreSql;
using Paramore.Brighter.PostgresSQL.Tests.TestDoubles;
using Paramore.Brighter.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.CircuitBreaker;

public class PostgreSqlTrippedTopicStoreWriteDeadlockTests : PostgreSqlTrippedTopicStoreTestBase
{
    private static readonly DateTimeOffset s_firstStamp = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task When_the_postgresql_trip_store_write_is_chosen_as_a_deadlock_victim_should_retry_once_and_succeed()
    {
        // Arrange
        var first = new RoutingKey("a");
        var last = new RoutingKey("z");
        var laterStamp = s_firstStamp.AddSeconds(10);
        await Store.WriteAsync([new TopicTrip(first, s_firstStamp), new TopicTrip(last, s_firstStamp)]);

        var recordingProvider = new RecordingConnectionProvider(ConnectionProvider);
        var store = new PostgreSqlTrippedTopicStore(recordingProvider, TableName, commandTimeout: TimeSpan.FromSeconds(30));
        await using var holder = await PostgreSqlDeadlockRowHolder.HoldAsync(ConnectionProvider, PgIdentifier.Quote(TableName), last.Value);

        // Act
        var write = store.WriteAsync([new TopicTrip(first, laterStamp), new TopicTrip(last, laterStamp)]);
        await holder.WaitUntilBlockedAsync();
        await holder.TouchAsync(first.Value);
        await holder.CommitAsync();
        var results = await write;
        var live = await Store.ReadLiveAsync(new TripWindow(laterStamp.AddSeconds(1), TimeSpan.FromHours(1)));

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal(2, recordingProvider.ExecutedCommands.Count);
        Assert.Equal(laterStamp, Assert.Single(live, trip => trip.Topic == first).TripTime);
        Assert.Equal(laterStamp, Assert.Single(live, trip => trip.Topic == last).TripTime);
    }
}
