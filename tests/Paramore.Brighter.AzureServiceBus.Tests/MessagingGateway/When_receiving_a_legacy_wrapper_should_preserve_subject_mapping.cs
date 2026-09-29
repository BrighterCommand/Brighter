#region Licence

/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

[Property("Category", "ASB")]
public class AzureServiceBusLegacyWrapperSubjectTests
{
    [Test]
    [Arguments(false, null, "")]
    [Arguments(true, "cloud-subject", "cloud-subject")]
    [Arguments(true, "", "")]
    [Arguments(true, null, "")]
    public async System.Threading.Tasks.Task When_receiving_a_legacy_wrapper_should_preserve_subject_mapping(
        bool hasSubject, string? subject, string expectedSubject)
    {
        // Arrange
        var properties = new Dictionary<string, object>();
        if (hasSubject)
            properties["cloudEvents:subject"] = subject!;
        var brokeredMessage = new BrokeredMessage
        {
            MessageBodyValue = BinaryData.FromString("{}").ToArray(),
            Id = Id.Random().Value,
            ApplicationProperties = properties
        };
        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            subscriptionName: new SubscriptionName("orders-subscription"),
            channelName: new ChannelName("orders-channel"),
            routingKey: new RoutingKey("orders"));
        var creator = new AzureServiceBusMessageCreator(subscription);

        // Act
        var received = creator.MapToBrighterMessage(brokeredMessage);

        // Assert
        await Assert.That(received.Header.Subject).IsEqualTo(expectedSubject);
    }
}
