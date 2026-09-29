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
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Core.Tests.Observability.StaleMessages.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages;

[NotInParallel]
[ClassDataSource<StaleMessageLoggingFixture>(Shared = SharedType.PerTestSession)]
public class OnlyTheOverallCountRunsTests
{
    private const string COUNT = "paramore.brighter.outbox.stale.count";
    private const string NO_CAPABILITY = "no capability";
    private const string NO_REGISTRY = "no registry";
    private const string EMPTY_REGISTRY = "empty registry";
    private const string EMPTY_TOPIC_ONLY = "empty topic only";

    private readonly TimerCountingTimeProvider _clock = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly CapturingLoggerProvider _logs;

    public OnlyTheOverallCountRunsTests(StaleMessageLoggingFixture fixture)
    {
        _logs = fixture.Logs;
    }

    [Test]
    [Arguments(NO_CAPABILITY)]
    [Arguments(NO_REGISTRY)]
    [Arguments(EMPTY_REGISTRY)]
    [Arguments(EMPTY_TOPIC_ONLY)]
    public async Task When_the_per_topic_count_is_left_out_should_run_only_the_overall_count(string setup)
    {
        //Arrange
        _logs.Reset();
        var outbox = setup == NO_CAPABILITY ? new StaleMessageCountingOutbox(_clock) : new StaleMessageTopicCountingOutbox(_clock);
        using var metrics = new StaleMessageMetricsFixture(services =>
        {
            services.AddSingleton<TimeProvider>(_clock);
            services.AddSingleton<IAmAnOutbox>(outbox);
            services.AddSingleton<IAmProducersConfiguration>(new ProducersConfiguration { ProducerRegistry = CreateRegistry(setup) });
            var builder = new ServiceCollectionBrighterBuilder(
                services,
                new ServiceCollectionSubscriberRegistry(services),
                new ServiceCollectionMessageMapperRegistryBuilder(services));
            builder.UseOutboxStaleMessageMonitor(TimeSpan.FromMinutes(5));
        });
        using var monitor = metrics.Services.GetServices<IHostedService>().OfType<TimedOutboxStaleMessageMonitor>().Single();
        var readings = metrics.Services.GetRequiredService<OutboxStaleMessageReadings>();
        var collections = new List<IReadOnlyList<StaleMeasurement>>();

        //Act
        await monitor.StartAsync(default);
        for (var dueTime = 1; dueTime <= 5; dueTime++)
        {
            await WaitUntilTheMonitorWaits(dueTime);
            _clock.Advance(TimeSpan.FromMinutes(1));
            await WaitForCompletions(readings, dueTime);
            collections.Add(metrics.Observe());
        }
        await WaitUntilTheMonitorWaits(6);
        _clock.Advance(TimeSpan.FromSeconds(30));
        collections.Add(metrics.Observe());

        //Assert
        foreach (var measurements in collections)
{
    await Assert.That(System.Linq.Enumerable.Where(measurements, m => m.Instrument == ObservedStaleMessageInstruments.MESSAGES)).HasSingleItem();
}
        foreach (var measurements in collections)
{
    await Assert.That(System.Linq.Enumerable.Any(measurements, m => m.Instrument == ObservedStaleMessageInstruments.TOPIC_MESSAGES)).IsFalse();
}
        foreach (var measurements in collections)
{
    await Assert.That(System.Linq.Enumerable.Any(measurements, m => m.Instrument == ObservedStaleMessageInstruments.MEASUREMENTS && Equals(m.Attributes[COUNT], "per_topic"))).IsFalse();
}
        await Assert.That(outbox.CountCalls.Count).IsEqualTo(5);
        if (outbox is StaleMessageTopicCountingOutbox topicCountingOutbox)
            await Assert.That(topicCountingOutbox.TopicCountCalls).IsEmpty();
        var entry = (await Assert.That(System.Linq.Enumerable.Where(_logs.Entries, logEntry => logEntry.LogLevel >= LogLevel.Information)).HasSingleItem());
        await Assert.That(entry.Message).Contains(setup == NO_CAPABILITY ? "no capability" : "no measured topic");
    }

    private static IAmAProducerRegistry? CreateRegistry(string setup) => setup switch
    {
        NO_CAPABILITY => new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            { new RoutingKey("orders"), Producer(new RoutingKey("orders")) },
            { new RoutingKey("payments"), Producer(new RoutingKey("payments")) }
        }),
        EMPTY_REGISTRY => new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>()),
        EMPTY_TOPIC_ONLY => new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            { RoutingKey.Empty, Producer(RoutingKey.Empty) }
        }),
        _ => null
    };

    private static InMemoryMessageProducer Producer(RoutingKey topic) =>
        new(new InternalBus(), new Publication { Topic = topic });

    private async Task WaitUntilTheMonitorWaits(int timers) =>
        await Assert.That(SpinWait.SpinUntil(() => _clock.TimersCreated >= timers, TimeSpan.FromSeconds(5))).IsTrue();

    private static async Task WaitForCompletions(OutboxStaleMessageReadings readings, long completions) =>
        await Assert.That(SpinWait.SpinUntil(
            () => readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Completed) >= completions,
            TimeSpan.FromSeconds(5))).IsTrue();
}
