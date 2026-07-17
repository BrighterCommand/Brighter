using System;
using System.Collections.Generic;
using System.Text.Json;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Transformers.MassTransit;

namespace Paramore.Brighter.Transforms.Adaptors.Tests.MassTransit;

public class MassTransitTransformTest
{
    private readonly MassTransitTransform _transform = new();

    [Test]
    public async Task wrap_should_use_default_value()
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
        await Assert.That(envelop).IsNotNull();
        await Assert.That(envelop.ConversationId is null).IsTrue();
        await Assert.That(envelop.DestinationAddress).IsNull();
        await Assert.That(envelop.FaultAddress).IsNull();
        await Assert.That(envelop.MessageType).IsNull();
        await Assert.That(envelop.RequestId is null).IsTrue();
        await Assert.That(envelop.ResponseAddress).IsNull();
        await Assert.That(envelop.SourceAddress).IsNull();
        await Assert.That(envelop.ExpirationTime).IsNull();
        await Assert.That(envelop.MessageId?.Value).IsEqualTo(message.Header.MessageId.Value);
        await Assert.That(envelop.CorrelationId?.Value).IsEqualTo(message.Header.CorrelationId.Value);
        await Assert.That(envelop.SentTime).IsEqualTo(message.Header.TimeStamp.DateTime);
    }
    
    [Test]
    public async Task wrap_should_use_from_request_context()
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
        await Assert.That(envelop).IsNotNull();
        await Assert.That(envelop.ConversationId?.Value).IsEqualTo(conversationId);
        await Assert.That(envelop.DestinationAddress?.ToString()).IsEqualTo(destinationAddress);
        await Assert.That(envelop.FaultAddress?.ToString()).IsEqualTo(faultAddress);
        await Assert.That(envelop.MessageType).IsEquivalentTo(messageType);
        await Assert.That(envelop.RequestId?.Value).IsEqualTo(requestId);
        await Assert.That(envelop.ResponseAddress?.ToString()).IsEqualTo(responseAddress);
        await Assert.That(envelop.SourceAddress?.ToString()).IsEqualTo(sourceAddress);
        await Assert.That(envelop.ExpirationTime).IsEqualTo(expirationTime);
        await Assert.That(envelop.MessageId).IsEqualTo(message.Header.MessageId);
        await Assert.That(envelop.CorrelationId).IsEqualTo(message.Header.CorrelationId);
        await Assert.That(envelop.SentTime).IsEqualTo(message.Header.TimeStamp.DateTime);
    }
    
    [Test]
    public async Task wrap_should_use_from_transform()
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
        await Assert.That(envelop).IsNotNull();
        await Assert.That(envelop.ConversationId?.Value).IsEqualTo(conversationId);
        await Assert.That(envelop.DestinationAddress?.ToString()).IsEqualTo(destinationAddress);
        await Assert.That(envelop.FaultAddress?.ToString()).IsEqualTo(faultAddress);
        await Assert.That(envelop.MessageType).IsEquivalentTo(messageType);
        await Assert.That(envelop.RequestId?.Value).IsEqualTo(requestId);
        await Assert.That(envelop.ResponseAddress?.ToString()).IsEqualTo(responseAddress);
        await Assert.That(envelop.SourceAddress?.ToString()).IsEqualTo(sourceAddress);
        await Assert.That(envelop.ExpirationTime).IsEqualTo(expirationTime);
        await Assert.That(envelop.MessageId).IsEqualTo(message.Header.MessageId);
        await Assert.That(envelop.CorrelationId).IsEqualTo(message.Header.CorrelationId);
        await Assert.That(envelop.SentTime).IsEqualTo(message.Header.TimeStamp.DateTime);
    }
    
    [Test]
    public async Task unwrap()
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
        await Assert.That(envelop).IsNotNull();
        await Assert.That(envelop.ConversationId?.Value).IsEqualTo(conversationId);
        await Assert.That(envelop.DestinationAddress?.ToString()).IsEqualTo(destinationAddress);
        await Assert.That(envelop.FaultAddress?.ToString()).IsEqualTo(faultAddress);
        await Assert.That(envelop.MessageType).IsEquivalentTo(messageType);
        await Assert.That(envelop.RequestId?.Value).IsEqualTo(requestId);
        await Assert.That(envelop.ResponseAddress?.ToString()).IsEqualTo(responseAddress);
        await Assert.That(envelop.SourceAddress?.ToString()).IsEqualTo(sourceAddress);
        await Assert.That(envelop.ExpirationTime).IsEqualTo(expirationTime);
        await Assert.That(envelop.MessageId).IsEqualTo(message.Header.MessageId);
        await Assert.That(envelop.CorrelationId).IsEqualTo(message.Header.CorrelationId);
        await Assert.That(envelop.SentTime).IsEqualTo(message.Header.TimeStamp.DateTime);
        await Assert.That(message.Header.Bag).HasSingleItem();
    }

    public class SomeEvent() : Event(Guid.NewGuid())
    {
        public string Name { get; set; } = string.Empty;
    }
}
