using System;
using System.Net.Mime;
using Paramore.Brighter.Outbox.Spanner;

namespace Paramore.Brighter.Gcp.Tests.Spanner.Outbox;

[Trait("Category", "Spanner")]
public class SqlBinaryOutboxWritingMessageTests : IDisposable
{
    private const string Key1 = "name1";
    private const string Key2 = "name2";
    private const string Key3 = "name3";
    private const string Key4 = "name4";
    private const string Key5 = "name5";
    private readonly Message _messageEarliest;
    private readonly SpannerOutbox _sqlOutbox;
    private Message? _storedMessage;
    private const string Value1 = "value1";
    private const string Value2 = "value2";
    private const int Value3 = 123;
    private readonly Guid _value4 = Guid.NewGuid();
    private readonly DateTime _value5 = DateTime.UtcNow;
    private readonly SpannerTestHelper _spannerTestHelper;
    private readonly RequestContext _context;

    public SqlBinaryOutboxWritingMessageTests()
    {
        _spannerTestHelper = new SpannerTestHelper(binaryMessagePayload: true);
        _spannerTestHelper.SetupMessageDb();

        _sqlOutbox = new(_spannerTestHelper.Configuration);
        var messageHeader = new MessageHeader(
            messageId: Id.Random(), 
            topic: new RoutingKey("test_topic"),
            messageType: MessageType.MT_DOCUMENT,
            timeStamp: DateTime.UtcNow.AddDays(-1),
            handledCount: 5,
            delayed: TimeSpan.FromMilliseconds(5),
            correlationId: Id.Random(),
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
        _sqlOutbox.Add(_messageEarliest, _context);
    }

    public void When_Writing_A_Message_To_A_Binary_Body_Outbox()
    {
        _storedMessage = _sqlOutbox.Get(_messageEarliest.Id, _context);

        //should read the message from the sql outbox
        Assert.Equal(_messageEarliest.Body.Value, _storedMessage.Body.Value);
        //should read the header from the sql outbox
        Assert.Equal(_messageEarliest.Header.Topic, _storedMessage.Header.Topic);
        Assert.Equal(_messageEarliest.Header.MessageType, _storedMessage.Header.MessageType);
        Assert.Equal(_messageEarliest.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss.fZ"), _storedMessage.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss.fZ"));
        Assert.Equal(0, _storedMessage.Header.HandledCount); // -- should be zero when read from outbox
        Assert.Equal(TimeSpan.Zero, _storedMessage.Header.Delayed); // -- should be zero when read from outbox
        Assert.Equal(_messageEarliest.Header.CorrelationId, _storedMessage.Header.CorrelationId);
        Assert.Equal(_messageEarliest.Header.ReplyTo, _storedMessage.Header.ReplyTo);
        Assert.Equal(_messageEarliest.Header.ContentType, _storedMessage.Header.ContentType);
        Assert.Equal(_messageEarliest.Header.PartitionKey, _storedMessage.Header.PartitionKey);

        //Bag serialization
        Assert.True(_storedMessage.Header.Bag.ContainsKey(Key1));
        Assert.Equal(Value1, _storedMessage.Header.Bag[Key1]);
        Assert.True(_storedMessage.Header.Bag.ContainsKey(Key2));
        Assert.Equal(Value2, _storedMessage.Header.Bag[Key2]);
        Assert.True(_storedMessage.Header.Bag.ContainsKey(Key3));
        Assert.Equal(Value3, _storedMessage.Header.Bag[Key3]);
        Assert.True(_storedMessage.Header.Bag.ContainsKey(Key4));
        Assert.Equal(_value4, _storedMessage.Header.Bag[Key4]);
        Assert.True(_storedMessage.Header.Bag.ContainsKey(Key5));
        Assert.Equal(_value5, _storedMessage.Header.Bag[Key5]);
    }

    public void Dispose()
    {
        _spannerTestHelper.CleanUpDb();
    }
}
