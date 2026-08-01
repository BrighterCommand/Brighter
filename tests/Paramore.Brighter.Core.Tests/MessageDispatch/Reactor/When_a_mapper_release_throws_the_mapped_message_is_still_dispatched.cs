using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.ServiceActivator;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Reactor;

/// <summary>
/// Regression for PR #4254 review finding 1 (the message-loss half of the disposal-timing change), sync pump.
/// The Reactor releases the transform pipeline (mapper + transforms) back to its factories after building the
/// request. Before the fix that release ran <b>inside</b> the try whose catch-all wraps everything in a
/// <see cref="MessageMappingException"/>, so a throwing mapper/transform <c>Release</c> reclassified a message
/// that mapped perfectly as <b>Unacceptable</b>: the handler never ran, the message was rejected and
/// discarded, and after <c>UnacceptableMessageLimit</c> the pump shut down.
///
/// The release failure must be logged and swallowed. This drives the Reactor pump with a valid event and a
/// mapper factory whose <c>Release</c> throws, and asserts the handler still received the event. Proved RED
/// against the pre-fix <c>using</c>-inside-the-try shape.
/// </summary>
public class MessagePumpMapperReleaseThrowsTests
{
    private readonly RoutingKey _routingKey = new("MyTopic");
    private readonly InternalBus _bus = new();
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly IAmAMessagePump _messagePump;
    private readonly MyEvent _myEvent = new();
    private readonly IDictionary<string, string> _receivedMessages = new Dictionary<string, string>();

    public MessagePumpMapperReleaseThrowsTests()
    {
        var subscriberRegistry = new SubscriberRegistry();
        subscriberRegistry.Register<MyEvent, MyEventHandler>();

        var handlerFactory = new SimpleHandlerFactorySync(_ => new MyEventHandler(_receivedMessages));

        var commandProcessor = new CommandProcessor(
            subscriberRegistry,
            handlerFactory,
            new InMemoryRequestContextFactory(),
            new PolicyRegistry(),
            new ResiliencePipelineRegistry<string>(),
            new InMemorySchedulerFactory());

        PipelineBuilder<MyEvent>.ClearPipelineCache();

        var channel = new Channel(new("myChannel"), _routingKey,
            new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, ackTimeout: TimeSpan.FromMilliseconds(1000)));

        //a mapper factory whose Release throws, standing in for a user Dispose/Release that faults
        var messageMapperRegistry = new MessageMapperRegistry(
            new ThrowingOnReleaseMessageMapperFactory(_ => new MyEventMessageMapper()),
            null);
        messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

        _messagePump = new ServiceActivator.Reactor(commandProcessor, _ => typeof(MyEvent),
            messageMapperRegistry, null, new InMemoryRequestContextFactory(), channel)
        {
            Channel = channel, TimeOut = TimeSpan.FromMilliseconds(5000)
        };

        var message = new Message(new MessageHeader(_myEvent.Id, _routingKey, MessageType.MT_EVENT),
            new MessageBody(JsonSerializer.Serialize(_myEvent, JsonSerialisationOptions.Options)));
        channel.Enqueue(message);
        channel.Enqueue(MessageFactory.CreateQuitMessage(_routingKey));
    }

    [Fact]
    public void When_a_mapper_release_throws_the_mapped_message_is_still_dispatched()
    {
        _messagePump.Run();

        //the message mapped, so it must reach the handler — not be rejected as Unacceptable because cleanup threw
        Assert.Contains(nameof(MyEventHandler), _receivedMessages);
        Assert.Equal(_myEvent.Id, _receivedMessages[nameof(MyEventHandler)]);
    }

    private sealed class ThrowingOnReleaseMessageMapperFactory(Func<Type, IAmAMessageMapper> factoryMethod)
        : IAmAMessageMapperFactory
    {
        public Lease<IAmAMessageMapper>? Create(Type messageMapperType) => new Lease<IAmAMessageMapper>(factoryMethod(messageMapperType));

        public void Release(Lease<IAmAMessageMapper>? lease) =>
            throw new InvalidOperationException("mapper release failed");
    }
}
