using System;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.CircuitBreaker;

public class MySqlTrippedTopicStoreMicrosecondStampTests : MySqlTrippedTopicStoreTestBase
{
    private const long TICKS_PER_MICROSECOND = 10;

    private static readonly DateTimeOffset s_firstStamp = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task When_a_microsecond_stamp_is_written_should_read_back_equal()
    {
        // Arrange
        var topic = new RoutingKey($"orders-{Uuid.NewAsString()}");
        var stampWithSubMicrosecondTicks = s_firstStamp.AddTicks(1_234_567);
        var microsecondStamp = stampWithSubMicrosecondTicks.AddTicks(-(stampWithSubMicrosecondTicks.Ticks % TICKS_PER_MICROSECOND));

        // Act
        var written = Assert.Single(await Store.WriteAsync([new TopicTrip(topic, microsecondStamp)]));
        var read = await Store.ReadAsync(microsecondStamp.AddSeconds(1), TimeSpan.FromHours(1));

        // Assert
        Assert.Equal(microsecondStamp, written.StoredTripTime);
        var live = Assert.Single(read.Live, trip => trip.Topic == topic);
        Assert.Equal(microsecondStamp, live.TripTime);
    }
}
