using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Outbox.PostgreSql;
using Paramore.Brighter.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.CircuitBreaker;

public class PostgreSqlTrippedTopicStorePurgeTests : PostgreSqlTrippedTopicStoreTestBase
{
    private static readonly DateTimeOffset s_midday = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_tripLength = TimeSpan.FromSeconds(50);

    [Fact]
    public async Task When_the_postgresql_purge_runs_should_delete_only_marked_rows_older_than_the_cutoff()
    {
        // Arrange
        var endedLongAgo = new RoutingKey($"ended-long-ago-{Uuid.NewAsString()}");
        var endedRecently = new RoutingKey($"ended-recently-{Uuid.NewAsString()}");
        var neverEnded = new RoutingKey($"never-ended-{Uuid.NewAsString()}");
        var longAgo = s_midday;
        var recently = s_midday.AddMinutes(30);
        var cutoff = s_midday.AddMinutes(15);

        await Store.WriteAsync([new TopicTrip(endedLongAgo, longAgo)]);
        await Store.ReadAsync(longAgo.AddMinutes(1), s_tripLength);
        await Store.WriteAsync([new TopicTrip(endedRecently, recently)]);
        await Store.ReadAsync(recently.AddMinutes(1), s_tripLength);
        await Store.WriteAsync([new TopicTrip(neverEnded, longAgo.AddHours(-1))]);

        // Act
        await PurgeEndedBeforeAsync(cutoff);
        var remaining = await TopicsInTableAsync();

        // Assert
        Assert.Contains("@EndedBefore", PostgreSqlTrippedTopicStore.GetPurgeSql(TableName));
        Assert.DoesNotContain(endedLongAgo.Value, remaining);
        Assert.Contains(endedRecently.Value, remaining);
        Assert.Contains(neverEnded.Value, remaining);
    }

    private async Task PurgeEndedBeforeAsync(DateTimeOffset endedBefore)
    {
        await using var connection = await ConnectionProvider.GetConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = PostgreSqlTrippedTopicStore.GetPurgeSql(TableName);
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@EndedBefore";
        parameter.DbType = DbType.DateTimeOffset;
        parameter.Value = endedBefore;
        command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<List<string>> TopicsInTableAsync()
    {
        await using var connection = await ConnectionProvider.GetConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT topic FROM {PgIdentifier.Quote(TableName)}";
        await using var reader = await command.ExecuteReaderAsync();
        var topics = new List<string>();
        while (await reader.ReadAsync()) topics.Add(reader.GetString(0));
        return topics;
    }
}
