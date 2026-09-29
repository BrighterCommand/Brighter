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

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Google.Api.Gax;
using Google.Cloud.PubSub.V1;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Sinks.TestCorrelator;
using Xunit;
using DeadLetterPolicy = Paramore.Brighter.MessagingGateway.GcpPubSub.DeadLetterPolicy;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull;

/// <summary>
/// R-11, R-26, NFR-4, AC-11 (second clause), AC-29 — async (Proactor) path:
/// Creating a GCP channel for a subscription whose delivery budget is unenforceable (no
/// <see cref="DeadLetterPolicy"/>, R-11) logs exactly one budget Warning at channel creation, via
/// <see cref="GcpPubSubChannelFactory.CreateAsyncChannelAsync"/>. Receiving messages afterwards never
/// logs another (NFR-4): the count is unchanged after 100 receives, and unchanged again after a
/// further 100.
///
/// Negative cases (AC-29): an R-7 subscription (requeueCount 0) and an R-10 subscription (budget at
/// the native redrive limit) each carry a <see cref="DeadLetterPolicy"/>, so
/// <c>DeliveryBudgetUnenforceableReason</c> is null and R-11's predicate is negative — zero budget
/// Warnings at creation, and zero across 2 x 100 receives. The expected count is computed from
/// <c>DeliveryBudgetUnenforceableReason</c> so the test holds on both branches of that property.
///
/// Log capture: the test assembly's module initializer routes ApplicationLogging to a Serilog
/// TestCorrelator sink; each test reads only Warnings within its own TestCorrelator context.
/// Warnings are filtered to the budget-specific template text so the IAM-tolerance Warnings that
/// the DeadLetterPolicy negative cases also emit (ADR 0078 GcpIamCallTolerance) do not pollute the
/// count.
/// </summary>
[Trait("Category", "GcpPubSubPull")]
public class GcpChannelUnenforceableBudgetWarningAsyncTests
{
    private const string BudgetWarningMarker = "delivery count cannot advance";
    private const string IamMember = "serviceAccount:test@brighter-test.iam.gserviceaccount.com";

    [Fact]
    public async Task When_a_gcp_channel_is_created_for_an_unenforceable_budget_should_log_one_warning_async()
    {
        // Arrange
        var topicName = new RoutingKey($"gen-budget-{Guid.NewGuid():N}");
        var channelName = new ChannelName($"gen-budget-{Guid.NewGuid():N}");

        var connection = CreateConnection();
        var channelFactory = new GcpPubSubChannelFactory(connection);

        var subscription = new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: topicName,
            messagePumpType: MessagePumpType.Proactor,
            ackDeadlineSeconds: 60,
            requeueCount: 3,
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Pull
            // deadLetter intentionally omitted — R-11 (no DeadLetterPolicy configured)
        );

