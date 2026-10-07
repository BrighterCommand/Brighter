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
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway;

public class GcpSubscriptionDlqRoutingKeyTests
{
    [Fact]
    public void When_creating_gcp_subscription_with_dlq_routing_keys_should_expose_properties()
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
        Assert.IsAssignableFrom<IUseBrighterDeadLetterSupport>(subscription);
        var dlqSupport = (IUseBrighterDeadLetterSupport)subscription;
        Assert.Equal(deadLetterRoutingKey, dlqSupport.DeadLetterRoutingKey);

        Assert.IsAssignableFrom<IUseBrighterInvalidMessageSupport>(subscription);
        var invalidSupport = (IUseBrighterInvalidMessageSupport)subscription;
        Assert.Equal(invalidMessageRoutingKey, invalidSupport.InvalidMessageRoutingKey);

        Assert.IsAssignableFrom<IUseBrighterDeadLetterSupport>(typedSubscription);
        var typedDlqSupport = (IUseBrighterDeadLetterSupport)typedSubscription;
        Assert.Equal(deadLetterRoutingKey, typedDlqSupport.DeadLetterRoutingKey);

        Assert.IsAssignableFrom<IUseBrighterInvalidMessageSupport>(typedSubscription);
        var typedInvalidSupport = (IUseBrighterInvalidMessageSupport)typedSubscription;
        Assert.Equal(invalidMessageRoutingKey, typedInvalidSupport.InvalidMessageRoutingKey);

        // Assert — existing native DeadLetter policy is unchanged by the addition of Brighter routing keys
        Assert.Same(nativeDeadLetterPolicy, subscription.DeadLetter);
        Assert.Equal(nativeDeadLetterPolicy.TopicName, subscription.DeadLetter!.TopicName);
        Assert.Equal(5, subscription.DeadLetter.MaxDeliveryAttempts);

        Assert.Same(nativeDeadLetterPolicy, typedSubscription.DeadLetter);
        Assert.Equal(nativeDeadLetterPolicy.TopicName, typedSubscription.DeadLetter!.TopicName);
        Assert.Equal(5, typedSubscription.DeadLetter.MaxDeliveryAttempts);
    }
}
