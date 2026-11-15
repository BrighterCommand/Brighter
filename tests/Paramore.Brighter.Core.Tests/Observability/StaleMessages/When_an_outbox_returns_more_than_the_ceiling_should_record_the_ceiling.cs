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

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages;

public class OutboxAboveTheCeilingTests
{
    private const string DESTINATION = "messaging.destination.name";
    private readonly TimerCountingTimeProvider _clock = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    [Test]
    public async Task When_an_outbox_returns_more_than_the_ceiling_should_record_the_ceiling()
    {
        //Arrange
        var outbox = new FixedCountTopicOutbox(150, new Dictionary<RoutingKey, int>
        {
            { new RoutingKey("orders"), 150 },
            { new RoutingKey("payments"), 0 }
        });
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
            builder.UseOutboxStaleMessageMonitor(TimeSpan.FromMinutes(5), options => options.CountCeiling = 100);
        });
        using var monitor = metrics.Services.GetServices<IHostedService>().OfType<TimedOutboxStaleMessageMonitor>().Single();
        var readings = metrics.Services.GetRequiredService<OutboxStaleMessageReadings>();

        //Act
        await monitor.StartAsync(default);
        await WaitUntilTheMonitorWaits(readings);
        _clock.Advance(TimeSpan.FromMinutes(1));
        await WaitForCompletions(readings, StaleMessageCount.Overall, 1);
        await WaitForCompletions(readings, StaleMessageCount.PerTopic, 1);
        var at120100 = metrics.Observe();

        //Assert
        var overall = await Assert.That(System.Linq.Enumerable.Where(at120100, m => m.Instrument == ObservedStaleMessageInstruments.MESSAGES)).HasSingleItem();
        await Assert.That(overall.Value).IsEqualTo(100);
        await Assert.That(at120100
                .Where(m => m.Instrument == ObservedStaleMessageInstruments.TOPIC_MESSAGES)
                .Select(m => $"{m.Attributes[DESTINATION]}={m.Value}")
                .OrderBy(series => series, StringComparer.Ordinal)).IsEqualTo(new[] { "orders=100", "payments=0" });
    }

    private static InMemoryMessageProducer Producer(string topic) =>
        new(new InternalBus(), new Publication { Topic = new RoutingKey(topic) });

    private async Task WaitUntilTheMonitorWaits(OutboxStaleMessageReadings readings)
    {
        var completed = readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Completed);
        await Assert.That(SpinWait.SpinUntil(() => _clock.TimersCreated >= completed + 1, TimeSpan.FromSeconds(5))).IsTrue();
    }

    private static async Task WaitForCompletions(OutboxStaleMessageReadings readings, StaleMessageCount count, long completions) =>
        await Assert.That(SpinWait.SpinUntil(
            () => readings.GetTotal(count, StaleMeasurementOutcome.Completed) >= completions,
            TimeSpan.FromSeconds(5))).IsTrue();
}
