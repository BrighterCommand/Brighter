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
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.Testing;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch;

public class DispatcherRestartStoppedPerformerTests
{
    private const string ORDERS = "orders";
    private const string BILLING = "billing";

    [Theory]
    [InlineData(MessagePumpType.Reactor)]
    [InlineData(MessagePumpType.Proactor)]
    public async Task When_a_performer_stops_itself_should_restart_it_when_active_performers_are_set(MessagePumpType pumpType)
    {
        // Arrange
        var bus = new InternalBus();
        var channelFactory = new InMemoryChannelFactory(bus, new FakeTimeProvider());
        using var dispatcher = NewRunningDispatcher(channelFactory, bus, pumpType);

        try
        {
            // The orders performer hits its unacceptable message limit and stops itself; billing keeps the dispatcher running
            await WaitUntilAsync(() => ConsumersFor(dispatcher, ORDERS).Length == 0);
            Assert.Single(ConsumersFor(dispatcher, BILLING));
            Assert.Equal(DispatcherState.DS_RUNNING, dispatcher.State);

            // Act
            dispatcher.SetActivePerformers(ORDERS, 1);

            // Assert
            Assert.Single(ConsumersFor(dispatcher, ORDERS));
            Assert.Single(ConsumersFor(dispatcher, BILLING));
        }
        finally
        {
            await dispatcher.End().WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [Fact]
    public async Task When_a_performer_stops_itself_should_only_start_the_missing_performers_when_scaling_up()
    {
        // Arrange
        var bus = new InternalBus();
        var channelFactory = new InMemoryChannelFactory(bus, new FakeTimeProvider());
        using var dispatcher = NewRunningDispatcher(channelFactory, bus, MessagePumpType.Reactor, ordersPerformers: 2, unacceptableMessages: 1);

        try
        {
            // Only one of the two orders performers receives the unacceptable message and stops itself
            await WaitUntilAsync(() => ConsumersFor(dispatcher, ORDERS).Length == 1);

            // Act
            dispatcher.SetActivePerformers(ORDERS, 2);

            // Assert
            Assert.Equal(2, ConsumersFor(dispatcher, ORDERS).Length);
        }
        finally
        {
            await dispatcher.End().WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [Fact]
    public async Task When_a_performer_stops_itself_should_not_start_more_when_set_to_zero()
    {
        // Arrange
        var bus = new InternalBus();
        var channelFactory = new InMemoryChannelFactory(bus, new FakeTimeProvider());
        using var dispatcher = NewRunningDispatcher(channelFactory, bus, MessagePumpType.Reactor);

        try
        {
            await WaitUntilAsync(() => ConsumersFor(dispatcher, ORDERS).Length == 0);

            // Act
            dispatcher.SetActivePerformers(ORDERS, 0);

            // Assert
            Assert.Empty(ConsumersFor(dispatcher, ORDERS));
            Assert.Single(ConsumersFor(dispatcher, BILLING));
        }
        finally
        {
            await dispatcher.End().WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    private static IAmAConsumer[] ConsumersFor(Dispatcher dispatcher, string subscriptionName)
        => dispatcher.Consumers.Where(consumer => consumer.Subscription.Name == subscriptionName).ToArray();

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("The expected dispatcher state was not reached.");
            await Task.Delay(20);
        }
    }

    private static Dispatcher NewRunningDispatcher(
        InMemoryChannelFactory channelFactory,
        InternalBus bus,
        MessagePumpType pumpType,
        int ordersPerformers = 1,
        int unacceptableMessages = 1)
    {
        var registry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
            new SimpleMessageMapperFactoryAsync(_ => new MyEventMessageMapperAsync()));
        registry.Register<MyEvent, MyEventMessageMapper>();
        registry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();

        var ordersTopic = new RoutingKey($"{ORDERS}-topic");
        for (var i = 0; i < unacceptableMessages; i++)
        {
            bus.Enqueue(new Message(
                new MessageHeader(Guid.NewGuid().ToString(), ordersTopic, MessageType.MT_UNACCEPTABLE),
                new MessageBody("")));
        }

        var dispatcher = new Dispatcher(
            new SpyCommandProcessor(),
            [
                NewSubscription(ORDERS, channelFactory, pumpType, ordersPerformers, unacceptableMessageLimit: 1),
                NewSubscription(BILLING, channelFactory, pumpType, 1, unacceptableMessageLimit: 0)
            ],
            registry,
            registry);
        dispatcher.Receive();
        return dispatcher;
    }

    private static Subscription NewSubscription(
        string name,
        IAmAChannelFactory channelFactory,
        MessagePumpType pumpType,
        int noOfPerformers,
        int unacceptableMessageLimit)
        => new Subscription<MyEvent>(
            new SubscriptionName(name),
            channelName: new ChannelName($"{name}-channel"),
            routingKey: new RoutingKey($"{name}-topic"),
            channelFactory: channelFactory,
            messagePumpType: pumpType,
            noOfPerformers: noOfPerformers,
            timeOut: TimeSpan.FromMilliseconds(10),
            emptyChannelDelay: TimeSpan.FromMilliseconds(10),
            unacceptableMessageLimit: unacceptableMessageLimit);
}
