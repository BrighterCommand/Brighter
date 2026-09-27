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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Google.Api.Gax;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull;

/// <summary>
/// R-20, R-21, NFR-5, AC-20 — async (Proactor) path:
/// A DLQ-backed GCP channel can be created on the Pub/Sub emulator whether or not IAM members are
/// configured. GcpIamCallTolerance absorbs IAM failures in both UpdateIAmRoleForDeadLetterAsync and
/// UpdateIAmRoleForSubscriptionAsync, logging exactly two Warnings (one per helper), each carrying
/// the five diagnostic elements from the Warning template.
///
/// Log capture: the test assembly's module initializer routes ApplicationLogging to a Serilog
/// TestCorrelator sink, and each test reads only the Warnings logged within its own
/// TestCorrelator context, so the tests are safe to run in parallel with other collections.
///
/// Environment: Pub/Sub emulator at localhost:8085, project brighter-test. In the members-unset
/// case an outbound call to the real Resource Manager API is attempted; with the mock access token
/// (GoogleCredential.FromAccessToken("mock"), the fallback when ADC does not resolve) that call is
/// refused with Unauthenticated, which is tolerated. See the sync variant for the full environment note.
/// </summary>
[Trait("Category", "GcpPubSubPull")]
public class DlqBackedGcpChannelIamToleranceAsyncTests
{
    /// <summary>
    /// Members unset: the Resource Manager client is built (or IAM construction is tolerated) and
    /// GetProjectAsync on the real GCP API fails with Unauthenticated (tolerated). GetIamPolicyAsync
    /// is not reached. Exactly two Warnings are logged, one per helper.
    /// </summary>
    [Fact]
    public async Task When_creating_a_dlq_backed_gcp_channel_on_the_emulator_should_tolerate_iam_failures_async()
    {
        // Arrange
        var topicName = new RoutingKey($"gen-iam-{Guid.NewGuid():N}");
        var dlqTopicName = new RoutingKey($"gen-iam-dlq-{Guid.NewGuid():N}");
        var channelName = new ChannelName($"gen-iam-{Guid.NewGuid():N}");
        var dlqChannelName = new ChannelName(dlqTopicName.Value);

        var connection = CreateConnection();
        var channelFactory = new GcpPubSubChannelFactory(connection);

        var subscription = new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: topicName,
            messagePumpType: MessagePumpType.Proactor,
            ackDeadlineSeconds: 60,
            deadLetter: new DeadLetterPolicy(dlqTopicName, dlqChannelName)
            {
                AckDeadlineSeconds = 60,
                MaxDeliveryAttempts = 5,
                // PublisherMember intentionally NOT set (members-unset case)
            },
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Pull
            // subscriberMember intentionally NOT set (members-unset case)
        );

