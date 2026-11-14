using System;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.CircuitBreaker;

public class PostgreSqlTrippedTopicStoreNeverDeletesTests : PostgreSqlTrippedTopicStoreTestBase
{
    private static readonly DateTimeOffset s_now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_tripLength = TimeSpan.FromSeconds(50);

    [Fact]
    public async Task When_expired_trips_are_ended_should_mark_the_row_and_never_delete_it()
    {
        // Arrange
        var topic = new RoutingKey($"orders-{Uuid.NewAsString()}");
        await Store.WriteAsync([new TopicTrip(topic, s_now - s_tripLength)]);

        // Act
        var ended = await Store.EndExpiredAsync(new TripWindow(s_now, s_tripLength));
        var (rows, markedRows) = await CountRowsAsync();

        // Assert
        Assert.Contains(ended, trip => trip.Topic == topic);
        Assert.Equal(1, rows);
        Assert.Equal(1, markedRows);
    }

    private async Task<(long Rows, long MarkedRows)> CountRowsAsync()
    {
        await using var connection = await ConnectionProvider.GetConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*), COUNT(ended_at) FROM {PgIdentifier.Quote(TableName)}";
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (reader.GetInt64(0), reader.GetInt64(1));
    }
}
