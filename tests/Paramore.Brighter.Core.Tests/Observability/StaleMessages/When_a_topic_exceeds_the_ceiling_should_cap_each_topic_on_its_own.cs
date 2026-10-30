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

public class CeilingForEachTopicTests
{
    private const string DESTINATION = "messaging.destination.name";
    private readonly TimerCountingTimeProvider _clock = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task When_a_topic_exceeds_the_ceiling_should_cap_each_topic_on_its_own()
    {
        //Arrange
        var outbox = new InMemoryOutbox(_clock);
        var context = new RequestContext();
        for (var i = 0; i < 50; i++)
            outbox.Add(Event("orders"), context);
        for (var i = 0; i < 2; i++)
            outbox.Add(Event("payments"), context);
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
                    { new RoutingKey("payments"), Producer("payments") },
                    { new RoutingKey("refunds"), Producer("refunds") }
                });
                configuration.Outbox = outbox;
            });
            builder.UseOutboxStaleMessageMonitor(TimeSpan.FromMinutes(5), options => options.CountCeiling = 3);
        });
        using var monitor = metrics.Services.GetServices<IHostedService>().OfType<TimedOutboxStaleMessageMonitor>().Single();
        var readings = metrics.Services.GetRequiredService<OutboxStaleMessageReadings>();

        //Act
        await monitor.StartAsync(default);
        AdvanceThroughDueTimes(readings, 6);
        WaitUntilTheMonitorWaits(readings);
        _clock.Advance(TimeSpan.FromSeconds(30));
        var at120630 = metrics.Observe();

        //Assert
        var overall = Assert.Single(at120630, m => m.Instrument == ObservedStaleMessageInstruments.MESSAGES);
        Assert.Equal(3, overall.Value);
        Assert.Equal(
            new[] { "orders=3", "payments=2", "refunds=0" },
            at120630
                .Where(m => m.Instrument == ObservedStaleMessageInstruments.TOPIC_MESSAGES)
                .Select(m => $"{m.Attributes[DESTINATION]}={m.Value}")
                .OrderBy(series => series, StringComparer.Ordinal));
    }

    private static InMemoryMessageProducer Producer(string topic) =>
        new(new InternalBus(), new Publication { Topic = new RoutingKey(topic) });

    private static Message Event(string topic) =>
        new(new MessageHeader(Id.Random(), new RoutingKey(topic), MessageType.MT_EVENT), new MessageBody("{}"));

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
            () => readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Completed) >= completions,
            TimeSpan.FromSeconds(5)));
}
