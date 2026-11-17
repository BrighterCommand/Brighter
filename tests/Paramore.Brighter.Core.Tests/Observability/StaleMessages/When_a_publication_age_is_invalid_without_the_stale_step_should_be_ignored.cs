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
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.Observability.StaleMessages.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages;

public class InvalidAgeWithoutStepTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task When_a_publication_age_is_invalid_without_the_stale_step_should_be_ignored()
    {
        //Arrange
        var outbox = new StaleMessageTopicCountingOutbox();
        IHost? host = null;

        //Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            var publication = new Publication { Topic = new RoutingKey("orders"), StaleMessageAge = TimeSpan.Zero };
            var producers = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                { new RoutingKey("orders"), new InMemoryMessageProducer(new InternalBus(), publication) }
            });
            host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<TimeProvider>(_clock);
                    var builder = new ServiceCollectionBrighterBuilder(
                        services,
                        new ServiceCollectionSubscriberRegistry(services),
                        new ServiceCollectionMessageMapperRegistryBuilder(services));
                    builder.AddProducers(configuration =>
                    {
                        configuration.ProducerRegistry = producers;
                        configuration.Outbox = outbox;
                    });
                })
                .Build();
            await host.StartAsync();
        });
        _clock.Advance(TimeSpan.FromMinutes(10));
        if (host is not null)
        {
            await host.StopAsync();
            host.Dispose();
        }

        //Assert
        Assert.Null(exception);
        Assert.Empty(outbox.CountCalls);
        Assert.Empty(outbox.TopicCountCalls);
    }
}
