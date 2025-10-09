using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.JsonMapper.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessageMappers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CloudEvents;

public class CloudEventJsonMessageMapperTests
{
    [Fact]
    public void When_mapping_command_to_message()
    {
        var mapper = new CloudEventJsonMessageMapper<MyCommand>();
        var command = new MyCommand { Value = Guid.NewGuid().ToString() };
        var publication = new Publication { Topic = new RoutingKey(Guid.NewGuid().ToString()) };
        var message = mapper.MapToMessage(command, publication);

        Assert.NotNull(message);
        Assert.Equal(new ContentType("application/cloudevents+json"), message.Header.ContentType);
        Assert.Equal(MessageType.MT_COMMAND, message.Header.MessageType);
        Assert.Equal(publication.Topic, message.Header.Topic);
        Assert.Equal(message.Id, command.Id);
        Assert.NotNull(message.Body);

        var body = JsonSerializer.Deserialize<CloudEventJsonMessageMapper<MyCommand>.CloudEventMessage>(
            message.Body.Bytes, JsonSerialisationOptions.Options);
        Assert.NotNull(body);
        Assert.Equal(message.Id, body.Id);
        Assert.Equal("application/json", body.DataContentType);
        Assert.Equal(command.Value, body.Data.Value);
    }

    [Fact]
    public async Task When_mapping_command_to_message_async()
    {
        var mapper = new CloudEventJsonMessageMapper<MyCommand>();
        var command = new MyCommand { Value = Guid.NewGuid().ToString() };
        var publication = new Publication { Topic = new RoutingKey(Guid.NewGuid().ToString()) };
        var message = await mapper.MapToMessageAsync(command, publication);

        Assert.NotNull(message);
        Assert.Equal(new ContentType("application/cloudevents+json"), message.Header.ContentType);
        Assert.Equal(MessageType.MT_COMMAND, message.Header.MessageType);
        Assert.Equal(publication.Topic, message.Header.Topic);
        Assert.Equal(message.Id, command.Id);
        Assert.NotNull(message.Body);

        var body = JsonSerializer.Deserialize<CloudEventJsonMessageMapper<MyCommand>.CloudEventMessage>(
            message.Body.Bytes, JsonSerialisationOptions.Options);
        Assert.NotNull(body);
        Assert.Equal(message.Id, body.Id);
        Assert.Equal("application/json", body.DataContentType);
        Assert.Equal(command.Value, body.Data.Value);
    }

    [Fact]
    public void When_mapping_event_to_message()
    {
        var mapper = new CloudEventJsonMessageMapper<MyEvent>();
        var @event = new MyEvent { Value = Guid.NewGuid().ToString() };
        var publication = new Publication { Topic = new RoutingKey(Guid.NewGuid().ToString()) };
        var message = mapper.MapToMessage(@event, publication);

        Assert.NotNull(message);
        Assert.Equal(new ContentType("application/cloudevents+json"), message.Header.ContentType);
        Assert.Equal(MessageType.MT_EVENT, message.Header.MessageType);
        Assert.Equal(publication.Topic, message.Header.Topic);
        Assert.Equal(message.Id, @event.Id);
        Assert.NotNull(message.Body);

        var body = JsonSerializer.Deserialize<CloudEventJsonMessageMapper<MyEvent>.CloudEventMessage>(
            message.Body.Bytes, JsonSerialisationOptions.Options);
        Assert.NotNull(body);
        Assert.Equal(message.Id, body.Id);
        Assert.Equal("application/json", body.DataContentType);
        Assert.Equal(@event.Value, body.Data.Value);
    }

    [Fact]
    public async Task When_mapping_event_to_message_async()
    {
        var mapper = new CloudEventJsonMessageMapper<MyEvent>();
        var @event = new MyEvent { Value = Guid.NewGuid().ToString() };
        var publication = new Publication { Topic = new RoutingKey(Guid.NewGuid().ToString()) };
        var message = await mapper.MapToMessageAsync(@event, publication);

        Assert.NotNull(message);
        Assert.Equal(new ContentType("application/cloudevents+json"), message.Header.ContentType);
        Assert.Equal(MessageType.MT_EVENT, message.Header.MessageType);
        Assert.Equal(publication.Topic, message.Header.Topic);
        Assert.Equal(message.Id, @event.Id);
        Assert.NotNull(message.Body);

        var body = JsonSerializer.Deserialize<CloudEventJsonMessageMapper<MyEvent>.CloudEventMessage>(
            message.Body.Bytes, JsonSerialisationOptions.Options);
        Assert.NotNull(body);
        Assert.Equal(message.Id, body.Id);
        Assert.Equal("application/json", body.DataContentType);
        Assert.Equal(@event.Value, body.Data.Value);
    }

    [Fact]
    public void When_mapping_request_to_message_should_throw_ArgumentNullException()
    {
        var mapper = new CloudEventJsonMessageMapper<MyRequest>();
        var request = new MyRequest { Value = Guid.NewGuid().ToString() };
        var publication = new Publication { Topic = new RoutingKey(Guid.NewGuid().ToString()) };

        Assert.Throws<ArgumentException>(() => mapper.MapToMessage(request, publication));
    }

    [Fact]
    public async Task When_mapping_request_to_message_should_throw_ArgumentNullException_async()
    {
        var mapper = new CloudEventJsonMessageMapper<MyRequest>();
        var request = new MyRequest { Value = Guid.NewGuid().ToString() };
        var publication = new Publication { Topic = new RoutingKey(Guid.NewGuid().ToString()) };

        await Assert.ThrowsAsync<ArgumentException>(() => mapper.MapToMessageAsync(request, publication));
    }

