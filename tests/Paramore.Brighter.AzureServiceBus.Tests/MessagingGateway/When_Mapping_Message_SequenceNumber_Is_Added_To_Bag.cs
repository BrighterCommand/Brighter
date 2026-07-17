using System;
using System.Collections.Generic;
using System.Text;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using System.Threading.Tasks;

// ASBConstants is internal to the gateway assembly; tests use the agreed
// public contract string directly so they act as a stability guard too.

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

[Property("Category", "ASB")]
public class AzureServiceBusMessageSequenceNumberTests
{
    private readonly AzureServiceBusMessageCreator _creator;

    public AzureServiceBusMessageSequenceNumberTests()
    {
        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            subscriptionName: new SubscriptionName("test-sub"),
            channelName: new ChannelName("test-channel"),
            routingKey: new RoutingKey("test-topic"),
            messagePumpType: MessagePumpType.Reactor);

        _creator = new AzureServiceBusMessageCreator(subscription);
    }

    [Test]
    public async Task When_mapping_a_message_the_sequence_number_is_added_to_the_header_bag()
    {
        // Arrange
        const long expectedSequenceNumber = 12345678L;

        var brokeredMessage = new BrokeredMessage
        {
            MessageBodyValue = Encoding.UTF8.GetBytes("{\"key\":\"value\"}"),
            ApplicationProperties = new Dictionary<string, object>
            {
                { "MessageType", "MT_EVENT" }
            },
            LockToken = Guid.NewGuid().ToString(),
            SequenceNumber = expectedSequenceNumber,
            Id = Guid.NewGuid().ToString(),
            CorrelationId = Guid.NewGuid().ToString(),
            ContentType = "application/json"
        };

        // Act
        var message = _creator.MapToBrighterMessage(brokeredMessage);

        // Assert
        await Assert.That(message.Header.Bag.ContainsKey("SequenceNumber")).IsTrue();
        await Assert.That(message.Header.Bag["SequenceNumber"]).IsEqualTo(expectedSequenceNumber);
    }

    [Test]
    public async Task When_mapping_a_message_with_a_zero_sequence_number_it_is_still_present_in_the_bag()
    {
        // Arrange
        var brokeredMessage = new BrokeredMessage
        {
            MessageBodyValue = Encoding.UTF8.GetBytes("{}"),
            ApplicationProperties = new Dictionary<string, object>
            {
                { "MessageType", "MT_EVENT" }
            },
            LockToken = Guid.NewGuid().ToString(),
            SequenceNumber = 0L,
            Id = Guid.NewGuid().ToString(),
            CorrelationId = Guid.NewGuid().ToString(),
            ContentType = "application/json"
        };

        // Act
        var message = _creator.MapToBrighterMessage(brokeredMessage);

        // Assert
        await Assert.That(message.Header.Bag.ContainsKey("SequenceNumber")).IsTrue();
        await Assert.That(message.Header.Bag["SequenceNumber"]).IsEqualTo(0L);
    }
}