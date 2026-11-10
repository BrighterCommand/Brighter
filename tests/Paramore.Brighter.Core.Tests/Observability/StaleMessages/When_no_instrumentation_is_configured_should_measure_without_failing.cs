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
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages;

[Collection(StaleMessageLoggingCollection.Name)]
public class NoInstrumentationTests
{
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly TimerCountingTimeProvider _clock = new(s_start);
    private readonly CapturingLoggerProvider _logs;

    public NoInstrumentationTests(StaleMessageLoggingFixture fixture)
    {
        _logs = fixture.Logs;
    }

    [Fact]
    public async Task When_no_instrumentation_is_configured_should_measure_without_failing()
    {
        //Arrange
        _logs.Reset();
        var outbox = new StaleMessageTopicCountingOutbox();
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(_clock);
        var builder = new ServiceCollectionBrighterBuilder(
            services,
            new ServiceCollectionSubscriberRegistry(services),
            new ServiceCollectionMessageMapperRegistryBuilder(services));
        builder.AddProducers(configuration =>
        {
            configuration.ProducerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                {
                    new RoutingKey("orders"),
                    new InMemoryMessageProducer(new InternalBus(), new Publication { Topic = new RoutingKey("orders") })
                }
            });
            configuration.Outbox = outbox;
        });
        builder.UseOutboxStaleMessageMonitor(TimeSpan.FromMinutes(5));
        await using var provider = services.BuildServiceProvider();
        using var monitor = provider.GetServices<IHostedService>().OfType<TimedOutboxStaleMessageMonitor>().Single();
        var readings = provider.GetRequiredService<OutboxStaleMessageReadings>();

        //Act
        var exception = await Record.ExceptionAsync(() => monitor.StartAsync(default));
        if (exception is null)
        {
            for (var dueTime = 1; dueTime <= 5; dueTime++)
            {
                Assert.True(SpinWait.SpinUntil(() => _clock.TimersCreated >= dueTime, TimeSpan.FromSeconds(5)));
                _clock.Advance(TimeSpan.FromMinutes(1));
                Assert.True(SpinWait.SpinUntil(
                    () => readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Completed) >= dueTime
                        && readings.GetTotal(StaleMessageCount.PerTopic, StaleMeasurementOutcome.Completed) >= dueTime,
                    TimeSpan.FromSeconds(5)));
            }

            exception = await Record.ExceptionAsync(() => monitor.StopAsync(default));
        }

        //Assert
        Assert.Null(exception);
        Assert.DoesNotContain(_logs.Entries, entry => entry.LogLevel >= LogLevel.Warning);
        Assert.Equal(5, readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Completed));
        Assert.Equal(5, readings.GetTotal(StaleMessageCount.PerTopic, StaleMeasurementOutcome.Completed));
        Assert.Equal(0, readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Failed));
        Assert.Equal(0, readings.GetTotal(StaleMessageCount.PerTopic, StaleMeasurementOutcome.Failed));
    }
}
