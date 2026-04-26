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

using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sqs;

[Trait("Category", "AWS")]
public class SqsSubscriptionDlqRoutingKeyTests
{
    [Test]
    public async Task When_creating_sqs_subscription_with_dlq_routing_keys_should_expose_properties()
    {
        //Arrange
        var deadLetterRoutingKey = new RoutingKey("orders-dlq");
        var invalidMessageRoutingKey = new RoutingKey("orders-invalid");

        //Act
        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName("test-subscription"),
            channelName: new ChannelName("test-channel"),
            routingKey: new RoutingKey("orders"),
            deadLetterRoutingKey: deadLetterRoutingKey,
            invalidMessageRoutingKey: invalidMessageRoutingKey
        );

        //Assert
        await Assert.That(subscription).IsAssignableTo<IUseBrighterDeadLetterSupport>();
        var dlqSupport = (IUseBrighterDeadLetterSupport)subscription;
        await Assert.That(dlqSupport.DeadLetterRoutingKey).IsEqualTo(deadLetterRoutingKey);

        await Assert.That(subscription).IsAssignableTo<IUseBrighterInvalidMessageSupport>();
        var invalidSupport = (IUseBrighterInvalidMessageSupport)subscription;
        await Assert.That(invalidSupport.InvalidMessageRoutingKey).IsEqualTo(invalidMessageRoutingKey);
    }

    [Test]
    public async Task When_creating_sqs_subscription_without_dlq_routing_keys_should_default_to_null()
    {
        //Arrange & Act
        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName("test-subscription"),
            channelName: new ChannelName("test-channel"),
            routingKey: new RoutingKey("orders")
        );

        //Assert
        await Assert.That(subscription).IsAssignableTo<IUseBrighterDeadLetterSupport>();
        var dlqSupport = (IUseBrighterDeadLetterSupport)subscription;
        await Assert.That(dlqSupport.DeadLetterRoutingKey is null).IsTrue();

        await Assert.That(subscription).IsAssignableTo<IUseBrighterInvalidMessageSupport>();
        var invalidSupport = (IUseBrighterInvalidMessageSupport)subscription;
        await Assert.That(invalidSupport.InvalidMessageRoutingKey is null).IsTrue();
    }
}
