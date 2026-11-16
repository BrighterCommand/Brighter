#region Licence

/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Observability;
using Xunit;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class DistributedReTripRestartsCooldownTests
{
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly OutboxCircuitBreakerOptions s_options = new()
    {
        CooldownDuration = TimeSpan.FromSeconds(50),
        ClockSkewLeeway = TimeSpan.Zero
    };

    [Fact]
    public async Task When_another_instance_re_trips_a_topic_should_restart_the_cooldown_everywhere()
    {
        // Arrange
        var clock = new FakeTimeProvider(s_start);
        var store = new InMemoryTrippedTopicStore();
        var breakerA = new DistributedOutboxCircuitBreaker(store, s_options, timeProvider: clock);
        var breakerB = new DistributedOutboxCircuitBreaker(store, s_options, timeProvider: clock);
        var topic = new RoutingKey("orders");

        // Act
        var batchA = breakerA.BeginTripBatch();
        batchA.TripTopic(topic);
        await batchA.WriteAsync();
        clock.Advance(TimeSpan.FromSeconds(30));
        var batchB = breakerB.BeginTripBatch();
        batchB.TripTopic(topic);
        await batchB.WriteAsync();
        clock.Advance(TimeSpan.FromSeconds(49.9));
        var listedOnA = await breakerA.ReadTrippedTopicsAsync();
        var listedOnB = await breakerB.ReadTrippedTopicsAsync();
        var syncOnA = breakerA.TrippedTopics.ToArray();
        var syncOnB = breakerB.TrippedTopics.ToArray();
        clock.Advance(TimeSpan.FromSeconds(0.1));
        var listedOnAAtEnd = await breakerA.ReadTrippedTopicsAsync();
        var listedOnBAtEnd = await breakerB.ReadTrippedTopicsAsync();
        var syncOnAAtEnd = breakerA.TrippedTopics.ToArray();
        var syncOnBAtEnd = breakerB.TrippedTopics.ToArray();

        // Assert
        Assert.Equal([topic], listedOnA);
        Assert.Equal([topic], listedOnB);
        Assert.Equal([topic], syncOnA);
        Assert.Equal([topic], syncOnB);
        Assert.Empty(listedOnAAtEnd);
        Assert.Empty(listedOnBAtEnd);
        Assert.Empty(syncOnAAtEnd);
        Assert.Empty(syncOnBAtEnd);
    }
}
