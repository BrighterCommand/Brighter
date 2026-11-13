#region Licence

/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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


using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class OutboxCircuitBreakerOptInTests
{
    private readonly RoutingKey _topic = new("opt-in.event");

    [Fact]
    public async Task When_no_circuit_breaker_is_registered_and_a_publish_fails_should_exclude_no_topic()
    {
        // Arrange
        var (provider, bus, publishFails) = BuildBrighter(registerBreaker: false);
        await using var _ = provider;
        var processor = provider.GetRequiredService<IAmACommandProcessor>();
        var mediator = provider.GetRequiredService<IAmAnOutboxProducerMediator>();

        // Act
        await Record.ExceptionAsync(() => processor.PostAsync(new DefaultMapperEvent()));
        publishFails.Value = false;
        await mediator.ClearOutstandingFromOutboxAsync(10, TimeSpan.Zero, false, new RequestContext());

        // Assert
        Assert.Null(provider.GetService<IAmAnOutboxCircuitBreaker>());
        Assert.Single(bus.Stream(_topic));
    }

    [Fact]
    public async Task When_an_in_memory_circuit_breaker_is_registered_and_a_publish_fails_should_exclude_only_that_instances_trip()
    {
        // Arrange
        var (provider, bus, publishFails) = BuildBrighter(registerBreaker: true);
        await using var _ = provider;
        var processor = provider.GetRequiredService<IAmACommandProcessor>();
        var mediator = provider.GetRequiredService<IAmAnOutboxProducerMediator>();
        var breaker = provider.GetRequiredService<IAmAnOutboxCircuitBreaker>();

        // Act
        await Record.ExceptionAsync(() => processor.PostAsync(new DefaultMapperEvent()));
        publishFails.Value = false;
        await mediator.ClearOutstandingFromOutboxAsync(10, TimeSpan.Zero, false, new RequestContext());

        // Assert
        Assert.IsType<InMemoryOutboxCircuitBreaker>(breaker);
        Assert.Equal([_topic], breaker.TrippedTopics.ToArray());
        Assert.Empty(bus.Stream(_topic));
    }

    private (ServiceProvider Provider, InternalBus Bus, FailureSwitch PublishFails) BuildBrighter(bool registerBreaker)
    {
        var services = new ServiceCollection();
        var builder = services.AddBrighter(_ => { });
        if (registerBreaker)
            services.AddSingleton<IAmAnOutboxCircuitBreaker>(new InMemoryOutboxCircuitBreaker());

        var bus = new InternalBus();
        var publishFails = new FailureSwitch { Value = true };
        var producer = new InMemoryMessageProducer(bus,
            new Publication { Topic = _topic, RequestType = typeof(DefaultMapperEvent) },
            InstrumentationOptions.All)
        {
            PublishFailurePredicate = _ => publishFails.Value ? throw new InvalidOperationException("The broker refused the message") : false
        };
        builder.AddProducers(options => options.ProducerRegistry = new ProducerRegistry(
            new Dictionary<RoutingKey, IAmAMessageProducer> { [_topic] = producer }));
        builder.MapperRegistry(_ => { });

        return (services.BuildServiceProvider(), bus, publishFails);
    }

    private sealed class FailureSwitch
    {
        public bool Value { get; set; }
    }
}
