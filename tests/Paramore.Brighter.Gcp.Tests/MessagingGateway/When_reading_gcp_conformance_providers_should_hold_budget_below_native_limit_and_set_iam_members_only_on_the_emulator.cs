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
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway;

/// <summary>
/// Verifies that every DLQ-backed GCP conformance provider (GcpPull, GcpPullOrdering,
/// GcpStream, GcpStreamOrdering) routes rejections through Brighter (R-27(a)(b), AC-36),
/// keeps a native delivery policy M=5 on a distinct .native topic and subscription
/// (ADR 0078 step 5), sets both IAM members only on the emulator (C-11) so emulator runs skip the
/// Cloud Resource Manager call while real Pub/Sub derives its own service agent, and declares Brighter
/// rejection-metadata keys.
/// All assertions are made on the constructed <see cref="GcpPubSubSubscription"/> without
/// touching the broker.
/// </summary>
[Trait("Category", "GcpPubSub")]
public class GcpConformanceProviderBudgetAndIamMembersTests
{
    [Fact]
    public void When_reading_gcp_pull_conformance_provider_should_hold_budget_below_native_limit_and_set_iam_members_only_on_the_emulator()
    {
        // Arrange
        var provider = new GcpPullMessageGatewayProvider();
        var keys = provider.RejectionMetadataKeys;

        // Act
        var subscription = provider.CreateSubscription(
            routingKey: new RoutingKey("test-gcp-pull"),
            channelName: new ChannelName("test-gcp-pull-ch"),
            makeChannel: OnMissingChannel.Assume,
            deadLetterRoutingKey: new RoutingKey("test-gcp-pull-dlq")
        );

        // Assert
        AssertDlqBackedSubscription(subscription, keys.StampsRejectionMetadata, keys.RejectionReason,
            keys.RejectionMessage, keys.RejectionTimestamp, keys.OriginalTopic, keys.OriginalType);
    }

    [Fact]
    public void When_reading_gcp_pull_ordering_conformance_provider_should_hold_budget_below_native_limit_and_set_iam_members_only_on_the_emulator()
    {
        // Arrange
        var provider = new GcpPullOrderingMessageGatewayProvider();
        var keys = provider.RejectionMetadataKeys;

        // Act
        var subscription = provider.CreateSubscription(
            routingKey: new RoutingKey("test-gcp-pull-ord"),
            channelName: new ChannelName("test-gcp-pull-ord-ch"),
            makeChannel: OnMissingChannel.Assume,
            deadLetterRoutingKey: new RoutingKey("test-gcp-pull-ord-dlq")
        );

        // Assert
        AssertDlqBackedSubscription(subscription, keys.StampsRejectionMetadata, keys.RejectionReason,
            keys.RejectionMessage, keys.RejectionTimestamp, keys.OriginalTopic, keys.OriginalType);
    }

    [Fact]
    public void When_reading_gcp_stream_conformance_provider_should_hold_budget_below_native_limit_and_set_iam_members_only_on_the_emulator()
    {
        // Arrange
        var provider = new GcpStreamMessageGatewayProvider();
        var keys = provider.RejectionMetadataKeys;

        // Act
        var subscription = provider.CreateSubscription(
            routingKey: new RoutingKey("test-gcp-stream"),
            channelName: new ChannelName("test-gcp-stream-ch"),
            makeChannel: OnMissingChannel.Assume,
            deadLetterRoutingKey: new RoutingKey("test-gcp-stream-dlq")
        );

        // Assert
        AssertDlqBackedSubscription(subscription, keys.StampsRejectionMetadata, keys.RejectionReason,
            keys.RejectionMessage, keys.RejectionTimestamp, keys.OriginalTopic, keys.OriginalType);
    }

    [Fact]
    public void When_reading_gcp_stream_ordering_conformance_provider_should_hold_budget_below_native_limit_and_set_iam_members_only_on_the_emulator()
    {
        // Arrange
        var provider = new GcpStreamOrderingMessageGatewayProvider();
        var keys = provider.RejectionMetadataKeys;

        // Act
        var subscription = provider.CreateSubscription(
            routingKey: new RoutingKey("test-gcp-stream-ord"),
            channelName: new ChannelName("test-gcp-stream-ord-ch"),
            makeChannel: OnMissingChannel.Assume,
            deadLetterRoutingKey: new RoutingKey("test-gcp-stream-ord-dlq")
        );

        // Assert
        AssertDlqBackedSubscription(subscription, keys.StampsRejectionMetadata, keys.RejectionReason,
            keys.RejectionMessage, keys.RejectionTimestamp, keys.OriginalTopic, keys.OriginalType);
    }

    private static void AssertDlqBackedSubscription(
        GcpPubSubSubscription subscription,
        bool stampsRejectionMetadata,
        string rejectionReasonKey,
        string rejectionMessageKey,
        string rejectionTimestampKey,
        string originalTopicKey,
        string originalTypeKey)
    {
        // The pump budget (requeueCount) is strictly below the native policy limit (R-27(a), A-5)
        Assert.Equal(3, subscription.RequeueCount);

        // The Brighter dead-letter route is set (R-27(a)); distinct from the native DeadLetterPolicy
        Assert.NotNull(subscription.DeadLetterRoutingKey);

        // Native policy M=5 on the {deadLetterRoutingKey}.native topic and subscription (ADR 0078 step 5)
        Assert.Equal(5, subscription.DeadLetter!.MaxDeliveryAttempts);
        Assert.Equal(
            $"{subscription.DeadLetterRoutingKey}.native",
            subscription.DeadLetter.TopicName.Value);
        Assert.Equal(
            $"{subscription.DeadLetterRoutingKey}.native",
            subscription.DeadLetter.Subscription.Value);

        // Both IAM members are set on the emulator only (C-11), where they skip the Cloud Resource Manager
        // call. Real Pub/Sub validates IAM members, so there they are left unset for the gateway to derive
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PUBSUB_EMULATOR_HOST")))
        {
            Assert.Null(subscription.DeadLetter.PublisherMember);
            Assert.Null(subscription.SubscriberMember);
        }
        else
        {
            Assert.StartsWith("serviceAccount:", subscription.DeadLetter.PublisherMember);
            Assert.StartsWith("serviceAccount:", subscription.SubscriberMember);
        }

        // Gateway stamps Brighter rejection metadata (R-27(b), AC-36)
        Assert.True(stampsRejectionMetadata);
        Assert.Equal(RejectionMetadataKeyNames.RejectionReason, rejectionReasonKey);
        Assert.Equal(RejectionMetadataKeyNames.RejectionMessage, rejectionMessageKey);
        Assert.Equal(RejectionMetadataKeyNames.RejectionTimestamp, rejectionTimestampKey);
        Assert.Equal(RejectionMetadataKeyNames.OriginalTopic, originalTopicKey);
        Assert.Equal(RejectionMetadataKeyNames.OriginalMessageType, originalTypeKey);
    }
}
