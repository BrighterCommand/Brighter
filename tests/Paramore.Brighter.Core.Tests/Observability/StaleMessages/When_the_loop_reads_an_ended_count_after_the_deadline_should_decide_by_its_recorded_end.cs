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
public class LateReadCountTests
{
    private const string DESTINATION = "messaging.destination.name";
    private readonly TimerCountingTimeProvider _clock = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly CapturingLoggerProvider _logs;

    public LateReadCountTests(StaleMessageLoggingFixture fixture)
    {
        _logs = fixture.Logs;
    }

    [Test]
    public async Task When_the_loop_reads_an_ended_count_after_the_deadline_should_decide_by_its_recorded_end()
    {
        //Arrange
        _logs.Reset();
        _logs.HoldEntry(entry => entry.LogLevel == LogLevel.Debug && entry.Message.Contains("stale messages overall"));
        var outbox = new StaleMessageTopicCountingOutbox();
        outbox.Returns(3);
        outbox.BlockTopicCountUntilReleased();
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
                    { new RoutingKey("orders"), Producer("orders") }
                });
                configuration.Outbox = outbox;
            });
            builder.UseOutboxStaleMessageMonitor(TimeSpan.FromMinutes(5), options =>
                options.MeasurementInterval = TimeSpan.FromMinutes(1));
        });
        using var monitor = metrics.Services.GetServices<IHostedService>().OfType<TimedOutboxStaleMessageMonitor>().Single();
        var readings = metrics.Services.GetRequiredService<OutboxStaleMessageReadings>();
        await monitor.StartAsync(default);
        await WaitUntilTheMonitorWaits(1);
        _clock.Advance(TimeSpan.FromMinutes(1));
        await Assert.That(SpinWait.SpinUntil(
            () => outbox.TopicCountCalls.Count == 1 && _logs.EntryHeld.IsCompleted,
            TimeSpan.FromSeconds(5))).IsTrue();

        //Act
        _clock.Advance(TimeSpan.FromSeconds(5));
        outbox.ReleaseTopicCount(new Dictionary<RoutingKey, int> { { new RoutingKey("orders"), 4 } });
        await Assert.That(SpinWait.SpinUntil(() => outbox.TopicCallEnded.IsCompleted, TimeSpan.FromSeconds(5))).IsTrue();
        _clock.Advance(TimeSpan.FromSeconds(15));
        _logs.ReleaseHeldEntry();
        await Assert.That(SpinWait.SpinUntil(
            () => readings.GetTotal(StaleMessageCount.PerTopic, StaleMeasurementOutcome.Completed)
                  + readings.GetTotal(StaleMessageCount.PerTopic, StaleMeasurementOutcome.Abandoned) >= 1
                  && readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Completed) >= 1,
            TimeSpan.FromSeconds(5))).IsTrue();
        var measurements = metrics.Observe();

        //Assert
        await Assert.That(readings.GetTotal(StaleMessageCount.PerTopic, StaleMeasurementOutcome.Completed)).IsEqualTo(1);
        await Assert.That(readings.GetTotal(StaleMessageCount.PerTopic, StaleMeasurementOutcome.Abandoned)).IsEqualTo(0);
        var topic = await Assert.That(System.Linq.Enumerable.Where(measurements, m => m.Instrument == ObservedStaleMessageInstruments.TOPIC_MESSAGES)).HasSingleItem();
        await Assert.That($"{topic.Attributes[DESTINATION]}={topic.Value}").IsEqualTo("orders=4");
    }

    private static InMemoryMessageProducer Producer(string topic) =>
        new(new InternalBus(), new Publication { Topic = new RoutingKey(topic) });

    private async Task WaitUntilTheMonitorWaits(int timers) =>
        await Assert.That(SpinWait.SpinUntil(() => _clock.TimersCreated >= timers, TimeSpan.FromSeconds(5))).IsTrue();
}
