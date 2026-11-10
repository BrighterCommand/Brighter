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
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages;

public class StaleStepNotAddedTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task When_the_stale_step_is_not_added_should_measure_nothing()
    {
        //Arrange
        var outbox = new StaleMessageTopicCountingOutbox();
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
                    {
                        new RoutingKey("orders"),
                        new InMemoryMessageProducer(
                            new InternalBus(),
                            new Publication { Topic = new RoutingKey("orders"), StaleMessageAge = TimeSpan.FromMinutes(1) })
                    }
                });
                configuration.Outbox = outbox;
                configuration.MaxOutStandingMessages = -1;
            });
        });
        var hostedServices = metrics.Services.GetServices<IHostedService>().ToList();

        //Act
        foreach (var hostedService in hostedServices)
            await hostedService.StartAsync(default);
        _clock.Advance(TimeSpan.FromMinutes(10));
        var observed = metrics.Observe();
        foreach (var hostedService in hostedServices)
            await hostedService.StopAsync(default);

        //Assert
        Assert.Empty(outbox.CountCalls);
        Assert.Empty(outbox.TopicCountCalls);
        Assert.DoesNotContain(observed, m => m.Instrument == ObservedStaleMessageInstruments.MESSAGES);
        Assert.DoesNotContain(observed, m => m.Instrument == ObservedStaleMessageInstruments.TOPIC_MESSAGES);
    }
}
