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

public class StaleMessageMonitorStartReadingsTests
{
    [Fact]
    public async Task When_the_monitor_starts_should_start_the_readings()
    {
        //Arrange
        var (monitor, readings) = BuildMonitor(new StaleMessageTopicCountingOutbox(), new RoutingKey("orders"));

        //Act
        await monitor.StartAsync(default);

        //Assert
        Assert.True(readings.IsStarted);
        Assert.Equal(nameof(StaleMessageTopicCountingOutbox), readings.OutboxType);
        Assert.True(readings.MeasuresTopics);
    }

    [Fact]
    public async Task When_the_monitor_starts_with_an_outbox_that_cannot_count_by_topic_should_start_the_readings_without_topics()
    {
        //Arrange
        var (monitor, readings) = BuildMonitor(new StaleMessageCountingOutbox(), new RoutingKey("orders"));

        //Act
        await monitor.StartAsync(default);

        //Assert
        Assert.True(readings.IsStarted);
        Assert.Equal(nameof(StaleMessageCountingOutbox), readings.OutboxType);
        Assert.False(readings.MeasuresTopics);
    }

    private static (TimedOutboxStaleMessageMonitor Monitor, OutboxStaleMessageReadings Readings) BuildMonitor(
        StaleMessageCountingOutbox outbox, RoutingKey topic)
    {
        var producers = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            { topic, new InMemoryMessageProducer(new InternalBus(), new Publication { Topic = topic }) }
        });

        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(new FakeTimeProvider());
        var builder = new ServiceCollectionBrighterBuilder(
            services,
            new ServiceCollectionSubscriberRegistry(services),
            new ServiceCollectionMessageMapperRegistryBuilder(services));
        builder.AddProducers(configuration =>
        {
            configuration.ProducerRegistry = producers;
            configuration.Outbox = outbox;
        });
        builder.UseOutboxStaleMessageMonitor(TimeSpan.FromMinutes(5));

        var provider = services.BuildServiceProvider();
        var monitor = provider.GetServices<IHostedService>().OfType<TimedOutboxStaleMessageMonitor>().Single();
        return (monitor, provider.GetRequiredService<OutboxStaleMessageReadings>());
    }
}
