using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.Testing;
using Paramore.Brighter.ServiceActivator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Reactor
{
    // Regression for #4075: Receive() must not return until every consumer is Open,
    // so an immediately-following Shut()/End() always reaches a running performer.
    public class DispatcherShutImmediatelyAfterReceiveTests
    {
        private const string ChannelName = "fakeChannel";
        private static readonly RoutingKey RoutingKey = new("fakekey");

        [Fact]
        public void When_Receive_Returns_All_Consumers_Are_Open()
        {
            for (var iteration = 0; iteration < 50; iteration++)
            {
                var dispatcher = BuildDispatcher(noOfPerformers: 5, subscriptionName: $"open-{iteration}");

                dispatcher.Receive();

                try
                {
                    var notOpen = dispatcher.Consumers
                        .Where(c => c.State != ConsumerState.Open)
                        .Select(c => c.Name.ToString())
                        .ToArray();

                    Assert.True(notOpen.Length == 0,
                        $"Iteration {iteration}: Receive() returned with consumers still Shut: [{string.Join(", ", notOpen)}]");
                }
                finally
                {
                    DrainDispatcher(dispatcher);
                }
            }
        }

        [Fact]
        public async Task When_Shut_Called_Immediately_After_Receive_End_Completes()
        {
            var hangTimeout = TimeSpan.FromSeconds(10);

            for (var iteration = 0; iteration < 25; iteration++)
            {
                var subscriptionName = new SubscriptionName($"shut-{iteration}");
                var dispatcher = BuildDispatcher(noOfPerformers: 5, subscriptionName: subscriptionName.Value);
                var subscription = dispatcher.Subscriptions.Single(s => s.Name == subscriptionName);

                dispatcher.Receive();
                dispatcher.Shut(subscription);

                var endTask = dispatcher.End();
                var winner = await Task.WhenAny(endTask, Task.Delay(hangTimeout));

                Assert.True(ReferenceEquals(winner, endTask),
                    $"Iteration {iteration}: End() did not complete within {hangTimeout}; orphaned performers leaked. " +
                    $"State={dispatcher.State}, openConsumers={dispatcher.Consumers.Count(c => c.State == ConsumerState.Open)}");

                Assert.Equal(DispatcherState.DS_STOPPED, dispatcher.State);
                Assert.Empty(dispatcher.Consumers);
            }
        }

        [Fact]
        public async Task When_End_Called_Immediately_After_Receive_End_Completes()
        {
            var hangTimeout = TimeSpan.FromSeconds(10);

            for (var iteration = 0; iteration < 25; iteration++)
            {
                var dispatcher = BuildDispatcher(noOfPerformers: 5, subscriptionName: $"end-{iteration}");

                dispatcher.Receive();

                var endTask = dispatcher.End();
                var winner = await Task.WhenAny(endTask, Task.Delay(hangTimeout));

                Assert.True(ReferenceEquals(winner, endTask),
                    $"Iteration {iteration}: End() did not complete within {hangTimeout}; orphaned performers leaked. " +
                    $"State={dispatcher.State}, openConsumers={dispatcher.Consumers.Count(c => c.State == ConsumerState.Open)}");

                Assert.Equal(DispatcherState.DS_STOPPED, dispatcher.State);
                Assert.Empty(dispatcher.Consumers);
            }
        }

        private static Dispatcher BuildDispatcher(int noOfPerformers, string subscriptionName)
        {
            var bus = new InternalBus();
            var commandProcessor = new SpyCommandProcessor();

            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
                null);
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

            var subscription = new Subscription<MyEvent>(
                new SubscriptionName(subscriptionName),
                noOfPerformers: noOfPerformers,
                timeOut: TimeSpan.FromMilliseconds(100),
                channelFactory: new InMemoryChannelFactory(bus, new FakeTimeProvider()),
                channelName: new ChannelName(ChannelName),
                messagePumpType: MessagePumpType.Reactor,
                routingKey: RoutingKey
            );

            return new Dispatcher(commandProcessor, new List<Subscription> { subscription }, messageMapperRegistry);
        }

        private static void DrainDispatcher(Dispatcher dispatcher)
        {
            if (dispatcher.State != DispatcherState.DS_RUNNING)
                return;

            foreach (var subscription in dispatcher.Subscriptions)
                dispatcher.Shut(subscription);

            dispatcher.End().Wait(TimeSpan.FromSeconds(10));
        }
    }
}
