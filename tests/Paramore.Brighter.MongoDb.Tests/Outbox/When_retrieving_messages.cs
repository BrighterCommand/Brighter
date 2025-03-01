using System;
using System.Linq;
using Paramore.Brighter.Outbox.MongoDb;
using Xunit;

namespace Paramore.Brighter.MongoDb.Tests.Outbox;

[Trait("Category", "MongoDb")]
public class MongoDbFetchMessageTests : IDisposable
{
    private readonly string _collection;
    private readonly Message _messageEarliest;
    private readonly Message _messageDispatched;
    private readonly Message _messageUnDispatched;
    private readonly MongoDbOutbox _outbox;

    public MongoDbFetchMessageTests()
    {
        _collection = $"outbox-{Guid.NewGuid():N}";
        _outbox = new MongoDbOutbox(Configuration.Create(_collection));
        var routingKey = new RoutingKey("test_topic");

        _messageEarliest = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_DOCUMENT),
            new MessageBody("message body"));
        _messageDispatched = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_DOCUMENT),
            new MessageBody("message body"));
        _messageUnDispatched = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_DOCUMENT),
            new MessageBody("message body"));
    }

    [Fact]
    public void When_Retrieving_Messages()
    {
        var context = new RequestContext();
        _outbox.Add([_messageEarliest, _messageDispatched, _messageUnDispatched], context);
        _outbox.MarkDispatched(_messageEarliest.Id, context, DateTime.UtcNow.AddHours(-3));
        _outbox.MarkDispatched(_messageDispatched.Id, context);

        var messages = _outbox.Get();

        //Assert
        Assert.Equal(3, (messages)?.Count());
    }

    [Fact]
    public void When_Retrieving_Messages_By_Id()
    {
        var context = new RequestContext();
        _outbox.Add([_messageEarliest, _messageDispatched, _messageUnDispatched], context);
        _outbox.MarkDispatched(_messageEarliest.Id, context, DateTime.UtcNow.AddHours(-3));
        _outbox.MarkDispatched(_messageDispatched.Id, context);

        var messages = _outbox.Get(
            [_messageEarliest.Id, _messageUnDispatched.Id],
            context);

        //Assert
        messages = messages.ToList();
        Assert.Equal(2, (messages)?.Count());
        Assert.Contains(x => x.Id == _messageEarliest.Id, messages);
        Assert.Contains(x => x.Id == _messageUnDispatched.Id, messages);
        Assert.DoesNotContain(x => x.Id == _messageDispatched.Id, messages);
    }

    [Fact]
    public void When_Retrieving_Message_By_Id()
    {
        var context = new RequestContext();
        _outbox.Add([_messageEarliest, _messageDispatched, _messageUnDispatched], context);
        _outbox.MarkDispatched(_messageEarliest.Id, context, DateTime.UtcNow.AddHours(-3));
        _outbox.MarkDispatched(_messageDispatched.Id, context);

        var messages = _outbox.Get(_messageDispatched.Id, context);

        //Assert
        Assert.Equal(_messageDispatched.Id, messages.Id);
    }

    public void Dispose()
    {
        Configuration.Cleanup(_collection);
    }
}
