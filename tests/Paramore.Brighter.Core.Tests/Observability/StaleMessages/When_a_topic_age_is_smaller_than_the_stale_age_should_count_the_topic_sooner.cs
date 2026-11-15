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

public class SmallestTopicAgeAgainstTheStoreTests
{
    private const string DESTINATION = "messaging.destination.name";
    private readonly TimerCountingTimeProvider _clock = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    [Test]
    public async Task When_a_topic_age_is_smaller_than_the_stale_age_should_count_the_topic_sooner()
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
                configuration.ProducerRegistry = new ProducerRegistry(new[]
                {
                    Producer(new RoutingKey("orders"), "orders.created", TimeSpan.FromMinutes(2)),
                    Producer(new RoutingKey("orders"), "orders.paid", TimeSpan.FromMinutes(4)),
                    Producer(new RoutingKey("orders"), "orders.shipped", null),
                    Producer(RoutingKey.Empty, "unrouted", TimeSpan.FromMinutes(1))
                }.ToDictionary(
                    producer => new ProducerKey(producer.Publication.Topic!, producer.Publication.Type),
                    producer => (IAmAMessageProducer)producer));
                configuration.Outbox = outbox;
            });
            builder.UseOutboxStaleMessageMonitor(TimeSpan.FromMinutes(5));
        });
        using var monitor = metrics.Services.GetServices<IHostedService>().OfType<TimedOutboxStaleMessageMonitor>().Single();
        var readings = metrics.Services.GetRequiredService<OutboxStaleMessageReadings>();

        //Act
        await monitor.StartAsync(default);
        await AdvanceThroughDueTimes(readings, 3);
        await WaitUntilTheMonitorWaits(readings);
        _clock.Advance(TimeSpan.FromSeconds(30));
        outbox.Add(OrdersEvent(), context);
        _clock.Advance(TimeSpan.FromSeconds(30));
        WaitForCompletions(readings, 4);
        await AdvanceThroughDueTimes(readings, 2);
        await WaitUntilTheMonitorWaits(readings);
        _clock.Advance(TimeSpan.FromSeconds(30));
        var at120630 = metrics.Observe();

        //Assert
        var overall = await Assert.That(System.Linq.Enumerable.Where(at120630, m => m.Instrument == ObservedStaleMessageInstruments.MESSAGES)).HasSingleItem();
        await Assert.That(overall.Value).IsEqualTo(0);
        var topic = await Assert.That(System.Linq.Enumerable.Where(at120630, m => m.Instrument == ObservedStaleMessageInstruments.TOPIC_MESSAGES)).HasSingleItem();
        await Assert.That($"{topic.Attributes[DESTINATION]}={topic.Value}").IsEqualTo("orders=1");
    }

    private static InMemoryMessageProducer Producer(RoutingKey topic, string type, TimeSpan? staleMessageAge) =>
        new(new InternalBus(), new Publication { Topic = topic, Type = new CloudEventsType(type), StaleMessageAge = staleMessageAge });

    private static Message OrdersEvent() =>
        new(new MessageHeader(Id.Random(), new RoutingKey("orders"), MessageType.MT_EVENT), new MessageBody("{}"));

    private async System.Threading.Tasks.Task AdvanceThroughDueTimes(OutboxStaleMessageReadings readings, int dueTimes)
    {
        for (var i = 0; i < dueTimes; i++)
        {
            var completed = readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Completed);
            await WaitUntilTheMonitorWaits(readings);
            _clock.Advance(TimeSpan.FromMinutes(1));
            WaitForCompletions(readings, completed + 1);
        }
    }

    private async Task WaitUntilTheMonitorWaits(OutboxStaleMessageReadings readings)
    {
        var completed = readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Completed);
        await Assert.That(SpinWait.SpinUntil(() => _clock.TimersCreated >= completed + 1, TimeSpan.FromSeconds(5))).IsTrue();
    }

    private static void WaitForCompletions(OutboxStaleMessageReadings readings, long completions) =>
        SpinWait.SpinUntil(
            () => readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Completed) >= completions,
            TimeSpan.FromSeconds(5));
}
