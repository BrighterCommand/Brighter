using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.Testing;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Proactor;

/// <summary>Regression test for issue #4071: dispatcher must not deadlock on a limited-concurrency TaskScheduler.</summary>
public class DispatcherOnLimitedConcurrencySchedulerTests
{
    private const string Topic = "myTopic";
    private const string ChannelName = "myChannel";

    [Fact]
    public void When_Dispatcher_Started_On_Limited_Concurrency_Scheduler_Should_Not_Deadlock()
    {
        var routingKey = new RoutingKey(Topic);
        var bus = new InternalBus();
        var consumer = new InMemoryMessageConsumer(routingKey, bus, TimeProvider.System, ackTimeout: TimeSpan.FromMilliseconds(1000));

        IAmAChannelSync channel = new Channel(new(ChannelName), new(Topic), consumer, 6);
        IAmACommandProcessor commandProcessor = new SpyCommandProcessor();

        var messageMapperRegistry = new MessageMapperRegistry(
            null,
            new SimpleMessageMapperFactoryAsync(_ => new MyEventMessageMapperAsync()));
        messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();

        var subscription = new Subscription<MyEvent>(
            new SubscriptionName("test"),
            noOfPerformers: 3,
            timeOut: TimeSpan.FromMilliseconds(100),
            channelFactory: new InMemoryChannelFactory(bus, TimeProvider.System),
            channelName: new ChannelName("fakeChannel"),
            messagePumpType: MessagePumpType.Proactor,
            routingKey: routingKey
        );
        var dispatcher = new Dispatcher(commandProcessor, new List<Subscription> { subscription }, messageMapperRegistryAsync: messageMapperRegistry);

        var @event = new MyEvent();
        var message = new MyEventMessageMapperAsync()
            .MapToMessageAsync(@event, new Publication { Topic = subscription.RoutingKey })
            .GetAwaiter()
            .GetResult();

        for (var i = 0; i < 6; i++)
            channel.Enqueue(message);

        Assert.Equal(DispatcherState.DS_AWAITING, dispatcher.State);

        var pair = new ConcurrentExclusiveSchedulerPair();
        var factory = new TaskFactory(pair.ExclusiveScheduler);

        var completed = factory.StartNew(() =>
        {
            dispatcher.Receive();
            return dispatcher.End();
        }).Unwrap();

        var finishedInTime = completed.Wait(TimeSpan.FromSeconds(30));
        Assert.True(finishedInTime, "Dispatcher deadlocked when started on a limited-concurrency TaskScheduler");
        Assert.Equal(DispatcherState.DS_STOPPED, dispatcher.State);
    }
}
