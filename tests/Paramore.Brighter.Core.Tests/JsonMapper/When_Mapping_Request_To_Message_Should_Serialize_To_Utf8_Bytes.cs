using System;
using System.Text.Json;
using Paramore.Brighter.Core.Tests.JsonMapper.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessageMappers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.JsonMapper;

public class JsonMessageMapperUtf8SerializationTests
{
    [Fact]
    public void When_mapping_request_to_message_should_produce_valid_utf8_body()
    {
        // Arrange
        var mapper = new JsonMessageMapper<MyCommand>();
        var command = new MyCommand { Value = "test-value" };
        var publication = new Publication { Topic = new RoutingKey("test.topic") };

        // Act
        var message = mapper.MapToMessage(command, publication);

        // Assert — body bytes are valid UTF-8 JSON that deserialises back
        var deserialized = JsonSerializer.Deserialize<MyCommand>(message.Body.Memory.Span, JsonSerialisationOptions.Options);
        Assert.NotNull(deserialized);
        Assert.Equal(command.Value, deserialized.Value);
    }

    [Fact]
    public void When_mapping_request_to_message_should_set_utf8_encoding()
    {
        // Arrange
        var mapper = new JsonMessageMapper<MyCommand>();
        var command = new MyCommand { Value = "test-value" };
        var publication = new Publication { Topic = new RoutingKey("test.topic") };

        // Act
        var message = mapper.MapToMessage(command, publication);

        // Assert
        Assert.Equal(CharacterEncoding.UTF8, message.Body.CharacterEncoding);
    }

    [Fact]
    public void When_mapping_request_to_message_and_back_should_round_trip()
    {
        // Arrange
        var mapper = new JsonMessageMapper<MyCommand>();
        var command = new MyCommand { Value = "round-trip-value" };
        var publication = new Publication { Topic = new RoutingKey("test.topic") };

        // Act
        var message = mapper.MapToMessage(command, publication);
        var result = mapper.MapToRequest(message);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(command.Value, result.Value);
    }

    [Fact]
    public void When_mapping_request_to_message_should_deserialize_from_memory_span()
    {
        // Arrange
        var mapper = new JsonMessageMapper<MyCommand>();
        var command = new MyCommand { Value = "span-deserialize" };
        var publication = new Publication { Topic = new RoutingKey("test.topic") };

        // Act
        var message = mapper.MapToMessage(command, publication);

        // Assert — can deserialize directly from the Memory.Span (the optimized path)
        var fromSpan = JsonSerializer.Deserialize<MyCommand>(message.Body.Memory.Span, JsonSerialisationOptions.Options);
        Assert.NotNull(fromSpan);
        Assert.Equal(command.Value, fromSpan.Value);
        Assert.Equal(command.Id, fromSpan.Id);
    }
}
