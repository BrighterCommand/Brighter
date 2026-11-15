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
public class SettingsAtTheirBoundsTests
{
    private const long ONE_MILLISECOND = 1;
    private const long A_YEAR_IN_MILLISECONDS = 365L * 24 * 60 * 60 * 1000;

    private readonly TimerCountingTimeProvider _clock = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly CapturingLoggerProvider _logs;

    public SettingsAtTheirBoundsTests(StaleMessageLoggingFixture fixture)
    {
        _logs = fixture.Logs;
    }

    [Test]
    [Arguments(ONE_MILLISECOND, 1, 59)]
    [Arguments(A_YEAR_IN_MILLISECONDS, 1000, 10)]
    public async Task When_settings_are_at_their_bounds_should_start_and_measure(
        long staleMessageAgeInMilliseconds, int countCeiling, int timeoutInSeconds)
    {
        //Arrange
        _logs.Reset();
        var outbox = new InMemoryOutbox(_clock);
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
            builder.UseOutboxStaleMessageMonitor(TimeSpan.FromMilliseconds(staleMessageAgeInMilliseconds), options =>
            {
                options.CountCeiling = countCeiling;
                options.MeasurementInterval = TimeSpan.FromMinutes(1);
                options.MeasurementTimeout = TimeSpan.FromSeconds(timeoutInSeconds);
            });
        });
        using var monitor = metrics.Services.GetServices<IHostedService>().OfType<TimedOutboxStaleMessageMonitor>().Single();
        var readings = metrics.Services.GetRequiredService<OutboxStaleMessageReadings>();

        //Act
        await monitor.StartAsync(default);
        await WaitUntilTheMonitorWaits(1);
        _clock.Advance(TimeSpan.FromMinutes(1));
        await WaitForCompletions(readings, 1);
        await WaitUntilTheMonitorWaits(2);
        _clock.Advance(TimeSpan.FromSeconds(30));

        //Assert
        await Assert.That(readings.IsStarted).IsTrue();
        await Assert.That(readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Completed)).IsEqualTo(1);
        await Assert.That(System.Linq.Enumerable.Any(_logs.Entries, entry => entry.LogLevel >= LogLevel.Warning)).IsFalse();
    }

    private static InMemoryMessageProducer Producer(string topic) =>
        new(new InternalBus(), new Publication { Topic = new RoutingKey(topic) });

    private async Task WaitUntilTheMonitorWaits(int timers) =>
        await Assert.That(SpinWait.SpinUntil(() => _clock.TimersCreated >= timers, TimeSpan.FromSeconds(5))).IsTrue();

    private static async Task WaitForCompletions(OutboxStaleMessageReadings readings, long completions) =>
        await Assert.That(SpinWait.SpinUntil(
            () => readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Completed) >= completions,
            TimeSpan.FromSeconds(5))).IsTrue();
}
