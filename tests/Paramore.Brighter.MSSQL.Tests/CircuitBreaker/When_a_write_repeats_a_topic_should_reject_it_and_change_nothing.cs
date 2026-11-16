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
        var afterRejectedWrite = await Store.ReadLiveAsync(new TripWindow(stamp.AddSeconds(1), TimeSpan.FromHours(1)));
        var results = await Store.WriteAsync([new TopicTrip(orders, stamp), new TopicTrip(invoices, stamp)]);

        // Assert
        Assert.DoesNotContain(afterRejectedWrite, trip => trip.Topic == orders || trip.Topic == invoices);
        Assert.Equal(2, results.Count);
        Assert.Contains(results, result => result.Topic == orders);
        Assert.Contains(results, result => result.Topic == invoices);
    }
}
