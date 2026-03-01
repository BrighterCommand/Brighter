using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.Testing;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.ServiceActivator;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Proactor;

public class MessageDispatchInvalidMessageActionAsyncTests
{
    private readonly RoutingKey _routingKey = new("myTopic");
    private readonly RoutingKey _invalidMessageRoutingKey = new("myInvalidMessageRoutingKey");
    private readonly InternalBus _bus = new();
    private readonly FakeTimeProvider _timeProvider = new();
    private Dispatcher _dispatcher;

    public MessageDispatchInvalidMessageActionAsyncTests()
    {
        // Arrange: Set up a message mapper that throws InvalidMessageAction on deserialization failure
        var subscriberRegistry = new SubscriberRegistry();
        subscriberRegistry.Register<MyRejectedEvent, MyRejectedEventHandler>();

        var handlerFactory = new SimpleHandlerFactory(
            (type) => new MyRejectedEventHandler(),
            (type) => throw new ConfigurationException()
        );

        // Use a mapper that throws InvalidMessageAction to simulate deserialization failure
        var mapperFactory = new SimpleMessageMapperFactoryAsync((r) => new MyInvalidMessageMapper());
        var messageMapperRegistry = new MessageMapperRegistry(null, mapperFactory, null, null);
        messageMapperRegistry.RegisterAsync<MyRejectedEvent, MyInvalidMessageMapper>();

        var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>();
        resiliencePipelineRegistry.AddBrighterDefault();

        var commandProcessor = new CommandProcessor(
            subscriberRegistry,
            handlerFactory,
            new InMemoryRequestContextFactory(),
            new PolicyRegistry(),
            resiliencePipelineRegistry,
            new InMemorySchedulerFactory()
        );

        var subscription = new InMemorySubscription<MyRejectedEvent>(
            new SubscriptionName("test"),
            noOfPerformers: 1,
            timeOut: TimeSpan.FromMilliseconds(1000),
            channelFactory: new InMemoryChannelFactory(_bus, _timeProvider),
            channelName: new ChannelName("myChannel"),
            messagePumpType: MessagePumpType.Proactor,
            routingKey: _routingKey
        );

        subscription.InvalidMessageRoutingKey = _invalidMessageRoutingKey;

        _dispatcher = new Dispatcher(
            commandProcessor,
            new List<Subscription> { subscription },
            messageMapperRegistryAsync: messageMapperRegistry,
            requestContextFactory: new InMemoryRequestContextFactory()
        );

        // Act: Send a message that will fail deserialization
        var @event = new MyRejectedEvent(Id.Random());
        var message = new MyRejectedEventHandlerMessageMapper().MapToMessage(@event, new Publication{Topic = _routingKey});
        _bus.Enqueue(message);

        _dispatcher.Receive();
    }

    [Fact]
    public async Task When_a_message_mapper_throws_invalid_message_action()
    {
        // Wait for the message to be processed (moved to invalid message topic) before stopping
        // Without this, End() can enqueue a QUIT message that the pump reads before the data message
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!_bus.Stream(_invalidMessageRoutingKey).Any() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        await _dispatcher.End();

        // Assert: The message should be removed from the source queue
        Assert.Empty(_bus.Stream(_routingKey));

        // Assert: The message should appear in the invalid message channel
        Assert.NotEmpty(_bus.Stream(_invalidMessageRoutingKey));
        var message = _bus.Dequeue(_invalidMessageRoutingKey);

        // Assert: The message should include rejection metadata
        var rejectionReason = $"Message rejected reason: {RejectionReason.Unacceptable} Description: Failed to deserialize message";
        Assert.Equal(rejectionReason, message.Header.Bag[Message.RejectionReasonHeaderName]);
    }
}
