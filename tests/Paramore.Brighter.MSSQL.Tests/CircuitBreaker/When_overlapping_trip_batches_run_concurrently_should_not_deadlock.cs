using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.CircuitBreaker;

public class MsSqlTrippedTopicStoreOverlappingBatchTests : MsSqlTrippedTopicStoreTestBase
{
    private const int REPETITIONS = 200;
    private const int TOPICS = 6;
    private const int WRITERS = 4;

    private static readonly DateTimeOffset s_start = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_tripLength = TimeSpan.FromSeconds(50);

    [Fact]
    public async Task When_overlapping_trip_batches_run_concurrently_should_not_deadlock()
    {
        for (var repetition = 0; repetition < REPETITIONS; repetition++)
        {
            // Arrange
            var topics = Enumerable.Range(0, TOPICS).Select(i => new RoutingKey($"topic-{i}-{Uuid.NewAsString()}")).ToArray();
            var latestStamp = s_start.AddSeconds(WRITERS - 1);

            // Act
            var calls = new List<Task>();
            for (var writer = 0; writer < WRITERS; writer++)
            {
                var inSuppliedOrder = writer % 2 == 0 ? topics : topics.Reverse().ToArray();
                var stamp = s_start.AddSeconds(writer);
                calls.Add(Task.Run(() => Store.WriteAsync(inSuppliedOrder.Select(topic => new TopicTrip(topic, stamp)).ToList())));
            }
            calls.Add(Task.Run(() => Store.EndExpiredAsync(new TripWindow(s_start, s_tripLength))));
            calls.Add(Task.Run(() => Store.ReadLiveAsync(new TripWindow(s_start, s_tripLength))));
            await Task.WhenAll(calls);
            var live = await Store.ReadLiveAsync(new TripWindow(s_start, s_tripLength));

            // Assert
            Assert.All(topics, topic => Assert.Equal(latestStamp, Assert.Single(live, trip => trip.Topic == topic).TripTime));
        }
    }
}
