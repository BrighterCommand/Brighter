using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

/// <summary>
/// Pins the contract that actually broke: a relative CloudEvents <c>source</c> and <c>dataschema</c>
/// must survive publish *and* receive. Testing either side alone would miss the asymmetry that caused
/// the defect — the publisher wrote <c>source</c> as a string but <c>dataschema</c> as a
/// <see cref="Uri"/>, and the reader parsed both as absolute-only.
/// </summary>
[Trait("Category", "ASB")]
public class AzureServiceBusRelativeCloudEventsUriRoundTripTests
{
    private readonly AzureServiceBusMessageCreator _creator;

    public AzureServiceBusRelativeCloudEventsUriRoundTripTests()
    {
        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            subscriptionName: new SubscriptionName("test-sub"),
            channelName: new ChannelName("test-channel"),
            routingKey: new RoutingKey("test-topic"),
            messagePumpType: MessagePumpType.Reactor);

        _creator = new AzureServiceBusMessageCreator(subscription);
    }

    [Fact]
    public void When_round_tripping_a_message_with_relative_cloud_events_uris_should_preserve_them()
    {
        // Arrange — both relative URIs are the only data that decides this test
        var relativeSource = new Uri(Uuid.NewAsString(), UriKind.Relative);
        var relativeDataSchema = new Uri("/schemas/v1", UriKind.Relative);

        var header = new MessageHeader(
            messageId: Guid.NewGuid().ToString(),
            topic: new RoutingKey("test-topic"),
            messageType: MessageType.MT_COMMAND,
            source: relativeSource,
            dataSchema: relativeDataSchema);

        var sent = new Message(header, new MessageBody("body"));

        // Act — publish, then read back what went on the wire
        var onTheWire = AzureServiceBusMessagePublisher.ConvertToServiceBusMessage(sent);

        var delivered = new BrokeredMessage
        {
            MessageBodyValue = Encoding.UTF8.GetBytes("body"),
            ApplicationProperties = onTheWire.ApplicationProperties
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            LockToken = Guid.NewGuid().ToString(),
            SequenceNumber = 1L,
            Id = sent.Id.Value,
            CorrelationId = Guid.NewGuid().ToString(),
            ContentType = "application/json"
        };

        var received = _creator.MapToBrighterMessage(delivered);

        // Assert
        Assert.Equal(relativeSource, received.Header.Source);
        Assert.False(received.Header.Source.IsAbsoluteUri);
        Assert.Equal(relativeDataSchema, received.Header.DataSchema);
        Assert.False(received.Header.DataSchema!.IsAbsoluteUri);
    }
}
