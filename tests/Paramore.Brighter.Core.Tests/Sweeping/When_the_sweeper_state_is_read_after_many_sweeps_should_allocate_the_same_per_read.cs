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
using System.Diagnostics.Metrics;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Paramore.Brighter.Outbox.Hosting.HealthChecks;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class ReadCostAfterManySweepsTests
{
    private const int FewSweeps = 10;
    private const int ManySweeps = 10_000;
    private const int Reads = 10_000;
    private const int WarmUpReads = 1_000;

    [Fact]
    public void When_the_sweeper_state_is_read_after_many_sweeps_should_allocate_the_same_per_read()
    {
        //Arrange
        var afterFew = StateAfter(FewSweeps);
        var afterMany = StateAfter(ManySweeps);
        var breaker = new InMemoryOutboxCircuitBreaker();
        using var gaugeAfterFew = new GaugeReader(afterFew, breaker);
        using var gaugeAfterMany = new GaugeReader(afterMany, breaker);
        var checkAfterFew = new OutboxSweeperInvestigateHealthCheck(afterFew, new TimedOutboxSweeperOptions(), () => breaker, 3);
        var checkAfterMany = new OutboxSweeperInvestigateHealthCheck(afterMany, new TimedOutboxSweeperOptions(), () => breaker, 3);

        //Act
        var gaugeBytesAfterFew = BytesPerRead(gaugeAfterFew.Observe);
        var gaugeBytesAfterMany = BytesPerRead(gaugeAfterMany.Observe);
        var checkBytesAfterFew = BytesPerRead(() => RunCheck(checkAfterFew));
        var checkBytesAfterMany = BytesPerRead(() => RunCheck(checkAfterMany));

        //Assert
        Assert.True(gaugeAfterFew.Observed > 0 && gaugeAfterMany.Observed > 0, "The gauges reported nothing");
        Assert.Equal(gaugeBytesAfterFew, gaugeBytesAfterMany);
        Assert.Equal(checkBytesAfterFew, checkBytesAfterMany);
    }

    private static OutboxSweeperState StateAfter(int sweeps)
    {
        var state = new OutboxSweeperState(new FakeTimeProvider());
        for (var sweep = 0; sweep < sweeps; sweep++)
        {
            state.SweepStarted();
            state.LockObtained();
            state.SweepEnded(SweepOutcome.Completed);
        }

        return state;
    }

    private static void RunCheck(OutboxSweeperInvestigateHealthCheck check) =>
        check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None).GetAwaiter().GetResult();

    private static long BytesPerRead(Action read)
    {
        for (var i = 0; i < WarmUpReads; i++) read();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Reads; i++) read();
        var after = GC.GetAllocatedBytesForCurrentThread();

        return after - before;
    }

    private sealed class GaugeReader : IDisposable
    {
        private readonly ServiceProvider _services = new ServiceCollection().AddMetrics().BuildServiceProvider();
        private readonly MeterListener _listener = new();
        private readonly SweeperMeter _meter;
        private long _observed;

        public GaugeReader(OutboxSweeperState state, IAmAnOutboxCircuitBreaker breaker)
        {
            var meterFactory = _services.GetRequiredService<IMeterFactory>();
            _meter = new SweeperMeter(meterFactory, state, () => breaker);
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.IsObservable && ReferenceEquals(instrument.Meter.Scope, meterFactory))
                    listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<double>((_, _, _, _) => _observed++);
            _listener.SetMeasurementEventCallback<long>((_, _, _, _) => _observed++);
            _listener.Start();
        }

        public long Observed => Volatile.Read(ref _observed);

        public void Observe() => _listener.RecordObservableInstruments();

        public void Dispose()
        {
            _listener.Dispose();
            _services.Dispose();
        }
    }
}
