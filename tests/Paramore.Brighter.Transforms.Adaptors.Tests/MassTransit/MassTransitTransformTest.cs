using System;
using System.Collections.Generic;
using System.Text.Json;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Transformers.MassTransit;
using Xunit;

namespace Paramore.Brighter.Transforms.Adaptors.Tests.MassTransit;

public class MassTransitTransformTest
{
    private readonly MassTransitTransform _transform = new();

    [Fact]
    public void wrap_should_use_default_value()
    {
        var message = new Message(
            new MessageHeader
            {
                MessageId = Id.Random(), 
                CorrelationId = Id.Random(), 
                TimeStamp = DateTimeOffset.UtcNow
            },
            new MessageBody("{\"test\": \"234\"}"));
        
        var wrap = _transform.Wrap(message, new Publication());

        var envelop =
            JsonSerializer.Deserialize<MassTransitMessageEnvelop<JsonElement>>(wrap.Body.Bytes,
                JsonSerialisationOptions.Options);
        Assert.NotNull(envelop);
        Assert.Null(envelop.ConversationId);
        Assert.Null(envelop.DestinationAddress);
        Assert.Null(envelop.FaultAddress);
        Assert.Null(envelop.MessageType);
        Assert.Null(envelop.RequestId);
        Assert.Null(envelop.ResponseAddress);
        Assert.Null(envelop.SourceAddress);
        Assert.Null(envelop.ExpirationTime);
        Assert.Equal(message.Header.MessageId, envelop.MessageId);
        Assert.Equal(message.Header.CorrelationId, envelop.CorrelationId);
        Assert.Equal(message.Header.TimeStamp.DateTime, envelop.SentTime);
    }
    
    [Fact]
    public void wrap_should_use_from_request_context()
    {
        var message = new Message(
            new MessageHeader
            {
                MessageId = Id.Random(), 
                CorrelationId = Id.Random(), 
                TimeStamp = DateTimeOffset.UtcNow
            },
            new MessageBody("{\"test\": \"234\"}"));

        var conversationId = Guid.NewGuid().ToString();
        var destinationAddress = Guid.NewGuid().ToString();
        var expirationTime = DateTimeOffset.UtcNow.AddHours(1).DateTime;
        var faultAddress = Guid.NewGuid().ToString();
        var initiatorId = Guid.NewGuid().ToString();
        var messageType = new[]{Guid.NewGuid().ToString()};
        var requestId = Guid.NewGuid().ToString();
        var responseAddress = Guid.NewGuid().ToString();
        var sourceAddress = Guid.NewGuid().ToString();

        _transform.Context = new RequestContext
        {
            Bag =
            {
                [MassTransitHeaderNames.ConversationId] = conversationId,
                [MassTransitHeaderNames.DestinationAddress] = destinationAddress,
                [MassTransitHeaderNames.ExpirationTime] = expirationTime,
                [MassTransitHeaderNames.FaultAddress] = faultAddress,
                [MassTransitHeaderNames.InitiatorId] = initiatorId,
                [MassTransitHeaderNames.MessageType] = messageType,
                [MassTransitHeaderNames.RequestId] = requestId,
                [MassTransitHeaderNames.ResponseAddress] = responseAddress,
                [MassTransitHeaderNames.SourceAddress] = sourceAddress
            }
        };
        
        var wrap = _transform.Wrap(message, new Publication());

        var envelop =
            JsonSerializer.Deserialize<MassTransitMessageEnvelop<JsonElement>>(wrap.Body.Bytes,
                JsonSerialisationOptions.Options);
        Assert.NotNull(envelop);
        Assert.Equal(conversationId, envelop.ConversationId?.Value);
        Assert.Equal(destinationAddress, envelop.DestinationAddress?.ToString());
        Assert.Equal(faultAddress, envelop.FaultAddress?.ToString());
        Assert.Equal(messageType, envelop.MessageType);
        Assert.Equal(requestId, envelop.RequestId?.Value);
        Assert.Equal(responseAddress, envelop.ResponseAddress?.ToString());
        Assert.Equal(sourceAddress, envelop.SourceAddress?.ToString());
        Assert.Equal(expirationTime, envelop.ExpirationTime);
        Assert.Equal(message.Header.MessageId, envelop.MessageId);
        Assert.Equal(message.Header.CorrelationId, envelop.CorrelationId);
        Assert.Equal(message.Header.TimeStamp.DateTime, envelop.SentTime);
    }
    
