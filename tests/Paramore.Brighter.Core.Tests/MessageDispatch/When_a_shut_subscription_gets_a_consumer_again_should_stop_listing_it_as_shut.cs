#region Licence

/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.Testing;

namespace Paramore.Brighter.Core.Tests.MessageDispatch;

public class DispatcherShutSubscriptionGetsConsumerTests
{
    [Test]
    public async Task When_a_shut_subscription_gets_a_consumer_again_should_stop_listing_it_as_shut()
    {
        // Arrange
        using var channelFactory = new InMemoryUnopenedConsumerChannelFactory(throwOnDispose: false);
        using var dispatcher = NewRunningDispatcher(channelFactory);
        var orders = new SubscriptionName("orders");

        try
        {
            dispatcher.Shut(orders);
            var shutSnapshot = dispatcher.GetLifecycleSnapshot();

            // Act
            dispatcher.Open(orders);
            var openedSnapshot = dispatcher.GetLifecycleSnapshot();

            dispatcher.Shut(orders);
            dispatcher.SetActivePerformers("orders", 2);
            var scaledUpSnapshot = dispatcher.GetLifecycleSnapshot();

            // Assert
            await Assert.That(shutSnapshot.ShutSubscriptions.ToArray()).IsEqualTo([orders]);
            await Assert.That(openedSnapshot.ShutSubscriptions).IsEmpty();
            await Assert.That(scaledUpSnapshot.ShutSubscriptions).IsEmpty();
        }
        finally
        {
            await dispatcher.End().WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [Test]
    public async Task When_a_shut_subscription_gets_no_consumer_should_keep_listing_it_as_shut()
    {
        // Arrange
        using var channelFactory = new InMemoryUnopenedConsumerChannelFactory(throwOnDispose: false);
        using var dispatcher = NewRunningDispatcher(channelFactory);
        var orders = new SubscriptionName("orders");

        try
        {
            dispatcher.Shut(orders);

            // Act
            dispatcher.SetActivePerformers("orders", 1);
            var snapshot = dispatcher.GetLifecycleSnapshot();

            // Assert
            await Assert.That(snapshot.ShutSubscriptions.ToArray()).IsEqualTo([orders]);
        }
        finally
        {
            await dispatcher.End().WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    private static Dispatcher NewRunningDispatcher(IAmAChannelFactory channelFactory)
    {
        var registry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
            new SimpleMessageMapperFactoryAsync(_ => new MyEventMessageMapperAsync()));
        registry.Register<MyEvent, MyEventMessageMapper>();
        registry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();
        var dispatcher = new Dispatcher(
            new SpyCommandProcessor(),
            [NewSubscription("orders", channelFactory), NewSubscription("billing", channelFactory)],
            registry,
            registry);
        dispatcher.Receive();
        return dispatcher;
    }

    private static Subscription NewSubscription(string name, IAmAChannelFactory channelFactory)
        => new Subscription<MyEvent>(
            new SubscriptionName(name),
            channelName: new ChannelName($"{name}-channel"),
            routingKey: new RoutingKey($"{name}-topic"),
            channelFactory: channelFactory,
            messagePumpType: MessagePumpType.Reactor,
            noOfPerformers: 1,
            timeOut: TimeSpan.FromMilliseconds(10),
            emptyChannelDelay: TimeSpan.FromMilliseconds(10));
}
