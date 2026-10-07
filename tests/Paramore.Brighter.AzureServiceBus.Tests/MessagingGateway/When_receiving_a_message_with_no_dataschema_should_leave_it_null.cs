using System;
using System.Collections.Generic;
using System.Text;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

/// <summary>
/// <c>MessageHeader.DataSchema</c> is nullable and every other Brighter backend leaves it null when
/// the wire carries no dataschema. Azure Service Bus instead fabricated a
/// <c>http://goparamore.io</c> value — which is the documented default for <c>Source</c>, not for
/// <c>DataSchema</c> — and then re-published that invention on every requeue.
/// </summary>
[Trait("Category", "ASB")]
public class AzureServiceBusAbsentDataSchemaTests
{
    private readonly AzureServiceBusMessageCreator _creator;

    public AzureServiceBusAbsentDataSchemaTests()
    {
        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            subscriptionName: new SubscriptionName("test-sub"),
            channelName: new ChannelName("test-channel"),
            routingKey: new RoutingKey("test-topic"),
            messagePumpType: MessagePumpType.Reactor);

        _creator = new AzureServiceBusMessageCreator(subscription);
    }

    [Fact]
    public void When_receiving_a_message_with_no_dataschema_should_leave_it_null()
    {
        // Arrange — the absence of a "cloudEvents:schema" property is what decides this test
        var received = new BrokeredMessage
        {
            MessageBodyValue = Encoding.UTF8.GetBytes("{\"key\":\"value\"}"),
            ApplicationProperties = new Dictionary<string, object>
            {
                { "MessageType", "MT_COMMAND" }
            },
            LockToken = Guid.NewGuid().ToString(),
            SequenceNumber = 1L,
            Id = Guid.NewGuid().ToString(),
            CorrelationId = Guid.NewGuid().ToString(),
            ContentType = "application/json"
        };

        // Act
        var message = _creator.MapToBrighterMessage(received);

        // Assert
        Assert.Null(message.Header.DataSchema);
    }
}
