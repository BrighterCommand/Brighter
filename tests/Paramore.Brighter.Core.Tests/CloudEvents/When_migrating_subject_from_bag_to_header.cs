using System;
using System.Net.Mime;
using System.Text.Json;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CloudEvents;

/// <summary>
/// Demonstrates the V9 → V10 migration issue for Subject.
/// In V9, users put Subject in the Bag and the Bag preserved PascalCase keys.
/// In V10, the Bag serialization applies camelCase to dictionary keys, so "Subject" becomes "subject".
/// The fix is to use MessageHeader.Subject instead of the Bag, which feeds the native SNS Subject field.
/// </summary>
public class CloudEventsBagSubjectMigrationTests
{
    [Fact]
    public void When_subject_is_in_bag_serialization_converts_to_camelCase()
    {
        //Arrange - V9 pattern: user puts Subject in the Bag with PascalCase key
        var message = new Message(
            new MessageHeader(Id.Random(), new RoutingKey("Test.Topic"), MessageType.MT_EVENT),
            new MessageBody("{\"orderId\": 123}"));

        message.Header.Bag.Add("Subject", "OrderAccepted");

        //Act - serialize the Bag as V10 does (using JsonSerialisationOptions with CamelCase policy)
        var bagJson = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);

        //Assert - the key is now camelCase, so a consumer looking for "Subject" won't find it
        using var doc = JsonDocument.Parse(bagJson);
        Assert.True(doc.RootElement.TryGetProperty("subject", out _), "Bag key should be serialized as camelCase 'subject'");
        Assert.False(doc.RootElement.TryGetProperty("Subject", out _), "Bag key 'Subject' should not survive serialization as PascalCase");
    }

    [Fact]
    public void When_subject_is_set_on_header_it_is_available_for_sns_publish_request()
    {
        //Arrange - V10 pattern: user sets Subject on the MessageHeader directly
        const string expectedSubject = "OrderAccepted";
        var message = new Message(
            new MessageHeader(
                Id.Random(),
                new RoutingKey("Test.Topic"),
                MessageType.MT_EVENT,
                subject: expectedSubject),
            new MessageBody("{\"orderId\": 123}"));

        //Assert - Header.Subject is the value the SNS publisher uses for the native SNS Subject field
        // (PublishRequest constructor third argument), which AWS serializes as PascalCase "Subject" in the
        // SNS notification envelope that downstream consumers (e.g. Python Lambdas) read
        Assert.Equal(expectedSubject, message.Header.Subject);
    }

    [Fact]
    public void When_subject_is_set_on_header_cloud_events_transformer_preserves_it()
    {
        //Arrange - V10 pattern: mapper sets Subject on header, publication has no Subject
        const string expectedSubject = "OrderAccepted";
        var message = new Message(
            new MessageHeader(
                Id.Random(),
                new RoutingKey("Test.Topic"),
                MessageType.MT_EVENT,
                subject: expectedSubject),
            new MessageBody("{\"orderId\": 123}"));

        var transformer = new CloudEventsTransformer();
        var publication = new Publication(); // empty, no subject

        //Act - CloudEventsTransformer wraps the message
        var wrapped = transformer.Wrap(message, publication);

        //Assert - Subject survives the transformer and is available for the SNS publisher
        Assert.Equal(expectedSubject, wrapped.Header.Subject);
    }
}
