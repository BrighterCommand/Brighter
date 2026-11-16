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
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class InMemoryTrippedTopicStoreLiveReadTests
{
    private InMemoryTrippedTopicStore Store { get; } = new();

    private static readonly DateTimeOffset s_now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_tripLength = TimeSpan.FromSeconds(50);

    [Fact]
    public async Task When_a_live_read_runs_should_change_nothing()
    {
        // Arrange
        var expiredTopic = new RoutingKey($"expired-{Uuid.NewAsString()}");
        var liveTopic = new RoutingKey($"live-{Uuid.NewAsString()}");
        var liveStamp = s_now - s_tripLength + TimeSpan.FromSeconds(1);
        await Store.WriteAsync([new TopicTrip(expiredTopic, s_now - s_tripLength), new TopicTrip(liveTopic, liveStamp)]);

        // Act
        var firstRead = await Store.ReadLiveAsync(new TripWindow(s_now, s_tripLength));
        var secondRead = await Store.ReadLiveAsync(new TripWindow(s_now, s_tripLength));
        var ended = await Store.EndExpiredAsync(new TripWindow(s_now, s_tripLength));

        // Assert
        Assert.Equal([new TopicTrip(liveTopic, liveStamp)], firstRead);
        Assert.Equal(firstRead, secondRead);
        Assert.Equal(expiredTopic, Assert.Single(ended).Topic);
    }
}
