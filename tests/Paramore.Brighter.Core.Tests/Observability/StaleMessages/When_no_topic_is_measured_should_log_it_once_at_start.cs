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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.Observability.StaleMessages.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages;

[Collection(StaleMessageLoggingCollection.Name)]
public class StaleMessageMonitorNoMeasuredTopicLogTests
{
    private const string NO_REGISTRY = "no registry";
    private const string EMPTY_REGISTRY = "empty registry";
    private const string EMPTY_TOPIC_ONLY = "empty topic only";

    private readonly CapturingLoggerProvider _logs;

    public StaleMessageMonitorNoMeasuredTopicLogTests(StaleMessageLoggingFixture fixture)
    {
        _logs = fixture.Logs;
    }

    [Theory]
    [InlineData(NO_REGISTRY)]
    [InlineData(EMPTY_REGISTRY)]
    [InlineData(EMPTY_TOPIC_ONLY)]
    public async Task When_no_topic_is_measured_should_log_it_once_at_start(string producers)
    {
        //Arrange
        _logs.Reset();
        var outbox = new StaleMessageTopicCountingOutbox();

        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(new FakeTimeProvider());
        services.AddSingleton<IAmAnOutbox>(outbox);
        services.AddSingleton<IAmProducersConfiguration>(new ProducersConfiguration { ProducerRegistry = CreateRegistry(producers) });
        var builder = new ServiceCollectionBrighterBuilder(
            services,
            new ServiceCollectionSubscriberRegistry(services),
            new ServiceCollectionMessageMapperRegistryBuilder(services));
        builder.UseOutboxStaleMessageMonitor(TimeSpan.FromMinutes(5));

        var provider = services.BuildServiceProvider();
        var monitor = provider.GetServices<IHostedService>().OfType<TimedOutboxStaleMessageMonitor>().Single();

        //Act
        await monitor.StartAsync(default);

        //Assert
        var entry = Assert.Single(_logs.Entries, logEntry => logEntry.LogLevel == LogLevel.Information);
        Assert.Contains(nameof(StaleMessageTopicCountingOutbox), entry.Message);
        Assert.Contains("no measured topic", entry.Message);
    }

    private static IAmAProducerRegistry? CreateRegistry(string producers) => producers switch
    {
        EMPTY_REGISTRY => new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>()),
        EMPTY_TOPIC_ONLY => new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            { RoutingKey.Empty, new InMemoryMessageProducer(new InternalBus(), new Publication { Topic = RoutingKey.Empty }) }
        }),
        _ => null
    };
}
