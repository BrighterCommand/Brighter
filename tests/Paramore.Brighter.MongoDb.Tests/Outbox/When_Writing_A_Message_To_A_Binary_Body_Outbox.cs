using System;
using System.Net.Mime;
using Paramore.Brighter.Outbox.MongoDb;
using Xunit;

namespace Paramore.Brighter.MongoDb.Tests.Outbox;

[Trait("Category", "MongoDb")]
public class MongoDbBinaryOutboxWritingMessageTests : IDisposable
{
    private readonly string _collection;
    private const string Key1 = "name1";
    private const string Key2 = "name2";
    private const string Key3 = "name3";
    private const string Key4 = "name4";
    private const string Key5 = "name5";
    private readonly Message _messageEarliest;
    private readonly MongoDbOutbox _outbox;
    private const string Value1 = "value1";
    private const string Value2 = "value2";
    private const int Value3 = 123;
    private readonly Guid _value4 = Guid.NewGuid();
    private readonly DateTime _value5 = DateTime.UtcNow;
    private readonly RequestContext _context;

    public MongoDbBinaryOutboxWritingMessageTests()
    {
        _collection = $"outbox-{Guid.NewGuid():N}";
        _outbox = new(Configuration.CreateOutbox(_collection));
        var messageHeader = new MessageHeader(
            messageId: Guid.NewGuid().ToString(),
            topic: new RoutingKey("test_topic"),
            messageType: MessageType.MT_DOCUMENT,
            timeStamp: DateTime.UtcNow.AddDays(-1),
            handledCount: 5,
            delayed: TimeSpan.FromMilliseconds(5),
            correlationId: Guid.NewGuid().ToString(),
            replyTo: new RoutingKey("ReplyTo"),
            contentType: new ContentType(MediaTypeNames.Text.Plain),
            partitionKey: Guid.NewGuid().ToString());
        messageHeader.Bag.Add(Key1, Value1);
        messageHeader.Bag.Add(Key2, Value2);
        messageHeader.Bag.Add(Key3, Value3);
        messageHeader.Bag.Add(Key4, _value4);
        messageHeader.Bag.Add(Key5, _value5);

        _context = new RequestContext();

        _messageEarliest = new Message(messageHeader, new MessageBody("message body"));
        _outbox.Add(_messageEarliest, _context);
    }

    [Fact]
    public void When_Writing_A_Message_To_A_Binary_Body_Outbox()
    {
        var storedMessage = _outbox.Get(_messageEarliest.Id, _context);

        //should read the message from the sql outbox
        Assert.Equal(_messageEarliest.Body.Value, storedMessage.Body.Value);
        //should read the header from the sql outbox
        Assert.Equal(_messageEarliest.Header.Topic, storedMessage.Header.Topic);
        Assert.Equal(_messageEarliest.Header.MessageType, storedMessage.Header.MessageType);
        Assert.Equal(_messageEarliest.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss.fZ"), storedMessage.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss.fZ"));
        Assert.Equal(0, storedMessage.Header.HandledCount); // -- should be zero when read from outbox
        Assert.Equal(TimeSpan.Zero, storedMessage.Header.Delayed); // -- should be zero when read from outbox
        Assert.Equal(_messageEarliest.Header.CorrelationId, storedMessage.Header.CorrelationId);
        Assert.Equal(_messageEarliest.Header.ReplyTo, storedMessage.Header.ReplyTo);
        Assert.Equal(_messageEarliest.Header.ContentType, storedMessage.Header.ContentType);
        Assert.Equal(_messageEarliest.Header.PartitionKey, storedMessage.Header.PartitionKey);

        //Bag serialization
        Assert.True(storedMessage.Header.Bag.ContainsKey(Key1));
        Assert.Equal(Value1, storedMessage.Header.Bag[Key1]);
        Assert.True(storedMessage.Header.Bag.ContainsKey(Key2));
        Assert.Equal(Value2, storedMessage.Header.Bag[Key2]);
        Assert.True(storedMessage.Header.Bag.ContainsKey(Key3));
        Assert.Equal(Value3, storedMessage.Header.Bag[Key3]);
        Assert.True(storedMessage.Header.Bag.ContainsKey(Key4));
        Assert.Equal(_value4, storedMessage.Header.Bag[Key4]);
        Assert.True(storedMessage.Header.Bag.ContainsKey(Key5));
        Assert.Equal(_value5, storedMessage.Header.Bag[Key5]);
    }

    public void Dispose()
    {
        Configuration.Cleanup(_collection);
    }
}
