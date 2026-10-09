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
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sqs;

/// <summary>
/// Verifies that <see cref="SqsSubscription"/> implements <see cref="IAmADeliveryCountingSubscription"/>
/// and exposes the native redrive limit from its <see cref="SqsAttributes.RedrivePolicy"/> (R-10, AC-10, ADR 0077).
/// </summary>
[Trait("Category", "AWS")]
public class SqsSubscriptionNativeRedriveLimitTests
{
    [Fact]
    public void When_an_sqs_subscription_has_a_redrive_policy_should_expose_native_redrive_limit()
    {
        //Arrange
        var redrivePolicy = new RedrivePolicy(new ChannelName("orders-dlq"), maxReceiveCount: 5);
        var queueAttributes = new SqsAttributes(redrivePolicy: redrivePolicy);

        //Act
        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName("test-subscription"),
            channelName: new ChannelName("test-channel"),
            routingKey: new RoutingKey("orders"),
            queueAttributes: queueAttributes
        );

        //Assert
        Assert.IsAssignableFrom<IAmADeliveryCountingSubscription>(subscription);
        var counting = (IAmADeliveryCountingSubscription)subscription;
        Assert.Equal(5, counting.NativeRedriveLimit);
        Assert.Null(counting.DeliveryBudgetUnenforceableReason);
    }

    [Fact]
    public void When_an_sqs_subscription_has_no_redrive_policy_should_report_null_native_redrive_limit()
    {
        //Arrange & Act
        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName("test-subscription"),
            channelName: new ChannelName("test-channel"),
            routingKey: new RoutingKey("orders")
        );

        //Assert
        Assert.IsAssignableFrom<IAmADeliveryCountingSubscription>(subscription);
        var counting = (IAmADeliveryCountingSubscription)subscription;
        Assert.Null(counting.NativeRedriveLimit);
        Assert.Null(counting.DeliveryBudgetUnenforceableReason);
    }
}
