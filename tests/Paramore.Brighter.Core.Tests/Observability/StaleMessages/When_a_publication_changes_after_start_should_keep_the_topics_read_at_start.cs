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

public class TopicsReadAtStartTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task When_a_publication_changes_after_start_should_keep_the_topics_read_at_start()
    {
        //Arrange
        var outbox = new StaleMessageTopicCountingOutbox();
        var orders = new InMemoryMessageProducer(
            new InternalBus(),
            new Publication { Topic = new RoutingKey("orders"), StaleMessageAge = TimeSpan.FromMinutes(2) });
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
                    { new RoutingKey("orders"), orders }
                });
                configuration.Outbox = outbox;
            });
            builder.UseOutboxStaleMessageMonitor(TimeSpan.FromMinutes(5));
        });
        using var monitor = metrics.Services.GetServices<IHostedService>().OfType<TimedOutboxStaleMessageMonitor>().Single();
        var readings = metrics.Services.GetRequiredService<OutboxStaleMessageReadings>();

        //Act
        await monitor.StartAsync(default);
        _clock.Advance(TimeSpan.FromMinutes(1));
        WaitForPerTopicCompletions(readings, 1);
        _clock.Advance(TimeSpan.FromSeconds(30));
        orders.Publication.Topic = new RoutingKey("payments");
        orders.Publication.StaleMessageAge = TimeSpan.FromMinutes(4);
        _clock.Advance(TimeSpan.FromSeconds(30));
        WaitForPerTopicCompletions(readings, 2);
        _clock.Advance(TimeSpan.FromMinutes(1));
        WaitForPerTopicCompletions(readings, 3);

        //Assert
        var calls = outbox.TopicCountCalls.ToArray();
        Assert.Equal(3, calls.Length);
        Assert.All(calls, call => Assert.Equal(
            new Dictionary<RoutingKey, TimeSpan> { [new RoutingKey("orders")] = TimeSpan.FromMinutes(2) },
            call.DispatchedSinceByTopic));
    }

    private static void WaitForPerTopicCompletions(OutboxStaleMessageReadings readings, long completions) =>
        Assert.True(SpinWait.SpinUntil(
            () => readings.GetTotal(StaleMessageCount.PerTopic, StaleMeasurementOutcome.Completed) >= completions,
            TimeSpan.FromSeconds(5)));
}
