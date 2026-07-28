using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Proactor;

/// <summary>
/// Regression for PR #4254 review finding 1 (the message-loss half of the disposal-timing change). The pump
/// releases the transform pipeline (mapper + transforms) back to its factories after building the request.
/// Before the fix that release ran <b>inside</b> the try whose catch-all wraps everything in a
/// <see cref="MessageMappingException"/>, so a throwing mapper/transform release — a user
/// <c>DisposeAsync</c>/<c>Release</c> that throws, or MS DI's sync scope <c>Dispose</c> of an
/// <c>IAsyncDisposable</c>-only service — was reclassified as an <b>Unacceptable</b> message. The handler
/// never ran, a message that mapped perfectly was rejected and discarded, and after
/// <c>UnacceptableMessageLimit</c> such messages the pump shut down: a cleanup-path bug became silent
/// message loss on the default consumer configuration.
///
/// The release failure must be logged and swallowed, not surfaced into the mapping path. This drives the
/// Proactor pump with a valid event and a mapper factory whose <c>ReleaseAsync</c> throws, and asserts the
/// handler still received the event. Proved RED against the pre-fix <c>await using</c>-inside-the-try shape.
/// </summary>
public class AsyncMessagePumpMapperReleaseThrowsTests
{
    private const string ChannelName = "myChannel";
    private readonly RoutingKey _routingKey = new("MyTopic");
    private readonly InternalBus _bus = new();
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly IAmAMessagePump _messagePump;
    private readonly MyEvent _myEvent = new();
    private readonly IDictionary<string, string> _receivedMessages = new Dictionary<string, string>();

    public AsyncMessagePumpMapperReleaseThrowsTests()
    {
        var subscriberRegistry = new SubscriberRegistry();
        subscriberRegistry.RegisterAsync<MyEvent, MyEventHandlerAsync>();

        var handlerFactory = new SimpleHandlerFactoryAsync(_ => new MyEventHandlerAsync(_receivedMessages));
        var commandProcessor = new CommandProcessor(
            subscriberRegistry,
            handlerFactory,
            new InMemoryRequestContextFactory(),
            new PolicyRegistry(),
            new ResiliencePipelineRegistry<string>(),
            new InMemorySchedulerFactory());

        PipelineBuilder<MyEvent>.ClearPipelineCache();

        var channel = new ChannelAsync(new(ChannelName), _routingKey,
            new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, ackTimeout: TimeSpan.FromMilliseconds(1000)));

        //a mapper factory whose release throws, standing in for a user DisposeAsync/Release that faults or
        //MS DI's sync scope Dispose of an IAsyncDisposable-only mapper
        var messageMapperRegistry = new MessageMapperRegistry(
            null,
            new ThrowingOnReleaseMessageMapperFactoryAsync(_ => new MyEventMessageMapperAsync()));
        messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();

        _messagePump = new ServiceActivator.Proactor(commandProcessor, _ => typeof(MyEvent),
            messageMapperRegistry, new EmptyMessageTransformerFactoryAsync(), new InMemoryRequestContextFactory(), channel)
        {
            Channel = channel, TimeOut = TimeSpan.FromMilliseconds(5000)
        };

        var message = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_EVENT),
            new MessageBody(JsonSerializer.Serialize(_myEvent)));
        channel.Enqueue(message);
        channel.Enqueue(MessageFactory.CreateQuitMessage(_routingKey));
    }

    [Fact]
    public void When_a_mapper_release_throws_the_mapped_message_is_still_dispatched()
    {
        _messagePump.Run();

        //the message mapped, so it must reach the handler — not be rejected as Unacceptable because cleanup threw
        Assert.Contains(nameof(MyEventHandlerAsync), _receivedMessages);
        Assert.Equal(_myEvent.Id, _receivedMessages[nameof(MyEventHandlerAsync)]);
    }

    private sealed class ThrowingOnReleaseMessageMapperFactoryAsync(Func<Type, IAmAMessageMapperAsync> factoryMethod)
        : IAmAMessageMapperFactoryAsync
    {
        public Lease<IAmAMessageMapperAsync>? Create(Type messageMapperType) => new Lease<IAmAMessageMapperAsync>(factoryMethod(messageMapperType));

        public void Release(Lease<IAmAMessageMapperAsync>? lease) =>
            throw new InvalidOperationException("mapper release failed");

        public ValueTask ReleaseAsync(Lease<IAmAMessageMapperAsync>? lease) =>
            throw new InvalidOperationException("mapper release failed");
    }
}