    [Fact]
    public void When_mapping_command_to_message_with_additional_properties()
    {
        var mapper = new CloudEventJsonMessageMapper<MyCommand>();
        var command = new MyCommand { Value = Guid.NewGuid().ToString() };
        var extraProperty = new Dictionary<string, object> { ["test"] = Guid.NewGuid().ToString() };
        var publication = new Publication
        {
            Topic = new RoutingKey(Guid.NewGuid().ToString()),
            CloudEventsAdditionalProperties = extraProperty
        };
        
        var message = mapper.MapToMessage(command, publication);

        Assert.NotNull(message);
        Assert.Equal(new ContentType("application/cloudevents+json"), message.Header.ContentType);
        Assert.Equal(MessageType.MT_COMMAND, message.Header.MessageType);
        Assert.Equal(publication.Topic, message.Header.Topic);
        Assert.Equal(message.Id, command.Id);
        Assert.NotNull(message.Body);

        var body = JsonSerializer.Deserialize<CloudEventJsonMessageMapper<MyCommand>.CloudEventMessage>(
            message.Body.Bytes, JsonSerialisationOptions.Options);
        Assert.NotNull(body);
        Assert.Equal(message.Id, body.Id);
        Assert.Equal("application/json", body.DataContentType);
        Assert.Equal(command.Value, body.Data.Value);
        Assert.NotNull(body.AdditionalProperties);
        Assert.Single(body.AdditionalProperties);
        Assert.Equal(extraProperty["test"], body.AdditionalProperties["test"]);
    }
    
    [Fact]
    public void When_mapping_command_to_message_with_duplicated_additional_properties()
    {
        var mapper = new CloudEventJsonMessageMapper<MyCommand>();
        var command = new MyCommand { Value = Guid.NewGuid().ToString() };
        var extraProperty = new Dictionary<string, object> { ["id"] = "test-id" };
        var publication = new Publication
        {
            Topic = new RoutingKey(Guid.NewGuid().ToString()),
            CloudEventsAdditionalProperties = extraProperty
        };
        
        var message = mapper.MapToMessage(command, publication);

        Assert.NotNull(message);
        Assert.Equal(new ContentType("application/cloudevents+json"), message.Header.ContentType);
        Assert.Equal(MessageType.MT_COMMAND, message.Header.MessageType);
        Assert.Equal(publication.Topic, message.Header.Topic);
        Assert.Equal(message.Id, command.Id);
        Assert.NotNull(message.Body);

        var body = JsonSerializer.Deserialize<CloudEventJsonMessageMapper<MyCommand>.CloudEventMessage>(
            message.Body.Bytes, JsonSerialisationOptions.Options);
        Assert.NotNull(body);
        Assert.Equal("test-id", body.Id);
        Assert.Equal("application/json", body.DataContentType);
        Assert.Equal(command.Value, body.Data.Value);
    }

    [Fact]
    public void When_mapping_message_to_command()
    {
        var command = new MyCommand { Value = Guid.NewGuid().ToString() };
        var mapper = new CloudEventJsonMessageMapper<MyCommand>();

        var request =
            mapper.MapToRequest(new Message(new MessageHeader(),
                new MessageBody(
                    JsonSerializer.Serialize(new CloudEventJsonMessageMapper<MyCommand>.CloudEventMessage
                    {
                        Data = command
                    }))));
        Assert.NotNull(request);
        Assert.Equal(command.Value, request.Value);
    }
    
    [Fact]
    public void When_mapping_message_to_command_set_context()
    {
        var command = new MyCommand { Value = Guid.NewGuid().ToString() };
        var mapper = new CloudEventJsonMessageMapper<MyCommand> { Context = new RequestContext() };

        var message = new Message(new MessageHeader{ PartitionKey = "123" },
            new MessageBody(
                JsonSerializer.Serialize(
                    new CloudEventJsonMessageMapper<MyCommand>.CloudEventMessage { Data = command })));
        var request = mapper.MapToRequest(message);

        Assert.NotNull(request);
        Assert.Equal(command.Value, request.Value);
        Assert.Equal(mapper.Context.Bag[RequestContextBagNames.PartitionKey], message.Header.PartitionKey);
    }

    [Fact]
    public async Task When_mapping_message_to_command_async()
    {
        var command = new MyCommand { Value = Guid.NewGuid().ToString() };
        var mapper = new CloudEventJsonMessageMapper<MyCommand>();

        var request = await mapper.MapToRequestAsync(new Message(new MessageHeader(),
            new MessageBody(
                JsonSerializer.Serialize(
                    new CloudEventJsonMessageMapper<MyCommand>.CloudEventMessage { Data = command }))));

        Assert.NotNull(request);
        Assert.Equal(command.Value, request.Value);
    }
    
    [Fact]
    public async Task When_mapping_message_to_command_set_context_async()
    {
        var command = new MyCommand { Value = Guid.NewGuid().ToString() };
        var mapper = new CloudEventJsonMessageMapper<MyCommand> { Context = new RequestContext() };

        var message = new Message(new MessageHeader{ PartitionKey = "123" },
            new MessageBody(
                JsonSerializer.Serialize(
                    new CloudEventJsonMessageMapper<MyCommand>.CloudEventMessage { Data = command })));
        var request = await mapper.MapToRequestAsync(message);

        Assert.NotNull(request);
        Assert.Equal(command.Value, request.Value);
        Assert.Equal(mapper.Context.Bag[RequestContextBagNames.PartitionKey], message.Header.PartitionKey);
        
    }
}