    [Fact]
    public void wrap_should_use_from_transform()
    {
        var message = new Message(
            new MessageHeader
            {
                MessageId = Id.Random(), 
                CorrelationId = Id.Random(), 
                TimeStamp = DateTimeOffset.UtcNow
            },
            new MessageBody("{\"test\": \"234\"}"));

        var conversationId = Guid.NewGuid().ToString();
        var destinationAddress = Guid.NewGuid().ToString();
        var expirationTime = DateTimeOffset.UtcNow.AddHours(1).DateTime;
        var faultAddress = Guid.NewGuid().ToString();
        var initiatorId = Guid.NewGuid().ToString();
        var messageType = new[]{Guid.NewGuid().ToString()};
        var requestId = Guid.NewGuid().ToString();
        var responseAddress = Guid.NewGuid().ToString();
        var sourceAddress = Guid.NewGuid().ToString();

        _transform.Context = new RequestContext
        {
            Bag =
            {
                [MassTransitHeaderNames.ConversationId] = conversationId,
                [MassTransitHeaderNames.ExpirationTime] = expirationTime,
                [MassTransitHeaderNames.InitiatorId] = initiatorId,
                [MassTransitHeaderNames.RequestId] = requestId
            }
        };
        
        _transform.InitializeWrapFromAttributeParams(destinationAddress, faultAddress, responseAddress, sourceAddress, messageType);
        
        var wrap = _transform.Wrap(message, new Publication());

        var envelop =
            JsonSerializer.Deserialize<MassTransitMessageEnvelop<JsonElement>>(wrap.Body.Bytes,
                JsonSerialisationOptions.Options);
        Assert.NotNull(envelop);
        Assert.Equal(conversationId, envelop.ConversationId?.Value);
        Assert.Equal(destinationAddress, envelop.DestinationAddress?.ToString());
        Assert.Equal(faultAddress, envelop.FaultAddress?.ToString());
        Assert.Equal(messageType, envelop.MessageType);
        Assert.Equal(requestId, envelop.RequestId?.Value);
        Assert.Equal(responseAddress, envelop.ResponseAddress?.ToString());
        Assert.Equal(sourceAddress, envelop.SourceAddress?.ToString());
        Assert.Equal(expirationTime, envelop.ExpirationTime);
        Assert.Equal(message.Header.MessageId, envelop.MessageId);
        Assert.Equal(message.Header.CorrelationId, envelop.CorrelationId);
        Assert.Equal(message.Header.TimeStamp.DateTime, envelop.SentTime);
    }
    
    [Fact]
    public void unwrap()
    {
        var message = new Message(
            new MessageHeader
            {
                MessageId = Id.Random(), 
                CorrelationId = Id.Random(), 
                TimeStamp = DateTimeOffset.UtcNow,
                Bag =  new Dictionary<string, object>
                {
                    ["test-header"] = "some-header"
                }
            },
            new MessageBody("{\"test\": \"234\"}"));

        var conversationId = Guid.NewGuid().ToString();
        var destinationAddress = Guid.NewGuid().ToString();
        var expirationTime = DateTimeOffset.UtcNow.AddHours(1).DateTime;
        var faultAddress = Guid.NewGuid().ToString();
        var initiatorId = Guid.NewGuid().ToString();
        var messageType = new[]{Guid.NewGuid().ToString()};
        var requestId = Guid.NewGuid().ToString();
        var responseAddress = Guid.NewGuid().ToString();
        var sourceAddress = Guid.NewGuid().ToString();

        _transform.Context = new RequestContext
        {
            Bag =
            {
                [MassTransitHeaderNames.ConversationId] = conversationId,
                [MassTransitHeaderNames.ExpirationTime] = expirationTime,
                [MassTransitHeaderNames.InitiatorId] = initiatorId,
                [MassTransitHeaderNames.RequestId] = requestId
            }
        };
        
        _transform.InitializeWrapFromAttributeParams(destinationAddress, faultAddress, responseAddress, sourceAddress, messageType);
        
        var wrap = _transform.Wrap(message, new Publication());

        var envelop =
            JsonSerializer.Deserialize<MassTransitMessageEnvelop<JsonElement>>(wrap.Body.Bytes,
                JsonSerialisationOptions.Options);
        Assert.NotNull(envelop);
        Assert.Equal(conversationId, envelop.ConversationId?.Value);
        Assert.Equal(destinationAddress, envelop.DestinationAddress?.ToString());
        Assert.Equal(faultAddress, envelop.FaultAddress?.ToString());
        Assert.Equal(messageType, envelop.MessageType);
        Assert.Equal(requestId, envelop.RequestId?.Value);
        Assert.Equal(responseAddress, envelop.ResponseAddress?.ToString());
        Assert.Equal(sourceAddress, envelop.SourceAddress?.ToString());
        Assert.Equal(expirationTime, envelop.ExpirationTime);
        Assert.Equal(message.Header.MessageId, envelop.MessageId);
        Assert.Equal(message.Header.CorrelationId, envelop.CorrelationId);
        Assert.Equal(message.Header.TimeStamp.DateTime, envelop.SentTime);
        Assert.Single(message.Header.Bag);
    }

    public class SomeEvent() : Event(Guid.NewGuid())
    {
        public string Name { get; set; } = string.Empty;
    }
}
