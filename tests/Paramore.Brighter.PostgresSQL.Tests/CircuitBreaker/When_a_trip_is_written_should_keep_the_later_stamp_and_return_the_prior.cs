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

namespace Paramore.Brighter.PostgresSQL.Tests.CircuitBreaker;

public class PostgreSqlTrippedTopicStoreKeepsLaterStampTests : PostgreSqlTrippedTopicStoreTestBase
{
    private static readonly DateTimeOffset s_firstStamp = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task When_a_trip_is_written_should_keep_the_later_stamp_and_return_the_prior()
    {
        // Arrange
        var topic = new RoutingKey($"orders-{Uuid.NewAsString()}");
        var firstStamp = s_firstStamp;
        var laterStamp = firstStamp.AddSeconds(10);
        var earlierStamp = firstStamp.AddSeconds(-10);

        // Act
        var first = Assert.Single(await Store.WriteAsync([new TopicTrip(topic, firstStamp)]));
        var later = Assert.Single(await Store.WriteAsync([new TopicTrip(topic, laterStamp)]));
        var earlier = Assert.Single(await Store.WriteAsync([new TopicTrip(topic, earlierStamp)]));
        var live = await Store.ReadLiveAsync(new TripWindow(laterStamp.AddSeconds(1), TimeSpan.FromHours(1)));

        // Assert
        Assert.Null(first.PriorTripTime);
        Assert.Equal(firstStamp, first.StoredTripTime);
        Assert.False(first.LeftEnded);

        Assert.Equal(firstStamp, later.PriorTripTime);
        Assert.Equal(laterStamp, later.StoredTripTime);

        Assert.Equal(laterStamp, earlier.PriorTripTime);
        Assert.Equal(laterStamp, earlier.StoredTripTime);

        var liveTrip = Assert.Single(live, trip => trip.Topic == topic);
        Assert.Equal(laterStamp, liveTrip.TripTime);
    }
}
