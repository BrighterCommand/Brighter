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
public class AbandonedCountTests
{
    private readonly TimerCountingTimeProvider _clock = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly CapturingLoggerProvider _logs;

    public AbandonedCountTests(StaleMessageLoggingFixture fixture)
    {
        _logs = fixture.Logs;
    }

    [Test]
    public async Task When_a_count_does_not_answer_within_the_timeout_should_abandon_it()
    {
        //Arrange
        _logs.Reset();
        var outbox = new StaleMessageCountingOutbox();
        outbox.BlockUntilCancelled();
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
        await Assert.That(SpinWait.SpinUntil(() => outbox.CountCalls.Count == 1, TimeSpan.FromSeconds(5))).IsTrue();
        var token = outbox.CountCalls.Single().CancellationToken;

        //Act
        _clock.Advance(TimeSpan.FromSeconds(10));

        //Assert
        await Assert.That(SpinWait.SpinUntil(() => token.IsCancellationRequested, TimeSpan.FromSeconds(5))).IsTrue();
        await Assert.That(SpinWait.SpinUntil(() => outbox.CallEnded.IsCompleted, TimeSpan.FromSeconds(5))).IsTrue();
        await Assert.That(SpinWait.SpinUntil(
            () => readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Abandoned) >= 1,
            TimeSpan.FromSeconds(5))).IsTrue();

        await Assert.That(readings.TryGetLatestOverall(out _)).IsFalse();
        await Assert.That(readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Abandoned)).IsEqualTo(1);
        await Assert.That(readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Failed)).IsEqualTo(0);

        var warnings = _logs.Entries.Where(entry => entry.LogLevel == LogLevel.Warning).Select(entry => entry.Message).ToArray();
        var warning = await Assert.That(warnings).HasSingleItem();
        await Assert.That(warning).Contains(nameof(StaleMessageCountingOutbox));
        await Assert.That(warning).Contains("abandoned the Overall count");
        await Assert.That(warning).Contains("00:00:10");
    }

    private static InMemoryMessageProducer Producer(string topic) =>
        new(new InternalBus(), new Publication { Topic = new RoutingKey(topic) });

    private async Task WaitUntilTheMonitorWaits(int timers) =>
        await Assert.That(SpinWait.SpinUntil(() => _clock.TimersCreated >= timers, TimeSpan.FromSeconds(5))).IsTrue();
}
