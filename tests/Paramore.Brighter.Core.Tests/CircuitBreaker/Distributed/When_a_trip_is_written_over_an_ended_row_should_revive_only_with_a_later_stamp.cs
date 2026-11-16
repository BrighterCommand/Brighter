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
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class InMemoryTrippedTopicStoreReviveEndedRowTests
{
    private InMemoryTrippedTopicStore Store { get; } = new();

    private static readonly DateTimeOffset s_now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_tripLength = TimeSpan.FromSeconds(50);

    [Fact]
    public async Task When_a_trip_is_written_over_an_ended_row_should_revive_only_with_a_later_stamp()
    {
        // Arrange
        var topic = new RoutingKey($"orders-{Uuid.NewAsString()}");
        var endedStamp = s_now - s_tripLength;
        var laterStamp = endedStamp + TimeSpan.FromSeconds(10);
        await Store.WriteAsync([new TopicTrip(topic, endedStamp)]);
        var marked = await Store.EndExpiredAsync(new TripWindow(s_now, s_tripLength));
        Assert.Contains(marked, trip => trip.Topic == topic);

        // Act
        var equal = Assert.Single(await Store.WriteAsync([new TopicTrip(topic, endedStamp)]));
        var earlier = Assert.Single(await Store.WriteAsync([new TopicTrip(topic, endedStamp - TimeSpan.FromSeconds(10))]));
        var afterEqualAndEarlier = await Store.ReadLiveAsync(new TripWindow(s_now, s_tripLength));
        var later = Assert.Single(await Store.WriteAsync([new TopicTrip(topic, laterStamp)]));
        var afterLater = await Store.ReadLiveAsync(new TripWindow(s_now, s_tripLength));

        // Assert
        Assert.True(equal.LeftEnded);
        Assert.Null(equal.PriorTripTime);
        Assert.True(earlier.LeftEnded);
        Assert.Null(earlier.PriorTripTime);
        Assert.DoesNotContain(afterEqualAndEarlier, trip => trip.Topic == topic);

        Assert.False(later.LeftEnded);
        Assert.Null(later.PriorTripTime);
        var revived = Assert.Single(afterLater, trip => trip.Topic == topic);
        Assert.Equal(laterStamp, revived.TripTime);
    }
}
