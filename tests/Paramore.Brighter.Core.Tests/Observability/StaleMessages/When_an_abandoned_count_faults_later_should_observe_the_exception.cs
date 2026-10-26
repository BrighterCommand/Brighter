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
using System.Collections.Concurrent;
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
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages;

[Collection(StaleMessageLoggingCollection.Name)]
public class AbandonedCountFaultsLaterTests
{
    private readonly TimerCountingTimeProvider _clock = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly CapturingLoggerProvider _logs;

    public AbandonedCountFaultsLaterTests(StaleMessageLoggingFixture fixture)
    {
        _logs = fixture.Logs;
    }

    [Fact]
    public async Task When_an_abandoned_count_faults_later_should_observe_the_exception()
    {
        //Arrange
        _logs.Reset();
        var faultedLater = new InvalidOperationException("the abandoned count failed later");
        var outbox = new StaleMessageCountingOutbox(_clock);
        outbox.ThrowsWhenReleased(faultedLater);
        outbox.Returns(0);
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
        var unobserved = new ConcurrentQueue<AggregateException>();
        void Record(object? sender, UnobservedTaskExceptionEventArgs args) =>
            unobserved.Enqueue(args.Exception);

        await monitor.StartAsync(default);
        WaitUntilTheMonitorWaits(1);
        _clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(SpinWait.SpinUntil(() => outbox.CountCalls.Count == 1, TimeSpan.FromSeconds(5)));
        _clock.Advance(TimeSpan.FromSeconds(10));
        Assert.True(SpinWait.SpinUntil(
            () => readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Abandoned) >= 1,
            TimeSpan.FromSeconds(5)));
        WaitUntilTheMonitorWaits(2);
        _clock.Advance(TimeSpan.FromSeconds(20));

        //Act
        TaskScheduler.UnobservedTaskException += Record;
        try
        {
            outbox.Release(0);
            Assert.True(SpinWait.SpinUntil(() => outbox.CallEnded.IsCompleted, TimeSpan.FromSeconds(5)));
            _clock.Advance(TimeSpan.FromSeconds(30));
            Assert.True(SpinWait.SpinUntil(
                () => outbox.CountCalls.Count == 2
                    && readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Completed) >= 1,
                TimeSpan.FromSeconds(5)));
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= Record;
        }

        //Assert
        Assert.DoesNotContain(unobserved, exception => exception.Flatten().InnerExceptions.Contains(faultedLater));
        Assert.DoesNotContain(_logs.Entries, entry => entry.LogLevel == LogLevel.Error || entry.Message.Contains("failed"));
    }

    private static InMemoryMessageProducer Producer(string topic) =>
        new(new InternalBus(), new Publication { Topic = new RoutingKey(topic) });

    private void WaitUntilTheMonitorWaits(int timers) =>
        Assert.True(SpinWait.SpinUntil(() => _clock.TimersCreated >= timers, TimeSpan.FromSeconds(5)));
}
