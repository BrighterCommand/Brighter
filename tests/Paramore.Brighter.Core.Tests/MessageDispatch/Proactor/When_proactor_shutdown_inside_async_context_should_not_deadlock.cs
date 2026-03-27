using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.Tasks;
using Paramore.Brighter.Testing;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Proactor;

/// <summary>
/// Regression test for the deadlock scenario described in issue #3684.
/// A Proactor running inside BrighterAsyncContext.Run() must be able to
/// shut down cleanly — including awaiting DisposeAsync on the channel —
/// without deadlocking on continuations posted back to the single-threaded
/// scheduler.
/// </summary>
public class ProactorShutdownInsideAsyncContextTests
{
    [Fact]
    public void When_Proactor_Shuts_Down_Inside_BrighterAsyncContext_Should_Not_Deadlock()
    {
        // Arrange
        var routingKey = new RoutingKey("test.deadlock.topic");
        var bus = new InternalBus();
        var timeProvider = new FakeTimeProvider();

        var commandProcessor = new SpyCommandProcessor();
        var consumer = new InMemoryMessageConsumer(routingKey, bus, timeProvider, ackTimeout: TimeSpan.FromMilliseconds(1000));

        // Use a channel whose DisposeAsync does real async work (Task.Yield)
        // to force continuations back onto the scheduler
        var channel = new AsyncContinuationChannelAsync(
            new ChannelName("test-deadlock-channel"),
            routingKey,
            consumer
        );

        var messageMapperRegistry = new MessageMapperRegistry(
            null,
            new SimpleMessageMapperFactoryAsync(_ => new MyEventMessageMapperAsync()));
        messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();

        var messagePump = new Brighter.ServiceActivator.Proactor(
            commandProcessor,
            message => typeof(MyEvent),
            messageMapperRegistry,
            new EmptyMessageTransformerFactoryAsync(),
            new InMemoryRequestContextFactory(),
            channel
        );
        messagePump.TimeOut = TimeSpan.FromMilliseconds(5000);

        // Enqueue a message followed by a quit
        var @event = new MyEvent();
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_EVENT),
            new MessageBody(JsonSerializer.Serialize(@event, JsonSerialisationOptions.Options))
        );
        channel.Enqueue(message);

        // Act — run the entire Proactor lifecycle inside BrighterAsyncContext.Run().
        // If the async dispose path uses sync-over-async (.Wait() / .GetResult()),
        // this will deadlock because continuations cannot be scheduled back onto the
        // single-threaded context.
        var completed = Task.Run(() =>
        {
            BrighterAsyncContext.Run(async () =>
            {
                var performer = new Performer(channel, messagePump);
                var task = performer.Run();
                performer.Stop(routingKey);
                await task;
            });
        });

        // Assert — the performer must complete within a reasonable timeout.
        // A deadlock would cause this to hang indefinitely.
        bool finishedInTime = completed.Wait(TimeSpan.FromSeconds(30));
        Assert.True(finishedInTime, "Proactor shutdown deadlocked inside BrighterAsyncContext.Run()");
        Assert.True(channel.DisposeAsyncCalled, "Proactor should call DisposeAsync, not Dispose");
    }
}

/// <summary>
/// A ChannelAsync that performs real async work in DisposeAsync (Task.Yield)
/// to ensure continuations are posted back to the scheduler. This is the
/// scenario that triggers a deadlock if the dispose path uses sync-over-async.
/// </summary>
internal sealed class AsyncContinuationChannelAsync(
    ChannelName channelName,
    RoutingKey routingKey,
    IAmAMessageConsumerAsync messageConsumer)
    : ChannelAsync(channelName, routingKey, messageConsumer)
{
    public bool DisposeAsyncCalled { get; private set; }

    public override async ValueTask DisposeAsync()
    {
        DisposeAsyncCalled = true;

        // Force an async continuation — this is the key part.
        // If the caller is blocking synchronously on a single-threaded context,
        // this yield will deadlock because the continuation can't be scheduled.
        await Task.Yield();

        await base.DisposeAsync();
    }
}
