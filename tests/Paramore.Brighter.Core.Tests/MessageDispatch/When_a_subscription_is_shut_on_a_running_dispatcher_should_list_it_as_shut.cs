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
#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.Testing;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch;

public class DispatcherShutSubscriptionsTests
{
    [Fact]
    public async Task When_a_subscription_is_shut_on_a_running_dispatcher_should_list_it_as_shut()
    {
        // Arrange
        using var channelFactory = new InMemoryUnopenedConsumerChannelFactory(throwOnDispose: false);
        var registry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
            new SimpleMessageMapperFactoryAsync(_ => new MyEventMessageMapperAsync()));
        registry.Register<MyEvent, MyEventMessageMapper>();
        registry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();
        using var running = new Dispatcher(new SpyCommandProcessor(), [NewSubscription(channelFactory)], registry, registry);
        using var awaiting = new Dispatcher(new SpyCommandProcessor(), [NewSubscription(channelFactory)], registry, registry);
        var orders = new SubscriptionName("orders");

        try
        {
            // Act
            running.Receive();
            running.Shut(orders);
            var runningSnapshot = running.GetLifecycleSnapshot();

            awaiting.Shut(orders);
            var awaitingSnapshot = awaiting.GetLifecycleSnapshot();

            // Assert
            Assert.Equal([orders], runningSnapshot.ShutSubscriptions.ToArray());
            Assert.Empty(awaitingSnapshot.ShutSubscriptions);
        }
        finally
        {
            await running.End().WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    private static Subscription NewSubscription(IAmAChannelFactory channelFactory)
        => new Subscription<MyEvent>(
            new SubscriptionName("orders"),
            channelName: new ChannelName("orders-channel"),
            routingKey: new RoutingKey("orders-topic"),
            channelFactory: channelFactory,
            messagePumpType: MessagePumpType.Reactor,
            noOfPerformers: 1,
            timeOut: TimeSpan.FromMilliseconds(10),
            emptyChannelDelay: TimeSpan.FromMilliseconds(10));
}
