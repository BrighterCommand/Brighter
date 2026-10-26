#region Licence
/* The MIT License (MIT)
Copyright © 2026 Guilherme Appel

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
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter.Core.Tests.Observability.StaleMessages.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages;

public class BlockingCancellationCallbackTests
{
    private const string COUNT = "paramore.brighter.outbox.stale.count";
    private const string OUTCOME = "paramore.brighter.outbox.stale.outcome";
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly TimerCountingTimeProvider _clock = new(s_start);

    [Fact]
    public async Task When_a_cancellation_callback_blocks_should_not_hold_the_loop()
    {
        //Arrange
        var outbox = new StaleMessageCountingOutbox(_clock);
        outbox.BlockCancellationCallbackUntilReleased();
        outbox.Returns(3);
        using var metrics = new StaleMessageMetricsFixture(services =>
        {
            services.AddSingleton<TimeProvider>(_clock);
            var builder = new ServiceCollectionBrighterBuilder(
                services,
                new ServiceCollectionSubscriberRegistry(services),
                new ServiceCollectionMessageMapperRegistryBuilder(services));
            builder.AddProducers(configuration =>
            {
                configuration.ProducerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
                {
                    { new RoutingKey("orders"), Producer("orders") }
                });
                configuration.Outbox = outbox;
            });
            builder.UseOutboxStaleMessageMonitor(TimeSpan.FromMinutes(5), options =>
                options.MeasurementInterval = TimeSpan.FromMinutes(1));
        });
        using var monitor = metrics.Services.GetServices<IHostedService>().OfType<TimedOutboxStaleMessageMonitor>().Single();
        var readings = metrics.Services.GetRequiredService<OutboxStaleMessageReadings>();
        await monitor.StartAsync(default);
        WaitUntilTheMonitorWaits(1);
        _clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(SpinWait.SpinUntil(() => outbox.CountCalls.Count == 1, TimeSpan.FromSeconds(5)));

        bool deadlineReturned;
        bool callbackBlocked;
        IReadOnlyList<StaleMeasurement> atTheDeadline;
        bool nextDueTimeReturned;
        bool nextDueTimeHandled;
        bool stillBlocked;

        //Act
        try
        {
            deadlineReturned = AdvanceReturns(TimeSpan.FromSeconds(10));
            callbackBlocked = outbox.CallbackBlocked.Wait(TimeSpan.FromSeconds(5));
            SpinWait.SpinUntil(
                () => readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Abandoned) >= 1,
                TimeSpan.FromSeconds(5));
            atTheDeadline = metrics.Observe();
            SpinWait.SpinUntil(() => _clock.TimersCreated >= 2, TimeSpan.FromSeconds(5));

            nextDueTimeReturned = AdvanceReturns(TimeSpan.FromSeconds(50));
            nextDueTimeHandled = SpinWait.SpinUntil(
                () => readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Skipped) >= 1
                    || outbox.CountCalls.Count >= 2,
                TimeSpan.FromSeconds(5));
            stillBlocked = !outbox.CallEnded.IsCompleted;
        }
        finally
        {
            outbox.ReleaseCallback();
        }

        //Assert
        Assert.True(deadlineReturned);
        Assert.True(callbackBlocked);
        Assert.Equal(1, Measured(atTheDeadline, "overall", "abandoned"));
        Assert.True(nextDueTimeReturned);
        Assert.True(nextDueTimeHandled);
        Assert.True(stillBlocked);
    }

    private static long Measured(IEnumerable<StaleMeasurement> measurements, string count, string outcome) =>
        measurements
            .Where(m => m.Instrument == ObservedStaleMessageInstruments.MEASUREMENTS
                && Equals(m.Attributes[COUNT], count)
                && Equals(m.Attributes[OUTCOME], outcome))
            .Sum(m => m.Value);

    private static InMemoryMessageProducer Producer(string topic) =>
        new(new InternalBus(), new Publication { Topic = new RoutingKey(topic) });

    private bool AdvanceReturns(TimeSpan step)
    {
        var advance = Task.Run(() => _clock.Advance(step));
        return SpinWait.SpinUntil(() => advance.IsCompleted, TimeSpan.FromSeconds(5));
    }

    private void WaitUntilTheMonitorWaits(int timers) =>
        Assert.True(SpinWait.SpinUntil(() => _clock.TimersCreated >= timers, TimeSpan.FromSeconds(5)));
}
