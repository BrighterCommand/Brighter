#region License

/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class AlternatingLockHolderTripEndTests
{
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_sweepInterval = TimeSpan.FromSeconds(10);
    private static readonly RoutingKey s_orders = new("orders");

    [Fact]
    public async Task When_instances_alternate_the_lock_should_end_the_trip_at_the_cooldown_duration_with_one_reset()
    {
        // Arrange
        var run = await SweepAlternatingAsync(firstSweeper: 0);

        // Assert
        Assert.Equal([true, true, true, true, false, false, false], run.ExcludedAtEachSweep);
        Assert.Equal(1, run.SpanCount("orders reset"));
        Assert.Equal(1, run.SpanCount("orders trip"));
        Assert.Equal(0, run.SpanCount("orders re_trip"));
    }

    [Fact]
    public async Task When_the_other_instance_holds_the_lock_at_the_end_should_still_announce_one_reset()
    {
        // Arrange
        var run = await SweepAlternatingAsync(firstSweeper: 1);

        // Assert
        Assert.Equal([true, true, true, true, false, false, false], run.ExcludedAtEachSweep);
        Assert.Equal(1, run.SpanCount("orders reset"));
    }

    private static async Task<Run> SweepAlternatingAsync(int firstSweeper)
    {
        var activities = new List<Activity>();
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource("Paramore.Brighter")
            .ConfigureResource(r => r.AddService("alternating-lock-tracer"))
            .AddInMemoryExporter(activities)
            .Build();
        var tracer = new BrighterTracer();
        var store = new InMemoryTrippedTopicStore();
        var clockA = new FakeTimeProvider(s_start);
        var clockB = new FakeTimeProvider(s_start);
        var breakers = new[]
        {
            new DistributedOutboxCircuitBreaker(store, tracer: tracer, timeProvider: clockA),
            new DistributedOutboxCircuitBreaker(store, tracer: tracer, timeProvider: clockB)
        };
        var batch = breakers[0].BeginTripBatch();
        batch.TripTopic(s_orders);
        await batch.WriteAsync();

        var excluded = new List<bool>();
        for (var sweep = 0; sweep < 7; sweep++)
        {
            clockA.Advance(s_sweepInterval);
            clockB.Advance(s_sweepInterval);
            breakers[0].CoolDown();
            breakers[1].CoolDown();
            var sweeper = breakers[(firstSweeper + sweep) % 2];
            var tripped = await sweeper.ReadTrippedTopicsAsync();
            excluded.Add(tripped.Contains(s_orders));
        }

        provider.ForceFlush();
        string[] spanNames;
        lock (activities)
            spanNames = activities.Select(a => a.DisplayName).ToArray();
        return new Run(excluded, spanNames);
    }

    private sealed record Run(List<bool> ExcludedAtEachSweep, string[] SpanNames)
    {
        public int SpanCount(string name) => SpanNames.Count(n => n == name);
    }
}
