using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Text.Json;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.DynamoDB.V4;
using Xunit;

namespace Paramore.Brighter.DynamoDB.V4.Tests.Outbox;

public class MessageItemTests
{
    private readonly MessageHeader _header;
    private readonly MessageBody _body;
    private readonly Message _message;
    private readonly MessageItem _messageItem;

    // Test data for all MessageHeader properties
    private readonly Guid _id = Guid.NewGuid();
    private readonly string _topic = "TestTopic";
    private readonly MessageType _messageType = MessageType.MT_COMMAND;
    private readonly DateTimeOffset _timestamp = DateTimeOffset.UtcNow;
    private readonly string _correlationId = "TestCorrelationId";
    private readonly string _replyTo = "TestReplyTo";
    private readonly string _contentType = "application/json";
    private readonly int _handledCount = 5;
    private readonly int _delayedMilliseconds = 10;
    private readonly string _bagKey = "TestBagKey";
    private readonly string _bagValue = "TestBagValue";
    private readonly string _type = "test.type";
    private readonly string _subject = "test.subject";
    private readonly string _specVersion = "1.0";
    private readonly string _source = "http://testsource";
    private readonly string _dataSchema = "http://schema";
    private readonly string _dataRef = "http://dataref";
    private readonly string _partitionKey = "partition-1";
    private readonly string _traceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00";
    private readonly string _traceState = "congo=t61rcWkgMzE";
    private readonly string _baggageKey = "baggage-key";
    private readonly string _baggageValue = "baggage-value";

    public MessageItemTests()
    {
        var bag = new Dictionary<string, object> { { _bagKey, _bagValue } };
        var baggage = new Baggage { { _baggageKey, _baggageValue } };
        _header = new MessageHeader(
            new Id(_id.ToString()),
            new RoutingKey(_topic),
            _messageType,
            source: new Uri(_source),
            type: new CloudEventsType(_type),
            timeStamp: _timestamp,
            correlationId: new Id(_correlationId),
            replyTo: new RoutingKey(_replyTo),
            contentType: new ContentType(_contentType),
            partitionKey: new PartitionKey(_partitionKey),
            dataSchema: new Uri(_dataSchema),
            subject: _subject,
            handledCount: _handledCount,
            delayed: TimeSpan.FromMilliseconds(_delayedMilliseconds),
            traceParent: new TraceParent(_traceParent),
            traceState: new TraceState(_traceState),
            baggage: baggage
        );
        foreach (var kv in bag)
            _header.Bag.Add(kv.Key, kv.Value);

        _header.DataRef = _dataRef;

        _body = new MessageBody("Test body");
        _message = new Message(_header, _body);
        _messageItem = new MessageItem(_message);
    }

    [Fact]
    public void When_saving_a_message_item()
    {
        Assert.Equal(_header.MessageId.Value, _messageItem.MessageId);
        Assert.Equal(_header.Topic.ToString(), _messageItem.Topic);
        Assert.Equal(_header.MessageType.ToString(), _messageItem.MessageType);
        Assert.Equal(0, _messageItem.HandledCount); //set to zero from outbox
        Assert.Equal(0, _messageItem.DelayedMilliseconds); //set to zero from outbox
        Assert.Equal(_header.CorrelationId.Value, _messageItem.CorrelationId);
        Assert.Equal(_header.ReplyTo?.ToString(), _messageItem.ReplyTo);
        Assert.Equal(_header.ContentType?.MediaType, _messageItem.ContentType);
        Assert.Equal(JsonSerializer.Serialize(_header.Bag), _messageItem.HeaderBag);
        Assert.Equal(_body.Bytes, _messageItem.Body);
        Assert.Equal(_header.Type, _messageItem.Type);
        Assert.Equal(_header.Subject, _messageItem.Subject);
        Assert.Equal(_header.SpecVersion, _messageItem.SpecVersion);
        Assert.Equal(_header.Source.ToString(), _messageItem.Source);
        Assert.Equal(_header.DataSchema?.ToString(), _messageItem.DataSchema);
        Assert.Equal(_header.DataRef, _messageItem.DataRef);
        Assert.Equal(_header.PartitionKey.Value, _messageItem.PartitionKey);
        Assert.Equal(_header.TraceParent?.Value, _messageItem.TraceParent);
        Assert.Equal(_header.TraceState?.Value, _messageItem.TraceState);
        Assert.Equal(_header.Baggage.ToString(),  _messageItem.Baggage);
        Assert.Equal(_header.TimeStamp.Ticks, _messageItem.CreatedTime);
    }

    [Fact]
    public void When_reading_a_message_item()
    {
        var result = _messageItem.ConvertToMessage();
        Assert.Equal(_message.Header.MessageId, result.Header.MessageId);
        Assert.Equal(_message.Header.Topic, result.Header.Topic);
        Assert.Equal(_message.Header.MessageType, result.Header.MessageType);
        Assert.Equal(0, result.Header.HandledCount); //zero handled count when read from outbox
        Assert.Equal(TimeSpan.Zero, result.Header.Delayed); //zero delayed when read from outbox
        Assert.Equal(_message.Header.CorrelationId, result.Header.CorrelationId);
        Assert.Equal(_message.Header.ReplyTo, result.Header.ReplyTo);
        Assert.Equal(_message.Header.ContentType?.MediaType, result.Header.ContentType?.MediaType);
        Assert.Equal(_message.Header.Bag, result.Header.Bag);
        Assert.Equal(_message.Body.Value, result.Body.Value);
        Assert.Equal(_message.Header.Type, result.Header.Type);
        Assert.Equal(_message.Header.Subject, result.Header.Subject);
        Assert.Equal(_message.Header.SpecVersion, result.Header.SpecVersion);
        Assert.Equal(_message.Header.Source.ToString(), result.Header.Source.ToString());
        Assert.Equal(_message.Header.DataSchema?.ToString(), result.Header.DataSchema?.ToString());
        Assert.Equal(_message.Header.DataRef, result.Header.DataRef);
        Assert.Equal(_message.Header.PartitionKey.Value, result.Header.PartitionKey.Value);
        Assert.Equal(_message.Header.TraceParent?.Value, result.Header.TraceParent?.Value);
        Assert.Equal(_message.Header.TraceState?.ToString(), result.Header.TraceState?.ToString());
        Assert.Equal(_message.Header.Baggage, result.Header.Baggage);
        Assert.Equal(_message.Header.TimeStamp, result.Header.TimeStamp);
    }
}
