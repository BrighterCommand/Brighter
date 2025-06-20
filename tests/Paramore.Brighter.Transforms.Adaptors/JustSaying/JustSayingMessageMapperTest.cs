using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Transformers.JustSaying;
using Xunit;

namespace Paramore.Brighter.Transforms.Adaptors.JustSaying;

public class JustSayingMessageMapperTest
{
    [Fact]
    public void MapToMessage_when_mapping_a_justsaying_command_property_should_not_be_override()
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
        
        var message = mapper.MapToMessage(command, new Publication());

        var obj = JsonSerializer.Deserialize<SomeJustSayingCommand>(message.Body.Bytes, JsonSerialisationOptions.Options);
        Assert.NotNull(obj);
        Assert.Equal(command.Id, obj.Id);
        Assert.Equal(command.Conversation, obj.Conversation);
        Assert.Equal(command.RaisingComponent, obj.RaisingComponent);
        Assert.Equal(command.Tenant, obj.Tenant);
        Assert.Equal(command.Version, obj.Version);
        Assert.Equal(command.TimeStamp, obj.TimeStamp);
        Assert.Equal(command.Name, obj.Name);
        Assert.Equal(MessageType.MT_COMMAND, message.Header.MessageType);
    }
    
    [Fact]
    public void MapToMessage_when_mapping_a_justsaying_event_property_should_not_be_override()
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
        };
        
        var message = mapper.MapToMessage(@event, new Publication());

        var obj = JsonSerializer.Deserialize<SomeJustSayingCommand>(message.Body.Bytes, JsonSerialisationOptions.Options);
        Assert.NotNull(obj);
        Assert.Equal(@event.Id, obj.Id);
        Assert.Equal(@event.Conversation, obj.Conversation);
        Assert.Equal(@event.RaisingComponent, obj.RaisingComponent);
        Assert.Equal(@event.Tenant, obj.Tenant);
        Assert.Equal(@event.Version, obj.Version);
        Assert.Equal(@event.TimeStamp, obj.TimeStamp);
        Assert.Equal(@event.Name, obj.Name);
        Assert.Equal(MessageType.MT_EVENT, message.Header.MessageType);
    }
    
    [Fact]
    public void MapToMessage_when_mapping_a_command_property_should_not_be_override()
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
        
        var message = mapper.MapToMessage(command, new Publication());

        var obj = JsonSerializer.Deserialize<WithJustSayingProperty>(message.Body.Bytes, JsonSerialisationOptions.Options);
        Assert.NotNull(obj);
        Assert.Equal(command.Id, obj.Id);
        Assert.Equal(command.Conversation, obj.Conversation);
        Assert.Equal(command.RaisingComponent, obj.RaisingComponent);
        Assert.Equal(command.Tenant, obj.Tenant);
        Assert.Equal(command.Version, obj.Version);
        Assert.Equal(command.TimeStamp, obj.TimeStamp);
        Assert.Equal(command.Name, obj.Name);
        Assert.Equal(MessageType.MT_COMMAND, message.Header.MessageType);
    }
    
    [Fact]
    public void MapToMessage_when_mapping_a_justsaying_command_property_should_set_from_request_context()
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
        
        var message = mapper.MapToMessage(command, new Publication());
        Assert.Equal(subject, message.Header.Subject);

        var obj = JsonSerializer.Deserialize<SomeJustSayingCommand>(message.Body.Bytes, JsonSerialisationOptions.Options);
        Assert.NotNull(obj);
        Assert.False(Id.IsNullOrEmpty(obj.Id));
        Assert.NotNull(obj.Conversation);
        Assert.NotEmpty(obj.Conversation);
        Assert.NotEqual(DateTimeOffset.MinValue, obj.TimeStamp);
        
        Assert.Equal(raisingComponent, obj.RaisingComponent);
        Assert.Equal(tenant, obj.Tenant);
        Assert.Equal(version, obj.Version);
        Assert.Equal(command.Name, obj.Name);
        Assert.Equal(MessageType.MT_COMMAND, message.Header.MessageType);
    }
    
    [Fact]
    public void MapToMessage_when_mapping_a_request_command_property_should_set_from_request_context()
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
        
        var message = mapper.MapToMessage(command, new Publication());
        Assert.Equal(subject, message.Header.Subject);

        var obj = JsonSerializer.Deserialize<WithJustSayingProperty>(message.Body.Bytes, JsonSerialisationOptions.Options);
        Assert.NotNull(obj);
        Assert.False(Id.IsNullOrEmpty(obj.Id));
        Assert.NotNull(obj.Conversation);
        Assert.NotEmpty(obj.Conversation);
        Assert.NotEqual(DateTimeOffset.MinValue, obj.TimeStamp);
        
        Assert.Equal(raisingComponent, obj.RaisingComponent);
        Assert.Equal(tenant, obj.Tenant);
        Assert.Equal(version, obj.Version);
        Assert.Equal(command.Name, obj.Name);
        Assert.Equal(MessageType.MT_COMMAND, message.Header.MessageType);
    }
    
    [Fact]
    public void MapToMessage_when_mapping_a_command_with_partial_justsaying_property_should_set_from_request_context()
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
        
        var message = mapper.MapToMessage(command, new Publication());
        Assert.Equal(subject, message.Header.Subject);

        var obj = JsonSerializer.Deserialize<WithPartialJustSayingProperty>(message.Body.Bytes, JsonSerialisationOptions.Options);
        Assert.NotNull(obj);
        Assert.False(Id.IsNullOrEmpty(obj.Id));
        Assert.NotEqual(DateTimeOffset.MinValue, obj.TimeStamp);
        Assert.Equal(tenant, obj.Tenant);
        Assert.Equal(command.Name, obj.Name);
        Assert.Equal(MessageType.MT_COMMAND, message.Header.MessageType);
        
        
        var doc = JsonNode.Parse(message.Body.Bytes, new JsonNodeOptions{ PropertyNameCaseInsensitive = true});
        Assert.NotNull(doc);

        var nodeConversation = doc[nameof(IJustSayingRequest.Conversation)]?.GetValue<string>();
        Assert.NotNull(nodeConversation);
        Assert.NotEmpty(nodeConversation);
        
        var nodeRaisingComponent = doc[nameof(IJustSayingRequest.RaisingComponent)]?.GetValue<string>();
        Assert.Equal(raisingComponent, nodeRaisingComponent);
        
        var nodeVersion = doc[nameof(IJustSayingRequest.Version)]?.GetValue<string>();
        Assert.Equal(version, nodeVersion);
    }
    
    [Fact]
    public void MapToMessage_when_mapping_a_command_with_non_justsaying_property_should_set_from_request_context()
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
        
        var message = mapper.MapToMessage(command, new Publication());
        Assert.Equal(subject, message.Header.Subject);

        var obj = JsonSerializer.Deserialize<NonJustSayingProperty>(message.Body.Bytes, JsonSerialisationOptions.Options);
        Assert.NotNull(obj);
        Assert.False(Id.IsNullOrEmpty(obj.Id));
        Assert.Equal(command.Name, obj.Name);
        Assert.Equal(MessageType.MT_COMMAND, message.Header.MessageType);
        
        var doc = JsonNode.Parse(message.Body.Bytes, new JsonNodeOptions{ PropertyNameCaseInsensitive = true});
        Assert.NotNull(doc);
        
        var nodeTimeStamp = doc[nameof(IJustSayingRequest.TimeStamp)]?.GetValue<string>();
        Assert.NotNull(nodeTimeStamp);
        Assert.NotEmpty(nodeTimeStamp);

        var nodeConversation = doc[nameof(IJustSayingRequest.Conversation)]?.GetValue<string>();
        Assert.NotNull(nodeConversation);
        Assert.NotEmpty(nodeConversation);
        
        var nodeTenant = doc[nameof(IJustSayingRequest.Tenant)]?.GetValue<string>();
        Assert.Equal(tenant, nodeTenant);
        
        var nodeRaisingComponent = doc[nameof(IJustSayingRequest.RaisingComponent)]?.GetValue<string>();
        Assert.Equal(raisingComponent, nodeRaisingComponent);
        
        var nodeVersion = doc[nameof(IJustSayingRequest.Version)]?.GetValue<string>();
        Assert.Equal(version, nodeVersion);
    }
    
    [Fact]
    public void MapToMessage_when_mapping_a_request_command_property_should_set_from_publication()
    {
        var mapper = new JustSayingMessageMapper<WithJustSayingProperty>();
        var command = new WithJustSayingProperty
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"Name{Guid.NewGuid().ToString()}",
        };

        var subject = $"Subject{Guid.NewGuid().ToString()}";
        var source = new Uri($"Raising{Guid.NewGuid().ToString()}", UriKind.Relative);

        var message = mapper.MapToMessage(command, new Publication
        {
            Subject = subject,
            Source = source
        });
        
        Assert.Equal(subject, message.Header.Subject);

        var obj = JsonSerializer.Deserialize<WithJustSayingProperty>(message.Body.Bytes, JsonSerialisationOptions.Options);
        Assert.NotNull(obj);
        Assert.False(Id.IsNullOrEmpty(obj.Id));
        Assert.NotNull(obj.Conversation);
        Assert.NotEmpty(obj.Conversation);
        Assert.NotEqual(DateTimeOffset.MinValue, obj.TimeStamp);
        
        Assert.Equal(source.ToString(), obj.RaisingComponent);
        Assert.Equal(command.Name, obj.Name);
        Assert.Equal(MessageType.MT_COMMAND, message.Header.MessageType);
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
