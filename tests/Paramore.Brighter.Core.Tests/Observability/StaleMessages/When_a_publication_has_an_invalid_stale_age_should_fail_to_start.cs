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
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.Observability.StaleMessages.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages;

public class StaleMessageMonitorInvalidPublicationStaleAgeTests
{
    public static TheoryData<TimeSpan> InvalidStaleAges => new()
    {
        TimeSpan.Zero,
        TimeSpan.FromSeconds(-1),
        TimeSpan.FromDays(365) + TimeSpan.FromTicks(1),
        TimeSpan.MaxValue
    };

    [Theory]
    [MemberData(nameof(InvalidStaleAges))]
    public async Task When_a_publication_has_an_invalid_stale_age_should_fail_to_start(TimeSpan staleMessageAge)
    {
        //Arrange
        var interval = TimeSpan.FromMinutes(1);
        var timeProvider = new FakeTimeProvider();
        var outbox = new StaleMessageCountingOutbox();
        var routingKey = new RoutingKey("orders");
        var producers = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            {
                routingKey,
                new InMemoryMessageProducer(new InternalBus(), new Publication { Topic = routingKey, StaleMessageAge = staleMessageAge })
            }
        });

        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(timeProvider);
        var builder = new ServiceCollectionBrighterBuilder(
            services,
            new ServiceCollectionSubscriberRegistry(services),
            new ServiceCollectionMessageMapperRegistryBuilder(services));
        builder.AddProducers(configuration =>
        {
            configuration.ProducerRegistry = producers;
            configuration.Outbox = outbox;
        });
        builder.UseOutboxStaleMessageMonitor(TimeSpan.FromMinutes(5), options => options.MeasurementInterval = interval);

        var provider = services.BuildServiceProvider();
        var monitor = provider.GetServices<IHostedService>().OfType<TimedOutboxStaleMessageMonitor>().Single();

        //Act
        var exception = await Assert.ThrowsAsync<ConfigurationException>(() => monitor.StartAsync(default));
        timeProvider.Advance(interval);

        //Assert
        Assert.Contains("orders", exception.Message);
        Assert.Contains(staleMessageAge.ToString(), exception.Message);
        Assert.Empty(outbox.CountCalls);
    }
}
