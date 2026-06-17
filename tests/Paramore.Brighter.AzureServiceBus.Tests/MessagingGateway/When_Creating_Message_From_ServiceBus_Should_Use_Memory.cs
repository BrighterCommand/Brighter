using System;
using System.Collections.Generic;
using System.Text;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

public class AzureServiceBusMessageMemoryTests
{
    private readonly AzureServiceBusMesssageCreator _creator;

    public AzureServiceBusMessageMemoryTests()
    {
        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            subscriptionName: new SubscriptionName("test-sub"),
            channelName: new ChannelName("test-channel"),
            routingKey: new RoutingKey("test-topic"),
            messagePumpType: MessagePumpType.Reactor);

        _creator = new AzureServiceBusMesssageCreator(subscription);
    }

    [Fact]
    public void When_creating_message_from_service_bus_should_preserve_body_content()
    {
        // Arrange
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

        // Act
        var message = _creator.MapToBrighterMessage(brokeredMessage);

        // Assert — body content is accessible via both Memory and Value
        Assert.Equal(bodyContent, message.Body.Value);
        Assert.True(message.Body.Memory.Length > 0);
    }

    [Fact]
    public void When_creating_message_from_service_bus_memory_should_expose_body_bytes()
    {
        // Arrange
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

        // Act — access MessageBodyMemory through the interface
        IBrokeredMessageWrapper wrapper = brokeredMessage;
        var memoryFromWrapper = wrapper.MessageBodyMemory;

        // Assert
        Assert.Equal(bodyBytes.Length, memoryFromWrapper.Length);
        Assert.True(memoryFromWrapper.Span.SequenceEqual(bodyBytes));
    }
}
