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

public class BothGaugesWorkedExampleTests
{
    private const string DESTINATION = "messaging.destination.name";
    private readonly TimerCountingTimeProvider _clock = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task When_topics_hold_stale_messages_at_their_own_ages_should_report_both_gauges()
    {
        //Arrange
        var outbox = new InMemoryOutbox(_clock);
        var context = new RequestContext();
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
                    { new RoutingKey("orders"), Producer("orders", TimeSpan.FromMinutes(2)) },
                    { new RoutingKey("payments"), Producer("payments", null) }
                });
                configuration.Outbox = outbox;
            });
            builder.UseOutboxStaleMessageMonitor(TimeSpan.FromMinutes(5));
        });
        using var monitor = metrics.Services.GetServices<IHostedService>().OfType<TimedOutboxStaleMessageMonitor>().Single();
        var readings = metrics.Services.GetRequiredService<OutboxStaleMessageReadings>();
        var o3 = Event("orders");
        outbox.Add(Event("orders"), context);
        outbox.Add(o3, context);
        outbox.Add(Event("payments"), context);
        outbox.MarkDispatched(o3.Id, context, new DateTimeOffset(2026, 1, 1, 12, 0, 1, TimeSpan.Zero));

        //Act
        await monitor.StartAsync(default);
        WaitUntilTheMonitorWaits(readings);
        _clock.Advance(TimeSpan.FromSeconds(30));
        var at120030 = metrics.Observe();
        _clock.Advance(TimeSpan.FromSeconds(30));
        WaitForCompletions(readings, 1);
        var at120100 = metrics.Observe();
        AdvanceThroughDueTimes(readings, 2);
        WaitUntilTheMonitorWaits(readings);
        _clock.Advance(TimeSpan.FromSeconds(30));
        outbox.Add(Event("orders"), context);
        outbox.Add(Event("payments"), context);
        _clock.Advance(TimeSpan.FromSeconds(30));
        WaitForCompletions(readings, 4);
        AdvanceThroughDueTimes(readings, 2);
        WaitUntilTheMonitorWaits(readings);
        _clock.Advance(TimeSpan.FromSeconds(30));
        var at120630 = metrics.Observe();

        //Assert
        Assert.DoesNotContain(at120030, m => m.Instrument == ObservedStaleMessageInstruments.MESSAGES);
        Assert.DoesNotContain(at120030, m => m.Instrument == ObservedStaleMessageInstruments.TOPIC_MESSAGES);
        Assert.Equal(0, Assert.Single(at120100, m => m.Instrument == ObservedStaleMessageInstruments.MESSAGES).Value);
        Assert.Equal(new[] { "orders=0", "payments=0" }, Topics(at120100));
        Assert.Equal(2, Assert.Single(at120630, m => m.Instrument == ObservedStaleMessageInstruments.MESSAGES).Value);
        Assert.Equal(new[] { "orders=2", "payments=1" }, Topics(at120630));
    }

    private static InMemoryMessageProducer Producer(string topic, TimeSpan? staleMessageAge) =>
        new(new InternalBus(), new Publication { Topic = new RoutingKey(topic), StaleMessageAge = staleMessageAge });

    private static Message Event(string topic) =>
        new(new MessageHeader(Id.Random(), new RoutingKey(topic), MessageType.MT_EVENT), new MessageBody("{}"));

    private static string[] Topics(IReadOnlyList<StaleMeasurement> measurements) =>
        measurements
            .Where(m => m.Instrument == ObservedStaleMessageInstruments.TOPIC_MESSAGES)
            .Select(m => $"{m.Attributes[DESTINATION]}={m.Value}")
            .OrderBy(topic => topic, StringComparer.Ordinal)
            .ToArray();

    private void AdvanceThroughDueTimes(OutboxStaleMessageReadings readings, int dueTimes)
    {
        for (var i = 0; i < dueTimes; i++)
        {
            var completed = readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Completed);
            WaitUntilTheMonitorWaits(readings);
            _clock.Advance(TimeSpan.FromMinutes(1));
            WaitForCompletions(readings, completed + 1);
        }
    }

    private void WaitUntilTheMonitorWaits(OutboxStaleMessageReadings readings)
    {
        var completed = readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Completed);
        Assert.True(SpinWait.SpinUntil(() => _clock.TimersCreated >= completed + 1, TimeSpan.FromSeconds(5)));
    }

    private static void WaitForCompletions(OutboxStaleMessageReadings readings, long completions) =>
        Assert.True(SpinWait.SpinUntil(
            () => readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Completed) >= completions
                  && readings.GetTotal(StaleMessageCount.PerTopic, StaleMeasurementOutcome.Completed) >= completions,
            TimeSpan.FromSeconds(5)));
}
