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

namespace Paramore.Brighter.MySQL.Tests.CircuitBreaker;

public class MySqlSharedTripTableTests : MySqlProvisionedTripTableTestBase
{
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly RoutingKey s_orders = new("orders");

    private readonly List<Activity> _activities = [];

    [Fact]
    public async Task When_two_breakers_share_a_trip_table_should_share_end_and_survive_restart()
    {
        // Arrange
        await ProvisionTripTableAsync();
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource("Paramore.Brighter")
            .ConfigureResource(r => r.AddService("mysql-shared-trips-tracer"))
            .AddInMemoryExporter(_activities)
            .Build();
        var clock = new FakeTimeProvider(s_start);
        var breakerA = NewBreaker(clock);
        var breakerB = NewBreaker(clock);

        // Act
        var batch = breakerA.BeginTripBatch();
        batch.TripTopic(s_orders);
        await batch.WriteAsync();

        clock.Advance(TimeSpan.FromSeconds(10));
        var seenByB = await breakerB.ReadTrippedTopicsAsync();
        var recreated = NewBreaker(clock);
        var seenByRecreated = await recreated.ReadTrippedTopicsAsync();

        clock.Advance(TimeSpan.FromSeconds(40));
        var afterCooldownByB = await breakerB.ReadTrippedTopicsAsync();
        var afterCooldownByA = await breakerA.ReadTrippedTopicsAsync();
        var afterCooldownByRecreated = await recreated.ReadTrippedTopicsAsync();
        provider.ForceFlush();

        // Assert
        Assert.Contains(s_orders, seenByB);
        Assert.Contains(s_orders, seenByRecreated);
        Assert.DoesNotContain(s_orders, afterCooldownByB);
        Assert.DoesNotContain(s_orders, afterCooldownByA);
        Assert.DoesNotContain(s_orders, afterCooldownByRecreated);
        Assert.Equal(1, SpanCount("orders reset"));
    }

    private DistributedOutboxCircuitBreaker NewBreaker(TimeProvider clock) =>
        new(NewStore(), tracer: new BrighterTracer(), timeProvider: clock);

    private int SpanCount(string name)
    {
        lock (_activities)
            return _activities.Count(a => a.DisplayName == name);
    }
}
