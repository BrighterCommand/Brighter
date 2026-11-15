using System;
using System.Collections.Generic;
using System.Text;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

public class AzureServiceBusMessageMemoryTests
{
    private readonly AzureServiceBusMessageCreator _creator;

    public AzureServiceBusMessageMemoryTests()
    {
        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            subscriptionName: new SubscriptionName("test-sub"),
            channelName: new ChannelName("test-channel"),
            routingKey: new RoutingKey("test-topic"),
            messagePumpType: MessagePumpType.Reactor);

        _creator = new AzureServiceBusMessageCreator(subscription);
    }

    [Test]
    public async Task When_creating_message_from_service_bus_should_preserve_body_content()
    {
        var bodyContent = "{\"key\":\"value\"}";
        var bodyBytes = Encoding.UTF8.GetBytes(bodyContent);

        var brokeredMessage = new BrokeredMessage
        {
            MessageBodyValue = bodyBytes,
            ApplicationProperties = new Dictionary<string, object>
            {
                { "MessageType", "MT_EVENT" }
            },
            LockToken = Guid.NewGuid().ToString(),
            Id = Guid.NewGuid().ToString(),
            CorrelationId = Guid.NewGuid().ToString(),
            ContentType = "application/json"
        };

        var message = _creator.MapToBrighterMessage(brokeredMessage);

        await Assert.That(message.Body.Value).IsEqualTo(bodyContent);
        await Assert.That(message.Body.Memory.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task When_creating_message_from_service_bus_memory_should_expose_body_bytes()
    {
        var bodyContent = "{\"key\":\"value\"}";
        var bodyBytes = Encoding.UTF8.GetBytes(bodyContent);

        var brokeredMessage = new BrokeredMessage
        {
            MessageBodyValue = bodyBytes,
            ApplicationProperties = new Dictionary<string, object>
            {
                { "MessageType", "MT_EVENT" }
            },
            LockToken = Guid.NewGuid().ToString(),
            Id = Guid.NewGuid().ToString(),
            CorrelationId = Guid.NewGuid().ToString(),
            ContentType = "application/json"
        };

        IBrokeredMessageWrapper wrapper = brokeredMessage;
        var memoryFromWrapper = wrapper.MessageBodyMemory;

        await Assert.That(memoryFromWrapper.Length).IsEqualTo(bodyBytes.Length);
        await Assert.That(memoryFromWrapper.Span.SequenceEqual(bodyBytes)).IsTrue();
    }
}
