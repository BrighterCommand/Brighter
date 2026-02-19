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
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class ChannelFactorySchedulerTests
{
    [Fact]
    public void Should_set_scheduler_on_channel_factory_that_implements_scheduler_interface()
    {
        // Arrange — configure AddConsumers with an InMemoryChannelFactory (which implements IAmAChannelFactoryWithScheduler)
        var bus = new InternalBus();
        var channelFactory = new InMemoryChannelFactory(bus, TimeProvider.System);

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

        // Assert — the channel factory should now have its Scheduler property set from DI
        Assert.NotNull(dispatcher);
        Assert.NotNull(channelFactory.Scheduler);
        Assert.IsAssignableFrom<IAmAMessageScheduler>(channelFactory.Scheduler);
    }

    [Fact]
    public void Should_set_custom_scheduler_on_channel_factory_when_UseScheduler_configured()
    {
        // Arrange — configure with a custom scheduler factory
        var bus = new InternalBus();
        var channelFactory = new InMemoryChannelFactory(bus, TimeProvider.System);
        var customSchedulerFactory = new StubSchedulerFactory();

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
            .UseScheduler(customSchedulerFactory)
            .AutoFromAssemblies();

        var provider = services.BuildServiceProvider();

        // Act — resolve IDispatcher
        var dispatcher = provider.GetRequiredService<IDispatcher>();

        // Assert — the channel factory should have the custom scheduler set
        Assert.NotNull(dispatcher);
        Assert.NotNull(channelFactory.Scheduler);
        Assert.IsType<StubScheduler>(channelFactory.Scheduler);
    }

    private class StubSchedulerFactory : IAmAMessageSchedulerFactory, IAmARequestSchedulerFactory
    {
        public IAmAMessageScheduler Create(IAmACommandProcessor processor) => new StubScheduler();
        public IAmARequestSchedulerSync CreateSync(IAmACommandProcessor processor) => throw new NotImplementedException();
        public IAmARequestSchedulerAsync CreateAsync(IAmACommandProcessor processor) => throw new NotImplementedException();
    }

    private class StubScheduler : IAmAMessageScheduler;
}
