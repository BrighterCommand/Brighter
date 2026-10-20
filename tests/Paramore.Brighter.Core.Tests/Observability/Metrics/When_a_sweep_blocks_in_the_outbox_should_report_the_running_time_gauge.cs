using System;
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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.Metrics;

public class SweeperRunningTimeGaugeTests
{
    private const string RunningTime = "paramore.brighter.outbox_sweeper.sweep.running_time";

    [Fact]
    public async Task When_a_sweep_blocks_in_the_outbox_should_report_the_running_time_gauge()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        var state = new OutboxSweeperState(timeProvider);
        using var mediator = new GatedOutboxProducerMediator();
        var services = new ServiceCollection();
        services.AddSingleton<IAmAnOutboxProducerMediator>(mediator);
        using var provider = services.BuildServiceProvider();
        using var metrics = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var meterFactory = metrics.GetRequiredService<IMeterFactory>();
        var meter = new SweeperMeter(meterFactory, state);
        using var recorded = new RunningTimeListener(meterFactory);
        var sweeper = new TimedOutboxSweeper(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new InMemoryLock(),
            new TimedOutboxSweeperOptions { TimerInterval = 60, MinimumMessageAge = TimeSpan.Zero },
            timeProvider: timeProvider,
            state: state);

        try
        {
            await sweeper.StartAsync(CancellationToken.None);
            var reachedMediator = mediator.WaitForSweepToStart(TimeSpan.FromSeconds(10));
            timeProvider.Advance(TimeSpan.FromSeconds(25));

            //Act
            await Task.Run(recorded.Observe).WaitAsync(TimeSpan.FromSeconds(1));

            //Assert
            Assert.True(reachedMediator, "The sweep did not reach the mediator");
            Assert.Equal([25.0], recorded.Values);
            Assert.Equal("s", recorded.Unit);
            Assert.Equal(0, recorded.TagCount);
        }
        finally
        {
            var stopping = sweeper.StopAsync(CancellationToken.None);
            mediator.Open();
            await stopping.WaitAsync(TimeSpan.FromSeconds(10));
            sweeper.Dispose();
        }
    }

    [Fact]
    public void When_a_meter_has_no_sweeper_state_should_report_a_running_time_of_zero()
    {
        //Arrange
        using var metrics = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var meterFactory = metrics.GetRequiredService<IMeterFactory>();
        var meter = new SweeperMeter(meterFactory, state: null);
        using var recorded = new RunningTimeListener(meterFactory);

        //Act
        recorded.Observe();

        //Assert
        Assert.Equal([0.0], recorded.Values);
    }

    private sealed class RunningTimeListener : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly List<double> _values = [];

        public RunningTimeListener(IMeterFactory meterFactory)
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (!ReferenceEquals(instrument.Meter.Scope, meterFactory) || instrument.Name != RunningTime) return;
                Unit = instrument.Unit;
                listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<double>((_, value, tags, _) =>
            {
                TagCount = tags.Length;
                _values.Add(value);
            });
            _listener.Start();
        }

        public string? Unit { get; private set; }
        public int TagCount { get; private set; }
        public IReadOnlyList<double> Values => _values;

        public void Observe() => _listener.RecordObservableInstruments();

        public void Dispose() => _listener.Dispose();
    }
}
