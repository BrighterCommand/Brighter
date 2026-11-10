using System;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.CircuitBreaker;

public class MsSqlTrippedTopicStoreDuplicateTopicWriteTests : MsSqlTrippedTopicStoreTestBase
{
    private static readonly DateTimeOffset s_firstStamp = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task When_a_write_repeats_a_topic_should_reject_it_and_change_nothing()
    {
        // Arrange
        var orders = new RoutingKey($"orders-{Uuid.NewAsString()}");
        var invoices = new RoutingKey($"invoices-{Uuid.NewAsString()}");
        var stamp = s_firstStamp;

        // Act
        var repeatsATopic = async () => await Store.WriteAsync(
            [new TopicTrip(orders, stamp), new TopicTrip(invoices, stamp), new TopicTrip(orders, stamp)]);
        await Assert.ThrowsAsync<ArgumentException>(repeatsATopic);
        var afterRejectedWrite = await Store.ReadLiveAsync(stamp.AddSeconds(1), TimeSpan.FromHours(1));
        var results = await Store.WriteAsync([new TopicTrip(orders, stamp), new TopicTrip(invoices, stamp)]);

        // Assert
        Assert.DoesNotContain(afterRejectedWrite, trip => trip.Topic == orders || trip.Topic == invoices);
        Assert.Equal(2, results.Count);
        Assert.Contains(results, result => result.Topic == orders);
        Assert.Contains(results, result => result.Topic == invoices);
    }
}
