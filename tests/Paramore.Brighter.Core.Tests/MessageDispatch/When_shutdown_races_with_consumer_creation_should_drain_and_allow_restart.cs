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

namespace Paramore.Brighter.Core.Tests.MessageDispatch;

public class DispatcherConcurrentShutdownTests
{
    [Test]
    [Arguments(MessagePumpType.Reactor, "receive", false)]
    [Arguments(MessagePumpType.Proactor, "receive", false)]
    [Arguments(MessagePumpType.Reactor, "open", false)]
    [Arguments(MessagePumpType.Proactor, "open", false)]
    [Arguments(MessagePumpType.Reactor, "scale", false)]
    [Arguments(MessagePumpType.Proactor, "scale", false)]
    [Arguments(MessagePumpType.Reactor, "receive", true)]
    [Arguments(MessagePumpType.Proactor, "receive", true)]
    [Arguments(MessagePumpType.Reactor, "open", true)]
    [Arguments(MessagePumpType.Proactor, "open", true)]
    [Arguments(MessagePumpType.Reactor, "scale", true)]
    [Arguments(MessagePumpType.Proactor, "scale", true)]
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
            await Assert.That(completedEarly).IsFalse().Because("Shutdown completed while an accepted consumer was still being created.");
            await Assert.That(pausedTransport.ReceiveCount).IsEqualTo(0);
            await AssertStoppedAndDisposed(dispatcher, factory);
            foreach (var job in initialJobs)
            {
                await Assert.That(() => ((IAsyncResult)job).AsyncWaitHandle).ThrowsExactly<ObjectDisposedException>();
            }

            ApplyOperation(dispatcher, factory, pumpType, operation);
            await Assert.That(dispatcher.State).IsEqualTo(DispatcherState.DS_RUNNING);
            await Assert.That(dispatcher.Consumers).IsNotEmpty();
            foreach (var consumer in dispatcher.Consumers)
            {
                await Assert.That(consumer.Job).IsNotNull();
            }
            await dispatcher.End().WaitAsync(TimeSpan.FromSeconds(10));
            await AssertStoppedAndDisposed(dispatcher, factory);
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

    [Test]
    [Arguments(MessagePumpType.Reactor, "receive")]
    [Arguments(MessagePumpType.Proactor, "receive")]
    [Arguments(MessagePumpType.Reactor, "open")]
    [Arguments(MessagePumpType.Proactor, "open")]
    [Arguments(MessagePumpType.Reactor, "scale")]
    [Arguments(MessagePumpType.Proactor, "scale")]
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
            Exception? error = null;
            try { ApplyOperation(dispatcher, factory, pumpType, operation); } catch (Exception e) { error = e; }

            // Assert
            await Assert.That(error).IsTypeOf<InvalidOperationException>();
            await Assert.That(factory.Transports.Count).IsEqualTo(transportsBefore);
            await Assert.That(subscription.NoOfPerformers).IsEqualTo(performersBefore);
            await Assert.That(System.Linq.Enumerable.Any(dispatcher.Subscriptions, item => item.Name == "additional")).IsFalse();
            factory.ResumeCreation();
            await opening.WaitAsync(TimeSpan.FromSeconds(10));
            await shutdown.WaitAsync(TimeSpan.FromSeconds(10));
            await AssertStoppedAndDisposed(dispatcher, factory);
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

    [Test]
    [Arguments(MessagePumpType.Reactor, false)]
    [Arguments(MessagePumpType.Proactor, false)]
    [Arguments(MessagePumpType.Reactor, true)]
    [Arguments(MessagePumpType.Proactor, true)]
    public async Task When_channel_creation_fails_should_release_created_consumers_and_allow_restart(
        MessagePumpType pumpType, bool throwOnDispose)
    {
        // Arrange
        using var factory = new InMemoryUnopenedConsumerChannelFactory(throwOnDispose) { FailOnCreationNumber = 2 };
        var subscription = CreateSubscription(factory, pumpType, "existing");
        subscription.SetNumberOfPerformers(3);
        using var dispatcher = CreateDispatcher(subscription);

        // Act
        var error = (await Assert.That(() => dispatcher.Receive()).ThrowsExactly<InvalidOperationException>());
        await dispatcher.End().WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        await Assert.That(error.Message).IsEqualTo("Channel creation failed.");
        await Assert.That(factory.Transports).HasSingleItem();
        await AssertStoppedAndDisposed(dispatcher, factory);
        factory.FailOnCreationNumber = null;
        dispatcher.Receive();
        await Assert.That(dispatcher.Consumers.Count()).IsEqualTo(3);
        await dispatcher.End().WaitAsync(TimeSpan.FromSeconds(10));
        await AssertStoppedAndDisposed(dispatcher, factory);
    }

    [Test]
    [Arguments("receive", false)]
    [Arguments("open", false)]
    [Arguments("scale", false)]
    [Arguments("receive", true)]
    [Arguments("open", true)]
    [Arguments("scale", true)]
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
        Exception? error = null;
        try { ApplyOperation(dispatcher, factory, pumpType, operation); } catch (Exception e) { error = e; }

        // Assert
        try
        {
            await Assert.That(error).IsTypeOf<ObjectDisposedException>();
            await Assert.That(factory.Transports.Count).IsEqualTo(transportsBefore);
            await AssertStoppedAndDisposed(dispatcher, factory);
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

    private static async Task AssertStoppedAndDisposed(Dispatcher dispatcher, InMemoryUnopenedConsumerChannelFactory factory)
    {
        await Assert.That(dispatcher.State).IsEqualTo(DispatcherState.DS_STOPPED);
        await Assert.That(dispatcher.Consumers).IsEmpty();
        foreach (var transport in factory.Transports)
        {
            await Assert.That(transport.DisposeCount).IsEqualTo(1);
        }
    }
}
