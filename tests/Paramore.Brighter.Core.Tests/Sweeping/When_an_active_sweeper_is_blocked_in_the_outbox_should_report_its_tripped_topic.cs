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
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Extensions.Diagnostics;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class BlockedActiveSweeperTrippedTopicsTests
{
    private const string InvestigateCheck = "brighter-outbox-sweeper-investigate";
    private const string TrippedTopicsGauge = "paramore.brighter.outbox_sweeper.tripped_topics";

    [Fact]
    public async Task When_an_active_sweeper_is_blocked_in_the_outbox_should_report_its_tripped_topic()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        var breaker = new InMemoryOutboxCircuitBreaker();
        breaker.TripTopic(new RoutingKey("orders"));
        using var mediator = new GatedOutboxProducerMediator().CompleteFirst(1);
        var services = SweeperContainer.Create(new InMemoryLock(), timeProvider, useOutboxSweeper: true,
            configureSweeper: options => options.TimerInterval = 10);
        services.AddSingleton<IAmAnOutboxProducerMediator>(mediator);
        services.AddSingleton<IAmAnOutboxCircuitBreaker>(breaker);
        services.AddLogging();
        services.AddMetrics();
        services.AddHealthChecks().AddBrighterOutboxSweeperHealthChecks();
        services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddBrighterInstrumentation());
        using var provider = services.BuildServiceProvider();
        using var meterProvider = provider.GetRequiredService<MeterProvider>();
        using var trippedTopics = new TrippedTopicsListener(provider.GetRequiredService<IMeterFactory>());
        var state = provider.GetRequiredService<OutboxSweeperState>();
        var healthChecks = provider.GetRequiredService<HealthCheckService>();
        var sweeper = provider.GetServices<IHostedService>().OfType<TimedOutboxSweeper>().Single();

        try
        {
            await sweeper.StartAsync(CancellationToken.None);
            var firstCompleted = SpinWait.SpinUntil(
                () => state.Read().LastOutcome == SweepOutcome.Completed && !state.Read().IsSweepInProgress, TimeSpan.FromSeconds(10));
            timeProvider.Advance(TimeSpan.FromSeconds(10));
            var reachedMediator = mediator.WaitForSweepToStart(TimeSpan.FromSeconds(10));

            //Act
            var report = await Task.Run(
                    () => healthChecks.CheckHealthAsync(registration => registration.Name == InvestigateCheck, CancellationToken.None))
                .WaitAsync(TimeSpan.FromSeconds(1));
            var gauge = await Task.Run(() =>
            {
                trippedTopics.Observe();
                return trippedTopics.Values.Last();
            }).WaitAsync(TimeSpan.FromSeconds(1));

            //Assert
            Assert.True(firstCompleted, "The previous sweep did not complete");
            Assert.True(reachedMediator, "The current sweep did not reach the mediator");
            var result = report.Entries[InvestigateCheck];
            Assert.Equal(HealthStatus.Degraded, result.Status);
            Assert.Equal(new[] { "tripped_topics" }, Assert.IsType<string[]>(result.Data[RemedyCheckConvention.ConditionsKey]));
            Assert.Contains("orders", result.Description);
            Assert.Equal(1L, gauge);
        }
        finally
        {
            mediator.Open();
            await sweeper.StopAsync(new CancellationToken(canceled: true));
        }
    }

    private sealed class TrippedTopicsListener : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly List<long> _values = [];

        public TrippedTopicsListener(IMeterFactory meterFactory)
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (!ReferenceEquals(instrument.Meter.Scope, meterFactory) || instrument.Name != TrippedTopicsGauge) return;
                listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<long>((_, value, _, _) => _values.Add(value));
            _listener.Start();
        }

        public IReadOnlyList<long> Values => _values;

        public void Observe() => _listener.RecordObservableInstruments();

        public void Dispose() => _listener.Dispose();
    }
}
