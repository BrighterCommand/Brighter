using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.JsonMapper.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessageMappers;

namespace Paramore.Brighter.Core.Tests.CloudEvents;
public class CloudEventJsonMessageMapperTests
{
    [Test]
    public async Task When_mapping_command_to_message()
    {
        var mapper = new CloudEventJsonMessageMapper<MyCommand>();
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
        await Assert.That(message.Header.ContentType).IsEqualTo(new ContentType("application/cloudevents+json"));
        await Assert.That(message.Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
        await Assert.That(message.Header.Topic).IsEqualTo(publication.Topic);
        await Assert.That(command.Id).IsEqualTo(message.Id);
        await Assert.That(message.Body).IsNotNull();
        var body = JsonSerializer.Deserialize<CloudEventJsonMessageMapper<MyCommand>.CloudEventMessage>(message.Body.Bytes, JsonSerialisationOptions.Options);
        await Assert.That(body).IsNotNull();
        await Assert.That(body.Id).IsEqualTo(message.Id);
        await Assert.That(body.DataContentType).IsEqualTo("application/json");
        await Assert.That(body.Data.Value).IsEqualTo(command.Value);
    }

    [Test]
    public async Task When_mapping_command_to_message_async()
    {
        var mapper = new CloudEventJsonMessageMapper<MyCommand>();
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
        await Assert.That(message.Header.ContentType).IsEqualTo(new ContentType("application/cloudevents+json"));
        await Assert.That(message.Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
        await Assert.That(message.Header.Topic).IsEqualTo(publication.Topic);
        await Assert.That(command.Id).IsEqualTo(message.Id);
        await Assert.That(message.Body).IsNotNull();
        var body = JsonSerializer.Deserialize<CloudEventJsonMessageMapper<MyCommand>.CloudEventMessage>(message.Body.Bytes, JsonSerialisationOptions.Options);
        await Assert.That(body).IsNotNull();
        await Assert.That(body.Id).IsEqualTo(message.Id);
        await Assert.That(body.DataContentType).IsEqualTo("application/json");
        await Assert.That(body.Data.Value).IsEqualTo(command.Value);
    }

    [Test]
    public async Task When_mapping_event_to_message()
    {
        var mapper = new CloudEventJsonMessageMapper<MyEvent>();
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
        await Assert.That(message.Header.ContentType).IsEqualTo(new ContentType("application/cloudevents+json"));
        await Assert.That(message.Header.MessageType).IsEqualTo(MessageType.MT_EVENT);
        await Assert.That(message.Header.Topic).IsEqualTo(publication.Topic);
        await Assert.That(@event.Id).IsEqualTo(message.Id);
        await Assert.That(message.Body).IsNotNull();
        var body = JsonSerializer.Deserialize<CloudEventJsonMessageMapper<MyEvent>.CloudEventMessage>(message.Body.Bytes, JsonSerialisationOptions.Options);
        await Assert.That(body).IsNotNull();
        await Assert.That(body.Id).IsEqualTo(message.Id);
        await Assert.That(body.DataContentType).IsEqualTo("application/json");
        await Assert.That(body.Data.Value).IsEqualTo(@event.Value);
    }

    [Test]
    public async Task When_mapping_event_to_message_async()
    {
        var mapper = new CloudEventJsonMessageMapper<MyEvent>();
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
        await Assert.That(message.Header.ContentType).IsEqualTo(new ContentType("application/cloudevents+json"));
        await Assert.That(message.Header.MessageType).IsEqualTo(MessageType.MT_EVENT);
        await Assert.That(message.Header.Topic).IsEqualTo(publication.Topic);
        await Assert.That(@event.Id).IsEqualTo(message.Id);
        await Assert.That(message.Body).IsNotNull();
        var body = JsonSerializer.Deserialize<CloudEventJsonMessageMapper<MyEvent>.CloudEventMessage>(message.Body.Bytes, JsonSerialisationOptions.Options);
        await Assert.That(body).IsNotNull();
        await Assert.That(body.Id).IsEqualTo(message.Id);
        await Assert.That(body.DataContentType).IsEqualTo("application/json");
        await Assert.That(body.Data.Value).IsEqualTo(@event.Value);
    }

    [Test]
    public async Task When_mapping_request_to_message_should_throw_ArgumentNullException()
    {
        var mapper = new CloudEventJsonMessageMapper<MyRequest>();
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
        var mapper = new CloudEventJsonMessageMapper<MyRequest>();
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
    public async Task When_mapping_command_to_message_with_additional_properties()
    {
        var mapper = new CloudEventJsonMessageMapper<MyCommand>();
        var command = new MyCommand
        {
            Value = Guid.NewGuid().ToString()
        };
        var extraProperty = new Dictionary<string, object>
        {
            ["test"] = Guid.NewGuid().ToString()
        };
        var publication = new Publication
        {
            Topic = new RoutingKey(Guid.NewGuid().ToString()),
            CloudEventsAdditionalProperties = extraProperty
        };
        var message = await mapper.MapToMessageAsync(command, publication);
        await Assert.That(message).IsNotNull();
        await Assert.That(message.Header.ContentType).IsEqualTo(new ContentType("application/cloudevents+json"));
        await Assert.That(message.Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
        await Assert.That(message.Header.Topic).IsEqualTo(publication.Topic);
        await Assert.That(command.Id).IsEqualTo(message.Id);
        await Assert.That(message.Body).IsNotNull();
        var body = JsonSerializer.Deserialize<CloudEventJsonMessageMapper<MyCommand>.CloudEventMessage>(message.Body.Bytes, JsonSerialisationOptions.Options);
        await Assert.That(body).IsNotNull();
        await Assert.That(body.Id).IsEqualTo(message.Id);
        await Assert.That(body.DataContentType).IsEqualTo("application/json");
        await Assert.That(body.Data.Value).IsEqualTo(command.Value);
        await Assert.That(body.AdditionalProperties).IsNotNull();
        await Assert.That(body.AdditionalProperties).HasSingleItem();
        await Assert.That(body.AdditionalProperties["test"]).IsEqualTo(extraProperty["test"]);
    }

    [Test]
    public async Task When_mapping_command_to_message_with_duplicated_additional_properties()
    {
        var mapper = new CloudEventJsonMessageMapper<MyCommand>();
        var command = new MyCommand
        {
            Value = Guid.NewGuid().ToString()
        };
        var extraProperty = new Dictionary<string, object>
        {
            ["id"] = "test-id"
        };
        var publication = new Publication
        {
            Topic = new RoutingKey(Guid.NewGuid().ToString()),
            CloudEventsAdditionalProperties = extraProperty
        };
        var message = await mapper.MapToMessageAsync(command, publication);
        await Assert.That(message).IsNotNull();
        await Assert.That(message.Header.ContentType).IsEqualTo(new ContentType("application/cloudevents+json"));
        await Assert.That(message.Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
        await Assert.That(message.Header.Topic).IsEqualTo(publication.Topic);
        await Assert.That(command.Id).IsEqualTo(message.Id);
        await Assert.That(message.Body).IsNotNull();
        var body = JsonSerializer.Deserialize<CloudEventJsonMessageMapper<MyCommand>.CloudEventMessage>(message.Body.Bytes, JsonSerialisationOptions.Options);
        await Assert.That(body).IsNotNull();
        await Assert.That(body.Id).IsEqualTo("test-id");
        await Assert.That(body.DataContentType).IsEqualTo("application/json");
        await Assert.That(body.Data.Value).IsEqualTo(command.Value);
    }

    [Test]
    public async Task When_mapping_message_to_command()
    {
        var command = new MyCommand
        {
            Value = Guid.NewGuid().ToString()
        };
        var mapper = new CloudEventJsonMessageMapper<MyCommand>();
        var request = await mapper.MapToRequestAsync(new Message(new MessageHeader(), new MessageBody(JsonSerializer.Serialize(new CloudEventJsonMessageMapper<MyCommand>.CloudEventMessage { Data = command }))));
        await Assert.That(request).IsNotNull();
        await Assert.That(request.Value).IsEqualTo(command.Value);
    }

    [Test]
    public async Task When_mapping_message_to_command_async()
    {
        var command = new MyCommand
        {
            Value = Guid.NewGuid().ToString()
        };
        var mapper = new CloudEventJsonMessageMapper<MyCommand>();
        var request = await mapper.MapToRequestAsync(new Message(new MessageHeader(), new MessageBody(JsonSerializer.Serialize(new CloudEventJsonMessageMapper<MyCommand>.CloudEventMessage { Data = command }))));
        await Assert.That(request).IsNotNull();
        await Assert.That(request.Value).IsEqualTo(command.Value);
    }
}