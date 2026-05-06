using System;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Transformers.JustSaying;

namespace Paramore.Brighter.Transforms.Adaptors.Tests.JustSaying;

public class JustSayingMessageMapperTest
{
    [Test]
    public async Task MapToMessage_when_mapping_a_justsaying_command_property_should_not_be_override()
    {
        var mapper = new JustSayingMessageMapper<SomeJustSayingCommand>();
        var command = new SomeJustSayingCommand
        {
            Id = Guid.NewGuid().ToString(),
            Conversation = $"Conversation{Guid.NewGuid().ToString()}",
            RaisingComponent = $"Raising{Guid.NewGuid().ToString()}",
            Tenant = $"Tenant{Guid.NewGuid().ToString()}",
            Version = $"Version{Guid.NewGuid().ToString()}",
            TimeStamp = DateTimeOffset.UtcNow,
            Name = $"Name{Guid.NewGuid().ToString()}",
        };
        
        var message = await mapper.MapToMessageAsync(command, new Publication());

        var obj = JsonSerializer.Deserialize<SomeJustSayingCommand>(message.Body.Bytes, JsonSerialisationOptions.Options);
        await Assert.That(obj).IsNotNull();
        await Assert.That(obj.Id).IsEqualTo(command.Id);
        await Assert.That(obj.Conversation).IsEqualTo(command.Conversation);
        await Assert.That(obj.RaisingComponent).IsEqualTo(command.RaisingComponent);
        await Assert.That(obj.Tenant).IsEqualTo(command.Tenant);
        await Assert.That(obj.Version).IsEqualTo(command.Version);
        await Assert.That(obj.TimeStamp).IsEqualTo(command.TimeStamp);
        await Assert.That(obj.Name).IsEqualTo(command.Name);
        await Assert.That(message.Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
    }
    
    [Test]
    public async Task MapToMessage_when_mapping_a_justsaying_event_property_should_not_be_override()
    {
        var mapper = new JustSayingMessageMapper<SomeJustSayingEvent>();
        var @event = new SomeJustSayingEvent
        {
            Id = Guid.NewGuid().ToString(),
            Conversation = $"Conversation{Guid.NewGuid().ToString()}",
            RaisingComponent = $"Raising{Guid.NewGuid().ToString()}",
            Tenant = $"Tenant{Guid.NewGuid().ToString()}",
            Version = $"Version{Guid.NewGuid().ToString()}",
            TimeStamp = DateTimeOffset.UtcNow,
            Name = $"Name{Guid.NewGuid().ToString()}",
            SourceIp = IPAddress.Loopback
        };
        
        var message = await mapper.MapToMessageAsync(@event, new Publication());

        var obj = JsonSerializer.Deserialize<SomeJustSayingCommand>(message.Body.Bytes, JsonSerialisationOptions.Options);
        await Assert.That(obj).IsNotNull();
        await Assert.That(obj.Id).IsEqualTo(@event.Id);
        await Assert.That(obj.Conversation).IsEqualTo(@event.Conversation);
        await Assert.That(obj.RaisingComponent).IsEqualTo(@event.RaisingComponent);
        await Assert.That(obj.Tenant).IsEqualTo(@event.Tenant);
        await Assert.That(obj.Version).IsEqualTo(@event.Version);
        await Assert.That(obj.TimeStamp).IsEqualTo(@event.TimeStamp);
        await Assert.That(obj.Name).IsEqualTo(@event.Name);
        await Assert.That(obj.SourceIp).IsEqualTo(IPAddress.Loopback);
        await Assert.That(message.Header.MessageType).IsEqualTo(MessageType.MT_EVENT);
    }
    
    [Test]
    public async Task MapToMessage_when_mapping_a_command_property_should_not_be_override()
    {
        var mapper = new JustSayingMessageMapper<WithJustSayingProperty>();
        var command = new WithJustSayingProperty
        {
            Id = Guid.NewGuid().ToString(),
            Conversation = $"Conversation{Guid.NewGuid().ToString()}",
            RaisingComponent = $"Raising{Guid.NewGuid().ToString()}",
            Tenant = $"Tenant{Guid.NewGuid().ToString()}",
            Version = $"Version{Guid.NewGuid().ToString()}",
            TimeStamp = DateTimeOffset.UtcNow,
            Name = $"Name{Guid.NewGuid().ToString()}",
        };
        
        var message = await mapper.MapToMessageAsync(command, new Publication());

        var obj = JsonSerializer.Deserialize<WithJustSayingProperty>(message.Body.Bytes, JsonSerialisationOptions.Options);
        await Assert.That(obj).IsNotNull();
        await Assert.That(obj.Id).IsEqualTo(command.Id);
        await Assert.That(obj.Conversation).IsEqualTo(command.Conversation);
        await Assert.That(obj.RaisingComponent).IsEqualTo(command.RaisingComponent);
        await Assert.That(obj.Tenant).IsEqualTo(command.Tenant);
        await Assert.That(obj.Version).IsEqualTo(command.Version);
        await Assert.That(obj.TimeStamp).IsEqualTo(command.TimeStamp);
        await Assert.That(obj.Name).IsEqualTo(command.Name);
        await Assert.That(message.Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
    }
    
    [Test]
    public async Task MapToMessage_when_mapping_a_justsaying_command_property_should_set_from_request_context()
    {
        var mapper = new JustSayingMessageMapper<SomeJustSayingCommand>();
        var command = new SomeJustSayingCommand
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"Name{Guid.NewGuid().ToString()}",
        };

        var subject = $"Subject{Guid.NewGuid().ToString()}";
        var raisingComponent = $"Raising{Guid.NewGuid().ToString()}";
        var tenant = $"Tenant{Guid.NewGuid().ToString()}";
        var version = $"Version{Guid.NewGuid().ToString()}";

        mapper.Context = new RequestContext
        {
            Bag =
            {
                [JustSayingAttributesName.Subject] = subject,
                [JustSayingAttributesName.RaisingComponent] = raisingComponent, 
                [JustSayingAttributesName.Tenant] = tenant,
                [JustSayingAttributesName.Version] = version 
            }
        };
        
        var message = await mapper.MapToMessageAsync(command, new Publication());
        await Assert.That(message.Header.Subject).IsEqualTo(subject);

        var obj = JsonSerializer.Deserialize<SomeJustSayingCommand>(message.Body.Bytes, JsonSerialisationOptions.Options);
        await Assert.That(obj).IsNotNull();
        await Assert.That(Id.IsNullOrEmpty(obj.Id)).IsFalse();
        await Assert.That(obj.Conversation).IsNotNull();
        await Assert.That(Id.IsNullOrEmpty(obj.Conversation)).IsFalse();
        await Assert.That(obj.TimeStamp).IsNotEqualTo(DateTimeOffset.MinValue);
        
        await Assert.That(obj.RaisingComponent).IsEqualTo(raisingComponent);
        await Assert.That(obj.Tenant).IsEqualTo(tenant);
        await Assert.That(obj.Version).IsEqualTo(version);
        await Assert.That(obj.Name).IsEqualTo(command.Name);
        await Assert.That(message.Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
    }
    
    [Test]
    public async Task MapToMessage_when_mapping_a_request_command_property_should_set_from_request_context()
    {
        var mapper = new JustSayingMessageMapper<WithJustSayingProperty>();
        var command = new WithJustSayingProperty
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"Name{Guid.NewGuid().ToString()}",
        };

        var subject = $"Subject{Guid.NewGuid().ToString()}";
        var raisingComponent = $"Raising{Guid.NewGuid().ToString()}";
        var tenant = $"Tenant{Guid.NewGuid().ToString()}";
        var version = $"Version{Guid.NewGuid().ToString()}";

        mapper.Context = new RequestContext
        {
            Bag =
            {
                [JustSayingAttributesName.Subject] = subject,
                [JustSayingAttributesName.RaisingComponent] = raisingComponent, 
                [JustSayingAttributesName.Tenant] = tenant,
                [JustSayingAttributesName.Version] = version 
            }
        };
        
        var message = await mapper.MapToMessageAsync(command, new Publication());
        await Assert.That(message.Header.Subject).IsEqualTo(subject);

        var obj = JsonSerializer.Deserialize<WithJustSayingProperty>(message.Body.Bytes, JsonSerialisationOptions.Options);
        await Assert.That(obj).IsNotNull();
        await Assert.That(Id.IsNullOrEmpty(obj.Id)).IsFalse();
        await Assert.That(obj.Conversation).IsNotNull();
        await Assert.That(obj.Conversation).IsNotEmpty();
        await Assert.That(obj.TimeStamp).IsNotEqualTo(DateTimeOffset.MinValue);
        
        await Assert.That(obj.RaisingComponent).IsEqualTo(raisingComponent);
        await Assert.That(obj.Tenant).IsEqualTo(tenant);
        await Assert.That(obj.Version).IsEqualTo(version);
        await Assert.That(obj.Name).IsEqualTo(command.Name);
        await Assert.That(message.Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
    }
    
    [Test]
    public async Task MapToMessage_when_mapping_a_command_with_partial_justsaying_property_should_set_from_request_context()
    {
        var mapper = new JustSayingMessageMapper<WithPartialJustSayingProperty>();
        var command = new WithPartialJustSayingProperty
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"Name{Guid.NewGuid().ToString()}",
        };

        var subject = $"Subject{Guid.NewGuid().ToString()}";
        var raisingComponent = $"Raising{Guid.NewGuid().ToString()}";
        var tenant = $"Tenant{Guid.NewGuid().ToString()}";
        var version = $"Version{Guid.NewGuid().ToString()}";

        mapper.Context = new RequestContext
        {
            Bag =
            {
                [JustSayingAttributesName.Subject] = subject,
                [JustSayingAttributesName.RaisingComponent] = raisingComponent, 
                [JustSayingAttributesName.Tenant] = tenant,
                [JustSayingAttributesName.Version] = version 
            }
        };
        
        var message = await mapper.MapToMessageAsync(command, new Publication());
        await Assert.That(message.Header.Subject).IsEqualTo(subject);

        var obj = JsonSerializer.Deserialize<WithPartialJustSayingProperty>(message.Body.Bytes, JsonSerialisationOptions.Options);
        await Assert.That(obj).IsNotNull();
        await Assert.That(Id.IsNullOrEmpty(obj.Id)).IsFalse();
        await Assert.That(obj.TimeStamp).IsNotEqualTo(DateTimeOffset.MinValue);
        await Assert.That(obj.Tenant).IsEqualTo(tenant);
        await Assert.That(obj.Name).IsEqualTo(command.Name);
        await Assert.That(message.Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
        
        
        var doc = JsonNode.Parse(message.Body.Bytes, new JsonNodeOptions{ PropertyNameCaseInsensitive = true});
        await Assert.That(doc).IsNotNull();

        var nodeConversation = doc[nameof(IJustSayingRequest.Conversation)]?.GetValue<string>();
        await Assert.That(nodeConversation).IsNotNull();
        await Assert.That(nodeConversation).IsNotEmpty();
        
        var nodeRaisingComponent = doc[nameof(IJustSayingRequest.RaisingComponent)]?.GetValue<string>();
        await Assert.That(nodeRaisingComponent).IsEqualTo(raisingComponent);
        
        var nodeVersion = doc[nameof(IJustSayingRequest.Version)]?.GetValue<string>();
        await Assert.That(nodeVersion).IsEqualTo(version);
    }
    
    [Test]
    public async Task MapToMessage_when_mapping_a_command_with_non_justsaying_property_should_set_from_request_context()
    {
        var mapper = new JustSayingMessageMapper<NonJustSayingProperty>();
        var command = new NonJustSayingProperty
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"Name{Guid.NewGuid().ToString()}",
        };

        var subject = $"Subject{Guid.NewGuid().ToString()}";
        var raisingComponent = $"Raising{Guid.NewGuid().ToString()}";
        var tenant = $"Tenant{Guid.NewGuid().ToString()}";
        var version = $"Version{Guid.NewGuid().ToString()}";

        mapper.Context = new RequestContext
        {
            Bag =
            {
                [JustSayingAttributesName.Subject] = subject,
                [JustSayingAttributesName.RaisingComponent] = raisingComponent, 
                [JustSayingAttributesName.Tenant] = tenant,
                [JustSayingAttributesName.Version] = version 
            }
        };
        
        var message = await mapper.MapToMessageAsync(command, new Publication());
        await Assert.That(message.Header.Subject).IsEqualTo(subject);

        var obj = JsonSerializer.Deserialize<NonJustSayingProperty>(message.Body.Bytes, JsonSerialisationOptions.Options);
        await Assert.That(obj).IsNotNull();
        await Assert.That(Id.IsNullOrEmpty(obj.Id)).IsFalse();
        await Assert.That(obj.Name).IsEqualTo(command.Name);
        await Assert.That(message.Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
        
        var doc = JsonNode.Parse(message.Body.Bytes, new JsonNodeOptions{ PropertyNameCaseInsensitive = true});
        await Assert.That(doc).IsNotNull();
        
        var nodeTimeStamp = doc[nameof(IJustSayingRequest.TimeStamp)]?.GetValue<string>();
        await Assert.That(nodeTimeStamp).IsNotNull();
        await Assert.That(nodeTimeStamp).IsNotEmpty();

        var nodeConversation = doc[nameof(IJustSayingRequest.Conversation)]?.GetValue<string>();
        await Assert.That(nodeConversation).IsNotNull();
        await Assert.That(nodeConversation).IsNotEmpty();
        
        var nodeTenant = doc[nameof(IJustSayingRequest.Tenant)]?.GetValue<string>();
        await Assert.That(nodeTenant).IsEqualTo(tenant);
        
        var nodeRaisingComponent = doc[nameof(IJustSayingRequest.RaisingComponent)]?.GetValue<string>();
        await Assert.That(nodeRaisingComponent).IsEqualTo(raisingComponent);
        
        var nodeVersion = doc[nameof(IJustSayingRequest.Version)]?.GetValue<string>();
        await Assert.That(nodeVersion).IsEqualTo(version);
    }
    
    [Test]
    public async Task MapToMessage_when_mapping_a_request_command_property_should_set_from_publication()
    {
        var mapper = new JustSayingMessageMapper<WithJustSayingProperty>();
        var command = new WithJustSayingProperty
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"Name{Guid.NewGuid().ToString()}",
        };

        var subject = $"Subject{Guid.NewGuid().ToString()}";
        var source = new Uri($"Raising{Guid.NewGuid().ToString()}", UriKind.Relative);

        var message = await mapper.MapToMessageAsync(command, new Publication
        {
            Subject = subject,
            Source = source
        });
        
        await Assert.That(message.Header.Subject).IsEqualTo(subject);

        var obj = JsonSerializer.Deserialize<WithJustSayingProperty>(message.Body.Bytes, JsonSerialisationOptions.Options);
        await Assert.That(obj).IsNotNull();
        await Assert.That(Id.IsNullOrEmpty(obj.Id)).IsFalse();
        await Assert.That(obj.Conversation).IsNotNull();
        await Assert.That(obj.Conversation).IsNotEmpty();
        await Assert.That(obj.TimeStamp).IsNotEqualTo(DateTimeOffset.MinValue);
        
        await Assert.That(obj.RaisingComponent).IsEqualTo(source.ToString());
        await Assert.That(obj.Name).IsEqualTo(command.Name);
        await Assert.That(message.Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
    }
    
    public class SomeJustSayingCommand : JustSayingCommand
    {
        public string Name { get; set; } = string.Empty;
    }
    
    public class SomeJustSayingEvent : JustSayingEvent
    {
        public string Name { get; set; } = string.Empty;
    }
    
    public class NonJustSayingProperty() : Command(Guid.NewGuid())
    {
        public string Name { get; set; } = string.Empty;
    }
    
    public class WithJustSayingProperty() : Command(Guid.NewGuid())
    {
        public string Name { get; set; } = string.Empty;
        
        public DateTimeOffset TimeStamp { get; set; }
    
        public string? RaisingComponent { get; set; }
    
        public string? Version { get; set; }
    
        public string? Tenant { get; set; }
    
        public string? Conversation { get; set; }
    }
    
    public class WithPartialJustSayingProperty() : Command(Guid.NewGuid())
    {
        public string Name { get; set; } = string.Empty;
        
        public DateTimeOffset TimeStamp { get; set; }
    
        public string? Tenant { get; set; }
    }
}
