using System;
using System.Net.Mime;
using System.Text.Json;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CloudEvents;

/// <summary>
/// Covers the V9 → V10 migration for Subject (PR #4132), and the root-cause fix for #4151 / #4054.
/// In V9, users put Subject in the Bag and the Bag preserved PascalCase keys. V10 briefly applied the
/// camelCase naming policy to dictionary keys, so "Subject" became "subject" and downstream consumers
/// (e.g. Python Lambdas reading SNS notifications) failed with KeyError. Bag keys are arbitrary user
/// identifiers, not C# property names, so the naming policy is no longer applied to them and the key
/// now round-trips verbatim. Using MessageHeader.Subject (which feeds the native SNS Subject field)
/// remains the recommended pattern.
/// </summary>
public class When_migrating_subject_from_bag_to_header
{
    [Fact]
    public void When_subject_is_in_bag_serialization_preserves_the_key_verbatim()
    {
        //Arrange - V9 pattern: user puts Subject in the Bag with a PascalCase key
        var message = new Message(
            new MessageHeader(Id.Random(), new RoutingKey("Test.Topic"), MessageType.MT_EVENT),
            new MessageBody("{\"orderId\": 123}"));

        message.Header.Bag.Add("Subject", "OrderAccepted");

        //Act - serialize the Bag through Brighter's options
        var bagJson = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);

        //Assert - the key survives verbatim, so a consumer looking for "Subject" still finds it
        using var doc = JsonDocument.Parse(bagJson);
        Assert.True(doc.RootElement.TryGetProperty("Subject", out _), "Bag key 'Subject' should survive serialization verbatim");
        Assert.False(doc.RootElement.TryGetProperty("subject", out _), "Bag key should not be rewritten to camelCase 'subject'");
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
