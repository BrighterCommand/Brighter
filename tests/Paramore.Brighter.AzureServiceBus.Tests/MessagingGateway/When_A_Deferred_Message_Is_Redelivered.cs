#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

/// <summary>
/// Covers the bug where DeferMessageAction caused a requeued message to carry
/// SequenceNumber in ApplicationProperties. On redelivery, MapToBrighterMessage
/// would throw ArgumentException (duplicate key) because SequenceNumber was
/// already added from the native broker property.
/// Fix: SequenceNumber is in ASBConstants.ReservedHeaders so it is never written
/// to ApplicationProperties during requeue.
/// </summary>
[Trait("Category", "ASB")]
public class When_A_Deferred_Message_Is_Redelivered
{
    private readonly AzureServiceBusMesssageCreator _creator;

    public When_A_Deferred_Message_Is_Redelivered()
    {
        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            subscriptionName: new SubscriptionName("test-sub"),
            channelName: new ChannelName("test-channel"),
            routingKey: new RoutingKey("test-topic"),
            messagePumpType: MessagePumpType.Reactor);

        _creator = new AzureServiceBusMesssageCreator(subscription);
    }

    [Fact]
    public void When_a_deferred_message_is_redelivered_it_does_not_throw_a_duplicate_key_exception()
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
        Assert.False(requeued.ApplicationProperties.ContainsKey("SequenceNumber"),
            "SequenceNumber must not be written to ApplicationProperties on requeue — " +
            "it is a broker-assigned property and must stay in ReservedHeaders.");

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
        Assert.Equal(firstDelivery.SequenceNumber, redelivered.Header.Bag["SequenceNumber"]);
    }
}
