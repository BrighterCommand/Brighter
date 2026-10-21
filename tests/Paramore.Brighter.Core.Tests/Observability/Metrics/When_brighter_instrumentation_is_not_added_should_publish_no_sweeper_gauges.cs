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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.Metrics;

public class UninstrumentedSweeperGaugeTests
{
    private const string RunningTime = "paramore.brighter.outbox_sweeper.sweep.running_time";
    private const string TrippedTopics = "paramore.brighter.outbox_sweeper.tripped_topics";

    [Fact]
    public async Task When_brighter_instrumentation_is_not_added_should_publish_no_sweeper_gauges()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        var services = SweeperContainer.Create(new InMemoryLock(), timeProvider, useOutboxSweeper: true);
        services.AddSingleton<IAmAnOutboxProducerMediator>(new SwitchableOutboxProducerMediator());
        using var provider = services.BuildServiceProvider();
        var meterFactory = provider.GetService<IMeterFactory>();
        var published = new List<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (meterFactory is null || !ReferenceEquals(instrument.Meter.Scope, meterFactory)) return;
            if (instrument.Name != RunningTime && instrument.Name != TrippedTopics) return;
            published.Add(instrument.Name);
            meterListener.EnableMeasurementEvents(instrument);
        };
        listener.Start();
        var state = provider.GetRequiredService<OutboxSweeperState>();
        var sweeper = provider.GetServices<IHostedService>().OfType<TimedOutboxSweeper>().Single();

        try
        {
            //Act
            await sweeper.StartAsync(CancellationToken.None);
            var swept = await WaitForCompletedSweep(state, TimeSpan.FromSeconds(10));
            listener.RecordObservableInstruments();

            //Assert
            Assert.True(swept, "No sweep completed");
            Assert.Empty(published);
        }
        finally
        {
            await sweeper.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    private static async Task<bool> WaitForCompletedSweep(OutboxSweeperState state, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (state.Read().LastOutcome == SweepOutcome.Completed) return true;
            await Task.Delay(10);
        }

        return false;
    }
}
