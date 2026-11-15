using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.Base.Test.CircuitBreaker;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Outbox.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.CircuitBreaker;

public class MySqlTrippedTopicStoreClosedConnectionTests : MySqlTrippedTopicStoreTestBase
{
    private static readonly DateTimeOffset s_now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_tripLength = TimeSpan.FromSeconds(50);

    [Fact]
    public async Task When_the_mysql_trip_store_is_given_a_closed_connection_should_open_it()
    {
        // Arrange
        var store = new MySqlTrippedTopicStore(new ClosedConnectionProvider(ConnectionProvider), TableName);
        var topic = new RoutingKey($"orders-{Uuid.NewAsString()}");

        // Act
        await store.WriteAsync([new TopicTrip(topic, s_now)]);
        var live = await store.ReadLiveAsync(new TripWindow(s_now, s_tripLength));

        // Assert
        Assert.Contains(live, trip => trip.Topic == topic);
    }
}
