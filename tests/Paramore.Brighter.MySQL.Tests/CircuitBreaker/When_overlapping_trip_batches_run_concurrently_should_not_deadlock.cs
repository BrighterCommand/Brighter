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
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.CircuitBreaker;

public class MySqlTrippedTopicStoreOverlappingBatchTests : MySqlTrippedTopicStoreTestBase
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
