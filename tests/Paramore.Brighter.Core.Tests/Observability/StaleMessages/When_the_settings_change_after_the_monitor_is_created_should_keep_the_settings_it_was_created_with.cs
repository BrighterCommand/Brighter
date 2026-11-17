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

public class SettingsReadAtCreationTests
{
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly TimerCountingTimeProvider _clock = new(s_start);

    [Fact]
    public async Task When_the_settings_change_after_the_monitor_is_created_should_keep_the_settings_it_was_created_with()
    {
        //Arrange
        var outbox = new StaleMessageCountingOutbox(_clock);
        outbox.Returns(50);
        TimedOutboxStaleMessageMonitorOptions? settings = null;
        using var metrics = new StaleMessageMetricsFixture(services =>
        {
            services.AddSingleton<TimeProvider>(_clock);
            var builder = new ServiceCollectionBrighterBuilder(
                services,
                new ServiceCollectionSubscriberRegistry(services),
                new ServiceCollectionMessageMapperRegistryBuilder(services));
            builder.AddProducers(configuration =>
            {
                configuration.ProducerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>());
                configuration.Outbox = outbox;
            });
            builder.UseOutboxStaleMessageMonitor(TimeSpan.FromMinutes(5), options =>
            {
                options.CountCeiling = 100;
                options.MeasurementInterval = TimeSpan.FromMinutes(1);
                settings = options;
            });
        });
        using var monitor = metrics.Services.GetServices<IHostedService>().OfType<TimedOutboxStaleMessageMonitor>().Single();
        var readings = metrics.Services.GetRequiredService<OutboxStaleMessageReadings>();

        //Act
        settings!.CountCeiling = 2;
        settings.MeasurementInterval = TimeSpan.FromMinutes(2);
        settings.MeasurementTimeout = TimeSpan.Zero;
        await monitor.StartAsync(default);
        WaitUntilTheMonitorWaits(1);
        _clock.Advance(TimeSpan.FromMinutes(1));
        WaitForOverallCompletions(readings, 1);
        WaitUntilTheMonitorWaits(2);
        _clock.Advance(TimeSpan.FromMinutes(1));
        WaitForOverallCompletions(readings, 2);
        WaitUntilTheMonitorWaits(3);
        _clock.Advance(TimeSpan.FromMinutes(1));
        WaitForOverallCompletions(readings, 3);

        //Assert
        Assert.Equal(new[] { 50L }, OverallValues(metrics));
        var calls = outbox.CountCalls.ToArray();
        Assert.Equal(
            new DateTimeOffset?[] { s_start.AddMinutes(1), s_start.AddMinutes(2), s_start.AddMinutes(3) },
            calls.Select(call => call.CalledAt));
        Assert.All(calls, call => Assert.Equal(100, call.MaxCount));
        Assert.Equal(3, readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Completed));
    }

    private static void WaitForOverallCompletions(OutboxStaleMessageReadings readings, long completions) =>
        SpinWait.SpinUntil(
            () => readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Completed) >= completions,
            TimeSpan.FromSeconds(5));

    private void WaitUntilTheMonitorWaits(int timers) =>
        SpinWait.SpinUntil(() => _clock.TimersCreated >= timers, TimeSpan.FromSeconds(5));

    private static long[] OverallValues(StaleMessageMetricsFixture metrics) =>
        metrics.Observe()
            .Where(m => m.Instrument == ObservedStaleMessageInstruments.MESSAGES)
            .Select(m => m.Value)
            .ToArray();
}
