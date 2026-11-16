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

namespace Paramore.Brighter.MSSQL.Tests.CircuitBreaker;

public class MsSqlTrippedTopicStoreMicrosecondStampTests : MsSqlTrippedTopicStoreTestBase
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
        var liveTrips = await Store.ReadLiveAsync(new TripWindow(microsecondStamp.AddSeconds(1), TimeSpan.FromHours(1)));

        // Assert
        Assert.Equal(microsecondStamp, written.StoredTripTime);
        var live = Assert.Single(liveTrips, trip => trip.Topic == topic);
        Assert.Equal(microsecondStamp, live.TripTime);
    }
}
