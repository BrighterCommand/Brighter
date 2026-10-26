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
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.Observability.StaleMessages.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages;

public class FirstDueTimeOverallCountTests
{
    [Fact]
    public async Task When_the_first_due_time_passes_should_report_the_overall_count()
    {
        //Arrange
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        using var metrics = new StaleMessageMetricsFixture(services =>
        {
            services.AddSingleton<TimeProvider>(clock);
            var builder = new ServiceCollectionBrighterBuilder(
                services,
                new ServiceCollectionSubscriberRegistry(services),
                new ServiceCollectionMessageMapperRegistryBuilder(services));
            builder.AddProducers(configuration =>
            {
                configuration.ProducerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>());
                configuration.Outbox = new InMemoryOutbox(clock);
            });
            builder.UseOutboxStaleMessageMonitor(TimeSpan.FromMinutes(5));
        });
        using var monitor = metrics.Services.GetServices<IHostedService>().OfType<TimedOutboxStaleMessageMonitor>().Single();
        var readings = metrics.Services.GetRequiredService<OutboxStaleMessageReadings>();

        //Act
        await monitor.StartAsync(default);
        clock.Advance(TimeSpan.FromSeconds(30));
        var beforeFirstDueTime = metrics.Observe();
        clock.Advance(TimeSpan.FromSeconds(30));
        SpinWait.SpinUntil(() => readings.TryGetLatestOverall(out _), TimeSpan.FromSeconds(5));
        clock.Advance(TimeSpan.FromSeconds(30));
        var afterFirstDueTime = metrics.Observe();

        //Assert
        Assert.DoesNotContain(beforeFirstDueTime, m => m.Instrument == ObservedStaleMessageInstruments.MESSAGES);
        var overall = Assert.Single(afterFirstDueTime, m => m.Instrument == ObservedStaleMessageInstruments.MESSAGES);
        Assert.Equal(0, overall.Value);
        var instrument = metrics.Instruments[overall.Instrument];
        Assert.Equal("Paramore.Brighter", instrument.Meter.Name);
        Assert.StartsWith("paramore.brighter.outbox.", instrument.Name);
        Assert.Equal("{message}", instrument.Unit);
    }
}
