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

using System;
using System.Collections.Generic;
using Grpc.Core;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway;

public class GcpStreamSubscriberConfigurationCompositionTests
{
    [Fact]
    public void When_a_gcp_stream_subscription_sets_its_own_streaming_configuration_should_run_the_connection_configuration_first()
    {
        // Arrange
        var configurationsRun = new List<string>();

        var connection = new GcpMessagingGatewayConnection
        {
            ProjectId = "hook-composition-tests",
            StreamConfiguration = _ => configurationsRun.Add("connection")
        };

        // Assume skips every server call while creating the consumer; the insecure local endpoint lets the
        // client build without credentials, so no Pub/Sub server is needed.
        var subscription = new GcpPubSubSubscription(
            new SubscriptionName($"hook-composition-{Guid.NewGuid():N}"),
            new ChannelName($"hook-composition-{Guid.NewGuid():N}"),
            new RoutingKey("hook-composition-topic"),
            requestType: typeof(Command),
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Assume,
            subscriptionMode: SubscriptionMode.Stream,
            streamingConfiguration: builder =>
            {
                configurationsRun.Add("subscription");
                builder.Endpoint = "localhost:1";
                builder.ChannelCredentials = ChannelCredentials.Insecure;
            });

        var factory = new GcpPubSubConsumerFactory(connection);

        // Act
        using var consumer = factory.Create(subscription);

        // Assert
        Assert.Equal(["connection", "subscription"], configurationsRun);
    }
}
