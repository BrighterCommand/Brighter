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

namespace Paramore.Brighter.PostgresSQL.Tests.CircuitBreaker;

public class PostgreSqlConcurrentFirstTripAnnouncementTests : PostgreSqlProvisionedTripTableTestBase
{
    private const int Repetitions = 1000;
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_cooldown = TimeSpan.FromSeconds(50);
    private static readonly TimeSpan s_laterOffset = TimeSpan.FromSeconds(5);
    private static readonly OutboxCircuitBreakerOptions s_options = new() { CooldownDuration = s_cooldown };

    private readonly string _run = Guid.NewGuid().ToString("N");
    private readonly List<Activity> _activities = [];

    [Fact]
    public async Task When_two_breakers_trip_a_topic_concurrently_against_the_database_should_announce_one_trip_and_one_retrip()
    {
        // Arrange
        await ProvisionTripTableAsync();
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource("Paramore.Brighter")
            .ConfigureResource(r => r.AddService("postgresql-concurrent-first-trip-tracer"))
            .AddInMemoryExporter(_activities)
            .Build();
        var tracer = new BrighterTracer();
        var breakerA = NewBreaker(tracer, s_start);
        var breakerB = NewBreaker(tracer, s_start + s_laterOffset);
        var observer = NewStore();
        var topics = Enumerable.Range(0, Repetitions).Select(i => new RoutingKey($"orders-{_run}-{i}")).ToList();
        var later = s_start + s_laterOffset;

        // Act
        foreach (var topic in topics)
        {
            var batchA = breakerA.BeginTripBatch();
            var batchB = breakerB.BeginTripBatch();
            batchA.TripTopic(topic);
            batchB.TripTopic(topic);
            await Task.WhenAll(Task.Run(() => batchA.WriteAsync()), Task.Run(() => batchB.WriteAsync()));
        }

        var liveBeforeEnd = await observer.ReadLiveAsync(new TripWindow(later + s_cooldown - TimeSpan.FromSeconds(0.1), s_cooldown));
        var endedBeforeEnd = await observer.EndExpiredAsync(new TripWindow(later + s_cooldown - TimeSpan.FromSeconds(0.1), s_cooldown));
        var endedAtEnd = await observer.EndExpiredAsync(new TripWindow(later + s_cooldown, s_cooldown));
        var liveAtEnd = await observer.ReadLiveAsync(new TripWindow(later + s_cooldown, s_cooldown));
        provider.ForceFlush();
        string[] spanNames;
        lock (_activities)
            spanNames = _activities.Select(a => a.DisplayName).ToArray();

        // Assert
        Assert.Equal(topics.Select(t => t.Value).Order(), liveBeforeEnd.Select(t => t.Topic.Value).Order());
        Assert.All(liveBeforeEnd, trip => Assert.Equal(later, trip.TripTime));
        Assert.Empty(endedBeforeEnd);
        Assert.Empty(liveAtEnd);
        Assert.Equal(Repetitions, endedAtEnd.Count);
        foreach (var topic in topics)
        {
            Assert.Equal(1, spanNames.Count(n => n == $"{topic} {CircuitBreakerSpanOperation.Trip.ToSpanName()}"));
            Assert.Equal(1, spanNames.Count(n => n == $"{topic} {CircuitBreakerSpanOperation.ReTrip.ToSpanName()}"));
        }
        Assert.Equal(Repetitions * 2, spanNames.Count(n => n.Contains(_run)));
    }

    private DistributedOutboxCircuitBreaker NewBreaker(BrighterTracer tracer, DateTimeOffset clockStart) =>
        new(NewStore(), s_options, tracer, timeProvider: new FakeTimeProvider(clockStart));
}
