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
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Base.Test.CircuitBreaker;
using Paramore.Brighter.Outbox.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.CircuitBreaker;

public class MySqlTrippedTopicStoreIssuesNoDeleteTests : MySqlTrippedTopicStoreTestBase
{
    private static readonly DateTimeOffset s_now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_tripLength = TimeSpan.FromSeconds(50);

    [Fact]
    public async Task When_the_mysql_trip_store_runs_should_issue_no_delete_command()
    {
        // Arrange
        var recordingProvider = new RecordingConnectionProvider(ConnectionProvider);
        var store = new MySqlTrippedTopicStore(recordingProvider, TableName);
        var topic = new RoutingKey($"orders-{Uuid.NewAsString()}");

        // Act
        await store.WriteAsync([new TopicTrip(topic, s_now - s_tripLength)]);
        var firstEnded = await store.EndExpiredAsync(new TripWindow(s_now, s_tripLength));
        await store.WriteAsync([new TopicTrip(topic, s_now)]);
        await store.EndExpiredAsync(new TripWindow(s_now, s_tripLength));
        await store.ReadLiveAsync(new TripWindow(s_now, s_tripLength));

        // Assert
        Assert.Contains(firstEnded, trip => trip.Topic == topic);
        Assert.NotEmpty(recordingProvider.ExecutedCommands);
        Assert.DoesNotContain(
            recordingProvider.ExecutedCommands,
            command => command.CommandText.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
    }
}
