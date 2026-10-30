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

public class StaleOverallCountFollowsTheStoreTests
{
    private readonly TimerCountingTimeProvider _clock = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task When_stale_messages_change_should_record_the_overall_count()
    {
        //Arrange
        var outbox = new InMemoryOutbox(_clock);
        var context = new RequestContext();
        var m1 = OrdersEvent();
        var m2 = OrdersEvent();
        var m3 = OrdersEvent();
        outbox.Add(m1, context);
        outbox.Add(m2, context);
        outbox.MarkDispatched(m2.Id, context, _clock.GetUtcNow().AddSeconds(1));
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
            builder.UseOutboxStaleMessageMonitor(TimeSpan.FromMinutes(5));
        });
        var hostedServices = metrics.Services.GetServices<IHostedService>().ToArray();
        using var monitor = hostedServices.OfType<TimedOutboxStaleMessageMonitor>().Single();
        var readings = metrics.Services.GetRequiredService<OutboxStaleMessageReadings>();

        //Act
        await monitor.StartAsync(default);
        AdvanceThroughDueTimes(readings, 4);
        outbox.Add(m3, context);
        AdvanceThroughDueTimes(readings, 2);
        WaitUntilTheMonitorWaits(readings);
        _clock.Advance(TimeSpan.FromSeconds(30));
        var at120630 = OverallValues(metrics);
        _clock.Advance(TimeSpan.FromSeconds(10));
        outbox.MarkDispatched(m1.Id, context);
        _clock.Advance(TimeSpan.FromSeconds(20));
        WaitForCompletions(readings, 7);
        WaitUntilTheMonitorWaits(readings);
        _clock.Advance(TimeSpan.FromSeconds(30));
        var at120730 = OverallValues(metrics);
        _clock.Advance(TimeSpan.FromSeconds(30));
        WaitForCompletions(readings, 8);
        AdvanceThroughDueTimes(readings, 2);
        WaitUntilTheMonitorWaits(readings);
        _clock.Advance(TimeSpan.FromSeconds(30));
        var at121030 = OverallValues(metrics);

        //Assert
        Assert.Equal(new[] { 1L }, at120630);
        Assert.Equal(new[] { 0L }, at120730);
        Assert.Equal(new[] { 1L }, at121030);
        Assert.DoesNotContain(hostedServices, service => service is TimedOutboxSweeper);
    }

    private static Message OrdersEvent() =>
        new(new MessageHeader(Id.Random(), new RoutingKey("orders"), MessageType.MT_EVENT), new MessageBody("{}"));

    private void AdvanceThroughDueTimes(OutboxStaleMessageReadings readings, int dueTimes)
    {
        for (var i = 0; i < dueTimes; i++)
        {
            var completed = readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Completed);
            WaitUntilTheMonitorWaits(readings);
            _clock.Advance(TimeSpan.FromMinutes(1));
            WaitForCompletions(readings, completed + 1);
        }
    }

    private void WaitUntilTheMonitorWaits(OutboxStaleMessageReadings readings)
    {
        var completed = readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Completed);
        Assert.True(SpinWait.SpinUntil(() => _clock.TimersCreated >= completed + 1, TimeSpan.FromSeconds(5)));
    }

    private static void WaitForCompletions(OutboxStaleMessageReadings readings, long completions) =>
        SpinWait.SpinUntil(
            () => readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Completed) >= completions,
            TimeSpan.FromSeconds(5));

    private static long[] OverallValues(StaleMessageMetricsFixture metrics) =>
        metrics.Observe()
            .Where(m => m.Instrument == ObservedStaleMessageInstruments.MESSAGES)
            .Select(m => m.Value)
            .ToArray();
}
