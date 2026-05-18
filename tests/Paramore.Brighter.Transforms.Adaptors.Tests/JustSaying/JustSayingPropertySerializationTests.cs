using System;
using System.Net;
using System.Text.Json.Nodes;
using Paramore.Brighter.Transformers.JustSaying;
using Xunit;

namespace Paramore.Brighter.Transforms.Adaptors.Tests.JustSaying;

/// <summary>
/// Tests that JustSayingCommand and JustSayingEvent serialize all properties as flat values
/// in the message body produced by JustSayingMessageMapper.
/// </summary>
public class JustSayingPropertySerializationTests
{
    [Fact]
    public void When_mapping_justsaying_command_should_serialize_all_properties_as_flat_values()
    {
        // Arrange
        var mapper = new JustSayingMessageMapper<TestJustSayingCommand>();
        var command = new TestJustSayingCommand
        {
            Id = Guid.NewGuid().ToString(),
            Conversation = Id.Random(),
            RaisingComponent = $"Raising{Guid.NewGuid()}",
            Tenant = $"Tenant{Guid.NewGuid()}",
            Version = $"Version{Guid.NewGuid()}",
            TimeStamp = DateTimeOffset.UtcNow,
            SourceIp = IPAddress.Loopback,
            Name = $"Name{Guid.NewGuid()}"
        };

        // Act
        var message = mapper.MapToMessage(command, new Publication());
        var doc = JsonNode.Parse(message.Body.Bytes, new JsonNodeOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(doc);

        // Assert - Conversation should be a flat string, not {"Value":"..."}
        var conversationNode = doc[nameof(IJustSayingRequest.Conversation)];
        Assert.NotNull(conversationNode);
        Assert.IsAssignableFrom<JsonValue>(conversationNode);
        Assert.Equal(command.Conversation.ToString(), conversationNode.GetValue<string>());

        // Assert - Tenant should be a flat string, not {"Value":"..."}
        var tenantNode = doc[nameof(IJustSayingRequest.Tenant)];
        Assert.NotNull(tenantNode);
        Assert.IsAssignableFrom<JsonValue>(tenantNode);
        Assert.Equal(command.Tenant.ToString(), tenantNode.GetValue<string>());

        // Assert - SourceIp should be a flat string, not an object
        var sourceIpNode = doc[nameof(IJustSayingRequest.SourceIp)];
        Assert.NotNull(sourceIpNode);
        Assert.IsAssignableFrom<JsonValue>(sourceIpNode);
        Assert.Equal(IPAddress.Loopback.ToString(), sourceIpNode.GetValue<string>());

        // Assert - primitive properties serialize correctly
        Assert.Equal(command.RaisingComponent, doc[nameof(IJustSayingRequest.RaisingComponent)]?.GetValue<string>());
        Assert.Equal(command.Version, doc[nameof(IJustSayingRequest.Version)]?.GetValue<string>());
        Assert.Equal(command.Name, doc["Name"]?.GetValue<string>());
        Assert.NotNull(doc[nameof(IJustSayingRequest.TimeStamp)]);
    }

    [Fact]
    public void When_mapping_justsaying_event_should_serialize_all_properties_as_flat_values()
    {
        // Arrange
        var mapper = new JustSayingMessageMapper<TestJustSayingEvent>();
        var @event = new TestJustSayingEvent
        {
            Id = Guid.NewGuid().ToString(),
            Conversation = Id.Random(),
            RaisingComponent = $"Raising{Guid.NewGuid()}",
            Tenant = $"Tenant{Guid.NewGuid()}",
            Version = $"Version{Guid.NewGuid()}",
            TimeStamp = DateTimeOffset.UtcNow,
            SourceIp = IPAddress.IPv6Loopback,
            Name = $"Name{Guid.NewGuid()}"
        };

        // Act
        var message = mapper.MapToMessage(@event, new Publication());
        var doc = JsonNode.Parse(message.Body.Bytes, new JsonNodeOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(doc);

        // Assert - Conversation should be a flat string, not {"Value":"..."}
        var conversationNode = doc[nameof(IJustSayingRequest.Conversation)];
        Assert.NotNull(conversationNode);
        Assert.IsAssignableFrom<JsonValue>(conversationNode);
        Assert.Equal(@event.Conversation.ToString(), conversationNode.GetValue<string>());

        // Assert - Tenant should be a flat string, not {"Value":"..."}
        var tenantNode = doc[nameof(IJustSayingRequest.Tenant)];
        Assert.NotNull(tenantNode);
        Assert.IsAssignableFrom<JsonValue>(tenantNode);
        Assert.Equal(@event.Tenant.ToString(), tenantNode.GetValue<string>());

        // Assert - SourceIp should be a flat string, not an object
        var sourceIpNode = doc[nameof(IJustSayingRequest.SourceIp)];
        Assert.NotNull(sourceIpNode);
        Assert.IsAssignableFrom<JsonValue>(sourceIpNode);
        Assert.Equal(IPAddress.IPv6Loopback.ToString(), sourceIpNode.GetValue<string>());

        // Assert - primitive properties serialize correctly
        Assert.Equal(@event.RaisingComponent, doc[nameof(IJustSayingRequest.RaisingComponent)]?.GetValue<string>());
        Assert.Equal(@event.Version, doc[nameof(IJustSayingRequest.Version)]?.GetValue<string>());
        Assert.Equal(@event.Name, doc["Name"]?.GetValue<string>());
        Assert.NotNull(doc[nameof(IJustSayingRequest.TimeStamp)]);
    }

    public class TestJustSayingCommand : JustSayingCommand
    {
        public string Name { get; set; } = string.Empty;
    }

    public class TestJustSayingEvent : JustSayingEvent
    {
        public string Name { get; set; } = string.Empty;
    }
}

