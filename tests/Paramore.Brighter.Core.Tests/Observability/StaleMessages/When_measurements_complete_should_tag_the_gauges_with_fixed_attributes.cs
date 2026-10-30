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

public class FixedGaugeAttributesTests
{
    private const string IMPLEMENTATION = "paramore.brighter.outbox.implementation";
    private const string DESTINATION = "messaging.destination.name";
    private static readonly string[] s_topics = { "orders", "payments", "refunds" };
    private readonly TimerCountingTimeProvider _clock = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    [Fact]
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
            WaitUntilTheMonitorWaits(readings);
            _clock.Advance(TimeSpan.FromMinutes(1));
            WaitForCompletions(readings, StaleMessageCount.Overall, dueTime);
            WaitForCompletions(readings, StaleMessageCount.PerTopic, dueTime);
            observations.Add(metrics.Observe());
        }

        //Assert
        Assert.All(observations, observed =>
        {
            var overall = Assert.Single(observed, m => m.Instrument == ObservedStaleMessageInstruments.MESSAGES);
            Assert.Equal(new[] { IMPLEMENTATION }, overall.Attributes.Keys);
            Assert.DoesNotContain(overall.Attributes.Values, value => forbiddenValues.Contains(value?.ToString() ?? ""));
            Assert.All(
                observed.Where(m => m.Instrument == ObservedStaleMessageInstruments.MEASUREMENTS),
                counter =>
                {
                    Assert.DoesNotContain(DESTINATION, counter.Attributes.Keys);
                    Assert.DoesNotContain(counter.Attributes.Values, value => forbiddenValues.Contains(value?.ToString() ?? ""));
                });
        });
        var topics = observations.Last().Where(m => m.Instrument == ObservedStaleMessageInstruments.TOPIC_MESSAGES).ToList();
        Assert.Equal(3, topics.Count);
        Assert.All(topics, topic =>
        {
            Assert.Equal(new[] { DESTINATION, IMPLEMENTATION }, topic.Attributes.Keys.OrderBy(key => key, StringComparer.Ordinal));
            Assert.DoesNotContain(topic.Attributes.Values, value => perMessageValues.Contains(value?.ToString() ?? ""));
        });
    }

    private static InMemoryMessageProducer Producer(string topic) =>
        new(new InternalBus(), new Publication { Topic = new RoutingKey(topic) });

    private static Message Event(string topic) =>
        new(
            new MessageHeader(Id.Random(), new RoutingKey(topic), MessageType.MT_EVENT, correlationId: Id.Random()),
            new MessageBody("{}"));

    private void WaitUntilTheMonitorWaits(OutboxStaleMessageReadings readings)
    {
        var completed = readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Completed);
        Assert.True(SpinWait.SpinUntil(() => _clock.TimersCreated >= completed + 1, TimeSpan.FromSeconds(5)));
    }

    private static void WaitForCompletions(OutboxStaleMessageReadings readings, StaleMessageCount count, long completions) =>
        Assert.True(SpinWait.SpinUntil(
            () => readings.GetTotal(count, StaleMeasurementOutcome.Completed) >= completions,
            TimeSpan.FromSeconds(5)));
}
