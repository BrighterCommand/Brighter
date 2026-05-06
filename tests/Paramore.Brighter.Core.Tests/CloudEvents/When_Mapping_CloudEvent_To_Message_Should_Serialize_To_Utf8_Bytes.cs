using System;
using System.Text.Json;
using Paramore.Brighter.Core.Tests.CloudEvents.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessageMappers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CloudEvents;

public class CloudEventJsonMessageMapperUtf8SerializationTests
{
    [Fact]
    public void When_mapping_cloud_event_to_message_should_produce_valid_utf8_body()
    {
        // Arrange
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

        // Act
        var message = mapper.MapToMessage(command, publication);

        // Assert — body bytes are valid UTF-8 JSON deserializable from Memory.Span
        var envelope = JsonSerializer.Deserialize<CloudEventJsonMessageMapper<MyCommand>.CloudEventMessage>(
            message.Body.Memory.Span, JsonSerialisationOptions.Options);
        Assert.NotNull(envelope);
        Assert.Equal(command.Value, envelope.Data.Value);
    }

    [Fact]
    public void When_mapping_cloud_event_to_message_should_preserve_envelope_fields()
    {
        // Arrange
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

        // Act
        var message = mapper.MapToMessage(command, publication);

        // Assert — cloud event envelope fields are preserved
        var envelope = JsonSerializer.Deserialize<CloudEventJsonMessageMapper<MyCommand>.CloudEventMessage>(
            message.Body.Memory.Span, JsonSerialisationOptions.Options);
        Assert.NotNull(envelope);
        Assert.Equal(source, envelope.Source);
        Assert.Equal("test.type", envelope.Type);
        Assert.Equal("test-subject", envelope.Subject);
        Assert.Equal(dataSchema, envelope.DataSchema);
    }

    [Fact]
    public void When_mapping_cloud_event_to_message_and_back_should_round_trip()
    {
        // Arrange
        var mapper = new CloudEventJsonMessageMapper<MyCommand>();
        var command = new MyCommand { Value = "round-trip-value" };
        var publication = new Publication
        {
            Topic = new RoutingKey("test.topic"),
            Source = new Uri("http://test.source"),
            Type = (CloudEventsType)"test.type"
        };

        // Act
        var message = mapper.MapToMessage(command, publication);
        var result = mapper.MapToRequest(message);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(command.Value, result.Value);
    }
}
