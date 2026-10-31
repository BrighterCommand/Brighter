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
using Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;
using Paramore.Brighter.Observability;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class StaleStampedWriteTests
{
    [Fact]
    public async Task When_a_write_stamp_has_already_run_out_should_not_write_and_drop_the_topic_silently()
    {
        using (TestCorrelator.CreateContext())
        {
            // Arrange
            var exportedActivities = new List<Activity>();
            using var traceProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource("Paramore.Brighter")
                .ConfigureResource(r => r.AddService("stale-write-tracer"))
                .AddInMemoryExporter(exportedActivities)
                .Build();
            var orders = new RoutingKey("orders");
            var store = new CountingTrippedTopicStore();
            var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
            var breaker = new DistributedOutboxCircuitBreaker(
                store,
                new OutboxCircuitBreakerOptions { CooldownDuration = TimeSpan.FromSeconds(50), ClockSkewLeeway = TimeSpan.Zero },
                new BrighterTracer(),
                timeProvider: clock);
            var batch = breaker.BeginTripBatch();
            batch.TripTopic(orders);
            clock.Advance(TimeSpan.FromSeconds(50));

            // Act
            await batch.WriteAsync();

            // Assert
            traceProvider.ForceFlush();
            Assert.Empty(store.Writes);
            Assert.Empty(exportedActivities);
            Assert.Empty(TestCorrelator.GetLogEventsFromCurrentContext());
            Assert.Empty(breaker.TrippedTopics);
        }
    }
}
