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
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch;

public class DispatcherLifecycleSnapshotTests
{
    [Fact]
    public async Task When_a_running_dispatcher_is_asked_for_its_lifecycle_snapshot_should_copy_its_state_and_consumers()
    {
        // Arrange
        using var channelFactory = new InMemoryUnopenedConsumerChannelFactory(throwOnDispose: false);
        var registry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
            new SimpleMessageMapperFactoryAsync(_ => new MyEventMessageMapperAsync()));
        registry.Register<MyEvent, MyEventMessageMapper>();
        registry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();
        var alpha = new SubscriptionName("alpha");
        var beta = new SubscriptionName("beta");
        Subscription[] subscriptions =
        [
            NewSubscription(alpha, channelFactory, noOfPerformers: 2),
            NewSubscription(beta, channelFactory, noOfPerformers: 1),
            NewSubscription(alpha, channelFactory, noOfPerformers: 1)
        ];
        using var dispatcher = new Dispatcher(new SpyCommandProcessor(), subscriptions, registry, registry);

        try
        {
            dispatcher.Receive();

            var runningConsumers = dispatcher.Consumers.ToArray();

            // Act
            var snapshot = dispatcher.GetLifecycleSnapshot();
            dispatcher.SetActivePerformers("beta", 3);

            // Assert
            Assert.Equal(DispatcherState.DS_RUNNING, snapshot.State);
            Assert.False(snapshot.EndRequested);
            Assert.Empty(snapshot.ShutSubscriptions);
            Assert.Equal(0, snapshot.ConsumerOperationsInFlight);
            Assert.Equal(4, runningConsumers.Length);
            Assert.Equal(runningConsumers.ToHashSet(), snapshot.Consumers.ToHashSet());
            Assert.Equal(2, snapshot.ExpectedConsumers.Count);
            Assert.Equal(3, snapshot.ExpectedConsumers[alpha]);
            Assert.Equal(1, snapshot.ExpectedConsumers[beta]);
        }
        finally
        {
            await dispatcher.End().WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    private static Subscription NewSubscription(
        SubscriptionName name, IAmAChannelFactory channelFactory, int noOfPerformers)
        => new Subscription<MyEvent>(
            name,
            channelName: new ChannelName($"{name.Value}-channel"),
            routingKey: new RoutingKey($"{name.Value}-topic"),
            channelFactory: channelFactory,
            messagePumpType: MessagePumpType.Reactor,
            noOfPerformers: noOfPerformers,
            timeOut: TimeSpan.FromMilliseconds(10),
            emptyChannelDelay: TimeSpan.FromMilliseconds(10));
}
