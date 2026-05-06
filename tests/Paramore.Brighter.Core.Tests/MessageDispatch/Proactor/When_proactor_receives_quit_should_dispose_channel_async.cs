using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Testing;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.ServiceActivator;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Proactor;
public class ProactorQuitAsyncDisposalTests
{
    private readonly RoutingKey _routingKey = new("test.topic");
    private readonly InternalBus _bus = new();
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly Task _performerTask;
    private readonly TrackingChannelAsync _trackingChannel;
    public ProactorQuitAsyncDisposalTests()
    {
        // Arrange
        var commandProcessor = new SpyCommandProcessor();
        var consumer = new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, ackTimeout: TimeSpan.FromMilliseconds(1000));
        _trackingChannel = new TrackingChannelAsync(new ChannelName("test-channel"), _routingKey, consumer);
        var messageMapperRegistry = new MessageMapperRegistry(null, new SimpleMessageMapperFactoryAsync(_ => new MyEventMessageMapperAsync()));
        messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();
        var messagePump = new Brighter.ServiceActivator.Proactor(commandProcessor, message => typeof(MyEvent), messageMapperRegistry, new EmptyMessageTransformerFactoryAsync(), new InMemoryRequestContextFactory(), _trackingChannel);
        messagePump.TimeOut = TimeSpan.FromMilliseconds(5000);
        // Enqueue a message followed by a quit
        var @event = new MyEvent();
        var message = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_EVENT), new MessageBody(JsonSerializer.Serialize(@event, JsonSerialisationOptions.Options)));
        _trackingChannel.Enqueue(message);
        // Act — start the performer and stop it (enqueues MT_QUIT)
        var performer = new Performer(_trackingChannel, messagePump);
        _performerTask = performer.Run();
        performer.Stop(_routingKey);
    }

    [Test]
    public async Task When_Proactor_Receives_Quit_Should_Dispose_Channel_Async()
    {
        // Assert — the performer task should complete cleanly
        await _performerTask.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(_performerTask.IsCompleted).IsTrue();
        await Assert.That(_performerTask.IsFaulted).IsFalse();
        await Assert.That(_performerTask.IsCanceled).IsFalse();
        // Assert — the Proactor should have called DisposeAsync, not Dispose
        await Assert.That(_trackingChannel.DisposeAsyncCalled).IsTrue();
    }
}

/// <summary>
/// A ChannelAsync subclass that tracks whether DisposeAsync or Dispose was called.
/// Used to verify the Proactor uses the async dispose path.
/// </summary>
internal sealed class TrackingChannelAsync(ChannelName channelName, RoutingKey routingKey, IAmAMessageConsumerAsync messageConsumer) : Paramore.Brighter.ChannelAsync(channelName, routingKey, messageConsumer), Paramore.Brighter.IAmAChannelAsync, Paramore.Brighter.IAmAChannel, System.IDisposable, System.IAsyncDisposable
{
    public bool DisposeAsyncCalled { get; private set; }

    public override async ValueTask DisposeAsync()
    {
        DisposeAsyncCalled = true;
        await base.DisposeAsync();
    }
}