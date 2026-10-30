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

public class PerTopicCountBlockingItsThreadTests
{
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly TimerCountingTimeProvider _clock = new(s_start);

    [Fact]
    public async Task When_the_per_topic_count_blocks_its_thread_should_still_record_the_overall_count()
    {
        //Arrange
        var outbox = new StaleMessageTopicCountingOutbox(_clock);
        outbox.Returns(1);
        outbox.BlockTopicThreadUntilReleased();
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
                    { new RoutingKey("orders"), Producer("orders") },
                    { new RoutingKey("payments"), Producer("payments") }
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
        _clock.Advance(TimeSpan.FromSeconds(59));

        var dueTime = s_start.AddMinutes(1);
        var deadline = dueTime.AddSeconds(10);
        var end = dueTime.AddSeconds(30);
        var now = s_start.AddSeconds(59);
        DateTimeOffset? heldAt = null;
        DateTimeOffset? cancelledAt = null;
        var beforeTheDeadline = new List<StaleMeasurement>();
        var collected = new List<StaleMeasurement>();

        //Act
        try
        {
            while (now < end)
            {
                now = now.AddSeconds(1);
                if (!AdvanceReturns(TimeSpan.FromSeconds(1)))
                {
                    heldAt = now;
                    break;
                }

                if (now == dueTime)
                    SpinWait.SpinUntil(
                        () => readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Completed) >= 1,
                        TimeSpan.FromSeconds(5));
                if (now == deadline)
                {
                    SpinWait.SpinUntil(() => IsTopicCountCancelled(outbox), TimeSpan.FromSeconds(5));
                    SpinWait.SpinUntil(
                        () => readings.GetTotal(StaleMessageCount.PerTopic, StaleMeasurementOutcome.Abandoned) >= 1,
                        TimeSpan.FromSeconds(5));
                }

                if (cancelledAt is null && IsTopicCountCancelled(outbox))
                    cancelledAt = now;
                var observed = metrics.Observe();
                collected.AddRange(observed);
                if (now < deadline)
                    beforeTheDeadline.AddRange(observed);
            }
        }
        finally
        {
            outbox.ReleaseTopicThread();
        }

        //Assert
        Assert.Null(heldAt);
        Assert.Equal([dueTime], outbox.CountCalls.Select(call => call.CalledAt));
        var overall = beforeTheDeadline
            .Where(m => m.Instrument == ObservedStaleMessageInstruments.MESSAGES)
            .Select(m => m.Value)
            .Distinct();
        Assert.Equal([1], overall);
        Assert.Equal(deadline, cancelledAt);
        Assert.DoesNotContain(collected, m => m.Instrument == ObservedStaleMessageInstruments.TOPIC_MESSAGES);
    }

    private static InMemoryMessageProducer Producer(string topic) =>
        new(new InternalBus(), new Publication { Topic = new RoutingKey(topic) });

    private static bool IsTopicCountCancelled(StaleMessageTopicCountingOutbox outbox) =>
        outbox.TopicCountCalls.Any(call => call.CancellationToken.IsCancellationRequested);

    private bool AdvanceReturns(TimeSpan step)
    {
        var advance = Task.Run(() => _clock.Advance(step));
        return SpinWait.SpinUntil(() => advance.IsCompleted, TimeSpan.FromSeconds(5));
    }

    private void WaitUntilTheMonitorWaits(int timers) =>
        Assert.True(SpinWait.SpinUntil(() => _clock.TimersCreated >= timers, TimeSpan.FromSeconds(5)));
}
