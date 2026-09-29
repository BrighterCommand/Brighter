using System;
using System.Net;
using System.Text.Json.Nodes;
using Paramore.Brighter.Transformers.JustSaying;
using System.Threading.Tasks;

namespace Paramore.Brighter.Transforms.Adaptors.Tests.JustSaying;

/// <summary>
/// Tests that JustSayingCommand and JustSayingEvent serialize all properties as flat values
/// in the message body produced by JustSayingMessageMapper.
/// </summary>
public class JustSayingPropertySerializationTests
{
    [Test]
    public async Task When_mapping_justsaying_command_should_serialize_all_properties_as_flat_values()
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
        await Assert.That(doc).IsNotNull();

        // Assert - Conversation should be a flat string, not {"Value":"..."}
        var conversationNode = doc[nameof(IJustSayingRequest.Conversation)];
        await Assert.That(conversationNode).IsNotNull();
        await Assert.That(conversationNode).IsAssignableTo<JsonValue>();
        await Assert.That(conversationNode.GetValue<string>()).IsEqualTo(command.Conversation.ToString());

        // Assert - Tenant should be a flat string, not {"Value":"..."}
        var tenantNode = doc[nameof(IJustSayingRequest.Tenant)];
        await Assert.That(tenantNode).IsNotNull();
        await Assert.That(tenantNode).IsAssignableTo<JsonValue>();
        await Assert.That(tenantNode.GetValue<string>()).IsEqualTo(command.Tenant.ToString());

        // Assert - SourceIp should be a flat string, not an object
        var sourceIpNode = doc[nameof(IJustSayingRequest.SourceIp)];
        await Assert.That(sourceIpNode).IsNotNull();
        await Assert.That(sourceIpNode).IsAssignableTo<JsonValue>();
        await Assert.That(sourceIpNode.GetValue<string>()).IsEqualTo(IPAddress.Loopback.ToString());

        // Assert - primitive properties serialize correctly
        await Assert.That(doc[nameof(IJustSayingRequest.RaisingComponent)]?.GetValue<string>()).IsEqualTo(command.RaisingComponent);
        await Assert.That(doc[nameof(IJustSayingRequest.Version)]?.GetValue<string>()).IsEqualTo(command.Version);
        await Assert.That(doc["Name"]?.GetValue<string>()).IsEqualTo(command.Name);
        await Assert.That(doc[nameof(IJustSayingRequest.TimeStamp)]).IsNotNull();
    }

    [Test]
    public async Task When_mapping_justsaying_event_should_serialize_all_properties_as_flat_values()
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
        await Assert.That(doc).IsNotNull();

        // Assert - Conversation should be a flat string, not {"Value":"..."}
        var conversationNode = doc[nameof(IJustSayingRequest.Conversation)];
        await Assert.That(conversationNode).IsNotNull();
        await Assert.That(conversationNode).IsAssignableTo<JsonValue>();
        await Assert.That(conversationNode.GetValue<string>()).IsEqualTo(@event.Conversation.ToString());

        // Assert - Tenant should be a flat string, not {"Value":"..."}
        var tenantNode = doc[nameof(IJustSayingRequest.Tenant)];
        await Assert.That(tenantNode).IsNotNull();
        await Assert.That(tenantNode).IsAssignableTo<JsonValue>();
        await Assert.That(tenantNode.GetValue<string>()).IsEqualTo(@event.Tenant.ToString());

        // Assert - SourceIp should be a flat string, not an object
        var sourceIpNode = doc[nameof(IJustSayingRequest.SourceIp)];
        await Assert.That(sourceIpNode).IsNotNull();
        await Assert.That(sourceIpNode).IsAssignableTo<JsonValue>();
        await Assert.That(sourceIpNode.GetValue<string>()).IsEqualTo(IPAddress.IPv6Loopback.ToString());

        // Assert - primitive properties serialize correctly
        await Assert.That(doc[nameof(IJustSayingRequest.RaisingComponent)]?.GetValue<string>()).IsEqualTo(@event.RaisingComponent);
        await Assert.That(doc[nameof(IJustSayingRequest.Version)]?.GetValue<string>()).IsEqualTo(@event.Version);
        await Assert.That(doc["Name"]?.GetValue<string>()).IsEqualTo(@event.Name);
        await Assert.That(doc[nameof(IJustSayingRequest.TimeStamp)]).IsNotNull();
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
