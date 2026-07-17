using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using System.Threading.Tasks;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

/// <summary>
/// Covers the bug where DeferMessageAction caused a requeued message to carry
/// SequenceNumber in ApplicationProperties. On redelivery, MapToBrighterMessage
/// would throw ArgumentException (duplicate key) because SequenceNumber was
/// already added from the native broker property.
/// Fix: SequenceNumber is in ASBConstants.ReservedHeaders so it is never written
/// to ApplicationProperties during requeue.
/// </summary>
[Property("Category", "ASB")]
public class When_A_Deferred_Message_Is_Redelivered
{
    private readonly AzureServiceBusMessageCreator _creator;

    public When_A_Deferred_Message_Is_Redelivered()
    {
        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            subscriptionName: new SubscriptionName("test-sub"),
            channelName: new ChannelName("test-channel"),
            routingKey: new RoutingKey("test-topic"),
            messagePumpType: MessagePumpType.Reactor);

        _creator = new AzureServiceBusMessageCreator(subscription);
    }

    [Test]
    public async Task When_a_deferred_message_is_redelivered_it_does_not_throw_a_duplicate_key_exception()
    {
        // Arrange — first delivery from ASB
        var firstDelivery = new BrokeredMessage
        {
            MessageBodyValue = Encoding.UTF8.GetBytes("{\"key\":\"value\"}"),
            ApplicationProperties = new Dictionary<string, object>
            {
                { "MessageType", "MT_COMMAND" }
            },
            LockToken = Guid.NewGuid().ToString(),
            SequenceNumber = 100L,
            Id = Guid.NewGuid().ToString(),
            CorrelationId = Guid.NewGuid().ToString(),
            ContentType = "application/json"
        };

        // First receive — handler will throw DeferMessageAction, triggering a requeue
        var brighterMessage = _creator.MapToBrighterMessage(firstDelivery);

        // Simulate requeue: Brighter calls ConvertToServiceBusMessage and republishes.
        // SequenceNumber must NOT appear in ApplicationProperties of the requeued message.
        var requeued = AzureServiceBusMessagePublisher.ConvertToServiceBusMessage(brighterMessage);
        await Assert.That(requeued.ApplicationProperties.ContainsKey("SequenceNumber")).IsFalse();

        // Arrange — second delivery (redelivery of the requeued message with the same sequence number)
        var secondDelivery = new BrokeredMessage
        {
            MessageBodyValue = Encoding.UTF8.GetBytes("{\"key\":\"value\"}"),
            ApplicationProperties = requeued.ApplicationProperties
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            LockToken = Guid.NewGuid().ToString(),
            SequenceNumber = firstDelivery.SequenceNumber,
            Id = firstDelivery.Id,
            CorrelationId = firstDelivery.CorrelationId,
            ContentType = "application/json"
        };

        // Act & Assert — must not throw ArgumentException: duplicate key SequenceNumber
        var redelivered = _creator.MapToBrighterMessage(secondDelivery);
        await Assert.That(redelivered.Header.Bag["SequenceNumber"]).IsEqualTo(firstDelivery.SequenceNumber);
    }
}