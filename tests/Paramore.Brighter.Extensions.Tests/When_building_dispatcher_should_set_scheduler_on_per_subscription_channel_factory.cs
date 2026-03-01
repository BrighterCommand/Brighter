#region Licence
/* The MIT License (MIT)

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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class PerSubscriptionChannelFactorySchedulerTests
{
    [Fact]
    public void Should_set_scheduler_on_per_subscription_channel_factory()
    {
        // Arrange — one subscription uses a per-subscription channel factory
        var bus = new InternalBus();
        var defaultFactory = new InMemoryChannelFactory(bus, TimeProvider.System);
        var perSubFactory = new SchedulerAwareChannelFactory(bus);

        var services = new ServiceCollection();
        services
            .AddConsumers(options =>
            {
                options.Subscriptions = new List<Subscription>
                {
                    new(
                        new SubscriptionName("default-sub"),
                        new ChannelName("default:in-memory"),
                        new RoutingKey("default.event"),
                        typeof(TestEvent),
                        messagePumpType: MessagePumpType.Reactor),
                    new(
                        new SubscriptionName("custom-sub"),
                        new ChannelName("custom:in-memory"),
                        new RoutingKey("custom.event"),
                        typeof(TestEvent),
                        messagePumpType: MessagePumpType.Reactor,
                        channelFactory: perSubFactory)
                };
                options.DefaultChannelFactory = defaultFactory;
            })
            .AddProducers(configure =>
            {
                configure.ProducerRegistry = new ProducerRegistry(
                    new Dictionary<ProducerKey, IAmAMessageProducer>
                    {
                        { new ProducerKey("in-memory"), new InMemoryMessageProducer(bus, new Publication { Topic = "test" }) }
                    });
            })
            .AutoFromAssemblies();

        var provider = services.BuildServiceProvider();

        // Act — resolve IDispatcher, which triggers BuildDispatcher
        var dispatcher = provider.GetRequiredService<IDispatcher>();

        // Assert — both the default and per-subscription factory should have the scheduler
        Assert.NotNull(dispatcher);
        Assert.NotNull(defaultFactory.Scheduler);
        Assert.NotNull(perSubFactory.Scheduler);
        Assert.IsAssignableFrom<IAmAMessageScheduler>(perSubFactory.Scheduler);
    }

    [Fact]
    public void Should_set_scheduler_on_combined_channel_factory_and_propagate_to_inner_factories()
    {
        // Arrange — use a CombinedChannelFactory as the default (multi-bus scenario)
        var bus = new InternalBus();
        var innerFactory = new InMemoryChannelFactory(bus, TimeProvider.System);
        var combinedFactory = new CombinedChannelFactory([innerFactory]);

        var services = new ServiceCollection();
        services
            .AddConsumers(options =>
            {
                options.Subscriptions = new List<Subscription>
                {
                    new(
                        new SubscriptionName("test"),
                        new ChannelName("test:in-memory"),
                        new RoutingKey("test.event"),
                        typeof(TestEvent),
                        messagePumpType: MessagePumpType.Reactor)
                };
                options.DefaultChannelFactory = combinedFactory;
            })
            .AddProducers(configure =>
            {
                configure.ProducerRegistry = new ProducerRegistry(
                    new Dictionary<ProducerKey, IAmAMessageProducer>
                    {
                        { new ProducerKey("in-memory"), new InMemoryMessageProducer(bus, new Publication { Topic = "test" }) }
                    });
            })
            .AutoFromAssemblies();

        var provider = services.BuildServiceProvider();

        // Act
        var dispatcher = provider.GetRequiredService<IDispatcher>();

        // Assert — the inner factory should have the scheduler propagated through CombinedChannelFactory
        Assert.NotNull(dispatcher);
        Assert.NotNull(innerFactory.Scheduler);
        Assert.IsAssignableFrom<IAmAMessageScheduler>(innerFactory.Scheduler);
    }

    private class SchedulerAwareChannelFactory : IAmAChannelFactory, IAmAChannelFactoryWithScheduler
    {
        private readonly InternalBus _bus;

        public IAmAMessageScheduler? Scheduler { get; set; }

        public SchedulerAwareChannelFactory(InternalBus bus) => _bus = bus;

        public IAmAChannelSync CreateSyncChannel(Subscription subscription)
        {
            return new Channel(
                subscription.ChannelName,
                subscription.RoutingKey,
                new InMemoryMessageConsumer(subscription.RoutingKey, _bus, TimeProvider.System));
        }

        public IAmAChannelAsync CreateAsyncChannel(Subscription subscription)
        {
            return new ChannelAsync(
                subscription.ChannelName,
                subscription.RoutingKey,
                new InMemoryMessageConsumer(subscription.RoutingKey, _bus, TimeProvider.System));
        }

        public Task<IAmAChannelAsync> CreateAsyncChannelAsync(Subscription subscription,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateAsyncChannel(subscription));
        }
    }
}
