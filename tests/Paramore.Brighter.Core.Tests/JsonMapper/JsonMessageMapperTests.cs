using System;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.JsonMapper.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessageMappers;

namespace Paramore.Brighter.Core.Tests.JsonMapper;
public class JsonMessageMapperTests
{
    [Test]
    public async Task When_mapping_command_to_message()
    {
        var mapper = new JsonMessageMapper<MyCommand>();
        var command = new MyCommand
        {
            Value = Guid.NewGuid().ToString()
        };
        var publication = new Publication
        {
            Topic = new RoutingKey(Guid.NewGuid().ToString())
        };
        var message = await mapper.MapToMessageAsync(command, publication);
        await Assert.That(message).IsNotNull();
        await Assert.That(message.Header.ContentType).IsEqualTo(new ContentType(MediaTypeNames.Application.Json));
        await Assert.That(message.Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
        await Assert.That(message.Header.Topic).IsEqualTo(publication.Topic);
        await Assert.That(command.Id).IsEqualTo(message.Id);
        await Assert.That(message.Body).IsNotNull();
        var body = JsonSerializer.Deserialize<MyCommand>(message.Body.Bytes, JsonSerialisationOptions.Options);
        await Assert.That(body).IsNotNull();
        await Assert.That(body.Value).IsEqualTo(command.Value);
    }

    [Test]
    public async Task When_mapping_command_to_message_async()
    {
        var mapper = new JsonMessageMapper<MyCommand>();
        var command = new MyCommand
        {
            Value = Guid.NewGuid().ToString()
        };
        var publication = new Publication
        {
            Topic = new RoutingKey(Guid.NewGuid().ToString())
        };
        var message = await mapper.MapToMessageAsync(command, publication);
        await Assert.That(message).IsNotNull();
        await Assert.That(message.Header.ContentType).IsEqualTo(new ContentType(MediaTypeNames.Application.Json));
        await Assert.That(message.Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
        await Assert.That(message.Header.Topic).IsEqualTo(publication.Topic);
        await Assert.That(command.Id).IsEqualTo(message.Id);
        await Assert.That(message.Body).IsNotNull();
        var body = JsonSerializer.Deserialize<MyCommand>(message.Body.Bytes, JsonSerialisationOptions.Options);
        await Assert.That(body).IsNotNull();
        await Assert.That(body.Value).IsEqualTo(command.Value);
    }

    [Test]
    public async Task When_mapping_event_to_message()
    {
        var mapper = new JsonMessageMapper<MyEvent>();
        var @event = new MyEvent
        {
            Value = Guid.NewGuid().ToString()
        };
        var publication = new Publication
        {
            Topic = new RoutingKey(Guid.NewGuid().ToString())
        };
        var message = await mapper.MapToMessageAsync(@event, publication);
        await Assert.That(message).IsNotNull();
        await Assert.That(message.Header.ContentType).IsEqualTo(new ContentType(MediaTypeNames.Application.Json));
        await Assert.That(message.Header.MessageType).IsEqualTo(MessageType.MT_EVENT);
        await Assert.That(message.Header.Topic).IsEqualTo(publication.Topic);
        await Assert.That(@event.Id).IsEqualTo(message.Id);
        await Assert.That(message.Body).IsNotNull();
        var body = JsonSerializer.Deserialize<MyCommand>(message.Body.Bytes, JsonSerialisationOptions.Options);
        await Assert.That(body).IsNotNull();
        await Assert.That(body.Value).IsEqualTo(@event.Value);
    }

    [Test]
    public async Task When_mapping_event_to_message_async()
    {
        var mapper = new JsonMessageMapper<MyEvent>();
        var @event = new MyEvent
        {
            Value = Guid.NewGuid().ToString()
        };
        var publication = new Publication
        {
            Topic = new RoutingKey(Guid.NewGuid().ToString())
        };
        var message = await mapper.MapToMessageAsync(@event, publication);
        await Assert.That(message).IsNotNull();
        await Assert.That(message.Header.ContentType).IsEqualTo(new ContentType(MediaTypeNames.Application.Json));
        await Assert.That(message.Header.MessageType).IsEqualTo(MessageType.MT_EVENT);
        await Assert.That(message.Header.Topic).IsEqualTo(publication.Topic);
        await Assert.That(@event.Id).IsEqualTo(message.Id);
        await Assert.That(message.Body).IsNotNull();
        var body = JsonSerializer.Deserialize<MyCommand>(message.Body.Bytes, JsonSerialisationOptions.Options);
        await Assert.That(body).IsNotNull();
        await Assert.That(body.Value).IsEqualTo(@event.Value);
    }

    [Test]
    public async Task When_mapping_request_to_message_should_throw_ArgumentNullException()
    {
        var mapper = new JsonMessageMapper<MyRequest>();
        var request = new MyRequest
        {
            Value = Guid.NewGuid().ToString()
        };
        var publication = new Publication
        {
            Topic = new RoutingKey(Guid.NewGuid().ToString())
        };
        await Assert.That(() => mapper.MapToMessage(request, publication)).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_mapping_request_to_message_should_throw_ArgumentNullException_async()
    {
        var mapper = new JsonMessageMapper<MyRequest>();
        var request = new MyRequest
        {
            Value = Guid.NewGuid().ToString()
        };
        var publication = new Publication
        {
            Topic = new RoutingKey(Guid.NewGuid().ToString())
        };
        await Assert.That(() => mapper.MapToMessageAsync(request, publication)).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_mapping_message_to_command()
    {
        var command = new MyCommand
        {
            Value = Guid.NewGuid().ToString()
        };
        var mapper = new JsonMessageMapper<MyCommand>();

        var request = mapper.MapToRequest(new Message(new MessageHeader(), new MessageBody(JsonSerializer.Serialize(command))));
        await Assert.That(request).IsNotNull();
        await Assert.That(request.Value).IsEqualTo(command.Value);
    }

    [Test]
    public async Task When_mapping_message_to_command_async ()
    {
        var command = new MyCommand { Value = Guid.NewGuid().ToString() };
        var mapper = new JsonMessageMapper<MyCommand>();

        var request = await mapper.MapToRequestAsync(new Message(new MessageHeader(), new MessageBody(JsonSerializer.Serialize(command))));
        await Assert.That(request).IsNotNull();
        await Assert.That(request.Value).IsEqualTo(command.Value);
    }

    [Test]
    public async Task When_mapping_command_to_message_with_null_reply_to()
    {
        //Arrange
        var mapper = new JsonMessageMapper<MyCommand>();
        var command = new MyCommand { Value = Guid.NewGuid().ToString() };
        var publication = new Publication
        {
            Topic = new RoutingKey("test-topic"),
            ReplyTo = null
        };

        //Act
        var message = mapper.MapToMessage(command, publication);

        //Assert
        await Assert.That(message.Header.ReplyTo).IsEqualTo(RoutingKey.Empty);
    }
}
