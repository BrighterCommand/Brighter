using System;
using System.Text.Json;
using Paramore.Brighter.Core.Tests.JsonMapper.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessageMappers;

namespace Paramore.Brighter.Core.Tests.JsonMapper;

public class JsonMessageMapperUtf8SerializationTests
{
    [Test]
    public async Task When_mapping_request_to_message_should_produce_valid_utf8_body()
    {
        var mapper = new JsonMessageMapper<MyCommand>();
        var command = new MyCommand { Value = "test-value" };
        var publication = new Publication { Topic = new RoutingKey("test.topic") };

        var message = mapper.MapToMessage(command, publication);

        var deserialized = JsonSerializer.Deserialize<MyCommand>(message.Body.Memory.Span, JsonSerialisationOptions.Options);
        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized!.Value).IsEqualTo(command.Value);
    }

    [Test]
    public async Task When_mapping_request_to_message_should_set_utf8_encoding()
    {
        var mapper = new JsonMessageMapper<MyCommand>();
        var command = new MyCommand { Value = "test-value" };
        var publication = new Publication { Topic = new RoutingKey("test.topic") };

        var message = mapper.MapToMessage(command, publication);

        await Assert.That(message.Body.CharacterEncoding).IsEqualTo(CharacterEncoding.UTF8);
    }

    [Test]
    public async Task When_mapping_request_to_message_and_back_should_round_trip()
    {
        var mapper = new JsonMessageMapper<MyCommand>();
        var command = new MyCommand { Value = "round-trip-value" };
        var publication = new Publication { Topic = new RoutingKey("test.topic") };

        var message = mapper.MapToMessage(command, publication);
        var result = mapper.MapToRequest(message);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value).IsEqualTo(command.Value);
    }

    [Test]
    public async Task When_mapping_request_to_message_should_deserialize_from_memory_span()
    {
        var mapper = new JsonMessageMapper<MyCommand>();
        var command = new MyCommand { Value = "span-deserialize" };
        var publication = new Publication { Topic = new RoutingKey("test.topic") };

        var message = mapper.MapToMessage(command, publication);

        var fromSpan = JsonSerializer.Deserialize<MyCommand>(message.Body.Memory.Span, JsonSerialisationOptions.Options);
        await Assert.That(fromSpan).IsNotNull();
        await Assert.That(fromSpan!.Value).IsEqualTo(command.Value);
        await Assert.That(fromSpan.Id).IsEqualTo(command.Id);
    }
}
