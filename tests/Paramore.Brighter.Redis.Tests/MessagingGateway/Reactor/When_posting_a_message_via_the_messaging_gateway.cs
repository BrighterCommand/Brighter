using System;
using System.Linq;
using System.Net.Mime;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Redis.Tests.MessagingGateway.Reactor;

[Collection("Redis Shared Pool")]   //shared connection pool so run sequentially
[Trait("Fragile", "CI")]
[Trait("Category", "Redis")]
public class RedisMessageProducerSendTests : IClassFixture<RedisFixture>
{
    private readonly RedisFixture _redisFixture;
    private readonly Message _message;
    private readonly string _topic;
    private readonly string _messageId;
    private readonly DateTime _timestamp;
    private readonly string _correlationId;
    private readonly string _replyTo;
    private readonly Uri _source;
    private readonly CloudEventsType _type;
    private readonly Uri _dataSchema;
    private readonly string _subject;
    private readonly TraceParent _traceParent;
    private readonly TraceState _traceState;
    private readonly Baggage _baggage;
    private readonly string _dataRef;
    private readonly PartitionKey _partitionKey;

    public RedisMessageProducerSendTests(RedisFixture redisFixture)
    {
        _topic = "test";
        _messageId = Guid.NewGuid().ToString();
        _timestamp = DateTime.UtcNow;
        _correlationId = Guid.NewGuid().ToString();
        _replyTo = "reply-queue";
        _source = new Uri("http://testing.example.com");
        _type = new CloudEventsType("test.message.type");
        _dataSchema = new Uri("http://schema.example.com");
        _subject = "test-subject";
        _traceParent = new TraceParent("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01");
        _traceState = new TraceState("congo=t61rcWkgMzE");
        _baggage = new Baggage();
        _baggage.LoadBaggage("userId=alice");

        _redisFixture = redisFixture;
        
        var header = new MessageHeader(
            messageId: new Id(_messageId),
            topic: new RoutingKey(_topic),
            messageType: MessageType.MT_COMMAND,
            source: _source,
            type: _type,
            timeStamp: _timestamp,
            correlationId: new Id(_correlationId),
            replyTo: new RoutingKey(_replyTo),
            contentType: new ContentType(MediaTypeNames.Application.Json),
            dataSchema: _dataSchema,
            subject: _subject,
            traceParent: _traceParent,
            traceState: _traceState,
            baggage: _baggage);
            
        header.Bag.Add("custom-header", "custom-value");
        header.DataRef = _dataRef;
        
        _message = new Message(header, new MessageBody("test content"));
    }

    [Fact]
    public void When_posting_a_message_via_the_messaging_gateway()
    {
        _redisFixture.MessageProducer.Send(_message);
        var sentMessage = _redisFixture.MessageConsumer.Receive(TimeSpan.FromMilliseconds(1000)).Single();
        _redisFixture.MessageConsumer.Acknowledge(sentMessage);

        // Assert message body
        Assert.Equal(_message.Body.Value, sentMessage.Body.Value);
        
        // Assert header properties
        Assert.Equal(_messageId, sentMessage.Header.MessageId);
        Assert.Equal(_topic, sentMessage.Header.Topic);
        Assert.Equal(MessageType.MT_COMMAND, sentMessage.Header.MessageType);
        Assert.Equal(_timestamp, sentMessage.Header.TimeStamp, TimeSpan.FromSeconds(5));
        Assert.Equal(_correlationId, sentMessage.Header.CorrelationId);
        Assert.Equal(_replyTo, sentMessage.Header.ReplyTo);
        Assert.Equal(MediaTypeNames.Application.Json, sentMessage.Header.ContentType!.ToString());
        Assert.Equal("custom-value", sentMessage.Header.Bag["custom-header"]);
        Assert.Equal(_source, sentMessage.Header.Source);
        Assert.Equal(_type, sentMessage.Header.Type);
        Assert.Equal(_dataSchema, sentMessage.Header.DataSchema);
        Assert.Equal(_subject, sentMessage.Header.Subject);
        Assert.Equal(MessageHeader.DefaultSpecVersion, sentMessage.Header.SpecVersion);
        Assert.Equal(_traceParent, sentMessage.Header.TraceParent);
        Assert.Equal(_traceState, sentMessage.Header.TraceState);
        Assert.Equal(_baggage, sentMessage.Header.Baggage);
    }
}
