#region Licence

/* The MIT License (MIT)
Copyright © 2026 gabisonia <irakli.gabisonia94@gmail.com>

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

public class DispatcherConcurrentShutdownTests
{
    [Theory]
    [InlineData(MessagePumpType.Reactor, "receive", false)]
    [InlineData(MessagePumpType.Proactor, "receive", false)]
    [InlineData(MessagePumpType.Reactor, "open", false)]
    [InlineData(MessagePumpType.Proactor, "open", false)]
    [InlineData(MessagePumpType.Reactor, "scale", false)]
    [InlineData(MessagePumpType.Proactor, "scale", false)]
    [InlineData(MessagePumpType.Reactor, "receive", true)]
    [InlineData(MessagePumpType.Proactor, "receive", true)]
    [InlineData(MessagePumpType.Reactor, "open", true)]
    [InlineData(MessagePumpType.Proactor, "open", true)]
    [InlineData(MessagePumpType.Reactor, "scale", true)]
    [InlineData(MessagePumpType.Proactor, "scale", true)]
    public async Task When_shutdown_races_with_consumer_creation_should_drain_and_allow_restart(
        MessagePumpType pumpType, string operation, bool throwOnDispose)
    {
        // Arrange
        using var factory = new InMemoryUnopenedConsumerChannelFactory(throwOnDispose);
        var subscription = CreateSubscription(factory, pumpType, "existing");
        using var dispatcher = CreateDispatcher(subscription);
        if (operation != "receive")
            dispatcher.Receive();

        var initialJobs = dispatcher.Consumers.Select(consumer => consumer.Job!).ToArray();
        factory.PauseNextCreation();
        var opening = Task.Run(() => ApplyOperation(dispatcher, factory, pumpType, operation));
        try
        {
            await factory.CreationPaused.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var pausedTransport = factory.Transports.Last();

            // Act
            var shutdown = dispatcher.End();
            var completedEarly = await Task.WhenAny(shutdown, Task.Delay(TimeSpan.FromMilliseconds(100))) == shutdown;
            factory.ResumeCreation();
            await opening.WaitAsync(TimeSpan.FromSeconds(10));
            await shutdown.WaitAsync(TimeSpan.FromSeconds(10));

            // Assert
            Assert.False(completedEarly, "Shutdown completed while an accepted consumer was still being created.");
            Assert.Equal(0, pausedTransport.ReceiveCount);
            AssertStoppedAndDisposed(dispatcher, factory);
            Assert.All(initialJobs, job => Assert.Throws<ObjectDisposedException>(() => ((IAsyncResult)job).AsyncWaitHandle));

            ApplyOperation(dispatcher, factory, pumpType, operation);
            Assert.Equal(DispatcherState.DS_RUNNING, dispatcher.State);
            Assert.NotEmpty(dispatcher.Consumers);
            Assert.All(dispatcher.Consumers, consumer => Assert.NotNull(consumer.Job));
            await dispatcher.End().WaitAsync(TimeSpan.FromSeconds(10));
            AssertStoppedAndDisposed(dispatcher, factory);
        }
        finally
        {
            factory.ResumeCreation();
            await opening.WaitAsync(TimeSpan.FromSeconds(10));
            foreach (var consumer in dispatcher.Consumers)
                consumer.Shut(consumer.Subscription.RoutingKey);
            try
            { await dispatcher.End().WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (InvalidOperationException) when (throwOnDispose) { }
        }
    }

    [Theory]
    [InlineData(MessagePumpType.Reactor, "receive")]
    [InlineData(MessagePumpType.Proactor, "receive")]
    [InlineData(MessagePumpType.Reactor, "open")]
    [InlineData(MessagePumpType.Proactor, "open")]
    [InlineData(MessagePumpType.Reactor, "scale")]
    [InlineData(MessagePumpType.Proactor, "scale")]
    public async Task When_shutdown_is_in_progress_should_reject_new_consumer_operations(
        MessagePumpType pumpType, string operation)
    {
        // Arrange
        using var factory = new InMemoryUnopenedConsumerChannelFactory(false);
        var subscription = CreateSubscription(factory, pumpType, "existing");
        using var dispatcher = CreateDispatcher(subscription);
        dispatcher.Receive();
        factory.PauseNextCreation();
        var opening = Task.Run(() => dispatcher.Open(CreateSubscription(factory, pumpType, "pending")));
        try
        {
            await factory.CreationPaused.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var shutdown = dispatcher.End();
            var transportsBefore = factory.Transports.Count;
            var performersBefore = subscription.NoOfPerformers;

            // Act
            var error = Record.Exception(() => ApplyOperation(dispatcher, factory, pumpType, operation));

            // Assert
            Assert.IsType<InvalidOperationException>(error);
            Assert.Equal(transportsBefore, factory.Transports.Count);
            Assert.Equal(performersBefore, subscription.NoOfPerformers);
            Assert.DoesNotContain(dispatcher.Subscriptions, item => item.Name == "additional");
            factory.ResumeCreation();
            await opening.WaitAsync(TimeSpan.FromSeconds(10));
            await shutdown.WaitAsync(TimeSpan.FromSeconds(10));
            AssertStoppedAndDisposed(dispatcher, factory);
        }
        finally
        {
            factory.ResumeCreation();
            await opening.WaitAsync(TimeSpan.FromSeconds(10));
            foreach (var consumer in dispatcher.Consumers)
                consumer.Shut(consumer.Subscription.RoutingKey);
            await dispatcher.End().WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [Theory]
    [InlineData(MessagePumpType.Reactor, false)]
    [InlineData(MessagePumpType.Proactor, false)]
    [InlineData(MessagePumpType.Reactor, true)]
    [InlineData(MessagePumpType.Proactor, true)]
    public async Task When_channel_creation_fails_should_release_created_consumers_and_allow_restart(
        MessagePumpType pumpType, bool throwOnDispose)
    {
        // Arrange
        using var factory = new InMemoryUnopenedConsumerChannelFactory(throwOnDispose) { FailOnCreationNumber = 2 };
        var subscription = CreateSubscription(factory, pumpType, "existing");
        subscription.SetNumberOfPerformers(3);
        using var dispatcher = CreateDispatcher(subscription);

        // Act
        var error = Assert.Throws<InvalidOperationException>(() => dispatcher.Receive());
        await dispatcher.End().WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        Assert.Equal("Channel creation failed.", error.Message);
        Assert.Single(factory.Transports);
        AssertStoppedAndDisposed(dispatcher, factory);
        factory.FailOnCreationNumber = null;
        dispatcher.Receive();
        Assert.Equal(3, dispatcher.Consumers.Count());
        await dispatcher.End().WaitAsync(TimeSpan.FromSeconds(10));
        AssertStoppedAndDisposed(dispatcher, factory);
    }

    [Theory]
    [InlineData("receive", false)]
    [InlineData("open", false)]
    [InlineData("scale", false)]
    [InlineData("receive", true)]
    [InlineData("open", true)]
    [InlineData("scale", true)]
    public async Task When_dispatcher_is_disposed_should_reject_new_consumer_operations(string operation, bool asyncDispose)
    {
        // Arrange
        using var factory = new InMemoryUnopenedConsumerChannelFactory(false);
        const MessagePumpType pumpType = MessagePumpType.Proactor;
        using var dispatcher = CreateDispatcher(CreateSubscription(factory, pumpType, "existing"));
        dispatcher.Receive();
        if (asyncDispose)
            await dispatcher.DisposeAsync();
        else
            dispatcher.Dispose();
        var transportsBefore = factory.Transports.Count;

        // Act
        var error = Record.Exception(() => ApplyOperation(dispatcher, factory, pumpType, operation));

        // Assert
        try
        {
            Assert.IsType<ObjectDisposedException>(error);
            Assert.Equal(transportsBefore, factory.Transports.Count);
            AssertStoppedAndDisposed(dispatcher, factory);
        }
        finally
        {
            foreach (var consumer in dispatcher.Consumers)
                consumer.Shut(consumer.Subscription.RoutingKey);
            await dispatcher.End().WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    private static void ApplyOperation(Dispatcher dispatcher, InMemoryUnopenedConsumerChannelFactory factory,
        MessagePumpType pumpType, string operation)
    {
        switch (operation)
        {
            case "receive":
                dispatcher.Receive();
                break;
            case "open":
                dispatcher.Open(CreateSubscription(factory, pumpType, "additional"));
                break;
            case "scale":
                dispatcher.SetActivePerformers("existing", 2);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }

    private static Subscription CreateSubscription(InMemoryUnopenedConsumerChannelFactory factory,
        MessagePumpType pumpType, string name) => new Subscription<MyEvent>(
        new SubscriptionName(name), channelName: new ChannelName(name), routingKey: new RoutingKey(name),
        channelFactory: factory, messagePumpType: pumpType, timeOut: TimeSpan.FromMilliseconds(10),
        emptyChannelDelay: TimeSpan.FromMilliseconds(10));

    private static Dispatcher CreateDispatcher(Subscription subscription)
    {
        var registry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
            new SimpleMessageMapperFactoryAsync(_ => new MyEventMessageMapperAsync()));
        registry.Register<MyEvent, MyEventMessageMapper>();
        registry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();
        return new Dispatcher(new SpyCommandProcessor(), [subscription], registry, registry);
    }

    private static void AssertStoppedAndDisposed(Dispatcher dispatcher, InMemoryUnopenedConsumerChannelFactory factory)
    {
        Assert.Equal(DispatcherState.DS_STOPPED, dispatcher.State);
        Assert.Empty(dispatcher.Consumers);
        Assert.All(factory.Transports, transport => Assert.Equal(1, transport.DisposeCount));
    }
}
