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

public class FixedGaugeAttributesTests
{
    private const string IMPLEMENTATION = "paramore.brighter.outbox.implementation";
    private const string DESTINATION = "messaging.destination.name";
    private static readonly string[] s_topics = { "orders", "payments", "refunds" };
    private readonly TimerCountingTimeProvider _clock = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    [Test]
    public async Task When_measurements_complete_should_tag_the_gauges_with_fixed_attributes()
    {
        //Arrange
        var outbox = new InMemoryOutbox(_clock);
        var context = new RequestContext();
        var messages = Enumerable.Range(0, 10).Select(i => Event(s_topics[i % s_topics.Length])).ToList();
        foreach (var message in messages)
            outbox.Add(message, context);
        var perMessageValues = messages
            .SelectMany(message => new[] { message.Id.Value, message.Header.CorrelationId.Value })
            .ToHashSet(StringComparer.Ordinal);
        var forbiddenValues = perMessageValues.Concat(s_topics).ToHashSet(StringComparer.Ordinal);
        using var metrics = new StaleMessageMetricsFixture(services =>
        {
            services.AddSingleton<TimeProvider>(_clock);
            var builder = new ServiceCollectionBrighterBuilder(
                services,
                new ServiceCollectionSubscriberRegistry(services),
                new ServiceCollectionMessageMapperRegistryBuilder(services));
            builder.AddProducers(configuration =>
            {
                configuration.ProducerRegistry = new ProducerRegistry(s_topics.ToDictionary(
                    topic => new RoutingKey(topic),
                    topic => (IAmAMessageProducer)Producer(topic)));
                configuration.Outbox = outbox;
            });
            builder.UseOutboxStaleMessageMonitor(TimeSpan.FromMinutes(5));
        });
        using var monitor = metrics.Services.GetServices<IHostedService>().OfType<TimedOutboxStaleMessageMonitor>().Single();
        var readings = metrics.Services.GetRequiredService<OutboxStaleMessageReadings>();
        var observations = new List<IReadOnlyList<StaleMeasurement>>();

        //Act
        await monitor.StartAsync(default);
        for (var dueTime = 1; dueTime <= 10; dueTime++)
        {
            await WaitUntilTheMonitorWaits(readings);
            _clock.Advance(TimeSpan.FromMinutes(1));
            await WaitForCompletions(readings, StaleMessageCount.Overall, dueTime);
            await WaitForCompletions(readings, StaleMessageCount.PerTopic, dueTime);
            observations.Add(metrics.Observe());
        }

        //Assert
        foreach (var observed in observations)
{
    var overall = (await Assert.That(System.Linq.Enumerable.Where(observed, m => m.Instrument == ObservedStaleMessageInstruments.MESSAGES)).HasSingleItem());
    await Assert.That(overall.Attributes.Keys).IsEqualTo(new[] { IMPLEMENTATION });
    await Assert.That(System.Linq.Enumerable.Any(overall.Attributes.Values, value => forbiddenValues.Contains(value?.ToString() ?? ""))).IsFalse();
    foreach (var counter in observed.Where(m => m.Instrument == ObservedStaleMessageInstruments.MEASUREMENTS))
    {
        await Assert.That(counter.Attributes.Keys).DoesNotContain(DESTINATION);
        await Assert.That(System.Linq.Enumerable.Any(counter.Attributes.Values, value => forbiddenValues.Contains(value?.ToString() ?? ""))).IsFalse();
    }
}
        var topics = observations.Last().Where(m => m.Instrument == ObservedStaleMessageInstruments.TOPIC_MESSAGES).ToList();
        await Assert.That(topics.Count).IsEqualTo(3);
        foreach (var topic in topics)
{
    await Assert.That(topic.Attributes.Keys.OrderBy(key => key, StringComparer.Ordinal)).IsEqualTo(new[] { DESTINATION, IMPLEMENTATION });
    await Assert.That(System.Linq.Enumerable.Any(topic.Attributes.Values, value => perMessageValues.Contains(value?.ToString() ?? ""))).IsFalse();
}
    }

    private static InMemoryMessageProducer Producer(string topic) =>
        new(new InternalBus(), new Publication { Topic = new RoutingKey(topic) });

    private static Message Event(string topic) =>
        new(
            new MessageHeader(Id.Random(), new RoutingKey(topic), MessageType.MT_EVENT, correlationId: Id.Random()),
            new MessageBody("{}"));

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