        IAmAChannelAsync? channel = null;
        try
        {
            using var logContext = TestCorrelator.CreateContext();

            // Act
            channel = await channelFactory.CreateAsyncChannelAsync(subscription);

            // Assert
            Assert.NotNull(channel);

            var projectId = GatewayFactory.GetProjectId();
            var warnings = TestCorrelator.GetLogEventsFromCurrentContext()
                .Where(e => e.Level == LogEventLevel.Warning)
                .Select(RenderLiteral)
                .ToList();

            // AC-20: exactly two Warnings, one per IAM helper
            Assert.Equal(2, warnings.Count);

            // Consequence clause (fifth element) present in all Warnings
            foreach (var w in warnings)
                Assert.Contains("native dead-lettering may be inactive", w);

            // Parse each Warning against the five-element template and assert
            var parsed = warnings.Select(ParseIamWarning).ToList();

            // Exactly one Warning per helper (Single throws if 0 or >1 match)
            var dlqParsed = parsed.Single(p => p.Helper == "UpdateIAmRoleForDeadLetterAsync");
            var subParsed = parsed.Single(p => p.Helper == "UpdateIAmRoleForSubscriptionAsync");

            // Members-unset: failure is at GetProjectAsync or construct ProjectsClient
            foreach (var p in new[] { dlqParsed, subParsed })
            {
                // {Rpc}: resource-manager lookup step, not GetIamPolicyAsync
                Assert.True(
                    p.Rpc == "GetProjectAsync" || p.Rpc == "construct ProjectsClient",
                    $"Expected GetProjectAsync or construct ProjectsClient, got: {p.Rpc}");
                // {Resource}: the project resource path
                Assert.Equal($"projects/{projectId}", p.Resource);
                // {Status}: one of the tolerated auth-failure codes (do not hard-assert which)
                Assert.True(
                    p.Status is "Unauthenticated" or "PermissionDenied" or "InvalidOperationException",
                    $"Expected a tolerated auth status, got: {p.Status}");
            }

            // GetIamPolicyAsync is not reached: resource-manager step is abandoned first
            Assert.DoesNotContain(parsed, p => p.Rpc == "GetIamPolicyAsync");
        }
        finally
        {
            channel?.Dispose();
            await channelFactory.DeleteTopicAsync(subscription);
            await channelFactory.DeleteSubscriptionAsync(subscription);
        }
    }

    /// <summary>
    /// Members set (C-11): no Resource Manager call is issued. GetIamPolicyAsync on the emulator
    /// returns Unimplemented (tolerated). Exactly two Warnings are logged, one per helper.
    /// </summary>
    [Fact]
    public async Task When_creating_a_dlq_backed_gcp_channel_on_the_emulator_with_members_set_should_succeed_and_log_two_warnings_async()
    {
        // Arrange
        var topicName = new RoutingKey($"gen-iam-{Guid.NewGuid():N}");
        var dlqTopicName = new RoutingKey($"gen-iam-dlq-{Guid.NewGuid():N}");
        var channelName = new ChannelName($"gen-iam-{Guid.NewGuid():N}");
        var dlqChannelName = new ChannelName(dlqTopicName.Value);

        // C-11: members explicitly configured — no Resource Manager client is ever built
        const string member = "serviceAccount:test@brighter-test.iam.gserviceaccount.com";

        var connection = CreateConnection();
        var channelFactory = new GcpPubSubChannelFactory(connection);

        var subscription = new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: topicName,
            messagePumpType: MessagePumpType.Proactor,
            ackDeadlineSeconds: 60,
            deadLetter: new DeadLetterPolicy(dlqTopicName, dlqChannelName)
            {
                AckDeadlineSeconds = 60,
                MaxDeliveryAttempts = 5,
                PublisherMember = member,
            },
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Pull,
            subscriberMember: member
        );

        IAmAChannelAsync? channel = null;
        try
        {
            using var logContext = TestCorrelator.CreateContext();

            // Act
            channel = await channelFactory.CreateAsyncChannelAsync(subscription);

            // Assert
            Assert.NotNull(channel);

            var projectId = GatewayFactory.GetProjectId();
            var expectedDlqResource = $"projects/{projectId}/topics/{dlqTopicName.Value}";
            var expectedSubResource = $"projects/{projectId}/subscriptions/{channelName.Value}";

            var warnings = TestCorrelator.GetLogEventsFromCurrentContext()
                .Where(e => e.Level == LogEventLevel.Warning)
                .Select(RenderLiteral)
                .ToList();

            // AC-20: exactly two Warnings, one per IAM helper
            Assert.Equal(2, warnings.Count);

            // Consequence clause (fifth element) present in all Warnings
            foreach (var w in warnings)
                Assert.Contains("native dead-lettering may be inactive", w);

            // Parse each Warning against the five-element template and assert
            var parsed = warnings.Select(ParseIamWarning).ToList();

            // Exactly one Warning per helper (Single throws if 0 or >1 match)
            var dlqParsed = parsed.Single(p => p.Helper == "UpdateIAmRoleForDeadLetterAsync");
            var subParsed = parsed.Single(p => p.Helper == "UpdateIAmRoleForSubscriptionAsync");

            // C-11 (members-set): failure is at GetIamPolicyAsync (no Resource Manager call issued)
            foreach (var p in new[] { dlqParsed, subParsed })
            {
                // {Rpc}: GetIamPolicyAsync (emulator returns Unimplemented)
                Assert.Equal("GetIamPolicyAsync", p.Rpc);
                // {Status}: Unimplemented from the emulator
                Assert.Equal("Unimplemented", p.Status);
            }

            // Exact resource names per helper (TopicName.ToString() / SubscriptionName.ToString())
            Assert.Equal(expectedDlqResource, dlqParsed.Resource);
            Assert.Equal(expectedSubResource, subParsed.Resource);

            // No Resource Manager calls: GetProjectAsync and construct ProjectsClient never reached
            Assert.DoesNotContain(parsed, p => p.Rpc == "GetProjectAsync" || p.Rpc == "construct ProjectsClient");
        }
        finally
        {
            channel?.Dispose();
            await channelFactory.DeleteTopicAsync(subscription);
            await channelFactory.DeleteSubscriptionAsync(subscription);
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

    /// <summary>
    /// Renders a captured event's message with literal (unquoted) property values, so it can be
    /// parsed against the Warning template.
    /// </summary>
    private static string RenderLiteral(LogEvent logEvent)
    {
        var writer = new StringWriter();
        new MessageTemplateTextFormatter("{Message:l}").Format(logEvent, writer);
        return writer.ToString();
    }

    /// <summary>
    /// Parses one Warning message against the five-element template:
    /// "{Helper} abandoned: {Rpc} on {Resource} failed with {Status}; native dead-lettering may be inactive".
    /// Asserts the message matches the template and returns the five elements.
    /// </summary>
    private static (string Helper, string Rpc, string Resource, string Status) ParseIamWarning(string message)
    {
        const string abandonedSep = " abandoned: ";
        const string onSep = " on ";
        const string failedSep = " failed with ";
        const string tail = "; native dead-lettering may be inactive";

        Assert.Contains(tail, message);

        var ai = message.IndexOf(abandonedSep, StringComparison.Ordinal);
        Assert.True(ai > 0, $"Missing '{abandonedSep}' in: {message}");
        var helper = message[..ai];

        var rpcStart = ai + abandonedSep.Length;
        var oi = message.IndexOf(onSep, rpcStart, StringComparison.Ordinal);
        Assert.True(oi > rpcStart, $"Missing '{onSep}' in: {message}");
        var rpc = message[rpcStart..oi];

        var resStart = oi + onSep.Length;
        var fi = message.IndexOf(failedSep, resStart, StringComparison.Ordinal);
        Assert.True(fi > resStart, $"Missing '{failedSep}' in: {message}");
        var resource = message[resStart..fi];

        var statusStart = fi + failedSep.Length;
        var ti = message.IndexOf(tail, statusStart, StringComparison.Ordinal);
        Assert.True(ti > statusStart, $"Missing tail in: {message}");
        var status = message[statusStart..ti];

        return (helper, rpc, resource, status);
    }
}
