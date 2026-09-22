using System;
using System.Collections.Generic;
using System.Text;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

/// <summary>
/// CloudEvents defines both <c>source</c> and <c>dataschema</c> as URI-*references*, which MAY be
/// relative. Brighter's own generated test message builders default the source to a bare GUID, so a
/// relative source is the ordinary case rather than an exotic one — and every transport other than
/// Azure Service Bus round-trips it without complaint.
/// </summary>
[Trait("Category", "ASB")]
public class AzureServiceBusRelativeCloudEventsUriTests
{
    private readonly AzureServiceBusMessageCreator _creator;

    public AzureServiceBusRelativeCloudEventsUriTests()
    {
        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            subscriptionName: new SubscriptionName("test-sub"),
            channelName: new ChannelName("test-channel"),
            routingKey: new RoutingKey("test-topic"),
            messagePumpType: MessagePumpType.Reactor);

        _creator = new AzureServiceBusMessageCreator(subscription);
    }

    [Fact]
    public void When_receiving_a_message_with_relative_cloud_events_uris_should_preserve_them()
    {
        // Arrange — the two relative URIs are the only data that decides this test
        var relativeSource = new Uri(Uuid.NewAsString(), UriKind.Relative);
        var relativeDataSchema = new Uri("/schemas/v1", UriKind.Relative);

        // The CloudEvents keys are written as string literals, not via ASBConstants, deliberately:
        // these are wire-format attribute names, so a change to a constant's *value* must fail a test
        // rather than silently rename an on-wire attribute. (ASBConstants is internal in any case.)
        var received = new BrokeredMessage
        {
            MessageBodyValue = Encoding.UTF8.GetBytes("{\"key\":\"value\"}"),
            ApplicationProperties = new Dictionary<string, object>
            {
                { "MessageType", "MT_COMMAND" },
                { "cloudEvents:source", relativeSource.ToString() },
                { "cloudEvents:schema", relativeDataSchema.ToString() }
            },
            LockToken = Guid.NewGuid().ToString(),
            SequenceNumber = 1L,
            Id = Guid.NewGuid().ToString(),
            CorrelationId = Guid.NewGuid().ToString(),
            ContentType = "application/json"
        };

        // Act
        var message = _creator.MapToBrighterMessage(received);

        // Assert
        Assert.Equal(relativeSource, message.Header.Source);
        Assert.False(message.Header.Source.IsAbsoluteUri);
        Assert.Equal(relativeDataSchema, message.Header.DataSchema);
    }
}
