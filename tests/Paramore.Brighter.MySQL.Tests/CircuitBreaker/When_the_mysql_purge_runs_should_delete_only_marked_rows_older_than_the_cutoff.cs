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
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Outbox.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.CircuitBreaker;

public class MySqlTrippedTopicStorePurgeTests : MySqlTrippedTopicStoreTestBase
{
    private static readonly DateTimeOffset s_midday = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_tripLength = TimeSpan.FromSeconds(50);

    [Fact]
    public async Task When_the_mysql_purge_runs_should_delete_only_marked_rows_older_than_the_cutoff()
    {
        // Arrange
        var endedLongAgo = new RoutingKey($"ended-long-ago-{Uuid.NewAsString()}");
        var endedRecently = new RoutingKey($"ended-recently-{Uuid.NewAsString()}");
        var neverEnded = new RoutingKey($"never-ended-{Uuid.NewAsString()}");
        var longAgo = s_midday;
        var recently = s_midday.AddMinutes(30);
        var cutoff = s_midday.AddMinutes(15);

        await Store.WriteAsync([new TopicTrip(endedLongAgo, longAgo)]);
        await Store.EndExpiredAsync(new TripWindow(longAgo.AddMinutes(1), s_tripLength));
        await Store.WriteAsync([new TopicTrip(endedRecently, recently)]);
        await Store.EndExpiredAsync(new TripWindow(recently.AddMinutes(1), s_tripLength));
        await Store.WriteAsync([new TopicTrip(neverEnded, longAgo.AddHours(-1))]);

        // Act
        await PurgeEndedBeforeAsync(cutoff);
        var remaining = await TopicsInTableAsync();

        // Assert
        Assert.Contains("@EndedBefore", MySqlTrippedTopicStore.GetPurgeSql(TableName));
        Assert.DoesNotContain(endedLongAgo.Value, remaining);
        Assert.Contains(endedRecently.Value, remaining);
        Assert.Contains(neverEnded.Value, remaining);
    }

    private async Task PurgeEndedBeforeAsync(DateTimeOffset endedBefore)
    {
        await using var connection = await ConnectionProvider.GetConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = MySqlTrippedTopicStore.GetPurgeSql(TableName);
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@EndedBefore";
        parameter.DbType = DbType.DateTime;
        parameter.Value = endedBefore.UtcDateTime;
        command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<List<string>> TopicsInTableAsync()
    {
        await using var connection = await ConnectionProvider.GetConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT `Topic` FROM `{TableName}`";
        await using var reader = await command.ExecuteReaderAsync();
        var topics = new List<string>();
        while (await reader.ReadAsync()) topics.Add(reader.GetString(0));
        return topics;
    }
}
