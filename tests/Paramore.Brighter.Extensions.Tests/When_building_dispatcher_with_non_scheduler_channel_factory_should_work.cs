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

public class When_building_dispatcher_with_non_scheduler_channel_factory_should_work
{
    [Fact]
    public void Should_build_dispatcher_without_errors()
    {
        // Arrange — configure with a channel factory that does NOT implement IAmAChannelFactoryWithScheduler
        var bus = new InternalBus();
        var channelFactory = new PlainChannelFactory(bus);

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
                options.DefaultChannelFactory = channelFactory;
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

        // Assert — dispatcher should build successfully; no scheduler-related exceptions thrown
        Assert.NotNull(dispatcher);
        Assert.IsNotType<IAmAChannelFactoryWithScheduler>(channelFactory);
    }

    /// <summary>
    /// A channel factory that only implements IAmAChannelFactory, not IAmAChannelFactoryWithScheduler.
    /// Used to verify backward compatibility when the scheduler interface is not implemented.
    /// </summary>
    private class PlainChannelFactory : IAmAChannelFactory
    {
        private readonly InternalBus _bus;

        public PlainChannelFactory(InternalBus bus)
        {
            _bus = bus;
        }

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
