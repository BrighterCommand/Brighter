#region Licence

/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using System.Threading.Tasks;
namespace Paramore.Brighter.Gcp.Tests.MessagingGateway;

public class GcpSubscriptionDlqRoutingKeyTests
{
    [Test]
    public async Task When_creating_gcp_subscription_with_dlq_routing_keys_should_expose_properties()
    {
        // Arrange
        var deadLetterRoutingKey = new RoutingKey("orders-dlq");
        var invalidMessageRoutingKey = new RoutingKey("orders-invalid");
        var nativeDeadLetterPolicy = new DeadLetterPolicy(
            new RoutingKey("orders-native-dlq"),
            new ChannelName("orders-dlq-sub"))
        {
            MaxDeliveryAttempts = 5
        };

        // Act
        var subscription = new GcpPubSubSubscription(
            subscriptionName: new SubscriptionName("test-subscription"),
            channelName: new ChannelName("test-channel"),
            routingKey: new RoutingKey("orders"),
            requestType: typeof(MyCommand),
            messagePumpType: MessagePumpType.Reactor,
            deadLetter: nativeDeadLetterPolicy,
            deadLetterRoutingKey: deadLetterRoutingKey,
            invalidMessageRoutingKey: invalidMessageRoutingKey
        );

        var typedSubscription = new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName("test-subscription-typed"),
            channelName: new ChannelName("test-channel-typed"),
            routingKey: new RoutingKey("orders-typed"),
            messagePumpType: MessagePumpType.Reactor,
            deadLetter: nativeDeadLetterPolicy,
            deadLetterRoutingKey: deadLetterRoutingKey,
            invalidMessageRoutingKey: invalidMessageRoutingKey
        );

        // Assert — Brighter routing key interfaces are satisfied
        await Assert.That(subscription).IsAssignableTo<IUseBrighterDeadLetterSupport>();
        var dlqSupport = (IUseBrighterDeadLetterSupport)subscription;
        await Assert.That(dlqSupport.DeadLetterRoutingKey).IsEqualTo(deadLetterRoutingKey);

        await Assert.That(subscription).IsAssignableTo<IUseBrighterInvalidMessageSupport>();
        var invalidSupport = (IUseBrighterInvalidMessageSupport)subscription;
        await Assert.That(invalidSupport.InvalidMessageRoutingKey).IsEqualTo(invalidMessageRoutingKey);

        await Assert.That(typedSubscription).IsAssignableTo<IUseBrighterDeadLetterSupport>();
        var typedDlqSupport = (IUseBrighterDeadLetterSupport)typedSubscription;
        await Assert.That(typedDlqSupport.DeadLetterRoutingKey).IsEqualTo(deadLetterRoutingKey);

        await Assert.That(typedSubscription).IsAssignableTo<IUseBrighterInvalidMessageSupport>();
        var typedInvalidSupport = (IUseBrighterInvalidMessageSupport)typedSubscription;
        await Assert.That(typedInvalidSupport.InvalidMessageRoutingKey).IsEqualTo(invalidMessageRoutingKey);

        // Assert — existing native DeadLetter policy is unchanged by the addition of Brighter routing keys
        await Assert.That(subscription.DeadLetter).IsSameReferenceAs(nativeDeadLetterPolicy);
        await Assert.That(subscription.DeadLetter!.TopicName).IsEqualTo(nativeDeadLetterPolicy.TopicName);
        await Assert.That(subscription.DeadLetter.MaxDeliveryAttempts).IsEqualTo(5);

        await Assert.That(typedSubscription.DeadLetter).IsSameReferenceAs(nativeDeadLetterPolicy);
        await Assert.That(typedSubscription.DeadLetter!.TopicName).IsEqualTo(nativeDeadLetterPolicy.TopicName);
        await Assert.That(typedSubscription.DeadLetter.MaxDeliveryAttempts).IsEqualTo(5);
    }
}
