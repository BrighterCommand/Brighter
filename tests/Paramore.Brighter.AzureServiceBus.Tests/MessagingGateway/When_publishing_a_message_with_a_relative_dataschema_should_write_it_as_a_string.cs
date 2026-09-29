using System;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

/// <summary>
/// The Azure Service Bus SDK serialises a <see cref="Uri"/> application property via
/// <c>Uri.AbsoluteUri</c>, which throws <see cref="InvalidOperationException"/> for a relative URI.
/// So a <c>dataschema</c> must go onto the wire as a string, exactly as <c>source</c> already does —
/// otherwise publishing a message with a relative dataschema fails inside the SDK at send time.
/// </summary>
[Property("Category", "ASB")]
public class AzureServiceBusRelativeDataSchemaPublishTests
{
    [Test]
    public async System.Threading.Tasks.Task When_publishing_a_message_with_a_relative_dataschema_should_write_it_as_a_string()
    {
        // Arrange — the relative dataschema is the only data that decides this test
        var relativeDataSchema = new Uri("/schemas/v1", UriKind.Relative);

        var header = new MessageHeader(
            messageId: Guid.NewGuid().ToString(),
            topic: new RoutingKey("test.topic"),
            messageType: MessageType.MT_COMMAND,
            dataSchema: relativeDataSchema);

        var message = new Message(header, new MessageBody("body"));

        // Act
        var asbMessage = AzureServiceBusMessagePublisher.ConvertToServiceBusMessage(message);

        // Assert
        // The CloudEvents keys are written as string literals, not via ASBConstants, deliberately:
        // these are wire-format attribute names, so a change to a constant's *value* must fail a test
        // rather than silently rename an on-wire attribute. (ASBConstants is internal in any case.)
        var written = asbMessage.ApplicationProperties["cloudEvents:schema"];
        await Assert.That(written).IsTypeOf<string>();
        await Assert.That(written).IsEqualTo(relativeDataSchema.ToString());
    }
}
