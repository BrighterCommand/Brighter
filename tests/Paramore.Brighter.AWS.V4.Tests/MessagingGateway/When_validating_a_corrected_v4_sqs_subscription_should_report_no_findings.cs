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

using System.Linq;
using Amazon;
using Amazon.Runtime;
using Paramore.Brighter.AWS.V4.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using Paramore.Brighter.ServiceActivator.Validation;
using Xunit;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway;

public class V4SqsCorrectedSubscriptionValidationTests
{
    [Fact]
    public void When_validating_a_corrected_v4_sqs_subscription_should_report_no_findings()
    {
        // Arrange — construction only, so no AWS connection is made (NFR-3): the AWSSQS.V4 ChannelFactory
        // and its underlying AWSMessagingGatewayConnection only store credentials/config until a channel
        // is actually created
        var connection = new AWSMessagingGatewayConnection(
            new BasicAWSCredentials("test", "test"),
            RegionEndpoint.EUWest1);
        var channelFactory = new ChannelFactory(connection);
        var combinedChannelFactory = new CombinedChannelFactory([channelFactory]);
        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName("t"),
            channelName: new ChannelName("t"),
            routingKey: new RoutingKey("t"));

        var spec = ConsumerValidationRules.ChannelFactoryCompatible(combinedChannelFactory);

        // Act
        var satisfied = spec.IsSatisfiedBy(subscription);
        var collector = new ValidationResultCollector<Subscription>();
        var results = spec.Accept(collector).ToList();

        // Assert — no findings: the corrected declared type matches the real SQS channel factory
        Assert.True(satisfied);
        Assert.Empty(results);
    }
}
