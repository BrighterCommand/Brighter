using System;
using System.Text.Json;
using Paramore.Brighter.Core.Tests.CloudEvents.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessageMappers;

namespace Paramore.Brighter.Core.Tests.CloudEvents;

public class CloudEventJsonMessageMapperUtf8SerializationTests
{
    [Test]
    public async Task When_mapping_cloud_event_to_message_should_produce_valid_utf8_body()
    {
        var mapper = new CloudEventJsonMessageMapper<MyCommand>();
        var command = new MyCommand { Value = "test-value" };
        var publication = new Publication
        {
            Topic = new RoutingKey("test.topic"),
            Source = new Uri("http://test.source"),
            Type = (CloudEventsType)"test.type",
            Subject = "test-subject",
            DataSchema = new Uri("http://test.schema")
        };

        var message = mapper.MapToMessage(command, publication);

        var envelope = JsonSerializer.Deserialize<CloudEventJsonMessageMapper<MyCommand>.CloudEventMessage>(
            message.Body.Memory.Span, JsonSerialisationOptions.Options);
        await Assert.That(envelope).IsNotNull();
        await Assert.That(envelope!.Data.Value).IsEqualTo(command.Value);
    }

    [Test]
    public async Task When_mapping_cloud_event_to_message_should_preserve_envelope_fields()
    {
        var mapper = new CloudEventJsonMessageMapper<MyCommand>();
        var command = new MyCommand { Value = "envelope-test" };
        var source = new Uri("http://test.source");
        var dataSchema = new Uri("http://test.schema");
        var publication = new Publication
        {
            Topic = new RoutingKey("test.topic"),
            Source = source,
            Type = (CloudEventsType)"test.type",
            Subject = "test-subject",
            DataSchema = dataSchema
        };

        var message = mapper.MapToMessage(command, publication);

        var envelope = JsonSerializer.Deserialize<CloudEventJsonMessageMapper<MyCommand>.CloudEventMessage>(
            message.Body.Memory.Span, JsonSerialisationOptions.Options);
        await Assert.That(envelope).IsNotNull();
        await Assert.That(envelope!.Source).IsEqualTo(source);
        await Assert.That(envelope.Type).IsEqualTo("test.type");
        await Assert.That(envelope.Subject).IsEqualTo("test-subject");
        await Assert.That(envelope.DataSchema).IsEqualTo(dataSchema);
    }

    [Test]
    public async Task When_mapping_cloud_event_to_message_and_back_should_round_trip()
    {
        var mapper = new CloudEventJsonMessageMapper<MyCommand>();
        var command = new MyCommand { Value = "round-trip-value" };
        var publication = new Publication
        {
            Topic = new RoutingKey("test.topic"),
            Source = new Uri("http://test.source"),
            Type = (CloudEventsType)"test.type"
        };

        var message = mapper.MapToMessage(command, publication);
        var result = mapper.MapToRequest(message);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value).IsEqualTo(command.Value);
    }
}
