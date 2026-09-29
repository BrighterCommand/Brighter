using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.Testing;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Proactor;

public class DispatcherOnLimitedConcurrencySchedulerTests
{
    private const string Topic = "myTopic";
    private const string ChannelName = "myChannel";

    [Test]
    public async Task When_Dispatcher_Started_On_Limited_Concurrency_Scheduler_Should_Not_Deadlock()
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

        await Assert.That(dispatcher.State).IsEqualTo(DispatcherState.DS_AWAITING);

        var pair = new ConcurrentExclusiveSchedulerPair();
        var factory = new TaskFactory(pair.ExclusiveScheduler);

        var completed = factory.StartNew(() =>
        {
            dispatcher.Receive();
            return dispatcher.End();
        }).Unwrap();

        var finishedInTime = completed.Wait(TimeSpan.FromSeconds(30));
        await Assert.That(finishedInTime).IsTrue().Because("Dispatcher deadlocked when started on a limited-concurrency TaskScheduler");
        await Assert.That(dispatcher.State).IsEqualTo(DispatcherState.DS_STOPPED);
    }
}
