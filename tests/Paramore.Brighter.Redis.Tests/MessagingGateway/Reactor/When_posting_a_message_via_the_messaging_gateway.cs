using System;
using System.Linq;
using System.Net.Mime;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Redis.Tests.MessagingGateway.Reactor;

[Category("Redis")]
[ClassDataSource<RedisFixture>(Shared = SharedType.PerClass)]
    public class RedisMessageProducerSendTests 
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
        _topic = redisFixture.Topic;
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

    [Test]
    public async Task When_posting_a_message_via_the_messaging_gateway()
    {
        //Need to receive to subscribe to feed, before we send a message. This returns an empty message we discard
        await _redisFixture.MessageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000));

        await _redisFixture.MessageProducer.SendAsync(_message);
        var sentMessage = (await _redisFixture.MessageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000))).Single();
        await _redisFixture.MessageConsumer.AcknowledgeAsync(sentMessage);

        // Assert message body
        await Assert.That(sentMessage.Body.Value).IsEqualTo(_message.Body.Value);
        
        // Assert header properties
        await Assert.That(sentMessage.Header.MessageId).IsEqualTo(_messageId);
        await Assert.That(sentMessage.Header.Topic).IsEqualTo(_topic);
        await Assert.That(sentMessage.Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
        await Assert.That(sentMessage.Header.TimeStamp).IsEqualTo(_timestamp).Within(TimeSpan.FromSeconds(5));
        await Assert.That(sentMessage.Header.CorrelationId).IsEqualTo(_correlationId);
        await Assert.That(sentMessage.Header.ReplyTo).IsEqualTo(_replyTo);
        await Assert.That(sentMessage.Header.ContentType!.ToString()).IsEqualTo(MediaTypeNames.Application.Json);
        await Assert.That(sentMessage.Header.Bag["custom-header"]).IsEqualTo("custom-value");
        await Assert.That(sentMessage.Header.Source).IsEqualTo(_source);
        await Assert.That(sentMessage.Header.Type).IsEqualTo(_type);
        await Assert.That(sentMessage.Header.DataSchema).IsEqualTo(_dataSchema);
        await Assert.That(sentMessage.Header.Subject).IsEqualTo(_subject);
        await Assert.That(sentMessage.Header.SpecVersion).IsEqualTo(MessageHeader.DefaultSpecVersion);
        await Assert.That(sentMessage.Header.TraceParent).IsEqualTo(_traceParent);
        await Assert.That(sentMessage.Header.TraceState).IsEqualTo(_traceState);
        await Assert.That(sentMessage.Header.Baggage).IsEqualTo(_baggage);
    }
}