        IAmAChannelAsync? channel = null;
        try
        {
            using var logContext = TestCorrelator.CreateContext();

            // Act
            channel = await channelFactory.CreateAsyncChannelAsync(subscription);

            // Assert — exactly one budget Warning naming the subscription, 3, and the reason
            var warnings = GetBudgetWarnings();
            Assert.Single(warnings);
            Assert.Contains(channelName.Value, warnings[0]);
            Assert.Contains("3", warnings[0]);
            Assert.Contains("no DeadLetterPolicy is configured", warnings[0]);

            // Act — receive 100 messages; the Warning count must not change (NFR-4)
            await PublishAndReceiveAsync(connection, channel, topicName, 100);
            Assert.Single(GetBudgetWarnings());

            // Act — receive another 100; still unchanged
            await PublishAndReceiveAsync(connection, channel, topicName, 100);
            Assert.Single(GetBudgetWarnings());
        }
        finally
        {
            channel?.Dispose();
            await channelFactory.DeleteTopicAsync(subscription);
            await channelFactory.DeleteSubscriptionAsync(subscription);
        }
    }

    [Theory]
    [InlineData(0, 5)] // R-7: requeueCount 0 — a pipeline-validation concern, not logged here
    [InlineData(5, 5)] // R-10: budget at the native redrive limit — also pipeline-validation only
    public async Task When_a_gcp_channel_is_created_for_a_budget_with_dead_letter_policy_should_log_expected_warnings_async(
        int requeueCount, int maxDeliveryAttempts)
    {
        // Arrange
        var topicName = new RoutingKey($"gen-budget-{Guid.NewGuid():N}");
        var dlqTopicName = new RoutingKey($"gen-budget-dlq-{Guid.NewGuid():N}");
        var channelName = new ChannelName($"gen-budget-{Guid.NewGuid():N}");
        var dlqChannelName = new ChannelName(dlqTopicName.Value);

        var connection = CreateConnection();
        var channelFactory = new GcpPubSubChannelFactory(connection);

        var subscription = new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: topicName,
            messagePumpType: MessagePumpType.Proactor,
            ackDeadlineSeconds: 60,
            requeueCount: requeueCount,
            deadLetter: new DeadLetterPolicy(dlqTopicName, dlqChannelName)
            {
                AckDeadlineSeconds = 60,
                MaxDeliveryAttempts = maxDeliveryAttempts,
                PublisherMember = IamMember,
            },
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Pull,
            subscriberMember: IamMember
        );

        // Compute the expectation from DeliveryBudgetUnenforceableReason (R-26) so the test holds
        // on both branches of that property, rather than hard-coding zero.
        var expectedWarnings =
            subscription.RequeueCount != -1 && subscription.DeliveryBudgetUnenforceableReason is not null
                ? 1
                : 0;

        IAmAChannelAsync? channel = null;
        try
        {
            using var logContext = TestCorrelator.CreateContext();

            // Act
            channel = await channelFactory.CreateAsyncChannelAsync(subscription);

            // Assert
            Assert.Equal(expectedWarnings, GetBudgetWarnings().Count);

            await PublishAndReceiveAsync(connection, channel, topicName, 100);
            Assert.Equal(expectedWarnings, GetBudgetWarnings().Count);

            await PublishAndReceiveAsync(connection, channel, topicName, 100);
            Assert.Equal(expectedWarnings, GetBudgetWarnings().Count);
        }
        finally
        {
            channel?.Dispose();
            await channelFactory.DeleteTopicAsync(subscription);
            await channelFactory.DeleteSubscriptionAsync(subscription);
        }
    }

    private static async Task PublishAndReceiveAsync(
        GcpMessagingGatewayConnection connection, IAmAChannelAsync channel, RoutingKey topicName, int count)
    {
        var topic = TopicName.FromProjectTopic(connection.ProjectId, topicName.Value);
        var builder = new PublisherClientBuilder
        {
            Credential = connection.Credential,
            TopicName = topic,
        };
        connection.PublisherConfiguration?.Invoke(builder);
        var client = await builder.BuildAsync();
        var producer = new GcpMessageProducer(client, new GcpPublication<MyCommand> { Topic = topicName });

        try
        {
            var messageBuilder = new DefaultMessageBuilder();
            for (var i = 0; i < count; i++)
            {
                await producer.SendAsync(messageBuilder.SetTopic(topicName).SetMessageId(Id.Random()).Build());
            }

            var received = 0;
            var attempts = 0;
            while (received < count)
            {
                attempts++;
                Assert.True(attempts < 2000, $"Gave up waiting for {count} messages; received {received}");

                var message = await channel.ReceiveAsync(TimeSpan.FromSeconds(10));
                if (message.Header.MessageType == MessageType.MT_NONE) continue;

                await channel.AcknowledgeAsync(message);
                received++;
            }
        }
        finally
        {
            await producer.DisposeAsync();
        }
    }

    private static GcpMessagingGatewayConnection CreateConnection() =>
        new()
        {
            Credential = GatewayFactory.GetCredential(),
            ProjectId = GatewayFactory.GetProjectId(),
            TopicManagerConfiguration = cfg =>
            {
                cfg.EmulatorDetection = EmulatorDetection.EmulatorOrProduction;
            },
            PublisherConfiguration = cfg =>
            {
                cfg.EmulatorDetection = EmulatorDetection.EmulatorOrProduction;
            },
            SubscriptionManagerConfiguration = cfg =>
            {
                cfg.EmulatorDetection = EmulatorDetection.EmulatorOrProduction;
            },
        };

    private static List<string> GetBudgetWarnings()
    {
        return TestCorrelator.GetLogEventsFromCurrentContext()
            .Where(e => e.Level == LogEventLevel.Warning)
            .Select(RenderLiteral)
            .Where(m => m.Contains(BudgetWarningMarker, StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>
    /// Renders a captured event's message with literal (unquoted) property values, so it can be
    /// searched for plain substrings such as the subscription name or requeue count.
    /// </summary>
    private static string RenderLiteral(LogEvent logEvent)
    {
        var writer = new StringWriter();
        new MessageTemplateTextFormatter("{Message:l}").Format(logEvent, writer);
        return writer.ToString();
    }
}
