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
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.Observability.StaleMessages.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages;

public class BoundedCallPerCountTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task When_five_due_times_complete_should_make_one_bounded_call_per_count_each(bool countsByTopic)
    {
        //Arrange
        var outbox = countsByTopic ? new StaleMessageTopicCountingOutbox() : new StaleMessageCountingOutbox();
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
            builder.UseOutboxStaleMessageMonitor(TimeSpan.FromMinutes(5), options => options.CountCeiling = 25);
        });
        using var monitor = metrics.Services.GetServices<IHostedService>().OfType<TimedOutboxStaleMessageMonitor>().Single();
        var readings = metrics.Services.GetRequiredService<OutboxStaleMessageReadings>();

        //Act
        await monitor.StartAsync(default);
        for (var dueTime = 1; dueTime <= 5; dueTime++)
        {
            _clock.Advance(TimeSpan.FromMinutes(1));
            WaitForCompletions(readings, dueTime, countsByTopic);
        }

        //Assert
        var countCalls = outbox.CountCalls.ToArray();
        Assert.Equal(5, countCalls.Length);
        Assert.All(countCalls, call =>
        {
            Assert.Equal(25, call.MaxCount);
            Assert.False(call.Synchronous);
        });
        if (outbox is StaleMessageTopicCountingOutbox topicCountingOutbox)
        {
            var topicCountCalls = topicCountingOutbox.TopicCountCalls.ToArray();
            Assert.Equal(5, topicCountCalls.Length);
            Assert.All(topicCountCalls, call =>
            {
                Assert.Equal(new[] { new RoutingKey("orders"), new RoutingKey("payments") }, call.DispatchedSinceByTopic.Keys.OrderBy(topic => topic.Value));
                Assert.Equal(25, call.MaxCountPerTopic);
            });
        }
    }

    private static InMemoryMessageProducer Producer(string topic) =>
        new(new InternalBus(), new Publication { Topic = new RoutingKey(topic) });

    private static void WaitForCompletions(OutboxStaleMessageReadings readings, long completions, bool countsByTopic) =>
        SpinWait.SpinUntil(
            () => readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Completed) >= completions
                  && (!countsByTopic || readings.GetTotal(StaleMessageCount.PerTopic, StaleMeasurementOutcome.Completed) >= completions),
            TimeSpan.FromSeconds(5));
}
